namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Interface for importing ComicVine-specific settings from Mylar3 configuration.
/// </summary>
public interface IMylar3ComicVineImporter
{
    /// <summary>
    /// Parse a Mylar3 config.ini file and extract ComicVine settings.
    /// </summary>
    Mylar3ComicVineSettings ParseComicVineSettings(string configContent);

    /// <summary>
    /// Parse a Mylar3 config.ini file from a file path.
    /// </summary>
    Task<Mylar3ComicVineSettings> ParseComicVineSettingsFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import ComicVine settings from Mylar3 config into Shortboxerr.
    /// </summary>
    Task<ComicVineImportResult> ImportComicVineSettingsAsync(
        Mylar3ComicVineSettings settings,
        ComicVineImportOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrate ComicVine IDs from a Mylar3 database to Shortboxerr series.
    /// </summary>
    Task<ComicVineIdMigrationResult> MigrateComicVineIdsAsync(
        string mylar3DbPath,
        ComicVineIdMigrationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate ComicVine IDs before migration.
    /// </summary>
    Task<ComicVineIdValidationResult> ValidateComicVineIdsAsync(
        string mylar3DbPath,
        CancellationToken cancellationToken = default);
}

#region Settings Types

/// <summary>
/// ComicVine settings extracted from Mylar3 config.ini.
/// </summary>
public class Mylar3ComicVineSettings
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SourcePath { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ComicVine API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Whether ComicVine integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cover cache enabled.
    /// </summary>
    public bool CacheCovers { get; set; } = true;

    /// <summary>
    /// Cover cache folder path.
    /// </summary>
    public string? CoverCachePath { get; set; }

    /// <summary>
    /// Auto-add new series from ComicVine.
    /// </summary>
    public bool AutoAdd { get; set; }

    /// <summary>
    /// Auto-match threshold (0-100).
    /// </summary>
    public int? AutoMatchThreshold { get; set; }

    /// <summary>
    /// Metadata refresh interval in days.
    /// </summary>
    public int? RefreshIntervalDays { get; set; }

    /// <summary>
    /// Preferred cover quality.
    /// </summary>
    public string? CoverQuality { get; set; }

    /// <summary>
    /// Skip variants when syncing issues.
    /// </summary>
    public bool SkipVariants { get; set; }

    /// <summary>
    /// Skip annuals when syncing issues.
    /// </summary>
    public bool SkipAnnuals { get; set; }

    /// <summary>
    /// Raw settings from the INI file for reference.
    /// </summary>
    public Dictionary<string, string> RawSettings { get; set; } = new();

    /// <summary>
    /// Settings that couldn't be mapped.
    /// </summary>
    public List<string> UnmappedSettings { get; set; } = new();

    /// <summary>
    /// Warnings during parsing.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

#endregion

#region Import Types

/// <summary>
/// Options for importing ComicVine settings.
/// </summary>
public class ComicVineImportOptions
{
    /// <summary>
    /// Whether to overwrite existing API key.
    /// </summary>
    public bool OverwriteApiKey { get; set; }

    /// <summary>
    /// Whether to import cache settings.
    /// </summary>
    public bool ImportCacheSettings { get; set; } = true;

    /// <summary>
    /// Whether to import auto-match settings.
    /// </summary>
    public bool ImportAutoMatchSettings { get; set; } = true;

    /// <summary>
    /// Whether to import refresh settings.
    /// </summary>
    public bool ImportRefreshSettings { get; set; } = true;
}

/// <summary>
/// Result of importing ComicVine settings.
/// </summary>
public class ComicVineImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool ApiKeyImported { get; set; }
    public bool CacheSettingsImported { get; set; }
    public bool AutoMatchSettingsImported { get; set; }
    public bool RefreshSettingsImported { get; set; }
    public List<string> ImportedSettings { get; set; } = new();
    public List<string> SkippedSettings { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion

#region Migration Types

/// <summary>
/// Options for migrating ComicVine IDs.
/// </summary>
public class ComicVineIdMigrationOptions
{
    /// <summary>
    /// Only migrate for series that match by title.
    /// </summary>
    public bool RequireTitleMatch { get; set; } = true;

    /// <summary>
    /// Validate each ComicVine ID is still valid.
    /// </summary>
    public bool ValidateIds { get; set; } = true;

    /// <summary>
    /// Whether to overwrite existing ComicVine IDs.
    /// </summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// Whether to sync metadata after migration.
    /// </summary>
    public bool SyncMetadataAfterMigration { get; set; } = true;

    /// <summary>
    /// Maximum series to migrate (0 = no limit).
    /// </summary>
    public int MaxSeries { get; set; }
}

/// <summary>
/// Result of validating ComicVine IDs.
/// </summary>
public class ComicVineIdValidationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalSeries { get; set; }
    public int ValidIds { get; set; }
    public int InvalidIds { get; set; }
    public int MatchedByTitle { get; set; }
    public int UnmatchedByTitle { get; set; }
    public List<ComicVineIdValidationItem> Items { get; set; } = new();
}

/// <summary>
/// Validation item for a single series.
/// </summary>
public class ComicVineIdValidationItem
{
    public required string Mylar3SeriesTitle { get; set; }
    public int Mylar3ComicVineId { get; set; }
    public bool IsIdValid { get; set; }
    public int? LocalSeriesId { get; set; }
    public string? LocalSeriesTitle { get; set; }
    public bool TitleMatched { get; set; }
    public string? ValidationError { get; set; }
}

/// <summary>
/// Result of migrating ComicVine IDs.
/// </summary>
public class ComicVineIdMigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalProcessed { get; set; }
    public int Migrated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int MetadataSynced { get; set; }
    public List<ComicVineIdMigrationItem> Items { get; set; } = new();
}

/// <summary>
/// Migration item for a single series.
/// </summary>
public class ComicVineIdMigrationItem
{
    public required string Mylar3SeriesTitle { get; set; }
    public int Mylar3ComicVineId { get; set; }
    public int? LocalSeriesId { get; set; }
    public required string Status { get; set; } // Migrated, Skipped, Failed
    public string? SkipReason { get; set; }
    public string? Error { get; set; }
    public bool MetadataSynced { get; set; }
}

#endregion

