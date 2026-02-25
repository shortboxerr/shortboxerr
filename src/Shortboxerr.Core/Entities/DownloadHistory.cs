using Shortboxerr.Core.Activity;

namespace Shortboxerr.Core.Entities;

/// <summary>
/// Persisted download history entry for tracking completed, failed, and cancelled downloads.
/// </summary>
public class DownloadHistory
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique download identifier from the download service.
    /// </summary>
    public required string DownloadId { get; set; }
    
    /// <summary>
    /// Source type (DDL, NZB, Torrent).
    /// </summary>
    public DownloadSourceType SourceType { get; set; }
    
    /// <summary>
    /// Source site or client name (e.g., "GetComics", "SABnzbd").
    /// </summary>
    public string? SourceSite { get; set; }
    
    /// <summary>
    /// Original source URL.
    /// </summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Release or file title.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Final destination path of the downloaded file.
    /// </summary>
    public string? DestinationPath { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Whether the download succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Download state (Completed, Failed, Cancelled).
    /// </summary>
    public DownloadHistoryState State { get; set; }
    
    /// <summary>
    /// Failure reason code (if failed).
    /// </summary>
    public string? FailureReason { get; set; }
    
    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; set; }
    
    /// <summary>
    /// Download duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// Average download speed in bytes per second.
    /// </summary>
    public double? AverageSpeedBytesPerSecond { get; set; }
    
    /// <summary>
    /// When the download started.
    /// </summary>
    public DateTime StartedAt { get; set; }
    
    /// <summary>
    /// When the download completed (success or failure).
    /// </summary>
    public DateTime CompletedAt { get; set; }
    
    /// <summary>
    /// Related series ID (if known).
    /// </summary>
    public int? SeriesId { get; set; }
    
    /// <summary>
    /// Related issue ID (if known).
    /// </summary>
    public int? IssueId { get; set; }
    
    // Navigation properties
    public Series? Series { get; set; }
    public Issue? Issue { get; set; }
}

/// <summary>
/// State of a completed download in history.
/// </summary>
public enum DownloadHistoryState
{
    Unknown = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3
}
