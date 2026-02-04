using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Manages the staging folder for manual imports.
/// </summary>
public class StagingService : IStagingService
{
    private readonly ShortboxerrDbContext _db;
    private readonly IFilenameParser _parser;
    private readonly ILogger<StagingService> _logger;
    private readonly string _stagingFolder;
    private readonly string _failedFolder;
    private readonly string[] _libraryRoots;
    private readonly string[] _allowedExtensions = { ".cbz", ".cbr", ".pdf" };

    public StagingService(
        ShortboxerrDbContext db,
        IFilenameParser parser,
        IConfiguration configuration,
        ILogger<StagingService> logger)
    {
        _db = db;
        _parser = parser;
        _logger = logger;
        
        _stagingFolder = Environment.GetEnvironmentVariable("SHORTBOXERR_STAGING") 
            ?? configuration["MediaManagement:StagingFolder"] 
            ?? "/data/staging";
        _failedFolder = Environment.GetEnvironmentVariable("SHORTBOXERR_FAILED")
            ?? configuration["MediaManagement:FailedFolder"]
            ?? "/data/failed";
        _libraryRoots = (configuration.GetSection("MediaManagement:RootFolders").Get<string[]>())
            ?? new[] { Environment.GetEnvironmentVariable("SHORTBOXERR_LIBRARY_ROOT") ?? "/data/library" };
    }

    public async Task<IReadOnlyList<StagedItem>> ScanStagingFolderAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<StagedItem>();

        _logger.LogInformation("Scanning staging folder: {StagingFolder}", _stagingFolder);

        if (!Directory.Exists(_stagingFolder))
        {
            _logger.LogWarning("Staging folder does not exist: {StagingFolder}", _stagingFolder);
            return items;
        }

        var files = Directory.EnumerateFiles(_stagingFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => _allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        _logger.LogInformation("Found {Count} files to process in staging folder", files.Count);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(file);
            _logger.LogDebug("File detected: {FileName}, Size: {Size:N0} bytes", fileInfo.Name, fileInfo.Length);

            var (parsedInfo, confidence, isCollection) = _parser.Parse(fileInfo.Name);
            
            _logger.LogDebug("Parse result: Series={Series}, Issue={Issue}, Year={Year}, Confidence={Confidence}%, Collection={IsCollection}",
                parsedInfo.SeriesTitle ?? "(none)", 
                parsedInfo.IssueNumber?.ToString() ?? "(none)", 
                parsedInfo.Year?.ToString() ?? "(none)",
                confidence, 
                isCollection);

            var item = new StagedItem
            {
                Path = file,
                FileName = fileInfo.Name,
                Size = fileInfo.Length,
                Extension = fileInfo.Extension.TrimStart('.').ToLowerInvariant(),
                LastModified = fileInfo.LastWriteTimeUtc,
                ParsedInfo = parsedInfo,
                ParseConfidence = confidence,
                IsCollection = isCollection
            };

            // Try to match to existing series/edition
            if (!string.IsNullOrEmpty(parsedInfo.SeriesTitle))
            {
                await TryMatchSeriesAsync(item, parsedInfo.SeriesTitle, cancellationToken);
            }

            // Validate file
            ValidateStagedItem(item);

            if (!string.IsNullOrEmpty(item.RejectionReason))
            {
                _logger.LogDebug("File rejected: {FileName}, Reason: {Reason}", fileInfo.Name, item.RejectionReason);
            }

            items.Add(item);
        }

        _logger.LogInformation("Staging scan complete: {Total} files, {Valid} valid, {Rejected} rejected",
            items.Count,
            items.Count(i => string.IsNullOrEmpty(i.RejectionReason)),
            items.Count(i => !string.IsNullOrEmpty(i.RejectionReason)));

        return items.OrderByDescending(i => i.ParseConfidence).ToList();
    }

    public async Task<ImportPreview> GetImportPreviewAsync(
        string sourcePath, 
        int? seriesId, 
        int? issueId, 
        int? editionId, 
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(sourcePath);
        if (!fileInfo.Exists)
        {
            return new ImportPreview
            {
                SourcePath = sourcePath,
                DestinationPath = "",
                NewFileName = "",
                CanImport = false,
                BlockReason = "Source file does not exist"
            };
        }

        var warnings = new List<string>();
        string destinationFolder;
        string newFileName = fileInfo.Name;
        string? seriesTitle = null;
        string? editionTitle = null;
        decimal? issueNumber = null;
        var isCollection = editionId.HasValue;

        // Determine destination based on series
        if (seriesId.HasValue)
        {
            var series = await _db.Series.FindAsync(new object[] { seriesId.Value }, cancellationToken);
            if (series == null)
            {
                return new ImportPreview
                {
                    SourcePath = sourcePath,
                    DestinationPath = "",
                    NewFileName = "",
                    CanImport = false,
                    BlockReason = $"Series {seriesId} not found"
                };
            }

            seriesTitle = series.Title;
            destinationFolder = series.Path ?? Path.Combine(_libraryRoots[0], SanitizePath(series.Title));
        }
        else
        {
            destinationFolder = _libraryRoots[0];
            warnings.Add("No series selected - file will be placed in library root");
        }

        // Get issue/edition details
        if (issueId.HasValue)
        {
            var issue = await _db.Issues.FindAsync(new object[] { issueId.Value }, cancellationToken);
            if (issue != null)
            {
                issueNumber = issue.IssueNumber;
                newFileName = GenerateIssueFilename(seriesTitle ?? "Unknown", issue.IssueNumber, fileInfo.Extension);
            }
        }
        else if (editionId.HasValue)
        {
            var edition = await _db.EditionTitles.FindAsync(new object[] { editionId.Value }, cancellationToken);
            if (edition != null)
            {
                editionTitle = edition.Title;
                newFileName = GenerateEditionFilename(edition.Title, edition.VolumeNumber, fileInfo.Extension);
                isCollection = true;
            }
        }

        var destinationPath = Path.Combine(destinationFolder, newFileName);
        var willRename = newFileName != fileInfo.Name;
        var willMove = Path.GetDirectoryName(destinationPath) != Path.GetDirectoryName(sourcePath);

        // Check for conflicts
        if (File.Exists(destinationPath))
        {
            warnings.Add("Destination file already exists - will be overwritten");
        }

        return new ImportPreview
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            NewFileName = newFileName,
            WillRename = willRename,
            WillMove = willMove,
            SeriesId = seriesId,
            SeriesTitle = seriesTitle,
            IssueId = issueId,
            IssueNumber = issueNumber,
            EditionId = editionId,
            EditionTitle = editionTitle,
            IsCollection = isCollection,
            Warnings = warnings,
            CanImport = true
        };
    }

    public async Task<ImportResult> ImportAsync(
        string sourcePath, 
        int? seriesId, 
        int? issueId, 
        int? editionId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Import initiated: {SourcePath}", sourcePath);
        _logger.LogDebug("Import target: SeriesId={SeriesId}, IssueId={IssueId}, EditionId={EditionId}", 
            seriesId, issueId, editionId);
        
        var preview = await GetImportPreviewAsync(sourcePath, seriesId, issueId, editionId, cancellationToken);
        
        if (!preview.CanImport)
        {
            _logger.LogWarning("Import blocked: {SourcePath}, Reason: {Reason}", sourcePath, preview.BlockReason);
            return new ImportResult
            {
                Success = false,
                SourcePath = sourcePath,
                ErrorMessage = preview.BlockReason
            };
        }

        // Check for duplicate detection
        var existingAsset = await _db.FileAssets
            .FirstOrDefaultAsync(f => f.Path == preview.DestinationPath, cancellationToken);
        if (existingAsset != null)
        {
            _logger.LogWarning("Duplicate detected: existing file at {Destination} (Asset ID: {AssetId})",
                preview.DestinationPath, existingAsset.Id);
        }

        try
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(preview.DestinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
                _logger.LogDebug("Created destination directory: {Directory}", destDir);
            }

            // Move the file (atomic on same filesystem)
            _logger.LogDebug("Moving file: {Source} → {Destination}", sourcePath, preview.DestinationPath);
            File.Move(sourcePath, preview.DestinationPath, overwrite: true);

            // Create FileAsset record
            var fileInfo = new FileInfo(preview.DestinationPath);
            var fileAsset = new FileAsset
            {
                Path = preview.DestinationPath,
                RelativePath = GetRelativePath(preview.DestinationPath),
                Size = fileInfo.Length,
                Format = fileInfo.Extension.TrimStart('.').ToLowerInvariant(),
                IssueId = issueId,
                EditionTitleId = editionId,
                DateAdded = DateTime.UtcNow
            };

            _db.FileAssets.Add(fileAsset);

            // Update Issue/Edition HasFile flag
            if (issueId.HasValue)
            {
                var issue = await _db.Issues.FindAsync(new object[] { issueId.Value }, cancellationToken);
                if (issue != null)
                {
                    issue.HasFile = true;
                    issue.UpdatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Updated Issue {IssueId}: HasFile=true", issueId);
                }
            }
            else if (editionId.HasValue)
            {
                var edition = await _db.EditionTitles.FindAsync(new object[] { editionId.Value }, cancellationToken);
                if (edition != null)
                {
                    edition.HasFile = true;
                    edition.UpdatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Updated Edition {EditionId}: HasFile=true", editionId);
                }
            }

            // Create history event
            var historyEvent = new HistoryEvent
            {
                EventType = HistoryEventType.FileImported,
                SeriesId = seriesId,
                IssueId = issueId,
                EditionTitleId = editionId,
                Message = $"Imported {Path.GetFileName(sourcePath)} to {preview.DestinationPath}",
                SourcePath = sourcePath,
                DestinationPath = preview.DestinationPath,
                Success = true
            };

            _db.HistoryEvents.Add(historyEvent);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Import success: {Source} → {Destination}, Size: {Size:N0} bytes, Format: {Format}",
                Path.GetFileName(sourcePath), preview.DestinationPath, fileInfo.Length, fileAsset.Format);

            return new ImportResult
            {
                Success = true,
                SourcePath = sourcePath,
                DestinationPath = preview.DestinationPath,
                FileAssetId = fileAsset.Id,
                HistoryEventId = historyEvent.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed: {Source} → {Destination}, Error: {Error}",
                sourcePath, preview.DestinationPath, ex.Message);

            // Log failure event
            var failEvent = new HistoryEvent
            {
                EventType = HistoryEventType.FileImported,
                SeriesId = seriesId,
                IssueId = issueId,
                EditionTitleId = editionId,
                Message = $"Failed to import {Path.GetFileName(sourcePath)}",
                SourcePath = sourcePath,
                Success = false,
                ErrorMessage = ex.Message
            };
            _db.HistoryEvents.Add(failEvent);
            await _db.SaveChangesAsync(cancellationToken);

            return new ImportResult
            {
                Success = false,
                SourcePath = sourcePath,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> MoveToFailedAsync(string sourcePath, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_failedFolder))
            {
                Directory.CreateDirectory(_failedFolder);
            }

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(_failedFolder, fileName);
            
            // Handle duplicates
            var counter = 1;
            while (File.Exists(destPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                destPath = Path.Combine(_failedFolder, $"{nameWithoutExt}_{counter++}{ext}");
            }

            File.Move(sourcePath, destPath);

            // Log history event
            var historyEvent = new HistoryEvent
            {
                EventType = HistoryEventType.FileMoved,
                Message = $"Moved {fileName} to failed folder: {reason}",
                SourcePath = sourcePath,
                DestinationPath = destPath,
                Success = true
            };
            _db.HistoryEvents.Add(historyEvent);
            await _db.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move {Source} to failed folder", sourcePath);
            return false;
        }
    }

    private async Task TryMatchSeriesAsync(StagedItem item, string seriesTitle, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Attempting series match for: '{SeriesTitle}'", seriesTitle);
        
        // Try exact match first
        var series = await _db.Series
            .FirstOrDefaultAsync(s => s.Title == seriesTitle || s.SortTitle == seriesTitle, cancellationToken);

        if (series == null)
        {
            // Try contains match
            series = await _db.Series
                .FirstOrDefaultAsync(s => s.Title.Contains(seriesTitle) || seriesTitle.Contains(s.Title), cancellationToken);
            
            if (series != null)
            {
                _logger.LogDebug("Series match (partial): '{ParsedTitle}' → '{SeriesTitle}' (ID: {SeriesId})",
                    seriesTitle, series.Title, series.Id);
            }
        }
        else
        {
            _logger.LogDebug("Series match (exact): '{ParsedTitle}' → '{SeriesTitle}' (ID: {SeriesId})",
                seriesTitle, series.Title, series.Id);
        }

        if (series != null)
        {
            item.SuggestedSeriesId = series.Id;
            var oldConfidence = item.ParseConfidence;
            item.ParseConfidence = Math.Min(100, item.ParseConfidence + 15);
            _logger.LogDebug("Confidence adjusted: {OldConfidence}% → {NewConfidence}%", oldConfidence, item.ParseConfidence);
        }
        else
        {
            _logger.LogDebug("No series match found for: '{SeriesTitle}'", seriesTitle);
        }
    }

    private void ValidateStagedItem(StagedItem item)
    {
        // Check extension
        if (!_allowedExtensions.Contains($".{item.Extension}"))
        {
            item.RejectionReason = $"Unsupported format: {item.Extension}";
            return;
        }

        // Check minimum size (probably corrupt if < 10KB)
        if (item.Size < 10 * 1024)
        {
            item.RejectionReason = "File too small (< 10KB)";
            return;
        }
    }

    private string GetRelativePath(string fullPath)
    {
        foreach (var root in _libraryRoots)
        {
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath[root.Length..].TrimStart(Path.DirectorySeparatorChar);
            }
        }
        return fullPath;
    }

    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GenerateIssueFilename(string series, decimal issueNumber, string extension)
    {
        var issueStr = issueNumber % 1 == 0 
            ? ((int)issueNumber).ToString("D3") 
            : issueNumber.ToString("000.0");
        return $"{SanitizePath(series)} #{issueStr}{extension}";
    }

    private static string GenerateEditionFilename(string title, int? volumeNumber, string extension)
    {
        var name = volumeNumber.HasValue 
            ? $"{SanitizePath(title)} Vol. {volumeNumber}" 
            : SanitizePath(title);
        return $"{name}{extension}";
    }
}



