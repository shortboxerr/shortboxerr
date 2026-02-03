using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Caching;

namespace Shortboxerr.Infrastructure.Caching;

/// <summary>
/// Memory cache service with statistics tracking, prefix-based invalidation,
/// and configurable TTLs.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly CacheSettings _settings;
    
    // Track cache keys for prefix-based invalidation
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    
    // Statistics tracking
    private long _hits;
    private long _misses;
    private long _itemsAdded;
    private long _itemsRemoved;
    private long _itemsEvicted;
    private DateTime _lastReset = DateTime.UtcNow;
    private readonly object _statsLock = new();

    public CacheService(
        IMemoryCache cache,
        ILogger<CacheService> logger,
        IOptions<CacheSettings>? settings = null)
    {
        _cache = cache;
        _logger = logger;
        _settings = settings?.Value ?? new CacheSettings();
    }

    #region Core Operations

    public T? Get<T>(string key)
    {
        if (!_settings.Enabled)
        {
            return default;
        }

        if (_cache.TryGetValue(key, out T? value))
        {
            if (_settings.TrackStatistics)
            {
                Interlocked.Increment(ref _hits);
            }
            _logger.LogDebug("Cache hit: {Key}", key);
            return value;
        }

        if (_settings.TrackStatistics)
        {
            Interlocked.Increment(ref _misses);
        }
        _logger.LogDebug("Cache miss: {Key}", key);
        return default;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get<T>(key));
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        if (!_settings.Enabled)
        {
            return await factory();
        }

        var value = Get<T>(key);
        if (value != null)
        {
            return value;
        }

        // Cache miss - create value
        value = await factory();
        Set(key, value, ttl);
        return value;
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var effectiveTtl = ttl ?? _settings.DefaultTtl;
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = effectiveTtl,
            PostEvictionCallbacks = { new PostEvictionCallbackRegistration
            {
                EvictionCallback = OnEviction
            }}
        };

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);
        
        if (_settings.TrackStatistics)
        {
            Interlocked.Increment(ref _itemsAdded);
        }
        
        _logger.LogDebug("Cache set: {Key} (TTL: {Ttl})", key, effectiveTtl);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        
        if (_settings.TrackStatistics)
        {
            Interlocked.Increment(ref _itemsRemoved);
        }
        
        _logger.LogDebug("Cache remove: {Key}", key);
    }

    public bool Exists(string key)
    {
        return _settings.Enabled && _cache.TryGetValue(key, out _);
    }

    #endregion

    #region Key Generation

    public string GenerateKey(string prefix, params object[] segments)
    {
        if (segments.Length == 0)
        {
            return prefix;
        }
        
        return $"{prefix}:{string.Join(":", segments.Select(s => s?.ToString() ?? "null"))}";
    }

    #endregion

    #region Bulk Operations

    public int RemoveByPrefix(string prefix)
    {
        var keysToRemove = _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        if (_settings.TrackStatistics)
        {
            Interlocked.Add(ref _itemsRemoved, keysToRemove.Count);
        }

        _logger.LogInformation("Cache invalidated {Count} entries with prefix: {Prefix}", 
            keysToRemove.Count, prefix);
        
        return keysToRemove.Count;
    }

    public void Clear()
    {
        var count = _keys.Count;
        
        foreach (var key in _keys.Keys)
        {
            _cache.Remove(key);
        }
        _keys.Clear();

        if (_settings.TrackStatistics)
        {
            Interlocked.Add(ref _itemsRemoved, count);
        }

        _logger.LogInformation("Cache cleared: {Count} entries removed", count);
    }

    #endregion

    #region Statistics

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            ItemCount = _keys.Count,
            ItemsAdded = Interlocked.Read(ref _itemsAdded),
            ItemsRemoved = Interlocked.Read(ref _itemsRemoved),
            ItemsEvicted = Interlocked.Read(ref _itemsEvicted),
            LastReset = _lastReset
        };
    }

    public void ResetStatistics()
    {
        lock (_statsLock)
        {
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
            Interlocked.Exchange(ref _itemsAdded, 0);
            Interlocked.Exchange(ref _itemsRemoved, 0);
            Interlocked.Exchange(ref _itemsEvicted, 0);
            _lastReset = DateTime.UtcNow;
        }
        
        _logger.LogInformation("Cache statistics reset");
    }

    #endregion

    #region Private Helpers

    private void OnEviction(object key, object? value, EvictionReason reason, object? state)
    {
        var keyStr = key.ToString() ?? "";
        _keys.TryRemove(keyStr, out _);

        if (reason == EvictionReason.Expired || reason == EvictionReason.Capacity)
        {
            if (_settings.TrackStatistics)
            {
                Interlocked.Increment(ref _itemsEvicted);
            }
            _logger.LogDebug("Cache evicted: {Key} (Reason: {Reason})", keyStr, reason);
        }
    }

    #endregion
}
