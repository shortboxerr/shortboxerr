namespace Shortboxerr.Core.Models;

/// <summary>
/// Represents a release candidate for evaluation by the DecisionEngine.
/// </summary>
public class Candidate
{
    /// <summary>
    /// Unique identifier for this candidate.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Original release title as found.
    /// </summary>
    public required string ReleaseTitle { get; init; }
    
    /// <summary>
    /// Source where this candidate was found.
    /// </summary>
    public required string Source { get; init; }
    
    /// <summary>
    /// Source priority (lower = better).
    /// </summary>
    public int SourcePriority { get; init; }
    
    /// <summary>
    /// Parsed series title.
    /// </summary>
    public string? SeriesTitle { get; set; }
    
    /// <summary>
    /// Parsed issue number (for singles).
    /// </summary>
    public decimal? IssueNumber { get; set; }
    
    /// <summary>
    /// Parsed volume number.
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// Parsed year.
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// File format (cbz, cbr, pdf).
    /// </summary>
    public string? Format { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long? Size { get; set; }
    
    /// <summary>
    /// Whether this is a collection (TPB/omnibus).
    /// </summary>
    public bool IsCollection { get; set; }
    
    /// <summary>
    /// Edition type indicator (TPB, HC, Omnibus, etc.).
    /// </summary>
    public string? EditionType { get; set; }
    
    /// <summary>
    /// Download URL or identifier.
    /// </summary>
    public string? DownloadUrl { get; set; }
    
    /// <summary>
    /// When the candidate was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional tags or info extracted from the release.
    /// </summary>
    public List<string> Tags { get; init; } = new();
}

/// <summary>
/// Result of evaluating a candidate through the DecisionEngine.
/// </summary>
public class CandidateEvaluation
{
    /// <summary>
    /// The evaluated candidate.
    /// </summary>
    public required Candidate Candidate { get; init; }
    
    /// <summary>
    /// Whether the candidate was accepted.
    /// </summary>
    public bool Accepted { get; init; }
    
    /// <summary>
    /// Final score (higher = better). Only meaningful if accepted.
    /// </summary>
    public int Score { get; init; }
    
    /// <summary>
    /// Rejection reason if not accepted.
    /// </summary>
    public RejectionReason? RejectionReason { get; init; }
    
    /// <summary>
    /// Detailed explanation of the decision.
    /// </summary>
    public required DecisionExplanation Explanation { get; init; }
}

/// <summary>
/// Reasons why a candidate may be rejected.
/// </summary>
public enum RejectionReason
{
    None = 0,
    
    // Format rejections
    UnsupportedFormat = 10,
    FormatNotPreferred = 11,
    
    // Size rejections
    TooSmall = 20,
    TooLarge = 21,
    
    // Content rejections
    BannedWordFound = 30,
    MissingRequiredWord = 31,
    
    // Match rejections
    SeriesMismatch = 40,
    IssueMismatch = 41,
    YearMismatch = 42,
    
    // Quality rejections  
    QualityTooLow = 50,
    DuplicateExists = 51,
    BetterVersionExists = 52,
    
    // Source rejections
    SourceDisabled = 60,
    SourceNotTrusted = 61,
    
    // Other
    ManuallyRejected = 90,
    Unknown = 99
}

/// <summary>
/// Detailed explanation of how a decision was made.
/// </summary>
public class DecisionExplanation
{
    /// <summary>
    /// Individual scoring factors that contributed to the final score.
    /// </summary>
    public List<ScoringFactor> ScoringFactors { get; init; } = new();
    
    /// <summary>
    /// Checks that were applied and their results.
    /// </summary>
    public List<CheckResult> Checks { get; init; } = new();
    
    /// <summary>
    /// Human-readable summary of the decision.
    /// </summary>
    public required string Summary { get; init; }
    
    /// <summary>
    /// Total score before any penalties.
    /// </summary>
    public int BaseScore { get; init; }
    
    /// <summary>
    /// Total penalties applied.
    /// </summary>
    public int Penalties { get; init; }
    
    /// <summary>
    /// Final computed score.
    /// </summary>
    public int FinalScore { get; init; }
}

/// <summary>
/// A single factor that contributed to the score.
/// </summary>
public record ScoringFactor(string Name, int Points, string Reason);

/// <summary>
/// Result of a single check in the decision process.
/// </summary>
public record CheckResult(string CheckName, bool Passed, string Details);



