using Shortboxerr.Core.Providers;

namespace Shortboxerr.Core.DownloadClients;

/// <summary>
/// Torrent download client abstraction.
/// Placeholder interface for future torrent support (qBittorrent, Transmission, Deluge, etc.).
/// NOTE: This is an interface-only placeholder - no implementation in EPIC 4.
/// </summary>
public interface ITorrentClient : IDownloadProvider
{
    /// <summary>
    /// Add a torrent by magnet link.
    /// </summary>
    Task<TorrentAddResult> AddMagnetAsync(
        string magnetLink,
        TorrentAddOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a torrent by .torrent file.
    /// </summary>
    Task<TorrentAddResult> AddTorrentFileAsync(
        byte[] torrentFile,
        TorrentAddOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a torrent by URL (downloads .torrent first).
    /// </summary>
    Task<TorrentAddResult> AddTorrentUrlAsync(
        string torrentUrl,
        TorrentAddOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get detailed information about a torrent.
    /// </summary>
    Task<TorrentInfo?> GetTorrentInfoAsync(
        string hash,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all torrents.
    /// </summary>
    Task<IReadOnlyList<TorrentInfo>> GetAllTorrentsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause a torrent.
    /// </summary>
    Task<bool> PauseAsync(string hash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resume a paused torrent.
    /// </summary>
    Task<bool> ResumeAsync(string hash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a torrent.
    /// </summary>
    Task<bool> RemoveAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set download location for a torrent.
    /// </summary>
    Task<bool> SetLocationAsync(string hash, string location, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set category/label for a torrent.
    /// </summary>
    Task<bool> SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for adding a torrent.
/// </summary>
public class TorrentAddOptions
{
    /// <summary>
    /// Download location override.
    /// </summary>
    public string? DownloadLocation { get; init; }
    
    /// <summary>
    /// Category/label to assign.
    /// </summary>
    public string? Category { get; init; }
    
    /// <summary>
    /// Whether to start in paused state.
    /// </summary>
    public bool StartPaused { get; init; }
    
    /// <summary>
    /// Download speed limit in KB/s (0 = unlimited).
    /// </summary>
    public int DownloadLimit { get; init; }
    
    /// <summary>
    /// Upload speed limit in KB/s (0 = unlimited).
    /// </summary>
    public int UploadLimit { get; init; }
    
    /// <summary>
    /// Ratio limit (stop seeding after reaching this ratio).
    /// </summary>
    public float? RatioLimit { get; init; }
    
    /// <summary>
    /// Seeding time limit in minutes.
    /// </summary>
    public int? SeedingTimeLimit { get; init; }
}

/// <summary>
/// Result of adding a torrent.
/// </summary>
public class TorrentAddResult
{
    /// <summary>
    /// Whether the torrent was added successfully.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Torrent hash (unique identifier).
    /// </summary>
    public string? Hash { get; init; }
    
    /// <summary>
    /// Torrent name.
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Error message if add failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Whether this torrent already existed.
    /// </summary>
    public bool AlreadyExists { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static TorrentAddResult Ok(string hash, string name) => new()
    {
        Success = true,
        Hash = hash,
        Name = name
    };
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static TorrentAddResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
    
    /// <summary>
    /// Create a duplicate result.
    /// </summary>
    public static TorrentAddResult Duplicate(string hash, string name) => new()
    {
        Success = true,
        Hash = hash,
        Name = name,
        AlreadyExists = true
    };
}

/// <summary>
/// Information about a torrent.
/// </summary>
public class TorrentInfo
{
    /// <summary>
    /// Torrent info hash.
    /// </summary>
    public required string Hash { get; init; }
    
    /// <summary>
    /// Torrent name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Current state.
    /// </summary>
    public TorrentState State { get; init; }
    
    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long TotalSize { get; init; }
    
    /// <summary>
    /// Downloaded bytes.
    /// </summary>
    public long Downloaded { get; init; }
    
    /// <summary>
    /// Uploaded bytes.
    /// </summary>
    public long Uploaded { get; init; }
    
    /// <summary>
    /// Download progress (0-100).
    /// </summary>
    public double Progress { get; init; }
    
    /// <summary>
    /// Current download speed in bytes/second.
    /// </summary>
    public long DownloadSpeed { get; init; }
    
    /// <summary>
    /// Current upload speed in bytes/second.
    /// </summary>
    public long UploadSpeed { get; init; }
    
    /// <summary>
    /// Share ratio.
    /// </summary>
    public float Ratio { get; init; }
    
    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? Eta { get; init; }
    
    /// <summary>
    /// Number of seeders.
    /// </summary>
    public int Seeders { get; init; }
    
    /// <summary>
    /// Number of leechers.
    /// </summary>
    public int Leechers { get; init; }
    
    /// <summary>
    /// When the torrent was added.
    /// </summary>
    public DateTime AddedAt { get; init; }
    
    /// <summary>
    /// When the torrent completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>
    /// Download location.
    /// </summary>
    public string? Location { get; init; }
    
    /// <summary>
    /// Category/label.
    /// </summary>
    public string? Category { get; init; }
    
    /// <summary>
    /// Content path (main file/folder).
    /// </summary>
    public string? ContentPath { get; init; }
}

/// <summary>
/// Torrent state.
/// </summary>
public enum TorrentState
{
    /// <summary>
    /// Unknown state.
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// Torrent is queued for download.
    /// </summary>
    QueuedForDownload = 1,
    
    /// <summary>
    /// Torrent is downloading.
    /// </summary>
    Downloading = 2,
    
    /// <summary>
    /// Torrent is paused.
    /// </summary>
    Paused = 3,
    
    /// <summary>
    /// Torrent is seeding.
    /// </summary>
    Seeding = 4,
    
    /// <summary>
    /// Torrent is queued for seeding.
    /// </summary>
    QueuedForSeeding = 5,
    
    /// <summary>
    /// Torrent has completed.
    /// </summary>
    Completed = 6,
    
    /// <summary>
    /// Torrent is in error state.
    /// </summary>
    Error = 7,
    
    /// <summary>
    /// Torrent metadata is being fetched.
    /// </summary>
    FetchingMetadata = 8,
    
    /// <summary>
    /// Torrent is being checked.
    /// </summary>
    Checking = 9,
    
    /// <summary>
    /// Torrent is stalled (no peers).
    /// </summary>
    Stalled = 10,
    
    /// <summary>
    /// Torrent upload is stalled.
    /// </summary>
    StalledUpload = 11
}

/// <summary>
/// Configuration for a torrent client.
/// </summary>
public class TorrentClientSettings
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
    /// Client type (qBittorrent, Transmission, Deluge, etc.).
    /// </summary>
    public required string ClientType { get; init; }
    
    /// <summary>
    /// Whether this client is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Priority for this client (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// Client host address.
    /// </summary>
    public required string Host { get; init; }
    
    /// <summary>
    /// Client port.
    /// </summary>
    public int Port { get; init; } = 8080;
    
    /// <summary>
    /// Use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; init; }
    
    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// Default download category/label.
    /// </summary>
    public string? DefaultCategory { get; init; }
    
    /// <summary>
    /// Default download directory.
    /// </summary>
    public string? DefaultDownloadDirectory { get; init; }
    
    /// <summary>
    /// Whether to remove torrent after import.
    /// </summary>
    public bool RemoveAfterImport { get; init; } = true;
    
    /// <summary>
    /// Whether to remove files after import.
    /// </summary>
    public bool RemoveFilesAfterImport { get; init; }
}

