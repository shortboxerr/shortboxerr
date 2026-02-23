using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Service for managing cover images with disk-based caching.
/// </summary>
public class CoverService : ICoverService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<CoverService> _logger;
    private readonly SemaphoreSlim _downloadSemaphore;

    // Thread-safe access statistics tracking
    private long _hits;
    private long _misses;
    private long _fallbacks;
    private long _placeholders;
    private long _bandwidthSaved;
    private DateTime _statsLastReset = DateTime.UtcNow;

    // Cache warming status
    private readonly object _warmingLock = new();
    private CacheWarmingStatus _warmingStatus = new();

    private const string PlaceholderFileName = "placeholder.png";
    private static readonly byte[] PlaceholderPng = CreatePlaceholderPng();
    private const long EstimatedAverageCoverSize = 50 * 1024; // 50KB average cover size estimate

    public CoverService(
        ShortboxerrDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<CoverService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _logger = logger;
        _downloadSemaphore = new SemaphoreSlim(3, 3); // Default concurrent limit
    }

    public async Task<CoverResult> GetSeriesCoverAsync(
        int seriesId, 
        CoverSize size = CoverSize.Medium, 
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null)
        {
            return CoverResult.NotFound($"Series {seriesId} not found");
        }

        var settings = await GetSettingsAsync(cancellationToken);

        // Check cache first
        var cachedPath = GetCachePath(settings.CacheDirectory, CoverType.Series, seriesId, size);
        if (File.Exists(cachedPath))
        {
            // Check if revalidation is needed
            if (settings.EnableRevalidation && !string.IsNullOrEmpty(series.CoverImageUrl))
            {
                var revalidationResult = await TryRevalidateAsync(
                    cachedPath, 
                    GetSizedUrl(series.CoverImageUrl, size), 
                    settings, 
                    CoverType.Series, 
                    seriesId, 
                    size, 
                    cancellationToken);
                
                if (revalidationResult != null)
                {
                    return revalidationResult;
                }
            }
            
            // Cache hit (no revalidation needed or revalidation confirmed unchanged)
            Interlocked.Increment(ref _hits);
            var fileInfo = new FileInfo(cachedPath);
            Interlocked.Add(ref _bandwidthSaved, fileInfo.Length);
            
            // Update last access time for LRU tracking
            TouchFile(cachedPath);
            return CreateCoverResult(cachedPath, CoverType.Series, seriesId, size, series.CoverImageUrl);
        }

        // No cached cover, try to download (cache miss)
        if (string.IsNullOrEmpty(series.CoverImageUrl))
        {
            Interlocked.Increment(ref _placeholders);
            return CoverResult.Placeholder(await EnsurePlaceholderAsync(cancellationToken));
        }

        Interlocked.Increment(ref _misses);
        var sizedUrl = GetSizedUrl(series.CoverImageUrl, size);
        return await DownloadCoverAsync(sizedUrl, CoverType.Series, seriesId, size, cancellationToken);
    }

    public async Task<CoverResult> GetIssueCoverAsync(
        int issueId, 
        CoverSize size = CoverSize.Medium, 
        CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.Series)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return CoverResult.NotFound($"Issue {issueId} not found");
        }

        var settings = await GetSettingsAsync(cancellationToken);

        // Check cache first
        var cachedPath = GetCachePath(settings.CacheDirectory, CoverType.Issue, issueId, size);
        if (File.Exists(cachedPath))
        {
            // Check if revalidation is needed
            if (settings.EnableRevalidation && !string.IsNullOrEmpty(issue.CoverImageUrl))
            {
                var revalidationResult = await TryRevalidateAsync(
                    cachedPath, 
                    GetSizedUrl(issue.CoverImageUrl, size), 
                    settings, 
                    CoverType.Issue, 
                    issueId, 
                    size, 
                    cancellationToken);
                
                if (revalidationResult != null)
                {
                    return revalidationResult;
                }
            }
            
            // Cache hit (no revalidation needed or revalidation confirmed unchanged)
            Interlocked.Increment(ref _hits);
            var fileInfo = new FileInfo(cachedPath);
            Interlocked.Add(ref _bandwidthSaved, fileInfo.Length);
            
            // Update last access time for LRU tracking
            TouchFile(cachedPath);
            return CreateCoverResult(cachedPath, CoverType.Issue, issueId, size, issue.CoverImageUrl);
        }

        // Try to download issue cover (cache miss)
        if (!string.IsNullOrEmpty(issue.CoverImageUrl))
        {
            Interlocked.Increment(ref _misses);
            var sizedUrl = GetSizedUrl(issue.CoverImageUrl, size);
            var result = await DownloadCoverAsync(sizedUrl, CoverType.Issue, issueId, size, cancellationToken);
            if (result.Success)
            {
                return result;
            }
            _logger.LogWarning("Failed to download issue cover for {IssueId}, falling back to series cover: {Error}", 
                issueId, result.Error);
        }

        // Fallback to series cover
        if (issue.Series != null && !string.IsNullOrEmpty(issue.Series.CoverImageUrl))
        {
            var seriesCoverPath = GetCachePath(settings.CacheDirectory, CoverType.Series, issue.SeriesId, size);
            
            // Check if series cover is cached (fallback hit)
            if (File.Exists(seriesCoverPath))
            {
                Interlocked.Increment(ref _fallbacks);
                var fileInfo = new FileInfo(seriesCoverPath);
                Interlocked.Add(ref _bandwidthSaved, fileInfo.Length);
                
                // Update last access time for LRU tracking
                TouchFile(seriesCoverPath);
                var fallbackResult = CreateCoverResult(seriesCoverPath, CoverType.Issue, issueId, size, issue.Series.CoverImageUrl);
                fallbackResult.IsFallback = true;
                return fallbackResult;
            }

            // Try to download series cover as fallback (fallback miss)
            Interlocked.Increment(ref _fallbacks);
            var sizedUrl = GetSizedUrl(issue.Series.CoverImageUrl, size);
            var seriesResult = await DownloadCoverAsync(sizedUrl, CoverType.Series, issue.SeriesId, size, cancellationToken);
            if (seriesResult.Success)
            {
                seriesResult.IsFallback = true;
                seriesResult.CoverType = CoverType.Issue;
                seriesResult.EntityId = issueId;
                return seriesResult;
            }
        }

        // Final fallback: placeholder
        Interlocked.Increment(ref _placeholders);
        return CoverResult.Placeholder(await EnsurePlaceholderAsync(cancellationToken));
    }

    public async Task<CoverResult> DownloadCoverAsync(
        string url, 
        CoverType type, 
        int entityId, 
        CoverSize size = CoverSize.Medium, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
        {
            return CoverResult.NotFound("No URL provided");
        }

        await _downloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            var settings = await GetSettingsAsync(cancellationToken);
            var cachePath = GetCachePath(settings.CacheDirectory, type, entityId, size);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var client = _httpClientFactory.CreateClient("CoverDownload");
            client.Timeout = TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            _logger.LogDebug("Downloading cover from {Url} to {Path}", url, cachePath);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return CoverResult.NotFound($"Failed to download cover: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            
            // Validate content type
            if (!contentType.StartsWith("image/"))
            {
                return CoverResult.NotFound($"Invalid content type: {contentType}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(cachePath);
            await stream.CopyToAsync(fileStream, cancellationToken);

            // Save metadata for revalidation
            await SaveCoverMetadataAsync(cachePath, url, response, cancellationToken);

            _logger.LogInformation("Downloaded cover for {Type} {Id} ({Size}): {Path}", 
                type, entityId, size, cachePath);

            // Trigger auto-cleanup if enabled and might be over limit
            if (settings.AutoCleanupEnabled && settings.MaxCacheSizeBytes > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await EnforceCacheLimitAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Background cache limit enforcement failed");
                    }
                });
            }

            return CreateCoverResult(cachePath, type, entityId, size, url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download cover from {Url}", url);
            return CoverResult.NotFound($"Download failed: {ex.Message}");
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    public async Task ClearSeriesCoverCacheAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var basePath = Path.Combine(settings.CacheDirectory, "series", seriesId.ToString());
        
        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, recursive: true);
            _logger.LogInformation("Cleared cover cache for series {SeriesId}", seriesId);
        }
    }

    public async Task ClearIssueCoverCacheAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var basePath = Path.Combine(settings.CacheDirectory, "issues", issueId.ToString());
        
        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, recursive: true);
            _logger.LogInformation("Cleared cover cache for issue {IssueId}", issueId);
        }
    }

    public async Task<CoverCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var stats = new CoverCacheStats();

        if (!Directory.Exists(settings.CacheDirectory))
        {
            return stats;
        }

        var files = Directory.GetFiles(settings.CacheDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(PlaceholderFileName))
            .Select(f => new FileInfo(f))
            .ToList();

        stats.TotalCovers = files.Count;
        stats.TotalSizeBytes = files.Sum(f => f.Length);

        var seriesDir = Path.Combine(settings.CacheDirectory, "series");
        if (Directory.Exists(seriesDir))
        {
            stats.SeriesCovers = Directory.GetFiles(seriesDir, "*.*", SearchOption.AllDirectories).Length;
        }

        var issuesDir = Path.Combine(settings.CacheDirectory, "issues");
        if (Directory.Exists(issuesDir))
        {
            stats.IssueCovers = Directory.GetFiles(issuesDir, "*.*", SearchOption.AllDirectories).Length;
        }

        var editionsDir = Path.Combine(settings.CacheDirectory, "editions");
        if (Directory.Exists(editionsDir))
        {
            stats.EditionCovers = Directory.GetFiles(editionsDir, "*.*", SearchOption.AllDirectories).Length;
        }

        if (files.Count > 0)
        {
            stats.OldestCover = files.Min(f => f.CreationTimeUtc);
            stats.NewestCover = files.Max(f => f.CreationTimeUtc);
        }

        return stats;
    }

    public async Task ClearAllCacheAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (Directory.Exists(settings.CacheDirectory))
        {
            // Delete all subdirectories but preserve the placeholder
            var subdirs = Directory.GetDirectories(settings.CacheDirectory);
            foreach (var dir in subdirs)
            {
                Directory.Delete(dir, recursive: true);
            }
            _logger.LogInformation("Cleared all cover cache");
        }
    }

    public async Task<DetailedCoverCacheStats> GetDetailedCacheStatsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var stats = new DetailedCoverCacheStats
        {
            MaxCacheSizeBytes = settings.MaxCacheSizeBytes
        };

        if (!Directory.Exists(settings.CacheDirectory))
        {
            return stats;
        }

        var files = Directory.GetFiles(settings.CacheDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(PlaceholderFileName))
            .Select(f => new FileInfo(f))
            .ToList();

        stats.TotalCovers = files.Count;
        stats.TotalSizeBytes = files.Sum(f => f.Length);

        // Count by type
        var seriesDir = Path.Combine(settings.CacheDirectory, "series");
        if (Directory.Exists(seriesDir))
        {
            stats.SeriesCovers = Directory.GetFiles(seriesDir, "*.*", SearchOption.AllDirectories).Length;
        }

        var issuesDir = Path.Combine(settings.CacheDirectory, "issues");
        if (Directory.Exists(issuesDir))
        {
            stats.IssueCovers = Directory.GetFiles(issuesDir, "*.*", SearchOption.AllDirectories).Length;
        }

        var editionsDir = Path.Combine(settings.CacheDirectory, "editions");
        if (Directory.Exists(editionsDir))
        {
            stats.EditionCovers = Directory.GetFiles(editionsDir, "*.*", SearchOption.AllDirectories).Length;
        }

        if (files.Count > 0)
        {
            stats.OldestCover = files.Min(f => f.CreationTimeUtc);
            stats.NewestCover = files.Max(f => f.CreationTimeUtc);
        }

        // Breakdown by size
        stats.BySize = new Dictionary<CoverSize, CoverSizeStats>
        {
            [CoverSize.Thumb] = GetSizeStats(files, "thumb"),
            [CoverSize.Small] = GetSizeStats(files, "small"),
            [CoverSize.Medium] = GetSizeStats(files, "medium"),
            [CoverSize.Large] = GetSizeStats(files, "large")
        };

        // Calculate pending eviction count if over limit
        if (stats.IsOverLimit)
        {
            var targetSize = (long)(settings.MaxCacheSizeBytes * settings.CleanupTargetPercent / 100.0);
            var bytesToFree = stats.TotalSizeBytes - targetSize;
            
            var sortedByAccess = files.OrderBy(f => f.LastAccessTimeUtc).ToList();
            long accumulated = 0;
            int evictionCount = 0;
            
            foreach (var file in sortedByAccess)
            {
                accumulated += file.Length;
                evictionCount++;
                if (accumulated >= bytesToFree)
                    break;
            }
            
            stats.PendingEvictionCount = evictionCount;
        }

        // Get last cleanup info from settings
        stats.LastCleanupAt = await _settingsService.GetAsync<DateTime?>("covers_last_cleanup", null, cancellationToken);
        stats.LastCleanupEvictedCount = await _settingsService.GetAsync<int>("covers_last_cleanup_count", 0, cancellationToken);

        // Include access statistics
        stats.AccessStats = GetAccessStats();

        return stats;
    }

    public CoverCacheAccessStats GetAccessStats()
    {
        return new CoverCacheAccessStats
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Fallbacks = Interlocked.Read(ref _fallbacks),
            Placeholders = Interlocked.Read(ref _placeholders),
            EstimatedBandwidthSavedBytes = Interlocked.Read(ref _bandwidthSaved),
            LastReset = _statsLastReset
        };
    }

    public void ResetAccessStats()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _fallbacks, 0);
        Interlocked.Exchange(ref _placeholders, 0);
        Interlocked.Exchange(ref _bandwidthSaved, 0);
        _statsLastReset = DateTime.UtcNow;
        
        _logger.LogInformation("Cover cache access statistics reset");
    }

    private static CoverSizeStats GetSizeStats(List<FileInfo> files, string sizeDirName)
    {
        var sizeFiles = files.Where(f => f.DirectoryName?.Contains(Path.DirectorySeparatorChar + sizeDirName + Path.DirectorySeparatorChar) == true 
                                        || f.Name.StartsWith(sizeDirName + ".", StringComparison.OrdinalIgnoreCase)).ToList();
        
        return new CoverSizeStats
        {
            Size = sizeDirName switch
            {
                "thumb" => CoverSize.Thumb,
                "small" => CoverSize.Small,
                "medium" => CoverSize.Medium,
                "large" => CoverSize.Large,
                _ => CoverSize.Medium
            },
            Count = sizeFiles.Count,
            TotalBytes = sizeFiles.Sum(f => f.Length)
        };
    }

    public async Task<CoverCleanupResult> CleanupCacheAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new CoverCleanupResult { Success = true };
        
        try
        {
            var settings = await GetSettingsAsync(cancellationToken);
            
            if (!Directory.Exists(settings.CacheDirectory))
            {
                return result;
            }

            var files = Directory.GetFiles(settings.CacheDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(PlaceholderFileName))
                .Select(f => new FileInfo(f))
                .ToList();

            result.SizeBefore = files.Sum(f => f.Length);

            // Step 1: Remove expired covers (retention policy)
            if (settings.RetentionDays > 0)
            {
                var expirationDate = DateTime.UtcNow.AddDays(-settings.RetentionDays);
                var expiredFiles = files.Where(f => f.CreationTimeUtc < expirationDate).ToList();
                
                foreach (var file in expiredFiles)
                {
                    try
                    {
                        file.Delete();
                        result.EvictedByRetention++;
                        result.BytesFreed += file.Length;
                        _logger.LogDebug("Evicted expired cover: {Path}", file.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete expired cover: {Path}", file.FullName);
                    }
                }

                // Update file list after retention cleanup
                files = files.Where(f => f.Exists).ToList();
            }

            // Step 2: Enforce size limit via LRU eviction
            var lruResult = await EnforceCacheLimitInternalAsync(settings, files, cancellationToken);
            result.EvictedByLru = lruResult.EvictedByLru;
            result.BytesFreed += lruResult.BytesFreed;

            result.SizeAfter = result.SizeBefore - result.BytesFreed;
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Store cleanup info
            await _settingsService.SetAsync("covers_last_cleanup", DateTime.UtcNow, cancellationToken);
            await _settingsService.SetAsync("covers_last_cleanup_count", result.TotalEvicted, cancellationToken);

            _logger.LogInformation(
                "Cover cache cleanup completed: evicted {Total} covers ({ByRetention} expired, {ByLru} LRU), freed {Bytes} bytes in {Duration}ms",
                result.TotalEvicted, result.EvictedByRetention, result.EvictedByLru, 
                result.BytesFreed, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cover cache cleanup failed");
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.Duration = stopwatch.Elapsed;
            return result;
        }
    }

    public async Task<CoverCleanupResult> EnforceCacheLimitAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!Directory.Exists(settings.CacheDirectory))
        {
            return new CoverCleanupResult { Success = true };
        }

        var files = Directory.GetFiles(settings.CacheDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(PlaceholderFileName))
            .Select(f => new FileInfo(f))
            .ToList();

        return await EnforceCacheLimitInternalAsync(settings, files, cancellationToken);
    }

    private Task<CoverCleanupResult> EnforceCacheLimitInternalAsync(
        CoverSettings settings, 
        List<FileInfo> files, 
        CancellationToken cancellationToken)
    {
        var result = new CoverCleanupResult { Success = true };

        if (settings.MaxCacheSizeBytes <= 0)
        {
            return Task.FromResult(result); // No limit configured
        }

        var currentSize = files.Sum(f => f.Length);
        result.SizeBefore = currentSize;

        if (currentSize <= settings.MaxCacheSizeBytes)
        {
            result.SizeAfter = currentSize;
            return Task.FromResult(result); // Under limit
        }

        // Calculate target size (cleanup to target percent of max)
        var targetSize = (long)(settings.MaxCacheSizeBytes * settings.CleanupTargetPercent / 100.0);
        var bytesToFree = currentSize - targetSize;

        _logger.LogInformation(
            "Cache over limit: {Current} bytes > {Max} bytes. Evicting to reach {Target} bytes",
            currentSize, settings.MaxCacheSizeBytes, targetSize);

        // Sort by last access time (LRU - least recently used first)
        var sortedByAccess = files.OrderBy(f => f.LastAccessTimeUtc).ToList();

        long freedBytes = 0;
        foreach (var file in sortedByAccess)
        {
            if (freedBytes >= bytesToFree)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileSize = file.Length;
                file.Delete();
                freedBytes += fileSize;
                result.EvictedByLru++;
                _logger.LogDebug("LRU evicted: {Path} (last access: {LastAccess})", 
                    file.FullName, file.LastAccessTimeUtc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cover during LRU eviction: {Path}", file.FullName);
            }
        }

        result.BytesFreed = freedBytes;
        result.SizeAfter = currentSize - freedBytes;

        _logger.LogInformation("LRU eviction completed: evicted {Count} covers, freed {Bytes} bytes", 
            result.EvictedByLru, freedBytes);

        return Task.FromResult(result);
    }

    public string GetPlaceholderPath()
    {
        // Return a path in the cache directory, will be created on first access
        return Path.Combine("covers", PlaceholderFileName);
    }

    #region Private Methods

    private static void TouchFile(string path)
    {
        try
        {
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
        }
        catch
        {
            // Ignore errors updating access time - not critical
        }
    }

    private async Task<CoverSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        return await _settingsService.GetAsync<CoverSettings>("covers", new CoverSettings(), cancellationToken) 
            ?? new CoverSettings();
    }

    #region Revalidation

    private static string GetMetadataPath(string coverPath)
    {
        return coverPath + ".meta.json";
    }

    private async Task<CoverCacheMetadata?> LoadCoverMetadataAsync(string coverPath, CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(coverPath);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<CoverCacheMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cover metadata from {Path}", metadataPath);
            return null;
        }
    }

    private async Task SaveCoverMetadataAsync(string coverPath, string sourceUrl, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var metadata = new CoverCacheMetadata
        {
            SourceUrl = sourceUrl,
            DownloadedAt = DateTime.UtcNow,
            LastValidatedAt = DateTime.UtcNow,
            FileSize = new FileInfo(coverPath).Length
        };

        // Extract ETag (remove quotes if present)
        if (response.Headers.ETag != null)
        {
            metadata.ETag = response.Headers.ETag.Tag?.Trim('"');
        }

        // Extract Last-Modified
        if (response.Content.Headers.LastModified.HasValue)
        {
            metadata.LastModified = response.Content.Headers.LastModified.Value.UtcDateTime;
        }

        var metadataPath = GetMetadataPath(coverPath);
        try
        {
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cover metadata to {Path}", metadataPath);
        }
    }

    private async Task UpdateMetadataValidationTimeAsync(string coverPath, CancellationToken cancellationToken)
    {
        var metadata = await LoadCoverMetadataAsync(coverPath, cancellationToken);
        if (metadata != null)
        {
            metadata.LastValidatedAt = DateTime.UtcNow;
            var metadataPath = GetMetadataPath(coverPath);
            try
            {
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cover metadata validation time at {Path}", metadataPath);
            }
        }
    }

    /// <summary>
    /// Attempts to revalidate a cached cover. Returns null if no revalidation is needed or if
    /// the cached file is still valid. Returns a new CoverResult if the file was re-downloaded.
    /// </summary>
    private async Task<CoverResult?> TryRevalidateAsync(
        string cachedPath, 
        string url, 
        CoverSettings settings, 
        CoverType type, 
        int entityId, 
        CoverSize size, 
        CancellationToken cancellationToken)
    {
        var metadata = await LoadCoverMetadataAsync(cachedPath, cancellationToken);
        
        // If no metadata or revalidation interval hasn't passed, skip revalidation
        if (metadata == null)
        {
            return null;
        }

        var hoursSinceValidation = (DateTime.UtcNow - metadata.LastValidatedAt).TotalHours;
        if (hoursSinceValidation < settings.RevalidationIntervalHours)
        {
            return null; // Recently validated, no need to check again
        }

        // Perform conditional GET
        try
        {
            using var client = _httpClientFactory.CreateClient("CoverDownload");
            client.Timeout = TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds);
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            
            // Add conditional headers
            if (!string.IsNullOrEmpty(metadata.ETag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{metadata.ETag}\"");
            }
            if (metadata.LastModified.HasValue)
            {
                request.Headers.IfModifiedSince = new DateTimeOffset(metadata.LastModified.Value);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // Cover hasn't changed, update validation time
                _logger.LogDebug("Cover revalidation: {Path} unchanged (304)", cachedPath);
                await UpdateMetadataValidationTimeAsync(cachedPath, cancellationToken);
                return null; // Use cached version
            }

            if (response.IsSuccessStatusCode)
            {
                // Cover has changed, re-download
                _logger.LogInformation("Cover revalidation: {Path} changed, re-downloading", cachedPath);
                
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                if (!contentType.StartsWith("image/"))
                {
                    _logger.LogWarning("Cover revalidation: invalid content type {ContentType}", contentType);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(cachedPath);
                await stream.CopyToAsync(fileStream, cancellationToken);

                await SaveCoverMetadataAsync(cachedPath, url, response, cancellationToken);

                // Count as a cache miss since we had to re-download
                Interlocked.Increment(ref _misses);
                
                return CreateCoverResult(cachedPath, type, entityId, size, url);
            }

            _logger.LogWarning("Cover revalidation failed with status {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cover revalidation failed for {Path}", cachedPath);
        }

        // On error, use cached version
        return null;
    }

    #endregion

    private static string GetCachePath(string cacheDirectory, CoverType type, int entityId, CoverSize size)
    {
        var typeDir = type switch
        {
            CoverType.Series => "series",
            CoverType.Issue => "issues",
            CoverType.Edition => "editions",
            _ => "other"
        };

        var sizeDir = size switch
        {
            CoverSize.Thumb => "thumb",
            CoverSize.Small => "small",
            CoverSize.Medium => "medium",
            CoverSize.Large => "large",
            _ => "medium"
        };

        return Path.Combine(cacheDirectory, typeDir, entityId.ToString(), $"{sizeDir}.jpg");
    }

    private static string GetSizedUrl(string originalUrl, CoverSize size)
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        // ComicVine URLs contain size indicators like /scale_small/, /scale_medium/, etc.
        // We need to replace these with the appropriate size
        var sizeSegment = size switch
        {
            CoverSize.Thumb => "scale_avatar",
            CoverSize.Small => "scale_small",
            CoverSize.Medium => "scale_medium",
            CoverSize.Large => "original",
            _ => "scale_medium"
        };

        // Common patterns in ComicVine URLs
        var patterns = new[] { "scale_avatar", "scale_small", "scale_medium", "scale_large", "original" };
        
        foreach (var pattern in patterns)
        {
            if (originalUrl.Contains(pattern))
            {
                return originalUrl.Replace(pattern, sizeSegment);
            }
        }

        // If no pattern found, return original
        return originalUrl;
    }

    private static CoverResult CreateCoverResult(string path, CoverType type, int entityId, CoverSize size, string? sourceUrl)
    {
        var fileInfo = new FileInfo(path);
        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        return new CoverResult
        {
            Success = true,
            FilePath = path,
            ContentType = contentType,
            CoverType = type,
            EntityId = entityId,
            Size = size,
            SourceUrl = sourceUrl,
            FileSize = fileInfo.Exists ? fileInfo.Length : null,
            CachedAt = fileInfo.Exists ? fileInfo.CreationTimeUtc : null
        };
    }

    private async Task<string> EnsurePlaceholderAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var placeholderPath = Path.Combine(settings.CacheDirectory, PlaceholderFileName);

        if (!File.Exists(placeholderPath))
        {
            var directory = Path.GetDirectoryName(placeholderPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(placeholderPath, PlaceholderPng, cancellationToken);
            _logger.LogDebug("Created placeholder image at {Path}", placeholderPath);
        }

        return placeholderPath;
    }

    /// <summary>
    /// Creates a simple gray placeholder PNG image.
    /// </summary>
    private static byte[] CreatePlaceholderPng()
    {
        // Minimal valid 1x1 gray PNG (67 bytes)
        // This is a pre-computed PNG for simplicity
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, // width = 1
            0x00, 0x00, 0x00, 0x01, // height = 1
            0x08, 0x02, // 8-bit RGB
            0x00, 0x00, 0x00, // compression, filter, interlace
            0x90, 0x77, 0x53, 0xDE, // CRC
            0x00, 0x00, 0x00, 0x0C, // IDAT length
            0x49, 0x44, 0x41, 0x54, // IDAT
            0x08, 0xD7, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, // compressed data (gray pixel)
            0x00, 0x04, 0x00, 0x01, // Adler32
            0x27, 0x34, 0x27, 0x0A, // CRC
            0x00, 0x00, 0x00, 0x00, // IEND length
            0x49, 0x45, 0x4E, 0x44, // IEND
            0xAE, 0x42, 0x60, 0x82  // CRC
        };
    }

    #endregion

    #region Cache Warming

    public async Task<CacheWarmingResult> WarmSeriesCacheAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        return await WarmCacheAsync(new[] { seriesId }, cancellationToken);
    }

    public async Task<CacheWarmingResult> WarmCacheAsync(IEnumerable<int> seriesIds, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new CacheWarmingResult { Success = true };
        var seriesIdList = seriesIds.ToList();

        if (seriesIdList.Count == 0)
        {
            return result;
        }

        var settings = await GetSettingsAsync(cancellationToken);
        var sizesToWarm = ParseWarmCacheSizes(settings.WarmCacheSizes);

        if (sizesToWarm.Count == 0)
        {
            sizesToWarm.Add(settings.DefaultSize);
        }

        try
        {
            // Get all issues for the series
            var issues = await _dbContext.Issues
                .Where(i => seriesIdList.Contains(i.SeriesId))
                .Include(i => i.Series)
                .ToListAsync(cancellationToken);

            var totalCovers = issues.Count * sizesToWarm.Count + seriesIdList.Count * sizesToWarm.Count;

            // Update warming status
            lock (_warmingLock)
            {
                _warmingStatus = new CacheWarmingStatus
                {
                    IsWarming = true,
                    TotalSeries = seriesIdList.Count,
                    TotalCovers = totalCovers,
                    StartedAt = DateTime.UtcNow
                };
            }

            // Warm series covers first
            foreach (var seriesId in seriesIdList)
            {
                var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
                if (series == null) continue;

                lock (_warmingLock)
                {
                    _warmingStatus.CurrentSeries = series.Title;
                }

                foreach (var size in sizesToWarm)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cachedPath = GetCachePath(settings.CacheDirectory, CoverType.Series, seriesId, size);
                    if (File.Exists(cachedPath))
                    {
                        result.CoversAlreadyCached++;
                    }
                    else if (!string.IsNullOrEmpty(series.CoverImageUrl))
                    {
                        var coverResult = await GetSeriesCoverAsync(seriesId, size, cancellationToken);
                        if (coverResult.Success)
                        {
                            result.CoversDownloaded++;
                            if (coverResult.FilePath != null && File.Exists(coverResult.FilePath))
                            {
                                result.BytesDownloaded += new FileInfo(coverResult.FilePath).Length;
                            }
                        }
                        else
                        {
                            result.FailedDownloads++;
                        }
                    }

                    lock (_warmingLock)
                    {
                        _warmingStatus.ProcessedCovers++;
                        UpdateEstimatedRemaining(stopwatch.Elapsed);
                    }
                }

                lock (_warmingLock)
                {
                    _warmingStatus.ProcessedSeries++;
                }

                result.SeriesProcessed++;
            }

            // Warm issue covers
            foreach (var issue in issues)
            {
                lock (_warmingLock)
                {
                    _warmingStatus.CurrentSeries = $"{issue.Series?.Title} #{issue.IssueNumber}";
                }

                foreach (var size in sizesToWarm)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cachedPath = GetCachePath(settings.CacheDirectory, CoverType.Issue, issue.Id, size);
                    if (File.Exists(cachedPath))
                    {
                        result.CoversAlreadyCached++;
                    }
                    else if (!string.IsNullOrEmpty(issue.CoverImageUrl))
                    {
                        var coverResult = await GetIssueCoverAsync(issue.Id, size, cancellationToken);
                        if (coverResult.Success)
                        {
                            result.CoversDownloaded++;
                            if (coverResult.FilePath != null && File.Exists(coverResult.FilePath))
                            {
                                result.BytesDownloaded += new FileInfo(coverResult.FilePath).Length;
                            }
                        }
                        else
                        {
                            result.FailedDownloads++;
                        }
                    }

                    lock (_warmingLock)
                    {
                        _warmingStatus.ProcessedCovers++;
                        UpdateEstimatedRemaining(stopwatch.Elapsed);
                    }
                }
            }

            _logger.LogInformation(
                "Cache warming completed: {SeriesCount} series, {Downloaded} downloaded, {Cached} already cached, {Failed} failed, {Bytes} bytes",
                result.SeriesProcessed, result.CoversDownloaded, result.CoversAlreadyCached, result.FailedDownloads, result.BytesDownloaded);
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Warming operation was cancelled";
            _logger.LogWarning("Cache warming cancelled");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Error during cache warming");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            lock (_warmingLock)
            {
                _warmingStatus = new CacheWarmingStatus { IsWarming = false };
            }
        }

        return result;
    }

    public CacheWarmingStatus GetWarmingStatus()
    {
        lock (_warmingLock)
        {
            return new CacheWarmingStatus
            {
                IsWarming = _warmingStatus.IsWarming,
                TotalSeries = _warmingStatus.TotalSeries,
                ProcessedSeries = _warmingStatus.ProcessedSeries,
                TotalCovers = _warmingStatus.TotalCovers,
                ProcessedCovers = _warmingStatus.ProcessedCovers,
                StartedAt = _warmingStatus.StartedAt,
                EstimatedRemaining = _warmingStatus.EstimatedRemaining,
                CurrentSeries = _warmingStatus.CurrentSeries
            };
        }
    }

    private void UpdateEstimatedRemaining(TimeSpan elapsed)
    {
        if (_warmingStatus.ProcessedCovers > 0 && _warmingStatus.TotalCovers > _warmingStatus.ProcessedCovers)
        {
            var avgTimePerCover = elapsed.TotalMilliseconds / _warmingStatus.ProcessedCovers;
            var remainingCovers = _warmingStatus.TotalCovers - _warmingStatus.ProcessedCovers;
            _warmingStatus.EstimatedRemaining = TimeSpan.FromMilliseconds(avgTimePerCover * remainingCovers);
        }
    }

    private static List<CoverSize> ParseWarmCacheSizes(string? sizesString)
    {
        var sizes = new List<CoverSize>();
        if (string.IsNullOrWhiteSpace(sizesString))
        {
            return sizes;
        }

        foreach (var part in sizesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<CoverSize>(part, true, out var size))
            {
                sizes.Add(size);
            }
        }

        return sizes;
    }

    #endregion
}

