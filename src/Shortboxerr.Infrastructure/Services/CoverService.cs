using System.Net.Http.Headers;
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

    private const string PlaceholderFileName = "placeholder.png";
    private static readonly byte[] PlaceholderPng = CreatePlaceholderPng();

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
            return CreateCoverResult(cachedPath, CoverType.Series, seriesId, size, series.CoverImageUrl);
        }

        // No cached cover, try to download
        if (string.IsNullOrEmpty(series.CoverImageUrl))
        {
            return CoverResult.Placeholder(await EnsurePlaceholderAsync(cancellationToken));
        }

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
            return CreateCoverResult(cachedPath, CoverType.Issue, issueId, size, issue.CoverImageUrl);
        }

        // Try to download issue cover
        if (!string.IsNullOrEmpty(issue.CoverImageUrl))
        {
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
            
            // Check if series cover is cached
            if (File.Exists(seriesCoverPath))
            {
                var fallbackResult = CreateCoverResult(seriesCoverPath, CoverType.Issue, issueId, size, issue.Series.CoverImageUrl);
                fallbackResult.IsFallback = true;
                return fallbackResult;
            }

            // Try to download series cover as fallback
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

            _logger.LogInformation("Downloaded cover for {Type} {Id} ({Size}): {Path}", 
                type, entityId, size, cachePath);

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

    public string GetPlaceholderPath()
    {
        // Return a path in the cache directory, will be created on first access
        return Path.Combine("covers", PlaceholderFileName);
    }

    #region Private Methods

    private async Task<CoverSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        return await _settingsService.GetAsync<CoverSettings>("covers", new CoverSettings(), cancellationToken) 
            ?? new CoverSettings();
    }

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
}

