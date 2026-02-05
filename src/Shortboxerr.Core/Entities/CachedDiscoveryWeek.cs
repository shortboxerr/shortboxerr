namespace Shortboxerr.Core.Entities;

/// <summary>
/// Persisted cache of ComicVine discovery data for a specific week.
/// This ensures pull list data survives application restarts.
/// </summary>
public class CachedDiscoveryWeek
{
    public int Id { get; set; }
    
    /// <summary>
    /// The start date of the week (Sunday).
    /// </summary>
    public DateTime WeekStart { get; set; }
    
    /// <summary>
    /// JSON-serialized list of ComicVine issues for this week.
    /// </summary>
    public required string IssuesJson { get; set; }
    
    /// <summary>
    /// When this cache entry was last refreshed from ComicVine.
    /// </summary>
    public DateTime LastRefreshed { get; set; }
    
    /// <summary>
    /// When this cache entry expires and should be refreshed.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Number of issues in this cache entry.
    /// </summary>
    public int IssueCount { get; set; }
    
    /// <summary>
    /// Cache tier at time of caching (for tracking purposes).
    /// </summary>
    public int CacheTier { get; set; }
}
