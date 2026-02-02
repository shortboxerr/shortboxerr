using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for handling post-download processing and import handoff.
/// Bridges the DDL download pipeline to the import pipeline.
/// </summary>
public interface IDdlImportService
{
    /// <summary>
    /// Process a completed download and prepare for import.
    /// </summary>
    Task<DdlImportResult> ProcessDownloadAsync(DdlDownloadResult downloadResult, DdlCandidate candidate, DdlImportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify a downloaded file is valid for import.
    /// </summary>
    Task<DdlVerificationResult> VerifyFileAsync(string filePath, DdlCandidate candidate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move a verified file to the staging folder.
    /// </summary>
    Task<DdlStagingResult> MoveToStagingAsync(string sourcePath, DdlCandidate candidate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Auto-match a candidate to existing series/issue in the database.
    /// </summary>
    Task<DdlMatchResult> AutoMatchAsync(DdlCandidate candidate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute import for a staged file (either auto or manual).
    /// </summary>
    Task<DdlImportResult> ExecuteImportAsync(string stagedFilePath, DdlCandidate candidate, DdlMatchResult? match = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get pending imports awaiting manual review.
    /// </summary>
    Task<IReadOnlyList<DdlPendingImport>> GetPendingImportsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Approve a pending import for processing.
    /// </summary>
    Task<DdlImportResult> ApprovePendingImportAsync(string pendingImportId, int? seriesId = null, int? issueId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reject a pending import and optionally delete the file.
    /// </summary>
    Task<bool> RejectPendingImportAsync(string pendingImportId, string reason, bool deleteFile = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for DDL import processing.
/// </summary>
public class DdlImportOptions
{
    /// <summary>
    /// Whether to auto-import when a confident match is found.
    /// Matches Mylar3's auto-import behavior.
    /// </summary>
    public bool AutoImportEnabled { get; set; } = true;
    
    /// <summary>
    /// Minimum match confidence (0-100) required for auto-import.
    /// </summary>
    public int AutoImportMinConfidence { get; set; } = 80;
    
    /// <summary>
    /// Whether to require series match for auto-import.
    /// </summary>
    public bool RequireSeriesMatch { get; set; } = true;
    
    /// <summary>
    /// Whether to require issue match for auto-import (singles only).
    /// </summary>
    public bool RequireIssueMatch { get; set; } = true;
    
    /// <summary>
    /// Custom staging folder path (if null, uses default).
    /// </summary>
    public string? StagingFolderPath { get; set; }
    
    /// <summary>
    /// Whether to delete source file after successful import.
    /// </summary>
    public bool DeleteSourceOnSuccess { get; set; } = true;
    
    /// <summary>
    /// Whether to create history events for import operations.
    /// </summary>
    public bool CreateHistoryEvents { get; set; } = true;
}

/// <summary>
/// Result of DDL import processing.
/// </summary>
public class DdlImportResult
{
    /// <summary>
    /// Unique import identifier.
    /// </summary>
    public required string ImportId { get; init; }
    
    /// <summary>
    /// Whether the import succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Current state of the import.
    /// </summary>
    public DdlImportState State { get; init; }
    
    /// <summary>
    /// Source file path (downloaded file).
    /// </summary>
    public string? SourcePath { get; init; }
    
    /// <summary>
    /// Staging file path (after move to staging).
    /// </summary>
    public string? StagingPath { get; init; }
    
    /// <summary>
    /// Final library path (after import).
    /// </summary>
    public string? LibraryPath { get; init; }
    
    /// <summary>
    /// Matched series ID (if matched).
    /// </summary>
    public int? SeriesId { get; init; }
    
    /// <summary>
    /// Matched series title.
    /// </summary>
    public string? SeriesTitle { get; init; }
    
    /// <summary>
    /// Matched issue ID (if matched, for singles).
    /// </summary>
    public int? IssueId { get; init; }
    
    /// <summary>
    /// Matched issue number.
    /// </summary>
    public decimal? IssueNumber { get; init; }
    
    /// <summary>
    /// Matched edition ID (if matched, for collections).
    /// </summary>
    public int? EditionId { get; init; }
    
    /// <summary>
    /// Created file asset ID.
    /// </summary>
    public int? FileAssetId { get; init; }
    
    /// <summary>
    /// Created history event ID.
    /// </summary>
    public int? HistoryEventId { get; init; }
    
    /// <summary>
    /// Match confidence (0-100).
    /// </summary>
    public int MatchConfidence { get; init; }
    
    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Whether this import is pending manual review.
    /// </summary>
    public bool PendingManualReview { get; init; }
    
    /// <summary>
    /// Pending import ID (if awaiting review).
    /// </summary>
    public string? PendingImportId { get; init; }
    
    /// <summary>
    /// When the import was processed.
    /// </summary>
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Create a successful import result.
    /// </summary>
    public static DdlImportResult Succeeded(string importId, string libraryPath, int? seriesId, string? seriesTitle, int? issueId, decimal? issueNumber, int? fileAssetId, int? historyEventId, int matchConfidence)
    {
        return new DdlImportResult
        {
            ImportId = importId,
            Success = true,
            State = DdlImportState.Completed,
            LibraryPath = libraryPath,
            SeriesId = seriesId,
            SeriesTitle = seriesTitle,
            IssueId = issueId,
            IssueNumber = issueNumber,
            FileAssetId = fileAssetId,
            HistoryEventId = historyEventId,
            MatchConfidence = matchConfidence
        };
    }
    
    /// <summary>
    /// Create a pending review result.
    /// </summary>
    public static DdlImportResult PendingReview(string importId, string pendingImportId, string stagingPath, int matchConfidence)
    {
        return new DdlImportResult
        {
            ImportId = importId,
            Success = false,
            State = DdlImportState.PendingReview,
            StagingPath = stagingPath,
            MatchConfidence = matchConfidence,
            PendingManualReview = true,
            PendingImportId = pendingImportId
        };
    }
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static DdlImportResult Failed(string importId, DdlImportState state, string errorMessage)
    {
        return new DdlImportResult
        {
            ImportId = importId,
            Success = false,
            State = state,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// State of DDL import processing.
/// </summary>
public enum DdlImportState
{
    /// <summary>
    /// Initial state.
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Verifying downloaded file.
    /// </summary>
    Verifying = 1,
    
    /// <summary>
    /// Moving to staging folder.
    /// </summary>
    MovingToStaging = 2,
    
    /// <summary>
    /// Matching to series/issue.
    /// </summary>
    Matching = 3,
    
    /// <summary>
    /// Awaiting manual review.
    /// </summary>
    PendingReview = 4,
    
    /// <summary>
    /// Importing to library.
    /// </summary>
    Importing = 5,
    
    /// <summary>
    /// Import completed successfully.
    /// </summary>
    Completed = 10,
    
    /// <summary>
    /// Verification failed.
    /// </summary>
    VerificationFailed = 20,
    
    /// <summary>
    /// Staging failed.
    /// </summary>
    StagingFailed = 21,
    
    /// <summary>
    /// Matching failed.
    /// </summary>
    MatchingFailed = 22,
    
    /// <summary>
    /// Import failed.
    /// </summary>
    ImportFailed = 23,
    
    /// <summary>
    /// Rejected by user.
    /// </summary>
    Rejected = 30
}

/// <summary>
/// Result of file verification.
/// </summary>
public class DdlVerificationResult
{
    /// <summary>
    /// Whether verification passed.
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// File path that was verified.
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }
    
    /// <summary>
    /// Detected file format.
    /// </summary>
    public string? DetectedFormat { get; init; }
    
    /// <summary>
    /// Whether the format is supported.
    /// </summary>
    public bool FormatSupported { get; init; }
    
    /// <summary>
    /// Error message (if invalid).
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Warnings (non-fatal issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of moving file to staging.
/// </summary>
public class DdlStagingResult
{
    /// <summary>
    /// Whether staging succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Original source path.
    /// </summary>
    public required string SourcePath { get; init; }
    
    /// <summary>
    /// Destination path in staging.
    /// </summary>
    public string? StagingPath { get; init; }
    
    /// <summary>
    /// Final filename in staging.
    /// </summary>
    public string? StagingFilename { get; init; }
    
    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of auto-matching a candidate.
/// </summary>
public class DdlMatchResult
{
    /// <summary>
    /// Whether a match was found.
    /// </summary>
    public bool MatchFound { get; init; }
    
    /// <summary>
    /// Overall match confidence (0-100).
    /// </summary>
    public int Confidence { get; init; }
    
    /// <summary>
    /// Matched series.
    /// </summary>
    public Series? Series { get; init; }
    
    /// <summary>
    /// Matched issue (for singles).
    /// </summary>
    public Issue? Issue { get; init; }
    
    /// <summary>
    /// Matched edition (for collections).
    /// </summary>
    public EditionTitle? Edition { get; init; }
    
    /// <summary>
    /// Whether this is a collection match.
    /// </summary>
    public bool IsCollection { get; init; }
    
    /// <summary>
    /// Match explanation.
    /// </summary>
    public string? Explanation { get; init; }
    
    /// <summary>
    /// Alternative matches (if any).
    /// </summary>
    public IReadOnlyList<DdlMatchResult> Alternatives { get; init; } = Array.Empty<DdlMatchResult>();
    
    /// <summary>
    /// Reasons why confidence was reduced.
    /// </summary>
    public IReadOnlyList<string> ConfidenceReductions { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Create a no-match result.
    /// </summary>
    public static DdlMatchResult NoMatch(string reason)
    {
        return new DdlMatchResult
        {
            MatchFound = false,
            Confidence = 0,
            Explanation = reason
        };
    }
}

/// <summary>
/// A pending import awaiting manual review.
/// </summary>
public class DdlPendingImport
{
    /// <summary>
    /// Unique pending import ID.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Path to file in staging folder.
    /// </summary>
    public required string StagingPath { get; init; }
    
    /// <summary>
    /// Original filename.
    /// </summary>
    public required string Filename { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }
    
    /// <summary>
    /// Original DDL candidate info.
    /// </summary>
    public DdlCandidate? Candidate { get; init; }
    
    /// <summary>
    /// Best match result (if any).
    /// </summary>
    public DdlMatchResult? BestMatch { get; init; }
    
    /// <summary>
    /// Suggested series ID.
    /// </summary>
    public int? SuggestedSeriesId { get; init; }
    
    /// <summary>
    /// Suggested series title.
    /// </summary>
    public string? SuggestedSeriesTitle { get; init; }
    
    /// <summary>
    /// Suggested issue number.
    /// </summary>
    public decimal? SuggestedIssueNumber { get; init; }
    
    /// <summary>
    /// Whether this is a collection.
    /// </summary>
    public bool IsCollection { get; init; }
    
    /// <summary>
    /// When the file was staged.
    /// </summary>
    public DateTime StagedAt { get; init; }
    
    /// <summary>
    /// Reason for requiring manual review.
    /// </summary>
    public string? ReviewReason { get; init; }
}

