using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for handling post-download processing and import handoff.
/// Bridges the DDL download pipeline to the import pipeline.
/// </summary>
public class DdlImportService : IDdlImportService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly IDdlReleaseParser _releaseParser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DdlImportService>? _logger;
    
    private readonly ConcurrentDictionary<string, DdlPendingImport> _pendingImports = new();
    
    // Magic bytes for format detection
    private static readonly byte[] ZipMagic = { 0x50, 0x4B }; // PK (ZIP, CBZ)
    private static readonly byte[] RarMagic = { 0x52, 0x61, 0x72 }; // Rar
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    private static readonly byte[] SevenZipMagic = { 0x37, 0x7A, 0xBC, 0xAF }; // 7z

    public DdlImportService(
        ShortboxerrDbContext dbContext, 
        IDdlReleaseParser releaseParser,
        IConfiguration configuration,
        ILogger<DdlImportService>? logger = null)
    {
        _dbContext = dbContext;
        _releaseParser = releaseParser;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DdlImportResult> ProcessDownloadAsync(DdlDownloadResult downloadResult, DdlCandidate candidate, DdlImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DdlImportOptions();
        var importId = Guid.NewGuid().ToString();
        
        _logger?.LogInformation("Processing download for import: {Title} from {Source}", 
            candidate.ReleaseTitle, candidate.SourceSite);
        
        if (!downloadResult.Success || string.IsNullOrEmpty(downloadResult.FilePath))
        {
            _logger?.LogWarning("Import skipped: download was unsuccessful. Reason: {Reason}", 
                downloadResult.FailureReason);
            return DdlImportResult.Failed(importId, DdlImportState.VerificationFailed, 
                $"Download was not successful: {downloadResult.ErrorMessage}");
        }
        
        try
        {
            // Step 1: Verify the downloaded file
            var verifyResult = await VerifyFileAsync(downloadResult.FilePath, candidate, cancellationToken);
            if (!verifyResult.IsValid)
            {
                return DdlImportResult.Failed(importId, DdlImportState.VerificationFailed, 
                    verifyResult.ErrorMessage ?? "File verification failed");
            }
            
            // Step 2: Move to staging folder
            var stagingResult = await MoveToStagingAsync(downloadResult.FilePath, candidate, cancellationToken);
            if (!stagingResult.Success)
            {
                return DdlImportResult.Failed(importId, DdlImportState.StagingFailed, 
                    stagingResult.ErrorMessage ?? "Failed to move file to staging");
            }
            
            // Step 3: Auto-match to series/issue
            var matchResult = await AutoMatchAsync(candidate, cancellationToken);
            
            // Step 4: Decide whether to auto-import or queue for review
            var shouldAutoImport = ShouldAutoImport(matchResult, options);
            
            if (shouldAutoImport && stagingResult.StagingPath != null)
            {
                // Auto-import
                return await ExecuteImportAsync(stagingResult.StagingPath, candidate, matchResult, cancellationToken);
            }
            else
            {
                // Queue for manual review
                var pendingId = Guid.NewGuid().ToString();
                var pending = new DdlPendingImport
                {
                    Id = pendingId,
                    StagingPath = stagingResult.StagingPath ?? downloadResult.FilePath,
                    Filename = stagingResult.StagingFilename ?? Path.GetFileName(downloadResult.FilePath),
                    FileSize = verifyResult.FileSize,
                    Candidate = candidate,
                    BestMatch = matchResult.MatchFound ? matchResult : null,
                    SuggestedSeriesId = matchResult.Series?.Id,
                    SuggestedSeriesTitle = matchResult.Series?.Title,
                    SuggestedIssueNumber = matchResult.Issue?.IssueNumber,
                    IsCollection = candidate.ParsedInfo.IsCollection,
                    StagedAt = DateTime.UtcNow,
                    ReviewReason = GetReviewReason(matchResult, options)
                };
                
                _pendingImports[pendingId] = pending;
                
                _logger?.LogInformation("File queued for manual review: {Filename} (reason: {Reason})", 
                    pending.Filename, pending.ReviewReason);
                
                return DdlImportResult.PendingReview(importId, pendingId, 
                    stagingResult.StagingPath ?? downloadResult.FilePath, matchResult.Confidence);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing download for import: {Candidate}", candidate.ReleaseTitle);
            return DdlImportResult.Failed(importId, DdlImportState.ImportFailed, ex.Message);
        }
    }

    public async Task<DdlVerificationResult> VerifyFileAsync(string filePath, DdlCandidate candidate, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        
        if (!File.Exists(filePath))
        {
            return new DdlVerificationResult
            {
                IsValid = false,
                FilePath = filePath,
                ErrorMessage = "File does not exist"
            };
        }
        
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        
        // Check for empty file
        if (fileSize == 0)
        {
            return new DdlVerificationResult
            {
                IsValid = false,
                FilePath = filePath,
                FileSize = 0,
                ErrorMessage = "File is empty"
            };
        }
        
        // Check minimum size
        var minSize = candidate.ParsedInfo.IsCollection ? 5_000_000L : 1_000_000L;
        if (fileSize < minSize)
        {
            warnings.Add($"File size ({FormatSize(fileSize)}) is below typical minimum for {(candidate.ParsedInfo.IsCollection ? "collections" : "singles")}");
        }
        
        // Detect format from magic bytes
        string? detectedFormat = null;
        bool formatSupported = false;
        
        try
        {
            var magicBytes = new byte[16]; // Need more bytes for HTML detection
            await using var fs = File.OpenRead(filePath);
            await fs.ReadAsync(magicBytes.AsMemory(0, Math.Min(16, (int)fileSize)), cancellationToken);
            
            (detectedFormat, formatSupported) = DetectFormat(magicBytes);
            
            if (detectedFormat == "html")
            {
                return new DdlVerificationResult
                {
                    IsValid = false,
                    FilePath = filePath,
                    FileSize = fileSize,
                    DetectedFormat = "html",
                    FormatSupported = false,
                    ErrorMessage = "File appears to be an HTML error page, not a comic archive"
                };
            }
            
            if (!formatSupported && detectedFormat != null)
            {
                warnings.Add($"Detected format '{detectedFormat}' may not be fully supported");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect file format for {FilePath}", filePath);
            warnings.Add("Could not verify file format");
        }
        
        return new DdlVerificationResult
        {
            IsValid = true,
            FilePath = filePath,
            FileSize = fileSize,
            DetectedFormat = detectedFormat,
            FormatSupported = formatSupported,
            Warnings = warnings
        };
    }

    public async Task<DdlStagingResult> MoveToStagingAsync(string sourcePath, DdlCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            var stagingFolder = GetStagingFolder();
            
            if (!Directory.Exists(stagingFolder))
            {
                Directory.CreateDirectory(stagingFolder);
            }
            
            // Generate staging filename
            var originalFilename = Path.GetFileName(sourcePath);
            var stagingFilename = SanitizeFilename(candidate.ReleaseTitle);
            
            // Ensure proper extension
            var originalExt = Path.GetExtension(sourcePath);
            if (!string.IsNullOrEmpty(originalExt) && !stagingFilename.EndsWith(originalExt, StringComparison.OrdinalIgnoreCase))
            {
                stagingFilename = Path.ChangeExtension(stagingFilename, originalExt);
            }
            
            var stagingPath = Path.Combine(stagingFolder, stagingFilename);
            
            // Handle existing file
            if (File.Exists(stagingPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(stagingFilename);
                var ext = Path.GetExtension(stagingFilename);
                var counter = 1;
                
                while (File.Exists(stagingPath))
                {
                    stagingFilename = $"{baseName}_{counter}{ext}";
                    stagingPath = Path.Combine(stagingFolder, stagingFilename);
                    counter++;
                }
            }
            
            // Move file
            await Task.Run(() => File.Move(sourcePath, stagingPath), cancellationToken);
            
            _logger?.LogInformation("Moved file to staging: {Source} → {Destination}", sourcePath, stagingPath);
            
            return new DdlStagingResult
            {
                Success = true,
                SourcePath = sourcePath,
                StagingPath = stagingPath,
                StagingFilename = stagingFilename
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move file to staging: {Source}", sourcePath);
            return new DdlStagingResult
            {
                Success = false,
                SourcePath = sourcePath,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<DdlMatchResult> AutoMatchAsync(DdlCandidate candidate, CancellationToken cancellationToken = default)
    {
        var parsed = candidate.ParsedInfo;
        var confidenceReductions = new List<string>();
        var confidence = 100;
        
        _logger?.LogDebug("Auto-matching candidate: {Title}, Parsed: Series={Series}, Issue={Issue}, Collection={IsCollection}", 
            candidate.ReleaseTitle, parsed.SeriesTitle, parsed.IssueNumber, parsed.IsCollection);
        
        if (string.IsNullOrWhiteSpace(parsed.SeriesTitle))
        {
            _logger?.LogDebug("Match failed: no series title parsed from release name");
            return DdlMatchResult.NoMatch("No series title could be parsed from release name");
        }
        
        // Try to find matching series
        var normalizedTitle = _releaseParser.NormalizeTitle(parsed.SeriesTitle);
        _logger?.LogDebug("Normalized title for matching: '{NormalizedTitle}'", normalizedTitle);
        
        var matchingSeries = await _dbContext.Series
            .Where(s => EF.Functions.Like(s.Title.ToLower(), $"%{normalizedTitle}%") ||
                       (s.SortTitle != null && EF.Functions.Like(s.SortTitle.ToLower(), $"%{normalizedTitle}%")))
            .ToListAsync(cancellationToken);
        
        if (matchingSeries.Count == 0)
        {
            return DdlMatchResult.NoMatch($"No series found matching '{parsed.SeriesTitle}'");
        }
        
        // Find best series match
        Series? bestSeries = null;
        var bestSeriesScore = 0;
        
        foreach (var series in matchingSeries)
        {
            var score = CalculateSeriesMatchScore(series, parsed);
            if (score > bestSeriesScore)
            {
                bestSeriesScore = score;
                bestSeries = series;
            }
        }
        
        if (bestSeries == null)
        {
            return DdlMatchResult.NoMatch("No confident series match found");
        }
        
        // Adjust confidence based on series match
        if (bestSeriesScore < 90)
        {
            var reduction = 90 - bestSeriesScore;
            confidence -= reduction;
            confidenceReductions.Add($"Series match not exact (-{reduction})");
        }
        
        // Handle collections vs singles
        if (parsed.IsCollection)
        {
            // Try to match edition
            var matchingEdition = await _dbContext.EditionTitles
                .Where(e => e.SeriesId == bestSeries.Id)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (matchingEdition != null)
            {
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = Math.Max(0, confidence),
                    Series = bestSeries,
                    Edition = matchingEdition,
                    IsCollection = true,
                    Explanation = $"Matched to collection: {bestSeries.Title} - {matchingEdition.Title}",
                    ConfidenceReductions = confidenceReductions
                };
            }
            
            // Collection without matching edition
            confidence -= 20;
            confidenceReductions.Add("No matching edition found (-20)");
            
            return new DdlMatchResult
            {
                MatchFound = true,
                Confidence = Math.Max(0, confidence),
                Series = bestSeries,
                IsCollection = true,
                Explanation = $"Matched to series (collection): {bestSeries.Title}",
                ConfidenceReductions = confidenceReductions
            };
        }
        else
        {
            // Try to match issue
            if (!parsed.IssueNumber.HasValue)
            {
                confidence -= 30;
                confidenceReductions.Add("No issue number in release name (-30)");
                
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = Math.Max(0, confidence),
                    Series = bestSeries,
                    IsCollection = false,
                    Explanation = $"Matched to series (no issue number): {bestSeries.Title}",
                    ConfidenceReductions = confidenceReductions
                };
            }
            
            var matchingIssue = await _dbContext.Issues
                .Where(i => i.SeriesId == bestSeries.Id && i.IssueNumber == parsed.IssueNumber.Value)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (matchingIssue != null)
            {
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = Math.Max(0, confidence),
                    Series = bestSeries,
                    Issue = matchingIssue,
                    IsCollection = false,
                    Explanation = $"Matched to issue: {bestSeries.Title} #{matchingIssue.IssueNumber}",
                    ConfidenceReductions = confidenceReductions
                };
            }
            
            // Issue not found in database
            confidence -= 15;
            confidenceReductions.Add("Issue not in database (-15)");
            
            return new DdlMatchResult
            {
                MatchFound = true,
                Confidence = Math.Max(0, confidence),
                Series = bestSeries,
                IsCollection = false,
                Explanation = $"Matched to series: {bestSeries.Title} (issue #{parsed.IssueNumber} not in database)",
                ConfidenceReductions = confidenceReductions
            };
        }
    }

    public async Task<DdlImportResult> ExecuteImportAsync(string stagedFilePath, DdlCandidate candidate, DdlMatchResult? match = null, CancellationToken cancellationToken = default)
    {
        var importId = Guid.NewGuid().ToString();
        
        try
        {
            if (!File.Exists(stagedFilePath))
            {
                return DdlImportResult.Failed(importId, DdlImportState.ImportFailed, "Staged file no longer exists");
            }
            
            match ??= await AutoMatchAsync(candidate, cancellationToken);
            
            if (match.Series == null)
            {
                return DdlImportResult.Failed(importId, DdlImportState.MatchingFailed, 
                    match.Explanation ?? "No series match found");
            }
            
            // Determine library path
            var libraryRoot = GetLibraryFolder();
            var seriesFolder = Path.Combine(libraryRoot, SanitizeFilename(match.Series.Title));
            
            if (!Directory.Exists(seriesFolder))
            {
                Directory.CreateDirectory(seriesFolder);
            }
            
            var filename = Path.GetFileName(stagedFilePath);
            var libraryPath = Path.Combine(seriesFolder, filename);
            
            // Handle existing file
            if (File.Exists(libraryPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(filename);
                var ext = Path.GetExtension(filename);
                libraryPath = Path.Combine(seriesFolder, $"{baseName}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}");
            }
            
            // Move to library
            await Task.Run(() => File.Move(stagedFilePath, libraryPath), cancellationToken);
            
            // Create file asset
            var fileAsset = new FileAsset
            {
                Path = libraryPath,
                RelativePath = Path.GetRelativePath(libraryRoot, libraryPath),
                Size = new FileInfo(libraryPath).Length,
                Format = Path.GetExtension(libraryPath).TrimStart('.').ToLowerInvariant(),
                IssueId = match.Issue?.Id,
                EditionTitleId = match.Edition?.Id,
                DateAdded = DateTime.UtcNow
            };
            
            _dbContext.FileAssets.Add(fileAsset);
            
            // Update issue status to Owned if this is a single issue import
            if (match.Issue != null)
            {
                match.Issue.HasFile = true;
                match.Issue.Status = IssueStatus.Owned;
                match.Issue.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Updated issue #{IssueNumber} status to Owned", match.Issue.IssueNumber);
            }
            
            // Create history event
            var historyEvent = new HistoryEvent
            {
                EventType = HistoryEventType.DdlImportCompleted,
                SeriesId = match.Series.Id,
                IssueId = match.Issue?.Id,
                EditionTitleId = match.Edition?.Id,
                Message = $"DDL import completed: {candidate.ReleaseTitle}",
                SourcePath = stagedFilePath,
                DestinationPath = libraryPath,
                Success = true,
                Data = JsonSerializer.Serialize(new
                {
                    CandidateId = candidate.Id,
                    SourceSite = candidate.SourceSite,
                    MatchConfidence = match.Confidence,
                    IsCollection = match.IsCollection,
                    Quality = candidate.ParsedInfo.Quality
                })
            };
            
            _dbContext.HistoryEvents.Add(historyEvent);
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger?.LogInformation("Successfully imported: {Candidate} → {Library}", candidate.ReleaseTitle, libraryPath);
            
            return DdlImportResult.Succeeded(
                importId,
                libraryPath,
                match.Series.Id,
                match.Series.Title,
                match.Issue?.Id,
                match.Issue?.IssueNumber,
                fileAsset.Id,
                historyEvent.Id,
                match.Confidence
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute import for {Candidate}", candidate.ReleaseTitle);
            return DdlImportResult.Failed(importId, DdlImportState.ImportFailed, ex.Message);
        }
    }

    public Task<IReadOnlyList<DdlPendingImport>> GetPendingImportsAsync(CancellationToken cancellationToken = default)
    {
        var pending = _pendingImports.Values
            .OrderByDescending(p => p.StagedAt)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<DdlPendingImport>>(pending);
    }

    public async Task<DdlImportResult> ApprovePendingImportAsync(string pendingImportId, int? seriesId = null, int? issueId = null, CancellationToken cancellationToken = default)
    {
        if (!_pendingImports.TryRemove(pendingImportId, out var pending))
        {
            return DdlImportResult.Failed(pendingImportId, DdlImportState.ImportFailed, "Pending import not found");
        }
        
        // Use provided IDs or fall back to suggested
        var finalSeriesId = seriesId ?? pending.SuggestedSeriesId;
        
        if (!finalSeriesId.HasValue)
        {
            // Re-add to pending
            _pendingImports[pendingImportId] = pending;
            return DdlImportResult.Failed(pendingImportId, DdlImportState.MatchingFailed, "No series ID provided or suggested");
        }
        
        // Create a match result with the approved IDs
        var series = await _dbContext.Series.FindAsync(new object[] { finalSeriesId.Value }, cancellationToken);
        Issue? issue = null;
        
        if (issueId.HasValue)
        {
            issue = await _dbContext.Issues.FindAsync(new object[] { issueId.Value }, cancellationToken);
        }
        
        var match = new DdlMatchResult
        {
            MatchFound = true,
            Confidence = 100, // Manual approval = 100% confidence
            Series = series,
            Issue = issue,
            IsCollection = pending.IsCollection,
            Explanation = "Manually approved"
        };
        
        var candidate = pending.Candidate ?? new DdlCandidate
        {
            Id = pendingImportId,
            ReleaseTitle = pending.Filename,
            SourceSite = "ManualImport",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = pending.SuggestedSeriesTitle,
                IssueNumber = pending.SuggestedIssueNumber,
                IsCollection = pending.IsCollection
            }
        };
        
        return await ExecuteImportAsync(pending.StagingPath, candidate, match, cancellationToken);
    }

    public Task<bool> RejectPendingImportAsync(string pendingImportId, string reason, bool deleteFile = false, CancellationToken cancellationToken = default)
    {
        if (!_pendingImports.TryRemove(pendingImportId, out var pending))
        {
            return Task.FromResult(false);
        }
        
        _logger?.LogInformation("Rejected pending import: {Filename} - {Reason}", pending.Filename, reason);
        
        if (deleteFile && File.Exists(pending.StagingPath))
        {
            try
            {
                File.Delete(pending.StagingPath);
                _logger?.LogInformation("Deleted rejected file: {Path}", pending.StagingPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete rejected file: {Path}", pending.StagingPath);
            }
        }
        
        return Task.FromResult(true);
    }

    private bool ShouldAutoImport(DdlMatchResult match, DdlImportOptions options)
    {
        if (!options.AutoImportEnabled)
        {
            return false;
        }
        
        if (!match.MatchFound)
        {
            return false;
        }
        
        if (match.Confidence < options.AutoImportMinConfidence)
        {
            return false;
        }
        
        if (options.RequireSeriesMatch && match.Series == null)
        {
            return false;
        }
        
        if (options.RequireIssueMatch && !match.IsCollection && match.Issue == null)
        {
            return false;
        }
        
        return true;
    }

    private static string GetReviewReason(DdlMatchResult match, DdlImportOptions options)
    {
        if (!options.AutoImportEnabled)
        {
            return "Auto-import is disabled";
        }
        
        if (!match.MatchFound)
        {
            return "No match found for release";
        }
        
        if (match.Confidence < options.AutoImportMinConfidence)
        {
            return $"Match confidence ({match.Confidence}%) below threshold ({options.AutoImportMinConfidence}%)";
        }
        
        if (options.RequireSeriesMatch && match.Series == null)
        {
            return "Series match required but not found";
        }
        
        if (options.RequireIssueMatch && !match.IsCollection && match.Issue == null)
        {
            return "Issue match required but not found";
        }
        
        return "Unknown reason";
    }

    private int CalculateSeriesMatchScore(Series series, DdlParsedInfo parsed)
    {
        var score = 0;
        var seriesTitle = _releaseParser.NormalizeTitle(series.Title);
        var parsedTitle = _releaseParser.NormalizeTitle(parsed.SeriesTitle ?? "");
        
        if (seriesTitle == parsedTitle)
        {
            score += 100;
        }
        else if (seriesTitle.Contains(parsedTitle) || parsedTitle.Contains(seriesTitle))
        {
            score += 70;
        }
        else
        {
            score += 30;
        }
        
        // Year bonus
        if (parsed.Year.HasValue && series.StartYear == parsed.Year.Value)
        {
            score += 20;
        }
        
        // Publisher bonus
        if (!string.IsNullOrEmpty(parsed.Publisher) && 
            series.Publisher?.Equals(parsed.Publisher, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 15;
        }
        
        return Math.Min(score, 100);
    }

    private static (string? format, bool supported) DetectFormat(byte[] magic)
    {
        if (StartsWithMagic(magic, ZipMagic))
            return ("cbz", true);
        if (StartsWithMagic(magic, RarMagic))
            return ("cbr", true);
        if (StartsWithMagic(magic, PdfMagic))
            return ("pdf", false);
        if (StartsWithMagic(magic, SevenZipMagic))
            return ("cb7", true);
        
        // Check for HTML
        var text = System.Text.Encoding.ASCII.GetString(magic).ToLowerInvariant();
        if (text.StartsWith("<!doctype") || text.StartsWith("<html") || text.StartsWith("<head"))
            return ("html", false);
        
        return (null, false);
    }

    private static bool StartsWithMagic(byte[] bytes, byte[] magic)
    {
        if (bytes.Length < magic.Length) return false;
        for (int i = 0; i < magic.Length; i++)
        {
            if (bytes[i] != magic[i]) return false;
        }
        return true;
    }

    private string GetStagingFolder()
    {
        // Use same config keys as StagingService for consistency
        return Environment.GetEnvironmentVariable("SHORTBOXERR_STAGING")
               ?? _configuration.GetValue<string>("MediaManagement:StagingFolder")
               ?? _configuration.GetValue<string>("Shortboxerr:StagingFolder") 
               ?? "/data/staging";
    }

    private string GetLibraryFolder()
    {
        // Use same config keys as StagingService for consistency
        var rootFolders = _configuration.GetSection("MediaManagement:RootFolders").Get<string[]>();
        if (rootFolders?.Length > 0)
        {
            return rootFolders[0];
        }
        return _configuration.GetValue<string>("Shortboxerr:LibraryFolder") 
               ?? "/data/library";
    }

    private static string SanitizeFilename(string filename)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            filename = filename.Replace(c, '_');
        }
        return filename.Trim();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1}GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1}MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F1}KB";
        return $"{bytes}B";
    }
}

