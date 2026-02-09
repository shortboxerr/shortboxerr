using System.Text.Json.Serialization;

namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Common interface for NZB download clients (SABnzbd, NZBGet, etc.).
/// </summary>
public interface INzbDownloadClient
{
    /// <summary>
    /// The type of download client.
    /// </summary>
    NzbDownloadClientType ClientType { get; }

    /// <summary>
    /// Tests connectivity to the download client.
    /// </summary>
    Task<NzbClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an NZB to the download queue.
    /// </summary>
    /// <param name="nzbContent">The NZB file content as bytes.</param>
    /// <param name="filename">The filename for the NZB.</param>
    /// <param name="options">Download options (category, priority, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the download ID if successful.</returns>
    Task<NzbAddResult> AddNzbAsync(byte[] nzbContent, string filename, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an NZB to the download queue via URL.
    /// </summary>
    /// <param name="nzbUrl">URL to the NZB file.</param>
    /// <param name="options">Download options (category, priority, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the download ID if successful.</returns>
    Task<NzbAddResult> AddNzbUrlAsync(string nzbUrl, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific download.
    /// </summary>
    /// <param name="downloadId">The download ID returned from AddNzb.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current status of the download.</returns>
    Task<NzbDownloadStatus?> GetDownloadStatusAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all downloads in the queue.
    /// </summary>
    Task<IReadOnlyList<NzbDownloadStatus>> GetQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets download history (completed/failed downloads).
    /// </summary>
    /// <param name="limit">Maximum number of history entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<NzbDownloadStatus>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a download from the queue.
    /// </summary>
    /// <param name="downloadId">The download ID to remove.</param>
    /// <param name="deleteFiles">Whether to delete downloaded files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RemoveDownloadAsync(string downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a download.
    /// </summary>
    Task<bool> PauseDownloadAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    Task<bool> ResumeDownloadAsync(string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets disk space information from the download client.
    /// </summary>
    Task<NzbDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Types of NZB download clients.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NzbDownloadClientType
{
    SABnzbd,
    NZBGet
}

/// <summary>
/// Download priority levels.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NzbPriority
{
    Low = -1,
    Normal = 0,
    High = 1,
    Force = 2
}

/// <summary>
/// Download status states.
/// </summary>
public enum NzbDownloadState
{
    Queued,
    Downloading,
    Paused,
    Verifying,
    Repairing,
    Extracting,
    PostProcessing,
    Completed,
    Failed,
    Deleted
}

/// <summary>
/// Options for adding an NZB download.
/// </summary>
public class NzbDownloadOptions
{
    /// <summary>
    /// Category to assign (e.g., "comics").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Download priority.
    /// </summary>
    public NzbPriority Priority { get; set; } = NzbPriority.Normal;

    /// <summary>
    /// Post-processing script to run.
    /// </summary>
    public string? PostProcessingScript { get; set; }

    /// <summary>
    /// Custom name for the download.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Result of testing a download client connection.
/// </summary>
public class NzbClientTestResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public string? Version { get; init; }
    public long ResponseTimeMs { get; init; }

    public static NzbClientTestResult Ok(string message, string? version = null, long responseTimeMs = 0) =>
        new() { Success = true, Message = message, Version = version, ResponseTimeMs = responseTimeMs };

    public static NzbClientTestResult Failed(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// Result of adding an NZB to the download queue.
/// </summary>
public class NzbAddResult
{
    public bool Success { get; init; }
    public string? DownloadId { get; init; }
    public string? ErrorMessage { get; init; }

    public static NzbAddResult Ok(string downloadId) =>
        new() { Success = true, DownloadId = downloadId };

    public static NzbAddResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Status of an NZB download.
/// </summary>
public class NzbDownloadStatus
{
    /// <summary>
    /// Unique identifier for this download.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name/title of the download.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current state of the download.
    /// </summary>
    public NzbDownloadState State { get; init; }

    /// <summary>
    /// Category assigned to the download.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long DownloadedBytes { get; init; }

    /// <summary>
    /// Download progress as percentage (0-100).
    /// </summary>
    public double ProgressPercent => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long SpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? TimeRemaining { get; init; }

    /// <summary>
    /// When the download was added.
    /// </summary>
    public DateTime? AddedAt { get; init; }

    /// <summary>
    /// When the download completed (if completed).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Path to downloaded files (if completed).
    /// </summary>
    public string? DownloadPath { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Priority of the download.
    /// </summary>
    public NzbPriority Priority { get; init; }
}

/// <summary>
/// Disk space information from the download client.
/// </summary>
public class NzbDiskSpace
{
    /// <summary>
    /// Total disk space in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Free disk space in bytes.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Whether disk space is low (based on client threshold).
    /// </summary>
    public bool IsLow { get; init; }

    /// <summary>
    /// Path being monitored.
    /// </summary>
    public string? Path { get; init; }
}
