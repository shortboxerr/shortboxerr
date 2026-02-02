using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Represents a release candidate from a DDL (Direct Download Link) source.
/// Extends the base candidate model with DDL-specific fields.
/// </summary>
public class DdlCandidate
{
    /// <summary>
    /// Unique identifier for this candidate.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Original release title as found on the DDL site.
    /// </summary>
    public required string ReleaseTitle { get; init; }
    
    /// <summary>
    /// Source site identifier (e.g., "GettyComics", "ReadComicOnline").
    /// </summary>
    public required string SourceSite { get; init; }
    
    /// <summary>
    /// URL of the page where this release was found.
    /// </summary>
    public string? SourceUrl { get; init; }
    
    /// <summary>
    /// Parsed information extracted from the release title.
    /// </summary>
    public required DdlParsedInfo ParsedInfo { get; init; }
    
    /// <summary>
    /// Available download links for this release.
    /// </summary>
    public List<DdlDownloadLink> DownloadLinks { get; init; } = new();
    
    /// <summary>
    /// Total file size in bytes (if known).
    /// </summary>
    public long? Size { get; init; }
    
    /// <summary>
    /// When this candidate was discovered.
    /// </summary>
    public DateTime DateFound { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Quality score assigned by the release parser.
    /// </summary>
    public int QualityScore { get; set; }
    
    /// <summary>
    /// Additional tags extracted from the release.
    /// </summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>
    /// Whether this candidate has been filtered out.
    /// </summary>
    public bool IsFiltered { get; set; }
    
    /// <summary>
    /// Reason for filtering (if filtered).
    /// </summary>
    public string? FilterReason { get; set; }
    
    /// <summary>
    /// Convert to the generic Candidate model for DecisionEngine processing.
    /// </summary>
    public Candidate ToCandidate() => new()
    {
        Id = Id,
        ReleaseTitle = ReleaseTitle,
        Source = SourceSite,
        SourcePriority = 0, // Will be set by provider
        SeriesTitle = ParsedInfo.SeriesTitle,
        IssueNumber = ParsedInfo.IssueNumber,
        VolumeNumber = ParsedInfo.VolumeNumber,
        Year = ParsedInfo.Year,
        Format = ParsedInfo.Format,
        Size = Size,
        IsCollection = ParsedInfo.IsCollection,
        EditionType = ParsedInfo.EditionType,
        DownloadUrl = DownloadLinks.FirstOrDefault()?.Url,
        DiscoveredAt = DateFound,
        Tags = Tags
    };
}

/// <summary>
/// Parsed information extracted from a DDL release title.
/// </summary>
public class DdlParsedInfo
{
    /// <summary>
    /// Extracted series title.
    /// </summary>
    public string? SeriesTitle { get; set; }
    
    /// <summary>
    /// Extracted issue number (for singles).
    /// </summary>
    public decimal? IssueNumber { get; set; }
    
    /// <summary>
    /// Extracted volume number.
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// Extracted year.
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Extracted publisher.
    /// </summary>
    public string? Publisher { get; set; }
    
    /// <summary>
    /// File format (cbz, cbr, pdf).
    /// </summary>
    public string? Format { get; set; }
    
    /// <summary>
    /// Whether this is a collection (TPB, HC, Omnibus, etc.).
    /// </summary>
    public bool IsCollection { get; set; }
    
    /// <summary>
    /// Edition type for collections.
    /// </summary>
    public string? EditionType { get; set; }
    
    /// <summary>
    /// Issue range for collections (e.g., "1-6").
    /// </summary>
    public string? IssueRange { get; set; }
    
    /// <summary>
    /// Release group or scene tag.
    /// </summary>
    public string? ReleaseGroup { get; set; }
    
    /// <summary>
    /// Quality indicator (Digital, Scan, Webrip, etc.).
    /// </summary>
    public string? Quality { get; set; }
    
    /// <summary>
    /// Parse confidence (0-100).
    /// </summary>
    public int Confidence { get; set; }
    
    /// <summary>
    /// Raw tokens extracted from the title.
    /// </summary>
    public List<string> Tokens { get; init; } = new();
}

/// <summary>
/// Represents a download link for a DDL candidate.
/// </summary>
public class DdlDownloadLink
{
    /// <summary>
    /// Download URL.
    /// </summary>
    public required string Url { get; init; }
    
    /// <summary>
    /// Link type (direct, redirect, hoster).
    /// </summary>
    public DdlLinkType LinkType { get; init; }
    
    /// <summary>
    /// Hosting service name (if applicable).
    /// </summary>
    public string? HostName { get; init; }
    
    /// <summary>
    /// Whether this link has been verified as working.
    /// </summary>
    public bool IsVerified { get; set; }
    
    /// <summary>
    /// Priority for this link (lower = preferred).
    /// </summary>
    public int Priority { get; init; }
    
    /// <summary>
    /// Part number for multi-part releases.
    /// </summary>
    public int? PartNumber { get; init; }
    
    /// <summary>
    /// Total parts for multi-part releases.
    /// </summary>
    public int? TotalParts { get; init; }
}

/// <summary>
/// Types of DDL download links.
/// </summary>
public enum DdlLinkType
{
    /// <summary>
    /// Direct download link to the file.
    /// </summary>
    Direct = 0,
    
    /// <summary>
    /// Link that redirects to the actual download.
    /// </summary>
    Redirect = 1,
    
    /// <summary>
    /// Link to a file hosting service.
    /// </summary>
    Hoster = 2,
    
    /// <summary>
    /// Magnet link (future support).
    /// </summary>
    Magnet = 3
}

