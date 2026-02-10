using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Providers;

/// <summary>
/// Provider interface for download/acquisition operations.
/// Download providers handle getting files from various sources.
/// </summary>
public interface IDownloadProvider : IProvider
{
    /// <summary>
    /// Supported protocols for this download provider.
    /// </summary>
    IReadOnlyList<string> SupportedProtocols { get; }
    
    /// <summary>
    /// Start downloading a candidate.
    /// </summary>
    Task<DownloadResult> DownloadAsync(Candidate candidate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the status of a download.
    /// </summary>
    Task<DownloadStatus> GetStatusAsync(string downloadId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancel a download in progress.
    /// </summary>
    Task<bool> CancelAsync(string downloadId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active downloads for this provider.
    /// </summary>
    Task<IReadOnlyList<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of starting a download.
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// Whether the download was started successfully.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Unique identifier for tracking this download.
    /// </summary>
    public string? DownloadId { get; init; }
    
    /// <summary>
    /// Error message if download failed to start.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// The candidate being downloaded.
    /// </summary>
    public Candidate? Candidate { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static DownloadResult Ok(string downloadId, Candidate candidate) => new()
    {
        Success = true,
        DownloadId = downloadId,
        Candidate = candidate
    };
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static DownloadResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Current status of a download.
/// </summary>
public class DownloadStatus
{
    /// <summary>
    /// Unique identifier for this download.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Current state of the download.
    /// </summary>
    public DownloadState State { get; init; }
    
    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Progress { get; init; }
    
    /// <summary>
    /// Total size in bytes (if known).
    /// </summary>
    public long? TotalBytes { get; init; }
    
    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long DownloadedBytes { get; init; }
    
    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long? SpeedBytesPerSecond { get; init; }
    
    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    
    /// <summary>
    /// Error message if download failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; init; }
    
    /// <summary>
    /// When the download was started.
    /// </summary>
    public DateTime StartedAt { get; init; }
    
    /// <summary>
    /// When the download completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>
    /// Output file path (when completed).
    /// </summary>
    public string? OutputPath { get; init; }
    
    /// <summary>
    /// Original candidate information.
    /// </summary>
    public string? CandidateTitle { get; init; }
    
    /// <summary>
    /// Source URL being downloaded.
    /// </summary>
    public string? SourceUrl { get; init; }
}

/// <summary>
/// State of a download operation.
/// </summary>
public enum DownloadState
{
    /// <summary>
    /// Download is queued but not started.
    /// </summary>
    Queued = 0,
    
    /// <summary>
    /// Download is in progress.
    /// </summary>
    Downloading = 1,
    
    /// <summary>
    /// Download is paused.
    /// </summary>
    Paused = 2,
    
    /// <summary>
    /// Download completed successfully.
    /// </summary>
    Completed = 3,
    
    /// <summary>
    /// Download failed.
    /// </summary>
    Failed = 4,
    
    /// <summary>
    /// Download was cancelled.
    /// </summary>
    Cancelled = 5,
    
    /// <summary>
    /// Download is being retried after failure.
    /// </summary>
    Retrying = 6,
    
    /// <summary>
    /// Download is being processed (post-download verification).
    /// </summary>
    Processing = 7,
    
    /// <summary>
    /// Download has stalled (no progress for extended period).
    /// </summary>
    Stalled = 8,
    
    /// <summary>
    /// Download state is unknown.
    /// </summary>
    Unknown = 99
}



