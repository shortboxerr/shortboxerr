using Shortboxerr.Core.Providers;

namespace Shortboxerr.Core.Entities;

/// <summary>
/// Persisted provider configuration.
/// Stores settings for indexers and download clients.
/// </summary>
public class ProviderDefinition
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Display name for this provider instance.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Provider implementation type (e.g., "DdlProvider", "RssIndexer").
    /// Used to instantiate the correct provider class.
    /// </summary>
    public required string Implementation { get; set; }
    
    /// <summary>
    /// Provider category (Indexer, DownloadClient).
    /// </summary>
    public ProviderCategory Category { get; set; }
    
    /// <summary>
    /// Provider type (DDL, RSS, etc.).
    /// </summary>
    public ProviderType Type { get; set; }
    
    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Priority order (lower = higher priority).
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// JSON-serialized provider settings.
    /// Schema depends on the provider implementation.
    /// </summary>
    public string? Settings { get; set; }
    
    /// <summary>
    /// Base URL for the provider (if applicable).
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// API key or token (if applicable).
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Username for authentication (if applicable).
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Password for authentication (stored encrypted).
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Last health check status.
    /// </summary>
    public HealthStatus LastHealthStatus { get; set; } = HealthStatus.Unknown;
    
    /// <summary>
    /// Last health check timestamp.
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }
    
    /// <summary>
    /// Last error message (if unhealthy).
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Consecutive failure count.
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// When this provider was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this provider was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optional tags for organization.
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// Provider categories.
/// </summary>
public enum ProviderCategory
{
    /// <summary>
    /// Search/discovery provider.
    /// </summary>
    Indexer = 1,
    
    /// <summary>
    /// Download/acquisition provider.
    /// </summary>
    DownloadClient = 2
}

