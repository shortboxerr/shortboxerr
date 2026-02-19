namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a variant cover for a comic issue.
/// </summary>
public class VariantCoverEntity
{
    /// <summary>
    /// Internal database ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Parent issue ID.
    /// </summary>
    public int IssueId { get; set; }

    /// <summary>
    /// ComicVine image ID.
    /// </summary>
    public int ComicVineImageId { get; set; }

    /// <summary>
    /// Original image URL.
    /// </summary>
    public string ImageUrl { get; set; } = "";

    /// <summary>
    /// Caption/description from ComicVine.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Image tags from ComicVine.
    /// </summary>
    public string? ImageTags { get; set; }

    /// <summary>
    /// Detected variant type (e.g., "Variant", "1:25 Incentive", "SDCC Exclusive").
    /// </summary>
    public string? VariantType { get; set; }

    /// <summary>
    /// Whether this is the main/primary cover (not a variant).
    /// </summary>
    public bool IsPrimaryCover { get; set; }

    /// <summary>
    /// Whether this is the user's preferred cover for display.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <summary>
    /// When this variant cover was detected/added.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Issue? Issue { get; set; }
}
