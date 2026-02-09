using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Represents a release candidate from an NZB/Usenet source.
/// Extends the base candidate model with NZB-specific fields.
/// </summary>
public class NzbCandidate
{
    /// <summary>
    /// Unique identifier for this candidate (typically the NZB GUID).
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Original release title as found on the indexer.
    /// </summary>
    public required string ReleaseTitle { get; init; }
    
    /// <summary>
    /// Indexer name/identifier.
    /// </summary>
    public required string IndexerName { get; init; }
    
    /// <summary>
    /// Indexer ID (for tracking/priority).
    /// </summary>
    public string? IndexerId { get; init; }
    
    /// <summary>
    /// URL to download the NZB file.
    /// </summary>
    public required string NzbUrl { get; init; }
    
    /// <summary>
    /// URL to the release info page on the indexer.
    /// </summary>
    public string? InfoUrl { get; init; }
    
    /// <summary>
    /// Parsed information extracted from the release title.
    /// </summary>
    public required NzbParsedInfo ParsedInfo { get; init; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }
    
    /// <summary>
    /// Publication date on the indexer.
    /// </summary>
    public DateTime PublishedDate { get; init; }
    
    /// <summary>
    /// Age in days since posting.
    /// </summary>
    public int Age => (int)(DateTime.UtcNow - PublishedDate).TotalDays;
    
    /// <summary>
    /// Category IDs assigned by the indexer.
    /// </summary>
    public List<int> Categories { get; init; } = new();
    
    /// <summary>
    /// Category names assigned by the indexer.
    /// </summary>
    public List<string> CategoryNames { get; init; } = new();
    
    /// <summary>
    /// Number of grabs/downloads on the indexer.
    /// </summary>
    public int? Grabs { get; init; }
    
    /// <summary>
    /// Number of files in the NZB.
    /// </summary>
    public int? Files { get; init; }
    
    /// <summary>
    /// Poster/uploader name.
    /// </summary>
    public string? Poster { get; init; }
    
    /// <summary>
    /// Usenet group where this was posted.
    /// </summary>
    public string? Group { get; init; }
    
    /// <summary>
    /// Whether the release is password protected.
    /// </summary>
    public bool IsPasswordProtected { get; init; }
    
    /// <summary>
    /// Quality score assigned by the release parser.
    /// </summary>
    public int QualityScore { get; set; }
    
    /// <summary>
    /// Indexer priority (lower = better).
    /// </summary>
    public int IndexerPriority { get; init; }
    
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
    /// When this candidate was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Convert to the generic Candidate model for DecisionEngine processing.
    /// </summary>
    public Candidate ToCandidate() => new()
    {
        Id = Id,
        ReleaseTitle = ReleaseTitle,
        Source = $"NZB:{IndexerName}",
        SourcePriority = IndexerPriority,
        SeriesTitle = ParsedInfo.SeriesTitle,
        IssueNumber = ParsedInfo.IssueNumber,
        VolumeNumber = ParsedInfo.VolumeNumber,
        Year = ParsedInfo.Year,
        Format = ParsedInfo.Format,
        Size = Size,
        IsCollection = ParsedInfo.IsCollection,
        EditionType = ParsedInfo.EditionType,
        DownloadUrl = NzbUrl,
        DiscoveredAt = DiscoveredAt,
        Tags = Tags
    };
    
    /// <summary>
    /// Create an NzbCandidate from a NewznabRelease.
    /// </summary>
    public static NzbCandidate FromNewznabRelease(NewznabRelease release, NzbParsedInfo parsedInfo, int indexerPriority = 50)
    {
        return new NzbCandidate
        {
            Id = release.Guid,
            ReleaseTitle = release.Title,
            IndexerName = release.IndexerName ?? "Unknown",
            IndexerId = release.IndexerId,
            NzbUrl = release.NzbUrl,
            InfoUrl = release.InfoUrl,
            ParsedInfo = parsedInfo,
            Size = release.Size,
            PublishedDate = release.PublishedDate,
            Categories = release.Categories,
            CategoryNames = release.CategoryNames,
            Grabs = release.Grabs,
            Files = release.Files,
            Poster = release.Poster,
            Group = release.Group,
            IsPasswordProtected = release.PasswordStatus == 1,
            IndexerPriority = indexerPriority,
            Tags = parsedInfo.Tags.ToList()
        };
    }
}

/// <summary>
/// Parsed information extracted from an NZB release title.
/// </summary>
public class NzbParsedInfo
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
    /// File format (cbz, cbr, pdf, epub).
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
    /// Release group (scene group that released this).
    /// </summary>
    public string? ReleaseGroup { get; set; }
    
    /// <summary>
    /// Quality indicator (Digital, Scan, Webrip, etc.).
    /// </summary>
    public string? Quality { get; set; }
    
    /// <summary>
    /// Whether this is a REPACK/PROPER release.
    /// </summary>
    public bool IsRepack { get; set; }
    
    /// <summary>
    /// Whether this is a PROPER release.
    /// </summary>
    public bool IsProper { get; set; }
    
    /// <summary>
    /// Whether this is an INTERNAL release.
    /// </summary>
    public bool IsInternal { get; set; }
    
    /// <summary>
    /// Parse confidence (0-100).
    /// </summary>
    public int Confidence { get; set; }
    
    /// <summary>
    /// Raw tokens extracted from the title.
    /// </summary>
    public List<string> Tokens { get; init; } = new();
    
    /// <summary>
    /// Additional tags/attributes found in the release name.
    /// </summary>
    public List<string> Tags { get; init; } = new();
}
