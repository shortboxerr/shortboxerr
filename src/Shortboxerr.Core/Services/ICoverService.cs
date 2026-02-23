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
    /// Gets detailed cache statistics including breakdown by size.
    /// </summary>
    Task<DetailedCoverCacheStats> GetDetailedCacheStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache access statistics (hit/miss ratios).
    /// </summary>
    CoverCacheAccessStats GetAccessStats();

    /// <summary>
    /// Resets cache access statistics.
    /// </summary>
    void ResetAccessStats();

    /// <summary>
    /// Clears all cached covers.
    /// </summary>
    Task ClearAllCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms the cache for a specific series (downloads all issue covers).
    /// </summary>
    Task<CacheWarmingResult> WarmSeriesCacheAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms the cache for multiple series.
    /// </summary>
    Task<CacheWarmingResult> WarmCacheAsync(IEnumerable<int> seriesIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current cache warming status.
    /// </summary>
    CacheWarmingStatus GetWarmingStatus();

    /// <summary>
    /// Performs cache cleanup: removes expired covers and enforces size limit via LRU eviction.
    /// </summary>
    Task<CoverCleanupResult> CleanupCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces cache size limit using LRU eviction.
    /// </summary>
    Task<CoverCleanupResult> EnforceCacheLimitAsync(CancellationToken cancellationToken = default);

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
/// Detailed cover cache statistics with breakdown by size.
/// </summary>
public class DetailedCoverCacheStats : CoverCacheStats
{
    /// <summary>
    /// Breakdown by cover size.
    /// </summary>
    public Dictionary<CoverSize, CoverSizeStats> BySize { get; set; } = new();

    /// <summary>
    /// Maximum configured cache size in bytes.
    /// </summary>
    public long MaxCacheSizeBytes { get; set; }

    /// <summary>
    /// Cache usage percentage (0-100).
    /// </summary>
    public double UsagePercent => MaxCacheSizeBytes > 0 
        ? Math.Round((double)TotalSizeBytes / MaxCacheSizeBytes * 100, 2) 
        : 0;

    /// <summary>
    /// Whether cache is over the configured limit.
    /// </summary>
    public bool IsOverLimit => MaxCacheSizeBytes > 0 && TotalSizeBytes > MaxCacheSizeBytes;

    /// <summary>
    /// Bytes over the limit (0 if under limit).
    /// </summary>
    public long BytesOverLimit => IsOverLimit ? TotalSizeBytes - MaxCacheSizeBytes : 0;

    /// <summary>
    /// Number of covers that would be evicted if cleanup runs now.
    /// </summary>
    public int PendingEvictionCount { get; set; }

    /// <summary>
    /// Timestamp of the last cleanup run.
    /// </summary>
    public DateTime? LastCleanupAt { get; set; }

    /// <summary>
    /// Number of covers evicted in the last cleanup.
    /// </summary>
    public int LastCleanupEvictedCount { get; set; }

    /// <summary>
    /// Hit/miss statistics for cache access monitoring.
    /// </summary>
    public CoverCacheAccessStats AccessStats { get; set; } = new();
}

/// <summary>
/// Cache access statistics for hit/miss ratio tracking.
/// </summary>
public class CoverCacheAccessStats
{
    /// <summary>Number of cache hits (cover found in cache).</summary>
    public long Hits { get; set; }
    
    /// <summary>Number of cache misses (cover not in cache, had to download).</summary>
    public long Misses { get; set; }
    
    /// <summary>Number of fallbacks (used series cover for issue, etc.).</summary>
    public long Fallbacks { get; set; }
    
    /// <summary>Number of placeholders served.</summary>
    public long Placeholders { get; set; }
    
    /// <summary>Total number of cover requests.</summary>
    public long TotalRequests => Hits + Misses + Fallbacks + Placeholders;
    
    /// <summary>Hit ratio (0-1). Higher is better.</summary>
    public double HitRatio => TotalRequests > 0 ? Math.Round((double)Hits / TotalRequests, 4) : 0;
    
    /// <summary>Miss ratio (0-1). Lower is better.</summary>
    public double MissRatio => TotalRequests > 0 ? Math.Round((double)Misses / TotalRequests, 4) : 0;
    
    /// <summary>Estimated bandwidth saved (bytes not re-downloaded due to cache hits).</summary>
    public long EstimatedBandwidthSavedBytes { get; set; }
    
    /// <summary>Timestamp when statistics were last reset.</summary>
    public DateTime LastReset { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Statistics for a specific cover size.
/// </summary>
public class CoverSizeStats
{
    public CoverSize Size { get; set; }
    public int Count { get; set; }
    public long TotalBytes { get; set; }
    public long AverageBytes => Count > 0 ? TotalBytes / Count : 0;
}

/// <summary>
/// Result of a cache cleanup operation.
/// </summary>
public class CoverCleanupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    /// Number of covers evicted due to LRU policy.
    /// </summary>
    public int EvictedByLru { get; set; }

    /// <summary>
    /// Number of covers evicted due to retention policy.
    /// </summary>
    public int EvictedByRetention { get; set; }

    /// <summary>
    /// Total number of covers evicted.
    /// </summary>
    public int TotalEvicted => EvictedByLru + EvictedByRetention;

    /// <summary>
    /// Bytes freed by cleanup.
    /// </summary>
    public long BytesFreed { get; set; }

    /// <summary>
    /// Cache size before cleanup.
    /// </summary>
    public long SizeBefore { get; set; }

    /// <summary>
    /// Cache size after cleanup.
    /// </summary>
    public long SizeAfter { get; set; }

    /// <summary>
    /// Duration of cleanup operation.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of a cache warming operation.
/// </summary>
public class CacheWarmingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>Number of series processed.</summary>
    public int SeriesProcessed { get; set; }

    /// <summary>Number of covers successfully downloaded.</summary>
    public int CoversDownloaded { get; set; }

    /// <summary>Number of covers that were already cached.</summary>
    public int CoversAlreadyCached { get; set; }

    /// <summary>Number of download failures.</summary>
    public int FailedDownloads { get; set; }

    /// <summary>Total bytes downloaded.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>Duration of warming operation.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Timestamp when warming completed.</summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Status of an ongoing cache warming operation.
/// </summary>
public class CacheWarmingStatus
{
    /// <summary>Whether warming is currently in progress.</summary>
    public bool IsWarming { get; set; }

    /// <summary>Total series to process.</summary>
    public int TotalSeries { get; set; }

    /// <summary>Series processed so far.</summary>
    public int ProcessedSeries { get; set; }

    /// <summary>Total covers to process.</summary>
    public int TotalCovers { get; set; }

    /// <summary>Covers processed so far.</summary>
    public int ProcessedCovers { get; set; }

    /// <summary>Progress percentage (0-100).</summary>
    public double ProgressPercent => TotalCovers > 0 
        ? Math.Round((double)ProcessedCovers / TotalCovers * 100, 1) 
        : 0;

    /// <summary>Timestamp when warming started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Estimated time remaining.</summary>
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <summary>Current series being processed.</summary>
    public string? CurrentSeries { get; set; }
}

/// <summary>
/// Metadata for a cached cover, used for efficient revalidation.
/// </summary>
public class CoverCacheMetadata
{
    /// <summary>ETag from the HTTP response.</summary>
    public string? ETag { get; set; }

    /// <summary>Last-Modified header from the HTTP response.</summary>
    public DateTime? LastModified { get; set; }

    /// <summary>When the cover was originally downloaded.</summary>
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the cover was last validated (checked for changes).</summary>
    public DateTime LastValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Original URL the cover was downloaded from.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }
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

    /// <summary>
    /// Maximum cache size in bytes (0 = unlimited).
    /// Default: 500MB (524,288,000 bytes).
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB

    /// <summary>
    /// Target cache size after cleanup as percentage of MaxCacheSizeBytes.
    /// When cleanup triggers, evict until reaching this percentage.
    /// Default: 80% (evict 20% of max when over limit).
    /// </summary>
    public int CleanupTargetPercent { get; set; } = 80;

    /// <summary>
    /// Interval in hours for background cache cleanup (0 = disabled).
    /// Default: 24 hours.
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether to enable automatic cleanup when cache exceeds limit.
    /// </summary>
    public bool AutoCleanupEnabled { get; set; } = true;

    /// <summary>
    /// Whether to automatically warm cache when a series is added.
    /// </summary>
    public bool WarmCacheOnSeriesAdd { get; set; } = false;

    /// <summary>
    /// Comma-separated list of sizes to warm (e.g., "Medium,Thumb").
    /// </summary>
    public string WarmCacheSizes { get; set; } = "Medium";

    /// <summary>
    /// Whether to use ETag/Last-Modified for efficient revalidation.
    /// </summary>
    public bool EnableRevalidation { get; set; } = true;

    /// <summary>
    /// Hours between revalidation checks (0 = revalidate on every request).
    /// Default: 168 hours (7 days).
    /// </summary>
    public int RevalidationIntervalHours { get; set; } = 168;
}

