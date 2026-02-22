namespace Shortboxerr.Core.Torrent;

/// <summary>
/// Deluge-specific client interface extending the base torrent client.
/// Deluge uses a JSON-RPC API with password-based authentication.
/// Reference: https://deluge.readthedocs.io/en/latest/devguide/how-to/curl-jsonrpc.html
/// </summary>
public interface IDelugeClient : ITorrentClient
{
    /// <summary>
    /// Gets Deluge daemon version information.
    /// </summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the libtorrent version.
    /// </summary>
    Task<string?> GetLibtorrentVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses all torrents.
    /// </summary>
    Task<bool> PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes all torrents.
    /// </summary>
    Task<bool> ResumeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the download/upload session totals.
    /// </summary>
    Task<DelugeSessionStatus?> GetSessionStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available labels (requires Label plugin).
    /// </summary>
    Task<IReadOnlyList<string>> GetLabelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a label on a torrent (requires Label plugin).
    /// </summary>
    Task<bool> SetLabelAsync(string hash, string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new label (requires Label plugin).
    /// </summary>
    Task<bool> AddLabelAsync(string label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a torrent's storage to a new location.
    /// </summary>
    Task<bool> MoveStorageAsync(string hash, string destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a torrent to recheck.
    /// </summary>
    Task<bool> ForceRecheckAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a torrent to reannounce to trackers.
    /// </summary>
    Task<bool> ForceReannounceAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets torrent options (max download/upload speed, etc.).
    /// </summary>
    Task<bool> SetTorrentOptionsAsync(string hash, DelugeTorrentOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the free disk space at the default download location.
    /// </summary>
    Task<long?> GetFreeSpaceAsync(string? path = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration values.
    /// </summary>
    Task<DelugeConfig?> GetConfigAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for connecting to Deluge daemon.
/// </summary>
public class DelugeSettings
{
    /// <summary>
    /// Deluge daemon hostname (e.g., localhost, 192.168.1.100).
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Deluge Web UI port number.
    /// Default: 8112 (Deluge Web UI default).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Password for the Deluge Web UI.
    /// Default password is "deluge".
    /// </summary>
    public string Password { get; set; } = "deluge";

    /// <summary>
    /// Default label for comic downloads (requires Label plugin).
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Default download path override.
    /// </summary>
    public string? DownloadPath { get; set; }

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
    /// Whether to move completed downloads to a different location.
    /// </summary>
    public bool MoveCompleted { get; set; } = false;

    /// <summary>
    /// Path to move completed downloads to.
    /// </summary>
    public string? MoveCompletedPath { get; set; }

    /// <summary>
    /// Gets the effective port number.
    /// Default: 8112 for Deluge Web UI.
    /// </summary>
    public int EffectivePort => Port ?? 8112;

    /// <summary>
    /// Gets the full base URL for Deluge Web UI.
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
    /// Gets the JSON-RPC endpoint URL.
    /// </summary>
    public string JsonRpcUrl => $"{BaseUrl}/json";
}

/// <summary>
/// Deluge session status (global stats).
/// </summary>
public class DelugeSessionStatus
{
    /// <summary>
    /// Current download rate in bytes per second.
    /// </summary>
    public long DownloadRateBps { get; init; }

    /// <summary>
    /// Current upload rate in bytes per second.
    /// </summary>
    public long UploadRateBps { get; init; }

    /// <summary>
    /// Total downloaded bytes this session.
    /// </summary>
    public long TotalDownloadedBytes { get; init; }

    /// <summary>
    /// Total uploaded bytes this session.
    /// </summary>
    public long TotalUploadedBytes { get; init; }

    /// <summary>
    /// Number of active downloads.
    /// </summary>
    public int NumDownloading { get; init; }

    /// <summary>
    /// Number of active uploads (seeding).
    /// </summary>
    public int NumSeeding { get; init; }

    /// <summary>
    /// Total number of torrents.
    /// </summary>
    public int NumTorrents { get; init; }

    /// <summary>
    /// Whether DHT is running.
    /// </summary>
    public bool DhtRunning { get; init; }

    /// <summary>
    /// Number of DHT nodes.
    /// </summary>
    public int DhtNodes { get; init; }

    /// <summary>
    /// Free disk space in bytes.
    /// </summary>
    public long FreeDiskSpace { get; init; }
}

/// <summary>
/// Options for setting torrent-specific settings.
/// </summary>
public class DelugeTorrentOptions
{
    /// <summary>
    /// Maximum download speed in KB/s (-1 = unlimited).
    /// </summary>
    public int? MaxDownloadSpeed { get; set; }

    /// <summary>
    /// Maximum upload speed in KB/s (-1 = unlimited).
    /// </summary>
    public int? MaxUploadSpeed { get; set; }

    /// <summary>
    /// Maximum connections for this torrent.
    /// </summary>
    public int? MaxConnections { get; set; }

    /// <summary>
    /// Maximum upload slots for this torrent.
    /// </summary>
    public int? MaxUploadSlots { get; set; }

    /// <summary>
    /// Whether to prioritize first/last pieces.
    /// </summary>
    public bool? PrioritizeFirstLastPieces { get; set; }

    /// <summary>
    /// Whether to enable sequential download.
    /// </summary>
    public bool? SequentialDownload { get; set; }

    /// <summary>
    /// Stop seeding when ratio reaches this value.
    /// </summary>
    public double? StopAtRatio { get; set; }

    /// <summary>
    /// Whether to remove the torrent when ratio is reached.
    /// </summary>
    public bool? RemoveAtRatio { get; set; }

    /// <summary>
    /// Whether to move completed downloads.
    /// </summary>
    public bool? MoveCompleted { get; set; }

    /// <summary>
    /// Path to move completed downloads to.
    /// </summary>
    public string? MoveCompletedPath { get; set; }

    /// <summary>
    /// Whether torrent should auto-manage.
    /// </summary>
    public bool? AutoManaged { get; set; }
}

/// <summary>
/// Deluge daemon configuration.
/// </summary>
public class DelugeConfig
{
    /// <summary>
    /// Default download location.
    /// </summary>
    public string? DownloadLocation { get; init; }

    /// <summary>
    /// Whether to move completed downloads.
    /// </summary>
    public bool MoveCompleted { get; init; }

    /// <summary>
    /// Path to move completed downloads to.
    /// </summary>
    public string? MoveCompletedPath { get; init; }

    /// <summary>
    /// Maximum download speed in KB/s (-1 = unlimited).
    /// </summary>
    public int MaxDownloadSpeed { get; init; }

    /// <summary>
    /// Maximum upload speed in KB/s (-1 = unlimited).
    /// </summary>
    public int MaxUploadSpeed { get; init; }

    /// <summary>
    /// Maximum connections.
    /// </summary>
    public int MaxConnections { get; init; }

    /// <summary>
    /// Maximum active downloads.
    /// </summary>
    public int MaxActiveDownloading { get; init; }

    /// <summary>
    /// Maximum active seeding.
    /// </summary>
    public int MaxActiveSeeding { get; init; }

    /// <summary>
    /// Maximum active torrents.
    /// </summary>
    public int MaxActiveLimit { get; init; }

    /// <summary>
    /// Whether DHT is enabled.
    /// </summary>
    public bool DhtEnabled { get; init; }

    /// <summary>
    /// Listen port range start.
    /// </summary>
    public int ListenPortStart { get; init; }

    /// <summary>
    /// Listen port range end.
    /// </summary>
    public int ListenPortEnd { get; init; }
}
