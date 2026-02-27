using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
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
    private readonly ISettingsService _settingsService;
    private readonly IMatchHistoryService? _matchHistoryService;
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
        ISettingsService settingsService,
        IMatchHistoryService? matchHistoryService = null,
        ILogger<DdlImportService>? logger = null)
    {
        _dbContext = dbContext;
        _releaseParser = releaseParser;
        _configuration = configuration;
        _settingsService = settingsService;
        _matchHistoryService = matchHistoryService;
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
                
                // Log match to history as pending review (EPIC 19.5)
                if (_matchHistoryService != null)
                {
                    try
                    {
                        await _matchHistoryService.LogMatchAsync(
                            candidate,
                            matchResult,
                            MatchOutcome.PendingReview,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to log match history for {ReleaseTitle}", candidate.ReleaseTitle);
                    }
                }
                
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
        
        // Load auto-match settings
        var settings = await _settingsService.GetAutoMatchSettingsAsync(cancellationToken);
        
        _logger?.LogDebug("Auto-matching candidate: {Title}, Parsed: Series={Series}, Issue={Issue}, Year={Year}, Collection={IsCollection}", 
            candidate.ReleaseTitle, parsed.SeriesTitle, parsed.IssueNumber, parsed.Year, parsed.IsCollection);
        
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
        
        // Detect ambiguous series (multiple series with same name)
        var isAmbiguous = settings.EnableAmbiguousSeriesDetection && IsAmbiguousSeries(matchingSeries, normalizedTitle);
        var ambiguityPenalty = 0;
        
        if (isAmbiguous)
        {
            _logger?.LogDebug("Ambiguous series detected: {Count} series match '{Title}'", matchingSeries.Count, normalizedTitle);
            
            // If ambiguous and no year in release, flag for manual review
            if (!parsed.Year.HasValue && settings.RequireYearForAmbiguousSeries)
            {
                confidence -= 40;
                ambiguityPenalty += 40;
                confidenceReductions.Add($"Ambiguous series (multiple matches) without year in release (-40)");
            }
        }
        
        // Publisher-based filtering for ambiguous series
        var seriesToConsider = matchingSeries;
        if (isAmbiguous && !string.IsNullOrEmpty(parsed.Publisher) && settings.PreferPublisherMatchForAmbiguous)
        {
            var publisherMatches = matchingSeries
                .Where(s => s.Publisher?.Equals(parsed.Publisher, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            
            if (publisherMatches.Count > 0)
            {
                _logger?.LogDebug("Publisher filtering: narrowed from {Total} to {Filtered} series with publisher '{Publisher}'",
                    matchingSeries.Count, publisherMatches.Count, parsed.Publisher);
                seriesToConsider = publisherMatches;
            }
            else
            {
                _logger?.LogDebug("Publisher filtering: no series match publisher '{Publisher}', considering all", parsed.Publisher);
            }
        }
        
        // Find best series match with year-aware and publisher-aware scoring
        Series? bestSeries = null;
        SeriesScoreResult? bestScoreResult = null;
        var bestSeriesScore = 0;
        var yearMismatchRejected = new List<(Series series, int yearDiff)>();
        var publisherMismatchRejected = new List<(Series series, string publisher)>();
        
        foreach (var series in seriesToConsider)
        {
            var scoreResult = CalculateSeriesMatchScoreDetailed(series, parsed, settings);
            
            // Check year tolerance for rejection
            if (settings.RejectMismatchedYears && parsed.Year.HasValue && series.StartYear.HasValue)
            {
                var (isWithinTolerance, yearDiff) = CheckYearTolerance(parsed.Year, series.StartYear, settings.YearMatchTolerance);
                
                if (!isWithinTolerance)
                {
                    _logger?.LogDebug("Series '{SeriesTitle}' ({SeriesYear}) rejected: year mismatch {YearDiff} exceeds tolerance {Tolerance}", 
                        series.Title, series.StartYear, yearDiff, settings.YearMatchTolerance);
                    yearMismatchRejected.Add((series, yearDiff));
                    continue; // Skip this series
                }
            }
            
            // Check publisher mismatch for rejection (when enabled)
            if (settings.RejectMismatchedPublishers && scoreResult.PublisherMismatch)
            {
                _logger?.LogDebug("Series '{SeriesTitle}' (publisher={SeriesPublisher}) rejected: publisher mismatch with release '{ReleasePublisher}'", 
                    series.Title, series.Publisher, parsed.Publisher);
                publisherMismatchRejected.Add((series, series.Publisher ?? "Unknown"));
                continue; // Skip this series
            }
            
            if (scoreResult.TotalScore > bestSeriesScore)
            {
                bestSeriesScore = scoreResult.TotalScore;
                bestSeries = series;
                bestScoreResult = scoreResult;
            }
        }
        
        // If all series were rejected, return detailed error
        if (bestSeries == null)
        {
            if (yearMismatchRejected.Count > 0 && publisherMismatchRejected.Count > 0)
            {
                var yearInfo = string.Join(", ", yearMismatchRejected.Select(r => $"'{r.series.Title}' (year {r.series.StartYear})"));
                var pubInfo = string.Join(", ", publisherMismatchRejected.Select(r => $"'{r.series.Title}' (publisher {r.publisher})"));
                return DdlMatchResult.NoMatch(
                    $"All matching series rejected. Year mismatches (release year: {parsed.Year}): {yearInfo}. " +
                    $"Publisher mismatches (release publisher: {parsed.Publisher}): {pubInfo}.");
            }
            else if (yearMismatchRejected.Count > 0)
            {
                var rejectedInfo = string.Join(", ", yearMismatchRejected.Select(r => $"'{r.series.Title}' ({r.series.StartYear})"));
                return DdlMatchResult.NoMatch(
                    $"All matching series rejected due to year mismatch. Release year: {parsed.Year}, " +
                    $"tolerance: ±{settings.YearMatchTolerance} years. Rejected: {rejectedInfo}");
            }
            else if (publisherMismatchRejected.Count > 0)
            {
                var rejectedInfo = string.Join(", ", publisherMismatchRejected.Select(r => $"'{r.series.Title}' ({r.publisher})"));
                return DdlMatchResult.NoMatch(
                    $"All matching series rejected due to publisher mismatch. Release publisher: {parsed.Publisher}. Rejected: {rejectedInfo}");
            }
            else
            {
                return DdlMatchResult.NoMatch("No confident series match found");
            }
        }
        
        // Log successful match with year info
        if (parsed.Year.HasValue && bestSeries.StartYear.HasValue)
        {
            var yearDiff = Math.Abs(parsed.Year.Value - bestSeries.StartYear.Value);
            if (yearDiff > 0)
            {
                _logger?.LogDebug("Series matched with year difference of {YearDiff}: release={ReleaseYear}, series={SeriesYear}", 
                    yearDiff, parsed.Year, bestSeries.StartYear);
            }
        }
        
        // Build confidence breakdown for diagnostics
        var scoreBreakdown = new ConfidenceBreakdown
        {
            TitleScore = bestScoreResult?.TitleScore ?? 0,
            YearAdjustment = bestScoreResult?.YearAdjustment ?? 0,
            PublisherAdjustment = bestScoreResult?.PublisherAdjustment ?? 0,
            AmbiguityPenalty = ambiguityPenalty,
            FinalScore = bestSeriesScore,
            ScoreExplanations = bestScoreResult?.Explanations ?? new List<string>(),
            YearMatchStatus = bestScoreResult?.YearMatchStatus ?? "Unknown",
            PublisherMatchStatus = bestScoreResult?.PublisherMatchStatus ?? "Unknown",
            IsAmbiguousSeries = isAmbiguous
        };
        
        // Adjust confidence based on series match
        if (bestSeriesScore < 90)
        {
            var reduction = 90 - bestSeriesScore;
            confidence -= reduction;
            confidenceReductions.Add($"Series match not exact (-{reduction})");
        }
        
        // Additional confidence reduction if there were rejected candidates
        if ((yearMismatchRejected.Count > 0 || publisherMismatchRejected.Count > 0) && isAmbiguous)
        {
            confidence -= 10;
            confidenceReductions.Add($"Other series with same name rejected due to year/publisher mismatch (-10)");
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
                var finalConfidence = Math.Max(0, confidence);
                var (reqReview, isLowConf, isFirst, reason) = await GetVerificationPropertiesAsync(
                    finalConfidence, bestSeries, settings, isAmbiguous, cancellationToken);
                    
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = finalConfidence,
                    Series = bestSeries,
                    Edition = matchingEdition,
                    IsCollection = true,
                    Explanation = $"Matched to collection: {bestSeries.Title} - {matchingEdition.Title}",
                    ConfidenceReductions = confidenceReductions,
                    RequiresManualReview = reqReview,
                    MinConfidenceThreshold = settings.MinConfidenceForAutoImport,
                    ScoreBreakdown = scoreBreakdown,
                    IsFirstIssueForSeries = isFirst,
                    IsLowConfidence = isLowConf,
                    ReviewReason = reason
                };
            }
            
            // Collection without matching edition
            confidence -= 20;
            confidenceReductions.Add("No matching edition found (-20)");
            
            var collectionConfidence = Math.Max(0, confidence);
            var (colReqReview, colIsLowConf, colIsFirst, colReason) = await GetVerificationPropertiesAsync(
                collectionConfidence, bestSeries, settings, isAmbiguous, cancellationToken);
                
            return new DdlMatchResult
            {
                MatchFound = true,
                Confidence = collectionConfidence,
                Series = bestSeries,
                IsCollection = true,
                Explanation = $"Matched to series (collection): {bestSeries.Title}",
                ConfidenceReductions = confidenceReductions,
                RequiresManualReview = colReqReview,
                MinConfidenceThreshold = settings.MinConfidenceForAutoImport,
                ScoreBreakdown = scoreBreakdown,
                IsFirstIssueForSeries = colIsFirst,
                IsLowConfidence = colIsLowConf,
                ReviewReason = colReason
            };
        }
        else
        {
            // Try to match issue
            if (!parsed.IssueNumber.HasValue)
            {
                confidence -= 30;
                confidenceReductions.Add("No issue number in release name (-30)");
                
                var noIssueConfidence = Math.Max(0, confidence);
                var (noIssueReqReview, noIssueLowConf, noIssueFirst, noIssueReason) = await GetVerificationPropertiesAsync(
                    noIssueConfidence, bestSeries, settings, isAmbiguous, cancellationToken);
                    
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = noIssueConfidence,
                    Series = bestSeries,
                    IsCollection = false,
                    Explanation = $"Matched to series (no issue number): {bestSeries.Title}",
                    ConfidenceReductions = confidenceReductions,
                    RequiresManualReview = noIssueReqReview,
                    MinConfidenceThreshold = settings.MinConfidenceForAutoImport,
                    ScoreBreakdown = scoreBreakdown,
                    IsFirstIssueForSeries = noIssueFirst,
                    IsLowConfidence = noIssueLowConf,
                    ReviewReason = noIssueReason
                };
            }
            
            var matchingIssue = await _dbContext.Issues
                .Where(i => i.SeriesId == bestSeries.Id && i.IssueNumber == parsed.IssueNumber.Value)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (matchingIssue != null)
            {
                var issueConfidence = Math.Max(0, confidence);
                var (issueReqReview, issueLowConf, issueFirst, issueReason) = await GetVerificationPropertiesAsync(
                    issueConfidence, bestSeries, settings, isAmbiguous, cancellationToken);
                    
                return new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = issueConfidence,
                    Series = bestSeries,
                    Issue = matchingIssue,
                    IsCollection = false,
                    Explanation = $"Matched to issue: {bestSeries.Title} #{matchingIssue.IssueNumber}",
                    ConfidenceReductions = confidenceReductions,
                    RequiresManualReview = issueReqReview,
                    MinConfidenceThreshold = settings.MinConfidenceForAutoImport,
                    ScoreBreakdown = scoreBreakdown,
                    IsFirstIssueForSeries = issueFirst,
                    IsLowConfidence = issueLowConf,
                    ReviewReason = issueReason
                };
            }
            
            // Issue not found in database
            confidence -= 15;
            confidenceReductions.Add("Issue not in database (-15)");
            
            var notFoundConfidence = Math.Max(0, confidence);
            var (nfReqReview, nfLowConf, nfFirst, nfReason) = await GetVerificationPropertiesAsync(
                notFoundConfidence, bestSeries, settings, isAmbiguous, cancellationToken);
                
            return new DdlMatchResult
            {
                MatchFound = true,
                Confidence = notFoundConfidence,
                Series = bestSeries,
                IsCollection = false,
                Explanation = $"Matched to series: {bestSeries.Title} (issue #{parsed.IssueNumber} not in database)",
                ConfidenceReductions = confidenceReductions,
                RequiresManualReview = nfReqReview,
                MinConfidenceThreshold = settings.MinConfidenceForAutoImport,
                ScoreBreakdown = scoreBreakdown,
                IsFirstIssueForSeries = nfFirst,
                IsLowConfidence = nfLowConf,
                ReviewReason = nfReason
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
            
            // Determine library path using folder format from settings
            var libraryRoot = GetLibraryFolder();
            var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
            var seriesFolderName = ExpandSeriesFolderFormat(settings.SeriesFolderFormat, match.Series);
            var seriesFolder = Path.Combine(libraryRoot, seriesFolderName);
            
            if (!Directory.Exists(seriesFolder))
            {
                Directory.CreateDirectory(seriesFolder);
            }
            
            // Update series.Path if not already set
            if (string.IsNullOrEmpty(match.Series.Path))
            {
                match.Series.Path = seriesFolder;
                match.Series.UpdatedAt = DateTime.UtcNow;
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
            
            // Log match to history (EPIC 19.5)
            if (_matchHistoryService != null)
            {
                try
                {
                    await _matchHistoryService.LogMatchAsync(
                        candidate,
                        match,
                        MatchOutcome.AutoImported,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to log match history for {ReleaseTitle}", candidate.ReleaseTitle);
                }
            }
            
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

    /// <summary>
    /// Internal class for returning detailed score breakdown from scoring method.
    /// </summary>
    private class SeriesScoreResult
    {
        public int TotalScore { get; init; }
        public int TitleScore { get; init; }
        public int YearAdjustment { get; init; }
        public int PublisherAdjustment { get; init; }
        public string YearMatchStatus { get; init; } = "Unknown";
        public string PublisherMatchStatus { get; init; } = "Unknown";
        public List<string> Explanations { get; init; } = new();
        public bool PublisherMismatch { get; init; }
    }

    private SeriesScoreResult CalculateSeriesMatchScoreDetailed(Series series, DdlParsedInfo parsed, AutoMatchSettings settings)
    {
        var explanations = new List<string>();
        
        var seriesTitle = _releaseParser.NormalizeTitle(series.Title);
        var parsedTitle = _releaseParser.NormalizeTitle(parsed.SeriesTitle ?? "");
        
        // Title scoring
        int titleScore;
        if (seriesTitle == parsedTitle)
        {
            titleScore = 100;
            explanations.Add("Title: exact match (+100)");
        }
        else if (seriesTitle.Contains(parsedTitle) || parsedTitle.Contains(seriesTitle))
        {
            titleScore = 70;
            explanations.Add($"Title: partial match (+70) '{parsedTitle}' vs '{seriesTitle}'");
        }
        else
        {
            titleScore = 30;
            explanations.Add($"Title: fuzzy match (+30) '{parsedTitle}' vs '{seriesTitle}'");
        }
        
        // Year scoring
        int yearAdjustment = 0;
        string yearMatchStatus;
        
        if (parsed.Year.HasValue && series.StartYear.HasValue)
        {
            var yearDiff = Math.Abs(parsed.Year.Value - series.StartYear.Value);
            
            if (yearDiff == 0)
            {
                yearAdjustment = 20;
                yearMatchStatus = "Exact";
                explanations.Add($"Year: exact match (+20) release={parsed.Year}, series={series.StartYear}");
            }
            else if (yearDiff <= settings.YearMatchTolerance)
            {
                yearAdjustment = 10;
                yearMatchStatus = "WithinTolerance";
                explanations.Add($"Year: within tolerance (+10) release={parsed.Year}, series={series.StartYear}, diff={yearDiff}");
            }
            else
            {
                yearAdjustment = -settings.YearMismatchPenalty;
                yearMatchStatus = "Mismatch";
                explanations.Add($"Year: mismatch (-{settings.YearMismatchPenalty}) release={parsed.Year}, series={series.StartYear}, diff={yearDiff}");
            }
        }
        else if (parsed.Year.HasValue && !series.StartYear.HasValue)
        {
            yearAdjustment = -5;
            yearMatchStatus = "SeriesUnknown";
            explanations.Add($"Year: series has no year (-5) release={parsed.Year}");
        }
        else if (!parsed.Year.HasValue && series.StartYear.HasValue)
        {
            yearAdjustment = 0;
            yearMatchStatus = "ReleaseUnknown";
            explanations.Add($"Year: release has no year, series={series.StartYear}");
        }
        else
        {
            yearAdjustment = 0;
            yearMatchStatus = "BothUnknown";
        }
        
        // Publisher scoring with enhanced logic
        int publisherAdjustment = 0;
        string publisherMatchStatus;
        bool publisherMismatch = false;
        
        if (!string.IsNullOrEmpty(parsed.Publisher) && !string.IsNullOrEmpty(series.Publisher))
        {
            if (series.Publisher.Equals(parsed.Publisher, StringComparison.OrdinalIgnoreCase))
            {
                publisherAdjustment = settings.PublisherMatchBonus;
                publisherMatchStatus = "Exact";
                explanations.Add($"Publisher: exact match (+{settings.PublisherMatchBonus}) '{parsed.Publisher}'");
            }
            else
            {
                publisherAdjustment = -settings.PublisherMismatchPenalty;
                publisherMatchStatus = "Mismatch";
                publisherMismatch = true;
                explanations.Add($"Publisher: mismatch (-{settings.PublisherMismatchPenalty}) release='{parsed.Publisher}', series='{series.Publisher}'");
            }
        }
        else if (!string.IsNullOrEmpty(parsed.Publisher))
        {
            publisherMatchStatus = "SeriesUnknown";
            explanations.Add($"Publisher: series has no publisher, release='{parsed.Publisher}'");
        }
        else if (!string.IsNullOrEmpty(series.Publisher))
        {
            publisherMatchStatus = "ReleaseUnknown";
            explanations.Add($"Publisher: release has no publisher, series='{series.Publisher}'");
        }
        else
        {
            publisherMatchStatus = "BothUnknown";
        }
        
        var totalScore = Math.Clamp(titleScore + yearAdjustment + publisherAdjustment, 0, 100);
        
        return new SeriesScoreResult
        {
            TotalScore = totalScore,
            TitleScore = titleScore,
            YearAdjustment = yearAdjustment,
            PublisherAdjustment = publisherAdjustment,
            YearMatchStatus = yearMatchStatus,
            PublisherMatchStatus = publisherMatchStatus,
            Explanations = explanations,
            PublisherMismatch = publisherMismatch
        };
    }
    
    private int CalculateSeriesMatchScore(Series series, DdlParsedInfo parsed, AutoMatchSettings settings)
    {
        return CalculateSeriesMatchScoreDetailed(series, parsed, settings).TotalScore;
    }

    /// <summary>
    /// Checks if the year from the release is within the acceptable tolerance of the series year.
    /// Returns (isWithinTolerance, yearDifference)
    /// </summary>
    private static (bool isWithinTolerance, int yearDiff) CheckYearTolerance(int? parsedYear, int? seriesYear, int tolerance)
    {
        if (!parsedYear.HasValue || !seriesYear.HasValue)
        {
            // Can't determine - treat as within tolerance but unknown
            return (true, 0);
        }
        
        var diff = Math.Abs(parsedYear.Value - seriesYear.Value);
        return (diff <= tolerance, diff);
    }

    /// <summary>
    /// Detects if multiple series share the same normalized base name.
    /// </summary>
    private bool IsAmbiguousSeries(IReadOnlyList<Series> matchingSeries, string normalizedTitle)
    {
        if (matchingSeries.Count <= 1)
            return false;
        
        // Count how many have exact or very close title matches
        var exactMatches = matchingSeries.Count(s => 
            _releaseParser.NormalizeTitle(s.Title).Equals(normalizedTitle, StringComparison.OrdinalIgnoreCase));
        
        return exactMatches > 1;
    }

    /// <summary>
    /// Check if the series has any existing file assets (imported issues).
    /// Used for RequireConfirmationForFirstIssue setting.
    /// </summary>
    private async Task<bool> IsFirstIssueForSeriesAsync(int seriesId, CancellationToken cancellationToken)
    {
        // Check if any issues in this series have files
        var hasExistingFiles = await _dbContext.Issues
            .Where(i => i.SeriesId == seriesId && i.HasFile)
            .AnyAsync(cancellationToken);
        
        return !hasExistingFiles;
    }

    /// <summary>
    /// Build verification properties for match result.
    /// </summary>
    private async Task<(bool requiresReview, bool isLowConfidence, bool isFirstIssue, string? reviewReason)> 
        GetVerificationPropertiesAsync(
            int confidence, 
            Series series, 
            AutoMatchSettings settings,
            bool isAmbiguous,
            CancellationToken cancellationToken)
    {
        var isLowConfidence = confidence >= settings.MinConfidenceForAutoImport && 
                              confidence < settings.LowConfidenceThreshold + 15; // Low confidence zone
        
        var isFirstIssue = await IsFirstIssueForSeriesAsync(series.Id, cancellationToken);
        
        // Determine if manual review is required and why
        var requiresReview = false;
        string? reviewReason = null;
        
        if (confidence < settings.MinConfidenceForAutoImport)
        {
            requiresReview = true;
            reviewReason = $"Confidence ({confidence}%) below auto-import threshold ({settings.MinConfidenceForAutoImport}%)";
        }
        else if (isFirstIssue && settings.RequireConfirmationForFirstIssue)
        {
            requiresReview = true;
            reviewReason = "First issue for series - confirmation required";
        }
        else if (isAmbiguous && isLowConfidence)
        {
            requiresReview = true;
            reviewReason = "Low confidence match for ambiguous series";
        }
        
        return (requiresReview, isLowConfidence, isFirstIssue, reviewReason);
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

    private static string ExpandSeriesFolderFormat(string format, Series series)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return SanitizeFilename(series.Title);
        }

        var result = format;
        
        result = Regex.Replace(result, @"\{Series Title\}", series.Title ?? "Unknown Series", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Series Year\}", series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Year\}", series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Publisher\}", series.Publisher ?? "Unknown Publisher", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Status\}", series.Status.ToString(), RegexOptions.IgnoreCase);
        
        // Clean up empty parentheses and extra whitespace
        result = Regex.Replace(result, @"\s*\(\s*\)", "");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        
        // Split by path separator and sanitize each part
        var parts = result.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sanitizedParts = parts.Select(SanitizeFilename).ToArray();
        
        return Path.Combine(sanitizedParts);
    }
}

