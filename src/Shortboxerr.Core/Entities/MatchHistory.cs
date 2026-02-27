namespace Shortboxerr.Core.Entities;

/// <summary>
/// Records detailed auto-matching decisions for auditing and accuracy tracking.
/// </summary>
public class MatchHistory
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique identifier for this match attempt (corresponds to DdlCandidate.Id).
    /// </summary>
    public required string MatchId { get; set; }
    
    /// <summary>
    /// The original release title that was matched.
    /// </summary>
    public required string ReleaseTitle { get; set; }
    
    /// <summary>
    /// Source site the release came from.
    /// </summary>
    public string? SourceSite { get; set; }
    
    /// <summary>
    /// Parsed series title from the release.
    /// </summary>
    public string? ParsedSeriesTitle { get; set; }
    
    /// <summary>
    /// Parsed issue number from the release.
    /// </summary>
    public string? ParsedIssueNumber { get; set; }
    
    /// <summary>
    /// Parsed year from the release.
    /// </summary>
    public int? ParsedYear { get; set; }
    
    /// <summary>
    /// Parsed publisher from the release.
    /// </summary>
    public string? ParsedPublisher { get; set; }
    
    /// <summary>
    /// The outcome of this match attempt.
    /// </summary>
    public MatchOutcome Outcome { get; set; }
    
    /// <summary>
    /// Whether a match was found.
    /// </summary>
    public bool MatchFound { get; set; }
    
    /// <summary>
    /// Final confidence score (0-100).
    /// </summary>
    public int ConfidenceScore { get; set; }
    
    /// <summary>
    /// The matched series ID (if any).
    /// </summary>
    public int? MatchedSeriesId { get; set; }
    
    /// <summary>
    /// The matched series title for display.
    /// </summary>
    public string? MatchedSeriesTitle { get; set; }
    
    /// <summary>
    /// The matched issue ID (if any).
    /// </summary>
    public int? MatchedIssueId { get; set; }
    
    /// <summary>
    /// The matched issue number for display.
    /// </summary>
    public string? MatchedIssueNumber { get; set; }
    
    /// <summary>
    /// Whether this was flagged as the first issue for the series.
    /// </summary>
    public bool WasFirstIssue { get; set; }
    
    /// <summary>
    /// Whether this required manual review.
    /// </summary>
    public bool RequiredManualReview { get; set; }
    
    /// <summary>
    /// Reason for requiring manual review (if any).
    /// </summary>
    public string? ReviewReason { get; set; }
    
    /// <summary>
    /// Detailed explanation of matching decision.
    /// </summary>
    public string? Explanation { get; set; }
    
    /// <summary>
    /// JSON containing the confidence breakdown details.
    /// </summary>
    public string? ScoreBreakdownJson { get; set; }
    
    /// <summary>
    /// JSON containing confidence reductions applied.
    /// </summary>
    public string? ConfidenceReductionsJson { get; set; }
    
    /// <summary>
    /// Whether user later marked this match as correct.
    /// Null = not yet verified, true = correct, false = incorrect.
    /// </summary>
    public bool? UserVerified { get; set; }
    
    /// <summary>
    /// If user marked as incorrect, what was the correct series ID.
    /// </summary>
    public int? CorrectedSeriesId { get; set; }
    
    /// <summary>
    /// If user marked as incorrect, what was the correct issue ID.
    /// </summary>
    public int? CorrectedIssueId { get; set; }
    
    /// <summary>
    /// Timestamp of the match attempt.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when user verified the match (if any).
    /// </summary>
    public DateTime? VerifiedAt { get; set; }
    
    // Navigation properties
    public Series? MatchedSeries { get; set; }
    public Issue? MatchedIssue { get; set; }
    public Series? CorrectedSeries { get; set; }
    public Issue? CorrectedIssue { get; set; }
}

/// <summary>
/// Outcome of a match attempt.
/// </summary>
public enum MatchOutcome
{
    /// <summary>No match found at all.</summary>
    NoMatch = 0,
    
    /// <summary>Match found and auto-imported.</summary>
    AutoImported = 1,
    
    /// <summary>Match found but queued for manual review.</summary>
    PendingReview = 2,
    
    /// <summary>Match found, user approved and imported.</summary>
    ManuallyApproved = 3,
    
    /// <summary>Match found, user rejected.</summary>
    ManuallyRejected = 4,
    
    /// <summary>Match found, user corrected to different series/issue.</summary>
    ManuallyCorrected = 5
}
