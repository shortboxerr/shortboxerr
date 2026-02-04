namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Interface for importing Mylar3 configuration files.
/// </summary>
public interface IMylar3ConfigImporter
{
    /// <summary>
    /// Parse a Mylar3 config.ini file and extract DDL provider configurations.
    /// </summary>
    /// <param name="configContent">Contents of the config.ini file</param>
    /// <returns>Import result with extracted providers and validation report</returns>
    Mylar3ImportResult ParseConfig(string configContent);
    
    /// <summary>
    /// Parse a Mylar3 config.ini file from a file path.
    /// </summary>
    /// <param name="filePath">Path to config.ini</param>
    /// <returns>Import result with extracted providers and validation report</returns>
    Task<Mylar3ImportResult> ParseConfigFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate imported providers against the current system state.
    /// </summary>
    Task<Mylar3ValidationReport> ValidateImportAsync(Mylar3ImportResult importResult, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute the import, creating provider definitions in the database.
    /// </summary>
    Task<Mylar3ExecutionResult> ExecuteImportAsync(Mylar3ImportResult importResult, Mylar3ImportOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Import pull list settings from parsed config.
    /// </summary>
    Task<Mylar3PullListImportResult> ImportPullListSettingsAsync(
        Mylar3PullListSettings settings, 
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of importing pull list settings from Mylar3.
/// </summary>
public class Mylar3PullListImportResult
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Settings that were imported.
    /// </summary>
    public List<string> ImportedSettings { get; set; } = new();

    /// <summary>
    /// Settings that were skipped (already set and overwrite=false).
    /// </summary>
    public List<string> SkippedSettings { get; set; } = new();

    /// <summary>
    /// Settings that couldn't be mapped to Shortboxerr equivalents.
    /// </summary>
    public List<string> UnmappedSettings { get; set; } = new();

    /// <summary>
    /// Warnings during import.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of parsing a Mylar3 config file.
/// </summary>
public record Mylar3ImportResult
{
    /// <summary>
    /// Whether the parse was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Extracted DDL provider configurations.
    /// </summary>
    public List<Mylar3DdlProvider> DdlProviders { get; init; } = new();
    
    /// <summary>
    /// General settings extracted from the config.
    /// </summary>
    public Mylar3GeneralSettings? GeneralSettings { get; init; }
    
    /// <summary>
    /// Pull list settings extracted from the config.
    /// </summary>
    public Mylar3PullListSettings? PullListSettings { get; init; }
    
    /// <summary>
    /// Sections that were found but not mapped.
    /// </summary>
    public List<string> UnmappedSections { get; init; } = new();
    
    /// <summary>
    /// Settings within sections that couldn't be mapped.
    /// </summary>
    public Dictionary<string, List<string>> UnmappedSettings { get; init; } = new();
    
    /// <summary>
    /// Warnings generated during parsing.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// Path to the source file (if imported from file).
    /// </summary>
    public string? SourcePath { get; init; }
    
    /// <summary>
    /// When the import was performed.
    /// </summary>
    public DateTime ImportedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A DDL provider configuration extracted from Mylar3.
/// </summary>
public class Mylar3DdlProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Site type (GettyComics, ReadComicOnline, etc.).
    /// </summary>
    public required string SiteType { get; init; }
    
    /// <summary>
    /// Base URL (if configured).
    /// </summary>
    public string? BaseUrl { get; init; }
    
    /// <summary>
    /// Whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
    
    /// <summary>
    /// Priority order.
    /// </summary>
    public int Priority { get; init; }
    
    /// <summary>
    /// Username (if auth required).
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Password (if auth required).
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// API key (if applicable).
    /// </summary>
    public string? ApiKey { get; init; }
    
    /// <summary>
    /// DDL-specific settings.
    /// </summary>
    public DdlProviderSettings? Settings { get; init; }
    
    /// <summary>
    /// Original section name in config.ini.
    /// </summary>
    public string? OriginalSection { get; init; }
    
    /// <summary>
    /// Raw settings from the INI file.
    /// </summary>
    public Dictionary<string, string> RawSettings { get; init; } = new();
}

/// <summary>
/// General settings extracted from Mylar3 config.
/// </summary>
public class Mylar3GeneralSettings
{
    /// <summary>
    /// Comic folder path.
    /// </summary>
    public string? ComicLocation { get; init; }
    
    /// <summary>
    /// Default quality profile.
    /// </summary>
    public string? QualityProfile { get; init; }
    
    /// <summary>
    /// Whether to auto-grab releases.
    /// </summary>
    public bool AutoGrab { get; init; } = true;
    
    /// <summary>
    /// Preferred format.
    /// </summary>
    public string? PreferredFormat { get; init; }
    
    /// <summary>
    /// Staging folder path.
    /// </summary>
    public string? StagingFolder { get; init; }
    
    /// <summary>
    /// Database path.
    /// </summary>
    public string? DatabasePath { get; init; }
}

/// <summary>
/// Pull list settings extracted from Mylar3 config.
/// </summary>
public class Mylar3PullListSettings
{
    /// <summary>
    /// Weekly pull list export folder path.
    /// </summary>
    public string? WeeklyPullFolder { get; init; }

    /// <summary>
    /// Weekly pull list export format (json, text, csv).
    /// </summary>
    public string? WeeklyPullFormat { get; init; }

    /// <summary>
    /// Whether to enable weekly pull list export.
    /// </summary>
    public bool? WeeklyPullEnabled { get; init; }

    /// <summary>
    /// Default series monitoring mode (all, future, manual, first_issue).
    /// </summary>
    public string? DefaultMonitoringMode { get; init; }

    /// <summary>
    /// Whether to auto-add new issues to wanted list.
    /// </summary>
    public bool? AutoAddToWanted { get; init; }

    /// <summary>
    /// Whether to include annuals in auto-add.
    /// </summary>
    public bool? IncludeAnnuals { get; init; }

    /// <summary>
    /// Whether to include specials in auto-add.
    /// </summary>
    public bool? IncludeSpecials { get; init; }

    /// <summary>
    /// Whether to skip variant covers.
    /// </summary>
    public bool? SkipVariants { get; init; }

    /// <summary>
    /// Hours to delay search after release.
    /// </summary>
    public int? SearchDelayHours { get; init; }

    /// <summary>
    /// Day of week to start the comic week (0=Sunday).
    /// </summary>
    public int? WeekStartDay { get; init; }

    /// <summary>
    /// Raw settings from the INI file for reference.
    /// </summary>
    public Dictionary<string, string> RawSettings { get; init; } = new();

    /// <summary>
    /// Settings that couldn't be mapped.
    /// </summary>
    public List<string> UnmappedSettings { get; init; } = new();
}

/// <summary>
/// Validation report for import.
/// </summary>
public class Mylar3ValidationReport
{
    /// <summary>
    /// Whether the import is valid.
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// Validation errors (blocking).
    /// </summary>
    public List<string> Errors { get; init; } = new();
    
    /// <summary>
    /// Validation warnings (non-blocking).
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// Providers that will be created.
    /// </summary>
    public List<string> ProvidersToCreate { get; init; } = new();
    
    /// <summary>
    /// Providers that already exist and will be skipped/updated.
    /// </summary>
    public List<string> ExistingProviders { get; init; } = new();
    
    /// <summary>
    /// Settings that differ from Mylar3 defaults.
    /// </summary>
    public List<string> SettingDeviations { get; init; } = new();
}

/// <summary>
/// Options for executing an import.
/// </summary>
public class Mylar3ImportOptions
{
    /// <summary>
    /// Whether to overwrite existing providers with the same name.
    /// </summary>
    public bool OverwriteExisting { get; init; }
    
    /// <summary>
    /// Whether to import disabled providers.
    /// </summary>
    public bool ImportDisabled { get; init; } = true;
    
    /// <summary>
    /// Whether to import credentials (passwords, API keys).
    /// </summary>
    public bool ImportCredentials { get; init; } = true;
    
    /// <summary>
    /// Prefix to add to imported provider names.
    /// </summary>
    public string? NamePrefix { get; init; }
    
    /// <summary>
    /// Whether to validate before executing.
    /// </summary>
    public bool ValidateFirst { get; init; } = true;
}

/// <summary>
/// Result of executing an import.
/// </summary>
public class Mylar3ExecutionResult
{
    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Number of providers created.
    /// </summary>
    public int ProvidersCreated { get; init; }
    
    /// <summary>
    /// Number of providers updated.
    /// </summary>
    public int ProvidersUpdated { get; init; }
    
    /// <summary>
    /// Number of providers skipped.
    /// </summary>
    public int ProvidersSkipped { get; init; }
    
    /// <summary>
    /// IDs of created providers.
    /// </summary>
    public List<int> CreatedProviderIds { get; init; } = new();
    
    /// <summary>
    /// Detailed results per provider.
    /// </summary>
    public List<ProviderImportDetail> Details { get; init; } = new();
}

/// <summary>
/// Detail for a single provider import.
/// </summary>
public class ProviderImportDetail
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Action taken (Created, Updated, Skipped, Failed).
    /// </summary>
    public ProviderImportAction Action { get; init; }
    
    /// <summary>
    /// Provider ID (if created/updated).
    /// </summary>
    public int? ProviderId { get; init; }
    
    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Import action for a provider.
/// </summary>
public enum ProviderImportAction
{
    Created,
    Updated,
    Skipped,
    Failed
}

