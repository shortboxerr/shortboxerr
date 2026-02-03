namespace Shortboxerr.Core.PullList;

/// <summary>
/// Service for managing the weekly pull list and release calendar.
/// </summary>
public interface IPullListService
{
    #region Calendar & Release Tracking
    
    /// <summary>
    /// Gets releases for a specific week.
    /// </summary>
    Task<WeeklyPullList> GetWeeklyReleasesAsync(
        DateTime weekOf,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets this week's releases.
    /// </summary>
    Task<WeeklyPullList> GetThisWeekAsync(
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming releases for the next N weeks.
    /// </summary>
    Task<List<WeeklyPullList>> GetUpcomingReleasesAsync(
        int weeks = 4,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets past releases for the last N weeks.
    /// </summary>
    Task<List<WeeklyPullList>> GetPastReleasesAsync(
        int weeks = 4,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full calendar view for a date range.
    /// </summary>
    Task<ReleaseCalendar> GetCalendarAsync(
        DateTime startDate,
        DateTime endDate,
        PullListFilter? filter = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Issue Management

    /// <summary>
    /// Marks an issue as wanted (add to pull list).
    /// </summary>
    Task<PullListActionResult> MarkAsWantedAsync(
        int issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an issue as owned.
    /// </summary>
    Task<PullListActionResult> MarkAsOwnedAsync(
        int issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an issue as skipped (don't want).
    /// </summary>
    Task<PullListActionResult> MarkAsSkippedAsync(
        int issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk action on multiple issues.
    /// </summary>
    Task<PullListBulkResult> BulkUpdateStatusAsync(
        IEnumerable<int> issueIds,
        Entities.IssueStatus newStatus,
        CancellationToken cancellationToken = default);

    #endregion

    #region Auto-Add & Monitoring

    /// <summary>
    /// Processes newly discovered issues and auto-adds based on monitoring mode.
    /// </summary>
    Task<AutoAddResult> ProcessNewIssuesAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes all series for new issues on release day.
    /// </summary>
    Task<AutoAddResult> ProcessReleaseDayAsync(
        DateTime releaseDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets series monitoring mode.
    /// </summary>
    Task<ComicVine.SeriesMonitoringMode> GetSeriesMonitoringModeAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates series monitoring mode.
    /// </summary>
    Task<PullListActionResult> SetSeriesMonitoringModeAsync(
        int seriesId,
        ComicVine.SeriesMonitoringMode mode,
        CancellationToken cancellationToken = default);

    #endregion

    #region Statistics

    /// <summary>
    /// Gets pull list statistics.
    /// </summary>
    Task<PullListStats> GetStatsAsync(
        CancellationToken cancellationToken = default);

    #endregion

    #region Settings

    /// <summary>
    /// Gets pull list settings.
    /// </summary>
    Task<PullListSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates pull list settings.
    /// </summary>
    Task<PullListActionResult> UpdateSettingsAsync(
        PullListSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets per-series pull list settings.
    /// </summary>
    Task<SeriesPullListSettings?> GetSeriesSettingsAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates per-series pull list settings.
    /// </summary>
    Task<PullListActionResult> UpdateSeriesSettingsAsync(
        SeriesPullListSettings settings,
        CancellationToken cancellationToken = default);

    #endregion
}

#region Models

/// <summary>
/// A week's worth of comic releases.
/// </summary>
public class WeeklyPullList
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public DateTime ReleaseDay { get; set; } // Typically Wednesday
    public List<PullListIssue> Issues { get; set; } = new();
    public int TotalCount => Issues.Count;
    public int WantedCount => Issues.Count(i => i.Status == Entities.IssueStatus.Wanted);
    public int OwnedCount => Issues.Count(i => i.Status == Entities.IssueStatus.Owned);
    public int SkippedCount => Issues.Count(i => i.Status == Entities.IssueStatus.Skipped);
}

/// <summary>
/// Issue in the pull list with series context.
/// </summary>
public class PullListIssue
{
    public int IssueId { get; set; }
    public int SeriesId { get; set; }
    public string SeriesTitle { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public decimal IssueNumber { get; set; }
    public string? IssueNumberText { get; set; }
    public string? IssueTitle { get; set; }
    public DateTime? StoreDate { get; set; }
    public DateTime? CoverDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public Entities.IssueStatus Status { get; set; }
    public bool IsAnnual { get; set; }
    public bool IsSpecial { get; set; }
    public string? SpecialType { get; set; }
}

/// <summary>
/// Release calendar spanning multiple weeks.
/// </summary>
public class ReleaseCalendar
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CalendarDay> Days { get; set; } = new();
    public Dictionary<string, List<PullListIssue>> ByPublisher { get; set; } = new();
    public Dictionary<int, List<PullListIssue>> BySeries { get; set; } = new();
}

/// <summary>
/// A single day in the release calendar.
/// </summary>
public class CalendarDay
{
    public DateTime Date { get; set; }
    public bool IsReleaseDay { get; set; }
    public List<PullListIssue> Issues { get; set; } = new();
}

/// <summary>
/// Filter options for pull list queries.
/// </summary>
public class PullListFilter
{
    public List<int>? SeriesIds { get; set; }
    public List<string>? Publishers { get; set; }
    public List<Entities.IssueStatus>? Statuses { get; set; }
    public bool? MonitoredOnly { get; set; }
    public bool IncludeAnnuals { get; set; } = true;
    public bool IncludeSpecials { get; set; } = true;
}

/// <summary>
/// Result of a single pull list action.
/// </summary>
public class PullListActionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? IssueId { get; set; }
    public Entities.IssueStatus? NewStatus { get; set; }
}

/// <summary>
/// Result of a bulk pull list action.
/// </summary>
public class PullListBulkResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<int> FailedIssueIds { get; set; } = new();
}

/// <summary>
/// Result of auto-add processing.
/// </summary>
public class AutoAddResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SeriesProcessed { get; set; }
    public int IssuesAdded { get; set; }
    public int IssuesSkipped { get; set; }
    public List<PullListIssue> AddedIssues { get; set; } = new();
}

/// <summary>
/// Pull list statistics.
/// </summary>
public class PullListStats
{
    public int TotalMonitoredSeries { get; set; }
    public int TotalWantedIssues { get; set; }
    public int TotalOwnedIssues { get; set; }
    public int TotalSkippedIssues { get; set; }
    public int ReleasingThisWeek { get; set; }
    public int ReleasingNextWeek { get; set; }
    public int MissedIssues { get; set; } // Past releases that are still wanted
    public Dictionary<string, int> WantedByPublisher { get; set; } = new();
}

/// <summary>
/// Pull list configuration settings.
/// </summary>
public class PullListSettings
{
    /// <summary>
    /// Day of week that starts a new comic week (default: Sunday).
    /// </summary>
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Day of week when comics are released (default: Wednesday).
    /// </summary>
    public DayOfWeek ReleaseDay { get; set; } = DayOfWeek.Wednesday;

    /// <summary>
    /// Default monitoring mode for newly added series.
    /// </summary>
    public ComicVine.SeriesMonitoringMode DefaultMonitoringMode { get; set; } = ComicVine.SeriesMonitoringMode.FutureIssues;

    /// <summary>
    /// Hours after release to wait before triggering auto-search (allows for proper uploads).
    /// </summary>
    public int SearchDelayHours { get; set; } = 6;

    /// <summary>
    /// Whether to automatically mark new issues as wanted based on monitoring mode.
    /// </summary>
    public bool AutoAddToWanted { get; set; } = true;

    /// <summary>
    /// Whether to include annual issues in auto-add.
    /// </summary>
    public bool IncludeAnnualsInAutoAdd { get; set; } = true;

    /// <summary>
    /// Whether to include special issues in auto-add.
    /// </summary>
    public bool IncludeSpecialsInAutoAdd { get; set; } = false;

    /// <summary>
    /// Whether to skip variant covers (issues with letters like "1A", "1B").
    /// </summary>
    public bool SkipVariantCovers { get; set; } = true;

    /// <summary>
    /// Number of weeks to show in upcoming view.
    /// </summary>
    public int UpcomingWeeksToShow { get; set; } = 4;

    /// <summary>
    /// Number of weeks to show in past view.
    /// </summary>
    public int PastWeeksToShow { get; set; } = 4;
}

/// <summary>
/// Per-series pull list overrides.
/// </summary>
public class SeriesPullListSettings
{
    public int SeriesId { get; set; }
    public ComicVine.SeriesMonitoringMode? MonitoringModeOverride { get; set; }
    public bool? IncludeAnnuals { get; set; }
    public bool? IncludeSpecials { get; set; }
    public bool? SkipVariants { get; set; }
    
    /// <summary>
    /// Priority for search ordering (higher = searched first). Default is 0.
    /// </summary>
    public int SearchPriority { get; set; } = 0;
}

#endregion
