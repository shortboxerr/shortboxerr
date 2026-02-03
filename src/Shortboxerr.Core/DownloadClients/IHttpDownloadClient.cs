namespace Shortboxerr.Core.DownloadClients;

/// <summary>
/// Built-in HTTP download client for direct URL-to-file downloads.
/// This is an internal service used by DDL providers and RSS indexers.
/// Unlike external download clients (qBittorrent, SABnzbd), this is NOT
/// a user-configurable provider - it's always available as a built-in service.
/// Similar to Mylar3's internal DDL download handling.
/// </summary>
public interface IHttpDownloadClient
{
    /// <summary>
    /// Download a file directly from a URL.
    /// </summary>
    Task<HttpDownloadResult> DownloadUrlAsync(
        string url, 
        string destinationPath,
        HttpDownloadOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the size of a remote file without downloading it.
    /// </summary>
    Task<long?> GetFileSizeAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a URL is reachable.
    /// </summary>
    Task<bool> IsReachableAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for an HTTP download client.
/// </summary>
public class HttpDownloadClientSettings
{
    /// <summary>
    /// Unique identifier for this client.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Display name for this client.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Whether this client is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Priority for this client (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// Default download directory.
    /// </summary>
    public required string DownloadDirectory { get; init; }
    
    /// <summary>
    /// Maximum concurrent downloads.
    /// </summary>
    public int MaxConcurrentDownloads { get; init; } = 3;
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;
    
    /// <summary>
    /// Maximum retry attempts on failure.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// Delay between retries in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;
    
    /// <summary>
    /// Custom User-Agent string.
    /// </summary>
    public string? UserAgent { get; init; }
    
    /// <summary>
    /// Whether to verify SSL certificates.
    /// </summary>
    public bool VerifySsl { get; init; } = true;
    
    /// <summary>
    /// Proxy URL (if required).
    /// </summary>
    public string? ProxyUrl { get; init; }
    
    /// <summary>
    /// Maximum download speed in bytes per second (0 = unlimited).
    /// </summary>
    public long MaxSpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Options for a single HTTP download operation.
/// </summary>
public class HttpDownloadOptions
{
    /// <summary>
    /// Custom User-Agent for this request.
    /// </summary>
    public string? UserAgent { get; init; }
    
    /// <summary>
    /// Custom headers to include.
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; init; }
    
    /// <summary>
    /// Cookies to include.
    /// </summary>
    public Dictionary<string, string>? Cookies { get; init; }
    
    /// <summary>
    /// Basic auth username.
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Basic auth password.
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// Request timeout override.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// Maximum retry attempts for this download.
    /// </summary>
    public int? MaxRetries { get; init; }
    
    /// <summary>
    /// Whether to resume partial downloads.
    /// </summary>
    public bool ResumePartial { get; init; } = true;
    
    /// <summary>
    /// Progress callback for download updates.
    /// </summary>
    public IProgress<HttpDownloadProgress>? Progress { get; init; }
    
    /// <summary>
    /// Referer header value.
    /// </summary>
    public string? Referer { get; init; }
}

/// <summary>
/// Progress information for an HTTP download.
/// </summary>
public class HttpDownloadProgress
{
    /// <summary>
    /// Total bytes to download (if known).
    /// </summary>
    public long? TotalBytes { get; init; }
    
    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }
    
    /// <summary>
    /// Download progress percentage (0-100).
    /// </summary>
    public double ProgressPercent => TotalBytes > 0 
        ? (double)BytesDownloaded / TotalBytes.Value * 100 
        : 0;
    
    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long SpeedBytesPerSecond { get; init; }
    
    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Result of an HTTP download operation.
/// </summary>
public class HttpDownloadResult
{
    /// <summary>
    /// Whether the download was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Path to the downloaded file (if successful).
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Final file size in bytes.
    /// </summary>
    public long? FileSize { get; init; }
    
    /// <summary>
    /// Error message if download failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// HTTP status code from the final request.
    /// </summary>
    public int? StatusCode { get; init; }
    
    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; init; }
    
    /// <summary>
    /// Total time taken for the download.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Average download speed in bytes per second.
    /// </summary>
    public long? AverageSpeedBytesPerSecond { get; init; }
    
    /// <summary>
    /// Content-Type from the response.
    /// </summary>
    public string? ContentType { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static HttpDownloadResult Ok(string filePath, long fileSize, TimeSpan duration) => new()
    {
        Success = true,
        FilePath = filePath,
        FileSize = fileSize,
        Duration = duration,
        AverageSpeedBytesPerSecond = duration.TotalSeconds > 0 
            ? (long)(fileSize / duration.TotalSeconds) 
            : null
    };
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static HttpDownloadResult Fail(string error, int? statusCode = null) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}

