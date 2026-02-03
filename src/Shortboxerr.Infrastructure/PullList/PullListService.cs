using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.PullList;

/// <summary>
/// Implementation of IPullListService for managing the weekly pull list.
/// </summary>
public class PullListService : IPullListService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly IComicVineClient _comicVineClient;
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PullListService> _logger;

    // Settings key
    private const string PullListSettingsKey = "pulllist";
    private const string SeriesSettingsKey = "pulllist_series";
    private const string DiscoveryCacheKey = "pulllist_discovery_{0}";
    
    // Comics typically release on Wednesday in the US
    private const DayOfWeek DefaultReleaseDay = DayOfWeek.Wednesday;
    
    // Cache discovery results for 30 minutes to minimize API calls
    private static readonly TimeSpan DiscoveryCacheDuration = TimeSpan.FromMinutes(30);

    public PullListService(
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        IComicVineClient comicVineClient,
        ISeriesMetadataService seriesMetadataService,
        IMemoryCache cache,
        ILogger<PullListService> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _comicVineClient = comicVineClient;
        _seriesMetadataService = seriesMetadataService;
        _cache = cache;
        _logger = logger;
    }

    #region Calendar & Release Tracking

    public async Task<WeeklyPullList> GetWeeklyReleasesAsync(
        DateTime weekOf,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetWeekBoundaries(weekOf);
        var releaseDay = GetReleaseDay(weekStart);

        var query = BuildIssueQuery(filter)
            .Where(i => i.StoreDate >= weekStart && i.StoreDate < weekEnd);

        // Note: SQLite doesn't support ORDER BY decimal, so we sort in memory
        var issues = await query
            .OrderBy(i => i.StoreDate)
            .ThenBy(i => i.Series!.Title)
            .ToListAsync(cancellationToken);
        
        // Sort by IssueNumber in memory (SQLite decimal limitation)
        issues = issues.OrderBy(i => i.StoreDate)
            .ThenBy(i => i.Series?.Title)
            .ThenBy(i => i.IssueNumber)
            .ToList();

        return new WeeklyPullList
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            ReleaseDay = releaseDay,
            Issues = issues.Select(MapToPullListIssue).ToList()
        };
    }

    public async Task<WeeklyPullList> GetThisWeekAsync(
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await GetWeeklyReleasesAsync(DateTime.Today, filter, cancellationToken);
    }

    public async Task<List<WeeklyPullList>> GetUpcomingReleasesAsync(
        int weeks = 4,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<WeeklyPullList>();
        var currentWeek = GetWeekStart(DateTime.Today);

        for (int i = 1; i <= weeks; i++)
        {
            var weekStart = currentWeek.AddDays(7 * i);
            var weekList = await GetWeeklyReleasesAsync(weekStart, filter, cancellationToken);
            result.Add(weekList);
        }

        return result;
    }

    public async Task<List<WeeklyPullList>> GetPastReleasesAsync(
        int weeks = 4,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<WeeklyPullList>();
        var currentWeek = GetWeekStart(DateTime.Today);

        for (int i = 1; i <= weeks; i++)
        {
            var weekStart = currentWeek.AddDays(-7 * i);
            var weekList = await GetWeeklyReleasesAsync(weekStart, filter, cancellationToken);
            result.Add(weekList);
        }

        return result;
    }

    public async Task<ReleaseCalendar> GetCalendarAsync(
        DateTime startDate,
        DateTime endDate,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildIssueQuery(filter)
            .Where(i => i.StoreDate >= startDate && i.StoreDate < endDate);

        var issues = await query
            .OrderBy(i => i.StoreDate)
            .ToListAsync(cancellationToken);

        var pullListIssues = issues.Select(MapToPullListIssue).ToList();

        // Group by day
        var days = new List<CalendarDay>();
        for (var date = startDate.Date; date < endDate.Date; date = date.AddDays(1))
        {
            var dayIssues = pullListIssues.Where(i => i.StoreDate?.Date == date).ToList();
            days.Add(new CalendarDay
            {
                Date = date,
                IsReleaseDay = date.DayOfWeek == DefaultReleaseDay,
                Issues = dayIssues
            });
        }

        // Group by publisher
        var byPublisher = pullListIssues
            .Where(i => !string.IsNullOrEmpty(i.Publisher))
            .GroupBy(i => i.Publisher!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group by series
        var bySeries = pullListIssues
            .GroupBy(i => i.SeriesId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new ReleaseCalendar
        {
            StartDate = startDate,
            EndDate = endDate,
            Days = days,
            ByPublisher = byPublisher,
            BySeries = bySeries
        };
    }

    #endregion

    #region Issue Management

    public async Task<PullListActionResult> MarkAsWantedAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        return await UpdateIssueStatusAsync(issueId, IssueStatus.Wanted, cancellationToken);
    }

    public async Task<PullListActionResult> MarkAsOwnedAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        return await UpdateIssueStatusAsync(issueId, IssueStatus.Owned, cancellationToken);
    }

    public async Task<PullListActionResult> MarkAsSkippedAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        return await UpdateIssueStatusAsync(issueId, IssueStatus.Skipped, cancellationToken);
    }

    public async Task<PullListBulkResult> BulkUpdateStatusAsync(
        IEnumerable<int> issueIds,
        IssueStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var result = new PullListBulkResult { Success = true };
        var ids = issueIds.ToList();

        try
        {
            var issues = await _dbContext.Issues
                .Where(i => ids.Contains(i.Id))
                .ToListAsync(cancellationToken);

            foreach (var issue in issues)
            {
                try
                {
                    issue.Status = newStatus;
                    issue.Monitored = newStatus == IssueStatus.Wanted;
                    issue.UpdatedAt = DateTime.UtcNow;
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update issue {IssueId}", issue.Id);
                    result.FailedIssueIds.Add(issue.Id);
                    result.FailedCount++;
                }
            }

            result.TotalProcessed = ids.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Bulk updated {Success}/{Total} issues to status {Status}",
                result.SuccessCount, result.TotalProcessed, newStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk update issues");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<PullListActionResult> UpdateIssueStatusAsync(
        int issueId,
        IssueStatus newStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);
            if (issue == null)
            {
                return new PullListActionResult
                {
                    Success = false,
                    Error = $"Issue {issueId} not found",
                    IssueId = issueId
                };
            }

            issue.Status = newStatus;
            issue.Monitored = newStatus == IssueStatus.Wanted;
            issue.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated issue {IssueId} to status {Status}", issueId, newStatus);

            return new PullListActionResult
            {
                Success = true,
                IssueId = issueId,
                NewStatus = newStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update issue {IssueId} status", issueId);
            return new PullListActionResult
            {
                Success = false,
                Error = ex.Message,
                IssueId = issueId
            };
        }
    }

    #endregion

    #region Auto-Add & Monitoring

    public async Task<AutoAddResult> ProcessNewIssuesAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var result = new AutoAddResult { Success = true, SeriesProcessed = 1 };

        try
        {
            var series = await _dbContext.Series
                .Include(s => s.Issues)
                .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

            if (series == null)
            {
                result.Success = false;
                result.Error = $"Series {seriesId} not found";
                return result;
            }

            if (!series.Monitored)
            {
                _logger.LogDebug("Series {SeriesId} is not monitored, skipping", seriesId);
                return result;
            }

            var issuesAdded = await ProcessSeriesMonitoringAsync(series, cancellationToken);
            result.IssuesAdded = issuesAdded.Count;
            result.AddedIssues = issuesAdded;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process new issues for series {SeriesId}", seriesId);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<AutoAddResult> ProcessReleaseDayAsync(
        DateTime releaseDate,
        CancellationToken cancellationToken = default)
    {
        var result = new AutoAddResult { Success = true };

        try
        {
            // Get all monitored series with issues releasing on this date
            var seriesWithReleases = await _dbContext.Series
                .Include(s => s.Issues.Where(i => i.StoreDate.HasValue && 
                    i.StoreDate.Value.Date == releaseDate.Date))
                .Where(s => s.Monitored)
                .ToListAsync(cancellationToken);

            foreach (var series in seriesWithReleases)
            {
                var addedIssues = await ProcessSeriesMonitoringAsync(series, cancellationToken);
                result.IssuesAdded += addedIssues.Count;
                result.AddedIssues.AddRange(addedIssues);
                result.SeriesProcessed++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Processed release day {Date}: {SeriesCount} series, {IssuesAdded} issues added",
                releaseDate.ToShortDateString(), result.SeriesProcessed, result.IssuesAdded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process release day {Date}", releaseDate);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<SeriesMonitoringMode> GetSeriesMonitoringModeAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        return series?.MonitoringMode ?? SeriesMonitoringMode.AllIssues;
    }

    public async Task<PullListActionResult> SetSeriesMonitoringModeAsync(
        int seriesId,
        SeriesMonitoringMode mode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
            if (series == null)
            {
                return new PullListActionResult
                {
                    Success = false,
                    Error = $"Series {seriesId} not found"
                };
            }

            series.MonitoringMode = mode;
            series.Monitored = mode != SeriesMonitoringMode.None;
            series.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated series {SeriesId} monitoring mode to {Mode}",
                seriesId, mode);

            return new PullListActionResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update monitoring mode for series {SeriesId}", seriesId);
            return new PullListActionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<List<PullListIssue>> ProcessSeriesMonitoringAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        var addedIssues = new List<PullListIssue>();

        foreach (var issue in series.Issues.Where(i => i.Status == IssueStatus.Missing))
        {
            var shouldAdd = series.MonitoringMode switch
            {
                SeriesMonitoringMode.AllIssues => true,
                SeriesMonitoringMode.FutureIssues => issue.StoreDate.HasValue && 
                    issue.StoreDate.Value >= series.CreatedAt,
                SeriesMonitoringMode.FirstIssue => issue.IssueNumber == 1,
                SeriesMonitoringMode.Manual => false,
                SeriesMonitoringMode.None => false,
                _ => false
            };

            if (shouldAdd)
            {
                issue.Status = IssueStatus.Wanted;
                issue.Monitored = true;
                issue.UpdatedAt = DateTime.UtcNow;

                addedIssues.Add(new PullListIssue
                {
                    IssueId = issue.Id,
                    SeriesId = series.Id,
                    SeriesTitle = series.Title,
                    Publisher = series.Publisher,
                    IssueNumber = issue.IssueNumber,
                    IssueNumberText = issue.IssueNumberText,
                    IssueTitle = issue.Title,
                    StoreDate = issue.StoreDate,
                    Status = IssueStatus.Wanted
                });
            }
        }

        return addedIssues;
    }

    #endregion

    #region Statistics

    public async Task<PullListStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var (thisWeekStart, thisWeekEnd) = GetWeekBoundaries(today);
        var (nextWeekStart, nextWeekEnd) = GetWeekBoundaries(today.AddDays(7));

        var stats = new PullListStats
        {
            TotalMonitoredSeries = await _dbContext.Series
                .CountAsync(s => s.Monitored, cancellationToken),

            TotalWantedIssues = await _dbContext.Issues
                .CountAsync(i => i.Status == IssueStatus.Wanted, cancellationToken),

            TotalOwnedIssues = await _dbContext.Issues
                .CountAsync(i => i.Status == IssueStatus.Owned, cancellationToken),

            TotalSkippedIssues = await _dbContext.Issues
                .CountAsync(i => i.Status == IssueStatus.Skipped, cancellationToken),

            ReleasingThisWeek = await _dbContext.Issues
                .CountAsync(i => i.StoreDate >= thisWeekStart && 
                    i.StoreDate < thisWeekEnd && 
                    i.Series!.Monitored, cancellationToken),

            ReleasingNextWeek = await _dbContext.Issues
                .CountAsync(i => i.StoreDate >= nextWeekStart && 
                    i.StoreDate < nextWeekEnd && 
                    i.Series!.Monitored, cancellationToken),

            MissedIssues = await _dbContext.Issues
                .CountAsync(i => i.Status == IssueStatus.Wanted && 
                    i.StoreDate < today && 
                    i.Series!.Monitored, cancellationToken)
        };

        // Wanted by publisher
        var wantedByPublisher = await _dbContext.Issues
            .Where(i => i.Status == IssueStatus.Wanted && i.Series!.Publisher != null)
            .GroupBy(i => i.Series!.Publisher!)
            .Select(g => new { Publisher = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        stats.WantedByPublisher = wantedByPublisher.ToDictionary(x => x.Publisher, x => x.Count);

        return stats;
    }

    public async Task<PullListConfigStatus> GetConfigStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new PullListConfigStatus();

        try
        {
            // Check ComicVine configuration
            status.IsComicVineConfigured = _comicVineClient.IsConfigured;

            // Get series counts
            status.TotalSeriesCount = await _dbContext.Series
                .CountAsync(cancellationToken);

            status.MatchedSeriesCount = await _dbContext.Series
                .CountAsync(s => s.ComicVineId != null, cancellationToken);

            status.MonitoredSeriesCount = await _dbContext.Series
                .CountAsync(s => s.Monitored, cancellationToken);

            // Check if there are releases this week
            var today = DateTime.Today;
            var (thisWeekStart, thisWeekEnd) = GetWeekBoundaries(today);
            status.HasReleasesThisWeek = await _dbContext.Issues
                .AnyAsync(i => i.StoreDate >= thisWeekStart && 
                              i.StoreDate < thisWeekEnd && 
                              i.Series!.Monitored, cancellationToken);

            // Determine suggested action
            if (!status.IsComicVineConfigured)
            {
                status.ActionType = PullListSuggestedActionType.ConfigureApiKey;
                status.SuggestedAction = "Configure your ComicVine API key to enable release tracking and discovery.";
            }
            else if (status.TotalSeriesCount == 0)
            {
                status.ActionType = PullListSuggestedActionType.AddSeries;
                status.SuggestedAction = "Add your first series to start tracking releases.";
            }
            else if (status.MatchedSeriesCount == 0)
            {
                status.ActionType = PullListSuggestedActionType.MatchSeries;
                status.SuggestedAction = "Match your series to ComicVine to track release dates.";
            }
            else if (!status.HasReleasesThisWeek && status.MonitoredSeriesCount > 0)
            {
                status.ActionType = PullListSuggestedActionType.TryAllReleases;
                status.SuggestedAction = "No releases from your library this week. Try All Releases to discover new comics.";
            }
            else
            {
                status.ActionType = PullListSuggestedActionType.None;
            }

            _logger.LogDebug(
                "Pull list config status: ComicVine={IsConfigured}, Series={Total}/{Matched}/{Monitored}",
                status.IsComicVineConfigured, 
                status.TotalSeriesCount, 
                status.MatchedSeriesCount, 
                status.MonitoredSeriesCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pull list config status");
        }

        return status;
    }

    #endregion

    #region Settings

    public async Task<PullListSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsService.GetAsync<PullListSettings>(
            PullListSettingsKey, 
            null,
            cancellationToken) ?? new PullListSettings();
    }

    public async Task<PullListActionResult> UpdateSettingsAsync(
        PullListSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _settingsService.SetAsync(PullListSettingsKey, settings, cancellationToken);
            _logger.LogInformation("Updated pull list settings");
            return new PullListActionResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pull list settings");
            return new PullListActionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<SeriesPullListSettings?> GetSeriesSettingsAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync<Dictionary<int, SeriesPullListSettings>>(
            SeriesSettingsKey,
            null,
            cancellationToken) ?? new Dictionary<int, SeriesPullListSettings>();

        return settings.TryGetValue(seriesId, out var seriesSettings) ? seriesSettings : null;
    }

    public async Task<PullListActionResult> UpdateSeriesSettingsAsync(
        SeriesPullListSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allSettings = await _settingsService.GetAsync<Dictionary<int, SeriesPullListSettings>>(
                SeriesSettingsKey,
                null,
                cancellationToken) ?? new Dictionary<int, SeriesPullListSettings>();

            allSettings[settings.SeriesId] = settings;

            await _settingsService.SetAsync(SeriesSettingsKey, allSettings, cancellationToken);
            
            _logger.LogInformation("Updated pull list settings for series {SeriesId}", settings.SeriesId);
            return new PullListActionResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update series pull list settings");
            return new PullListActionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    #endregion

    #region Discovery & One-Off Additions (Mylar3 "This Week" Parity)

    public async Task<WeeklyDiscoveryList> GetWeeklyDiscoveryAsync(
        DateTime weekOf,
        DiscoveryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetWeekBoundaries(weekOf);
        var releaseDay = GetReleaseDay(weekStart);
        var cacheKey = string.Format(DiscoveryCacheKey, weekStart.ToString("yyyy-MM-dd"));

        // Try cache first
        if (_cache.TryGetValue<List<ComicVineIssue>>(cacheKey, out var cachedIssues) && cachedIssues != null)
        {
            _logger.LogDebug("Using cached discovery results for week of {WeekStart}", weekStart);
            return await BuildDiscoveryListAsync(cachedIssues, weekStart, weekEnd, releaseDay, filter, cancellationToken);
        }

        // Fetch from ComicVine API
        _logger.LogInformation("Fetching ComicVine releases for week of {WeekStart}", weekStart);
        
        var allIssues = new List<ComicVineIssue>();
        var offset = 0;
        const int limit = 100;
        
        // Query ComicVine for issues with store_date in this week
        // ComicVine date filter format: YYYY-MM-DD|YYYY-MM-DD
        var dateFilter = $"{weekStart:yyyy-MM-dd}|{weekEnd.AddDays(-1):yyyy-MM-dd}";
        
        while (true)
        {
            var result = await _comicVineClient.GetIssuesByStoreDateAsync(
                dateFilter, 
                offset, 
                limit, 
                cancellationToken);

            if (!result.Success || result.Results == null)
            {
                _logger.LogWarning("Failed to fetch ComicVine releases: {Error}", result.Error);
                break;
            }

            allIssues.AddRange(result.Results);
            
            if (result.Results.Count < limit || allIssues.Count >= result.TotalResults)
                break;
                
            offset += limit;
            
            // Rate limit protection
            await Task.Delay(200, cancellationToken);
        }

        _logger.LogInformation("Retrieved {Count} issues from ComicVine for week of {WeekStart}", 
            allIssues.Count, weekStart);

        // Cache the results
        _cache.Set(cacheKey, allIssues, DiscoveryCacheDuration);

        return await BuildDiscoveryListAsync(allIssues, weekStart, weekEnd, releaseDay, filter, cancellationToken);
    }

    public async Task<AddOneOffResult> AddIssueOneOffAsync(
        int comicVineIssueId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding one-off issue with ComicVine ID {IssueId}", comicVineIssueId);

            // Check if issue already exists in database
            var existingIssue = await _dbContext.Issues
                .Include(i => i.Series)
                .FirstOrDefaultAsync(i => i.ComicVineId == comicVineIssueId, cancellationToken);

            if (existingIssue != null)
            {
                // Issue exists, just mark it as wanted
                existingIssue.Status = IssueStatus.Wanted;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new AddOneOffResult
                {
                    Success = true,
                    IssueId = existingIssue.Id,
                    SeriesId = existingIssue.SeriesId,
                    SeriesTitle = existingIssue.Series?.Title,
                    IssueNumber = existingIssue.IssueNumber,
                    SeriesCreated = false
                };
            }

            // Fetch issue details from ComicVine
            var issueResult = await _comicVineClient.GetIssueAsync(comicVineIssueId, cancellationToken);
            if (!issueResult.Success || issueResult.Data == null)
            {
                return new AddOneOffResult
                {
                    Success = false,
                    Error = issueResult.Error ?? "Failed to fetch issue from ComicVine"
                };
            }

            var cvIssue = issueResult.Data;
            var volumeId = cvIssue.Volume?.Id ?? 0;
            
            if (volumeId == 0)
            {
                return new AddOneOffResult
                {
                    Success = false,
                    Error = "Issue has no associated volume in ComicVine"
                };
            }

            // Check if series exists
            var seriesCreated = false;
            var series = await _dbContext.Series
                .FirstOrDefaultAsync(s => s.ComicVineId == volumeId, cancellationToken);

            if (series == null)
            {
                // Create minimal series record (not monitored)
                var volumeResult = await _comicVineClient.GetVolumeAsync(volumeId, cancellationToken);
                if (!volumeResult.Success || volumeResult.Data == null)
                {
                    return new AddOneOffResult
                    {
                        Success = false,
                        Error = "Failed to fetch series info from ComicVine"
                    };
                }

                var cvVolume = volumeResult.Data;
                series = new Series
                {
                    Title = cvVolume.Name ?? "Unknown",
                    ComicVineId = volumeId,
                    ComicVineUrl = cvVolume.SiteDetailUrl,
                    Publisher = cvVolume.Publisher?.Name,
                    StartYear = cvVolume.StartYear,
                    CoverImageUrl = cvVolume.Image?.MediumUrl,
                    Monitored = false, // Don't monitor - this is a one-off
                    MonitoringMode = SeriesMonitoringMode.None,
                    CreatedAt = DateTime.UtcNow,
                    MetadataLastRefreshed = DateTime.UtcNow
                };

                _dbContext.Series.Add(series);
                await _dbContext.SaveChangesAsync(cancellationToken);
                seriesCreated = true;
                
                _logger.LogInformation("Created minimal series record for one-off: {SeriesTitle}", series.Title);
            }

            // Create the issue record
            var issue = new Issue
            {
                SeriesId = series.Id,
                ComicVineId = comicVineIssueId,
                ComicVineUrl = cvIssue.SiteDetailUrl,
                IssueNumber = decimal.TryParse(cvIssue.IssueNumber, out var num) ? num : 0,
                IssueNumberText = cvIssue.IssueNumber,
                Title = cvIssue.Name,
                StoreDate = cvIssue.StoreDate,
                CoverDate = cvIssue.CoverDate,
                CoverImageUrl = cvIssue.Image?.MediumUrl,
                Overview = cvIssue.Description,
                Status = IssueStatus.Wanted,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Issues.Add(issue);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Added one-off issue: {SeriesTitle} #{IssueNumber}", 
                series.Title, issue.IssueNumber);

            return new AddOneOffResult
            {
                Success = true,
                IssueId = issue.Id,
                SeriesId = series.Id,
                SeriesTitle = series.Title,
                IssueNumber = issue.IssueNumber,
                SeriesCreated = seriesCreated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add one-off issue {IssueId}", comicVineIssueId);
            return new AddOneOffResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<AddFromDiscoveryResult> AddSeriesFromDiscoveryAsync(
        int comicVineVolumeId,
        int? markIssueWantedComicVineId = null,
        SeriesMonitoringMode monitoringMode = SeriesMonitoringMode.FutureIssues,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding series from discovery with ComicVine Volume ID {VolumeId}", comicVineVolumeId);

            // Check if series already exists
            var existingSeries = await _dbContext.Series
                .FirstOrDefaultAsync(s => s.ComicVineId == comicVineVolumeId, cancellationToken);

            if (existingSeries != null)
            {
                // Series exists - update monitoring if needed
                if (!existingSeries.Monitored || existingSeries.MonitoringMode != monitoringMode)
                {
                    existingSeries.Monitored = true;
                    existingSeries.MonitoringMode = monitoringMode;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // Mark specific issue as wanted if requested
                int? markedIssueId = null;
                if (markIssueWantedComicVineId.HasValue)
                {
                    var issue = await _dbContext.Issues
                        .FirstOrDefaultAsync(i => i.SeriesId == existingSeries.Id && 
                            i.ComicVineId == markIssueWantedComicVineId.Value, cancellationToken);
                    if (issue != null)
                    {
                        issue.Status = IssueStatus.Wanted;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        markedIssueId = issue.Id;
                    }
                }

                return new AddFromDiscoveryResult
                {
                    Success = true,
                    SeriesId = existingSeries.Id,
                    SeriesTitle = existingSeries.Title,
                    IssuesCreated = 0,
                    MarkedWantedIssueId = markedIssueId,
                    AlreadyExists = true
                };
            }

            // Use series metadata service to add the series
            var addResult = await _seriesMetadataService.AddSeriesByComicVineIdAsync(
                comicVineVolumeId,
                rootFolder: null,
                monitored: true,
                monitoringMode: monitoringMode,
                cancellationToken: cancellationToken);

            if (!addResult.Success || !addResult.SeriesId.HasValue)
            {
                return new AddFromDiscoveryResult
                {
                    Success = false,
                    Error = addResult.Error ?? "Failed to add series from ComicVine"
                };
            }

            // Mark specific issue as wanted if requested
            int? issueMarkedId = null;
            if (markIssueWantedComicVineId.HasValue)
            {
                var issue = await _dbContext.Issues
                    .FirstOrDefaultAsync(i => i.SeriesId == addResult.SeriesId.Value && 
                        i.ComicVineId == markIssueWantedComicVineId.Value, cancellationToken);
                if (issue != null)
                {
                    issue.Status = IssueStatus.Wanted;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    issueMarkedId = issue.Id;
                }
            }

            _logger.LogInformation("Added series from discovery: {SeriesTitle} with {IssueCount} issues", 
                addResult.Title, addResult.IssuesCreated);

            return new AddFromDiscoveryResult
            {
                Success = true,
                SeriesId = addResult.SeriesId,
                SeriesTitle = addResult.Title,
                IssuesCreated = addResult.IssuesCreated,
                MarkedWantedIssueId = issueMarkedId,
                AlreadyExists = addResult.AlreadyExists
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add series from discovery {VolumeId}", comicVineVolumeId);
            return new AddFromDiscoveryResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<WeeklyDiscoveryList> BuildDiscoveryListAsync(
        List<ComicVineIssue> comicVineIssues,
        DateTime weekStart,
        DateTime weekEnd,
        DateTime releaseDay,
        DiscoveryFilter? filter,
        CancellationToken cancellationToken)
    {
        // Get all local series with ComicVine IDs for matching
        var localSeriesLookup = await _dbContext.Series
            .Where(s => s.ComicVineId != null)
            .ToDictionaryAsync(s => s.ComicVineId!.Value, s => s, cancellationToken);

        // Get all local issues with ComicVine IDs for matching
        var localIssueLookup = await _dbContext.Issues
            .Where(i => i.ComicVineId != null)
            .ToDictionaryAsync(i => i.ComicVineId!.Value, i => i, cancellationToken);

        var discoveryIssues = new List<DiscoverableIssue>();

        foreach (var cvIssue in comicVineIssues)
        {
            var volumeId = cvIssue.Volume?.Id ?? 0;
            var issueId = cvIssue.Id;

            // Check if in library
            var isInLibrary = localIssueLookup.ContainsKey(issueId) || 
                              (volumeId > 0 && localSeriesLookup.ContainsKey(volumeId));
            
            Series? localSeries = volumeId > 0 && localSeriesLookup.TryGetValue(volumeId, out var s) ? s : null;
            Issue? localIssue = localIssueLookup.TryGetValue(issueId, out var i) ? i : null;

            // Apply filters
            if (filter != null)
            {
                if (filter.InLibraryOnly == true && !isInLibrary) continue;
                if (filter.NewOnly == true && isInLibrary) continue;
                
                if (filter.Publishers?.Any() == true)
                {
                    // Note: Volume ref doesn't include publisher, only check local series
                    var publisher = localSeries?.Publisher;
                    if (publisher == null || !filter.Publishers.Contains(publisher, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                // Check for annuals/specials
                var issueNumText = cvIssue.IssueNumber?.ToUpperInvariant() ?? "";
                var isAnnual = issueNumText.Contains("ANNUAL") || 
                               (cvIssue.Name?.ToUpperInvariant().Contains("ANNUAL") ?? false);
                var isSpecial = issueNumText.Contains("SPECIAL") || 
                                issueNumText.StartsWith("½") ||
                                (cvIssue.Name?.ToUpperInvariant().Contains("SPECIAL") ?? false);

                if (!filter.IncludeAnnuals && isAnnual) continue;
                if (!filter.IncludeSpecials && isSpecial) continue;
            }

            discoveryIssues.Add(new DiscoverableIssue
            {
                ComicVineIssueId = issueId,
                ComicVineVolumeId = volumeId,
                SeriesTitle = cvIssue.Volume?.Name ?? "Unknown",
                Publisher = localSeries?.Publisher, // Only from local series; volume ref doesn't include publisher
                StartYear = localSeries?.StartYear, // Only from local series; volume ref doesn't include start year
                IssueNumber = decimal.TryParse(cvIssue.IssueNumber, out var num) ? num : 0,
                IssueNumberText = cvIssue.IssueNumber,
                IssueTitle = cvIssue.Name,
                StoreDate = cvIssue.StoreDate,
                CoverDate = cvIssue.CoverDate,
                CoverImageUrl = cvIssue.Image?.MediumUrl,
                IsInLibrary = isInLibrary,
                LocalSeriesId = localSeries?.Id,
                LocalIssueId = localIssue?.Id,
                Status = localIssue?.Status,
                IsSeriesMonitored = localSeries?.Monitored ?? false
            });
        }

        return new WeeklyDiscoveryList
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            ReleaseDay = releaseDay,
            Issues = discoveryIssues
                .OrderBy(i => i.SeriesTitle)
                .ThenBy(i => i.IssueNumber)
                .ToList()
        };
    }

    #endregion

    #region Private Helpers

    private IQueryable<Issue> BuildIssueQuery(PullListFilter? filter)
    {
        var query = _dbContext.Issues
            .Include(i => i.Series)
            .AsQueryable();

        if (filter != null)
        {
            if (filter.SeriesIds?.Any() == true)
                query = query.Where(i => filter.SeriesIds.Contains(i.SeriesId));

            if (filter.Publishers?.Any() == true)
                query = query.Where(i => filter.Publishers.Contains(i.Series!.Publisher!));

            if (filter.Statuses?.Any() == true)
                query = query.Where(i => filter.Statuses.Contains(i.Status));

            if (filter.MonitoredOnly == true)
                query = query.Where(i => i.Series!.Monitored);

            if (!filter.IncludeAnnuals)
                query = query.Where(i => !i.IsAnnual);

            if (!filter.IncludeSpecials)
                query = query.Where(i => !i.IsSpecial);
        }

        return query;
    }

    private static PullListIssue MapToPullListIssue(Issue issue)
    {
        return new PullListIssue
        {
            IssueId = issue.Id,
            SeriesId = issue.SeriesId,
            SeriesTitle = issue.Series?.Title ?? "Unknown",
            Publisher = issue.Series?.Publisher,
            IssueNumber = issue.IssueNumber,
            IssueNumberText = issue.IssueNumberText,
            IssueTitle = issue.Title,
            StoreDate = issue.StoreDate,
            CoverDate = issue.CoverDate,
            CoverImageUrl = issue.CoverImageUrl,
            Status = issue.Status,
            IsAnnual = issue.IsAnnual,
            IsSpecial = issue.IsSpecial,
            SpecialType = issue.SpecialType
        };
    }

    private static (DateTime start, DateTime end) GetWeekBoundaries(DateTime date)
    {
        var weekStart = GetWeekStart(date);
        return (weekStart, weekStart.AddDays(7));
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        // Week starts on Sunday (US standard for comics)
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static DateTime GetReleaseDay(DateTime weekStart)
    {
        // Release day is Wednesday
        return weekStart.AddDays((int)DefaultReleaseDay);
    }

    #endregion
}
