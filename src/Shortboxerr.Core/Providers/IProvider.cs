namespace Shortboxerr.Core.Providers;

/// <summary>
/// Base abstraction for all provider types (indexers, download clients, etc.).
/// Follows Arr-style provider pattern.
/// </summary>
public interface IProvider
{
    /// <summary>
    /// Unique identifier for this provider instance.
    /// </summary>
    int Id { get; }
    
    /// <summary>
    /// Display name for this provider.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Provider type identifier (e.g., "DDL", "RSS", "Torrent").
    /// </summary>
    ProviderType Type { get; }
    
    /// <summary>
    /// Whether this provider is currently enabled.
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Priority order (lower = higher priority).
    /// </summary>
    int Priority { get; set; }
    
    /// <summary>
    /// Test the provider connection and configuration.
    /// </summary>
    Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current health status of this provider.
    /// </summary>
    Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of providers supported by the system.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Direct Download Link provider (Mylar3-compatible).
    /// </summary>
    Ddl = 1,
    
    /// <summary>
    /// RSS/Atom feed indexer.
    /// </summary>
    Rss = 2,
    
    /// <summary>
    /// Newznab-compatible indexer.
    /// </summary>
    Newznab = 3,
    
    /// <summary>
    /// Torznab-compatible indexer.
    /// </summary>
    Torznab = 4,
    
    /// <summary>
    /// Generic HTTP download client.
    /// </summary>
    HttpDownload = 10,
    
    /// <summary>
    /// Torrent download client (future).
    /// </summary>
    Torrent = 11,
    
    /// <summary>
    /// Usenet download client (future).
    /// </summary>
    Usenet = 12
}

/// <summary>
/// Result of testing a provider's connection and configuration.
/// </summary>
public class ProviderTestResult
{
    /// <summary>
    /// Whether the test was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public required string Message { get; init; }
    
    /// <summary>
    /// Sample results count (for indexers).
    /// </summary>
    public int? SampleResultCount { get; init; }
    
    /// <summary>
    /// Test latency in milliseconds.
    /// </summary>
    public long? LatencyMs { get; init; }
    
    /// <summary>
    /// Any errors encountered during testing.
    /// </summary>
    public List<string> Errors { get; init; } = new();
    
    /// <summary>
    /// Create a successful test result.
    /// </summary>
    public static ProviderTestResult Ok(string message, int? sampleCount = null, long? latencyMs = null) => new()
    {
        Success = true,
        Message = message,
        SampleResultCount = sampleCount,
        LatencyMs = latencyMs
    };
    
    /// <summary>
    /// Create a failed test result.
    /// </summary>
    public static ProviderTestResult Fail(string message, params string[] errors) => new()
    {
        Success = false,
        Message = message,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Health status of a provider.
/// </summary>
public class ProviderHealth
{
    /// <summary>
    /// Current health status.
    /// </summary>
    public HealthStatus Status { get; init; }
    
    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// When this health check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Number of consecutive failures (if unhealthy).
    /// </summary>
    public int FailureCount { get; init; }
    
    /// <summary>
    /// Last successful operation time.
    /// </summary>
    public DateTime? LastSuccessAt { get; init; }
    
    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastError { get; init; }
}

/// <summary>
/// Provider health status values.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Provider is healthy and operational.
    /// </summary>
    Healthy = 0,
    
    /// <summary>
    /// Provider is experiencing issues but may still work.
    /// </summary>
    Degraded = 1,
    
    /// <summary>
    /// Provider is not working.
    /// </summary>
    Unhealthy = 2,
    
    /// <summary>
    /// Provider health is unknown (not yet tested).
    /// </summary>
    Unknown = 3,
    
    /// <summary>
    /// Provider is disabled by user.
    /// </summary>
    Disabled = 4
}



