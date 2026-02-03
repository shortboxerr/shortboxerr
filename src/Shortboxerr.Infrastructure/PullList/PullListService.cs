using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.PullList;

/// <summary>
/// Implementation of IPullListService for managing the weekly pull list.
/// </summary>
public class PullListService : IPullListService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ILogger<PullListService> _logger;

    // Comics typically release on Wednesday in the US
    private const DayOfWeek DefaultReleaseDay = DayOfWeek.Wednesday;

    public PullListService(
        ShortboxerrDbContext dbContext,
        ILogger<PullListService> logger)
    {
        _dbContext = dbContext;
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

        var issues = await query
            .OrderBy(i => i.StoreDate)
            .ThenBy(i => i.Series!.Title)
            .ThenBy(i => i.IssueNumber)
            .ToListAsync(cancellationToken);

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
