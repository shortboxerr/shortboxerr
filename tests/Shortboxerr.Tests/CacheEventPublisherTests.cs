using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Caching;
using Shortboxerr.Infrastructure.Caching;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for cache event publishing infrastructure.
/// </summary>
[Collection(nameof(CacheEventPublisherTestsCollection))]
public class CacheEventPublisherTests
{
    /// <summary>
    /// CacheService publishes via <c>Task.Run</c>; CI runners may need more than a fixed sleep.
    /// </summary>
    private static void WaitForAtLeastEventCount(LocalCacheEventPublisher publisher, int minCount, int timeoutMs = 10_000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (publisher.GetRecentEvents(100).Count >= minCount)
                return;
            Thread.Sleep(15);
        }

        Assert.Fail($"Expected at least {minCount} cache events within {timeoutMs}ms; got {publisher.GetRecentEvents(100).Count}.");
    }

    #region LocalCacheEventPublisher Tests

    [Fact]
    public async Task PublishAsync_StoresEventInLog()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var @event = new CacheEvent
        {
            Type = CacheEventType.KeyRemoved,
            Key = "test:key",
            AffectedCount = 1
        };

        // Act
        await publisher.PublishAsync(@event);

        // Assert
        var events = publisher.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal("test:key", events[0].Key);
        Assert.Equal(CacheEventType.KeyRemoved, events[0].Type);
        Assert.NotNull(events[0].SourceInstance);
    }

    [Fact]
    public async Task PublishAsync_NotifiesSubscribers()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        CacheEvent? receivedEvent = null;
        
        publisher.Subscribe(async e =>
        {
            receivedEvent = e;
            await Task.CompletedTask;
        });

        var @event = new CacheEvent
        {
            Type = CacheEventType.CacheCleared,
            Key = "*",
            AffectedCount = 10
        };

        // Act
        await publisher.PublishAsync(@event);

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(CacheEventType.CacheCleared, receivedEvent.Type);
        Assert.Equal(10, receivedEvent.AffectedCount);
    }

    [Fact]
    public async Task Subscribe_Unsubscribe_StopsReceivingEvents()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        int eventCount = 0;
        
        var subscription = publisher.Subscribe(async e =>
        {
            eventCount++;
            await Task.CompletedTask;
        });

        // Act - send event before unsubscribe
        await publisher.PublishAsync(new CacheEvent { Type = CacheEventType.Added, Key = "key1" });
        
        // Unsubscribe
        subscription.Dispose();
        
        // Send event after unsubscribe
        await publisher.PublishAsync(new CacheEvent { Type = CacheEventType.Added, Key = "key2" });

        // Assert - should only receive one event
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task GetRecentEvents_RespectsLimit()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        
        for (int i = 0; i < 20; i++)
        {
            await publisher.PublishAsync(new CacheEvent
            {
                Type = CacheEventType.Added,
                Key = $"key{i}"
            });
        }

        // Act
        var events = publisher.GetRecentEvents(5);

        // Assert
        Assert.Equal(5, events.Count);
        Assert.Equal("key19", events[0].Key); // Most recent first
        Assert.Equal("key15", events[4].Key);
    }

    [Fact]
    public async Task GetRecentEvents_ReturnsNewestFirst()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        
        await publisher.PublishAsync(new CacheEvent { Type = CacheEventType.Added, Key = "first" });
        await Task.Delay(10); // Ensure different timestamps
        await publisher.PublishAsync(new CacheEvent { Type = CacheEventType.Added, Key = "second" });
        await Task.Delay(10);
        await publisher.PublishAsync(new CacheEvent { Type = CacheEventType.Added, Key = "third" });

        // Act
        var events = publisher.GetRecentEvents(10);

        // Assert
        Assert.Equal("third", events[0].Key);
        Assert.Equal("second", events[1].Key);
        Assert.Equal("first", events[2].Key);
    }

    #endregion

    #region CacheService Integration Tests

    [Fact]
    public void CacheService_Set_PublishesAddedEvent()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            publisher);

        // Act
        service.Set("test:key", "value");
        WaitForAtLeastEventCount(publisher, 1);

        // Assert
        var events = publisher.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal(CacheEventType.Added, events[0].Type);
        Assert.Equal("test:key", events[0].Key);
    }

    [Fact]
    public void CacheService_Set_ExistingKey_PublishesUpdatedEvent()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            publisher);

        // Act - set initial value
        service.Set("test:key", "value1");
        WaitForAtLeastEventCount(publisher, 1);

        // Update value
        service.Set("test:key", "value2");
        WaitForAtLeastEventCount(publisher, 2);

        // Assert
        var events = publisher.GetRecentEvents(10);
        Assert.Equal(2, events.Count);
        Assert.Equal(CacheEventType.Updated, events[0].Type); // Most recent
        Assert.Equal(CacheEventType.Added, events[1].Type);
    }

    [Fact]
    public void CacheService_Remove_PublishesKeyRemovedEvent()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            publisher);

        service.Set("test:key", "value");
        WaitForAtLeastEventCount(publisher, 1);

        // Act
        service.Remove("test:key");
        WaitForAtLeastEventCount(publisher, 2);

        // Assert
        var events = publisher.GetRecentEvents(10);
        Assert.Equal(2, events.Count);
        Assert.Equal(CacheEventType.KeyRemoved, events[0].Type);
    }

    [Fact]
    public void CacheService_RemoveByPrefix_PublishesPrefixInvalidatedEvent()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            publisher);

        service.Set("series:1", "value1");
        service.Set("series:2", "value2");
        service.Set("other:1", "value3");
        WaitForAtLeastEventCount(publisher, 3);

        // Act
        service.RemoveByPrefix("series");
        WaitForAtLeastEventCount(publisher, 4);

        // Assert
        var events = publisher.GetRecentEvents(10);
        var prefixEvent = events.FirstOrDefault(e => e.Type == CacheEventType.PrefixInvalidated);
        Assert.NotNull(prefixEvent);
        Assert.Equal("series", prefixEvent.Key);
        Assert.Equal(2, prefixEvent.AffectedCount);
    }

    [Fact]
    public void CacheService_Clear_PublishesCacheClearedEvent()
    {
        // Arrange
        var publisher = new LocalCacheEventPublisher();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            publisher);

        service.Set("key1", "value1");
        service.Set("key2", "value2");
        service.Set("key3", "value3");
        WaitForAtLeastEventCount(publisher, 3);

        // Act
        service.Clear();
        WaitForAtLeastEventCount(publisher, 4);

        // Assert
        var events = publisher.GetRecentEvents(10);
        var clearEvent = events.FirstOrDefault(e => e.Type == CacheEventType.CacheCleared);
        Assert.NotNull(clearEvent);
        Assert.Equal("*", clearEvent.Key);
        Assert.Equal(3, clearEvent.AffectedCount);
    }

    [Fact]
    public void CacheService_WithoutPublisher_DoesNotThrow()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CacheService(
            cache,
            NullLogger<CacheService>.Instance,
            Options.Create(new CacheSettings()),
            eventPublisher: null);

        // Act & Assert - should not throw
        service.Set("key", "value");
        service.Remove("key");
        service.RemoveByPrefix("prefix");
        service.Clear();
    }

    #endregion
}
