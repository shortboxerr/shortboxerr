namespace Shortboxerr.Core.Entities;

/// <summary>
/// Records metadata refresh events for tracking and auditing.
/// </summary>
public class MetadataRefreshEvent
{
    public int Id { get; set; }
    
    /// <summary>
    /// Type of item refreshed (Series, Issue, Edition).
    /// </summary>
    public required string ItemType { get; set; }
    
    /// <summary>
    /// ID of the item that was refreshed.
    /// </summary>
    public int ItemId { get; set; }
    
    /// <summary>
    /// Title of the item (denormalized for display).
    /// </summary>
    public required string ItemTitle { get; set; }
    
    /// <summary>
    /// Whether the refresh was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if refresh failed.
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Whether any metadata was actually changed.
    /// </summary>
    public bool MetadataChanged { get; set; }
    
    /// <summary>
    /// JSON array of field names that were updated.
    /// </summary>
    public string? UpdatedFieldsJson { get; set; }
    
    /// <summary>
    /// Number of new issues discovered (for series refresh).
    /// </summary>
    public int NewIssuesDiscovered { get; set; }
    
    /// <summary>
    /// Source of the refresh (Manual, Scheduled, Import).
    /// </summary>
    public required string Source { get; set; }
    
    /// <summary>
    /// When the refresh occurred.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

