namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for tracking and analyzing download host reliability statistics over time.
/// Provides data for intelligent host selection and priority ordering.
/// </summary>
public interface IHostReliabilityService
{
    /// <summary>
    /// Records a successful download from a host.
    /// </summary>
    Task RecordSuccessAsync(
        string hostId,
        string ddlSiteId,
        long bytesDownloaded,
        TimeSpan downloadDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed download attempt from a host.
    /// </summary>
    Task RecordFailureAsync(
        string hostId,
        string ddlSiteId,
        HostResolverFailureReason failureReason,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reliability statistics for a specific host across all DDL sites.
    /// </summary>
    Task<HostReliabilityStats?> GetHostStatsAsync(
        string hostId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reliability statistics for a specific host on a specific DDL site.
    /// </summary>
    Task<HostReliabilityStats?> GetHostStatsAsync(
        string hostId,
        string ddlSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reliability statistics for all tracked hosts.
    /// </summary>
    Task<IReadOnlyList<HostReliabilityStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reliability statistics for all hosts on a specific DDL site.
    /// </summary>
    Task<IReadOnlyList<HostReliabilityStats>> GetStatsBySiteAsync(
        string ddlSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets hosts ranked by reliability score for a DDL site.
    /// </summary>
    Task<IReadOnlyList<HostReliabilityRanking>> GetHostRankingsAsync(
        string ddlSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets hosts ranked by reliability score across all DDL sites.
    /// </summary>
    Task<IReadOnlyList<HostReliabilityRanking>> GetGlobalHostRankingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the reliability score for a host on a specific site.
    /// </summary>
    Task<double> CalculateReliabilityScoreAsync(
        string hostId,
        string ddlSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recommended host order for downloading from a DDL site.
    /// Considers reliability, speed, and recent failures.
    /// </summary>
    Task<IReadOnlyList<string>> GetRecommendedHostOrderAsync(
        string ddlSiteId,
        IEnumerable<string> availableHosts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate statistics across all hosts and sites.
    /// </summary>
    Task<HostReliabilitySummary> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all statistics for a specific host.
    /// </summary>
    Task ClearHostStatsAsync(
        string hostId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all statistics for a specific DDL site.
    /// </summary>
    Task ClearSiteStatsAsync(
        string ddlSiteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all reliability statistics.
    /// </summary>
    Task ClearAllStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges old statistics beyond the retention period.
    /// </summary>
    Task<int> PurgeOldStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current settings for reliability tracking.
    /// </summary>
    Task<HostReliabilitySettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the reliability tracking settings.
    /// </summary>
    Task SaveSettingsAsync(
        HostReliabilitySettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reliability statistics for a download host.
/// </summary>
public class HostReliabilityStats
{
    /// <summary>
    /// Host identifier (e.g., "mediafire", "mega", "pixeldrain").
    /// </summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the host.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// DDL site identifier (null = aggregate across all sites).
    /// </summary>
    public string? DdlSiteId { get; init; }

    /// <summary>
    /// Total number of successful downloads.
    /// </summary>
    public int TotalSuccesses { get; init; }

    /// <summary>
    /// Total number of failed downloads.
    /// </summary>
    public int TotalFailures { get; init; }

    /// <summary>
    /// Total bytes downloaded successfully.
    /// </summary>
    public long TotalBytesDownloaded { get; init; }

    /// <summary>
    /// Average download speed in bytes per second.
    /// </summary>
    public double AverageSpeedBps { get; init; }

    /// <summary>
    /// Median download speed in bytes per second.
    /// </summary>
    public double MedianSpeedBps { get; init; }

    /// <summary>
    /// Success rate (0-100).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Calculated reliability score (0-100).
    /// </summary>
    public double ReliabilityScore { get; init; }

    /// <summary>
    /// Last successful download time.
    /// </summary>
    public DateTime? LastSuccessTime { get; init; }

    /// <summary>
    /// Last failure time.
    /// </summary>
    public DateTime? LastFailureTime { get; init; }

    /// <summary>
    /// Most recent failure reason.
    /// </summary>
    public HostResolverFailureReason? LastFailureReason { get; init; }

    /// <summary>
    /// Breakdown of failures by reason.
    /// </summary>
    public IReadOnlyDictionary<HostResolverFailureReason, int> FailuresByReason { get; init; }
        = new Dictionary<HostResolverFailureReason, int>();

    /// <summary>
    /// When statistics tracking began.
    /// </summary>
    public DateTime TrackingSince { get; init; }

    /// <summary>
    /// Last time any activity was recorded.
    /// </summary>
    public DateTime LastActivityTime { get; init; }

    /// <summary>
    /// Total number of download attempts.
    /// </summary>
    public int TotalAttempts => TotalSuccesses + TotalFailures;

    /// <summary>
    /// Average file size downloaded in bytes.
    /// </summary>
    public double AverageFileSizeBytes => TotalSuccesses > 0
        ? (double)TotalBytesDownloaded / TotalSuccesses
        : 0;
}

/// <summary>
/// Host ranking based on reliability.
/// </summary>
public class HostReliabilityRanking
{
    /// <summary>
    /// Rank position (1 = best).
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Host identifier.
    /// </summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the host.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// DDL site identifier (null = global ranking).
    /// </summary>
    public string? DdlSiteId { get; init; }

    /// <summary>
    /// Calculated reliability score (0-100).
    /// </summary>
    public double ReliabilityScore { get; init; }

    /// <summary>
    /// Success rate (0-100).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Average download speed in bytes per second.
    /// </summary>
    public double AverageSpeedBps { get; init; }

    /// <summary>
    /// Total number of downloads tracked.
    /// </summary>
    public int TotalAttempts { get; init; }

    /// <summary>
    /// Whether this host is currently blacklisted.
    /// </summary>
    public bool IsBlacklisted { get; init; }

    /// <summary>
    /// Trend indicator: improving, stable, or declining.
    /// </summary>
    public ReliabilityTrend Trend { get; init; }
}

/// <summary>
/// Trend in host reliability.
/// </summary>
public enum ReliabilityTrend
{
    /// <summary>
    /// Not enough data to determine trend.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Reliability is improving over time.
    /// </summary>
    Improving = 1,

    /// <summary>
    /// Reliability is stable.
    /// </summary>
    Stable = 2,

    /// <summary>
    /// Reliability is declining.
    /// </summary>
    Declining = 3
}

/// <summary>
/// Summary of host reliability across all hosts and sites.
/// </summary>
public class HostReliabilitySummary
{
    /// <summary>
    /// Total number of hosts tracked.
    /// </summary>
    public int TotalHostsTracked { get; init; }

    /// <summary>
    /// Total number of DDL sites with data.
    /// </summary>
    public int TotalSitesTracked { get; init; }

    /// <summary>
    /// Total successful downloads across all hosts.
    /// </summary>
    public int TotalSuccesses { get; init; }

    /// <summary>
    /// Total failed downloads across all hosts.
    /// </summary>
    public int TotalFailures { get; init; }

    /// <summary>
    /// Overall success rate (0-100).
    /// </summary>
    public double OverallSuccessRate { get; init; }

    /// <summary>
    /// Total bytes downloaded across all hosts.
    /// </summary>
    public long TotalBytesDownloaded { get; init; }

    /// <summary>
    /// Average speed across all hosts (bytes per second).
    /// </summary>
    public double OverallAverageSpeedBps { get; init; }

    /// <summary>
    /// Most reliable host globally.
    /// </summary>
    public string? MostReliableHost { get; init; }

    /// <summary>
    /// Fastest host by average speed.
    /// </summary>
    public string? FastestHost { get; init; }

    /// <summary>
    /// Least reliable host globally.
    /// </summary>
    public string? LeastReliableHost { get; init; }

    /// <summary>
    /// When tracking data begins.
    /// </summary>
    public DateTime? TrackingSince { get; init; }

    /// <summary>
    /// Most recent activity.
    /// </summary>
    public DateTime? LastActivityTime { get; init; }
}

/// <summary>
/// Settings for host reliability tracking.
/// </summary>
public class HostReliabilitySettings
{
    /// <summary>
    /// Whether reliability tracking is enabled.
    /// </summary>
    public bool TrackingEnabled { get; set; } = true;

    /// <summary>
    /// How long to retain statistics.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Minimum number of attempts before calculating reliability score.
    /// </summary>
    public int MinAttemptsForScore { get; set; } = 5;

    /// <summary>
    /// Weight given to success rate in reliability score (0-1).
    /// </summary>
    public double SuccessRateWeight { get; set; } = 0.6;

    /// <summary>
    /// Weight given to download speed in reliability score (0-1).
    /// </summary>
    public double SpeedWeight { get; set; } = 0.3;

    /// <summary>
    /// Weight given to recency in reliability score (0-1).
    /// </summary>
    public double RecencyWeight { get; set; } = 0.1;

    /// <summary>
    /// Whether to use reliability scores for host ordering.
    /// </summary>
    public bool UseForHostOrdering { get; set; } = true;

    /// <summary>
    /// How many recent attempts to use for trend calculation.
    /// </summary>
    public int TrendWindowSize { get; set; } = 10;

    /// <summary>
    /// Success rate change threshold for trend detection (percentage points).
    /// </summary>
    public double TrendChangeThreshold { get; set; } = 10.0;
}

/// <summary>
/// Individual download record for detailed tracking.
/// </summary>
public class HostDownloadRecord
{
    /// <summary>
    /// Unique identifier for this record.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Host identifier.
    /// </summary>
    public string HostId { get; init; } = string.Empty;

    /// <summary>
    /// DDL site identifier.
    /// </summary>
    public string DdlSiteId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the download was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Bytes downloaded (for successful downloads).
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Download duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Download speed in bytes per second.
    /// </summary>
    public double SpeedBps => Duration.TotalSeconds > 0
        ? BytesDownloaded / Duration.TotalSeconds
        : 0;

    /// <summary>
    /// Failure reason (for failed downloads).
    /// </summary>
    public HostResolverFailureReason? FailureReason { get; init; }

    /// <summary>
    /// Error message (for failed downloads).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When this download was recorded.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
