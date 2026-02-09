namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Service for handling completed NZB downloads and importing them.
/// Monitors download clients for completed downloads and processes them for import.
/// </summary>
public interface INzbImportService
{
    /// <summary>
    /// Scans for newly completed downloads from configured download clients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of completed downloads ready for processing</returns>
    Task<IReadOnlyList<NzbCompletedDownload>> GetCompletedDownloadsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes a completed download - extracts files, moves to staging, and triggers import.
    /// </summary>
    /// <param name="download">The completed download to process</param>
    /// <param name="options">Processing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the import processing</returns>
    Task<NzbImportResult> ProcessCompletedDownloadAsync(NzbCompletedDownload download, NzbImportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes all pending completed downloads.
    /// </summary>
    /// <param name="options">Processing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of all import operations</returns>
    Task<IReadOnlyList<NzbImportResult>> ProcessAllCompletedAsync(NzbImportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a download as processed (to avoid reprocessing).
    /// </summary>
    Task MarkAsProcessedAsync(string downloadId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a download has already been processed.
    /// </summary>
    Task<bool> IsProcessedAsync(string downloadId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the list of processed download IDs.
    /// </summary>
    Task<IReadOnlySet<string>> GetProcessedDownloadIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a completed NZB download ready for import.
/// </summary>
public class NzbCompletedDownload
{
    /// <summary>
    /// Unique identifier from the download client.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Name/title of the download.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Path where files were downloaded.
    /// </summary>
    public required string DownloadPath { get; init; }
    
    /// <summary>
    /// When the download completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }
    
    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long TotalBytes { get; init; }
    
    /// <summary>
    /// Category assigned in the download client.
    /// </summary>
    public string? Category { get; init; }
    
    /// <summary>
    /// Name of the download client (SABnzbd, NZBGet).
    /// </summary>
    public required string ClientName { get; init; }
    
    /// <summary>
    /// Provider ID from the provider system (if available).
    /// </summary>
    public int? ProviderId { get; init; }
    
    /// <summary>
    /// Original NZB status from the download client.
    /// </summary>
    public NzbDownloadStatus? OriginalStatus { get; init; }
}

/// <summary>
/// Options for processing NZB imports.
/// </summary>
public class NzbImportOptions
{
    /// <summary>
    /// Whether to automatically import files that match with high confidence.
    /// </summary>
    public bool AutoImport { get; set; } = true;
    
    /// <summary>
    /// Minimum confidence score required for auto-import (0-100).
    /// </summary>
    public int MinAutoImportConfidence { get; set; } = 80;
    
    /// <summary>
    /// Whether to delete empty directories after import.
    /// </summary>
    public bool CleanupEmptyDirectories { get; set; } = true;
    
    /// <summary>
    /// Whether to remove the download from client history after successful import.
    /// </summary>
    public bool RemoveFromHistory { get; set; } = false;
    
    /// <summary>
    /// Whether to extract archives (RAR, ZIP) before processing.
    /// </summary>
    public bool ExtractArchives { get; set; } = true;
    
    /// <summary>
    /// Categories to process (empty = all categories).
    /// </summary>
    public List<string> Categories { get; set; } = new();
}

/// <summary>
/// Result of processing an NZB import.
/// </summary>
public class NzbImportResult
{
    /// <summary>
    /// Unique identifier for this import operation.
    /// </summary>
    public required string ImportId { get; init; }
    
    /// <summary>
    /// The download ID that was processed.
    /// </summary>
    public required string DownloadId { get; init; }
    
    /// <summary>
    /// Download name/title.
    /// </summary>
    public required string DownloadName { get; init; }
    
    /// <summary>
    /// Whether the overall import was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Current state of the import.
    /// </summary>
    public NzbImportState State { get; init; }
    
    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Files that were found and processed.
    /// </summary>
    public List<NzbImportedFile> ImportedFiles { get; init; } = new();
    
    /// <summary>
    /// Files that were skipped (not comic files).
    /// </summary>
    public List<string> SkippedFiles { get; init; } = new();
    
    /// <summary>
    /// Archives that were extracted.
    /// </summary>
    public List<string> ExtractedArchives { get; init; } = new();
    
    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// History event ID if recorded.
    /// </summary>
    public int? HistoryEventId { get; init; }
    
    public static NzbImportResult Failed(string importId, string downloadId, string downloadName, NzbImportState state, string error)
    {
        return new NzbImportResult
        {
            ImportId = importId,
            DownloadId = downloadId,
            DownloadName = downloadName,
            Success = false,
            State = state,
            ErrorMessage = error
        };
    }
    
    public static NzbImportResult Ok(string importId, string downloadId, string downloadName, List<NzbImportedFile> files)
    {
        return new NzbImportResult
        {
            ImportId = importId,
            DownloadId = downloadId,
            DownloadName = downloadName,
            Success = true,
            State = NzbImportState.Completed,
            ImportedFiles = files
        };
    }
}

/// <summary>
/// Information about a file that was imported.
/// </summary>
public class NzbImportedFile
{
    /// <summary>
    /// Original path in download directory.
    /// </summary>
    public required string SourcePath { get; init; }
    
    /// <summary>
    /// Path in staging folder (if moved to staging).
    /// </summary>
    public string? StagingPath { get; init; }
    
    /// <summary>
    /// Final destination path (if auto-imported).
    /// </summary>
    public string? DestinationPath { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }
    
    /// <summary>
    /// File format (CBZ, CBR, PDF).
    /// </summary>
    public string? Format { get; init; }
    
    /// <summary>
    /// Parsed series title.
    /// </summary>
    public string? ParsedSeriesTitle { get; init; }
    
    /// <summary>
    /// Parsed issue number.
    /// </summary>
    public decimal? ParsedIssueNumber { get; init; }
    
    /// <summary>
    /// Match confidence (0-100).
    /// </summary>
    public int MatchConfidence { get; init; }
    
    /// <summary>
    /// Matched series ID (if matched).
    /// </summary>
    public int? MatchedSeriesId { get; init; }
    
    /// <summary>
    /// Matched issue ID (if matched).
    /// </summary>
    public int? MatchedIssueId { get; init; }
    
    /// <summary>
    /// Whether the file was auto-imported or queued for review.
    /// </summary>
    public bool WasAutoImported { get; init; }
    
    /// <summary>
    /// File asset ID (if imported).
    /// </summary>
    public int? FileAssetId { get; init; }
}

/// <summary>
/// States for NZB import processing.
/// </summary>
public enum NzbImportState
{
    /// <summary>
    /// Download found, not yet processed.
    /// </summary>
    Pending,
    
    /// <summary>
    /// Extracting archive files.
    /// </summary>
    Extracting,
    
    /// <summary>
    /// Scanning for comic files.
    /// </summary>
    Scanning,
    
    /// <summary>
    /// Moving files to staging.
    /// </summary>
    Staging,
    
    /// <summary>
    /// Auto-importing matched files.
    /// </summary>
    Importing,
    
    /// <summary>
    /// Successfully completed.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Completed but files queued for manual review.
    /// </summary>
    CompletedPendingReview,
    
    /// <summary>
    /// Failed - no comic files found.
    /// </summary>
    NoFilesFound,
    
    /// <summary>
    /// Failed - extraction error.
    /// </summary>
    ExtractionFailed,
    
    /// <summary>
    /// Failed - general error.
    /// </summary>
    Failed
}
