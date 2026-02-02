namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for downloading files from DDL sources.
/// Handles retries, failure tracking, and download progress.
/// </summary>
public interface IDdlDownloadService
{
    /// <summary>
    /// Download a file from a DDL candidate.
    /// </summary>
    Task<DdlDownloadResult> DownloadAsync(DdlCandidate candidate, DdlDownloadOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download from a specific URL with options.
    /// </summary>
    Task<DdlDownloadResult> DownloadUrlAsync(string url, string destinationPath, DdlDownloadOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the status of an active download.
    /// </summary>
    DdlDownloadStatus? GetDownloadStatus(string downloadId);
    
    /// <summary>
    /// Cancel an active download.
    /// </summary>
    bool CancelDownload(string downloadId);
    
    /// <summary>
    /// Get all active downloads.
    /// </summary>
    IReadOnlyList<DdlDownloadStatus> GetActiveDownloads();
    
    /// <summary>
    /// Get recent download history.
    /// </summary>
    IReadOnlyList<DdlDownloadHistoryEntry> GetDownloadHistory(int limit = 50);
    
    /// <summary>
    /// Check if a URL can be resumed (partial download exists).
    /// </summary>
    Task<bool> CanResumeAsync(string url, string destinationPath);
}

/// <summary>
/// Options for configuring a DDL download.
/// </summary>
public class DdlDownloadOptions
{
    /// <summary>
    /// Destination folder for the download.
    /// </summary>
    public string? DestinationFolder { get; set; }
    
    /// <summary>
    /// Custom filename (if null, derived from URL or candidate).
    /// </summary>
    public string? CustomFilename { get; set; }
    
    /// <summary>
    /// Maximum retry attempts (default: 3, Mylar3 default).
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Base delay between retries in milliseconds.
    /// Actual delay uses exponential backoff.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Maximum retry delay in milliseconds.
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300; // 5 minutes
    
    /// <summary>
    /// Whether to attempt resuming partial downloads.
    /// </summary>
    public bool EnableResume { get; set; } = true;
    
    /// <summary>
    /// Custom User-Agent string.
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Custom headers to include in requests.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
    
    /// <summary>
    /// Cookies to include in requests.
    /// </summary>
    public Dictionary<string, string> Cookies { get; set; } = new();
    
    /// <summary>
    /// Whether to verify the downloaded file.
    /// </summary>
    public bool VerifyDownload { get; set; } = true;
    
    /// <summary>
    /// Minimum expected file size (for verification).
    /// </summary>
    public long? MinExpectedSize { get; set; }
    
    /// <summary>
    /// Maximum expected file size (for verification).
    /// </summary>
    public long? MaxExpectedSize { get; set; }
    
    /// <summary>
    /// Progress callback.
    /// </summary>
    public Action<DdlDownloadProgress>? OnProgress { get; set; }
}

/// <summary>
/// Result of a DDL download operation.
/// </summary>
public class DdlDownloadResult
{
    /// <summary>
    /// Unique download identifier.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Whether the download succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Path to the downloaded file (if successful).
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Final filename.
    /// </summary>
    public string? FileName { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }
    
    /// <summary>
    /// Download duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Average download speed in bytes per second.
    /// </summary>
    public double BytesPerSecond { get; init; }
    
    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryAttempts { get; init; }
    
    /// <summary>
    /// Failure reason (if failed).
    /// </summary>
    public DdlDownloadFailureReason FailureReason { get; init; }
    
    /// <summary>
    /// Detailed error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// HTTP status code (if applicable).
    /// </summary>
    public int? HttpStatusCode { get; init; }
    
    /// <summary>
    /// Source URL that was downloaded.
    /// </summary>
    public string? SourceUrl { get; init; }
    
    /// <summary>
    /// Whether the download was resumed from a partial file.
    /// </summary>
    public bool WasResumed { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static DdlDownloadResult Succeeded(string downloadId, string filePath, string fileName, long fileSize, TimeSpan duration, int retryAttempts = 0, bool wasResumed = false, string? sourceUrl = null)
    {
        var bytesPerSecond = duration.TotalSeconds > 0 ? fileSize / duration.TotalSeconds : 0;
        return new DdlDownloadResult
        {
            DownloadId = downloadId,
            Success = true,
            FilePath = filePath,
            FileName = fileName,
            FileSize = fileSize,
            Duration = duration,
            BytesPerSecond = bytesPerSecond,
            RetryAttempts = retryAttempts,
            FailureReason = DdlDownloadFailureReason.None,
            WasResumed = wasResumed,
            SourceUrl = sourceUrl
        };
    }
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static DdlDownloadResult Failed(string downloadId, DdlDownloadFailureReason reason, string errorMessage, int retryAttempts = 0, int? httpStatusCode = null, string? sourceUrl = null)
    {
        return new DdlDownloadResult
        {
            DownloadId = downloadId,
            Success = false,
            FailureReason = reason,
            ErrorMessage = errorMessage,
            RetryAttempts = retryAttempts,
            HttpStatusCode = httpStatusCode,
            SourceUrl = sourceUrl
        };
    }
}

/// <summary>
/// Reasons for download failure.
/// </summary>
public enum DdlDownloadFailureReason
{
    /// <summary>
    /// No failure.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Network timeout.
    /// </summary>
    Timeout = 10,
    
    /// <summary>
    /// Connection failed.
    /// </summary>
    ConnectionFailed = 11,
    
    /// <summary>
    /// DNS resolution failed.
    /// </summary>
    DnsFailure = 12,
    
    /// <summary>
    /// HTTP 404 Not Found.
    /// </summary>
    NotFound = 20,
    
    /// <summary>
    /// HTTP 401/403 Unauthorized/Forbidden.
    /// </summary>
    Unauthorized = 21,
    
    /// <summary>
    /// HTTP 429 Too Many Requests.
    /// </summary>
    RateLimited = 22,
    
    /// <summary>
    /// Server error (5xx).
    /// </summary>
    ServerError = 23,
    
    /// <summary>
    /// Other HTTP error.
    /// </summary>
    HttpError = 29,
    
    /// <summary>
    /// Downloaded file is empty.
    /// </summary>
    EmptyFile = 30,
    
    /// <summary>
    /// Downloaded file is too small.
    /// </summary>
    FileTooSmall = 31,
    
    /// <summary>
    /// Downloaded file is too large.
    /// </summary>
    FileTooLarge = 32,
    
    /// <summary>
    /// Downloaded file appears to be an HTML error page.
    /// </summary>
    HtmlErrorPage = 33,
    
    /// <summary>
    /// File verification failed (wrong magic bytes).
    /// </summary>
    VerificationFailed = 34,
    
    /// <summary>
    /// Disk full or write error.
    /// </summary>
    DiskError = 40,
    
    /// <summary>
    /// Download was cancelled.
    /// </summary>
    Cancelled = 50,
    
    /// <summary>
    /// Maximum retries exceeded.
    /// </summary>
    MaxRetriesExceeded = 60,
    
    /// <summary>
    /// No valid download links available.
    /// </summary>
    NoValidLinks = 70,
    
    /// <summary>
    /// Unknown error.
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Status of an active download.
/// </summary>
public class DdlDownloadStatus
{
    /// <summary>
    /// Unique download identifier.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Source URL being downloaded.
    /// </summary>
    public required string SourceUrl { get; init; }
    
    /// <summary>
    /// Destination file path.
    /// </summary>
    public required string DestinationPath { get; init; }
    
    /// <summary>
    /// Current state.
    /// </summary>
    public DdlDownloadState State { get; set; }
    
    /// <summary>
    /// Total bytes to download (if known).
    /// </summary>
    public long? TotalBytes { get; set; }
    
    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; set; }
    
    /// <summary>
    /// Download progress (0-100).
    /// </summary>
    public double ProgressPercent => TotalBytes > 0 ? (BytesDownloaded * 100.0 / TotalBytes.Value) : 0;
    
    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public double BytesPerSecond { get; set; }
    
    /// <summary>
    /// When the download started.
    /// </summary>
    public DateTime StartedAt { get; init; }
    
    /// <summary>
    /// Current retry attempt (0 = first attempt).
    /// </summary>
    public int CurrentRetry { get; set; }
    
    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// State of a download operation.
/// </summary>
public enum DdlDownloadState
{
    /// <summary>
    /// Queued, waiting to start.
    /// </summary>
    Queued = 0,
    
    /// <summary>
    /// Connecting to server.
    /// </summary>
    Connecting = 1,
    
    /// <summary>
    /// Actively downloading.
    /// </summary>
    Downloading = 2,
    
    /// <summary>
    /// Paused.
    /// </summary>
    Paused = 3,
    
    /// <summary>
    /// Retrying after failure.
    /// </summary>
    Retrying = 4,
    
    /// <summary>
    /// Verifying downloaded file.
    /// </summary>
    Verifying = 5,
    
    /// <summary>
    /// Completed successfully.
    /// </summary>
    Completed = 10,
    
    /// <summary>
    /// Failed.
    /// </summary>
    Failed = 11,
    
    /// <summary>
    /// Cancelled.
    /// </summary>
    Cancelled = 12
}

/// <summary>
/// Progress information for a download.
/// </summary>
public class DdlDownloadProgress
{
    /// <summary>
    /// Download identifier.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }
    
    /// <summary>
    /// Total bytes (if known).
    /// </summary>
    public long? TotalBytes { get; init; }
    
    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercent { get; init; }
    
    /// <summary>
    /// Current download speed.
    /// </summary>
    public double BytesPerSecond { get; init; }
    
    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Entry in the download history.
/// </summary>
public class DdlDownloadHistoryEntry
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Download identifier.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Source URL.
    /// </summary>
    public required string SourceUrl { get; init; }
    
    /// <summary>
    /// Source site.
    /// </summary>
    public string? SourceSite { get; init; }
    
    /// <summary>
    /// Release title.
    /// </summary>
    public string? ReleaseTitle { get; init; }
    
    /// <summary>
    /// Final destination path.
    /// </summary>
    public string? DestinationPath { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }
    
    /// <summary>
    /// Whether the download succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Failure reason (if failed).
    /// </summary>
    public DdlDownloadFailureReason? FailureReason { get; init; }
    
    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; init; }
    
    /// <summary>
    /// Download duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// When the download started.
    /// </summary>
    public DateTime StartedAt { get; init; }
    
    /// <summary>
    /// When the download completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }
}

