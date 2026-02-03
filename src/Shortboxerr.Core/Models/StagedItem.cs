namespace Shortboxerr.Core.Models;

/// <summary>
/// Represents a file in the staging folder awaiting import.
/// </summary>
public class StagedItem
{
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Filename without path.
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }
    
    /// <summary>
    /// File extension (cbz, cbr, pdf).
    /// </summary>
    public required string Extension { get; init; }
    
    /// <summary>
    /// Last modified time of the file.
    /// </summary>
    public DateTime LastModified { get; init; }
    
    /// <summary>
    /// Parsed information from the filename.
    /// </summary>
    public ParsedComicInfo? ParsedInfo { get; set; }
    
    /// <summary>
    /// Quality/confidence score of the parse (0-100).
    /// </summary>
    public int ParseConfidence { get; set; }
    
    /// <summary>
    /// Suggested series match (if any).
    /// </summary>
    public int? SuggestedSeriesId { get; set; }
    
    /// <summary>
    /// Suggested edition match (if any).
    /// </summary>
    public int? SuggestedEditionId { get; set; }
    
    /// <summary>
    /// Whether this appears to be a collection (TPB/omnibus) vs single issue.
    /// </summary>
    public bool IsCollection { get; set; }
    
    /// <summary>
    /// Rejection reason if file cannot be imported.
    /// </summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Information parsed from a comic filename.
/// </summary>
public class ParsedComicInfo
{
    /// <summary>
    /// Series title extracted from filename.
    /// </summary>
    public string? SeriesTitle { get; set; }
    
    /// <summary>
    /// Issue number (for singles).
    /// </summary>
    public decimal? IssueNumber { get; set; }
    
    /// <summary>
    /// Volume number (for TPBs or series volumes).
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// Year if present in filename.
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Publisher if detected.
    /// </summary>
    public string? Publisher { get; set; }
    
    /// <summary>
    /// Edition type indicator (TPB, HC, Omnibus, etc.).
    /// </summary>
    public string? EditionIndicator { get; set; }
    
    /// <summary>
    /// Issue range for collections (e.g., "1-6").
    /// </summary>
    public string? IssueRange { get; set; }
    
    /// <summary>
    /// Any additional tags/info from filename.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}



