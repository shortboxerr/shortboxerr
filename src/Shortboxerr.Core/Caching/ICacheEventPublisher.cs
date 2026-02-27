namespace Shortboxerr.Core.Caching;

/// <summary>
/// Abstraction for publishing cache invalidation events.
/// Enables distributed cache coordination across multiple instances.
/// </summary>
/// <remarks>
/// The default LocalCacheEventPublisher works for single-instance deployments.
/// For multi-instance deployments, implement with Redis pub/sub, RabbitMQ, etc.
/// </remarks>
public interface ICacheEventPublisher
{
    /// <summary>
    /// Publishes a cache invalidation event.
    /// </summary>
    /// <param name="event">The cache event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(CacheEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to cache invalidation events.
    /// </summary>
    /// <param name="handler">Handler to invoke when events are received.</param>
    /// <returns>Disposable subscription that unsubscribes when disposed.</returns>
    IDisposable Subscribe(Func<CacheEvent, Task> handler);

    /// <summary>
    /// Gets recent cache events for monitoring/debugging.
    /// </summary>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <returns>List of recent cache events, newest first.</returns>
    IReadOnlyList<CacheEvent> GetRecentEvents(int limit = 100);
}

/// <summary>
/// Represents a cache invalidation event.
/// </summary>
public record CacheEvent
{
    /// <summary>Unique event identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Type of cache event.</summary>
    public CacheEventType Type { get; init; }

    /// <summary>Cache key or prefix affected.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Reason for the cache event.</summary>
    public string? Reason { get; init; }

    /// <summary>Source instance that generated the event.</summary>
    public string? SourceInstance { get; init; }

    /// <summary>When the event occurred (UTC).</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Number of cache entries affected.</summary>
    public int AffectedCount { get; init; }
}

/// <summary>
/// Types of cache events.
/// </summary>
public enum CacheEventType
{
    /// <summary>Single key removed.</summary>
    KeyRemoved,

    /// <summary>Multiple keys removed by prefix.</summary>
    PrefixInvalidated,

    /// <summary>All cache entries cleared.</summary>
    CacheCleared,

    /// <summary>Cache entry evicted due to expiration or capacity.</summary>
    Evicted,

    /// <summary>Cache entry added.</summary>
    Added,

    /// <summary>Cache entry updated.</summary>
    Updated
}
