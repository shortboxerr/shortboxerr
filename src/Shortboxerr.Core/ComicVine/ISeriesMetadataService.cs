using System.Text.Json.Serialization;
using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for managing series metadata via ComicVine.
/// </summary>
public interface ISeriesMetadataService
{
    /// <summary>
    /// Searches ComicVine for series matching the given query.
    /// </summary>
    Task<SeriesSearchResult> SearchSeriesAsync(
        string query,
        string? publisher = null,
        int? yearStart = null,
        int? yearEnd = null,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a series from ComicVine by its volume ID.
    /// </summary>
    Task<SeriesMatchCandidate?> GetSeriesByComicVineIdAsync(
        int volumeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches a local series to a ComicVine volume.
    /// </summary>
    Task<SeriesMatchResult> MatchSeriesAsync(
        int seriesId,
        int comicVineVolumeId,
        bool syncMetadata = true,
        bool createMissingIssues = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-matches a local series to ComicVine based on title and year.
    /// </summary>
    Task<SeriesAutoMatchResult> AutoMatchSeriesAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-matches all unmatched series in the library.
    /// </summary>
    Task<BulkMatchResult> AutoMatchAllSeriesAsync(
        int? confidenceThreshold = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the ComicVine match from a series.
    /// </summary>
    Task<bool> UnmatchSeriesAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes metadata for a matched series from ComicVine.
    /// </summary>
    Task<SeriesRefreshResult> RefreshSeriesMetadataAsync(
        int seriesId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new series from ComicVine by volume ID.
    /// Creates the series and all its issues.
    /// </summary>
    Task<SeriesAddResult> AddSeriesByComicVineIdAsync(
        int volumeId,
        string? rootFolder = null,
        bool monitored = true,
        SeriesMonitoringMode monitoringMode = SeriesMonitoringMode.AllIssues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs issue list from ComicVine for a matched series.
    /// Creates missing issues and updates existing ones.
    /// </summary>
    Task<IssueSyncResult> SyncIssuesFromComicVineAsync(
        int seriesId,
        CancellationToken cancellationToken = default);
}

#region Result Types

/// <summary>
/// Result of a series search.
/// </summary>
public class SeriesSearchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SeriesMatchCandidate> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
}

/// <summary>
/// A candidate match for a series from ComicVine.
/// </summary>
public class SeriesMatchCandidate
{
    /// <summary>
    /// ComicVine volume ID.
    /// </summary>
    public int ComicVineId { get; set; }
    
    /// <summary>
    /// Series title.
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Alternate names/aliases.
    /// </summary>
    public List<string> Aliases { get; set; } = new();
    
    /// <summary>
    /// Publisher name.
    /// </summary>
    public string? Publisher { get; set; }
    
    /// <summary>
    /// ComicVine publisher ID.
    /// </summary>
    public int? PublisherId { get; set; }
    
    /// <summary>
    /// Year the series started.
    /// </summary>
    public int? StartYear { get; set; }
    
    /// <summary>
    /// Series description.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Total issue count.
    /// </summary>
    public int IssueCount { get; set; }
    
    /// <summary>
    /// Cover image URL.
    /// </summary>
    public string? CoverImageUrl { get; set; }
    
    /// <summary>
    /// ComicVine site URL.
    /// </summary>
    public string? ComicVineUrl { get; set; }
    
    /// <summary>
    /// Match confidence score (0-100).
    /// </summary>
    public int ConfidenceScore { get; set; }
    
    /// <summary>
    /// Reasons for the confidence score.
    /// </summary>
    public List<string> ConfidenceReasons { get; set; } = new();
}

/// <summary>
/// Result of matching a series to ComicVine.
/// </summary>
public class SeriesMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public int? ComicVineId { get; set; }
    public bool MetadataSynced { get; set; }
    public int IssuesCreated { get; set; }
    public int IssuesUpdated { get; set; }
}

/// <summary>
/// Result of auto-matching a series.
/// </summary>
public class SeriesAutoMatchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public int? MatchedComicVineId { get; set; }
    public int ConfidenceScore { get; set; }
    public bool RequiresManualReview { get; set; }
    public List<SeriesMatchCandidate> Candidates { get; set; } = new();
}

/// <summary>
/// Result of bulk matching all unmatched series.
/// </summary>
public class BulkMatchResult
{
    public bool Success { get; set; }
    public int TotalProcessed { get; set; }
    public int Matched { get; set; }
    public int RequiresReview { get; set; }
    public int Failed { get; set; }
    public List<SeriesAutoMatchResult> Results { get; set; } = new();
}

/// <summary>
/// Result of refreshing series metadata.
/// </summary>
public class SeriesRefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public bool MetadataChanged { get; set; }
    public int IssuesAdded { get; set; }
    public int IssuesUpdated { get; set; }
    public DateTime? LastRefreshed { get; set; }
}

/// <summary>
/// Result of adding a series from ComicVine.
/// </summary>
public class SeriesAddResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public int? ComicVineId { get; set; }
    public string? Title { get; set; }
    public int IssuesCreated { get; set; }
    public bool AlreadyExists { get; set; }
    public int? ExistingSeriesId { get; set; }
}

/// <summary>
/// Result of syncing issues from ComicVine.
/// </summary>
public class IssueSyncResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? SeriesId { get; set; }
    public int IssuesAdded { get; set; }
    public int IssuesUpdated { get; set; }
    public int TotalIssues { get; set; }
}

#endregion

#region Enums

/// <summary>
/// How a series should be monitored for new issues.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeriesMonitoringMode
{
    /// <summary>
    /// Monitor all issues (past and future).
    /// </summary>
    AllIssues = 0,
    
    /// <summary>
    /// Only monitor future issues.
    /// </summary>
    FutureIssues = 1,
    
    /// <summary>
    /// Manual selection only.
    /// </summary>
    Manual = 2,
    
    /// <summary>
    /// Only first issues (for new series discovery).
    /// </summary>
    FirstIssue = 3,
    
    /// <summary>
    /// Don't monitor this series at all.
    /// </summary>
    None = 4
}

#endregion

