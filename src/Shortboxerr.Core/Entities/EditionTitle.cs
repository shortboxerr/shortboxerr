namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a collected edition (TPB, hardcover, omnibus, etc.).
/// Collections are first-class citizens in Shortboxerr.
/// </summary>
public class EditionTitle
{
    public int Id { get; set; }
    
    /// <summary>
    /// Parent series ID (can be null for standalone collections).
    /// </summary>
    public int? SeriesId { get; set; }
    
    /// <summary>
    /// Title of the collected edition.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Sortable title.
    /// </summary>
    public string? SortTitle { get; set; }
    
    /// <summary>
    /// Type of collected edition.
    /// </summary>
    public EditionType EditionType { get; set; } = EditionType.TradesPaperback;
    
    /// <summary>
    /// Volume number in a TPB series (e.g., Vol. 1, Vol. 2).
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// ISBN if available.
    /// </summary>
    public string? Isbn { get; set; }
    
    /// <summary>
    /// Publisher name.
    /// </summary>
    public string? Publisher { get; set; }
    
    /// <summary>
    /// Release/publication date.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }
    
    /// <summary>
    /// Page count.
    /// </summary>
    public int? PageCount { get; set; }
    
    /// <summary>
    /// External ID from metadata provider.
    /// </summary>
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Source of the external ID.
    /// </summary>
    public string? ExternalSource { get; set; }
    
    /// <summary>
    /// Overview/description.
    /// </summary>
    public string? Overview { get; set; }
    
    /// <summary>
    /// Whether this edition is monitored for acquisition.
    /// </summary>
    public bool Monitored { get; set; }
    
    /// <summary>
    /// Whether the edition has been acquired.
    /// </summary>
    public bool HasFile { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public Series? Series { get; set; }
    public FileAsset? File { get; set; }
    public ICollection<EditionContent> Contents { get; set; } = new List<EditionContent>();
}

public enum EditionType
{
    TradesPaperback = 0,
    Hardcover = 1,
    Omnibus = 2,
    Compendium = 3,
    AbsoluteEdition = 4,
    DeluxeEdition = 5,
    Other = 99
}

