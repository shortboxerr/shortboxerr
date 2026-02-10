namespace Shortboxerr.Core.Activity;

/// <summary>
/// Service for tracking and reporting download activity across all download clients.
/// Provides real-time visibility into active downloads from DDL, NZB, and torrent sources.
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// Gets all active download activities across all clients.
    /// </summary>
    Task<IReadOnlyList<DownloadActivity>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent activity history (completed, failed, cancelled).
    /// </summary>
    /// <param name="limit">Maximum number of items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DownloadActivity>> GetRecentHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific download's activity by ID.
    /// </summary>
    Task<DownloadActivity?> GetByIdAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity statistics summary.
    /// </summary>
    Task<ActivitySummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a download (if supported by the client).
    /// </summary>
    Task<bool> PauseAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    Task<bool> ResumeAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a download.
    /// </summary>
    Task<bool> CancelAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a failed download.
    /// </summary>
    Task<bool> RetryAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a completed/failed item from history.
    /// </summary>
    Task<bool> RemoveFromHistoryAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all completed items from history.
    /// </summary>
    Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a download activity from any source (DDL, NZB, Torrent).
/// </summary>
public class DownloadActivity
{
    /// <summary>
    /// Unique identifier for this download.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Type of download source.
    /// </summary>
    public DownloadSourceType SourceType { get; init; }

    /// <summary>
    /// Name of the download client (e.g., "SABnzbd", "qBittorrent", "DDL").
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Provider ID (for NZB/Torrent clients).
    /// </summary>
    public int? ProviderId { get; init; }

    /// <summary>
    /// Display title (usually the release title).
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Current state of the download.
    /// </summary>
    public ActivityState State { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Total size in bytes (if known).
    /// </summary>
    public long? TotalBytes { get; set; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long DownloadedBytes { get; set; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long? SpeedBytesPerSecond { get; set; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// When the download was added/started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// When the download completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Related series ID (if matched).
    /// </summary>
    public int? SeriesId { get; set; }

    /// <summary>
    /// Related issue ID (if matched).
    /// </summary>
    public int? IssueId { get; set; }

    /// <summary>
    /// Category assigned to the download.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Output path for completed download.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Source URL or NZB URL.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Formatted display of progress (e.g., "45.2 MB / 100 MB").
    /// </summary>
    public string ProgressDisplay
    {
        get
        {
            if (TotalBytes == null || TotalBytes == 0)
                return $"{FormatBytes(DownloadedBytes)}";
            return $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes.Value)}";
        }
    }

    /// <summary>
    /// Formatted display of speed (e.g., "5.2 MB/s").
    /// </summary>
    public string SpeedDisplay => SpeedBytesPerSecond.HasValue 
        ? $"{FormatBytes(SpeedBytesPerSecond.Value)}/s" 
        : "";

    /// <summary>
    /// Formatted display of ETA (e.g., "5m 30s").
    /// </summary>
    public string EtaDisplay
    {
        get
        {
            if (EstimatedTimeRemaining == null)
                return "";
            var eta = EstimatedTimeRemaining.Value;
            if (eta.TotalHours >= 1)
                return $"{(int)eta.TotalHours}h {eta.Minutes}m";
            if (eta.TotalMinutes >= 1)
                return $"{eta.Minutes}m {eta.Seconds}s";
            return $"{eta.Seconds}s";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}

/// <summary>
/// Types of download sources.
/// </summary>
public enum DownloadSourceType
{
    /// <summary>Direct download link (HTTP download).</summary>
    Ddl = 0,

    /// <summary>NZB/Usenet download.</summary>
    Nzb = 1,

    /// <summary>Torrent download.</summary>
    Torrent = 2
}

/// <summary>
/// Unified state for download activities.
/// </summary>
public enum ActivityState
{
    /// <summary>Waiting in queue.</summary>
    Queued = 0,

    /// <summary>Downloading in progress.</summary>
    Downloading = 1,

    /// <summary>Paused by user.</summary>
    Paused = 2,

    /// <summary>Download completed successfully.</summary>
    Completed = 3,

    /// <summary>Download failed.</summary>
    Failed = 4,

    /// <summary>Cancelled by user.</summary>
    Cancelled = 5,

    /// <summary>Retrying after failure.</summary>
    Retrying = 6,

    /// <summary>Post-processing (extracting, verifying).</summary>
    Processing = 7,

    /// <summary>Importing to library.</summary>
    Importing = 8,

    /// <summary>Seeding (torrent-specific).</summary>
    Seeding = 9,

    /// <summary>Stalled (no progress).</summary>
    Stalled = 10,

    /// <summary>Warning state (low disk space, etc.).</summary>
    Warning = 11
}

/// <summary>
/// Summary of download activity.
/// </summary>
public class ActivitySummary
{
    /// <summary>Number of active downloads.</summary>
    public int ActiveCount { get; init; }

    /// <summary>Number of queued downloads.</summary>
    public int QueuedCount { get; init; }

    /// <summary>Number of completed downloads (in history).</summary>
    public int CompletedCount { get; init; }

    /// <summary>Number of failed downloads (in history).</summary>
    public int FailedCount { get; init; }

    /// <summary>Total download speed across all clients (bytes/sec).</summary>
    public long TotalSpeedBytesPerSecond { get; init; }

    /// <summary>Formatted total speed.</summary>
    public string TotalSpeedDisplay
    {
        get
        {
            if (TotalSpeedBytesPerSecond >= 1024 * 1024)
                return $"{TotalSpeedBytesPerSecond / (1024.0 * 1024):F1} MB/s";
            if (TotalSpeedBytesPerSecond >= 1024)
                return $"{TotalSpeedBytesPerSecond / 1024.0:F1} KB/s";
            return $"{TotalSpeedBytesPerSecond} B/s";
        }
    }

    /// <summary>Breakdown by source type.</summary>
    public Dictionary<DownloadSourceType, int> BySourceType { get; init; } = new();

    /// <summary>Whether any client is currently downloading.</summary>
    public bool IsDownloading => ActiveCount > 0;

    /// <summary>Whether any download has failed.</summary>
    public bool HasFailures => FailedCount > 0;
}
