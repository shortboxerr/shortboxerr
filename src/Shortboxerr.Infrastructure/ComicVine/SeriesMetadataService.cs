using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of ISeriesMetadataService using ComicVine.
/// </summary>
public class SeriesMetadataService : ISeriesMetadataService
{
    private readonly IComicVineClient _comicVineClient;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SeriesMetadataService> _logger;
    private readonly SeriesStatusDeterminer _statusDeterminer;
    
    // Settings key for pull list (same as PullListService)
    private const string PullListSettingsKey = "pulllist";
    
    // Pattern for detecting annual issues or series
    private static readonly Regex AnnualPattern = new(
        @"(?:^|\s)Annual(?:\s+#?(\d+))?(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Pattern for extracting parent series name from an annual series title
    // E.g., "Batman Annual" -> "Batman", "Amazing Spider-Man Annual" -> "Amazing Spider-Man"
    private static readonly Regex AnnualSeriesPattern = new(
        @"^(.+?)\s+Annual$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Pattern for detecting special issues
    private static readonly Regex SpecialPattern = new(
        @"(?:^|\s)(Special|One[- ]?Shot|Giant[- ]?Size|King[- ]?Size|80[- ]?Page Giant|100[- ]?Page|Preview|Prologue|Epilogue|Finale|Zero Hour|Infinity|Secret Files|Sourcebook|Handbook|Who'?s Who|Directory|Index)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SeriesMetadataService(
        IComicVineClient comicVineClient,
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        ILogger<SeriesMetadataService> logger)
    {
        _comicVineClient = comicVineClient;
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
        _statusDeterminer = new SeriesStatusDeterminer(logger as ILogger<SeriesStatusDeterminer>);
    }
    
    /// <summary>
    /// Gets pull list settings directly from settings service (avoids circular dependency with PullListService).
    /// </summary>
    private async Task<PullListSettings> GetPullListSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsService.GetAsync<PullListSettings>(
            PullListSettingsKey, 
            null,
            cancellationToken) ?? new PullListSettings();
    }

    /// <inheritdoc />
    public async Task<SeriesSearchResult> SearchSeriesAsync(
        string query,
        string? publisher = null,
        int? yearStart = null,
        int? yearEnd = null,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The ComicVineClient handles API key validation internally
            var searchResult = await _comicVineClient.SearchVolumesAsync(query, page, limit, cancellationToken);

            if (!searchResult.Success)
            {
                return new SeriesSearchResult
                {
                    Success = false,
                    Error = searchResult.Error
                };
            }

            var candidates = searchResult.Results
                .Select(v => MapVolumeToCandidate(v, query, publisher, yearStart))
                .ToList();

            // Apply additional filtering
            if (!string.IsNullOrEmpty(publisher))
            {
                candidates = candidates
                    .Where(c => c.Publisher?.Contains(publisher, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            if (yearStart.HasValue)
            {
                candidates = candidates
                    .Where(c => c.StartYear >= yearStart.Value)
                    .ToList();
            }

            if (yearEnd.HasValue)
            {
                candidates = candidates
                    .Where(c => c.StartYear <= yearEnd.Value)
                    .ToList();
            }

            // Sort by confidence score
            candidates = candidates.OrderByDescending(c => c.ConfidenceScore).ToList();

            return new SeriesSearchResult
            {
                Success = true,
                Results = candidates,
                TotalResults = searchResult.TotalResults,
                Page = page,
                Limit = limit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search ComicVine for series: {Query}", query);
            return new SeriesSearchResult
            {
                Success = false,
                Error = $"Search failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<SeriesMatchCandidate?> GetSeriesByComicVineIdAsync(
        int volumeId,
        CancellationToken cancellationToken = default)
    {
        if (!_comicVineClient.IsConfigured)
        {
            return null;
        }

        try
        {
            var result = await _comicVineClient.GetVolumeAsync(volumeId, cancellationToken);
            
            if (!result.Success || result.Data == null)
            {
                return null;
            }

            return MapVolumeToCandidate(result.Data, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ComicVine volume: {VolumeId}", volumeId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SeriesMatchCandidate?> GetSeriesByComicVineIssueIdAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        if (!_comicVineClient.IsConfigured)
        {
            return null;
        }

        try
        {
            var issueResult = await _comicVineClient.GetIssueAsync(issueId, cancellationToken);
            if (!issueResult.Success || issueResult.Data?.Volume == null)
            {
                return null;
            }

            var volumeId = issueResult.Data.Volume.Id;
            return await GetSeriesByComicVineIdAsync(volumeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get series from ComicVine issue: {IssueId}", issueId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SeriesMatchResult> MatchSeriesAsync(
        int seriesId,
        int comicVineVolumeId,
        bool syncMetadata = true,
        bool createMissingIssues = true,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null)
        {
            return new SeriesMatchResult
            {
                Success = false,
                Error = $"Series with ID {seriesId} not found"
            };
        }

        // Get ComicVine volume
        var volume = await _comicVineClient.GetVolumeAsync(comicVineVolumeId, cancellationToken);
        if (!volume.Success || volume.Data == null)
        {
            return new SeriesMatchResult
            {
                Success = false,
                Error = volume.Error ?? "Failed to fetch ComicVine volume"
            };
        }

        // Update series with ComicVine ID
        series.ComicVineId = comicVineVolumeId;
        series.ExternalId = comicVineVolumeId.ToString();
        series.ExternalSource = "ComicVine";
        series.ComicVineUrl = volume.Data.SiteDetailUrl;
        series.UpdatedAt = DateTime.UtcNow;

        var issuesCreated = 0;
        var issuesUpdated = 0;

        if (syncMetadata)
        {
            ApplyVolumeMetadataToSeries(series, volume.Data);
        }

        if (createMissingIssues)
        {
            var syncResult = await SyncIssuesFromComicVineInternalAsync(series, cancellationToken);
            issuesCreated = syncResult.IssuesAdded;
            issuesUpdated = syncResult.IssuesUpdated;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Matched series {SeriesId} ({Title}) to ComicVine volume {ComicVineId}",
            seriesId, series.Title, comicVineVolumeId);

        return new SeriesMatchResult
        {
            Success = true,
            SeriesId = seriesId,
            ComicVineId = comicVineVolumeId,
            MetadataSynced = syncMetadata,
            IssuesCreated = issuesCreated,
            IssuesUpdated = issuesUpdated
        };
    }

    /// <inheritdoc />
    public async Task<SeriesAutoMatchResult> AutoMatchSeriesAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null)
        {
            return new SeriesAutoMatchResult
            {
                Success = false,
                Error = $"Series with ID {seriesId} not found"
            };
        }

        // Get auto-match threshold from settings
        var cvSettings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        var threshold = cvSettings?.AutoMatchThreshold ?? 85;

        // Search for matches
        var searchResult = await SearchSeriesAsync(
            series.Title,
            series.Publisher,
            series.StartYear,
            null,
            page: 1,
            limit: 5,
            cancellationToken);

        if (!searchResult.Success || !searchResult.Results.Any())
        {
            return new SeriesAutoMatchResult
            {
                Success = false,
                Error = searchResult.Error ?? "No matches found",
                SeriesId = seriesId,
                Candidates = searchResult.Results
            };
        }

        var topMatch = searchResult.Results.First();
        var requiresReview = topMatch.ConfidenceScore < threshold;

        // Auto-match if above threshold
        if (!requiresReview)
        {
            var matchResult = await MatchSeriesAsync(
                seriesId,
                topMatch.ComicVineId,
                syncMetadata: true,
                createMissingIssues: true,
                cancellationToken);

            if (!matchResult.Success)
            {
                return new SeriesAutoMatchResult
                {
                    Success = false,
                    Error = matchResult.Error,
                    SeriesId = seriesId,
                    Candidates = searchResult.Results
                };
            }
        }

        return new SeriesAutoMatchResult
        {
            Success = true,
            SeriesId = seriesId,
            MatchedComicVineId = requiresReview ? null : topMatch.ComicVineId,
            ConfidenceScore = topMatch.ConfidenceScore,
            RequiresManualReview = requiresReview,
            Candidates = searchResult.Results
        };
    }

    /// <inheritdoc />
    public async Task<BulkMatchResult> AutoMatchAllSeriesAsync(
        int? confidenceThreshold = null,
        CancellationToken cancellationToken = default)
    {
        var cvSettings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        var threshold = confidenceThreshold ?? cvSettings?.AutoMatchThreshold ?? 85;

        // Get all unmatched series
        var unmatchedSeries = await _dbContext.Series
            .Where(s => s.ComicVineId == null)
            .ToListAsync(cancellationToken);

        var results = new List<SeriesAutoMatchResult>();
        var matched = 0;
        var requiresReview = 0;
        var failed = 0;

        foreach (var series in unmatchedSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await AutoMatchSeriesAsync(series.Id, cancellationToken);
            results.Add(result);

            if (result.Success)
            {
                if (result.RequiresManualReview)
                    requiresReview++;
                else
                    matched++;
            }
            else
            {
                failed++;
            }
        }

        return new BulkMatchResult
        {
            Success = true,
            TotalProcessed = unmatchedSeries.Count,
            Matched = matched,
            RequiresReview = requiresReview,
            Failed = failed,
            Results = results
        };
    }

    /// <inheritdoc />
    public async Task<bool> UnmatchSeriesAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null)
        {
            return false;
        }

        series.ComicVineId = null;
        series.ExternalId = null;
        series.ExternalSource = null;
        series.ComicVineUrl = null;
        series.ComicVinePublisherId = null;
        series.ComicVineLastUpdated = null;
        // Keep other metadata (overview, cover, etc.) - user may have customized
        series.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unmatched series {SeriesId} ({Title}) from ComicVine",
            seriesId, series.Title);

        return true;
    }

    /// <inheritdoc />
    public async Task<SeriesRefreshResult> RefreshSeriesMetadataAsync(
        int seriesId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new SeriesRefreshResult
            {
                Success = false,
                Error = $"Series with ID {seriesId} not found"
            };
        }

        if (!series.ComicVineId.HasValue)
        {
            return new SeriesRefreshResult
            {
                Success = false,
                Error = "Series is not matched to ComicVine"
            };
        }

        // Check if refresh is needed
        var cvSettings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        var refreshInterval = TimeSpan.FromDays(cvSettings?.RefreshIntervalDays ?? 7);

        if (!forceRefresh && series.MetadataLastRefreshed.HasValue)
        {
            var timeSinceRefresh = DateTime.UtcNow - series.MetadataLastRefreshed.Value;
            if (timeSinceRefresh < refreshInterval)
            {
                return new SeriesRefreshResult
                {
                    Success = true,
                    SeriesId = seriesId,
                    MetadataChanged = false,
                    LastRefreshed = series.MetadataLastRefreshed
                };
            }
        }

        // Fetch latest data from ComicVine
        var volume = await _comicVineClient.GetVolumeAsync(series.ComicVineId.Value, cancellationToken);
        if (!volume.Success || volume.Data == null)
        {
            return new SeriesRefreshResult
            {
                Success = false,
                Error = volume.Error ?? "Failed to fetch ComicVine volume"
            };
        }

        // Track changes
        var metadataChanged = false;
        if (series.Title != volume.Data.Name)
        {
            series.Title = volume.Data.Name;
            metadataChanged = true;
        }
        if (series.Overview != volume.Data.Description)
        {
            series.Overview = volume.Data.Description;
            metadataChanged = true;
        }

        ApplyVolumeMetadataToSeries(series, volume.Data);
        series.MetadataLastRefreshed = DateTime.UtcNow;

        // Sync issues
        var syncResult = await SyncIssuesFromComicVineInternalAsync(series, cancellationToken);

        // Update series status (only if not manually set)
        if (series.StatusSource != StatusSource.Manual)
        {
            var lastIssueDate = volume.Data.LastIssue != null 
                ? await GetIssueReleaseDateAsync(volume.Data.LastIssue.Id, cancellationToken)
                : null;
            var firstIssueDate = volume.Data.FirstIssue != null
                ? await GetIssueReleaseDateAsync(volume.Data.FirstIssue.Id, cancellationToken)
                : null;
                
            var (status, statusSource, statusReasons) = _statusDeterminer.DetermineStatusFromComicVine(
                volume.Data.Name,
                volume.Data.StartYear,
                volume.Data.IssueCount,
                firstIssueDate,
                lastIssueDate,
                ParseComicVineDate(volume.Data.DateLastUpdated));
                
            if (series.Status != status)
            {
                _logger.LogInformation("Updated series {SeriesId} ({Title}) status from {OldStatus} to {NewStatus}. Reasons: {Reasons}",
                    seriesId, series.Title, series.Status, status, string.Join("; ", statusReasons));
                series.Status = status;
                series.StatusSource = statusSource;
                metadataChanged = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SeriesRefreshResult
        {
            Success = true,
            SeriesId = seriesId,
            MetadataChanged = metadataChanged || syncResult.IssuesAdded > 0 || syncResult.IssuesUpdated > 0,
            IssuesAdded = syncResult.IssuesAdded,
            IssuesUpdated = syncResult.IssuesUpdated,
            LastRefreshed = series.MetadataLastRefreshed
        };
    }

    /// <inheritdoc />
    public async Task<SeriesAddResult> AddSeriesByComicVineIdAsync(
        int volumeId,
        string? rootFolder = null,
        bool monitored = true,
        SeriesMonitoringMode monitoringMode = SeriesMonitoringMode.AllIssues,
        CancellationToken cancellationToken = default)
    {
        // Check if series already exists
        var existingSeries = await _dbContext.Series
            .FirstOrDefaultAsync(s => s.ComicVineId == volumeId, cancellationToken);

        if (existingSeries != null)
        {
            return new SeriesAddResult
            {
                Success = false,
                Error = "Series already exists in library",
                AlreadyExists = true,
                ExistingSeriesId = existingSeries.Id,
                ComicVineId = volumeId,
                Title = existingSeries.Title
            };
        }

        // Fetch volume from ComicVine
        var volume = await _comicVineClient.GetVolumeAsync(volumeId, cancellationToken);
        if (!volume.Success || volume.Data == null)
        {
            return new SeriesAddResult
            {
                Success = false,
                Error = volume.Error ?? "Failed to fetch ComicVine volume"
            };
        }

        // Create new series
        var series = new Series
        {
            Title = volume.Data.Name,
            SortTitle = GenerateSortTitle(volume.Data.Name),
            Publisher = volume.Data.Publisher?.Name,
            StartYear = volume.Data.StartYear,
            Overview = volume.Data.Description,
            ComicVineId = volumeId,
            ExternalId = volumeId.ToString(),
            ExternalSource = "ComicVine",
            ComicVineUrl = volume.Data.SiteDetailUrl,
            ComicVinePublisherId = volume.Data.Publisher?.Id,
            CoverImageUrl = volume.Data.Image?.MediumUrl ?? volume.Data.Image?.SmallUrl,
            TotalIssueCount = volume.Data.IssueCount,
            Aliases = volume.Data.Aliases.Any() ? string.Join("\n", volume.Data.Aliases) : null,
            ComicVineLastUpdated = ParseComicVineDate(volume.Data.DateLastUpdated),
            MetadataLastRefreshed = DateTime.UtcNow,
            Monitored = monitored,
            // Status will be set after we determine it from available data
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.ComicVine,
            Path = rootFolder != null ? System.IO.Path.Combine(rootFolder, SanitizeFolderName(volume.Data.Name)) : null
        };

        // Determine series status from ComicVine data
        var lastIssueDate = volume.Data.LastIssue != null 
            ? await GetIssueReleaseDateAsync(volume.Data.LastIssue.Id, cancellationToken)
            : null;
        var firstIssueDate = volume.Data.FirstIssue != null
            ? await GetIssueReleaseDateAsync(volume.Data.FirstIssue.Id, cancellationToken)
            : null;
            
        var (status, statusSource, statusReasons) = _statusDeterminer.DetermineStatusFromComicVine(
            volume.Data.Name,
            volume.Data.StartYear,
            volume.Data.IssueCount,
            firstIssueDate,
            lastIssueDate,
            ParseComicVineDate(volume.Data.DateLastUpdated));
            
        series.Status = status;
        series.StatusSource = statusSource;
        
        _logger.LogInformation("Determined series status: {Status} (source: {Source}). Reasons: {Reasons}",
            status, statusSource, string.Join("; ", statusReasons));

        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Sync issues
        var syncResult = await SyncIssuesFromComicVineInternalAsync(series, cancellationToken, monitoringMode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added series {SeriesId} ({Title}) from ComicVine volume {ComicVineId} with {IssueCount} issues",
            series.Id, series.Title, volumeId, syncResult.IssuesAdded);

        // Detect and link annual series (Mylar3 parity)
        var linkedAnnualSeriesIds = new List<int>();
        
        // Check if this series is an annual series (e.g., "Batman Annual")
        var annualMatch = AnnualSeriesPattern.Match(series.Title);
        if (annualMatch.Success)
        {
            // This is an annual series - try to find and link to parent
            var parentName = annualMatch.Groups[1].Value.Trim();
            series.SeriesType = SeriesType.Annual;
            
            var parentSeries = await _dbContext.Series
                .Where(s => s.Title == parentName && s.StartYear == series.StartYear && s.Publisher == series.Publisher)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (parentSeries == null)
            {
                // Try without exact year match
                parentSeries = await _dbContext.Series
                    .Where(s => s.Title == parentName && s.Publisher == series.Publisher)
                    .OrderByDescending(s => s.StartYear.HasValue && series.StartYear.HasValue && 
                                            Math.Abs(s.StartYear.Value - series.StartYear.Value) <= 2)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            
            if (parentSeries != null)
            {
                series.ParentSeriesId = parentSeries.Id;
                _logger.LogInformation("Linked annual series {AnnualTitle} to parent series {ParentTitle}",
                    series.Title, parentSeries.Title);
            }
            else
            {
                _logger.LogInformation("Annual series {Title} added, but no parent series found in library", series.Title);
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Check if series-annual integration is enabled (defaults to true)
            var pullListSettings = await GetPullListSettingsAsync(cancellationToken);
            if (pullListSettings.EnableSeriesAnnualIntegration ?? true)
            {
                // This is a regular series - link existing annual series and auto-add from ComicVine
                linkedAnnualSeriesIds = await TryLinkExistingAnnualSeriesAsync(series, cancellationToken);
                
                // Also search ComicVine for annual series and automatically add them (Mylar3 parity)
                var autoAddedIds = await AutoAddAnnualSeriesFromComicVineAsync(series, rootFolder, monitored, monitoringMode, cancellationToken);
                linkedAnnualSeriesIds.AddRange(autoAddedIds);
            }
            else
            {
                _logger.LogDebug("Series-annual integration is disabled, skipping automatic annual linking for {Title}", series.Title);
            }
        }

        return new SeriesAddResult
        {
            Success = true,
            SeriesId = series.Id,
            ComicVineId = volumeId,
            Title = series.Title,
            IssuesCreated = syncResult.IssuesAdded,
            LinkedAnnualSeriesIds = linkedAnnualSeriesIds
        };
    }
    
    /// <summary>
    /// Searches for and links existing annual series to a parent series.
    /// </summary>
    private async Task<List<int>> TryLinkExistingAnnualSeriesAsync(Series parentSeries, CancellationToken cancellationToken)
    {
        var linkedIds = new List<int>();
        
        // Look for annual series that match this parent (e.g., "Batman" -> find "Batman Annual")
        var annualSearchName = $"{parentSeries.Title} Annual";
        
        var existingAnnuals = await _dbContext.Series
            .Where(s => s.Title.Contains(parentSeries.Title) && 
                       s.Title.Contains("Annual") &&
                       s.ParentSeriesId == null &&
                       s.Publisher == parentSeries.Publisher)
            .ToListAsync(cancellationToken);
        
        foreach (var annual in existingAnnuals)
        {
            // Verify this is actually an annual for this parent
            var match = AnnualSeriesPattern.Match(annual.Title);
            if (match.Success && match.Groups[1].Value.Trim().Equals(parentSeries.Title, StringComparison.OrdinalIgnoreCase))
            {
                annual.ParentSeriesId = parentSeries.Id;
                annual.SeriesType = SeriesType.Annual;
                linkedIds.Add(annual.Id);
                
                _logger.LogInformation("Linked existing annual series {AnnualTitle} to parent series {ParentTitle}",
                    annual.Title, parentSeries.Title);
            }
        }
        
        if (linkedIds.Any())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        
        return linkedIds;
    }
    
    /// <summary>
    /// Automatically searches ComicVine for annual series related to a parent series and adds them.
    /// This provides seamless Mylar3 parity where adding "Batman" also adds "Batman Annual".
    /// </summary>
    private async Task<List<int>> AutoAddAnnualSeriesFromComicVineAsync(
        Series parentSeries,
        string? rootFolder,
        bool monitored,
        SeriesMonitoringMode monitoringMode,
        CancellationToken cancellationToken)
    {
        var addedIds = new List<int>();
        
        try
        {
            // Search ComicVine for "{Title} Annual"
            var searchQuery = $"{parentSeries.Title} Annual";
            var searchResult = await _comicVineClient.SearchVolumesAsync(searchQuery, 1, 10, cancellationToken);
            
            if (!searchResult.Success || !searchResult.Results.Any())
            {
                _logger.LogDebug("No annual series found on ComicVine for {ParentTitle}", parentSeries.Title);
                return addedIds;
            }
            
            // Find volumes that match the pattern "{ParentTitle} Annual" with same publisher
            foreach (var volume in searchResult.Results)
            {
                // Skip if already in library
                var exists = await _dbContext.Series.AnyAsync(s => s.ComicVineId == volume.Id, cancellationToken);
                if (exists) continue;
                
                // Check if this is actually an annual series for our parent
                var match = AnnualSeriesPattern.Match(volume.Name);
                if (!match.Success) continue;
                
                var extractedParentName = match.Groups[1].Value.Trim();
                if (!extractedParentName.Equals(parentSeries.Title, StringComparison.OrdinalIgnoreCase)) continue;
                
                // Check publisher match (if available)
                if (volume.Publisher?.Name != null && parentSeries.Publisher != null &&
                    !volume.Publisher.Name.Equals(parentSeries.Publisher, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                // Check year proximity (annual series should be within 2 years of parent)
                if (volume.StartYear.HasValue && parentSeries.StartYear.HasValue &&
                    Math.Abs(volume.StartYear.Value - parentSeries.StartYear.Value) > 2)
                {
                    continue;
                }
                
                _logger.LogInformation("Auto-adding annual series {AnnualTitle} for parent {ParentTitle}",
                    volume.Name, parentSeries.Title);
                
                // Add the annual series
                var annualSeries = new Series
                {
                    Title = volume.Name,
                    SortTitle = GenerateSortTitle(volume.Name),
                    Publisher = volume.Publisher?.Name,
                    StartYear = volume.StartYear,
                    Overview = volume.Description,
                    ComicVineId = volume.Id,
                    ExternalId = volume.Id.ToString(),
                    ExternalSource = "ComicVine",
                    ComicVineUrl = volume.SiteDetailUrl,
                    ComicVinePublisherId = volume.Publisher?.Id,
                    CoverImageUrl = volume.Image?.MediumUrl ?? volume.Image?.SmallUrl,
                    TotalIssueCount = volume.IssueCount,
                    Aliases = volume.Aliases.Any() ? string.Join("\n", volume.Aliases) : null,
                    MetadataLastRefreshed = DateTime.UtcNow,
                    Monitored = monitored,
                    Status = SeriesStatus.Continuing,
                    StatusSource = StatusSource.ComicVine,
                    Path = rootFolder != null ? System.IO.Path.Combine(rootFolder, SanitizeFolderName(volume.Name)) : null,
                    // Link to parent
                    SeriesType = SeriesType.Annual,
                    ParentSeriesId = parentSeries.Id
                };
                
                _dbContext.Series.Add(annualSeries);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                // Sync issues for the annual series
                var syncResult = await SyncIssuesFromComicVineInternalAsync(annualSeries, cancellationToken, monitoringMode);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                addedIds.Add(annualSeries.Id);
                
                _logger.LogInformation("Added annual series {SeriesId} ({Title}) with {IssueCount} issues, linked to parent {ParentTitle}",
                    annualSeries.Id, annualSeries.Title, syncResult.IssuesAdded, parentSeries.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error auto-adding annual series for {ParentTitle}. Manual addition may be required.",
                parentSeries.Title);
        }
        
        return addedIds;
    }
    
    /// <summary>
    /// Searches ComicVine for annual series related to a parent series.
    /// Returns candidates that can be added to the library.
    /// </summary>
    public async Task<List<SeriesMatchCandidate>> SearchForAnnualSeriesAsync(
        int parentSeriesId,
        CancellationToken cancellationToken = default)
    {
        var parentSeries = await _dbContext.Series.FindAsync(new object[] { parentSeriesId }, cancellationToken);
        if (parentSeries == null)
        {
            return new List<SeriesMatchCandidate>();
        }
        
        var candidates = new List<SeriesMatchCandidate>();
        
        // Search for "[Title] Annual" on ComicVine
        var searchQuery = $"{parentSeries.Title} Annual";
        var searchResult = await SearchSeriesAsync(searchQuery, parentSeries.Publisher, parentSeries.StartYear, null, 1, 20, cancellationToken);
        
        if (searchResult.Success)
        {
            foreach (var candidate in searchResult.Results)
            {
                // Filter to only include actual annual series
                var match = AnnualSeriesPattern.Match(candidate.Title);
                if (match.Success)
                {
                    var parentName = match.Groups[1].Value.Trim();
                    
                    // Check if it matches our parent series
                    if (parentName.Equals(parentSeries.Title, StringComparison.OrdinalIgnoreCase) ||
                        NormalizeTitle(parentName).Equals(NormalizeTitle(parentSeries.Title), StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if not already in library
                        var exists = await _dbContext.Series.AnyAsync(s => s.ComicVineId == candidate.ComicVineId, cancellationToken);
                        if (!exists)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }
        }
        
        return candidates;
    }
    
    /// <summary>
    /// Adds annual series from ComicVine and links them to the parent series.
    /// </summary>
    public async Task<SeriesAddResult> AddAnnualSeriesAsync(
        int parentSeriesId,
        int annualVolumeId,
        CancellationToken cancellationToken = default)
    {
        var parentSeries = await _dbContext.Series.FindAsync(new object[] { parentSeriesId }, cancellationToken);
        if (parentSeries == null)
        {
            return new SeriesAddResult
            {
                Success = false,
                Error = $"Parent series with ID {parentSeriesId} not found"
            };
        }
        
        // Add the annual series
        var result = await AddSeriesByComicVineIdAsync(
            annualVolumeId,
            parentSeries.Path, // Use same root folder as parent
            parentSeries.Monitored,
            parentSeries.MonitoringMode,
            cancellationToken);
        
        if (result.Success && result.SeriesId.HasValue)
        {
            // Force link to parent (in case auto-detection failed)
            var annualSeries = await _dbContext.Series.FindAsync(new object[] { result.SeriesId.Value }, cancellationToken);
            if (annualSeries != null && annualSeries.ParentSeriesId != parentSeriesId)
            {
                annualSeries.ParentSeriesId = parentSeriesId;
                annualSeries.SeriesType = SeriesType.Annual;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Scans all existing series in the library and links annual series to their parents.
    /// Call this to update existing series that were added before the annual linking feature.
    /// </summary>
    public async Task<AnnualLinkingResult> LinkExistingAnnualSeriesAsync(CancellationToken cancellationToken = default)
    {
        var result = new AnnualLinkingResult();
        
        // Get all series that might be annuals (contain "Annual" in title)
        var allSeries = await _dbContext.Series.ToListAsync(cancellationToken);
        
        var potentialAnnuals = allSeries
            .Where(s => AnnualSeriesPattern.IsMatch(s.Title) && s.ParentSeriesId == null)
            .ToList();
        
        var regularSeries = allSeries
            .Where(s => !AnnualSeriesPattern.IsMatch(s.Title))
            .ToList();
        
        _logger.LogInformation("Scanning {AnnualCount} potential annual series for linking", potentialAnnuals.Count);
        
        foreach (var annual in potentialAnnuals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var match = AnnualSeriesPattern.Match(annual.Title);
            if (!match.Success) continue;
            
            var parentName = match.Groups[1].Value.Trim();
            
            // Try to find exact match first
            var parent = regularSeries.FirstOrDefault(s => 
                s.Title.Equals(parentName, StringComparison.OrdinalIgnoreCase) &&
                s.Publisher == annual.Publisher);
            
            // If no exact match, try fuzzy match with year
            if (parent == null && annual.StartYear.HasValue)
            {
                parent = regularSeries.FirstOrDefault(s =>
                    s.Title.Equals(parentName, StringComparison.OrdinalIgnoreCase) &&
                    s.StartYear.HasValue &&
                    Math.Abs(s.StartYear.Value - annual.StartYear.Value) <= 2);
            }
            
            // Last resort: just match by name
            if (parent == null)
            {
                parent = regularSeries.FirstOrDefault(s =>
                    s.Title.Equals(parentName, StringComparison.OrdinalIgnoreCase));
            }
            
            if (parent != null)
            {
                annual.ParentSeriesId = parent.Id;
                annual.SeriesType = SeriesType.Annual;
                result.LinkedCount++;
                result.Links.Add(new AnnualLink
                {
                    AnnualSeriesId = annual.Id,
                    AnnualSeriesTitle = annual.Title,
                    ParentSeriesId = parent.Id,
                    ParentSeriesTitle = parent.Title
                });
                
                _logger.LogInformation("Linked annual series '{AnnualTitle}' to parent '{ParentTitle}'",
                    annual.Title, parent.Title);
            }
            else
            {
                result.UnlinkedAnnuals.Add(new UnlinkedAnnual
                {
                    SeriesId = annual.Id,
                    Title = annual.Title,
                    ExpectedParentName = parentName
                });
                
                _logger.LogDebug("Could not find parent for annual series '{AnnualTitle}' (expected: '{ParentName}')",
                    annual.Title, parentName);
            }
        }
        
        if (result.LinkedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        
        result.Success = true;
        result.TotalScanned = potentialAnnuals.Count;
        
        _logger.LogInformation("Annual linking complete: {Linked}/{Total} series linked",
            result.LinkedCount, result.TotalScanned);
        
        return result;
    }
    
    /// <summary>
    /// Unlinks all annual series from their parents.
    /// Called when series-annual integration is disabled.
    /// </summary>
    public async Task<AnnualUnlinkingResult> UnlinkAllAnnualSeriesAsync(CancellationToken cancellationToken = default)
    {
        var result = new AnnualUnlinkingResult();
        
        try
        {
            // Find all series that are currently linked to a parent
            var linkedAnnuals = await _dbContext.Series
                .Include(s => s.ParentSeries)
                .Where(s => s.ParentSeriesId != null)
                .ToListAsync(cancellationToken);
            
            _logger.LogInformation("Found {Count} linked annual series to unlink", linkedAnnuals.Count);
            
            foreach (var annual in linkedAnnuals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                result.UnlinkedSeries.Add(new UnlinkedSeriesInfo
                {
                    SeriesId = annual.Id,
                    Title = annual.Title,
                    FormerParentSeriesId = annual.ParentSeriesId,
                    FormerParentTitle = annual.ParentSeries?.Title ?? ""
                });
                
                _logger.LogInformation("Unlinking annual series '{AnnualTitle}' from parent '{ParentTitle}'",
                    annual.Title, annual.ParentSeries?.Title ?? "unknown");
                
                // Clear the parent link but keep the SeriesType as Annual for reference
                annual.ParentSeriesId = null;
            }
            
            if (linkedAnnuals.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            
            result.Success = true;
            result.UnlinkedCount = linkedAnnuals.Count;
            
            _logger.LogInformation("Annual unlinking complete: {Count} series unlinked", result.UnlinkedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlink annual series");
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    /// <summary>
    /// For a specific series, detect if it's an annual and try to link it.
    /// </summary>
    public async Task<bool> TryLinkSingleSeriesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null) return false;
        
        var match = AnnualSeriesPattern.Match(series.Title);
        if (!match.Success) return false;
        
        var parentName = match.Groups[1].Value.Trim();
        
        var parent = await _dbContext.Series
            .Where(s => s.Title == parentName && s.Publisher == series.Publisher && s.Id != seriesId)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (parent == null)
        {
            parent = await _dbContext.Series
                .Where(s => s.Title == parentName && s.Id != seriesId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        
        if (parent != null)
        {
            series.ParentSeriesId = parent.Id;
            series.SeriesType = SeriesType.Annual;
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Linked series '{Title}' to parent '{ParentTitle}'",
                series.Title, parent.Title);
            return true;
        }
        
        return false;
    }

    /// <inheritdoc />
    public async Task<IssueSyncResult> SyncIssuesFromComicVineAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new IssueSyncResult
            {
                Success = false,
                Error = $"Series with ID {seriesId} not found"
            };
        }

        if (!series.ComicVineId.HasValue)
        {
            return new IssueSyncResult
            {
                Success = false,
                Error = "Series is not matched to ComicVine"
            };
        }

        var result = await SyncIssuesFromComicVineInternalAsync(series, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    #region Private Methods

    /// <summary>
    /// Gets the release date of an issue from ComicVine.
    /// </summary>
    private async Task<DateTime?> GetIssueReleaseDateAsync(int issueId, CancellationToken cancellationToken)
    {
        try
        {
            var issueResult = await _comicVineClient.GetIssueAsync(issueId, cancellationToken);
            if (issueResult.Success && issueResult.Data != null)
            {
                // Prefer store date, then cover date
                return issueResult.Data.StoreDate ?? issueResult.Data.CoverDate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get release date for issue {IssueId}", issueId);
        }
        return null;
    }

    private async Task<IssueSyncResult> SyncIssuesFromComicVineInternalAsync(
        Series series,
        CancellationToken cancellationToken,
        SeriesMonitoringMode? monitoringMode = null)
    {
        if (!series.ComicVineId.HasValue)
        {
            return new IssueSyncResult { Success = false, Error = "Series not matched" };
        }

        var existingIssues = series.Issues?.ToList() ?? new List<Issue>();
        var existingByComicVineId = existingIssues
            .Where(i => i.ComicVineId.HasValue)
            .ToDictionary(i => i.ComicVineId!.Value);
        var existingByNumber = existingIssues
            .Where(i => !i.ComicVineId.HasValue)
            .GroupBy(i => i.IssueNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var issuesAdded = 0;
        var issuesUpdated = 0;
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            var issuesResult = await _comicVineClient.GetVolumeIssuesAsync(
                series.ComicVineId.Value, page, pageSize, cancellationToken);

            if (!issuesResult.Success)
            {
                break;
            }

            foreach (var cvIssue in issuesResult.Results)
            {
                // Try to find existing issue
                Issue? issue = null;
                
                if (existingByComicVineId.TryGetValue(cvIssue.Id, out var existingById))
                {
                    issue = existingById;
                }
                else if (TryParseIssueNumber(cvIssue.IssueNumber, out var number) &&
                         existingByNumber.TryGetValue(number, out var existingByNum))
                {
                    issue = existingByNum;
                }

                if (issue != null)
                {
                    // Update existing issue
                    UpdateIssueFromComicVine(issue, cvIssue, series.Title);
                    issuesUpdated++;
                }
                else
                {
                    // Create new issue
                    issue = CreateIssueFromComicVine(series.Id, cvIssue, series.Title);
                    
                    // Set monitoring based on mode
                    if (monitoringMode == SeriesMonitoringMode.Manual)
                    {
                        issue.Monitored = false;
                    }
                    else if (monitoringMode == SeriesMonitoringMode.FutureIssues)
                    {
                        issue.Monitored = cvIssue.StoreDate > DateTime.UtcNow;
                    }
                    else if (monitoringMode == SeriesMonitoringMode.FirstIssue)
                    {
                        issue.Monitored = cvIssue.IssueNumber == "1" || cvIssue.IssueNumber == "0";
                    }

                    _dbContext.Issues.Add(issue);
                    issuesAdded++;
                }
            }

            // Check if we've fetched all issues
            if (issuesResult.Results.Count < pageSize)
            {
                break;
            }

            page++;
        }

        return new IssueSyncResult
        {
            Success = true,
            SeriesId = series.Id,
            IssuesAdded = issuesAdded,
            IssuesUpdated = issuesUpdated,
            TotalIssues = existingIssues.Count + issuesAdded
        };
    }

    private SeriesMatchCandidate MapVolumeToCandidate(
        ComicVineVolume volume,
        string? searchQuery,
        string? filterPublisher,
        int? filterYear)
    {
        var confidence = CalculateConfidenceScore(volume, searchQuery, filterPublisher, filterYear, out var reasons);

        return new SeriesMatchCandidate
        {
            ComicVineId = volume.Id,
            Title = volume.Name,
            Aliases = volume.Aliases,
            Publisher = volume.Publisher?.Name,
            PublisherId = volume.Publisher?.Id,
            StartYear = volume.StartYear,
            Description = volume.Description,
            IssueCount = volume.IssueCount,
            CoverImageUrl = volume.Image?.MediumUrl ?? volume.Image?.SmallUrl,
            ComicVineUrl = volume.SiteDetailUrl,
            ConfidenceScore = confidence,
            ConfidenceReasons = reasons
        };
    }

    private int CalculateConfidenceScore(
        ComicVineVolume volume,
        string? searchQuery,
        string? filterPublisher,
        int? filterYear,
        out List<string> reasons)
    {
        reasons = new List<string>();
        var score = 50; // Base score

        if (string.IsNullOrEmpty(searchQuery))
        {
            reasons.Add("No search query for comparison");
            return score;
        }

        var normalizedQuery = NormalizeTitle(searchQuery);
        var normalizedTitle = NormalizeTitle(volume.Name);

        // Exact title match
        if (normalizedTitle.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
            reasons.Add("Exact title match (+40)");
        }
        // Title starts with query
        else if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
            reasons.Add("Title starts with query (+25)");
        }
        // Title contains query
        else if (normalizedTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
            reasons.Add("Title contains query (+15)");
        }
        // Check aliases
        else if (volume.Aliases.Any(a => NormalizeTitle(a).Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 35;
            reasons.Add("Alias exact match (+35)");
        }

        // Publisher match
        if (!string.IsNullOrEmpty(filterPublisher) && 
            volume.Publisher?.Name?.Contains(filterPublisher, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 10;
            reasons.Add("Publisher match (+10)");
        }

        // Year match
        if (filterYear.HasValue && volume.StartYear == filterYear)
        {
            score += 10;
            reasons.Add("Year exact match (+10)");
        }
        else if (filterYear.HasValue && volume.StartYear.HasValue &&
                 Math.Abs(volume.StartYear.Value - filterYear.Value) <= 2)
        {
            score += 5;
            reasons.Add("Year close match (+5)");
        }

        // Issue count bonus (more issues = more established series)
        if (volume.IssueCount > 50)
        {
            score += 5;
            reasons.Add("Large issue count (+5)");
        }

        // Cap at 100
        return Math.Min(100, score);
    }

    private void ApplyVolumeMetadataToSeries(Series series, ComicVineVolume volume)
    {
        series.Publisher = volume.Publisher?.Name ?? series.Publisher;
        series.ComicVinePublisherId = volume.Publisher?.Id;
        series.StartYear = volume.StartYear ?? series.StartYear;
        series.Overview = volume.Description ?? series.Overview;
        series.Aliases = volume.Aliases.Any() ? string.Join("\n", volume.Aliases) : series.Aliases;
        series.CoverImageUrl = volume.Image?.MediumUrl ?? volume.Image?.SmallUrl ?? series.CoverImageUrl;
        series.TotalIssueCount = volume.IssueCount;
        series.ComicVineLastUpdated = ParseComicVineDate(volume.DateLastUpdated);
        series.UpdatedAt = DateTime.UtcNow;
    }

    private Issue CreateIssueFromComicVine(int seriesId, ComicVineIssue cvIssue, string? seriesTitle = null)
    {
        TryParseIssueNumber(cvIssue.IssueNumber, out var issueNumber);
        
        // Detect special issue types
        var (isAnnual, isSpecial, specialType) = DetectSpecialIssueType(cvIssue.IssueNumber, cvIssue.Name, seriesTitle);

        return new Issue
        {
            SeriesId = seriesId,
            IssueNumber = issueNumber,
            IssueNumberText = cvIssue.IssueNumber,
            Title = cvIssue.Name,
            Overview = cvIssue.Description,
            ReleaseDate = cvIssue.StoreDate ?? cvIssue.CoverDate,
            StoreDate = cvIssue.StoreDate,
            CoverDate = cvIssue.CoverDate,
            ComicVineId = cvIssue.Id,
            ExternalId = cvIssue.Id.ToString(),
            ExternalSource = "ComicVine",
            ComicVineUrl = cvIssue.SiteDetailUrl,
            CoverImageUrl = cvIssue.Image?.MediumUrl ?? cvIssue.Image?.SmallUrl,
            MetadataLastRefreshed = DateTime.UtcNow,
            Monitored = true,
            IsAnnual = isAnnual,
            IsSpecial = isSpecial,
            SpecialType = specialType
        };
    }

    private void UpdateIssueFromComicVine(Issue issue, ComicVineIssue cvIssue, string? seriesTitle = null)
    {
        TryParseIssueNumber(cvIssue.IssueNumber, out var issueNumber);
        
        // Detect special issue types
        var (isAnnual, isSpecial, specialType) = DetectSpecialIssueType(cvIssue.IssueNumber, cvIssue.Name, seriesTitle);

        issue.IssueNumber = issueNumber;
        issue.IssueNumberText = cvIssue.IssueNumber;
        issue.Title = cvIssue.Name;
        issue.Overview = cvIssue.Description;
        issue.ReleaseDate = cvIssue.StoreDate ?? cvIssue.CoverDate ?? issue.ReleaseDate;
        issue.StoreDate = cvIssue.StoreDate;
        issue.CoverDate = cvIssue.CoverDate;
        issue.ComicVineId = cvIssue.Id;
        issue.ExternalId = cvIssue.Id.ToString();
        issue.ExternalSource = "ComicVine";
        issue.ComicVineUrl = cvIssue.SiteDetailUrl;
        issue.CoverImageUrl = cvIssue.Image?.MediumUrl ?? cvIssue.Image?.SmallUrl;
        issue.MetadataLastRefreshed = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.IsAnnual = isAnnual;
        issue.IsSpecial = isSpecial;
        issue.SpecialType = specialType;
    }

    private static bool TryParseIssueNumber(string? issueNumberText, out decimal issueNumber)
    {
        issueNumber = 0;

        if (string.IsNullOrWhiteSpace(issueNumberText))
            return false;

        // Handle special cases
        if (issueNumberText == "½")
        {
            issueNumber = 0.5m;
            return true;
        }

        // Try direct parse
        if (decimal.TryParse(issueNumberText, out issueNumber))
            return true;

        // Handle formats like "1a", "1b", etc.
        var match = Regex.Match(
            issueNumberText, @"^(\d+(?:\.\d+)?)", RegexOptions.None);
        
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out issueNumber))
            return true;

        return false;
    }
    
    /// <summary>
    /// Detects if an issue is an annual or special issue based on its number, title, and series title.
    /// </summary>
    private static (bool IsAnnual, bool IsSpecial, string? SpecialType) DetectSpecialIssueType(
        string? issueNumber, 
        string? title,
        string? seriesTitle = null)
    {
        var textToCheck = $"{issueNumber} {title}".Trim();
        
        // Check for annual - first check the issue text, then the series title
        if (AnnualPattern.IsMatch(textToCheck))
        {
            return (true, false, null);
        }
        
        // If the series itself is an annual series (e.g., "Batman Annual", "Absolute Batman Annual")
        // mark all its issues as annuals
        if (!string.IsNullOrEmpty(seriesTitle) && AnnualPattern.IsMatch(seriesTitle))
        {
            return (true, false, null);
        }

        // Check for other special types
        var specialMatch = SpecialPattern.Match(textToCheck);
        if (specialMatch.Success)
        {
            var specialType = specialMatch.Groups[1].Value.Trim();
            return (false, true, NormalizeSpecialType(specialType));
        }

        // Check for negative issue numbers (often specials)
        if (decimal.TryParse(issueNumber, out var num) && num < 0)
        {
            return (false, true, "Preview");
        }

        return (false, false, null);
    }
    
    private static string NormalizeSpecialType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "special" => "Special",
            "one-shot" or "oneshot" => "One-Shot",
            "giant-size" or "giantsize" or "giant size" => "Giant-Size",
            "king-size" or "kingsize" or "king size" => "King-Size",
            "80-page giant" or "80 page giant" => "80-Page Giant",
            "100-page" or "100 page" => "100-Page",
            "preview" => "Preview",
            "prologue" => "Prologue",
            "epilogue" => "Epilogue",
            "finale" => "Finale",
            "infinity" => "Infinity",
            _ => type
        };
    }

    /// <summary>
    /// Parses ComicVine date format "YYYY-MM-DD HH:MM:SS" to DateTime.
    /// </summary>
    private static DateTime? ParseComicVineDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // ComicVine returns dates in format "YYYY-MM-DD HH:MM:SS"
        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Fallback to general parsing
        if (DateTime.TryParse(dateString, out result))
        {
            return result;
        }

        return null;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return "";

        // Remove common prefixes
        var result = title;
        var prefixes = new[] { "The ", "A ", "An " };
        foreach (var prefix in prefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result = result[prefix.Length..];
                break;
            }
        }

        // Remove special characters
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^\w\s]", "");
        
        // Normalize whitespace
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result.ToLowerInvariant();
    }

    private static string GenerateSortTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return "";

        var result = title;
        var prefixes = new[] { "The ", "A ", "An " };
        foreach (var prefix in prefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result = result[prefix.Length..] + ", " + prefix.Trim();
                break;
            }
        }

        return result;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var result = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return result.Trim();
    }

    #endregion
}

