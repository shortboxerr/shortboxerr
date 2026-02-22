namespace Shortboxerr.Core.Torrent;

/// <summary>
/// Service responsible for handling completed torrent imports.
/// Detects completed downloads, handles file transfer (copy/hardlink),
/// and manages torrent removal based on seeding requirements.
/// </summary>
public interface ITorrentImportService
{
    /// <summary>
    /// Scans for completed torrents and processes them for import.
    /// </summary>
    Task<IReadOnlyList<TorrentImportResult>> ProcessCompletedTorrentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a specific completed torrent for import.
    /// </summary>
    Task<TorrentImportResult> ProcessTorrentAsync(
        string hash,
        TorrentClientType clientType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a torrent is ready for import (completed + seeding requirements met).
    /// </summary>
    Task<TorrentReadyResult> CheckTorrentReadyAsync(
        TorrentStatus status,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports files from a completed torrent to the library.
    /// </summary>
    Task<TorrentFileImportResult> ImportFilesAsync(
        TorrentStatus status,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles post-import cleanup (remove torrent if configured).
    /// </summary>
    Task<bool> CleanupTorrentAsync(
        string hash,
        TorrentClientType clientType,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current import settings.
    /// </summary>
    Task<TorrentImportSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the import settings.
    /// </summary>
    Task SaveSettingsAsync(TorrentImportSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// Settings for torrent import handling.
/// </summary>
public class TorrentImportSettings
{
    /// <summary>
    /// Whether to automatically process completed torrents.
    /// </summary>
    public bool AutoImportEnabled { get; set; } = true;

    /// <summary>
    /// How to transfer files: Copy, HardLink, or Move.
    /// </summary>
    public FileTransferMode TransferMode { get; set; } = FileTransferMode.HardLink;

    /// <summary>
    /// Whether to remove torrent after successful import.
    /// </summary>
    public bool RemoveAfterImport { get; set; } = false;

    /// <summary>
    /// Whether to delete downloaded files when removing torrent.
    /// Only applies if RemoveAfterImport is true.
    /// </summary>
    public bool DeleteFilesOnRemove { get; set; } = false;

    /// <summary>
    /// Minimum seeding ratio before allowing removal.
    /// Set to 0 to ignore ratio requirements.
    /// </summary>
    public double MinimumSeedRatio { get; set; } = 1.0;

    /// <summary>
    /// Minimum seeding time in minutes before allowing removal.
    /// Set to 0 to ignore time requirements.
    /// </summary>
    public int MinimumSeedTimeMinutes { get; set; } = 0;

    /// <summary>
    /// Whether ratio OR time requirement is sufficient (true),
    /// or both must be met (false).
    /// </summary>
    public bool SeedRequirementsOrMode { get; set; } = true;

    /// <summary>
    /// Category/label to filter which torrents to process.
    /// Empty = process all torrents.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Destination directory for imported files.
    /// If null, uses the default library path.
    /// </summary>
    public string? DestinationPath { get; set; }

    /// <summary>
    /// How often to scan for completed torrents (in minutes).
    /// </summary>
    public int ScanIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// File extensions to import (e.g., ".cbz", ".cbr").
    /// Empty = import all files.
    /// </summary>
    public List<string> FileExtensions { get; set; } = new() { ".cbz", ".cbr", ".cb7", ".pdf" };

    /// <summary>
    /// Whether to extract archives during import.
    /// </summary>
    public bool ExtractArchives { get; set; } = false;

    /// <summary>
    /// Whether to preserve folder structure from torrent.
    /// </summary>
    public bool PreserveFolderStructure { get; set; } = false;
}

/// <summary>
/// How to transfer files from download location to library.
/// </summary>
public enum FileTransferMode
{
    /// <summary>
    /// Copy files (safest, uses more disk space).
    /// </summary>
    Copy = 0,

    /// <summary>
    /// Create hard links (efficient, same filesystem only).
    /// </summary>
    HardLink = 1,

    /// <summary>
    /// Move files (removes from download location).
    /// Cannot be used if seeding is required.
    /// </summary>
    Move = 2
}

/// <summary>
/// Result of processing a completed torrent.
/// </summary>
public class TorrentImportResult
{
    /// <summary>
    /// Torrent hash.
    /// </summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// Torrent name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Client type the torrent was from.
    /// </summary>
    public TorrentClientType ClientType { get; init; }

    /// <summary>
    /// Whether import was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Import status.
    /// </summary>
    public TorrentImportStatus Status { get; init; }

    /// <summary>
    /// Number of files imported.
    /// </summary>
    public int FilesImported { get; init; }

    /// <summary>
    /// Total bytes imported.
    /// </summary>
    public long BytesImported { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When the import was processed.
    /// </summary>
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the torrent was removed after import.
    /// </summary>
    public bool TorrentRemoved { get; init; }

    public static TorrentImportResult Imported(string hash, string name, TorrentClientType clientType, int files, long bytes, bool removed = false)
        => new()
        {
            Hash = hash,
            Name = name,
            ClientType = clientType,
            Success = true,
            Status = TorrentImportStatus.Imported,
            FilesImported = files,
            BytesImported = bytes,
            TorrentRemoved = removed
        };

    public static TorrentImportResult Skipped(string hash, string name, TorrentClientType clientType, TorrentImportStatus reason)
        => new()
        {
            Hash = hash,
            Name = name,
            ClientType = clientType,
            Success = true,
            Status = reason
        };

    public static TorrentImportResult Failed(string hash, string name, TorrentClientType clientType, string error)
        => new()
        {
            Hash = hash,
            Name = name,
            ClientType = clientType,
            Success = false,
            Status = TorrentImportStatus.Failed,
            ErrorMessage = error
        };
}

/// <summary>
/// Status of a torrent import operation.
/// </summary>
public enum TorrentImportStatus
{
    /// <summary>
    /// Successfully imported.
    /// </summary>
    Imported = 0,

    /// <summary>
    /// Skipped - not completed yet.
    /// </summary>
    NotCompleted = 1,

    /// <summary>
    /// Skipped - still seeding (ratio not met).
    /// </summary>
    SeedingRatioNotMet = 2,

    /// <summary>
    /// Skipped - still seeding (time not met).
    /// </summary>
    SeedingTimeNotMet = 3,

    /// <summary>
    /// Skipped - wrong category.
    /// </summary>
    WrongCategory = 4,

    /// <summary>
    /// Skipped - no matching files found.
    /// </summary>
    NoMatchingFiles = 5,

    /// <summary>
    /// Skipped - already imported.
    /// </summary>
    AlreadyImported = 6,

    /// <summary>
    /// Import failed.
    /// </summary>
    Failed = 7
}

/// <summary>
/// Result of checking if a torrent is ready for import.
/// </summary>
public class TorrentReadyResult
{
    /// <summary>
    /// Whether the torrent is ready for import.
    /// </summary>
    public bool IsReady { get; init; }

    /// <summary>
    /// Reason if not ready.
    /// </summary>
    public TorrentImportStatus Status { get; init; }

    /// <summary>
    /// Current ratio (if seeding).
    /// </summary>
    public double? CurrentRatio { get; init; }

    /// <summary>
    /// Required ratio.
    /// </summary>
    public double? RequiredRatio { get; init; }

    /// <summary>
    /// Minutes seeded so far.
    /// </summary>
    public int? MinutesSeeded { get; init; }

    /// <summary>
    /// Required minutes to seed.
    /// </summary>
    public int? RequiredMinutes { get; init; }

    public static TorrentReadyResult Ready()
        => new() { IsReady = true, Status = TorrentImportStatus.Imported };

    public static TorrentReadyResult NotReady(TorrentImportStatus reason, double? currentRatio = null, double? requiredRatio = null, int? minutesSeeded = null, int? requiredMinutes = null)
        => new()
        {
            IsReady = false,
            Status = reason,
            CurrentRatio = currentRatio,
            RequiredRatio = requiredRatio,
            MinutesSeeded = minutesSeeded,
            RequiredMinutes = requiredMinutes
        };
}

/// <summary>
/// Result of importing files from a torrent.
/// </summary>
public class TorrentFileImportResult
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of files imported.
    /// </summary>
    public int FilesImported { get; init; }

    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// List of imported file paths.
    /// </summary>
    public List<string> ImportedFiles { get; init; } = new();

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether hard links were used.
    /// </summary>
    public bool UsedHardLinks { get; init; }

    public static TorrentFileImportResult Succeeded(int files, long bytes, List<string> paths, bool hardLinks)
        => new()
        {
            Success = true,
            FilesImported = files,
            BytesTransferred = bytes,
            ImportedFiles = paths,
            UsedHardLinks = hardLinks
        };

    public static TorrentFileImportResult NoFiles()
        => new()
        {
            Success = true,
            FilesImported = 0,
            BytesTransferred = 0
        };

    public static TorrentFileImportResult Error(string message)
        => new()
        {
            Success = false,
            ErrorMessage = message
        };
}
