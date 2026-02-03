namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for managing cover images for series and issues.
/// Handles downloading, caching, and fallback logic.
/// </summary>
public interface ICoverService
{
    /// <summary>
    /// Gets the cover for a series, downloading if necessary.
    /// </summary>
    Task<CoverResult> GetSeriesCoverAsync(int seriesId, CoverSize size = CoverSize.Medium, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cover for an issue, with fallback to series cover if missing.
    /// </summary>
    Task<CoverResult> GetIssueCoverAsync(int issueId, CoverSize size = CoverSize.Medium, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and caches a cover from a URL.
    /// </summary>
    Task<CoverResult> DownloadCoverAsync(string url, CoverType type, int entityId, CoverSize size = CoverSize.Medium, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cached cover for a series.
    /// </summary>
    Task ClearSeriesCoverCacheAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cached cover for an issue.
    /// </summary>
    Task ClearIssueCoverCacheAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    Task<CoverCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached covers.
    /// </summary>
    Task ClearAllCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the placeholder image.
    /// </summary>
    string GetPlaceholderPath();
}

/// <summary>
/// Cover image sizes matching ComicVine's image sizes.
/// </summary>
public enum CoverSize
{
    /// <summary>
    /// Small thumbnail (35x35).
    /// </summary>
    Thumb = 0,

    /// <summary>
    /// Small image (90x90).
    /// </summary>
    Small = 1,

    /// <summary>
    /// Medium image (~400px wide).
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Large/original image.
    /// </summary>
    Large = 3
}

/// <summary>
/// Type of cover entity.
/// </summary>
public enum CoverType
{
    Series = 0,
    Issue = 1,
    Edition = 2
}

/// <summary>
/// Result of a cover operation.
/// </summary>
public class CoverResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// Local file path to the cached cover.
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Content type (e.g., "image/jpeg", "image/png").
    /// </summary>
    public string? ContentType { get; set; }
    
    /// <summary>
    /// Whether the cover is a placeholder.
    /// </summary>
    public bool IsPlaceholder { get; set; }
    
    /// <summary>
    /// Whether the cover is a fallback (e.g., series cover for issue).
    /// </summary>
    public bool IsFallback { get; set; }
    
    /// <summary>
    /// The original URL the cover was downloaded from.
    /// </summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// The type of entity this cover belongs to.
    /// </summary>
    public CoverType CoverType { get; set; }
    
    /// <summary>
    /// The ID of the entity this cover belongs to.
    /// </summary>
    public int EntityId { get; set; }
    
    /// <summary>
    /// The size of this cover.
    /// </summary>
    public CoverSize Size { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long? FileSize { get; set; }
    
    /// <summary>
    /// When the cover was cached.
    /// </summary>
    public DateTime? CachedAt { get; set; }

    public static CoverResult NotFound(string error) => new()
    {
        Success = false,
        Error = error
    };

    public static CoverResult Placeholder(string path) => new()
    {
        Success = true,
        FilePath = path,
        ContentType = "image/png",
        IsPlaceholder = true
    };
}

/// <summary>
/// Cover cache statistics.
/// </summary>
public class CoverCacheStats
{
    public int TotalCovers { get; set; }
    public int SeriesCovers { get; set; }
    public int IssueCovers { get; set; }
    public int EditionCovers { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime? OldestCover { get; set; }
    public DateTime? NewestCover { get; set; }
}

/// <summary>
/// Settings for cover caching.
/// </summary>
public class CoverSettings
{
    /// <summary>
    /// Directory where covers are cached.
    /// </summary>
    public string CacheDirectory { get; set; } = "covers";

    /// <summary>
    /// Number of days to keep cached covers (0 = indefinite).
    /// </summary>
    public int RetentionDays { get; set; } = 0;

    /// <summary>
    /// Default size to download when not specified.
    /// </summary>
    public CoverSize DefaultSize { get; set; } = CoverSize.Medium;

    /// <summary>
    /// Whether to download all sizes when fetching a cover.
    /// </summary>
    public bool DownloadAllSizes { get; set; } = false;

    /// <summary>
    /// Maximum concurrent downloads.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Timeout for cover downloads in seconds.
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 30;
}

