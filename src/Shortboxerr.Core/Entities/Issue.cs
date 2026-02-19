namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a single issue of a comic series.
/// </summary>
public class Issue
{
    public int Id { get; set; }
    
    /// <summary>
    /// Parent series ID.
    /// </summary>
    public int SeriesId { get; set; }
    
    /// <summary>
    /// Issue number (can be decimal for .1 issues).
    /// </summary>
    public decimal IssueNumber { get; set; }
    
    /// <summary>
    /// Issue number as string (handles specials like "½", "Annual 1", etc.)
    /// </summary>
    public string? IssueNumberText { get; set; }
    
    /// <summary>
    /// Display title (e.g., "The Death of Gwen Stacy").
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Cover date or release date.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }
    
    /// <summary>
    /// Store date (actual release date, different from cover date).
    /// </summary>
    public DateTime? StoreDate { get; set; }
    
    /// <summary>
    /// External ID from metadata provider.
    /// </summary>
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Source of the external ID.
    /// </summary>
    public string? ExternalSource { get; set; }
    
    /// <summary>
    /// Overview/description of the issue.
    /// </summary>
    public string? Overview { get; set; }
    
    /// <summary>
    /// Whether this issue is monitored for acquisition.
    /// </summary>
    public bool Monitored { get; set; } = true;
    
    /// <summary>
    /// Whether the issue has been acquired.
    /// </summary>
    public bool HasFile { get; set; }
    
    /// <summary>
    /// Issue status for pull list and acquisition tracking.
    /// </summary>
    public IssueStatus Status { get; set; } = IssueStatus.Wanted;
    
    /// <summary>
    /// Whether this issue is satisfied by a collected edition.
    /// </summary>
    public bool SatisfiedByEdition { get; set; }
    
    #region ComicVine Metadata
    
    /// <summary>
    /// ComicVine issue ID.
    /// </summary>
    public int? ComicVineId { get; set; }
    
    /// <summary>
    /// Cover date from ComicVine (may differ from store date).
    /// </summary>
    public DateTime? CoverDate { get; set; }
    
    /// <summary>
    /// URL to cover image.
    /// </summary>
    public string? CoverImageUrl { get; set; }
    
    /// <summary>
    /// Link to ComicVine page.
    /// </summary>
    public string? ComicVineUrl { get; set; }
    
    /// <summary>
    /// When metadata was last refreshed from ComicVine.
    /// </summary>
    public DateTime? MetadataLastRefreshed { get; set; }
    
    #endregion
    
    /// <summary>
    /// Whether this is an annual (e.g., "Annual 1", "Annual 2020").
    /// </summary>
    public bool IsAnnual { get; set; }
    
    /// <summary>
    /// Whether this is a special issue (one-shot, special edition, etc.).
    /// </summary>
    public bool IsSpecial { get; set; }
    
    /// <summary>
    /// Type of special issue if IsSpecial is true.
    /// </summary>
    public string? SpecialType { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    #region Auto-Search Tracking
    
    /// <summary>
    /// When this issue was last searched for automatically.
    /// </summary>
    public DateTime? LastSearchedAt { get; set; }
    
    /// <summary>
    /// Number of search attempts for this issue.
    /// </summary>
    public int SearchAttempts { get; set; }
    
    /// <summary>
    /// Last error encountered during search (if any).
    /// </summary>
    public string? LastSearchError { get; set; }
    
    #endregion
    
    // Navigation properties
    public Series? Series { get; set; }
    public FileAsset? File { get; set; }
    public ICollection<IssueStoryArc> StoryArcs { get; set; } = new List<IssueStoryArc>();
    public ICollection<VariantCoverEntity> VariantCovers { get; set; } = new List<VariantCoverEntity>();
}

/// <summary>
/// Status of an issue in the acquisition workflow.
/// </summary>
public enum IssueStatus
{
    /// <summary>
    /// Issue is wanted and being searched for.
    /// </summary>
    Wanted = 0,
    
    /// <summary>
    /// Issue has been downloaded/owned.
    /// </summary>
    Owned = 1,
    
    /// <summary>
    /// Issue is being downloaded.
    /// </summary>
    Downloading = 2,
    
    /// <summary>
    /// Issue was intentionally skipped by user.
    /// </summary>
    Skipped = 3,
    
    /// <summary>
    /// Issue is missing but not being searched (low priority).
    /// </summary>
    Missing = 4,
    
    /// <summary>
    /// Issue is in staging awaiting import.
    /// </summary>
    Staged = 5
}
