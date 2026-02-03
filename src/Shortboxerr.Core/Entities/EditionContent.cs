namespace Shortboxerr.Core.Entities;

/// <summary>
/// Maps which issues are contained in a collected edition.
/// This enables the "edition satisfies singles" feature.
/// </summary>
public class EditionContent
{
    public int Id { get; set; }
    
    /// <summary>
    /// The edition that contains this issue.
    /// </summary>
    public int EditionTitleId { get; set; }
    
    /// <summary>
    /// The issue that is contained (can be null for non-tracked issues).
    /// </summary>
    public int? IssueId { get; set; }
    
    /// <summary>
    /// Series ID for the contained issue (denormalized for queries).
    /// </summary>
    public int? SeriesId { get; set; }
    
    /// <summary>
    /// Issue number (denormalized, useful when IssueId is null).
    /// </summary>
    public decimal? IssueNumber { get; set; }
    
    /// <summary>
    /// Order/position in the collected edition.
    /// </summary>
    public int SortOrder { get; set; }
    
    // Navigation properties
    public EditionTitle? EditionTitle { get; set; }
    public Issue? Issue { get; set; }
    public Series? Series { get; set; }
}



