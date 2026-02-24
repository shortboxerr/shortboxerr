namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a team appearing in an issue.
/// Links issues to ComicVine team data.
/// </summary>
public class IssueTeam
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
    /// ComicVine team ID.
    /// </summary>
    public int ComicVineTeamId { get; set; }
    
    /// <summary>
    /// Team name (e.g., "Avengers", "X-Men", "Justice League").
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// ComicVine URL for the team.
    /// </summary>
    public string? ComicVineUrl { get; set; }
    
    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
