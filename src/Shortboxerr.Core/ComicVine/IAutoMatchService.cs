using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for automatic matching of imported files to ComicVine metadata.
/// Provides auto-matching during import and bulk matching operations.
/// </summary>
public interface IAutoMatchService
{
    /// <summary>
    /// Attempt to auto-match a staged item to ComicVine based on parsed filename info.
    /// Returns match suggestions with confidence scores.
    /// </summary>
    Task<AutoMatchResult> AutoMatchStagedItemAsync(
        StagedItem stagedItem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-match all unmatched series in the library.
    /// Returns a summary of matches and items requiring review.
    /// </summary>
    Task<BulkAutoMatchResult> AutoMatchAllUnmatchedSeriesAsync(
        int? confidenceThreshold = null,
        bool matchImmediately = false,
        IProgress<BulkMatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-match all unmatched editions in the library.
    /// </summary>
    Task<BulkAutoMatchResult> AutoMatchAllUnmatchedEditionsAsync(
        int? confidenceThreshold = null,
        bool matchImmediately = false,
        IProgress<BulkMatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending matches that require manual review.
    /// </summary>
    Task<IReadOnlyList<PendingMatch>> GetPendingMatchesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept a pending match.
    /// </summary>
    Task<bool> AcceptPendingMatchAsync(
        int pendingMatchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a pending match.
    /// </summary>
    Task<bool> RejectPendingMatchAsync(
        int pendingMatchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current auto-match settings.
    /// </summary>
    Task<AutoMatchSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
}

#region Result Types

/// <summary>
/// Result of auto-matching a staged item.
/// </summary>
public class AutoMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// Whether a high-confidence match was found and applied.
    /// </summary>
    public bool AutoMatched { get; set; }
    
    /// <summary>
    /// The matched series ID (if auto-matched or existing match).
    /// </summary>
    public int? MatchedSeriesId { get; set; }
    
    /// <summary>
    /// The matched edition ID (for collections).
    /// </summary>
    public int? MatchedEditionId { get; set; }
    
    /// <summary>
    /// The matched issue ID (for singles).
    /// </summary>
    public int? MatchedIssueId { get; set; }
    
    /// <summary>
    /// Confidence score of the top match.
    /// </summary>
    public int ConfidenceScore { get; set; }
    
    /// <summary>
    /// Whether manual review is recommended.
    /// </summary>
    public bool RequiresReview { get; set; }
    
    /// <summary>
    /// All candidate matches found.
    /// </summary>
    public List<AutoMatchCandidate> Candidates { get; set; } = new();
    
    /// <summary>
    /// Parsed information from the filename.
    /// </summary>
    public ParsedComicInfo? ParsedInfo { get; set; }
}

/// <summary>
/// A candidate match during auto-matching.
/// </summary>
public class AutoMatchCandidate
{
    public int ComicVineId { get; set; }
    public required string Title { get; set; }
    public int? Year { get; set; }
    public string? Publisher { get; set; }
    public int IssueCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public int ConfidenceScore { get; set; }
    public List<string> ConfidenceReasons { get; set; } = new();
    
    /// <summary>
    /// If we have a local series already matched to this ComicVine ID.
    /// </summary>
    public int? ExistingSeriesId { get; set; }
    public string? ExistingSeriesTitle { get; set; }
}

/// <summary>
/// Result of a bulk auto-match operation.
/// </summary>
public class BulkAutoMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// Total items processed.
    /// </summary>
    public int TotalProcessed { get; set; }
    
    /// <summary>
    /// Items auto-matched (above threshold).
    /// </summary>
    public int AutoMatched { get; set; }
    
    /// <summary>
    /// Items queued for manual review.
    /// </summary>
    public int QueuedForReview { get; set; }
    
    /// <summary>
    /// Items that failed to match (no results).
    /// </summary>
    public int NoMatchFound { get; set; }
    
    /// <summary>
    /// Items that encountered errors.
    /// </summary>
    public int Errors { get; set; }
    
    /// <summary>
    /// Details of each match result.
    /// </summary>
    public List<BulkMatchItemResult> Results { get; set; } = new();
}

/// <summary>
/// Result for a single item in a bulk match operation.
/// </summary>
public class BulkMatchItemResult
{
    public int ItemId { get; set; }
    public required string ItemTitle { get; set; }
    public bool Success { get; set; }
    public int? MatchedComicVineId { get; set; }
    public int ConfidenceScore { get; set; }
    public bool RequiresReview { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Progress update during bulk matching.
/// </summary>
public class BulkMatchProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentItem { get; set; } = "";
    public int Matched { get; set; }
    public int RequiresReview { get; set; }
    public int Failed { get; set; }
}

/// <summary>
/// A match pending manual review.
/// </summary>
public class PendingMatch
{
    public int Id { get; set; }
    
    /// <summary>
    /// Type of item (Series, Edition).
    /// </summary>
    public required string ItemType { get; set; }
    
    /// <summary>
    /// Local item ID.
    /// </summary>
    public int ItemId { get; set; }
    
    /// <summary>
    /// Local item title.
    /// </summary>
    public required string ItemTitle { get; set; }
    
    /// <summary>
    /// When the match was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Top candidates for this match.
    /// </summary>
    public List<AutoMatchCandidate> Candidates { get; set; } = new();
}

// Note: AutoMatchSettings is now defined in Shortboxerr.Core.Services.ISettingsService
// This consolidates settings for both ComicVine matching and DDL import matching.

#endregion

