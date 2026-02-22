namespace Shortboxerr.Core.Import;

/// <summary>
/// Service for importing configuration from Mylar3 config.ini files.
/// Supports importing NZB indexers (newznab), download client settings (SABnzbd/NZBGet),
/// and provides validation reports.
/// </summary>
public interface IMylar3ConfigImporter
{
    /// <summary>
    /// Parses a Mylar3 config.ini file and extracts importable settings.
    /// </summary>
    Task<Mylar3ConfigParseResult> ParseConfigAsync(
        string configPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses Mylar3 config.ini content from a string.
    /// </summary>
    Task<Mylar3ConfigParseResult> ParseConfigContentAsync(
        string configContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports parsed settings into Shortboxerr configuration.
    /// </summary>
    Task<Mylar3ImportResult> ImportAsync(
        Mylar3ConfigParseResult parseResult,
        Mylar3ImportOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates parsed settings without importing.
    /// </summary>
    Task<Mylar3ValidationReport> ValidateAsync(
        Mylar3ConfigParseResult parseResult,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing a Mylar3 config.ini file.
/// </summary>
public class Mylar3ConfigParseResult
{
    /// <summary>
    /// Whether parsing was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed newznab indexer configurations.
    /// </summary>
    public List<Mylar3NewznabConfig> Indexers { get; init; } = new();

    /// <summary>
    /// Parsed SABnzbd configuration (if present).
    /// </summary>
    public Mylar3SabnzbdConfig? Sabnzbd { get; init; }

    /// <summary>
    /// Parsed NZBGet configuration (if present).
    /// </summary>
    public Mylar3NzbgetConfig? Nzbget { get; init; }

    /// <summary>
    /// General Mylar3 settings that may be useful.
    /// </summary>
    public Mylar3GeneralConfig? General { get; init; }

    /// <summary>
    /// Parsing errors encountered.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Warnings during parsing.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Raw sections found in the config file.
    /// </summary>
    public List<string> SectionsFound { get; init; } = new();

    public static Mylar3ConfigParseResult Failed(string error)
        => new() { Success = false, Errors = new List<string> { error } };

    public static Mylar3ConfigParseResult Parsed(
        List<Mylar3NewznabConfig> indexers,
        Mylar3SabnzbdConfig? sabnzbd,
        Mylar3NzbgetConfig? nzbget,
        Mylar3GeneralConfig? general,
        List<string> warnings,
        List<string> sections)
        => new()
        {
            Success = true,
            Indexers = indexers,
            Sabnzbd = sabnzbd,
            Nzbget = nzbget,
            General = general,
            Warnings = warnings,
            SectionsFound = sections
        };
}

/// <summary>
/// Newznab indexer configuration from Mylar3.
/// </summary>
public class Mylar3NewznabConfig
{
    /// <summary>
    /// Indexer name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Indexer host URL.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// API key.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// User ID (if required by the indexer).
    /// </summary>
    public string? Uid { get; init; }

    /// <summary>
    /// Categories to search (comma-separated in Mylar3).
    /// </summary>
    public List<string> Categories { get; init; } = new();

    /// <summary>
    /// Whether the indexer is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether to verify SSL certificates.
    /// </summary>
    public bool VerifySsl { get; init; } = true;

    /// <summary>
    /// Provider type: newznab or torznab.
    /// </summary>
    public string ProviderType { get; init; } = "newznab";
}

/// <summary>
/// SABnzbd configuration from Mylar3.
/// </summary>
public class Mylar3SabnzbdConfig
{
    /// <summary>
    /// SABnzbd host URL.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// SABnzbd port.
    /// </summary>
    public int Port { get; init; } = 8080;

    /// <summary>
    /// API key.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Category for comic downloads.
    /// </summary>
    public string Category { get; init; } = "comics";

    /// <summary>
    /// Whether to use SSL.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Download priority.
    /// </summary>
    public string? Priority { get; init; }

    /// <summary>
    /// Whether SABnzbd is enabled in Mylar3.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// NZBGet configuration from Mylar3.
/// </summary>
public class Mylar3NzbgetConfig
{
    /// <summary>
    /// NZBGet host URL.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// NZBGet port.
    /// </summary>
    public int Port { get; init; } = 6789;

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Category for comic downloads.
    /// </summary>
    public string Category { get; init; } = "comics";

    /// <summary>
    /// Whether to use SSL.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Download priority.
    /// </summary>
    public string? Priority { get; init; }

    /// <summary>
    /// Whether NZBGet is enabled in Mylar3.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// General Mylar3 settings.
/// </summary>
public class Mylar3GeneralConfig
{
    /// <summary>
    /// Comics directory path.
    /// </summary>
    public string? ComicLocation { get; init; }

    /// <summary>
    /// Download directory path.
    /// </summary>
    public string? DownloadDirectory { get; init; }

    /// <summary>
    /// Whether NZB is the preferred method.
    /// </summary>
    public bool NzbEnabled { get; init; }

    /// <summary>
    /// Whether torrents are enabled.
    /// </summary>
    public bool TorrentEnabled { get; init; }

    /// <summary>
    /// Which download client is preferred: sabnzbd or nzbget.
    /// </summary>
    public string? PreferredNzbClient { get; init; }
}

/// <summary>
/// Options for importing Mylar3 settings.
/// </summary>
public class Mylar3ImportOptions
{
    /// <summary>
    /// Whether to import indexer configurations.
    /// </summary>
    public bool ImportIndexers { get; set; } = true;

    /// <summary>
    /// Whether to import SABnzbd settings.
    /// </summary>
    public bool ImportSabnzbd { get; set; } = true;

    /// <summary>
    /// Whether to import NZBGet settings.
    /// </summary>
    public bool ImportNzbget { get; set; } = true;

    /// <summary>
    /// Whether to overwrite existing configurations.
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// Whether to import disabled items.
    /// </summary>
    public bool ImportDisabled { get; set; } = false;

    /// <summary>
    /// Test connections after import.
    /// </summary>
    public bool TestConnections { get; set; } = true;
}

/// <summary>
/// Result of importing Mylar3 settings.
/// </summary>
public class Mylar3ImportResult
{
    /// <summary>
    /// Whether the import was successful overall.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of indexers imported.
    /// </summary>
    public int IndexersImported { get; init; }

    /// <summary>
    /// Number of indexers skipped.
    /// </summary>
    public int IndexersSkipped { get; init; }

    /// <summary>
    /// Whether SABnzbd was imported.
    /// </summary>
    public bool SabnzbdImported { get; init; }

    /// <summary>
    /// Whether NZBGet was imported.
    /// </summary>
    public bool NzbgetImported { get; init; }

    /// <summary>
    /// Import errors.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Import warnings.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Detailed results per imported item.
    /// </summary>
    public List<Mylar3ImportItemResult> ItemResults { get; init; } = new();

    public static Mylar3ImportResult Failed(string error)
        => new() { Success = false, Errors = new List<string> { error } };
}

/// <summary>
/// Result for a single imported item.
/// </summary>
public class Mylar3ImportItemResult
{
    /// <summary>
    /// Item type: Indexer, SABnzbd, NZBGet.
    /// </summary>
    public string ItemType { get; init; } = string.Empty;

    /// <summary>
    /// Item name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether import was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Action taken: Imported, Updated, Skipped.
    /// </summary>
    public ImportAction Action { get; init; }

    /// <summary>
    /// Connection test result (if tested).
    /// </summary>
    public bool? ConnectionTestPassed { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Action taken during import.
/// </summary>
public enum ImportAction
{
    /// <summary>
    /// Item was newly imported.
    /// </summary>
    Imported = 0,

    /// <summary>
    /// Existing item was updated.
    /// </summary>
    Updated = 1,

    /// <summary>
    /// Item was skipped (already exists or disabled).
    /// </summary>
    Skipped = 2,

    /// <summary>
    /// Import failed.
    /// </summary>
    Failed = 3
}

/// <summary>
/// Validation report for Mylar3 configuration.
/// </summary>
public class Mylar3ValidationReport
{
    /// <summary>
    /// Whether all validations passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors (blocking issues).
    /// </summary>
    public List<Mylar3ValidationItem> Errors { get; init; } = new();

    /// <summary>
    /// Validation warnings (non-blocking issues).
    /// </summary>
    public List<Mylar3ValidationItem> Warnings { get; init; } = new();

    /// <summary>
    /// Informational items.
    /// </summary>
    public List<Mylar3ValidationItem> Info { get; init; } = new();

    /// <summary>
    /// Summary of what will be imported.
    /// </summary>
    public Mylar3ImportSummary Summary { get; init; } = new();
}

/// <summary>
/// A single validation item.
/// </summary>
public class Mylar3ValidationItem
{
    /// <summary>
    /// Item type being validated.
    /// </summary>
    public string ItemType { get; init; } = string.Empty;

    /// <summary>
    /// Item name.
    /// </summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>
    /// Field being validated.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Validation message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Summary of items to be imported.
/// </summary>
public class Mylar3ImportSummary
{
    /// <summary>
    /// Total indexers found.
    /// </summary>
    public int TotalIndexers { get; init; }

    /// <summary>
    /// Enabled indexers (will be imported by default).
    /// </summary>
    public int EnabledIndexers { get; init; }

    /// <summary>
    /// Whether SABnzbd configuration was found.
    /// </summary>
    public bool HasSabnzbd { get; init; }

    /// <summary>
    /// Whether SABnzbd is enabled.
    /// </summary>
    public bool SabnzbdEnabled { get; init; }

    /// <summary>
    /// Whether NZBGet configuration was found.
    /// </summary>
    public bool HasNzbget { get; init; }

    /// <summary>
    /// Whether NZBGet is enabled.
    /// </summary>
    public bool NzbgetEnabled { get; init; }
}
