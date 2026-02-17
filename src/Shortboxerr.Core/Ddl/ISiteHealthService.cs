namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for monitoring DDL site health and availability.
/// </summary>
public interface ISiteHealthService
{
    /// <summary>
    /// Get the current health status for all registered sites.
    /// </summary>
    Task<IReadOnlyList<SiteHealthStatus>> GetAllHealthStatusesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current health status for a specific site.
    /// </summary>
    Task<SiteHealthStatus?> GetHealthStatusAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Perform a health check on a specific site.
    /// </summary>
    Task<SiteHealthCheckResult> CheckSiteHealthAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Perform health checks on all enabled sites.
    /// </summary>
    Task<IReadOnlyList<SiteHealthCheckResult>> CheckAllSitesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the health check history for a site.
    /// </summary>
    Task<IReadOnlyList<SiteHealthCheckResult>> GetHealthHistoryAsync(string siteType, int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear health check history for a site (useful after fixing issues).
    /// </summary>
    Task ClearHealthHistoryAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Re-enable a site that was auto-disabled due to health failures.
    /// </summary>
    Task<bool> ReEnableSiteAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record a successful operation (search, download) for a site.
    /// </summary>
    void RecordSuccess(string siteType);
    
    /// <summary>
    /// Record a failed operation for a site.
    /// </summary>
    void RecordFailure(string siteType, string errorMessage);
    
    /// <summary>
    /// Get health monitoring settings.
    /// </summary>
    SiteHealthSettings GetSettings();
    
    /// <summary>
    /// Update health monitoring settings.
    /// </summary>
    void UpdateSettings(SiteHealthSettings settings);
}

/// <summary>
/// Current health status for a DDL site.
/// </summary>
public class SiteHealthStatus
{
    /// <summary>
    /// Site type identifier.
    /// </summary>
    public required string SiteType { get; init; }
    
    /// <summary>
    /// Display name of the site.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Current health state.
    /// </summary>
    public SiteHealthState State { get; init; }
    
    /// <summary>
    /// Whether the site is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }
    
    /// <summary>
    /// Whether the site was auto-disabled due to health failures.
    /// </summary>
    public bool IsAutoDisabled { get; init; }
    
    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
    
    /// <summary>
    /// Last error message if unhealthy.
    /// </summary>
    public string? LastErrorMessage { get; init; }
    
    /// <summary>
    /// When the last health check was performed.
    /// </summary>
    public DateTime? LastCheckTime { get; init; }
    
    /// <summary>
    /// When the last successful operation occurred.
    /// </summary>
    public DateTime? LastSuccessTime { get; init; }
    
    /// <summary>
    /// When the last failure occurred.
    /// </summary>
    public DateTime? LastFailureTime { get; init; }
    
    /// <summary>
    /// Average response latency in milliseconds.
    /// </summary>
    public int AverageLatencyMs { get; init; }
    
    /// <summary>
    /// Success rate over the last N checks (0-100).
    /// </summary>
    public double SuccessRate { get; init; }
    
    /// <summary>
    /// Detected issues that may indicate site changes.
    /// </summary>
    public IReadOnlyList<string> DetectedIssues { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// When the site was auto-disabled (if applicable).
    /// </summary>
    public DateTime? AutoDisabledAt { get; init; }
}

/// <summary>
/// Health state of a DDL site.
/// </summary>
public enum SiteHealthState
{
    /// <summary>
    /// Health status is unknown (not yet checked).
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// Site is healthy and responding normally.
    /// </summary>
    Healthy = 1,
    
    /// <summary>
    /// Site is responding but with issues (slow, partial failures).
    /// </summary>
    Degraded = 2,
    
    /// <summary>
    /// Site is not responding or consistently failing.
    /// </summary>
    Unhealthy = 3,
    
    /// <summary>
    /// Site was auto-disabled due to repeated failures.
    /// </summary>
    Disabled = 4
}

/// <summary>
/// Result of a single health check operation.
/// </summary>
public class SiteHealthCheckResult
{
    /// <summary>
    /// Site type identifier.
    /// </summary>
    public required string SiteType { get; init; }
    
    /// <summary>
    /// Whether the health check passed.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// When the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; init; }
    
    /// <summary>
    /// Number of results returned from test search.
    /// </summary>
    public int? ResultCount { get; init; }
    
    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Type of failure if applicable.
    /// </summary>
    public HealthCheckFailureType? FailureType { get; init; }
    
    /// <summary>
    /// Detailed diagnostics information.
    /// </summary>
    public HealthCheckDiagnostics? Diagnostics { get; init; }
    
    /// <summary>
    /// Any warnings detected during the check.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Type of health check failure.
/// </summary>
public enum HealthCheckFailureType
{
    /// <summary>
    /// Unknown failure type.
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// Network connectivity issue.
    /// </summary>
    NetworkError = 1,
    
    /// <summary>
    /// DNS resolution failure.
    /// </summary>
    DnsError = 2,
    
    /// <summary>
    /// SSL/TLS certificate error.
    /// </summary>
    SslError = 3,
    
    /// <summary>
    /// Request timeout.
    /// </summary>
    Timeout = 4,
    
    /// <summary>
    /// HTTP error status code (4xx, 5xx).
    /// </summary>
    HttpError = 5,
    
    /// <summary>
    /// Site returned blocked/rate-limited response.
    /// </summary>
    RateLimited = 6,
    
    /// <summary>
    /// Cloudflare or similar challenge detected.
    /// </summary>
    CloudflareChallenge = 7,
    
    /// <summary>
    /// Site layout/structure has changed.
    /// </summary>
    LayoutChanged = 8,
    
    /// <summary>
    /// No results when results were expected.
    /// </summary>
    NoResults = 9,
    
    /// <summary>
    /// Results could not be parsed correctly.
    /// </summary>
    ParseError = 10,
    
    /// <summary>
    /// Authentication failure.
    /// </summary>
    AuthenticationFailed = 11
}

/// <summary>
/// Detailed diagnostics from a health check.
/// </summary>
public class HealthCheckDiagnostics
{
    /// <summary>
    /// HTTP status code received.
    /// </summary>
    public int? HttpStatusCode { get; init; }
    
    /// <summary>
    /// Content-Type header from response.
    /// </summary>
    public string? ContentType { get; init; }
    
    /// <summary>
    /// Whether Cloudflare protection was detected.
    /// </summary>
    public bool CloudflareDetected { get; init; }
    
    /// <summary>
    /// HTML title from response (useful for detecting error pages).
    /// </summary>
    public string? PageTitle { get; init; }
    
    /// <summary>
    /// Whether expected page structure was found.
    /// </summary>
    public bool ExpectedStructureFound { get; init; }
    
    /// <summary>
    /// Missing expected elements (CSS selectors).
    /// </summary>
    public IReadOnlyList<string> MissingElements { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Content hash for detecting layout changes over time.
    /// </summary>
    public string? StructureHash { get; init; }
    
    /// <summary>
    /// Last known structure hash for comparison.
    /// </summary>
    public string? PreviousStructureHash { get; init; }
}

/// <summary>
/// Settings for site health monitoring.
/// </summary>
public class SiteHealthSettings
{
    /// <summary>
    /// Whether automatic health checking is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Interval between health checks in minutes.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 30;
    
    /// <summary>
    /// Number of consecutive failures before marking unhealthy.
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;
    
    /// <summary>
    /// Number of consecutive failures before auto-disabling.
    /// </summary>
    public int AutoDisableThreshold { get; set; } = 5;
    
    /// <summary>
    /// Whether to auto-disable sites after threshold failures.
    /// </summary>
    public bool AutoDisableEnabled { get; set; } = true;
    
    /// <summary>
    /// Timeout for health check requests in seconds.
    /// </summary>
    public int CheckTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum history entries to retain per site.
    /// </summary>
    public int MaxHistoryEntries { get; set; } = 100;
    
    /// <summary>
    /// Latency threshold for degraded status in milliseconds.
    /// </summary>
    public int DegradedLatencyThresholdMs { get; set; } = 5000;
    
    /// <summary>
    /// Whether to notify on state changes (future: webhook/notification).
    /// </summary>
    public bool NotifyOnStateChange { get; set; } = false;
    
    /// <summary>
    /// Whether to detect and report layout/structure changes.
    /// </summary>
    public bool DetectLayoutChanges { get; set; } = true;
}
