using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shortboxerr.Core.Caching;
using Shortboxerr.Infrastructure.Caching;
using Xunit;

namespace Shortboxerr.Tests;

public class CacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<CacheService>> _mockLogger;
    private readonly CacheService _service;
    private readonly CacheSettings _settings;

    public CacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<CacheService>>();
        _settings = new CacheSettings
        {
            Enabled = true,
            TrackStatistics = true,
            DefaultTtl = TimeSpan.FromMinutes(5)
        };

        _service = new CacheService(
            _memoryCache,
            _mockLogger.Object,
            Options.Create(_settings));
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    #region Core Operations Tests

    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        // Arrange
        var key = "test:key";
        var value = "test-value";

        // Act
        _service.Set(key, value);
        var result = _service.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Get_WhenKeyDoesNotExist_ReturnsDefault()
    {
        // Act
        var result = _service.Get<string>("nonexistent:key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_WithCustomTtl_ExpiresAfterTtl()
    {
        // Arrange
        var key = "expiring:key";
        var value = "temp-value";
        var ttl = TimeSpan.FromMilliseconds(50);

        // Act
        _service.Set(key, value, ttl);
        Thread.Sleep(100); // Wait for expiration
        var result = _service.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Exists_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var key = "exists:key";
        _service.Set(key, "value");

        // Act
        var result = _service.Exists(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Exists_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = _service.Exists("nonexistent:key");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Remove_RemovesKey()
    {
        // Arrange
        var key = "remove:key";
        _service.Set(key, "value");

        // Act
        _service.Remove(key);
        var result = _service.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenKeyDoesNotExist_CreatesAndCaches()
    {
        // Arrange
        var key = "getorcreate:key";
        var factoryCalled = false;

        // Act
        var result = await _service.GetOrCreateAsync(key, async () =>
        {
            factoryCalled = true;
            return await Task.FromResult("created-value");
        });

        // Assert
        Assert.True(factoryCalled);
        Assert.Equal("created-value", result);
        Assert.Equal("created-value", _service.Get<string>(key));
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenKeyExists_ReturnsExistingWithoutFactory()
    {
        // Arrange
        var key = "getorcreate:existing";
        _service.Set(key, "existing-value");
        var factoryCalled = false;

        // Act
        var result = await _service.GetOrCreateAsync(key, async () =>
        {
            factoryCalled = true;
            return await Task.FromResult("new-value");
        });

        // Assert
        Assert.False(factoryCalled);
        Assert.Equal("existing-value", result);
    }

    #endregion

    #region Key Generation Tests

    [Fact]
    public void GenerateKey_WithNoSegments_ReturnsPrefix()
    {
        // Act
        var key = _service.GenerateKey("prefix");

        // Assert
        Assert.Equal("prefix", key);
    }

    [Fact]
    public void GenerateKey_WithSegments_ReturnsFormattedKey()
    {
        // Act
        var key = _service.GenerateKey("series", "detail", 123);

        // Assert
        Assert.Equal("series:detail:123", key);
    }

    [Fact]
    public void GenerateKey_WithNullSegment_HandlesGracefully()
    {
        // Act
        var key = _service.GenerateKey("series", "detail", null!, 123);

        // Assert
        Assert.Equal("series:detail:null:123", key);
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public void RemoveByPrefix_RemovesMatchingKeys()
    {
        // Arrange
        _service.Set("pulllist:week:2024-01", "data1");
        _service.Set("pulllist:week:2024-02", "data2");
        _service.Set("series:list", "data3");

        // Act
        var removed = _service.RemoveByPrefix("pulllist");

        // Assert
        Assert.Equal(2, removed);
        Assert.Null(_service.Get<string>("pulllist:week:2024-01"));
        Assert.Null(_service.Get<string>("pulllist:week:2024-02"));
        Assert.Equal("data3", _service.Get<string>("series:list"));
    }

    [Fact]
    public void Clear_RemovesAllKeys()
    {
        // Arrange
        _service.Set("key1", "value1");
        _service.Set("key2", "value2");
        _service.Set("key3", "value3");

        // Act
        _service.Clear();

        // Assert
        Assert.Null(_service.Get<string>("key1"));
        Assert.Null(_service.Get<string>("key2"));
        Assert.Null(_service.Get<string>("key3"));
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_TracksCacheHits()
    {
        // Arrange
        _service.Set("hit:key", "value");

        // Act
        _service.Get<string>("hit:key");
        _service.Get<string>("hit:key");
        _service.Get<string>("hit:key");
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(3, stats.Hits);
    }

    [Fact]
    public void GetStatistics_TracksCacheMisses()
    {
        // Act
        _service.Get<string>("miss:key1");
        _service.Get<string>("miss:key2");
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.Misses);
    }

    [Fact]
    public void GetStatistics_TracksItemsAdded()
    {
        // Act
        _service.Set("added:1", "value");
        _service.Set("added:2", "value");
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.ItemsAdded);
    }

    [Fact]
    public void GetStatistics_TracksItemsRemoved()
    {
        // Arrange
        _service.Set("remove:1", "value");
        _service.Set("remove:2", "value");

        // Act
        _service.Remove("remove:1");
        _service.Remove("remove:2");
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.ItemsRemoved);
    }

    [Fact]
    public void GetStatistics_CalculatesHitRatio()
    {
        // Arrange
        _service.Set("ratio:key", "value");

        // Act - 2 hits, 2 misses = 50% ratio
        _service.Get<string>("ratio:key"); // hit
        _service.Get<string>("ratio:key"); // hit
        _service.Get<string>("ratio:miss1"); // miss
        _service.Get<string>("ratio:miss2"); // miss
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(0.5, stats.HitRatio);
    }

    [Fact]
    public void GetStatistics_TracksItemCount()
    {
        // Arrange
        _service.Set("count:1", "value");
        _service.Set("count:2", "value");
        _service.Set("count:3", "value");

        // Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(3, stats.ItemCount);
    }

    [Fact]
    public void ResetStatistics_ResetsAllCounters()
    {
        // Arrange
        _service.Set("reset:key", "value");
        _service.Get<string>("reset:key");
        _service.Get<string>("reset:miss");

        // Act
        _service.ResetStatistics();
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.ItemsAdded);
        Assert.Equal(0, stats.ItemsRemoved);
    }

    #endregion

    #region Disabled Cache Tests

    [Fact]
    public void Get_WhenCacheDisabled_ReturnsDefault()
    {
        // Arrange
        var disabledSettings = new CacheSettings { Enabled = false };
        var disabledService = new CacheService(
            _memoryCache,
            _mockLogger.Object,
            Options.Create(disabledSettings));

        disabledService.Set("key", "value");

        // Act
        var result = disabledService.Get<string>("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheDisabled_AlwaysCallsFactory()
    {
        // Arrange
        var disabledSettings = new CacheSettings { Enabled = false };
        var disabledService = new CacheService(
            _memoryCache,
            _mockLogger.Object,
            Options.Create(disabledSettings));

        var callCount = 0;

        // Act
        await disabledService.GetOrCreateAsync("key", () =>
        {
            callCount++;
            return Task.FromResult("value");
        });
        await disabledService.GetOrCreateAsync("key", () =>
        {
            callCount++;
            return Task.FromResult("value");
        });

        // Assert
        Assert.Equal(2, callCount); // Factory called both times
    }

    #endregion

    #region Complex Object Tests

    [Fact]
    public void Set_And_Get_WithComplexObject()
    {
        // Arrange
        var key = "complex:key";
        var value = new TestObject { Id = 1, Name = "Test", Created = DateTime.UtcNow };

        // Act
        _service.Set(key, value);
        var result = _service.Get<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public void Set_And_Get_WithList()
    {
        // Arrange
        var key = "list:key";
        var value = new List<string> { "item1", "item2", "item3" };

        // Act
        _service.Set(key, value);
        var result = _service.Get<List<string>>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("item2", result);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime Created { get; set; }
    }

    #endregion
}
