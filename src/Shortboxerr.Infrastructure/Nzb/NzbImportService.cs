using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Handles completed NZB downloads and imports them into the library.
/// </summary>
public class NzbImportService : INzbImportService
{
    private readonly ShortboxerrDbContext _db;
    private readonly ISabnzbdClient _sabnzbdClient;
    private readonly IFilenameParser _filenameParser;
    private readonly IStagingService _stagingService;
    private readonly ISettingsService _settingsService;
    private readonly IArchiveExtractor _archiveExtractor;
    private readonly ILogger<NzbImportService> _logger;
    private readonly string _stagingFolder;
    private readonly string[] _comicExtensions = { ".cbz", ".cbr", ".pdf", ".epub" };
    private readonly string[] _archiveExtensions = { ".zip", ".rar", ".7z" };
    
    // Key for storing processed download IDs in settings
    private const string ProcessedDownloadsKey = "nzb_processed_downloads";
    
    public NzbImportService(
        ShortboxerrDbContext db,
        ISabnzbdClient sabnzbdClient,
        IFilenameParser filenameParser,
        IStagingService stagingService,
        ISettingsService settingsService,
        IArchiveExtractor archiveExtractor,
        IConfiguration configuration,
        ILogger<NzbImportService> logger)
    {
        _db = db;
        _sabnzbdClient = sabnzbdClient;
        _filenameParser = filenameParser;
        _stagingService = stagingService;
        _settingsService = settingsService;
        _archiveExtractor = archiveExtractor;
        _logger = logger;
        
        _stagingFolder = Environment.GetEnvironmentVariable("SHORTBOXERR_STAGING") 
            ?? configuration["MediaManagement:StagingFolder"] 
            ?? "/data/staging";
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<NzbCompletedDownload>> GetCompletedDownloadsAsync(CancellationToken cancellationToken = default)
    {
        var completedDownloads = new List<NzbCompletedDownload>();
        var processedIds = await GetProcessedDownloadIdsAsync(cancellationToken);
        
        try
        {
            // Get history from SABnzbd
            var history = await _sabnzbdClient.GetHistoryAsync(100, cancellationToken);
            
            foreach (var item in history)
            {
                // Skip already processed
                if (processedIds.Contains(item.Id))
                {
                    continue;
                }
                
                // Only process completed downloads
                if (item.State != NzbDownloadState.Completed)
                {
                    continue;
                }
                
                // Must have a download path
                if (string.IsNullOrEmpty(item.DownloadPath))
                {
                    _logger.LogWarning("Completed download {Name} has no download path, skipping", item.Name);
                    continue;
                }
                
                // Check if path exists
                if (!Directory.Exists(item.DownloadPath) && !File.Exists(item.DownloadPath))
                {
                    _logger.LogWarning("Download path does not exist: {Path}", item.DownloadPath);
                    continue;
                }
                
                completedDownloads.Add(new NzbCompletedDownload
                {
                    DownloadId = item.Id,
                    Name = item.Name,
                    DownloadPath = item.DownloadPath,
                    CompletedAt = item.CompletedAt ?? DateTime.UtcNow,
                    TotalBytes = item.TotalBytes,
                    Category = item.Category,
                    ClientName = "SABnzbd",
                    OriginalStatus = item
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed downloads from SABnzbd");
        }
        
        _logger.LogDebug("Found {Count} unprocessed completed downloads", completedDownloads.Count);
        return completedDownloads;
    }
    
    /// <inheritdoc />
    public async Task<NzbImportResult> ProcessCompletedDownloadAsync(
        NzbCompletedDownload download, 
        NzbImportOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        options ??= new NzbImportOptions();
        var importId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("Processing completed download: {Name} from {Path}", 
            download.Name, download.DownloadPath);
        
        try
        {
            // Step 1: Find comic files
            var comicFiles = await FindComicFilesAsync(download.DownloadPath, options, cancellationToken);
            
            if (comicFiles.Count == 0)
            {
                _logger.LogWarning("No comic files found in {Path}", download.DownloadPath);
                await MarkAsProcessedAsync(download.DownloadId, cancellationToken);
                
                return NzbImportResult.Failed(importId, download.DownloadId, download.Name, 
                    NzbImportState.NoFilesFound, "No comic files found in download");
            }
            
            _logger.LogInformation("Found {Count} comic files in download", comicFiles.Count);
            
            // Step 2: Process each comic file
            var importedFiles = new List<NzbImportedFile>();
            var skippedFiles = new List<string>();
            
            foreach (var filePath in comicFiles)
            {
                var importedFile = await ProcessComicFileAsync(filePath, download, options, cancellationToken);
                
                if (importedFile != null)
                {
                    importedFiles.Add(importedFile);
                }
                else
                {
                    skippedFiles.Add(filePath);
                }
            }
            
            // Step 3: Mark as processed
            await MarkAsProcessedAsync(download.DownloadId, cancellationToken);
            
            // Step 4: Create history event
            var historyEventId = await CreateHistoryEventAsync(download, importedFiles, cancellationToken);
            
            // Step 5: Cleanup empty directories if enabled
            if (options.CleanupEmptyDirectories && Directory.Exists(download.DownloadPath))
            {
                CleanupEmptyDirectories(download.DownloadPath);
            }
            
            var allAutoImported = importedFiles.All(f => f.WasAutoImported);
            var state = allAutoImported ? NzbImportState.Completed : NzbImportState.CompletedPendingReview;
            
            _logger.LogInformation("Completed processing download {Name}: {ImportedCount} files imported, {SkippedCount} skipped",
                download.Name, importedFiles.Count, skippedFiles.Count);
            
            return new NzbImportResult
            {
                ImportId = importId,
                DownloadId = download.DownloadId,
                DownloadName = download.Name,
                Success = true,
                State = state,
                ImportedFiles = importedFiles,
                SkippedFiles = skippedFiles,
                HistoryEventId = historyEventId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing download {Name}", download.Name);
            return NzbImportResult.Failed(importId, download.DownloadId, download.Name, 
                NzbImportState.Failed, ex.Message);
        }
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<NzbImportResult>> ProcessAllCompletedAsync(
        NzbImportOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<NzbImportResult>();
        var completedDownloads = await GetCompletedDownloadsAsync(cancellationToken);
        
        foreach (var download in completedDownloads)
        {
            // Check category filter
            if (options?.Categories.Count > 0 && 
                !string.IsNullOrEmpty(download.Category) &&
                !options.Categories.Contains(download.Category, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping download {Name}: category {Category} not in filter", 
                    download.Name, download.Category);
                continue;
            }
            
            var result = await ProcessCompletedDownloadAsync(download, options, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    /// <inheritdoc />
    public async Task MarkAsProcessedAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var processedIds = await GetProcessedDownloadIdsInternalAsync(cancellationToken);
        processedIds.Add(downloadId);
        
        // Keep only last 1000 IDs to prevent unbounded growth
        if (processedIds.Count > 1000)
        {
            processedIds = processedIds.Skip(processedIds.Count - 1000).ToHashSet();
        }
        
        await _settingsService.SetAsync(ProcessedDownloadsKey, processedIds, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<bool> IsProcessedAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var processedIds = await GetProcessedDownloadIdsAsync(cancellationToken);
        return processedIds.Contains(downloadId);
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetProcessedDownloadIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await GetProcessedDownloadIdsInternalAsync(cancellationToken);
        return ids;
    }
    
    private async Task<HashSet<string>> GetProcessedDownloadIdsInternalAsync(CancellationToken cancellationToken)
    {
        return await _settingsService.GetAsync(ProcessedDownloadsKey, new HashSet<string>(), cancellationToken) 
               ?? new HashSet<string>();
    }
    
    private async Task<List<string>> FindComicFilesAsync(string path, NzbImportOptions options, CancellationToken cancellationToken)
    {
        var comicFiles = new List<string>();
        
        // Check if path is a file or directory
        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (_comicExtensions.Contains(ext))
            {
                comicFiles.Add(path);
            }
            else if (options.ExtractArchives && _archiveExtensions.Contains(ext))
            {
                // Extract archive and find comic files
                var extractedFiles = await ExtractArchiveAsync(path, cancellationToken);
                comicFiles.AddRange(extractedFiles.Where(f => 
                    _comicExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
            }
        }
        else if (Directory.Exists(path))
        {
            // Recursively find comic files
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                
                if (_comicExtensions.Contains(ext))
                {
                    comicFiles.Add(file);
                }
                else if (options.ExtractArchives && _archiveExtensions.Contains(ext))
                {
                    var extractedFiles = await ExtractArchiveAsync(file, cancellationToken);
                    comicFiles.AddRange(extractedFiles.Where(f => 
                        _comicExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
                }
            }
        }
        
        return comicFiles;
    }
    
    private async Task<List<string>> ExtractArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        var extractedFiles = new List<string>();
        
        try
        {
            // Check if archive type is supported
            if (!_archiveExtractor.IsSupportedArchive(archivePath))
            {
                _logger.LogWarning("Unsupported archive format: {Archive}", archivePath);
                return extractedFiles;
            }

            // Extract to sibling directory
            var result = await _archiveExtractor.ExtractToSiblingDirectoryAsync(archivePath, cancellationToken);
            
            if (result.Success)
            {
                extractedFiles.AddRange(result.ExtractedFiles);
                _logger.LogInformation(
                    "Extracted {Count} files ({Size:N0} bytes) from {Archive} ({Type}) in {Duration}ms",
                    result.FileCount,
                    result.TotalExtractedSize,
                    Path.GetFileName(archivePath),
                    result.ArchiveType,
                    result.Duration.TotalMilliseconds);
            }
            else if (result.IsPasswordProtected)
            {
                _logger.LogWarning("Archive is password-protected, skipping: {Archive}", archivePath);
            }
            else
            {
                _logger.LogError("Failed to extract archive: {Archive} - {Error}", archivePath, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting archive: {Archive}", archivePath);
        }
        
        return extractedFiles;
    }
    
    private async Task<NzbImportedFile?> ProcessComicFileAsync(
        string filePath, 
        NzbCompletedDownload download, 
        NzbImportOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var filename = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length; // Capture size before any file operations
            var format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            
            // Parse the filename
            var (parsedInfo, confidence, isCollection) = _filenameParser.Parse(filename);
            
            _logger.LogDebug("Parsed {File}: Series={Series}, Issue={Issue}, Confidence={Confidence}",
                filename, parsedInfo.SeriesTitle, parsedInfo.IssueNumber, confidence);
            
            // Try to match to a series
            int? matchedSeriesId = null;
            int? matchedIssueId = null;
            
            if (!string.IsNullOrEmpty(parsedInfo.SeriesTitle))
            {
                var seriesMatch = await TryMatchSeriesAsync(parsedInfo.SeriesTitle, cancellationToken);
                if (seriesMatch != null)
                {
                    matchedSeriesId = seriesMatch.Id;
                    
                    // Try to match issue
                    if (parsedInfo.IssueNumber.HasValue)
                    {
                        var issueMatch = await TryMatchIssueAsync(seriesMatch.Id, parsedInfo.IssueNumber.Value, cancellationToken);
                        matchedIssueId = issueMatch?.Id;
                    }
                }
            }
            
            // Decide whether to auto-import
            var shouldAutoImport = options.AutoImport && 
                                   confidence >= options.MinAutoImportConfidence &&
                                   matchedSeriesId.HasValue;
            
            string? stagingPath = null;
            string? destinationPath = null;
            int? fileAssetId = null;
            
            if (shouldAutoImport && matchedSeriesId.HasValue)
            {
                // Auto-import via StagingService
                var importResult = await _stagingService.ImportAsync(
                    filePath, 
                    matchedSeriesId.Value, 
                    matchedIssueId,
                    null, // editionId
                    cancellationToken);
                
                if (importResult.Success)
                {
                    destinationPath = importResult.DestinationPath;
                    fileAssetId = importResult.FileAssetId;
                    _logger.LogInformation("Auto-imported {File} to {Destination}", filename, destinationPath);
                }
                else
                {
                    // Fall back to staging
                    stagingPath = await MoveToStagingAsync(filePath, download.Name, cancellationToken);
                    shouldAutoImport = false;
                }
            }
            else
            {
                // Move to staging for manual review
                stagingPath = await MoveToStagingAsync(filePath, download.Name, cancellationToken);
            }
            
            return new NzbImportedFile
            {
                SourcePath = filePath,
                StagingPath = stagingPath,
                DestinationPath = destinationPath,
                Size = fileSize,
                Format = format,
                ParsedSeriesTitle = parsedInfo.SeriesTitle,
                ParsedIssueNumber = parsedInfo.IssueNumber,
                MatchConfidence = confidence,
                MatchedSeriesId = matchedSeriesId,
                MatchedIssueId = matchedIssueId,
                WasAutoImported = shouldAutoImport && destinationPath != null,
                FileAssetId = fileAssetId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing comic file: {File}", filePath);
            return null;
        }
    }
    
    private async Task<string?> MoveToStagingAsync(string sourcePath, string downloadName, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(_stagingFolder))
            {
                Directory.CreateDirectory(_stagingFolder);
            }
            
            var filename = Path.GetFileName(sourcePath);
            var stagingPath = Path.Combine(_stagingFolder, filename);
            
            // Handle existing file
            if (File.Exists(stagingPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(filename);
                var ext = Path.GetExtension(filename);
                var counter = 1;
                
                while (File.Exists(stagingPath))
                {
                    stagingPath = Path.Combine(_stagingFolder, $"{baseName}_{counter}{ext}");
                    counter++;
                }
            }
            
            await Task.Run(() => File.Move(sourcePath, stagingPath), cancellationToken);
            _logger.LogInformation("Moved {Source} to staging: {Destination}", sourcePath, stagingPath);
            
            return stagingPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving file to staging: {Source}", sourcePath);
            return null;
        }
    }
    
    private async Task<Series?> TryMatchSeriesAsync(string seriesTitle, CancellationToken cancellationToken)
    {
        // Try exact match first
        var exactMatch = await _db.Series
            .FirstOrDefaultAsync(s => s.Title == seriesTitle, cancellationToken);
        
        if (exactMatch != null)
            return exactMatch;
        
        // Try case-insensitive match
        var caseInsensitiveMatch = await _db.Series
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.Title, seriesTitle), cancellationToken);
        
        if (caseInsensitiveMatch != null)
            return caseInsensitiveMatch;
        
        // Try contains match (for partial titles)
        var containsMatch = await _db.Series
            .FirstOrDefaultAsync(s => s.Title.Contains(seriesTitle) || seriesTitle.Contains(s.Title), cancellationToken);
        
        return containsMatch;
    }
    
    private async Task<Issue?> TryMatchIssueAsync(int seriesId, decimal issueNumber, CancellationToken cancellationToken)
    {
        return await _db.Issues
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.IssueNumber == issueNumber, cancellationToken);
    }
    
    private async Task<int?> CreateHistoryEventAsync(
        NzbCompletedDownload download, 
        List<NzbImportedFile> importedFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            // Store download metadata in Data field
            var downloadData = new
            {
                DownloadId = download.DownloadId,
                ClientName = download.ClientName,
                Category = download.Category,
                TotalBytes = download.TotalBytes,
                CompletedAt = download.CompletedAt,
                ImportedFileCount = importedFiles.Count,
                AutoImportedCount = importedFiles.Count(f => f.WasAutoImported)
            };
            
            var historyEvent = new HistoryEvent
            {
                EventType = HistoryEventType.DownloadCompleted,
                Message = $"NZB download completed: {download.Name}",
                SourcePath = download.DownloadPath,
                Success = true,
                Data = System.Text.Json.JsonSerializer.Serialize(downloadData)
            };
            
            // Link to first matched series/issue if available
            var firstMatched = importedFiles.FirstOrDefault(f => f.MatchedSeriesId.HasValue);
            if (firstMatched != null)
            {
                historyEvent.SeriesId = firstMatched.MatchedSeriesId;
                historyEvent.IssueId = firstMatched.MatchedIssueId;
            }
            
            _db.HistoryEvents.Add(historyEvent);
            await _db.SaveChangesAsync(cancellationToken);
            
            return historyEvent.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating history event for download {Name}", download.Name);
            return null;
        }
    }
    
    private void CleanupEmptyDirectories(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;
            
            // Recursively delete empty subdirectories
            foreach (var dir in Directory.GetDirectories(path))
            {
                CleanupEmptyDirectories(dir);
            }
            
            // Delete this directory if empty
            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
                _logger.LogDebug("Deleted empty directory: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up directory: {Path}", path);
        }
    }
}
