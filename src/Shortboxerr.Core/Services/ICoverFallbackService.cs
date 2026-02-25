namespace Shortboxerr.Core.Services;

/// <summary>
/// Service that provides cover image fallback logic.
/// 
/// Priority hierarchy:
/// 1. ComicVine issue-specific cover (primary, source of truth - checked before calling this service)
/// 2. Metron issue cover via ComicVine issue ID lookup (exact matching via cv_id field)
/// 3. Metron issue cover via ComicVine volume ID + issue number (series ID mapping then issue lookup)
/// 4. Metron issue cover via series name/issue number search (fuzzy fallback)
/// 5. ComicVine volume/series cover (final fallback)
/// 
/// This service should only be invoked when the primary ComicVine issue cover is missing.
/// </summary>
public interface ICoverFallbackService
{
    /// <summary>
    /// Gets a cover image for an issue using ComicVine IDs for Metron lookup.
    /// Tries multiple strategies in priority order:
    /// 1. Direct lookup by CV issue ID
    /// 2. Lookup by CV volume ID (to get Metron series) + issue number
    /// 3. Falls back to volume cover if provided
    /// </summary>
    /// <param name="comicVineIssueId">ComicVine issue ID for direct Metron lookup</param>
    /// <param name="comicVineVolumeId">ComicVine volume ID for series-based lookup (optional but recommended)</param>
    /// <param name="issueNumber">Issue number for series-based lookup (required if comicVineVolumeId provided)</param>
    /// <param name="volumeCoverUrl">ComicVine volume cover URL as final fallback</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing cover URL and source information</returns>
    Task<CoverFallbackResult> GetCoverByCvIdAsync(
        int comicVineIssueId,
        int? comicVineVolumeId = null,
        string? issueNumber = null,
        string? volumeCoverUrl = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cover image for an issue when ComicVine issue ID is not available.
    /// Tries multiple strategies in priority order:
    /// 1. Lookup by CV volume ID (to get Metron series) + issue number (if volume ID provided)
    /// 2. Search by series name/issue number with heuristic matching
    /// 3. Falls back to volume cover if provided
    /// </summary>
    /// <param name="seriesName">Series name for searching alternate sources</param>
    /// <param name="issueNumber">Issue number for searching alternate sources</param>
    /// <param name="comicVineVolumeId">ComicVine volume ID for series-based lookup (optional but recommended)</param>
    /// <param name="publisher">Publisher name for better matching</param>
    /// <param name="expectedStoreDate">Expected store date for better matching</param>
    /// <param name="volumeCoverUrl">ComicVine volume cover URL as final fallback</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing cover URL and source information</returns>
    Task<CoverFallbackResult> GetCoverAsync(
        string seriesName,
        string issueNumber,
        int? comicVineVolumeId = null,
        string? publisher = null,
        DateTime? expectedStoreDate = null,
        string? volumeCoverUrl = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cover statistics including hit/miss rates per source.
    /// </summary>
    Task<CoverFallbackStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears cached fallback cover data for a specific series/issue.
    /// Should be called when ComicVine cover becomes available.
    /// </summary>
    Task ClearCacheAsync(string seriesName, string issueNumber, CancellationToken cancellationToken = default);

}

/// <summary>
/// Result of a cover fallback query.
/// </summary>
public class CoverFallbackResult
{
    /// <summary>Whether a cover was found.</summary>
    public bool Success { get; set; }

    /// <summary>The cover image URL.</summary>
    public string? CoverUrl { get; set; }

    /// <summary>Which source provided the cover.</summary>
    public CoverSource Source { get; set; }

    /// <summary>Error message if the lookup failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether the result came from cache.</summary>
    public bool FromCache { get; set; }

    /// <summary>Time taken to resolve the cover in milliseconds.</summary>
    public long ResolutionTimeMs { get; set; }

    /// <summary>
    /// Matching method used to select this cover (e.g., CvId, IdLessHeuristic, VolumeFallback).
    /// </summary>
    public string? MatchMethod { get; set; }

    /// <summary>
    /// Match confidence from 0.0 to 1.0 for heuristic matches.
    /// Null for deterministic/non-heuristic paths.
    /// </summary>
    public double? MatchConfidence { get; set; }

    /// <summary>
    /// True when an ID-less Metron candidate was rejected for low confidence.
    /// </summary>
    public bool WasConfidenceRejected { get; set; }

    public static CoverFallbackResult NotFound(string? error = null, bool wasConfidenceRejected = false) => new()
    {
        Success = false,
        Source = CoverSource.None,
        Error = error ?? "No cover found in any source",
        WasConfidenceRejected = wasConfidenceRejected
    };

    public static CoverFallbackResult Found(
        string coverUrl,
        CoverSource source,
        bool fromCache = false,
        string? matchMethod = null,
        double? matchConfidence = null) => new()
    {
        Success = true,
        CoverUrl = coverUrl,
        Source = source,
        FromCache = fromCache,
        MatchMethod = matchMethod,
        MatchConfidence = matchConfidence
    };
}

/// <summary>
/// Identifies the source of a cover image.
/// </summary>
public enum CoverSource
{
    /// <summary>No cover found.</summary>
    None = 0,

    /// <summary>ComicVine issue-specific cover (primary).</summary>
    ComicVineIssue = 1,

    /// <summary>Metron issue cover via ComicVine ID lookup (official API fallback).</summary>
    Metron = 2,

    /// <summary>Marvel API cover (Marvel comics only).</summary>
    MarvelApi = 3,

    /// <summary>ComicVine volume/series cover (final fallback).</summary>
    ComicVineVolume = 4
}

/// <summary>
/// Statistics about cover fallback usage.
/// </summary>
public class CoverFallbackStats
{
    /// <summary>Total cover requests.</summary>
    public long TotalRequests { get; set; }

    /// <summary>Requests fulfilled by ComicVine issue cover.</summary>
    public long ComicVineIssueHits { get; set; }

    /// <summary>Requests fulfilled by Metron via ComicVine ID lookup.</summary>
    public long MetronHits { get; set; }

    /// <summary>Requests fulfilled by Marvel API.</summary>
    public long MarvelApiHits { get; set; }

    /// <summary>Requests fulfilled by ComicVine volume cover.</summary>
    public long ComicVineVolumeHits { get; set; }

    /// <summary>Requests that found no cover.</summary>
    public long Misses { get; set; }

    /// <summary>Cache hit ratio (0.0 - 1.0).</summary>
    public double CacheHitRatio { get; set; }

    /// <summary>Average resolution time in milliseconds.</summary>
    public double AverageResolutionTimeMs { get; set; }
}
