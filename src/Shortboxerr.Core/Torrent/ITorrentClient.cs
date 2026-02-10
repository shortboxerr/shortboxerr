namespace Shortboxerr.Core.Torrent;

/// <summary>
/// Common interface for torrent download clients (qBittorrent, Transmission, Deluge, etc.).
/// Based on Sonarr/Radarr patterns for torrent client integration.
/// </summary>
public interface ITorrentClient
{
    /// <summary>
    /// The type of torrent client.
    /// </summary>
    TorrentClientType ClientType { get; }

    /// <summary>
    /// Tests the connection to the torrent client.
    /// </summary>
    Task<TorrentClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a torrent by magnet link.
    /// </summary>
    Task<TorrentAddResult> AddTorrentMagnetAsync(string magnetUri, TorrentAddOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a torrent by URL (to a .torrent file).
    /// </summary>
    Task<TorrentAddResult> AddTorrentUrlAsync(string torrentUrl, TorrentAddOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a torrent by uploading the .torrent file content.
    /// </summary>
    Task<TorrentAddResult> AddTorrentFileAsync(byte[] torrentContent, string filename, TorrentAddOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific torrent by hash.
    /// </summary>
    Task<TorrentStatus?> GetStatusAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all torrents currently in the client (queue + seeding).
    /// </summary>
    Task<IReadOnlyList<TorrentStatus>> GetAllTorrentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a torrent from the client.
    /// </summary>
    /// <param name="hash">The torrent hash.</param>
    /// <param name="deleteFiles">If true, also delete downloaded files.</param>
    Task<bool> RemoveTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a torrent.
    /// </summary>
    Task<bool> PauseTorrentAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused torrent.
    /// </summary>
    Task<bool> ResumeTorrentAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available categories/labels from the torrent client.
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets disk space information.
    /// </summary>
    Task<TorrentDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Supported torrent client types.
/// </summary>
public enum TorrentClientType
{
    /// <summary>
    /// qBittorrent - most popular, excellent Web API v2.
    /// </summary>
    QBittorrent = 1,

    /// <summary>
    /// Transmission - lightweight, RPC-based API.
    /// </summary>
    Transmission = 2,

    /// <summary>
    /// Deluge - feature-rich, JSON-RPC daemon.
    /// </summary>
    Deluge = 3,

    /// <summary>
    /// rTorrent/ruTorrent - power user option with XML-RPC.
    /// </summary>
    RTorrent = 4
}

/// <summary>
/// Result of a torrent client connection test.
/// </summary>
public class TorrentClientTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Version { get; init; }
    public long ResponseTimeMs { get; init; }

    public static TorrentClientTestResult Ok(string message, string? version = null, long responseTimeMs = 0)
        => new() { Success = true, Message = message, Version = version, ResponseTimeMs = responseTimeMs };

    public static TorrentClientTestResult Failed(string message)
        => new() { Success = false, Message = message };
}

/// <summary>
/// Result of adding a torrent.
/// </summary>
public class TorrentAddResult
{
    public bool Success { get; init; }
    public string? Hash { get; init; }
    public string? ErrorMessage { get; init; }

    public static TorrentAddResult Ok(string hash)
        => new() { Success = true, Hash = hash };

    public static TorrentAddResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Options for adding a torrent.
/// </summary>
public class TorrentAddOptions
{
    /// <summary>
    /// Category/label for the torrent.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Save/download path override.
    /// </summary>
    public string? SavePath { get; set; }

    /// <summary>
    /// Start torrent paused.
    /// </summary>
    public bool AddPaused { get; set; }

    /// <summary>
    /// Skip hash checking (for trusted sources).
    /// </summary>
    public bool SkipHashCheck { get; set; }

    /// <summary>
    /// Priority for this torrent.
    /// </summary>
    public TorrentPriority Priority { get; set; } = TorrentPriority.Normal;

    /// <summary>
    /// Ratio limit (e.g., 1.0 = 100%, 2.0 = 200%).
    /// </summary>
    public double? RatioLimit { get; set; }

    /// <summary>
    /// Seeding time limit in minutes.
    /// </summary>
    public int? SeedingTimeLimitMinutes { get; set; }

    /// <summary>
    /// Whether to enable sequential download.
    /// </summary>
    public bool SequentialDownload { get; set; }

    /// <summary>
    /// Whether to download first/last pieces first (for preview).
    /// </summary>
    public bool FirstLastPiecePriority { get; set; }
}

/// <summary>
/// Priority levels for torrents.
/// </summary>
public enum TorrentPriority
{
    /// <summary>
    /// Lower priority than normal.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority (default).
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Higher priority than normal.
    /// </summary>
    High = 2,

    /// <summary>
    /// Maximum/force priority.
    /// </summary>
    Force = 3
}

/// <summary>
/// Status of a torrent in the client.
/// </summary>
public class TorrentStatus
{
    /// <summary>
    /// Torrent info hash (unique identifier).
    /// </summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// Torrent name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Current state of the torrent.
    /// </summary>
    public TorrentState State { get; init; }

    /// <summary>
    /// Category/label.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Downloaded bytes.
    /// </summary>
    public long DownloadedBytes { get; init; }

    /// <summary>
    /// Uploaded bytes.
    /// </summary>
    public long UploadedBytes { get; init; }

    /// <summary>
    /// Download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBps { get; init; }

    /// <summary>
    /// Upload speed in bytes per second.
    /// </summary>
    public long UploadSpeedBps { get; init; }

    /// <summary>
    /// Number of seeds connected.
    /// </summary>
    public int Seeds { get; init; }

    /// <summary>
    /// Number of peers connected.
    /// </summary>
    public int Peers { get; init; }

    /// <summary>
    /// Share ratio (uploaded / downloaded).
    /// </summary>
    public double Ratio { get; init; }

    /// <summary>
    /// Estimated time remaining in seconds.
    /// </summary>
    public int? EtaSeconds { get; init; }

    /// <summary>
    /// Save/download path.
    /// </summary>
    public string? SavePath { get; init; }

    /// <summary>
    /// Content path (actual file/folder location).
    /// </summary>
    public string? ContentPath { get; init; }

    /// <summary>
    /// When the torrent was added.
    /// </summary>
    public DateTime? AddedOn { get; init; }

    /// <summary>
    /// When the download completed.
    /// </summary>
    public DateTime? CompletedOn { get; init; }

    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;

    /// <summary>
    /// Whether the torrent is completed downloading.
    /// </summary>
    public bool IsCompleted => State == TorrentState.Completed || State == TorrentState.Seeding || Progress >= 100;

    /// <summary>
    /// Estimated time remaining as TimeSpan.
    /// </summary>
    public TimeSpan? TimeRemaining => EtaSeconds.HasValue ? TimeSpan.FromSeconds(EtaSeconds.Value) : null;
}

/// <summary>
/// States a torrent can be in.
/// </summary>
public enum TorrentState
{
    /// <summary>
    /// Waiting in queue.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Currently downloading.
    /// </summary>
    Downloading = 1,

    /// <summary>
    /// Paused by user.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Checking/verifying files.
    /// </summary>
    Checking = 3,

    /// <summary>
    /// Seeding to other peers.
    /// </summary>
    Seeding = 4,

    /// <summary>
    /// Download completed (may still be seeding).
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Error occurred.
    /// </summary>
    Error = 6,

    /// <summary>
    /// Stalled (no seeds/peers available).
    /// </summary>
    Stalled = 7,

    /// <summary>
    /// Metadata downloading (magnet).
    /// </summary>
    FetchingMetadata = 8,

    /// <summary>
    /// Moving files.
    /// </summary>
    Moving = 9,

    /// <summary>
    /// Unknown state.
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Disk space information.
/// </summary>
public class TorrentDiskSpace
{
    /// <summary>
    /// Free space in bytes.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Total space in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Whether free space is low.
    /// </summary>
    public bool IsLow { get; init; }

    /// <summary>
    /// Path being checked.
    /// </summary>
    public string? Path { get; init; }
}
