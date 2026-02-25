using Shortboxerr.Core.Services;

namespace Shortboxerr.Core.Nzb;

/// <summary>
/// SABnzbd-specific client interface extending the base download client.
/// </summary>
public interface ISabnzbdClient : INzbDownloadClient
{
    /// <summary>
    /// Gets the SABnzbd version.
    /// </summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available categories configured in SABnzbd.
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available post-processing scripts configured in SABnzbd.
    /// </summary>
    Task<IReadOnlyList<string>> GetScriptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the entire download queue.
    /// </summary>
    Task<bool> PauseQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the entire download queue.
    /// </summary>
    Task<bool> ResumeQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the download speed limit.
    /// </summary>
    /// <param name="speedKbps">Speed limit in KB/s. 0 = unlimited.</param>
    Task<bool> SetSpeedLimitAsync(int speedKbps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current server statistics.
    /// </summary>
    Task<SabnzbdServerStats?> GetServerStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for connecting to SABnzbd.
/// </summary>
public class SabnzbdSettings
{
    /// <summary>
    /// SABnzbd hostname (e.g., localhost, 192.168.1.100, sabnzbd.local).
    /// Do not include protocol (http://) or port.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// SABnzbd port number.
    /// Default: 80 for HTTP, 443 for HTTPS if UseSsl is true.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    [SensitiveCredential]
    public required string ApiKey { get; set; }

    /// <summary>
    /// Default category for comic downloads.
    /// </summary>
    public string Category { get; set; } = "comics";

    /// <summary>
    /// Default priority for downloads.
    /// </summary>
    public NzbPriority DefaultPriority { get; set; } = NzbPriority.Normal;

    /// <summary>
    /// Whether to use SSL/TLS (HTTPS).
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Post-processing script to run after download.
    /// </summary>
    public string? PostProcessingScript { get; set; }

    /// <summary>
    /// Gets the effective port number based on Port setting and UseSsl.
    /// </summary>
    public int EffectivePort => Port ?? (UseSsl ? 443 : 80);

    /// <summary>
    /// Gets the full base URL for SABnzbd API requests.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            var protocol = UseSsl ? "https" : "http";
            var port = EffectivePort;
            
            // Only include port if non-standard
            var includePort = (UseSsl && port != 443) || (!UseSsl && port != 80);
            
            if (includePort)
            {
                return $"{protocol}://{Host}:{port}";
            }
            return $"{protocol}://{Host}";
        }
    }
    
    /// <summary>
    /// Indicates whether the client has minimum required configuration.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// SABnzbd server statistics.
/// </summary>
public class SabnzbdServerStats
{
    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long SpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Total data downloaded today in bytes.
    /// </summary>
    public long TodayBytes { get; init; }

    /// <summary>
    /// Total data downloaded this week in bytes.
    /// </summary>
    public long WeekBytes { get; init; }

    /// <summary>
    /// Total data downloaded this month in bytes.
    /// </summary>
    public long MonthBytes { get; init; }

    /// <summary>
    /// Total data downloaded all time in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Number of items in queue.
    /// </summary>
    public int QueueCount { get; init; }

    /// <summary>
    /// Size of queue in bytes.
    /// </summary>
    public long QueueSizeBytes { get; init; }

    /// <summary>
    /// Estimated time to complete queue.
    /// </summary>
    public TimeSpan? TimeRemaining { get; init; }

    /// <summary>
    /// Whether the queue is paused.
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>
    /// Current speed limit in KB/s (0 = unlimited).
    /// </summary>
    public int SpeedLimitKbps { get; init; }
}
