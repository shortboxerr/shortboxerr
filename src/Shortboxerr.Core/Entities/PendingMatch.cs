namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a pending ComicVine match requiring manual review.
/// </summary>
public class PendingMatch
{
    public int Id { get; set; }
    
    /// <summary>
    /// Type of item being matched (Series, Edition).
    /// </summary>
    public required string ItemType { get; set; }
    
    /// <summary>
    /// ID of the local item.
    /// </summary>
    public int ItemId { get; set; }
    
    /// <summary>
    /// Title of the local item (denormalized for display).
    /// </summary>
    public required string ItemTitle { get; set; }
    
    /// <summary>
    /// JSON-serialized list of match candidates.
    /// </summary>
    public string CandidatesJson { get; set; } = "[]";
    
    /// <summary>
    /// Top confidence score from candidates.
    /// </summary>
    public int TopConfidenceScore { get; set; }
    
    /// <summary>
    /// Status of the pending match.
    /// </summary>
    public PendingMatchStatus Status { get; set; } = PendingMatchStatus.Pending;
    
    /// <summary>
    /// ComicVine ID that was selected (when accepted).
    /// </summary>
    public int? SelectedComicVineId { get; set; }
    
    /// <summary>
    /// When the pending match was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the pending match was resolved (accepted/rejected).
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
}

public enum PendingMatchStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

