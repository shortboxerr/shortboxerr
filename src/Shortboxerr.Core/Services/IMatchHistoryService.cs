using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for logging and querying auto-match history.
/// </summary>
public interface IMatchHistoryService
{
    /// <summary>
    /// Log a match decision.
    /// </summary>
    Task<MatchHistory> LogMatchAsync(
        DdlCandidate candidate,
        DdlMatchResult result,
        MatchOutcome outcome,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a match record with user verification.
    /// </summary>
    Task<MatchHistory?> VerifyMatchAsync(
        int matchHistoryId,
        bool isCorrect,
        int? correctedSeriesId = null,
        int? correctedIssueId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get match history with filtering and pagination.
    /// </summary>
    Task<MatchHistoryQueryResult> GetHistoryAsync(
        MatchHistoryQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get match accuracy statistics.
    /// </summary>
    Task<MatchAccuracyStats> GetAccuracyStatsAsync(
        int? seriesId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get series with frequent mismatches.
    /// </summary>
    Task<IReadOnlyList<SeriesMismatchSummary>> GetProblematicSeriesAsync(
        int minMismatches = 2,
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for match history.
/// </summary>
public class MatchHistoryQuery
{
    public int? SeriesId { get; set; }
    public MatchOutcome? Outcome { get; set; }
    public bool? RequiredReview { get; set; }
    public bool? UserVerified { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public MatchHistorySortBy SortBy { get; set; } = MatchHistorySortBy.Timestamp;
    public bool SortDescending { get; set; } = true;
}

public enum MatchHistorySortBy
{
    Timestamp,
    ConfidenceScore,
    SeriesTitle,
    Outcome
}

/// <summary>
/// Result of a match history query.
/// </summary>
public class MatchHistoryQueryResult
{
    public IReadOnlyList<MatchHistory> Records { get; init; } = Array.Empty<MatchHistory>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Match accuracy statistics.
/// </summary>
public class MatchAccuracyStats
{
    public int TotalMatches { get; init; }
    public int AutoImported { get; init; }
    public int PendingReview { get; init; }
    public int ManuallyApproved { get; init; }
    public int ManuallyRejected { get; init; }
    public int ManuallyCorrected { get; init; }
    public int NoMatchFound { get; init; }
    
    public int VerifiedCorrect { get; init; }
    public int VerifiedIncorrect { get; init; }
    public int Unverified { get; init; }
    
    public double AccuracyRate => 
        (VerifiedCorrect + VerifiedIncorrect) > 0 
            ? (double)VerifiedCorrect / (VerifiedCorrect + VerifiedIncorrect) * 100 
            : 0;
    
    public double AutoImportAccuracy { get; init; }
    public double AverageConfidence { get; init; }
    
    public DateTime? OldestRecord { get; init; }
    public DateTime? NewestRecord { get; init; }
}

/// <summary>
/// Summary of mismatch frequency for a series.
/// </summary>
public class SeriesMismatchSummary
{
    public int SeriesId { get; init; }
    public string SeriesTitle { get; init; } = string.Empty;
    public int TotalMatches { get; init; }
    public int Mismatches { get; init; }
    public double MismatchRate => TotalMatches > 0 ? (double)Mismatches / TotalMatches * 100 : 0;
    public DateTime LastMismatch { get; init; }
}
