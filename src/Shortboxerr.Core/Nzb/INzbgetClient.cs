namespace Shortboxerr.Core.Nzb;

/// <summary>
/// NZBGet-specific client interface extending the base download client.
/// NZBGet uses JSON-RPC API (not REST like SABnzbd).
/// </summary>
public interface INzbgetClient : INzbDownloadClient
{
    /// <summary>
    /// Gets the NZBGet version.
    /// </summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available categories configured in NZBGet.
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets NZBGet server status information.
    /// </summary>
    Task<NzbgetStatus?> GetStatusAsync(CancellationToken cancellationToken = default);

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
    /// Reloads the NZBGet configuration from disk.
    /// </summary>
    Task<bool> ReloadConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the incoming directory for NZB files.
    /// </summary>
    Task<bool> ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a log message to NZBGet's log.
    /// </summary>
    /// <param name="kind">Log kind: info, warning, error, detail, debug</param>
    /// <param name="message">Message to log</param>
    Task<bool> WriteLogAsync(string kind, string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for connecting to NZBGet.
/// </summary>
public class NzbgetSettings
{
    /// <summary>
    /// NZBGet hostname (e.g., localhost, 192.168.1.100).
    /// Do not include protocol (http://) or port.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// NZBGet port number.
    /// Default: 6789 (NZBGet's default port).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Username for authentication.
    /// Default in NZBGet is "nzbget".
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Password for authentication.
    /// Default in NZBGet is "tegbzn6789".
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Default category for comic downloads.
    /// </summary>
    public string Category { get; set; } = "comics";

    /// <summary>
    /// Default priority for downloads.
    /// NZBGet priorities: -100 (very low), -50 (low), 0 (normal), 50 (high), 100 (very high), 900 (force)
    /// </summary>
    public NzbgetPriority DefaultPriority { get; set; } = NzbgetPriority.Normal;

    /// <summary>
    /// Whether to use SSL/TLS (HTTPS).
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to add downloads in paused state.
    /// </summary>
    public bool AddPaused { get; set; } = false;

    /// <summary>
    /// Gets the effective port number.
    /// Default: 6789 (NZBGet's standard port).
    /// </summary>
    public int EffectivePort => Port ?? 6789;

    /// <summary>
    /// Gets the full base URL for NZBGet JSON-RPC API.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            var protocol = UseSsl ? "https" : "http";
            return $"{protocol}://{Host}:{EffectivePort}";
        }
    }

    /// <summary>
    /// Gets the JSON-RPC endpoint URL with authentication.
    /// </summary>
    public string JsonRpcUrl => $"{BaseUrl}/{Username}:{Password}/jsonrpc";
    
    /// <summary>
    /// Indicates whether the client has minimum required configuration.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// NZBGet-specific priority values.
/// NZBGet uses numeric priorities: -100 to 900.
/// </summary>
public enum NzbgetPriority
{
    /// <summary>
    /// Very low priority (-100).
    /// </summary>
    VeryLow = -100,
    
    /// <summary>
    /// Low priority (-50).
    /// </summary>
    Low = -50,
    
    /// <summary>
    /// Normal priority (0).
    /// </summary>
    Normal = 0,
    
    /// <summary>
    /// High priority (50).
    /// </summary>
    High = 50,
    
    /// <summary>
    /// Very high priority (100).
    /// </summary>
    VeryHigh = 100,
    
    /// <summary>
    /// Force download (900). Downloads immediately, ignoring queue order.
    /// </summary>
    Force = 900
}

/// <summary>
/// NZBGet server status information.
/// </summary>
public class NzbgetStatus
{
    /// <summary>
    /// Number of items remaining in the queue.
    /// </summary>
    public int RemainingSizeMB { get; init; }

    /// <summary>
    /// Forced download size remaining in MB.
    /// </summary>
    public int ForcedSizeMB { get; init; }

    /// <summary>
    /// Current download speed in KB/s.
    /// </summary>
    public int DownloadRate { get; init; }

    /// <summary>
    /// Average download speed in KB/s.
    /// </summary>
    public int AverageDownloadRate { get; init; }

    /// <summary>
    /// Speed limit in KB/s (0 = unlimited).
    /// </summary>
    public int DownloadLimit { get; init; }

    /// <summary>
    /// Whether download is paused.
    /// </summary>
    public bool DownloadPaused { get; init; }

    /// <summary>
    /// Number of threads currently downloading.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Post-processing queue size.
    /// </summary>
    public int PostJobCount { get; init; }

    /// <summary>
    /// NZBGet uptime in seconds.
    /// </summary>
    public int UpTimeSec { get; init; }

    /// <summary>
    /// Download time today in seconds.
    /// </summary>
    public int DownloadTimeSec { get; init; }

    /// <summary>
    /// Whether server is in standby mode.
    /// </summary>
    public bool ServerStandBy { get; init; }

    /// <summary>
    /// Free disk space in MB.
    /// </summary>
    public long FreeDiskSpaceMB { get; init; }

    /// <summary>
    /// Number of news servers defined.
    /// </summary>
    public int NewsServers { get; init; }

    /// <summary>
    /// NZBGet version string (when retrieved via version() call).
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// Represents an NZBGet group (download item) in the queue.
/// </summary>
public class NzbgetGroup
{
    /// <summary>
    /// NZB ID (internal NZBGet ID).
    /// </summary>
    public int NZBID { get; init; }

    /// <summary>
    /// Name of the NZB/download.
    /// </summary>
    public string NZBName { get; init; } = string.Empty;

    /// <summary>
    /// Category assigned to the download.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Total file size in bytes.
    /// </summary>
    public long FileSizeLo { get; init; }

    /// <summary>
    /// High bits of file size (for large files).
    /// </summary>
    public long FileSizeHi { get; init; }

    /// <summary>
    /// Remaining size to download in bytes.
    /// </summary>
    public long RemainingSizeLo { get; init; }

    /// <summary>
    /// High bits of remaining size.
    /// </summary>
    public long RemainingSizeHi { get; init; }

    /// <summary>
    /// Paused size in bytes.
    /// </summary>
    public long PausedSizeLo { get; init; }

    /// <summary>
    /// High bits of paused size.
    /// </summary>
    public long PausedSizeHi { get; init; }

    /// <summary>
    /// Number of files in the NZB.
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Number of remaining files.
    /// </summary>
    public int RemainingFileCount { get; init; }

    /// <summary>
    /// Number of par2 files.
    /// </summary>
    public int RemainingParCount { get; init; }

    /// <summary>
    /// Priority value.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Download status: QUEUED, PAUSED, DOWNLOADING, FETCHING, etc.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Destination directory.
    /// </summary>
    public string DestDir { get; init; } = string.Empty;

    /// <summary>
    /// Final directory after post-processing.
    /// </summary>
    public string FinalDir { get; init; } = string.Empty;

    /// <summary>
    /// Health percentage (0-1000, divide by 10 for %).
    /// </summary>
    public int Health { get; init; }

    /// <summary>
    /// Total size including high bits.
    /// </summary>
    public long TotalSize => FileSizeLo + (FileSizeHi << 32);

    /// <summary>
    /// Remaining size including high bits.
    /// </summary>
    public long RemainingSize => RemainingSizeLo + (RemainingSizeHi << 32);

    /// <summary>
    /// Downloaded size.
    /// </summary>
    public long DownloadedSize => TotalSize - RemainingSize;

    /// <summary>
    /// Progress percentage.
    /// </summary>
    public double ProgressPercent => TotalSize > 0 ? (double)DownloadedSize / TotalSize * 100 : 0;
}

/// <summary>
/// Represents an NZBGet history item (completed download).
/// </summary>
public class NzbgetHistoryItem
{
    /// <summary>
    /// NZB ID.
    /// </summary>
    public int NZBID { get; init; }

    /// <summary>
    /// Name of the download.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Category.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Destination directory.
    /// </summary>
    public string DestDir { get; init; } = string.Empty;

    /// <summary>
    /// Final directory.
    /// </summary>
    public string FinalDir { get; init; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeLo { get; init; }

    /// <summary>
    /// High bits of file size.
    /// </summary>
    public long FileSizeHi { get; init; }

    /// <summary>
    /// History status: SUCCESS, FAILURE, DELETED, etc.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Detailed status message.
    /// </summary>
    public string? StatusText { get; init; }

    /// <summary>
    /// Unix timestamp when download was completed.
    /// </summary>
    public long HistoryTime { get; init; }

    /// <summary>
    /// Download time in seconds.
    /// </summary>
    public int DownloadTimeSec { get; init; }

    /// <summary>
    /// Post-processing time in seconds.
    /// </summary>
    public int PostTotalTimeSec { get; init; }

    /// <summary>
    /// Par repair status: NONE, FAILURE, MANUAL, SUCCESS.
    /// </summary>
    public string ParStatus { get; init; } = string.Empty;

    /// <summary>
    /// Unpack status: NONE, FAILURE, SPACE, PASSWORD, SUCCESS.
    /// </summary>
    public string UnpackStatus { get; init; } = string.Empty;

    /// <summary>
    /// Script status: NONE, FAILURE, SUCCESS.
    /// </summary>
    public string ScriptStatus { get; init; } = string.Empty;

    /// <summary>
    /// Total size including high bits.
    /// </summary>
    public long TotalSize => FileSizeLo + (FileSizeHi << 32);

    /// <summary>
    /// Completion time as DateTime.
    /// </summary>
    public DateTime CompletedAt => DateTimeOffset.FromUnixTimeSeconds(HistoryTime).UtcDateTime;

    /// <summary>
    /// Whether the download was successful.
    /// </summary>
    public bool IsSuccess => Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
}
