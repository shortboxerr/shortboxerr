using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Providers;

/// <summary>
/// Service for monitoring download client health and managing failover.
/// </summary>
public interface IDownloadClientHealthService
{
    /// <summary>
    /// Gets the health status for a specific download client.
    /// </summary>
    Task<DownloadClientHealthStatus> GetHealthAsync(int providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health status for all download clients.
    /// </summary>
    Task<IReadOnlyList<DownloadClientHealthStatus>> GetAllHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful download operation.
    /// </summary>
    Task RecordSuccessAsync(int providerId, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed download operation.
    /// </summary>
    Task RecordFailureAsync(int providerId, string errorMessage, bool isTransient = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets download clients ordered by health and priority for failover.
    /// Excludes clients that are currently unhealthy or have too many failures.
    /// </summary>
    Task<IReadOnlyList<IDownloadProvider>> GetHealthyClientsAsync(ProviderType? type = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a download client is available for downloads.
    /// </summary>
    Task<bool> IsAvailableAsync(int providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on a specific download client.
    /// </summary>
    Task<DownloadClientCheckResult> CheckHealthAsync(int providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs health checks on all enabled download clients.
    /// </summary>
    Task<IReadOnlyList<DownloadClientCheckResult>> CheckAllHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the health status for a download client.
    /// </summary>
    Task ResetHealthAsync(int providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated health summary for all download clients.
    /// </summary>
    Task<DownloadClientHealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to download using available clients with automatic failover.
    /// </summary>
    /// <param name="candidate">The candidate to download.</param>
    /// <param name="preferredType">Preferred provider type (Usenet, Torrent, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download result from the first successful client, or failure details.</returns>
    Task<FailoverDownloadResult> DownloadWithFailoverAsync(
        Candidate candidate,
        ProviderType? preferredType = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status for a single download client.
/// </summary>
public record DownloadClientHealthStatus
{
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public ProviderType Type { get; init; }
    public DownloadClientState State { get; init; }
    public bool IsHealthy => State == DownloadClientState.Unknown || State == DownloadClientState.Healthy || State == DownloadClientState.Degraded;
    public double AverageDownloadTimeSeconds { get; init; }
    public double? LastDownloadTimeSeconds { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double SuccessRate => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;
    public DateTime? LastSuccessAt { get; init; }
    public DateTime? LastFailureAt { get; init; }
    public string? LastErrorMessage { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime LastUpdatedAt { get; init; }
}

/// <summary>
/// Health state of a download client.
/// </summary>
public enum DownloadClientState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unavailable = 3,
    Offline = 4
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public record DownloadClientCheckResult
{
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public ProviderType Type { get; init; }
    public bool Success { get; init; }
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CheckedAt { get; init; }
}

/// <summary>
/// Aggregated health summary across all download clients.
/// </summary>
public record DownloadClientHealthSummary
{
    public int TotalClients { get; init; }
    public int EnabledClients { get; init; }
    public int HealthyClients { get; init; }
    public int DegradedClients { get; init; }
    public int UnavailableClients { get; init; }
    public int OfflineClients { get; init; }
    public double AverageDownloadTimeSeconds { get; init; }
    public double OverallHealthPercent => EnabledClients > 0
        ? (double)(HealthyClients + DegradedClients) / EnabledClients * 100
        : 0;
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Result of a download with failover attempt.
/// </summary>
public record FailoverDownloadResult
{
    public bool Success { get; init; }
    public string? DownloadId { get; init; }
    public int? UsedProviderId { get; init; }
    public string? UsedProviderName { get; init; }
    public int AttemptsCount { get; init; }
    public IReadOnlyList<FailoverAttempt> Attempts { get; init; } = Array.Empty<FailoverAttempt>();
    public string? FinalErrorMessage { get; init; }
    public TimeSpan TotalDuration { get; init; }
}

/// <summary>
/// Details of a single failover attempt.
/// </summary>
public record FailoverAttempt
{
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
}
