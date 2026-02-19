namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Service for monitoring and tracking the health of NZB indexers.
/// </summary>
public interface IIndexerHealthService
{
    /// <summary>
    /// Gets the health status for a specific indexer.
    /// </summary>
    Task<IndexerHealthStatus> GetHealthAsync(string indexerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health status for all indexers.
    /// </summary>
    Task<IReadOnlyList<IndexerHealthStatus>> GetAllHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful request to an indexer.
    /// </summary>
    Task RecordSuccessAsync(string indexerId, TimeSpan responseTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed request to an indexer.
    /// </summary>
    Task RecordFailureAsync(string indexerId, string errorMessage, bool isRateLimited = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next available indexer based on health status and rate limits.
    /// Returns indexers in priority order, skipping unhealthy or rate-limited ones.
    /// </summary>
    Task<IReadOnlyList<NewznabIndexer>> GetHealthyIndexersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an indexer is currently rate limited.
    /// </summary>
    Task<bool> IsRateLimitedAsync(string indexerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on a specific indexer.
    /// </summary>
    Task<IndexerHealthCheckResult> CheckHealthAsync(string indexerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs health checks on all enabled indexers.
    /// </summary>
    Task<IReadOnlyList<IndexerHealthCheckResult>> CheckAllHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the health status for an indexer (e.g., after manual intervention).
    /// </summary>
    Task ResetHealthAsync(string indexerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated health statistics.
    /// </summary>
    Task<IndexerHealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status for a single indexer.
/// </summary>
public record IndexerHealthStatus
{
    /// <summary>
    /// Indexer ID.
    /// </summary>
    public required string IndexerId { get; init; }

    /// <summary>
    /// Indexer name.
    /// </summary>
    public required string IndexerName { get; init; }

    /// <summary>
    /// Overall health state.
    /// </summary>
    public IndexerHealthState State { get; init; }

    /// <summary>
    /// Whether the indexer is currently operational.
    /// </summary>
    public bool IsHealthy => State == IndexerHealthState.Healthy || State == IndexerHealthState.Degraded;

    /// <summary>
    /// Whether the indexer is currently rate limited.
    /// </summary>
    public bool IsRateLimited { get; init; }

    /// <summary>
    /// When rate limiting expires (if rate limited).
    /// </summary>
    public DateTime? RateLimitExpiresAt { get; init; }

    /// <summary>
    /// Average response time in milliseconds (recent requests).
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// Last recorded response time in milliseconds.
    /// </summary>
    public double? LastResponseTimeMs { get; init; }

    /// <summary>
    /// Number of successful requests in the tracking window.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed requests in the tracking window.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;

    /// <summary>
    /// Last successful request time.
    /// </summary>
    public DateTime? LastSuccessAt { get; init; }

    /// <summary>
    /// Last failure time.
    /// </summary>
    public DateTime? LastFailureAt { get; init; }

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastErrorMessage { get; init; }

    /// <summary>
    /// Consecutive failure count.
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// When this status was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }
}

/// <summary>
/// Health state of an indexer.
/// </summary>
public enum IndexerHealthState
{
    /// <summary>
    /// Unknown state (never checked).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Indexer is healthy and responding normally.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Indexer is responding but with degraded performance.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Indexer is temporarily unavailable (rate limited or temporary error).
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// Indexer has failed multiple times and is considered offline.
    /// </summary>
    Offline = 4
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public record IndexerHealthCheckResult
{
    /// <summary>
    /// Indexer ID.
    /// </summary>
    public required string IndexerId { get; init; }

    /// <summary>
    /// Indexer name.
    /// </summary>
    public required string IndexerName { get; init; }

    /// <summary>
    /// Whether the check was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// HTTP status code (if applicable).
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Whether the failure was due to rate limiting.
    /// </summary>
    public bool IsRateLimited { get; init; }

    /// <summary>
    /// When the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; }
}

/// <summary>
/// Aggregated health summary across all indexers.
/// </summary>
public record IndexerHealthSummary
{
    /// <summary>
    /// Total number of configured indexers.
    /// </summary>
    public int TotalIndexers { get; init; }

    /// <summary>
    /// Number of enabled indexers.
    /// </summary>
    public int EnabledIndexers { get; init; }

    /// <summary>
    /// Number of healthy indexers.
    /// </summary>
    public int HealthyIndexers { get; init; }

    /// <summary>
    /// Number of degraded indexers.
    /// </summary>
    public int DegradedIndexers { get; init; }

    /// <summary>
    /// Number of unavailable indexers.
    /// </summary>
    public int UnavailableIndexers { get; init; }

    /// <summary>
    /// Number of offline indexers.
    /// </summary>
    public int OfflineIndexers { get; init; }

    /// <summary>
    /// Number of currently rate-limited indexers.
    /// </summary>
    public int RateLimitedIndexers { get; init; }

    /// <summary>
    /// Average response time across all healthy indexers (ms).
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// Overall health percentage (healthy + degraded / enabled).
    /// </summary>
    public double OverallHealthPercent => EnabledIndexers > 0
        ? (double)(HealthyIndexers + DegradedIndexers) / EnabledIndexers * 100
        : 0;

    /// <summary>
    /// When this summary was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}
