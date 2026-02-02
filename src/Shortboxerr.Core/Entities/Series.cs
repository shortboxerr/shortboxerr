namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a comic book series (e.g., "Amazing Spider-Man", "Batman").
/// </summary>
public class Series
{
    public int Id { get; set; }
    
    /// <summary>
    /// Display title of the series.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Sortable title (e.g., "Amazing Spider-Man, The").
    /// </summary>
    public string? SortTitle { get; set; }
    
    /// <summary>
    /// Publisher name (e.g., "Marvel", "DC Comics").
    /// </summary>
    public string? Publisher { get; set; }
    
    /// <summary>
    /// Year the series started.
    /// </summary>
    public int? StartYear { get; set; }
    
    /// <summary>
    /// Year the series ended (null if ongoing).
    /// </summary>
    public int? EndYear { get; set; }
    
    /// <summary>
    /// Series status.
    /// </summary>
    public SeriesStatus Status { get; set; } = SeriesStatus.Continuing;
    
    /// <summary>
    /// Path to the series folder on disk.
    /// </summary>
    public string? Path { get; set; }
    
    /// <summary>
    /// External ID from metadata provider (e.g., ComicVine).
    /// </summary>
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Source of the external ID.
    /// </summary>
    public string? ExternalSource { get; set; }
    
    /// <summary>
    /// Overview/description of the series.
    /// </summary>
    public string? Overview { get; set; }
    
    /// <summary>
    /// Whether the series is monitored for new releases.
    /// </summary>
    public bool Monitored { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    public ICollection<EditionTitle> Editions { get; set; } = new List<EditionTitle>();
}

public enum SeriesStatus
{
    Continuing = 0,
    Ended = 1,
    Hiatus = 2
}

