using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Caching;

namespace Shortboxerr.Infrastructure.Caching;

/// <summary>
/// In-memory cache event publisher for single-instance deployments.
/// Maintains an event log for monitoring and supports local subscribers.
/// </summary>
/// <remarks>
/// For multi-instance deployments, replace with a distributed implementation
/// using Redis pub/sub, RabbitMQ, or similar.
/// </remarks>
public class LocalCacheEventPublisher : ICacheEventPublisher
{
    private readonly ILogger<LocalCacheEventPublisher>? _logger;
    private readonly ConcurrentQueue<CacheEvent> _eventLog = new();
    private readonly ConcurrentDictionary<Guid, Func<CacheEvent, Task>> _subscribers = new();
    private readonly string _instanceId;
    private const int MaxEventLogSize = 1000;

    public LocalCacheEventPublisher(ILogger<LocalCacheEventPublisher>? logger = null)
    {
        _logger = logger;
        _instanceId = Environment.MachineName + "-" + Environment.ProcessId;
    }

    public async Task PublishAsync(CacheEvent @event, CancellationToken cancellationToken = default)
    {
        var eventWithSource = @event with { SourceInstance = _instanceId };

        _eventLog.Enqueue(eventWithSource);

        while (_eventLog.Count > MaxEventLogSize)
        {
            _eventLog.TryDequeue(out _);
        }

        _logger?.LogDebug("Cache event published: {Type} - {Key}", @event.Type, @event.Key);

        foreach (var subscriber in _subscribers.Values)
        {
            try
            {
                await subscriber(eventWithSource);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache event subscriber failed");
            }
        }
    }

    public IDisposable Subscribe(Func<CacheEvent, Task> handler)
    {
        var subscriptionId = Guid.NewGuid();
        _subscribers.TryAdd(subscriptionId, handler);

        _logger?.LogDebug("Cache event subscriber added: {Id}", subscriptionId);

        return new Subscription(() =>
        {
            _subscribers.TryRemove(subscriptionId, out _);
            _logger?.LogDebug("Cache event subscriber removed: {Id}", subscriptionId);
        });
    }

    public IReadOnlyList<CacheEvent> GetRecentEvents(int limit = 100)
    {
        return _eventLog
            .Reverse()
            .Take(limit)
            .ToList();
    }

    private class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _onDispose();
            }
        }
    }
}
