namespace Shortboxerr.Core.Mylar3Migration;

/// <summary>
/// Service for migrating data from Mylar3 to Shortboxerr.
/// </summary>
public interface IMylar3MigrationService
{
    /// <summary>
    /// Analyzes a Mylar3 database and returns a snapshot of what can be migrated.
    /// </summary>
    Task<Mylar3Snapshot> AnalyzeDatabaseAsync(
        string dbPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the analysis to a JSON file for review.
    /// </summary>
    Task<string> ExportSnapshotAsync(
        Mylar3Snapshot snapshot,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports data from a Mylar3 snapshot into Shortboxerr.
    /// </summary>
    Task<Mylar3MigrationResult> ImportAsync(
        Mylar3Snapshot snapshot,
        Mylar3MigrationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the full migration: analyze → import.
    /// </summary>
    Task<Mylar3MigrationResult> MigrateAsync(
        string dbPath,
        Mylar3MigrationOptions options,
        CancellationToken cancellationToken = default);
}

#region Snapshot Types

/// <summary>
/// Snapshot of Mylar3 database content for migration.
/// </summary>
public class Mylar3Snapshot
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SourcePath { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Series found in Mylar3 database.
    /// </summary>
    public List<Mylar3Series> Series { get; set; } = new();

    /// <summary>
    /// Issues found in Mylar3 database.
    /// </summary>
    public List<Mylar3Issue> Issues { get; set; } = new();

    /// <summary>
    /// Files associated with issues/series in Mylar3.
    /// </summary>
    public List<Mylar3File> Files { get; set; } = new();

    /// <summary>
    /// Summary statistics.
    /// </summary>
    public Mylar3SnapshotStats Stats { get; set; } = new();

    /// <summary>
    /// Tables found in the database.
    /// </summary>
    public List<string> TablesFound { get; set; } = new();

    /// <summary>
    /// Warnings encountered during analysis.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Statistics from the Mylar3 snapshot.
/// </summary>
public class Mylar3SnapshotStats
{
    public int TotalSeries { get; set; }
    public int TotalIssues { get; set; }
    public int TotalFiles { get; set; }
    public int WantedIssues { get; set; }
    public int DownloadedIssues { get; set; }
    public int SeriesWithComicVineId { get; set; }
    public int IssuesWithComicVineId { get; set; }
}

/// <summary>
/// Series data from Mylar3.
/// </summary>
public class Mylar3Series
{
    public string? ComicId { get; set; }
    public string? ComicName { get; set; }
    public int? ComicYear { get; set; }
    public string? ComicPublisher { get; set; }
    public string? ComicImageUrl { get; set; }
    public string? Status { get; set; }
    public int? TotalIssues { get; set; }
    public int? HaveIssues { get; set; }
    public string? ComicLocation { get; set; }
    public bool IsIgnored { get; set; }
    public DateTime? DateAdded { get; set; }
    public DateTime? LastUpdated { get; set; }
    
    /// <summary>
    /// ComicVine volume ID (parsed from ComicId if numeric).
    /// </summary>
    public int? ComicVineId { get; set; }
}

/// <summary>
/// Issue data from Mylar3.
/// </summary>
public class Mylar3Issue
{
    public string? IssueId { get; set; }
    public string? ComicId { get; set; }
    public string? IssueNumber { get; set; }
    public string? IssueName { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? StoreDate { get; set; }
    public string? Status { get; set; }
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// ComicVine issue ID (parsed from IssueId if numeric).
    /// </summary>
    public int? ComicVineId { get; set; }
}

/// <summary>
/// File association from Mylar3.
/// </summary>
public class Mylar3File
{
    public string? IssueId { get; set; }
    public string? ComicId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public DateTime? ImportedDate { get; set; }
}

#endregion

#region Migration Types

/// <summary>
/// Options for Mylar3 migration.
/// </summary>
public class Mylar3MigrationOptions
{
    /// <summary>
    /// Whether to import series data.
    /// </summary>
    public bool ImportSeries { get; set; } = true;

    /// <summary>
    /// Whether to import issues.
    /// </summary>
    public bool ImportIssues { get; set; } = true;

    /// <summary>
    /// Whether to import file associations.
    /// </summary>
    public bool ImportFiles { get; set; } = true;

    /// <summary>
    /// Whether to skip series that already exist (by title match).
    /// </summary>
    public bool SkipExistingSeries { get; set; } = true;

    /// <summary>
    /// Whether to update existing series with Mylar3 data.
    /// </summary>
    public bool UpdateExistingSeries { get; set; } = false;

    /// <summary>
    /// Whether to sync metadata from ComicVine after import.
    /// </summary>
    public bool SyncMetadataAfterImport { get; set; } = true;

    /// <summary>
    /// Maximum series to import (0 = no limit).
    /// </summary>
    public int MaxSeries { get; set; }

    /// <summary>
    /// Whether to skip ignored series in Mylar3.
    /// </summary>
    public bool SkipIgnoredSeries { get; set; } = true;

    /// <summary>
    /// Whether to import wanted status from Mylar3.
    /// </summary>
    public bool ImportWantedStatus { get; set; } = true;

    /// <summary>
    /// Whether to run in dry-run mode (no changes).
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// Result of Mylar3 migration.
/// </summary>
public class Mylar3MigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public bool WasDryRun { get; set; }

    // Series stats
    public int SeriesProcessed { get; set; }
    public int SeriesImported { get; set; }
    public int SeriesUpdated { get; set; }
    public int SeriesSkipped { get; set; }
    public int SeriesFailed { get; set; }

    // Issue stats
    public int IssuesProcessed { get; set; }
    public int IssuesImported { get; set; }
    public int IssuesUpdated { get; set; }
    public int IssuesSkipped { get; set; }
    public int IssuesFailed { get; set; }

    // File stats
    public int FilesProcessed { get; set; }
    public int FilesAssociated { get; set; }
    public int FilesSkipped { get; set; }

    // Metadata stats
    public int MetadataSynced { get; set; }

    /// <summary>
    /// Detailed items for reporting.
    /// </summary>
    public List<Mylar3MigrationItem> Items { get; set; } = new();

    /// <summary>
    /// Warnings during migration.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Individual migration item status.
/// </summary>
public class Mylar3MigrationItem
{
    public required string EntityType { get; set; } // Series, Issue, File
    public string? Mylar3Id { get; set; }
    public string? Mylar3Name { get; set; }
    public int? ShortboxerrId { get; set; }
    public required string Status { get; set; } // Imported, Updated, Skipped, Failed
    public string? Reason { get; set; }
    public string? Error { get; set; }
}

#endregion
