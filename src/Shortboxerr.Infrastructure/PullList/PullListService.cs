using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.WalkSoftly;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.WalkSoftly;

namespace Shortboxerr.Infrastructure.PullList;

/// <summary>
/// Implementation of IPullListService for managing the weekly pull list.
/// </summary>
public class PullListService : IPullListService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly IComicVineClient _comicVineClient;
    private readonly IWalkSoftlyClient _walkSoftlyClient;
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PullListService> _logger;

    // Settings key
    private const string PullListSettingsKey = "pulllist";
    private const string SeriesSettingsKey = "pulllist_series";
    
    // Comics typically release on Wednesday in the US
    private const DayOfWeek DefaultReleaseDay = DayOfWeek.Wednesday;
    
    // Cache durations
    private static readonly TimeSpan DiscoveryCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WeeklyPullListCacheDuration = TimeSpan.FromMinutes(5);

    public PullListService(
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        IComicVineClient comicVineClient,
        IWalkSoftlyClient walkSoftlyClient,
        ISeriesMetadataService seriesMetadataService,
        ICacheService cacheService,
        ILogger<PullListService> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _comicVineClient = comicVineClient;
        _walkSoftlyClient = walkSoftlyClient;
        _seriesMetadataService = seriesMetadataService;
        _cacheService = cacheService;
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
        
        // Get settings for cache tier calculation
        var settings = await GetSettingsAsync(cancellationToken);

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
            Issues = issues.Select(MapToPullListIssue).ToList(),
            CacheMetadata = CreateCacheMetadata(releaseDay, settings, fromCache: false)
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
        // Owned status is typically set automatically when a file is imported,
        // but can also be set manually (e.g., for issues owned physically but not digitally)
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
                .Include(i => i.File)
                .Where(i => ids.Contains(i.Id))
                .ToListAsync(cancellationToken);

            foreach (var issue in issues)
            {
                try
                {
                    // Mylar3 parity: Status can be changed on ANY issue, including those with files.
                    // This allows:
                    // - Marking an owned issue as "Wanted" to trigger a re-search (replace/upgrade)
                    // - Marking an issue as "Skipped" even if owned (won't affect existing file)
                    // 
                    // HasFile is a separate flag from Status - they are independent:
                    // - HasFile = true means a file exists in the library
                    // - Status = Wanted means "search for this issue"
                    // - Both can be true simultaneously (re-download scenario)
                    
                    issue.Status = newStatus;
                    // Monitored = true for Wanted, false for Skipped/other
                    issue.Monitored = newStatus == IssueStatus.Wanted;
                    issue.UpdatedAt = DateTime.UtcNow;
                    result.SuccessCount++;
                    
                    var hasFile = issue.File != null || issue.HasFile;
                    _logger.LogDebug("Updated issue {IssueId} to status {Status}, Monitored={Monitored}, HasFile={HasFile}", 
                        issue.Id, newStatus, issue.Monitored, hasFile);
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
            
            // Invalidate pull list cache since status affects query results
            InvalidatePullListCache();

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
    
    /// <summary>
    /// Invalidates all pull list related caches when data changes.
    /// Also invalidates series issues cache since status changes affect issue display.
    /// </summary>
    private void InvalidatePullListCache()
    {
        _cacheService.RemoveByPrefix(CacheKeys.PullListWeek);
        _cacheService.RemoveByPrefix(CacheKeys.PullListUpcoming);
        _cacheService.RemoveByPrefix(CacheKeys.PullListPast);
        _cacheService.RemoveByPrefix(CacheKeys.DashboardStats);
        _cacheService.RemoveByPrefix(CacheKeys.DashboardThisWeek);
        // Also invalidate series issues cache - status changes need to be reflected
        _cacheService.RemoveByPrefix(CacheKeys.Series);
        _logger.LogDebug("Pull list and series cache invalidated due to status change");
    }

    private async Task<PullListActionResult> UpdateIssueStatusAsync(
        int issueId,
        IssueStatus newStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            var issue = await _dbContext.Issues
                .Include(i => i.File)
                .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);
                
            if (issue == null)
            {
                return new PullListActionResult
                {
                    Success = false,
                    Error = $"Issue {issueId} not found",
                    IssueId = issueId
                };
            }

            // Mylar3 parity: Status can be changed on ANY issue, including those with files.
            // This allows:
            // - Marking an owned issue as "Wanted" to trigger a re-search (replace/upgrade)
            // - Marking an issue as "Skipped" even if owned (won't affect existing file)
            // - Manually marking as "Owned" for physical/external copies
            // 
            // HasFile is a separate flag from Status - they are independent:
            // - HasFile = true means a file exists in the library
            // - Status = Wanted means "search for this issue"
            // - Both can be true simultaneously (re-download scenario)
            
            var hasFile = issue.File != null || issue.HasFile;
            _logger.LogDebug("Changing issue {IssueId} status from {OldStatus} to {NewStatus}, HasFile={HasFile}", 
                issueId, issue.Status, newStatus, hasFile);

            issue.Status = newStatus;
            issue.Monitored = newStatus == IssueStatus.Wanted;
            issue.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            
            // Invalidate pull list cache since status affects query results
            InvalidatePullListCache();

            _logger.LogInformation("Updated issue {IssueId} to status {Status}, Monitored={Monitored}", 
                issueId, newStatus, issue.Monitored);

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

    private Task<List<PullListIssue>> ProcessSeriesMonitoringAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        var addedIssues = new List<PullListIssue>();

        foreach (var issue in series.Issues.Where(i => i.Status == IssueStatus.Missing))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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

        return Task.FromResult(addedIssues);
    }

    #endregion

    #region Statistics
    
    // Cache duration for dashboard stats (1 minute - frequently accessed, quickly stale)
    private static readonly TimeSpan StatsCacheDuration = TimeSpan.FromMinutes(1);

    public async Task<PullListStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = _cacheService.GenerateKey(CacheKeys.DashboardStats);
        
        return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
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

            _logger.LogDebug("Pull list stats calculated and cached");
            return stats;
        }, StatsCacheDuration);
    }

    public async Task<PullListConfigStatus> GetConfigStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new PullListConfigStatus();

        try
        {
            // Check ComicVine configuration (use async method for reliable check)
            status.IsComicVineConfigured = await _comicVineClient.IsConfiguredAsync(cancellationToken);

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
            // Get current settings to detect changes
            var currentSettings = await GetSettingsAsync(cancellationToken);
            var oldIntegrationEnabled = currentSettings.EnableSeriesAnnualIntegration ?? true;
            var newIntegrationEnabled = settings.EnableSeriesAnnualIntegration ?? true;
            
            // Save the new settings
            await _settingsService.SetAsync(PullListSettingsKey, settings, cancellationToken);
            _logger.LogInformation("Updated pull list settings");
            
            // Check if series-annual integration setting changed
            if (oldIntegrationEnabled != newIntegrationEnabled)
            {
                _logger.LogInformation("Series-annual integration changed from {Old} to {New}", 
                    oldIntegrationEnabled, newIntegrationEnabled);
                
                if (newIntegrationEnabled)
                {
                    // Integration was enabled - link all annual series
                    _logger.LogInformation("Enabling series-annual integration: linking annual series to parents");
                    var linkResult = await _seriesMetadataService.LinkExistingAnnualSeriesAsync(cancellationToken);
                    
                    if (linkResult.Success)
                    {
                        _logger.LogInformation("Automatically linked {Count} annual series", linkResult.LinkedCount);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to auto-link annual series: {Error}", linkResult.Error);
                    }
                }
                else
                {
                    // Integration was disabled - unlink all annual series
                    _logger.LogInformation("Disabling series-annual integration: unlinking annual series from parents");
                    var unlinkResult = await _seriesMetadataService.UnlinkAllAnnualSeriesAsync(cancellationToken);
                    
                    if (unlinkResult.Success)
                    {
                        _logger.LogInformation("Automatically unlinked {Count} annual series", unlinkResult.UnlinkedCount);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to auto-unlink annual series: {Error}", unlinkResult.Error);
                    }
                }
                
                // Invalidate all series-related caches since the parent links have changed
                _cacheService.RemoveByPrefix(CacheKeys.Series);
                _cacheService.RemoveByPrefix(CacheKeys.DashboardStats);
                _logger.LogDebug("Series cache invalidated due to annual integration setting change");
            }
            
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
        var memoryCacheKey = _cacheService.GenerateKey(CacheKeys.PullListDiscovery, weekStart.ToString("yyyy-MM-dd"));
        
        // Get settings for intelligent cache tier
        var settings = await GetSettingsAsync(cancellationToken);
        var cacheTier = DetermineCacheTier(releaseDay, settings);
        var cacheTtl = GetCacheTtl(cacheTier, settings);
        
        var fromCache = false;
        var refreshedAt = DateTime.UtcNow;
        List<ComicVineIssue> allIssues;

        // Step 1: Check in-memory cache first (fastest)
        var memoryCachedIssues = _cacheService.Get<List<ComicVineIssue>>(memoryCacheKey);
        if (memoryCachedIssues != null)
        {
            _logger.LogDebug("Discovery cache HIT (memory) for week of {WeekStart}", weekStart);
            allIssues = memoryCachedIssues;
            fromCache = true;
            
            // Get refresh time from database for metadata
            var dbCacheEntry = await _dbContext.CachedDiscoveryWeeks
                .FirstOrDefaultAsync(c => c.WeekStart == weekStart.Date, cancellationToken);
            if (dbCacheEntry != null)
            {
                refreshedAt = dbCacheEntry.LastRefreshed;
            }
        }
        else
        {
            // Step 2: Check database cache (persists across restarts)
            var dbCacheEntry = await _dbContext.CachedDiscoveryWeeks
                .FirstOrDefaultAsync(c => c.WeekStart == weekStart.Date, cancellationToken);
            
            if (dbCacheEntry != null && dbCacheEntry.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogDebug("Discovery cache HIT (database) for week of {WeekStart}", weekStart);
                
                // Deserialize from database
                allIssues = JsonSerializer.Deserialize<List<ComicVineIssue>>(dbCacheEntry.IssuesJson) ?? new List<ComicVineIssue>();
                fromCache = true;
                refreshedAt = dbCacheEntry.LastRefreshed;
                
                // Warm the memory cache for faster subsequent access
                _cacheService.Set(memoryCacheKey, allIssues, cacheTtl);
            }
            else
            {
                // Step 3: Fetch from WalkSoftly/ComicVine (cache miss)
                _logger.LogInformation("Fetching releases for week of {WeekStart} (CacheTier: {CacheTier}, TTL: {CacheTtl})", 
                    weekStart, cacheTier, cacheTtl);
                
                var (fetchedIssues, dataSource) = await FetchWeeklyReleasesAsync(weekStart, weekEnd, settings, cancellationToken);
                allIssues = fetchedIssues;
                refreshedAt = DateTime.UtcNow;
                
                // Persist to database
                await PersistDiscoveryCacheAsync(weekStart, allIssues, cacheTtl, cacheTier, cancellationToken);
                
                // Also store in memory cache
                _cacheService.Set(memoryCacheKey, allIssues, cacheTtl);
                
                _logger.LogInformation("Retrieved and cached {Count} issues from {DataSource} for week of {WeekStart}", 
                    allIssues.Count, dataSource, weekStart);
            }
        }

        var discoveryList = await BuildDiscoveryListAsync(allIssues, weekStart, weekEnd, releaseDay, filter, cancellationToken);
        
        // Add cache metadata
        discoveryList.CacheMetadata = CreateCacheMetadata(releaseDay, settings, fromCache, refreshedAt);
        
        return discoveryList;
    }

    /// <summary>
    /// Fetches weekly releases using WalkSoftly as primary source, falling back to ComicVine.
    /// </summary>
    private async Task<(List<ComicVineIssue> Issues, string DataSource)> FetchWeeklyReleasesAsync(
        DateTime weekStart,
        DateTime weekEnd,
        PullListSettings settings,
        CancellationToken cancellationToken)
    {
        // Calculate week number for WalkSoftly
        var releaseDay = weekStart.AddDays((int)DayOfWeek.Wednesday);
        var cal = CultureInfo.InvariantCulture.Calendar;
        var weekNumber = cal.GetWeekOfYear(releaseDay, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var year = releaseDay.Year;

        // Try WalkSoftly first if enabled
        if (settings.UseWalkSoftly)
        {
            _logger.LogInformation("Fetching releases from WalkSoftly for week {Week}, {Year}", weekNumber, year);
            
            var wsResult = await _walkSoftlyClient.GetWeeklyReleasesAsync(weekNumber, year, cancellationToken);
            
            if (wsResult.Success && wsResult.Releases.Count > 0)
            {
                // Apply publisher filtering
                var filteredReleases = PublisherFilter.FilterByPublisher(
                    wsResult.Releases, 
                    settings.IgnoredPublishers);
                
                _logger.LogInformation(
                    "WalkSoftly returned {Total} releases, {Filtered} after publisher filtering",
                    wsResult.Releases.Count, filteredReleases.Count);
                
                var issues = filteredReleases.Select(ConvertWalkSoftlyToComicVineIssue).ToList();
                return (issues, "WalkSoftly");
            }
            
            _logger.LogWarning("WalkSoftly request failed: {Error}", wsResult.Error);
            
            if (!settings.WalkSoftlyFallbackToComicVine)
            {
                _logger.LogWarning("WalkSoftly fallback disabled, returning empty list");
                return (new List<ComicVineIssue>(), "WalkSoftly (failed)");
            }
            
            _logger.LogInformation("Falling back to ComicVine for week {Week}, {Year}", weekNumber, year);
        }

        // Fall back to ComicVine
        var cvIssues = await FetchComicVineIssuesForWeekAsync(weekStart, weekEnd, cancellationToken);
        return (cvIssues, "ComicVine");
    }

    /// <summary>
    /// Converts a WalkSoftly release to ComicVineIssue format for compatibility with existing code.
    /// </summary>
    private static ComicVineIssue ConvertWalkSoftlyToComicVineIssue(WalkSoftlyRelease release)
    {
        return new ComicVineIssue
        {
            Id = release.IssueId ?? 0,
            Name = null, // WalkSoftly doesn't provide issue title
            IssueNumber = release.Issue?.TrimStart('#') ?? "0",
            StoreDate = release.ShipDate,
            CoverDate = release.CoverDate,
            Volume = release.ComicId.HasValue 
                ? new ComicVineVolumeRef 
                { 
                    Id = release.ComicId.Value, 
                    Name = release.Series 
                } 
                : null,
            Image = null // WalkSoftly doesn't provide images
        };
    }

    /// <summary>
    /// Fetches issues from ComicVine for a specific week (fallback/legacy method).
    /// </summary>
    private async Task<List<ComicVineIssue>> FetchComicVineIssuesForWeekAsync(
        DateTime weekStart,
        DateTime weekEnd,
        CancellationToken cancellationToken)
    {
        var issues = new List<ComicVineIssue>();
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

            issues.AddRange(result.Results);
            
            if (result.Results.Count < limit || issues.Count >= result.TotalResults)
                break;
                
            offset += limit;
            
            // Rate limit protection
            await Task.Delay(200, cancellationToken);
        }

        return issues;
    }

    /// <summary>
    /// Persists discovery cache to the database.
    /// </summary>
    private async Task PersistDiscoveryCacheAsync(
        DateTime weekStart,
        List<ComicVineIssue> issues,
        TimeSpan ttl,
        CacheTier tier,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var issuesJson = JsonSerializer.Serialize(issues);
        
        // Upsert: update if exists, insert if not
        var existing = await _dbContext.CachedDiscoveryWeeks
            .FirstOrDefaultAsync(c => c.WeekStart == weekStart.Date, cancellationToken);
        
        if (existing != null)
        {
            existing.IssuesJson = issuesJson;
            existing.LastRefreshed = now;
            existing.ExpiresAt = now.Add(ttl);
            existing.IssueCount = issues.Count;
            existing.CacheTier = (int)tier;
        }
        else
        {
            _dbContext.CachedDiscoveryWeeks.Add(new CachedDiscoveryWeek
            {
                WeekStart = weekStart.Date,
                IssuesJson = issuesJson,
                LastRefreshed = now,
                ExpiresAt = now.Add(ttl),
                IssueCount = issues.Count,
                CacheTier = (int)tier
            });
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
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

    public async Task<DiscoveryPublishersResult> GetDiscoveryPublishersAsync(
        DateTime weekOf,
        bool includeComicVineLookup = false,
        CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetWeekBoundaries(weekOf);
        var memoryCacheKey = _cacheService.GenerateKey(CacheKeys.PullListDiscovery, weekStart.ToString("yyyy-MM-dd"));
        
        // Get cached discovery data or fetch from ComicVine
        List<ComicVineIssue> allIssues;
        var memoryCachedIssues = _cacheService.Get<List<ComicVineIssue>>(memoryCacheKey);
        
        if (memoryCachedIssues != null)
        {
            allIssues = memoryCachedIssues;
        }
        else
        {
            // Check database cache
            var dbCacheEntry = await _dbContext.CachedDiscoveryWeeks
                .FirstOrDefaultAsync(c => c.WeekStart == weekStart.Date, cancellationToken);
            
            if (dbCacheEntry != null && dbCacheEntry.ExpiresAt > DateTime.UtcNow)
            {
                allIssues = JsonSerializer.Deserialize<List<ComicVineIssue>>(dbCacheEntry.IssuesJson) ?? new List<ComicVineIssue>();
            }
            else
            {
                // Need to fetch - call GetWeeklyDiscoveryAsync to populate cache
                var discovery = await GetWeeklyDiscoveryAsync(weekOf, null, cancellationToken);
                // Re-fetch from cache after population
                memoryCachedIssues = _cacheService.Get<List<ComicVineIssue>>(memoryCacheKey);
                allIssues = memoryCachedIssues ?? new List<ComicVineIssue>();
            }
        }

        var result = new DiscoveryPublishersResult
        {
            WeekOf = weekStart,
            TotalIssueCount = allIssues.Count,
            IncludedComicVineLookup = includeComicVineLookup
        };

        // Get all local series with ComicVine IDs and their publishers
        var localSeriesLookup = await _dbContext.Series
            .Where(s => s.ComicVineId != null && !string.IsNullOrEmpty(s.Publisher))
            .ToDictionaryAsync(s => s.ComicVineId!.Value, s => s.Publisher!, cancellationToken);

        // Group issues by volume to count series/issues per publisher
        var volumeGroups = allIssues
            .Where(i => i.Volume?.Id > 0)
            .GroupBy(i => i.Volume!.Id)
            .ToList();

        // Track publishers from library
        var libraryPublisherStats = new Dictionary<string, (int issues, int series, HashSet<int> volumeIds)>(StringComparer.OrdinalIgnoreCase);
        var unmatchedVolumeIds = new HashSet<int>();

        foreach (var volumeGroup in volumeGroups)
        {
            var volumeId = volumeGroup.Key;
            var issueCount = volumeGroup.Count();
            
            if (localSeriesLookup.TryGetValue(volumeId, out var publisher) && !string.IsNullOrWhiteSpace(publisher))
            {
                if (!libraryPublisherStats.TryGetValue(publisher, out var stats))
                {
                    stats = (0, 0, new HashSet<int>());
                }
                stats.issues += issueCount;
                stats.volumeIds.Add(volumeId);
                libraryPublisherStats[publisher] = (stats.issues, stats.volumeIds.Count, stats.volumeIds);
            }
            else
            {
                unmatchedVolumeIds.Add(volumeId);
            }
        }

        // Add library publishers to result
        result.LibraryPublishers = libraryPublisherStats
            .Select(kvp => new DiscoveryPublisher
            {
                Name = kvp.Key,
                IssueCount = kvp.Value.issues,
                SeriesCount = kvp.Value.series,
                HasLibrarySeries = true
            })
            .OrderBy(p => p.Name)
            .ToList();

        // Optionally fetch publishers from ComicVine for unmatched volumes
        if (includeComicVineLookup && unmatchedVolumeIds.Count > 0)
        {
            var comicVinePublisherStats = new Dictionary<string, (int issues, int series, HashSet<int> volumeIds, int? publisherId)>(StringComparer.OrdinalIgnoreCase);
            
            // Limit lookup to avoid excessive API calls (batch first 50 unmatched)
            var volumesToLookup = unmatchedVolumeIds.Take(50).ToList();
            
            foreach (var volumeId in volumesToLookup)
            {
                try
                {
                    var volumeResult = await _comicVineClient.GetVolumeAsync(volumeId, cancellationToken);
                    if (volumeResult.Success && volumeResult.Data?.Publisher != null)
                    {
                        var publisherName = volumeResult.Data.Publisher.Name ?? "Unknown";
                        var publisherId = volumeResult.Data.Publisher.Id;
                        var issueCount = volumeGroups.FirstOrDefault(g => g.Key == volumeId)?.Count() ?? 0;
                        
                        if (!comicVinePublisherStats.TryGetValue(publisherName, out var stats))
                        {
                            stats = (0, 0, new HashSet<int>(), publisherId);
                        }
                        stats.issues += issueCount;
                        stats.volumeIds.Add(volumeId);
                        comicVinePublisherStats[publisherName] = (stats.issues, stats.volumeIds.Count, stats.volumeIds, publisherId);
                    }
                    
                    // Rate limit protection
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch publisher for volume {VolumeId}", volumeId);
                }
            }

            result.ComicVinePublishers = comicVinePublisherStats
                .Select(kvp => new DiscoveryPublisher
                {
                    Name = kvp.Key,
                    IssueCount = kvp.Value.issues,
                    SeriesCount = kvp.Value.series,
                    HasLibrarySeries = false,
                    ComicVinePublisherId = kvp.Value.publisherId
                })
                .OrderBy(p => p.Name)
                .ToList();
        }

        // Merge all publishers for the combined list
        var allPublishers = new Dictionary<string, DiscoveryPublisher>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var pub in result.LibraryPublishers)
        {
            allPublishers[pub.Name] = pub;
        }
        
        foreach (var pub in result.ComicVinePublishers)
        {
            if (allPublishers.TryGetValue(pub.Name, out var existing))
            {
                // Merge counts
                existing.IssueCount += pub.IssueCount;
                existing.SeriesCount += pub.SeriesCount;
                if (pub.ComicVinePublisherId.HasValue && !existing.ComicVinePublisherId.HasValue)
                {
                    existing.ComicVinePublisherId = pub.ComicVinePublisherId;
                }
            }
            else
            {
                allPublishers[pub.Name] = new DiscoveryPublisher
                {
                    Name = pub.Name,
                    IssueCount = pub.IssueCount,
                    SeriesCount = pub.SeriesCount,
                    HasLibrarySeries = pub.HasLibrarySeries,
                    ComicVinePublisherId = pub.ComicVinePublisherId
                };
            }
        }

        result.AllPublishers = allPublishers.Values
            .OrderBy(p => p.Name)
            .ToList();

        _logger.LogDebug(
            "Retrieved {LibraryCount} library publishers and {ComicVineCount} ComicVine publishers for week of {WeekStart}",
            result.LibraryPublishers.Count, result.ComicVinePublishers.Count, weekStart);

        return result;
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

    #region Weekly Export (Mylar3 Parity)

    public async Task<WeeklyExportResult> ExportCurrentWeekAsync(CancellationToken cancellationToken = default)
    {
        return await ExportWeekAsync(DateTime.Today, cancellationToken);
    }

    public async Task<WeeklyExportResult> ExportWeekAsync(DateTime weekOf, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.ExportWeeklyPullList)
        {
            return new WeeklyExportResult
            {
                Success = false,
                Error = "Weekly export is not enabled in settings."
            };
        }

        if (string.IsNullOrWhiteSpace(settings.WeeklyExportDirectory))
        {
            return new WeeklyExportResult
            {
                Success = false,
                Error = "Weekly export directory is not configured."
            };
        }

        var (weekStart, weekEnd) = GetWeekBoundaries(weekOf);
        var releaseDay = GetReleaseDay(weekStart);
        var year = releaseDay.Year;
        var weekNumber = GetIsoWeekNumber(releaseDay);

        // Create directory structure: {export_dir}/{YYYY}-{WW}
        var weekDirName = $"{year}-{weekNumber:D2}";
        var exportDir = Path.Combine(settings.WeeklyExportDirectory, weekDirName);

        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(exportDir);

            // Get the pull list for this week
            var pullList = await GetWeeklyReleasesAsync(weekOf, null, cancellationToken);

            // Build export data
            var exportData = BuildExportData(pullList, releaseDay, year, weekNumber);

            // Determine filename based on format
            var (fileName, content) = GenerateExportContent(exportData, settings.WeeklyExportFormat);
            var exportPath = Path.Combine(exportDir, fileName);

            // Write to file
            await File.WriteAllTextAsync(exportPath, content, cancellationToken);

            var fileInfo = new FileInfo(exportPath);

            _logger.LogInformation(
                "Exported weekly pull list for week {Year}-{Week} to {Path} ({IssueCount} issues)",
                year, weekNumber, exportPath, pullList.Issues.Count);

            return new WeeklyExportResult
            {
                Success = true,
                ExportDirectory = exportDir,
                ExportFilePath = exportPath,
                Format = settings.WeeklyExportFormat,
                Year = year,
                WeekNumber = weekNumber,
                ReleaseDay = releaseDay,
                TotalIssues = pullList.TotalCount,
                WantedIssues = pullList.WantedCount,
                OwnedIssues = pullList.OwnedCount,
                ExportedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export weekly pull list for week {Year}-{Week}", year, weekNumber);
            return new WeeklyExportResult
            {
                Success = false,
                Error = $"Export failed: {ex.Message}",
                Year = year,
                WeekNumber = weekNumber,
                ReleaseDay = releaseDay
            };
        }
    }

    public Task<List<WeeklyExportInfo>> GetExportHistoryAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var settings = await GetSettingsAsync(cancellationToken);
            var result = new List<WeeklyExportInfo>();

            if (string.IsNullOrWhiteSpace(settings.WeeklyExportDirectory) || !Directory.Exists(settings.WeeklyExportDirectory))
            {
                return result;
            }

            // Scan for week directories (format: YYYY-WW)
            var weekDirs = Directory.GetDirectories(settings.WeeklyExportDirectory)
                .Select(d => new DirectoryInfo(d))
                .Where(d => System.Text.RegularExpressions.Regex.IsMatch(d.Name, @"^\d{4}-\d{2}$"))
                .OrderByDescending(d => d.Name)
                .Take(limit);

            foreach (var dir in weekDirs)
            {
                // Parse year and week from directory name
                var parts = dir.Name.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var week))
                {
                    // Find export file in directory
                    var exportFile = dir.GetFiles("releases.*")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (exportFile != null)
                    {
                        var format = exportFile.Extension.ToLowerInvariant() switch
                        {
                            ".json" => WeeklyExportFormat.Json,
                            ".txt" => WeeklyExportFormat.Text,
                            ".csv" => WeeklyExportFormat.Csv,
                            _ => WeeklyExportFormat.Json
                        };

                        // Calculate release day from year and week
                        var releaseDay = GetDateFromIsoWeek(year, week, DayOfWeek.Wednesday);

                        result.Add(new WeeklyExportInfo
                        {
                            Year = year,
                            WeekNumber = week,
                            ReleaseDay = releaseDay,
                            DirectoryPath = dir.FullName,
                            FilePath = exportFile.FullName,
                            Format = format,
                            ExportedAt = exportFile.LastWriteTimeUtc,
                            FileSizeBytes = exportFile.Length,
                            IssueCount = await GetIssueCountFromFile(exportFile.FullName, format)
                        });
                    }
                }
            }

            return result;
        }, cancellationToken);
    }

    private static WeeklyExportData BuildExportData(WeeklyPullList pullList, DateTime releaseDay, int year, int weekNumber)
    {
        var exportData = new WeeklyExportData
        {
            Metadata = new WeeklyExportMetadata
            {
                Year = year,
                WeekNumber = weekNumber,
                WeekStart = pullList.WeekStart,
                WeekEnd = pullList.WeekEnd,
                ReleaseDay = releaseDay,
                ExportedAt = DateTime.UtcNow
            },
            Issues = pullList.Issues.Select(i => new WeeklyExportIssue
            {
                SeriesTitle = i.SeriesTitle,
                IssueNumber = i.IssueNumber,
                IssueNumberText = i.IssueNumberText,
                IssueTitle = i.IssueTitle,
                Publisher = i.Publisher,
                StoreDate = i.StoreDate,
                Status = i.Status.ToString(),
                ComicVineIssueId = null, // Not available from PullListIssue
                ComicVineVolumeId = null,
                IsAnnual = i.IsAnnual,
                IsSpecial = i.IsSpecial,
                SpecialType = i.SpecialType
            }).ToList(),
            Summary = new WeeklyExportSummary
            {
                TotalCount = pullList.TotalCount,
                WantedCount = pullList.WantedCount,
                OwnedCount = pullList.OwnedCount,
                SkippedCount = pullList.SkippedCount,
                MissingCount = pullList.Issues.Count(i => i.Status == IssueStatus.Missing),
                ByPublisher = pullList.Issues
                    .Where(i => !string.IsNullOrEmpty(i.Publisher))
                    .GroupBy(i => i.Publisher!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByStatus = pullList.Issues
                    .GroupBy(i => i.Status.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            }
        };

        return exportData;
    }

    private static (string fileName, string content) GenerateExportContent(WeeklyExportData data, WeeklyExportFormat format)
    {
        return format switch
        {
            WeeklyExportFormat.Json => ("releases.json", GenerateJsonExport(data)),
            WeeklyExportFormat.Text => ("releases.txt", GenerateTextExport(data)),
            WeeklyExportFormat.Csv => ("releases.csv", GenerateCsvExport(data)),
            _ => ("releases.json", GenerateJsonExport(data))
        };
    }

    private static string GenerateJsonExport(WeeklyExportData data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(data, options);
    }

    private static string GenerateTextExport(WeeklyExportData data)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"Weekly Pull List - Week {data.Metadata.WeekNumber}, {data.Metadata.Year}");
        sb.AppendLine($"Release Day: {data.Metadata.ReleaseDay:yyyy-MM-dd}");
        sb.AppendLine($"Exported: {data.Metadata.ExportedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        // Group by publisher
        var byPublisher = data.Issues
            .GroupBy(i => i.Publisher ?? "Unknown")
            .OrderBy(g => g.Key);

        foreach (var group in byPublisher)
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var issue in group.OrderBy(i => i.SeriesTitle).ThenBy(i => i.IssueNumber))
            {
                var issueText = issue.IssueNumberText ?? $"#{issue.IssueNumber}";
                var status = $"[{issue.Status}]";
                sb.AppendLine($"  {issue.SeriesTitle} {issueText} {status}");
            }
            sb.AppendLine();
        }

        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Total: {data.Summary.TotalCount} | Wanted: {data.Summary.WantedCount} | Owned: {data.Summary.OwnedCount}");

        return sb.ToString();
    }

    private static string GenerateCsvExport(WeeklyExportData data)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine("SeriesTitle,IssueNumber,IssueNumberText,IssueTitle,Publisher,StoreDate,Status,IsAnnual,IsSpecial,SpecialType");

        // Data rows
        foreach (var issue in data.Issues.OrderBy(i => i.SeriesTitle).ThenBy(i => i.IssueNumber))
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvField(issue.SeriesTitle),
                issue.IssueNumber,
                EscapeCsvField(issue.IssueNumberText ?? ""),
                EscapeCsvField(issue.IssueTitle ?? ""),
                EscapeCsvField(issue.Publisher ?? ""),
                issue.StoreDate?.ToString("yyyy-MM-dd") ?? "",
                issue.Status,
                issue.IsAnnual,
                issue.IsSpecial,
                EscapeCsvField(issue.SpecialType ?? "")
            ));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static int GetIsoWeekNumber(DateTime date)
    {
        // ISO 8601 week number calculation
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    private static DateTime GetDateFromIsoWeek(int year, int week, DayOfWeek targetDay)
    {
        // Get the first day of the year
        var jan4 = new DateTime(year, 1, 4);
        
        // Get the first Monday of the first ISO week
        var daysToMonday = (jan4.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)jan4.DayOfWeek - 1);
        var firstMonday = jan4.AddDays(-daysToMonday);
        
        // Add weeks and adjust to target day
        var targetDate = firstMonday.AddDays((week - 1) * 7);
        var daysToTarget = ((int)targetDay - (int)DayOfWeek.Monday + 7) % 7;
        return targetDate.AddDays(daysToTarget);
    }

    private static async Task<int> GetIssueCountFromFile(string filePath, WeeklyExportFormat format)
    {
        try
        {
            if (format == WeeklyExportFormat.Json && File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<WeeklyExportData>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return data?.Issues.Count ?? 0;
            }
            else if (format == WeeklyExportFormat.Csv && File.Exists(filePath))
            {
                // Count lines minus header
                var lines = await File.ReadAllLinesAsync(filePath);
                return Math.Max(0, lines.Length - 1);
            }
        }
        catch
        {
            // If we can't read the file, return 0
        }
        return 0;
    }

    #endregion

    #region Private Helpers

    private IQueryable<Issue> BuildIssueQuery(PullListFilter? filter)
    {
        var query = _dbContext.Issues
            .Include(i => i.Series)
            .AsQueryable();

        // Default to monitored-only when no filter specified (pull list shows tracked series)
        // This ensures consistency with GetStatsAsync which also filters by monitored
        bool filterByMonitored = filter?.MonitoredOnly ?? true;
        
        if (filterByMonitored)
            query = query.Where(i => i.Series!.Monitored);

        if (filter != null)
        {
            if (filter.SeriesIds?.Any() == true)
                query = query.Where(i => filter.SeriesIds.Contains(i.SeriesId));

            if (filter.Publishers?.Any() == true)
                query = query.Where(i => filter.Publishers.Contains(i.Series!.Publisher!));

            if (filter.Statuses?.Any() == true)
                query = query.Where(i => filter.Statuses.Contains(i.Status));

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

    /// <summary>
    /// Determines the cache tier for a week based on its release day and current settings.
    /// </summary>
    private CacheTier DetermineCacheTier(DateTime releaseDay, PullListSettings settings)
    {
        var now = DateTime.UtcNow;
        var transitionDate = releaseDay.AddDays(settings.CacheBufferDays);
        
        // If we're before or on the transition date, the week is "active"
        if (now.Date <= transitionDate.Date)
        {
            return CacheTier.Active;
        }
        
        return CacheTier.Historical;
    }

    /// <summary>
    /// Gets the cache TTL for a week based on its tier.
    /// </summary>
    private TimeSpan GetCacheTtl(CacheTier tier, PullListSettings settings)
    {
        return tier switch
        {
            CacheTier.Active => TimeSpan.FromMinutes(settings.ActiveCacheTtlMinutes),
            CacheTier.Historical => TimeSpan.FromDays(settings.HistoricalCacheTtlDays),
            _ => TimeSpan.FromMinutes(30) // Default fallback
        };
    }

    /// <summary>
    /// Creates cache metadata for a pull list week.
    /// </summary>
    private PullListCacheMetadata CreateCacheMetadata(
        DateTime releaseDay, 
        PullListSettings settings, 
        bool fromCache,
        DateTime? lastRefreshed = null)
    {
        var now = DateTime.UtcNow;
        var tier = DetermineCacheTier(releaseDay, settings);
        var ttl = GetCacheTtl(tier, settings);
        var refreshedAt = lastRefreshed ?? now;
        var transitionDate = releaseDay.AddDays(settings.CacheBufferDays);
        
        DateTime? nextScheduledRefresh = null;
        if (tier == CacheTier.Active)
        {
            // Active weeks have scheduled refreshes (part of background service)
            nextScheduledRefresh = refreshedAt.Add(ttl);
        }
        else if (settings.HistoricalRefreshEnabled)
        {
            // Historical weeks only refresh if enabled
            nextScheduledRefresh = refreshedAt.AddDays(settings.HistoricalRefreshIntervalDays);
        }
        
        return new PullListCacheMetadata
        {
            LastRefreshed = refreshedAt,
            ExpiresAt = refreshedAt.Add(ttl),
            NextScheduledRefresh = nextScheduledRefresh,
            Tier = tier,
            ReleaseDay = releaseDay,
            TransitionDate = transitionDate,
            FromCache = fromCache
        };
    }

    #endregion
}
