namespace Shortboxerr.Core.Caching;

/// <summary>
/// Caching service abstraction providing consistent cache operations with
/// statistics tracking, prefix-based invalidation, and configurable TTLs.
/// </summary>
public interface ICacheService
{
    #region Core Operations

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">Type of the cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <returns>The cached value or default if not found.</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Gets a value from the cache asynchronously.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the cache, or creates and caches it if not found.
    /// </summary>
    /// <typeparam name="T">Type of the cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="factory">Factory function to create the value if not cached.</param>
    /// <param name="ttl">Time-to-live for the cached value.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Time-to-live (null uses default).</param>
    void Set<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// Sets a value in the cache asynchronously.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    void Remove(string key);

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    bool Exists(string key);

    #endregion

    #region Key Generation

    /// <summary>
    /// Generates a cache key with the specified prefix and segments.
    /// </summary>
    /// <param name="prefix">Cache key prefix (e.g., "series", "pulllist").</param>
    /// <param name="segments">Key segments to append.</param>
    /// <returns>A formatted cache key.</returns>
    string GenerateKey(string prefix, params object[] segments);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Removes all cached entries with keys starting with the specified prefix.
    /// </summary>
    /// <param name="prefix">Key prefix to match.</param>
    /// <returns>Number of entries removed.</returns>
    int RemoveByPrefix(string prefix);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    void Clear();

    #endregion

    #region Statistics

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();

    /// <summary>
    /// Resets cache statistics.
    /// </summary>
    void ResetStatistics();

    #endregion
}

#region Models

/// <summary>
/// Cache statistics for monitoring hit/miss ratios.
/// </summary>
public class CacheStatistics
{
    /// <summary>Number of cache hits.</summary>
    public long Hits { get; set; }
    
    /// <summary>Number of cache misses.</summary>
    public long Misses { get; set; }
    
    /// <summary>Total number of get operations.</summary>
    public long TotalRequests => Hits + Misses;
    
    /// <summary>Hit ratio (0-1).</summary>
    public double HitRatio => TotalRequests > 0 ? (double)Hits / TotalRequests : 0;
    
    /// <summary>Current number of items in cache.</summary>
    public int ItemCount { get; set; }
    
    /// <summary>Number of items added since last reset.</summary>
    public long ItemsAdded { get; set; }
    
    /// <summary>Number of items removed since last reset.</summary>
    public long ItemsRemoved { get; set; }
    
    /// <summary>Number of items evicted (expired) since last reset.</summary>
    public long ItemsEvicted { get; set; }
    
    /// <summary>Time when statistics were last reset.</summary>
    public DateTime LastReset { get; set; } = DateTime.UtcNow;
    
    /// <summary>Estimated memory usage in bytes (if available).</summary>
    public long? EstimatedMemoryBytes { get; set; }
}

/// <summary>
/// Cache configuration settings.
/// </summary>
public class CacheSettings
{
    /// <summary>Default TTL when none specified (default: 5 minutes).</summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>TTL for pull list queries (default: 5 minutes).</summary>
    public TimeSpan PullListTtl { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>TTL for series list queries (default: 2 minutes).</summary>
    public TimeSpan SeriesListTtl { get; set; } = TimeSpan.FromMinutes(2);
    
    /// <summary>TTL for series detail (default: 5 minutes).</summary>
    public TimeSpan SeriesDetailTtl { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>TTL for dashboard stats (default: 1 minute).</summary>
    public TimeSpan DashboardStatsTtl { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>TTL for ComicVine API responses (default: 30 minutes).</summary>
    public TimeSpan ComicVineApiTtl { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>Whether caching is enabled (for debugging).</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Maximum number of items to cache (0 = unlimited).</summary>
    public int MaxItems { get; set; } = 10000;
    
    /// <summary>Whether to track detailed statistics (slight performance overhead).</summary>
    public bool TrackStatistics { get; set; } = true;
}

/// <summary>
/// Well-known cache key prefixes for consistency.
/// </summary>
public static class CacheKeys
{
    public const string PullList = "pulllist";
    public const string PullListWeek = "pulllist:week";
    public const string PullListUpcoming = "pulllist:upcoming";
    public const string PullListPast = "pulllist:past";
    public const string PullListDiscovery = "pulllist:discovery";
    
    public const string Series = "series";
    public const string SeriesList = "series:list";
    public const string SeriesDetail = "series:detail";
    
    public const string Issue = "issue";
    public const string IssueList = "issue:list";
    
    public const string Dashboard = "dashboard";
    public const string DashboardStats = "dashboard:stats";
    public const string DashboardThisWeek = "dashboard:thisweek";
    
    public const string ComicVine = "comicvine";
    public const string ComicVineSearch = "comicvine:search";
    public const string ComicVineVolume = "comicvine:volume";
    public const string ComicVineIssue = "comicvine:issue";
}

#endregion
