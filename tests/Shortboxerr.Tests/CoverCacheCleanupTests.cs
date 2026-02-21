using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Shortboxerr.Tests;

public class CoverCacheCleanupTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<CoverService>> _mockLogger;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly CoverService _coverService;

    public CoverCacheCleanupTests()
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), $"shortboxerr_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testCacheDir);

        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<CoverService>>();

        _coverService = new CoverService(
            _dbContext,
            _mockHttpClientFactory.Object,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (Directory.Exists(_testCacheDir))
        {
            Directory.Delete(_testCacheDir, recursive: true);
        }
    }

    private void SetupSettings(CoverSettings settings)
    {
        _mockSettingsService
            .Setup(s => s.GetAsync<CoverSettings>("covers", It.IsAny<CoverSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockSettingsService
            .Setup(s => s.GetAsync<DateTime?>("covers_last_cleanup", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _mockSettingsService
            .Setup(s => s.GetAsync<int>("covers_last_cleanup_count", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private string CreateTestFile(string relativePath, int sizeKb = 100, DateTime? lastAccess = null)
    {
        var fullPath = Path.Combine(_testCacheDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var data = new byte[sizeKb * 1024];
        File.WriteAllBytes(fullPath, data);

        if (lastAccess.HasValue)
        {
            File.SetLastAccessTimeUtc(fullPath, lastAccess.Value);
        }

        return fullPath;
    }

    #region Settings Tests

    [Fact]
    public void CoverSettings_DefaultMaxCacheSize_Is500MB()
    {
        var settings = new CoverSettings();
        Assert.Equal(500 * 1024 * 1024, settings.MaxCacheSizeBytes);
    }

    [Fact]
    public void CoverSettings_DefaultCleanupTargetPercent_Is80()
    {
        var settings = new CoverSettings();
        Assert.Equal(80, settings.CleanupTargetPercent);
    }

    [Fact]
    public void CoverSettings_DefaultCleanupIntervalHours_Is24()
    {
        var settings = new CoverSettings();
        Assert.Equal(24, settings.CleanupIntervalHours);
    }

    [Fact]
    public void CoverSettings_DefaultAutoCleanupEnabled_IsTrue()
    {
        var settings = new CoverSettings();
        Assert.True(settings.AutoCleanupEnabled);
    }

    #endregion

    #region Detailed Stats Tests

    [Fact]
    public async Task GetDetailedCacheStatsAsync_EmptyCache_ReturnsZeros()
    {
        SetupSettings(new CoverSettings { CacheDirectory = _testCacheDir });

        var stats = await _coverService.GetDetailedCacheStatsAsync();

        Assert.Equal(0, stats.TotalCovers);
        Assert.Equal(0, stats.TotalSizeBytes);
        Assert.False(stats.IsOverLimit);
    }

    [Fact]
    public async Task GetDetailedCacheStatsAsync_WithFiles_CalculatesCorrectly()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 1024 * 1024 // 1MB limit
        });

        // Create test files (100KB each)
        CreateTestFile("series/1/medium.jpg");
        CreateTestFile("series/2/medium.jpg");
        CreateTestFile("issues/1/thumb.jpg");

        var stats = await _coverService.GetDetailedCacheStatsAsync();

        Assert.Equal(3, stats.TotalCovers);
        Assert.Equal(300 * 1024, stats.TotalSizeBytes);
        Assert.Equal(1024 * 1024, stats.MaxCacheSizeBytes);
        Assert.False(stats.IsOverLimit);
    }

    [Fact]
    public async Task GetDetailedCacheStatsAsync_OverLimit_CalculatesCorrectly()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 200 * 1024 // 200KB limit
        });

        // Create 300KB of files (over 200KB limit)
        CreateTestFile("series/1/medium.jpg", 100);
        CreateTestFile("series/2/medium.jpg", 100);
        CreateTestFile("series/3/medium.jpg", 100);

        var stats = await _coverService.GetDetailedCacheStatsAsync();

        Assert.True(stats.IsOverLimit);
        Assert.Equal(100 * 1024, stats.BytesOverLimit);
        Assert.True(stats.UsagePercent > 100);
    }

    [Fact]
    public async Task GetDetailedCacheStatsAsync_CalculatesPendingEvictionCount()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 200 * 1024, // 200KB limit
            CleanupTargetPercent = 50 // Target 100KB after cleanup
        });

        // Create 300KB of files
        CreateTestFile("series/1/medium.jpg", 100, DateTime.UtcNow.AddHours(-3)); // oldest
        CreateTestFile("series/2/medium.jpg", 100, DateTime.UtcNow.AddHours(-2));
        CreateTestFile("series/3/medium.jpg", 100, DateTime.UtcNow.AddHours(-1)); // newest

        var stats = await _coverService.GetDetailedCacheStatsAsync();

        Assert.True(stats.IsOverLimit);
        Assert.True(stats.PendingEvictionCount >= 2); // Need to evict 200KB to reach 100KB target
    }

    #endregion

    #region LRU Eviction Tests

    [Fact]
    public async Task EnforceCacheLimitAsync_UnderLimit_NoEviction()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 1024 * 1024 // 1MB limit
        });

        CreateTestFile("series/1/medium.jpg", 100); // 100KB < 1MB

        var result = await _coverService.EnforceCacheLimitAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.EvictedByLru);
        Assert.True(File.Exists(Path.Combine(_testCacheDir, "series/1/medium.jpg")));
    }

    [Fact]
    public async Task EnforceCacheLimitAsync_OverLimit_EvictsLeastRecentlyUsed()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 250 * 1024, // 250KB limit
            CleanupTargetPercent = 80 // Target 200KB
        });

        // Create files with different access times
        var oldestFile = CreateTestFile("series/1/medium.jpg", 100, DateTime.UtcNow.AddHours(-3));
        var middleFile = CreateTestFile("series/2/medium.jpg", 100, DateTime.UtcNow.AddHours(-2));
        var newestFile = CreateTestFile("series/3/medium.jpg", 100, DateTime.UtcNow.AddHours(-1));

        // 300KB > 250KB limit, need to evict to reach 200KB target (100KB to free)

        var result = await _coverService.EnforceCacheLimitAsync();

        Assert.True(result.Success);
        Assert.True(result.EvictedByLru >= 1);
        
        // Oldest file should be evicted first
        Assert.False(File.Exists(oldestFile));
        // Newest file should remain
        Assert.True(File.Exists(newestFile));
    }

    [Fact]
    public async Task EnforceCacheLimitAsync_NoLimit_NoEviction()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 0 // No limit
        });

        CreateTestFile("series/1/medium.jpg", 100);
        CreateTestFile("series/2/medium.jpg", 100);

        var result = await _coverService.EnforceCacheLimitAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.EvictedByLru);
    }

    #endregion

    #region Retention Policy Tests

    [Fact]
    public async Task CleanupCacheAsync_WithRetentionDays_EvictsExpiredCovers()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 0, // No size limit
            RetentionDays = 7 // 7 day retention
        });

        // Create old file (10 days old)
        var oldFilePath = CreateTestFile("series/1/medium.jpg", 100);
        File.SetCreationTimeUtc(oldFilePath, DateTime.UtcNow.AddDays(-10));

        // Create recent file (1 day old)
        var recentFilePath = CreateTestFile("series/2/medium.jpg", 100);
        File.SetCreationTimeUtc(recentFilePath, DateTime.UtcNow.AddDays(-1));

        var result = await _coverService.CleanupCacheAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.EvictedByRetention);
        Assert.False(File.Exists(oldFilePath));
        Assert.True(File.Exists(recentFilePath));
    }

    [Fact]
    public async Task CleanupCacheAsync_NoRetentionDays_NoRetentionEviction()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 0,
            RetentionDays = 0 // No retention limit
        });

        // Create old file (100 days old)
        var oldFilePath = CreateTestFile("series/1/medium.jpg", 100);
        File.SetCreationTimeUtc(oldFilePath, DateTime.UtcNow.AddDays(-100));

        var result = await _coverService.CleanupCacheAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.EvictedByRetention);
        Assert.True(File.Exists(oldFilePath));
    }

    #endregion

    #region Combined Cleanup Tests

    [Fact]
    public async Task CleanupCacheAsync_CombinesRetentionAndLru()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 150 * 1024, // 150KB limit
            CleanupTargetPercent = 66, // Target 100KB
            RetentionDays = 7
        });

        // Create expired file (retention)
        var expiredPath = CreateTestFile("series/1/medium.jpg", 100);
        File.SetCreationTimeUtc(expiredPath, DateTime.UtcNow.AddDays(-10));
        File.SetLastAccessTimeUtc(expiredPath, DateTime.UtcNow.AddHours(-1));

        // Create old accessed file (LRU candidate)
        var oldAccessPath = CreateTestFile("series/2/medium.jpg", 100);
        File.SetCreationTimeUtc(oldAccessPath, DateTime.UtcNow.AddDays(-1));
        File.SetLastAccessTimeUtc(oldAccessPath, DateTime.UtcNow.AddHours(-5));

        // Create recent file
        var recentPath = CreateTestFile("series/3/medium.jpg", 100);
        File.SetCreationTimeUtc(recentPath, DateTime.UtcNow.AddDays(-1));
        File.SetLastAccessTimeUtc(recentPath, DateTime.UtcNow);

        var result = await _coverService.CleanupCacheAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.EvictedByRetention); // expired file
        Assert.False(File.Exists(expiredPath));
        
        // After retention cleanup, we're at 200KB, still over 150KB limit
        // LRU should evict the old accessed file to reach ~100KB
        Assert.True(result.EvictedByLru >= 1);
    }

    [Fact]
    public async Task CleanupCacheAsync_ReturnsCorrectStats()
    {
        SetupSettings(new CoverSettings 
        { 
            CacheDirectory = _testCacheDir,
            MaxCacheSizeBytes = 150 * 1024,
            CleanupTargetPercent = 50
        });

        CreateTestFile("series/1/medium.jpg", 100, DateTime.UtcNow.AddHours(-2));
        CreateTestFile("series/2/medium.jpg", 100, DateTime.UtcNow.AddHours(-1));

        var result = await _coverService.CleanupCacheAsync();

        Assert.True(result.Success);
        Assert.Equal(200 * 1024, result.SizeBefore);
        Assert.True(result.SizeAfter < result.SizeBefore);
        Assert.True(result.BytesFreed > 0);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task CleanupCacheAsync_EmptyCache_ReturnsSuccess()
    {
        SetupSettings(new CoverSettings { CacheDirectory = _testCacheDir });

        var result = await _coverService.CleanupCacheAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalEvicted);
        Assert.Equal(0, result.BytesFreed);
    }

    #endregion

    #region CleanupResult Tests

    [Fact]
    public void CoverCleanupResult_TotalEvicted_SumsCorrectly()
    {
        var result = new CoverCleanupResult
        {
            EvictedByLru = 5,
            EvictedByRetention = 3
        };

        Assert.Equal(8, result.TotalEvicted);
    }

    [Fact]
    public void DetailedCoverCacheStats_UsagePercent_CalculatesCorrectly()
    {
        var stats = new DetailedCoverCacheStats
        {
            TotalSizeBytes = 250 * 1024 * 1024, // 250MB
            MaxCacheSizeBytes = 500 * 1024 * 1024 // 500MB
        };

        Assert.Equal(50.0, stats.UsagePercent);
    }

    [Fact]
    public void DetailedCoverCacheStats_IsOverLimit_TrueWhenOver()
    {
        var stats = new DetailedCoverCacheStats
        {
            TotalSizeBytes = 600 * 1024 * 1024,
            MaxCacheSizeBytes = 500 * 1024 * 1024
        };

        Assert.True(stats.IsOverLimit);
        Assert.Equal(100 * 1024 * 1024, stats.BytesOverLimit);
    }

    [Fact]
    public void DetailedCoverCacheStats_IsOverLimit_FalseWhenUnder()
    {
        var stats = new DetailedCoverCacheStats
        {
            TotalSizeBytes = 400 * 1024 * 1024,
            MaxCacheSizeBytes = 500 * 1024 * 1024
        };

        Assert.False(stats.IsOverLimit);
        Assert.Equal(0, stats.BytesOverLimit);
    }

    [Fact]
    public void DetailedCoverCacheStats_NoLimit_IsNotOverLimit()
    {
        var stats = new DetailedCoverCacheStats
        {
            TotalSizeBytes = 1000 * 1024 * 1024, // 1GB
            MaxCacheSizeBytes = 0 // No limit
        };

        Assert.False(stats.IsOverLimit);
        Assert.Equal(0, stats.UsagePercent);
    }

    #endregion
}
