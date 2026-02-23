namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for batching and deduplicating ComicVine API requests.
/// Reduces API calls by combining multiple lookups into single requests
/// and returning cached results for concurrent identical requests.
/// </summary>
public interface IComicVineRequestBatcher
{
    /// <summary>
    /// Gets multiple issues by their IDs in a batched request.
    /// If the same ID is requested multiple times concurrently, it will only fetch once.
    /// </summary>
    Task<IReadOnlyDictionary<int, ComicVineIssue?>> GetIssuesBatchAsync(
        IEnumerable<int> issueIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single issue, deduplicating concurrent requests for the same ID.
    /// </summary>
    Task<ComicVineResult<ComicVineIssue>> GetIssueDeduplicatedAsync(
        int issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple volumes by their IDs in a batched request.
    /// </summary>
    Task<IReadOnlyDictionary<int, ComicVineVolume?>> GetVolumesBatchAsync(
        IEnumerable<int> volumeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single volume, deduplicating concurrent requests for the same ID.
    /// </summary>
    Task<ComicVineResult<ComicVineVolume>> GetVolumeDeduplicatedAsync(
        int volumeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets batching statistics.
    /// </summary>
    ComicVineBatchingStats GetStats();

    /// <summary>
    /// Resets batching statistics.
    /// </summary>
    void ResetStats();
}

/// <summary>
/// Statistics for the request batcher.
/// </summary>
public class ComicVineBatchingStats
{
    /// <summary>
    /// Total number of individual item requests received.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of API calls actually made (after batching/deduplication).
    /// </summary>
    public long ActualApiCalls { get; set; }

    /// <summary>
    /// Number of requests served from deduplication (concurrent identical requests).
    /// </summary>
    public long DeduplicatedRequests { get; set; }

    /// <summary>
    /// Number of items retrieved via batched requests.
    /// </summary>
    public long BatchedItems { get; set; }

    /// <summary>
    /// Number of batch requests made.
    /// </summary>
    public long BatchRequests { get; set; }

    /// <summary>
    /// Average items per batch request.
    /// </summary>
    public double AverageItemsPerBatch => BatchRequests > 0 ? (double)BatchedItems / BatchRequests : 0;

    /// <summary>
    /// Percentage of requests that were deduplicated.
    /// </summary>
    public double DeduplicationRate => TotalRequests > 0 ? (double)DeduplicatedRequests / TotalRequests * 100 : 0;

    /// <summary>
    /// Estimated API calls saved through batching and deduplication.
    /// </summary>
    public long EstimatedSavedApiCalls => TotalRequests - ActualApiCalls;

    /// <summary>
    /// Efficiency percentage (saved calls / total requests).
    /// </summary>
    public double EfficiencyRate => TotalRequests > 0 ? (double)EstimatedSavedApiCalls / TotalRequests * 100 : 0;
}
