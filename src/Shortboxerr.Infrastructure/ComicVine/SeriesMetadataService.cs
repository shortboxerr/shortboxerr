using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
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

        return new SeriesAddResult
        {
            Success = true,
            SeriesId = series.Id,
            ComicVineId = volumeId,
            Title = series.Title,
            IssuesCreated = syncResult.IssuesAdded
        };
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
                    UpdateIssueFromComicVine(issue, cvIssue);
                    issuesUpdated++;
                }
                else
                {
                    // Create new issue
                    issue = CreateIssueFromComicVine(series.Id, cvIssue);
                    
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

    private Issue CreateIssueFromComicVine(int seriesId, ComicVineIssue cvIssue)
    {
        TryParseIssueNumber(cvIssue.IssueNumber, out var issueNumber);

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
            Monitored = true
        };
    }

    private void UpdateIssueFromComicVine(Issue issue, ComicVineIssue cvIssue)
    {
        TryParseIssueNumber(cvIssue.IssueNumber, out var issueNumber);

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
        var match = System.Text.RegularExpressions.Regex.Match(
            issueNumberText, @"^(\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.None);
        
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out issueNumber))
            return true;

        return false;
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

