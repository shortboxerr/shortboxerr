using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for managing collected edition metadata via ComicVine.
/// Handles TPBs, hardcovers, omnibuses, and other collected editions.
/// </summary>
public interface IEditionMetadataService
{
    /// <summary>
    /// Searches ComicVine for collected editions matching the given query.
    /// </summary>
    Task<EditionSearchResult> SearchEditionsAsync(
        string query,
        string? publisher = null,
        int? year = null,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a collected edition from ComicVine by its volume ID.
    /// </summary>
    Task<EditionMatchCandidate?> GetEditionByComicVineIdAsync(
        int volumeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches a local edition to a ComicVine volume.
    /// </summary>
    Task<EditionMatchResult> MatchEditionAsync(
        int editionId,
        int comicVineVolumeId,
        bool syncMetadata = true,
        bool mapContents = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-matches a local edition to ComicVine based on title.
    /// </summary>
    Task<EditionAutoMatchResult> AutoMatchEditionAsync(
        int editionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the ComicVine match from an edition.
    /// </summary>
    Task<bool> UnmatchEditionAsync(
        int editionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes metadata for a matched edition from ComicVine.
    /// </summary>
    Task<EditionMatchResult> RefreshEditionMetadataAsync(
        int editionId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs the issues contained in an edition from ComicVine.
    /// </summary>
    Task<EditionContentSyncResult> SyncEditionContentsAsync(
        int editionId,
        CancellationToken cancellationToken = default);
}

#region Result Types

/// <summary>
/// Result of searching for editions on ComicVine.
/// </summary>
public class EditionSearchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<EditionMatchCandidate> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
}

/// <summary>
/// A candidate match for a collected edition from ComicVine.
/// </summary>
public class EditionMatchCandidate
{
    public int ComicVineId { get; set; }
    public required string Title { get; set; }
    public int? StartYear { get; set; }
    public string? Publisher { get; set; }
    public string? Description { get; set; }
    public int IssueCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ComicVineUrl { get; set; }
    public int ConfidenceScore { get; set; }
    public List<string> ConfidenceReasons { get; set; } = new();
    
    /// <summary>
    /// Detected edition type based on title analysis.
    /// </summary>
    public EditionType? DetectedEditionType { get; set; }
}

/// <summary>
/// Result of matching an edition to ComicVine.
/// </summary>
public class EditionMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? EditionId { get; set; }
    public int? ComicVineId { get; set; }
    public bool MetadataSynced { get; set; }
    public bool ContentsMapped { get; set; }
    public int IssuesMapped { get; set; }
}

/// <summary>
/// Result of auto-matching an edition.
/// </summary>
public class EditionAutoMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? EditionId { get; set; }
    public int? MatchedComicVineId { get; set; }
    public int ConfidenceScore { get; set; }
    public bool RequiresManualReview { get; set; }
    public List<EditionMatchCandidate> Candidates { get; set; } = new();
}

/// <summary>
/// Result of syncing edition contents from ComicVine.
/// </summary>
public class EditionContentSyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int EditionId { get; set; }
    public int IssuesFound { get; set; }
    public int IssuesMapped { get; set; }
    public int IssuesCreated { get; set; }
    public List<EditionContentMapping> Mappings { get; set; } = new();
}

/// <summary>
/// Mapping of an issue contained in an edition.
/// </summary>
public class EditionContentMapping
{
    public int ComicVineIssueId { get; set; }
    public string IssueNumber { get; set; } = "";
    public string? IssueTitle { get; set; }
    public int? LocalIssueId { get; set; }
    public int? LocalSeriesId { get; set; }
    public string? LocalSeriesTitle { get; set; }
    public bool WasCreated { get; set; }
}

#endregion

