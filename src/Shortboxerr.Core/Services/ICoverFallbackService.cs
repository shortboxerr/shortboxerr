namespace Shortboxerr.Core.Services;

/// <summary>
/// Service that provides cover image fallback logic.
/// 
/// Priority hierarchy:
/// 1. ComicVine issue-specific cover (primary, source of truth)
/// 2. League of Comic Geeks issue cover (unofficial fallback)
/// 3. ComicVine volume/series cover (final fallback)
/// 
/// This service should only be invoked when the primary ComicVine issue cover is missing.
/// </summary>
public interface ICoverFallbackService
{
    /// <summary>
    /// Gets a cover image for an issue, querying fallback sources if the primary source has no cover.
    /// </summary>
    /// <param name="seriesName">Series name for searching alternate sources</param>
    /// <param name="issueNumber">Issue number for searching alternate sources</param>
    /// <param name="publisher">Publisher name for better matching</param>
    /// <param name="volumeCoverUrl">ComicVine volume cover URL as final fallback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing cover URL and source information</returns>
    Task<CoverFallbackResult> GetCoverAsync(
        string seriesName,
        string issueNumber,
        string? publisher = null,
        string? volumeCoverUrl = null,
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

    public static CoverFallbackResult NotFound(string? error = null) => new()
    {
        Success = false,
        Source = CoverSource.None,
        Error = error ?? "No cover found in any source"
    };

    public static CoverFallbackResult Found(string coverUrl, CoverSource source, bool fromCache = false) => new()
    {
        Success = true,
        CoverUrl = coverUrl,
        Source = source,
        FromCache = fromCache
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

    /// <summary>League of Comic Geeks issue cover (fallback).</summary>
    LeagueOfComicGeeks = 2,

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

    /// <summary>Requests fulfilled by LOCG.</summary>
    public long LocgHits { get; set; }

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
