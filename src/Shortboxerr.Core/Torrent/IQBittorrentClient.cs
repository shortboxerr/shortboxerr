namespace Shortboxerr.Core.Torrent;

/// <summary>
/// qBittorrent-specific client interface extending the base torrent client.
/// qBittorrent uses a Web API v2 with session-based authentication.
/// Reference: https://github.com/qbittorrent/qBittorrent/wiki/WebUI-API-(qBittorrent-4.1)
/// </summary>
public interface IQBittorrentClient : ITorrentClient
{
    /// <summary>
    /// Gets the qBittorrent version.
    /// </summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Web API version.
    /// </summary>
    Task<string?> GetApiVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses all torrents.
    /// </summary>
    Task<bool> PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes all torrents.
    /// </summary>
    Task<bool> ResumeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the global download speed limit.
    /// </summary>
    /// <param name="speedBps">Speed limit in bytes per second. 0 = unlimited.</param>
    Task<bool> SetDownloadLimitAsync(long speedBps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the global upload speed limit.
    /// </summary>
    /// <param name="speedBps">Speed limit in bytes per second. 0 = unlimited.</param>
    Task<bool> SetUploadLimitAsync(long speedBps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the transfer info (global download/upload stats).
    /// </summary>
    Task<QBittorrentTransferInfo?> GetTransferInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets application preferences.
    /// </summary>
    Task<QBittorrentPreferences?> GetPreferencesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new category.
    /// </summary>
    Task<bool> CreateCategoryAsync(string name, string? savePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rechecks a torrent.
    /// </summary>
    Task<bool> RecheckTorrentAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a torrent to start (ignores queue).
    /// </summary>
    Task<bool> ForceStartAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the category for a torrent.
    /// </summary>
    Task<bool> SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets torrent priority (first, last, increase, decrease).
    /// </summary>
    Task<bool> SetPriorityAsync(string hash, QBittorrentPriority priority, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for connecting to qBittorrent.
/// </summary>
public class QBittorrentSettings
{
    /// <summary>
    /// qBittorrent hostname (e.g., localhost, 192.168.1.100).
    /// Do not include protocol (http://) or port.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// qBittorrent Web UI port number.
    /// Default: 8080 (qBittorrent's default port).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Default category for comic downloads.
    /// </summary>
    public string Category { get; set; } = "comics";

    /// <summary>
    /// Default save path override.
    /// </summary>
    public string? SavePath { get; set; }

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
    /// Default ratio limit for seeding.
    /// </summary>
    public double? DefaultRatioLimit { get; set; }

    /// <summary>
    /// Default seeding time limit in minutes.
    /// </summary>
    public int? DefaultSeedingTimeLimit { get; set; }

    /// <summary>
    /// Whether to start sequential download by default.
    /// </summary>
    public bool SequentialDownload { get; set; } = false;

    /// <summary>
    /// Whether to prioritize first/last pieces by default.
    /// </summary>
    public bool FirstLastPiecePriority { get; set; } = false;

    /// <summary>
    /// Gets the effective port number.
    /// Default: 8080 for HTTP, 8080 for HTTPS (qBittorrent uses same port).
    /// </summary>
    public int EffectivePort => Port ?? 8080;

    /// <summary>
    /// Gets the full base URL for qBittorrent Web API.
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
    /// Gets the API endpoint URL.
    /// </summary>
    public string ApiUrl => $"{BaseUrl}/api/v2";
}

/// <summary>
/// qBittorrent-specific priority operations.
/// </summary>
public enum QBittorrentPriority
{
    /// <summary>
    /// Move to top of queue.
    /// </summary>
    TopPriority,

    /// <summary>
    /// Move to bottom of queue.
    /// </summary>
    BottomPriority,

    /// <summary>
    /// Increase priority by one.
    /// </summary>
    IncreasePriority,

    /// <summary>
    /// Decrease priority by one.
    /// </summary>
    DecreasePriority
}

/// <summary>
/// qBittorrent transfer info (global stats).
/// </summary>
public class QBittorrentTransferInfo
{
    /// <summary>
    /// Global download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBps { get; init; }

    /// <summary>
    /// Global upload speed in bytes per second.
    /// </summary>
    public long UploadSpeedBps { get; init; }

    /// <summary>
    /// Download speed limit in bytes per second (0 = unlimited).
    /// </summary>
    public long DownloadLimitBps { get; init; }

    /// <summary>
    /// Upload speed limit in bytes per second (0 = unlimited).
    /// </summary>
    public long UploadLimitBps { get; init; }

    /// <summary>
    /// Total session downloaded bytes.
    /// </summary>
    public long SessionDownloadedBytes { get; init; }

    /// <summary>
    /// Total session uploaded bytes.
    /// </summary>
    public long SessionUploadedBytes { get; init; }

    /// <summary>
    /// Total all-time downloaded bytes.
    /// </summary>
    public long AllTimeDownloadedBytes { get; init; }

    /// <summary>
    /// Total all-time uploaded bytes.
    /// </summary>
    public long AllTimeUploadedBytes { get; init; }

    /// <summary>
    /// Connection status.
    /// </summary>
    public string ConnectionStatus { get; init; } = string.Empty;

    /// <summary>
    /// DHT nodes connected.
    /// </summary>
    public int DhtNodes { get; init; }

    /// <summary>
    /// Free disk space on download path.
    /// </summary>
    public long FreeDiskSpaceBytes { get; init; }
}

/// <summary>
/// qBittorrent application preferences.
/// </summary>
public class QBittorrentPreferences
{
    /// <summary>
    /// Download directory.
    /// </summary>
    public string? SavePath { get; init; }

    /// <summary>
    /// Export .torrent files directory.
    /// </summary>
    public string? ExportDir { get; init; }

    /// <summary>
    /// Export finished .torrent files directory.
    /// </summary>
    public string? ExportDirFin { get; init; }

    /// <summary>
    /// Whether to append extension to incomplete files.
    /// </summary>
    public bool AppendExtension { get; init; }

    /// <summary>
    /// Whether to pre-allocate disk space.
    /// </summary>
    public bool PreallocateAll { get; init; }

    /// <summary>
    /// Maximum active downloads.
    /// </summary>
    public int MaxActiveDownloads { get; init; }

    /// <summary>
    /// Maximum active uploads.
    /// </summary>
    public int MaxActiveUploads { get; init; }

    /// <summary>
    /// Maximum active torrents.
    /// </summary>
    public int MaxActiveTorrents { get; init; }

    /// <summary>
    /// Whether queueing is enabled.
    /// </summary>
    public bool QueueingEnabled { get; init; }

    /// <summary>
    /// Listen port.
    /// </summary>
    public int ListenPort { get; init; }
}
