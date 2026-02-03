using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for managing issue metadata via ComicVine.
/// </summary>
public interface IIssueMetadataService
{
    /// <summary>
    /// Gets issue details from ComicVine by ID.
    /// </summary>
    Task<IssueDetailResult> GetIssueByComicVineIdAsync(int comicVineIssueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes metadata for a specific issue from ComicVine.
    /// </summary>
    Task<IssueRefreshResult> RefreshIssueMetadataAsync(int issueId, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes metadata for all issues in a series from ComicVine.
    /// </summary>
    Task<IssuesBulkRefreshResult> RefreshSeriesIssuesMetadataAsync(int seriesId, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs story arcs for an issue from ComicVine.
    /// </summary>
    Task<IssueStoryArcSyncResult> SyncIssueStoryArcsAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects and marks special issues (annuals, one-shots, etc.) in a series.
    /// </summary>
    Task<SpecialIssueDetectionResult> DetectSpecialIssuesAsync(int seriesId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of fetching issue details from ComicVine.
/// </summary>
public class IssueDetailResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ComicVineIssueDetail? Issue { get; set; }
}

/// <summary>
/// Detailed issue information from ComicVine.
/// </summary>
public class ComicVineIssueDetail
{
    public int ComicVineId { get; set; }
    public string? Name { get; set; }
    public string IssueNumber { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? CoverDate { get; set; }
    public DateTime? StoreDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ComicVineUrl { get; set; }
    public int? VolumeId { get; set; }
    public string? VolumeName { get; set; }
    public List<StoryArcInfo> StoryArcs { get; set; } = new();
    public bool IsAnnual { get; set; }
    public bool IsSpecial { get; set; }
    public string? SpecialType { get; set; }
}

/// <summary>
/// Story arc information.
/// </summary>
public class StoryArcInfo
{
    public int ComicVineId { get; set; }
    public string Name { get; set; } = "";
    public string? ComicVineUrl { get; set; }
}

/// <summary>
/// Result of refreshing issue metadata.
/// </summary>
public class IssueRefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int IssueId { get; set; }
    public int? ComicVineId { get; set; }
    public bool WasUpdated { get; set; }
    public List<string> UpdatedFields { get; set; } = new();
    public int StoryArcsAdded { get; set; }
    public int StoryArcsRemoved { get; set; }
}

/// <summary>
/// Result of bulk refreshing issues.
/// </summary>
public class IssuesBulkRefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SeriesId { get; set; }
    public int TotalIssues { get; set; }
    public int IssuesRefreshed { get; set; }
    public int IssuesFailed { get; set; }
    public int IssuesSkipped { get; set; }
    public List<IssueRefreshResult> Results { get; set; } = new();
}

/// <summary>
/// Result of syncing issue story arcs.
/// </summary>
public class IssueStoryArcSyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int IssueId { get; set; }
    public int StoryArcsAdded { get; set; }
    public int StoryArcsRemoved { get; set; }
    public List<string> StoryArcNames { get; set; } = new();
}

/// <summary>
/// Result of detecting special issues.
/// </summary>
public class SpecialIssueDetectionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SeriesId { get; set; }
    public int AnnualsDetected { get; set; }
    public int SpecialsDetected { get; set; }
    public List<SpecialIssueInfo> SpecialIssues { get; set; } = new();
}

/// <summary>
/// Information about a detected special issue.
/// </summary>
public class SpecialIssueInfo
{
    public int IssueId { get; set; }
    public string IssueNumber { get; set; } = "";
    public bool IsAnnual { get; set; }
    public bool IsSpecial { get; set; }
    public string? SpecialType { get; set; }
}

