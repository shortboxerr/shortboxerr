namespace Shortboxerr.Core.Entities;

/// <summary>
/// Represents a file on disk (CBZ, CBR, PDF, etc.).
/// </summary>
public class FileAsset
{
    public int Id { get; set; }
    
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// Relative path from the library root.
    /// </summary>
    public string? RelativePath { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// File hash (SHA256).
    /// </summary>
    public string? Hash { get; set; }
    
    /// <summary>
    /// Archive format (cbz, cbr, pdf).
    /// </summary>
    public required string Format { get; set; }
    
    /// <summary>
    /// Page count if detected.
    /// </summary>
    public int? PageCount { get; set; }
    
    /// <summary>
    /// Associated issue ID (for single issues).
    /// </summary>
    public int? IssueId { get; set; }
    
    /// <summary>
    /// Associated edition ID (for collected editions).
    /// </summary>
    public int? EditionTitleId { get; set; }
    
    /// <summary>
    /// Quality profile match score.
    /// </summary>
    public int? QualityScore { get; set; }
    
    /// <summary>
    /// Date the file was added to the library.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last modified date of the file.
    /// </summary>
    public DateTime? LastModified { get; set; }
    
    // Navigation properties
    public Issue? Issue { get; set; }
    public EditionTitle? EditionTitle { get; set; }
}

