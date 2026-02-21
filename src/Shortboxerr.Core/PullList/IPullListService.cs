using System.Text.Json.Serialization;

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

    /// <summary>
    /// Gets all ComicVine releases for a week (discovery mode - Mylar3 "This Week" parity).
    /// Includes both monitored and unmonitored series.
    /// </summary>
    Task<WeeklyDiscoveryList> GetWeeklyDiscoveryAsync(
        DateTime weekOf,
        DiscoveryFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available publishers for discovery filtering for a specific week.
    /// Returns publishers from library series that have releases this week,
    /// plus optionally fetches publisher info from ComicVine for new series.
    /// </summary>
    Task<DiscoveryPublishersResult> GetDiscoveryPublishersAsync(
        DateTime weekOf,
        bool includeComicVineLookup = false,
        CancellationToken cancellationToken = default);

    #endregion

    #region Discovery & One-Off Additions

    /// <summary>
    /// Adds a single issue as wanted without fully adding the series (one-off).
    /// Creates minimal series record if needed.
    /// </summary>
    Task<AddOneOffResult> AddIssueOneOffAsync(
        int comicVineIssueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a series from discovery and optionally marks the current issue as wanted.
    /// </summary>
    Task<AddFromDiscoveryResult> AddSeriesFromDiscoveryAsync(
        int comicVineVolumeId,
        int? markIssueWantedComicVineId = null,
        ComicVine.SeriesMonitoringMode monitoringMode = ComicVine.SeriesMonitoringMode.FutureIssues,
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

    /// <summary>
    /// Gets pull list configuration status for UX improvements.
    /// </summary>
    Task<PullListConfigStatus> GetConfigStatusAsync(
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

    #region Weekly Export (Mylar3 Parity)

    /// <summary>
    /// Exports the current week's pull list to a file.
    /// </summary>
    Task<WeeklyExportResult> ExportCurrentWeekAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a specific week's pull list to a file.
    /// </summary>
    Task<WeeklyExportResult> ExportWeekAsync(
        DateTime weekOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets export history (list of exported weeks).
    /// </summary>
    Task<List<WeeklyExportInfo>> GetExportHistoryAsync(
        int limit = 10,
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
    
    /// <summary>
    /// Metadata about the cache state for this week's data.
    /// </summary>
    public PullListCacheMetadata? CacheMetadata { get; set; }
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
/// Configuration status for pull list UX improvements.
/// </summary>
public class PullListConfigStatus
{
    /// <summary>
    /// Whether ComicVine API key is configured.
    /// </summary>
    public bool IsComicVineConfigured { get; set; }

    /// <summary>
    /// Total number of series in the library.
    /// </summary>
    public int TotalSeriesCount { get; set; }

    /// <summary>
    /// Number of series matched to ComicVine.
    /// </summary>
    public int MatchedSeriesCount { get; set; }

    /// <summary>
    /// Number of monitored series.
    /// </summary>
    public int MonitoredSeriesCount { get; set; }

    /// <summary>
    /// Timestamp when ComicVine discovery cache was last refreshed.
    /// </summary>
    public DateTime? DiscoveryCacheLastRefreshed { get; set; }

    /// <summary>
    /// Whether there are any issues releasing this week.
    /// </summary>
    public bool HasReleasesThisWeek { get; set; }

    /// <summary>
    /// Suggested next action for the user.
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Action type for UI routing.
    /// </summary>
    public PullListSuggestedActionType ActionType { get; set; }
}

/// <summary>
/// Action types for pull list UX guidance.
/// </summary>
public enum PullListSuggestedActionType
{
    None = 0,
    ConfigureApiKey = 1,
    AddSeries = 2,
    MatchSeries = 3,
    TryAllReleases = 4
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
    /// Whether to enable series-annual integration (Mylar3 parity).
    /// When true: Annual series (e.g., "Batman Annual") are hidden from the main series list
    /// and their issues appear in the parent series' Annuals section.
    /// When false: Annual series appear as separate entries in the series list.
    /// Default: true (for Mylar3 parity).
    /// </summary>
    /// <remarks>
    /// Nullable to distinguish between "not set" (null, defaults to true) and "explicitly set to false".
    /// Use GetEffectiveEnableSeriesAnnualIntegration() or ?? true to get the effective value.
    /// </remarks>
    [JsonPropertyName("enableSeriesAnnualIntegration")]
    public bool? EnableSeriesAnnualIntegration { get; set; }

    /// <summary>
    /// Whether to skip variant covers (issues with letters like "1A", "1B").
    /// </summary>
    public bool SkipVariantCovers { get; set; } = true;

    /// <summary>
    /// Specific hours of the day (0-23) when release day processing is allowed.
    /// Empty list means all hours are allowed. 
    /// Default: 6am, 12pm to allow time for ComicVine data to be updated.
    /// </summary>
    public List<int> ReleaseDayProcessingHours { get; set; } = new() { 6, 12 };

    /// <summary>
    /// Number of weeks to show in upcoming view.
    /// </summary>
    public int UpcomingWeeksToShow { get; set; } = 4;

    /// <summary>
    /// Number of weeks to show in past view.
    /// </summary>
    public int PastWeeksToShow { get; set; } = 4;

    #region Weekly Export Settings (Mylar3 Parity)

    /// <summary>
    /// Whether to enable weekly pull list export to file.
    /// </summary>
    public bool ExportWeeklyPullList { get; set; } = false;

    /// <summary>
    /// Directory for weekly export files (under comics root).
    /// </summary>
    public string? WeeklyExportDirectory { get; set; }

    /// <summary>
    /// Format for weekly export files.
    /// </summary>
    public WeeklyExportFormat WeeklyExportFormat { get; set; } = WeeklyExportFormat.Json;

    /// <summary>
    /// Whether to automatically export on release day.
    /// </summary>
    public bool AutoExportOnReleaseDay { get; set; } = true;

    /// <summary>
    /// Fields to include in export (null = all fields).
    /// </summary>
    public List<string>? ExportFields { get; set; }

    #endregion

    #region Cache Tier Settings (Intelligent Cache Lifecycle)

    /// <summary>
    /// Number of days after release day to continue active cache refresh.
    /// During this buffer period, cache refreshes more frequently.
    /// After buffer expires, week becomes "historical" with longer cache TTL.
    /// Default: 2 days (e.g., Wednesday release + 2 = Friday cutoff)
    /// </summary>
    public int CacheBufferDays { get; set; } = 2;

    /// <summary>
    /// Cache TTL (in days) for historical weeks (past release day + buffer).
    /// Historical data rarely changes, so longer TTL conserves API calls.
    /// Default: 7 days
    /// </summary>
    public int HistoricalCacheTtlDays { get; set; } = 7;

    /// <summary>
    /// Whether to enable periodic refresh of historical cache data.
    /// If false, historical data only refreshes on manual request.
    /// Default: false (conserves API calls)
    /// </summary>
    public bool HistoricalRefreshEnabled { get; set; } = false;

    /// <summary>
    /// Interval (in days) between historical cache refreshes.
    /// Only applies when HistoricalRefreshEnabled is true.
    /// Default: 7 days
    /// </summary>
    public int HistoricalRefreshIntervalDays { get; set; } = 7;

    /// <summary>
    /// Cache TTL (in minutes) for active weeks (before/on release day + buffer).
    /// Shorter TTL allows capturing last-minute changes.
    /// Default: 30 minutes
    /// </summary>
    public int ActiveCacheTtlMinutes { get; set; } = 30;

    #endregion
}

/// <summary>
/// Format options for weekly export files.
/// </summary>
public enum WeeklyExportFormat
{
    /// <summary>JSON format - structured data for programmatic access.</summary>
    Json = 0,
    /// <summary>Plain text format - human-readable list.</summary>
    Text = 1,
    /// <summary>CSV format - spreadsheet compatible.</summary>
    Csv = 2
}

/// <summary>
/// Cache tier indicating the refresh behavior for a week's pull list data.
/// </summary>
public enum CacheTier
{
    /// <summary>
    /// Week is before or on release day + buffer period.
    /// Cache refreshes frequently to capture last-minute changes.
    /// </summary>
    Active,
    
    /// <summary>
    /// Week is past release day + buffer period.
    /// Cache has longer TTL; data rarely changes.
    /// </summary>
    Historical
}

/// <summary>
/// Metadata about cached pull list data.
/// </summary>
public class PullListCacheMetadata
{
    /// <summary>
    /// When the data was last fetched from ComicVine.
    /// </summary>
    public DateTime LastRefreshed { get; set; }
    
    /// <summary>
    /// When the cache entry will expire.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Next scheduled background refresh (null if manual refresh only).
    /// </summary>
    public DateTime? NextScheduledRefresh { get; set; }
    
    /// <summary>
    /// Current cache tier for this week.
    /// </summary>
    public CacheTier Tier { get; set; }
    
    /// <summary>
    /// The release day for this week.
    /// </summary>
    public DateTime ReleaseDay { get; set; }
    
    /// <summary>
    /// When this week transitions from Active to Historical tier.
    /// </summary>
    public DateTime TransitionDate { get; set; }
    
    /// <summary>
    /// Whether data is currently from cache (vs fresh fetch).
    /// </summary>
    public bool FromCache { get; set; }
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

#region Discovery Models (Mylar3 "This Week" Parity)

/// <summary>
/// A week's worth of discoverable releases from ComicVine.
/// </summary>
public class WeeklyDiscoveryList
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public DateTime ReleaseDay { get; set; }
    public List<DiscoverableIssue> Issues { get; set; } = new();
    public int TotalCount => Issues.Count;
    public int InLibraryCount => Issues.Count(i => i.IsInLibrary);
    public int NewCount => Issues.Count(i => !i.IsInLibrary);
    
    /// <summary>
    /// Metadata about the cache state for this week's data.
    /// </summary>
    public PullListCacheMetadata? CacheMetadata { get; set; }
}

/// <summary>
/// An issue available for discovery (may or may not be in library).
/// </summary>
public class DiscoverableIssue
{
    // ComicVine identifiers
    public int ComicVineIssueId { get; set; }
    public int ComicVineVolumeId { get; set; }
    
    // Series info
    public string SeriesTitle { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int? StartYear { get; set; }
    
    // Issue info
    public decimal IssueNumber { get; set; }
    public string? IssueNumberText { get; set; }
    public string? IssueTitle { get; set; }
    public DateTime? StoreDate { get; set; }
    public DateTime? CoverDate { get; set; }
    public string? CoverImageUrl { get; set; }
    
    // Library status
    public bool IsInLibrary { get; set; }
    public int? LocalSeriesId { get; set; }
    public int? LocalIssueId { get; set; }
    public Entities.IssueStatus? Status { get; set; }
    public bool IsSeriesMonitored { get; set; }
}

/// <summary>
/// Filter options for discovery queries.
/// </summary>
public class DiscoveryFilter
{
    public List<string>? Publishers { get; set; }
    public bool? InLibraryOnly { get; set; }
    public bool? NewOnly { get; set; }
    public bool IncludeAnnuals { get; set; } = true;
    public bool IncludeSpecials { get; set; } = true;
}

/// <summary>
/// Result of fetching discovery publishers for filter dropdown.
/// </summary>
public class DiscoveryPublishersResult
{
    /// <summary>
    /// Publishers from series in the local library that have releases this week.
    /// </summary>
    public List<DiscoveryPublisher> LibraryPublishers { get; set; } = new();
    
    /// <summary>
    /// Publishers from ComicVine for series not in the local library.
    /// Only populated if includeComicVineLookup is true.
    /// </summary>
    public List<DiscoveryPublisher> ComicVinePublishers { get; set; } = new();
    
    /// <summary>
    /// All unique publishers merged, sorted alphabetically.
    /// </summary>
    public List<DiscoveryPublisher> AllPublishers { get; set; } = new();
    
    /// <summary>
    /// Week this data is for.
    /// </summary>
    public DateTime WeekOf { get; set; }
    
    /// <summary>
    /// Total issue count for this week.
    /// </summary>
    public int TotalIssueCount { get; set; }
    
    /// <summary>
    /// Whether ComicVine lookup was performed.
    /// </summary>
    public bool IncludedComicVineLookup { get; set; }
}

/// <summary>
/// Publisher info for discovery filter dropdown.
/// </summary>
public class DiscoveryPublisher
{
    /// <summary>
    /// Publisher name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of issues releasing this week from this publisher.
    /// </summary>
    public int IssueCount { get; set; }
    
    /// <summary>
    /// Number of series releasing this week from this publisher.
    /// </summary>
    public int SeriesCount { get; set; }
    
    /// <summary>
    /// Whether any of these series are in the local library.
    /// </summary>
    public bool HasLibrarySeries { get; set; }
    
    /// <summary>
    /// ComicVine publisher ID (if available).
    /// </summary>
    public int? ComicVinePublisherId { get; set; }
}

/// <summary>
/// Result of adding a one-off issue.
/// </summary>
public class AddOneOffResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? IssueId { get; set; }
    public int? SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public decimal? IssueNumber { get; set; }
    public bool SeriesCreated { get; set; }
}

/// <summary>
/// Result of adding a series from discovery.
/// </summary>
public class AddFromDiscoveryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public int IssuesCreated { get; set; }
    public int? MarkedWantedIssueId { get; set; }
    public bool AlreadyExists { get; set; }
}

#endregion

#region Weekly Export Models (Mylar3 Parity)

/// <summary>
/// Result of a weekly export operation.
/// </summary>
public class WeeklyExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    /// <summary>Directory path where export was saved.</summary>
    public string? ExportDirectory { get; set; }
    
    /// <summary>Full file path of the exported file.</summary>
    public string? ExportFilePath { get; set; }
    
    /// <summary>Format of the export.</summary>
    public WeeklyExportFormat Format { get; set; }
    
    /// <summary>Week information.</summary>
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public DateTime ReleaseDay { get; set; }
    
    /// <summary>Export content statistics.</summary>
    public int TotalIssues { get; set; }
    public int WantedIssues { get; set; }
    public int OwnedIssues { get; set; }
    
    /// <summary>Timestamp when export was created.</summary>
    public DateTime ExportedAt { get; set; }
}

/// <summary>
/// Information about a previously exported week.
/// </summary>
public class WeeklyExportInfo
{
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public DateTime ReleaseDay { get; set; }
    public string DirectoryPath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public WeeklyExportFormat Format { get; set; }
    public DateTime ExportedAt { get; set; }
    public long FileSizeBytes { get; set; }
    public int IssueCount { get; set; }
}

/// <summary>
/// Data structure for weekly export file content.
/// </summary>
public class WeeklyExportData
{
    /// <summary>Metadata about the export.</summary>
    public WeeklyExportMetadata Metadata { get; set; } = new();
    
    /// <summary>List of issues releasing this week.</summary>
    public List<WeeklyExportIssue> Issues { get; set; } = new();
    
    /// <summary>Summary statistics.</summary>
    public WeeklyExportSummary Summary { get; set; } = new();
}

/// <summary>
/// Metadata for the weekly export file.
/// </summary>
public class WeeklyExportMetadata
{
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public DateTime ReleaseDay { get; set; }
    public DateTime ExportedAt { get; set; }
    public string ExportVersion { get; set; } = "1.0";
}

/// <summary>
/// Issue data for weekly export.
/// </summary>
public class WeeklyExportIssue
{
    public string SeriesTitle { get; set; } = string.Empty;
    public decimal IssueNumber { get; set; }
    public string? IssueNumberText { get; set; }
    public string? IssueTitle { get; set; }
    public string? Publisher { get; set; }
    public DateTime? StoreDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ComicVineIssueId { get; set; }
    public int? ComicVineVolumeId { get; set; }
    public bool IsAnnual { get; set; }
    public bool IsSpecial { get; set; }
    public string? SpecialType { get; set; }
}

/// <summary>
/// Summary statistics for weekly export.
/// </summary>
public class WeeklyExportSummary
{
    public int TotalCount { get; set; }
    public int WantedCount { get; set; }
    public int OwnedCount { get; set; }
    public int SkippedCount { get; set; }
    public int MissingCount { get; set; }
    public Dictionary<string, int> ByPublisher { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
}

#endregion
