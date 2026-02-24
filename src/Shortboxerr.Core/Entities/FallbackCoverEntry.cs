namespace Shortboxerr.Core.Entities;

/// <summary>
/// Tracks issues that have fallback covers (LOCG instead of ComicVine).
/// Used to periodically check if ComicVine has caught up with cover images.
/// </summary>
public class FallbackCoverEntry
{
    public int Id { get; set; }
    
    /// <summary>
    /// ComicVine issue ID.
    /// </summary>
    public int ComicVineIssueId { get; set; }
    
    /// <summary>
    /// ComicVine volume ID.
    /// </summary>
    public int ComicVineVolumeId { get; set; }
    
    /// <summary>
    /// Series name for fuzzy matching.
    /// </summary>
    public required string SeriesName { get; set; }
    
    /// <summary>
    /// Issue number (as string).
    /// </summary>
    public required string IssueNumber { get; set; }
    
    /// <summary>
    /// The fallback cover URL currently in use.
    /// </summary>
    public required string FallbackCoverUrl { get; set; }
    
    /// <summary>
    /// Source of the fallback cover.
    /// </summary>
    public required string FallbackSource { get; set; }
    
    /// <summary>
    /// When this entry was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When ComicVine was last checked for an updated cover.
    /// </summary>
    public DateTime? LastChecked { get; set; }
    
    /// <summary>
    /// The week start date this issue belongs to.
    /// </summary>
    public DateTime WeekStart { get; set; }
}
