using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Service that batches and deduplicates ComicVine API requests to reduce API calls.
/// 
/// Batching: Combines multiple issue/volume lookups into single API requests using filters.
/// Deduplication: Concurrent identical requests share the same API call result.
/// </summary>
public class ComicVineRequestBatcher : IComicVineRequestBatcher
{
    private readonly IComicVineClient _client;
    private readonly ILogger<ComicVineRequestBatcher>? _logger;

    // Deduplication: Track in-flight requests to share results
    private readonly ConcurrentDictionary<string, Task<object?>> _inflightRequests = new();

    // Statistics
    private long _totalRequests;
    private long _actualApiCalls;
    private long _deduplicatedRequests;
    private long _batchedItems;
    private long _batchRequests;

    // Batch size limits (ComicVine typically allows up to 100 results per request)
    private const int MaxBatchSize = 100;
    private const int MaxIdsInFilter = 50; // Keep filter URL reasonable length

    public ComicVineRequestBatcher(
        IComicVineClient client,
        ILogger<ComicVineRequestBatcher>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<int, ComicVineIssue?>> GetIssuesBatchAsync(
        IEnumerable<int> issueIds,
        CancellationToken cancellationToken = default)
    {
        var idList = issueIds.Distinct().ToList();
        var results = new Dictionary<int, ComicVineIssue?>();

        if (idList.Count == 0)
        {
            return results;
        }

        // For small batches, just use individual lookups (may benefit from cache)
        // Don't track here - individual calls will track themselves
        if (idList.Count <= 3)
        {
            foreach (var id in idList)
            {
                var result = await GetIssueDeduplicatedAsync(id, cancellationToken);
                results[id] = result.Data;
            }
            return results;
        }

        // For larger batches, track batch requests
        Interlocked.Add(ref _totalRequests, idList.Count);

        // Process in batches
        var batches = idList.Chunk(MaxIdsInFilter).ToList();

        foreach (var batch in batches)
        {
            var batchResults = await FetchIssuesBatchInternalAsync(batch.ToList(), cancellationToken);
            foreach (var kvp in batchResults)
            {
                results[kvp.Key] = kvp.Value;
            }
        }

        // Mark any IDs not found as null
        foreach (var id in idList)
        {
            if (!results.ContainsKey(id))
            {
                results[id] = null;
            }
        }

        return results;
    }

    private async Task<Dictionary<int, ComicVineIssue?>> FetchIssuesBatchInternalAsync(
        List<int> issueIds,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, ComicVineIssue?>();

        if (issueIds.Count == 0)
        {
            return results;
        }

        // Create a batch key for deduplication
        var sortedIds = issueIds.OrderBy(x => x).ToList();
        var batchKey = $"batch:issues:{string.Join(",", sortedIds)}";

        // Check if this exact batch is already in flight
        if (_inflightRequests.TryGetValue(batchKey, out var existingTask))
        {
            _logger?.LogDebug("Reusing in-flight batch request for {Count} issues", issueIds.Count);
            Interlocked.Add(ref _deduplicatedRequests, issueIds.Count);
            var existingResult = await existingTask;
            return existingResult as Dictionary<int, ComicVineIssue?> ?? new Dictionary<int, ComicVineIssue?>();
        }

        // Create new batch task
        var batchTask = ExecuteIssueBatchAsync(sortedIds, cancellationToken);
        var wrappedTask = batchTask.ContinueWith(t => (object?)t.Result, cancellationToken);

        if (_inflightRequests.TryAdd(batchKey, wrappedTask))
        {
            try
            {
                results = await batchTask;
            }
            finally
            {
                // Remove from in-flight after a short delay to allow concurrent requests to deduplicate
                _ = Task.Delay(100, CancellationToken.None).ContinueWith(_ =>
                {
                    _inflightRequests.TryRemove(batchKey, out Task<object?>? _);
                }, CancellationToken.None);
            }
        }
        else
        {
            // Another thread added it first, use theirs
            var existingResult = await _inflightRequests[batchKey];
            results = existingResult as Dictionary<int, ComicVineIssue?> ?? new Dictionary<int, ComicVineIssue?>();
        }

        return results;
    }

    private async Task<Dictionary<int, ComicVineIssue?>> ExecuteIssueBatchAsync(
        List<int> issueIds,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, ComicVineIssue?>();

        Interlocked.Increment(ref _actualApiCalls);
        Interlocked.Increment(ref _batchRequests);
        Interlocked.Add(ref _batchedItems, issueIds.Count);

        _logger?.LogDebug("Executing batch request for {Count} issues: {Ids}",
            issueIds.Count, string.Join(", ", issueIds.Take(10)) + (issueIds.Count > 10 ? "..." : ""));

        // Use the native batch method which fetches via ID filter
        var batchResult = await _client.GetIssuesByIdsAsync(issueIds, cancellationToken);

        if (batchResult.Success)
        {
            foreach (var issue in batchResult.Results)
            {
                results[issue.Id] = issue;
            }
        }

        // Mark unfound IDs as null
        foreach (var id in issueIds)
        {
            if (!results.ContainsKey(id))
            {
                results[id] = null;
            }
        }

        return results;
    }

    public async Task<ComicVineResult<ComicVineIssue>> GetIssueDeduplicatedAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        var requestKey = $"issue:{issueId}";

        // Check if request is already in flight
        if (_inflightRequests.TryGetValue(requestKey, out var existingTask))
        {
            _logger?.LogDebug("Deduplicating request for issue {IssueId}", issueId);
            Interlocked.Increment(ref _deduplicatedRequests);
            var existingResult = await existingTask;
            return existingResult as ComicVineResult<ComicVineIssue> ?? new ComicVineResult<ComicVineIssue> { Success = false };
        }

        // Create new task
        var fetchTask = _client.GetIssueAsync(issueId, cancellationToken);
        var wrappedTask = fetchTask.ContinueWith(t => (object?)t.Result, cancellationToken);

        if (_inflightRequests.TryAdd(requestKey, wrappedTask))
        {
            try
            {
                Interlocked.Increment(ref _actualApiCalls);
                return await fetchTask;
            }
            finally
            {
                // Keep in-flight briefly for deduplication window
                _ = Task.Delay(100, CancellationToken.None).ContinueWith(_ =>
                {
                    _inflightRequests.TryRemove(requestKey, out Task<object?>? _);
                }, CancellationToken.None);
            }
        }
        else
        {
            // Another thread added it first
            Interlocked.Increment(ref _deduplicatedRequests);
            var existingResult = await _inflightRequests[requestKey];
            return existingResult as ComicVineResult<ComicVineIssue> ?? new ComicVineResult<ComicVineIssue> { Success = false };
        }
    }

    public async Task<IReadOnlyDictionary<int, ComicVineVolume?>> GetVolumesBatchAsync(
        IEnumerable<int> volumeIds,
        CancellationToken cancellationToken = default)
    {
        var idList = volumeIds.Distinct().ToList();
        var results = new Dictionary<int, ComicVineVolume?>();

        if (idList.Count == 0)
        {
            return results;
        }

        Interlocked.Add(ref _totalRequests, idList.Count);

        // For small batches, use individual deduplication
        if (idList.Count <= 3)
        {
            foreach (var id in idList)
            {
                var result = await GetVolumeDeduplicatedAsync(id, cancellationToken);
                results[id] = result.Data;
            }
            return results;
        }

        // Use native batch method for larger requests
        Interlocked.Increment(ref _actualApiCalls);
        Interlocked.Increment(ref _batchRequests);
        Interlocked.Add(ref _batchedItems, idList.Count);

        _logger?.LogDebug("Executing batch request for {Count} volumes", idList.Count);

        var batchResult = await _client.GetVolumesByIdsAsync(idList, cancellationToken);

        if (batchResult.Success)
        {
            foreach (var volume in batchResult.Results)
            {
                results[volume.Id] = volume;
            }
        }

        // Mark unfound IDs as null
        foreach (var id in idList)
        {
            if (!results.ContainsKey(id))
            {
                results[id] = null;
            }
        }

        return results;
    }

    public async Task<ComicVineResult<ComicVineVolume>> GetVolumeDeduplicatedAsync(
        int volumeId,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        var requestKey = $"volume:{volumeId}";

        // Check if request is already in flight
        if (_inflightRequests.TryGetValue(requestKey, out var existingTask))
        {
            _logger?.LogDebug("Deduplicating request for volume {VolumeId}", volumeId);
            Interlocked.Increment(ref _deduplicatedRequests);
            var existingResult = await existingTask;
            return existingResult as ComicVineResult<ComicVineVolume> ?? new ComicVineResult<ComicVineVolume> { Success = false };
        }

        // Create new task
        var fetchTask = _client.GetVolumeAsync(volumeId, cancellationToken);
        var wrappedTask = fetchTask.ContinueWith(t => (object?)t.Result, cancellationToken);

        if (_inflightRequests.TryAdd(requestKey, wrappedTask))
        {
            try
            {
                Interlocked.Increment(ref _actualApiCalls);
                return await fetchTask;
            }
            finally
            {
                // Keep in-flight briefly for deduplication window
                _ = Task.Delay(100, CancellationToken.None).ContinueWith(_ =>
                {
                    _inflightRequests.TryRemove(requestKey, out Task<object?>? _);
                }, CancellationToken.None);
            }
        }
        else
        {
            // Another thread added it first
            Interlocked.Increment(ref _deduplicatedRequests);
            var existingResult = await _inflightRequests[requestKey];
            return existingResult as ComicVineResult<ComicVineVolume> ?? new ComicVineResult<ComicVineVolume> { Success = false };
        }
    }

    public ComicVineBatchingStats GetStats()
    {
        return new ComicVineBatchingStats
        {
            TotalRequests = Interlocked.Read(ref _totalRequests),
            ActualApiCalls = Interlocked.Read(ref _actualApiCalls),
            DeduplicatedRequests = Interlocked.Read(ref _deduplicatedRequests),
            BatchedItems = Interlocked.Read(ref _batchedItems),
            BatchRequests = Interlocked.Read(ref _batchRequests)
        };
    }

    public void ResetStats()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _actualApiCalls, 0);
        Interlocked.Exchange(ref _deduplicatedRequests, 0);
        Interlocked.Exchange(ref _batchedItems, 0);
        Interlocked.Exchange(ref _batchRequests, 0);

        _logger?.LogInformation("ComicVine request batcher statistics reset");
    }
}
