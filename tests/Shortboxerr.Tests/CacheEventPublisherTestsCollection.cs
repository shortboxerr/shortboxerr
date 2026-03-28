namespace Shortboxerr.Tests;

/// <summary>
/// Serializes cache event tests so <see cref="CacheService"/> <c>Task.Run</c> publishes are not starved
/// on the thread pool when the full suite runs with high parallelism (fixes flaky CI).
/// </summary>
[CollectionDefinition(nameof(CacheEventPublisherTestsCollection), DisableParallelization = true)]
public class CacheEventPublisherTestsCollection
{
}
