namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a story arc that an issue belongs to.
/// Links issues to their ComicVine story arcs.
/// </summary>
public class IssueStoryArc
{
    public int Id { get; set; }
    
    /// <summary>
    /// The issue this story arc reference belongs to.
    /// </summary>
    public int IssueId { get; set; }
    
    /// <summary>
    /// ComicVine story arc ID.
    /// </summary>
    public int ComicVineStoryArcId { get; set; }
    
    /// <summary>
    /// Story arc name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// URL to the story arc on ComicVine.
    /// </summary>
    public string? ComicVineUrl { get; set; }
    
    /// <summary>
    /// Position of this issue within the story arc (if known).
    /// </summary>
    public int? Position { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Issue? Issue { get; set; }
}

