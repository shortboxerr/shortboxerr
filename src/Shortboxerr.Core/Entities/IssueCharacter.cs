namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a character appearing in an issue.
/// Links issues to ComicVine character data.
/// </summary>
public class IssueCharacter
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the issue.
    /// </summary>
    public int IssueId { get; set; }
    
    /// <summary>
    /// Navigation property to the issue.
    /// </summary>
    public Issue? Issue { get; set; }
    
    /// <summary>
    /// ComicVine character ID.
    /// </summary>
    public int ComicVineCharacterId { get; set; }
    
    /// <summary>
    /// Character name (e.g., "Batman", "Spider-Man").
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Character's real name if known (e.g., "Bruce Wayne", "Peter Parker").
    /// </summary>
    public string? RealName { get; set; }
    
    /// <summary>
    /// ComicVine URL for the character.
    /// </summary>
    public string? ComicVineUrl { get; set; }
    
    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
