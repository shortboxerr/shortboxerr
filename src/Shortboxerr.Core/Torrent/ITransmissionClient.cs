namespace Shortboxerr.Core.Torrent;

/// <summary>
/// Transmission-specific client interface extending the base torrent client.
/// Transmission uses a JSON-RPC API with session ID for CSRF protection.
/// Reference: https://github.com/transmission/transmission/blob/main/docs/rpc-spec.md
/// </summary>
public interface ITransmissionClient : ITorrentClient
{
    /// <summary>
    /// Gets Transmission session information including version.
    /// </summary>
    Task<TransmissionSessionInfo?> GetSessionInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets session statistics (download/upload totals, etc.).
    /// </summary>
    Task<TransmissionSessionStats?> GetSessionStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts (resumes) all torrents.
    /// </summary>
    Task<bool> StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops (pauses) all torrents.
    /// </summary>
    Task<bool> StopAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a torrent's data to a new location.
    /// </summary>
    Task<bool> MoveTorrentAsync(string hash, string newLocation, bool moveData = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a torrent's path.
    /// </summary>
    Task<bool> RenameTorrentPathAsync(string hash, string oldPath, string newPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies (rechecks) a torrent.
    /// </summary>
    Task<bool> VerifyTorrentAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks tracker for more peers.
    /// </summary>
    Task<bool> ReannounceAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the download directory (session-level).
    /// </summary>
    Task<bool> SetDownloadDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the global speed limits.
    /// </summary>
    Task<bool> SetSpeedLimitsAsync(long? downloadLimitKBps, long? uploadLimitKBps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the free space in a directory.
    /// </summary>
    Task<long?> GetFreeSpaceAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for connecting to Transmission.
/// </summary>
public class TransmissionSettings
{
    /// <summary>
    /// Transmission hostname (e.g., localhost, 192.168.1.100).
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Transmission RPC port number.
    /// Default: 9091 (Transmission's default port).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Username for authentication (if enabled).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (if enabled).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Default download directory for new torrents.
    /// </summary>
    public string? DownloadDir { get; set; }

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
    /// RPC path (default: /transmission/rpc).
    /// Some reverse proxies may use a different path.
    /// </summary>
    public string RpcPath { get; set; } = "/transmission/rpc";

    /// <summary>
    /// Gets the effective port number.
    /// Default: 9091 for HTTP, 9091 for HTTPS.
    /// </summary>
    public int EffectivePort => Port ?? 9091;

    /// <summary>
    /// Gets the full RPC URL for Transmission.
    /// </summary>
    public string RpcUrl
    {
        get
        {
            var protocol = UseSsl ? "https" : "http";
            var path = RpcPath.StartsWith('/') ? RpcPath : $"/{RpcPath}";
            return $"{protocol}://{Host}:{EffectivePort}{path}";
        }
    }
}

/// <summary>
/// Transmission session information.
/// </summary>
public class TransmissionSessionInfo
{
    /// <summary>
    /// Transmission version string.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// RPC version number.
    /// </summary>
    public int RpcVersion { get; init; }

    /// <summary>
    /// Minimum RPC version this server supports.
    /// </summary>
    public int RpcVersionMinimum { get; init; }

    /// <summary>
    /// Default download directory.
    /// </summary>
    public string? DownloadDir { get; init; }

    /// <summary>
    /// Configuration directory.
    /// </summary>
    public string? ConfigDir { get; init; }

    /// <summary>
    /// Download speed limit in KB/s (0 = disabled).
    /// </summary>
    public long SpeedLimitDownKBps { get; init; }

    /// <summary>
    /// Whether download speed limit is enabled.
    /// </summary>
    public bool SpeedLimitDownEnabled { get; init; }

    /// <summary>
    /// Upload speed limit in KB/s (0 = disabled).
    /// </summary>
    public long SpeedLimitUpKBps { get; init; }

    /// <summary>
    /// Whether upload speed limit is enabled.
    /// </summary>
    public bool SpeedLimitUpEnabled { get; init; }

    /// <summary>
    /// Default seed ratio limit.
    /// </summary>
    public double SeedRatioLimit { get; init; }

    /// <summary>
    /// Whether seed ratio limit is enabled.
    /// </summary>
    public bool SeedRatioLimited { get; init; }

    /// <summary>
    /// Whether incomplete directory is enabled.
    /// </summary>
    public bool IncompleteDirEnabled { get; init; }

    /// <summary>
    /// Incomplete directory path.
    /// </summary>
    public string? IncompleteDir { get; init; }
}

/// <summary>
/// Transmission session statistics.
/// </summary>
public class TransmissionSessionStats
{
    /// <summary>
    /// Number of active torrents.
    /// </summary>
    public int ActiveTorrentCount { get; init; }

    /// <summary>
    /// Total number of paused torrents.
    /// </summary>
    public int PausedTorrentCount { get; init; }

    /// <summary>
    /// Total number of torrents.
    /// </summary>
    public int TorrentCount { get; init; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBps { get; init; }

    /// <summary>
    /// Current upload speed in bytes per second.
    /// </summary>
    public long UploadSpeedBps { get; init; }

    /// <summary>
    /// Cumulative session stats.
    /// </summary>
    public TransmissionCumulativeStats? CurrentStats { get; init; }

    /// <summary>
    /// Cumulative all-time stats.
    /// </summary>
    public TransmissionCumulativeStats? CumulativeStats { get; init; }
}

/// <summary>
/// Cumulative statistics for Transmission.
/// </summary>
public class TransmissionCumulativeStats
{
    /// <summary>
    /// Bytes downloaded.
    /// </summary>
    public long DownloadedBytes { get; init; }

    /// <summary>
    /// Bytes uploaded.
    /// </summary>
    public long UploadedBytes { get; init; }

    /// <summary>
    /// Files added.
    /// </summary>
    public int FilesAdded { get; init; }

    /// <summary>
    /// Session count.
    /// </summary>
    public int SessionCount { get; init; }

    /// <summary>
    /// Seconds active.
    /// </summary>
    public long SecondsActive { get; init; }
}
