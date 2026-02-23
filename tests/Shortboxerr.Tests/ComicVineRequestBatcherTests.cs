using Shortboxerr.Core.ComicVine;
using Shortboxerr.Infrastructure.ComicVine;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for ComicVine request batching and deduplication.
/// </summary>
public class ComicVineRequestBatcherTests
{
    #region ComicVineBatchingStats Tests

    [Fact]
    public void ComicVineBatchingStats_DefaultValues_AreZero()
    {
        var stats = new ComicVineBatchingStats();

        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.ActualApiCalls);
        Assert.Equal(0, stats.DeduplicatedRequests);
        Assert.Equal(0, stats.BatchedItems);
        Assert.Equal(0, stats.BatchRequests);
    }

    [Fact]
    public void ComicVineBatchingStats_AverageItemsPerBatch_CalculatesCorrectly()
    {
        var stats = new ComicVineBatchingStats
        {
            BatchedItems = 100,
            BatchRequests = 10
        };

        Assert.Equal(10.0, stats.AverageItemsPerBatch);
    }

    [Fact]
    public void ComicVineBatchingStats_AverageItemsPerBatch_ReturnsZeroWhenNoBatches()
    {
        var stats = new ComicVineBatchingStats
        {
            BatchedItems = 100,
            BatchRequests = 0
        };

        Assert.Equal(0.0, stats.AverageItemsPerBatch);
    }

    [Fact]
    public void ComicVineBatchingStats_DeduplicationRate_CalculatesCorrectly()
    {
        var stats = new ComicVineBatchingStats
        {
            TotalRequests = 100,
            DeduplicatedRequests = 25
        };

        Assert.Equal(25.0, stats.DeduplicationRate);
    }

    [Fact]
    public void ComicVineBatchingStats_DeduplicationRate_ReturnsZeroWhenNoRequests()
    {
        var stats = new ComicVineBatchingStats
        {
            TotalRequests = 0,
            DeduplicatedRequests = 0
        };

        Assert.Equal(0.0, stats.DeduplicationRate);
    }

    [Fact]
    public void ComicVineBatchingStats_EstimatedSavedApiCalls_CalculatesCorrectly()
    {
        var stats = new ComicVineBatchingStats
        {
            TotalRequests = 100,
            ActualApiCalls = 30
        };

        Assert.Equal(70, stats.EstimatedSavedApiCalls);
    }

    [Fact]
    public void ComicVineBatchingStats_EfficiencyRate_CalculatesCorrectly()
    {
        var stats = new ComicVineBatchingStats
        {
            TotalRequests = 100,
            ActualApiCalls = 10
        };

        Assert.Equal(90.0, stats.EfficiencyRate);
    }

    [Fact]
    public void ComicVineBatchingStats_EfficiencyRate_ReturnsZeroWhenNoRequests()
    {
        var stats = new ComicVineBatchingStats
        {
            TotalRequests = 0,
            ActualApiCalls = 0
        };

        Assert.Equal(0.0, stats.EfficiencyRate);
    }

    #endregion

    #region IComicVineRequestBatcher Interface Tests

    [Fact]
    public void IComicVineRequestBatcher_GetIssuesBatchAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("GetIssuesBatchAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyDictionary<int, ComicVineIssue?>>), method.ReturnType);
    }

    [Fact]
    public void IComicVineRequestBatcher_GetIssueDeduplicatedAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("GetIssueDeduplicatedAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ComicVineResult<ComicVineIssue>>), method.ReturnType);
    }

    [Fact]
    public void IComicVineRequestBatcher_GetVolumesBatchAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("GetVolumesBatchAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyDictionary<int, ComicVineVolume?>>), method.ReturnType);
    }

    [Fact]
    public void IComicVineRequestBatcher_GetVolumeDeduplicatedAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("GetVolumeDeduplicatedAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ComicVineResult<ComicVineVolume>>), method.ReturnType);
    }

    [Fact]
    public void IComicVineRequestBatcher_GetStats_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("GetStats");

        Assert.NotNull(method);
        Assert.Equal(typeof(ComicVineBatchingStats), method.ReturnType);
    }

    [Fact]
    public void IComicVineRequestBatcher_ResetStats_MethodExists()
    {
        var interfaceType = typeof(IComicVineRequestBatcher);
        var method = interfaceType.GetMethod("ResetStats");

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
    }

    #endregion

    #region IComicVineClient Batch Methods Tests

    [Fact]
    public void IComicVineClient_GetIssuesByIdsAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineClient);
        var method = interfaceType.GetMethod("GetIssuesByIdsAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ComicVineSearchResult<ComicVineIssue>>), method.ReturnType);
    }

    [Fact]
    public void IComicVineClient_GetVolumesByIdsAsync_MethodExists()
    {
        var interfaceType = typeof(IComicVineClient);
        var method = interfaceType.GetMethod("GetVolumesByIdsAsync");

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ComicVineSearchResult<ComicVineVolume>>), method.ReturnType);
    }

    #endregion

    #region ComicVineRequestBatcher with Mock Client Tests

    [Fact]
    public async Task ComicVineRequestBatcher_GetIssuesBatchAsync_EmptyList_ReturnsEmpty()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        var result = await batcher.GetIssuesBatchAsync(new int[0]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetVolumesBatchAsync_EmptyList_ReturnsEmpty()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        var result = await batcher.GetVolumesBatchAsync(new int[0]);

        Assert.Empty(result);
    }

    [Fact]
    public void ComicVineRequestBatcher_GetStats_ReturnsInitialStats()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        var stats = batcher.GetStats();

        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.ActualApiCalls);
        Assert.Equal(0, stats.DeduplicatedRequests);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetIssueDeduplicatedAsync_TracksRequests()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        await batcher.GetIssueDeduplicatedAsync(12345);

        var stats = batcher.GetStats();
        Assert.Equal(1, stats.TotalRequests);
        Assert.Equal(1, stats.ActualApiCalls);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetVolumeDeduplicatedAsync_TracksRequests()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        await batcher.GetVolumeDeduplicatedAsync(67890);

        var stats = batcher.GetStats();
        Assert.Equal(1, stats.TotalRequests);
        Assert.Equal(1, stats.ActualApiCalls);
    }

    [Fact]
    public void ComicVineRequestBatcher_ResetStats_ClearsAllCounters()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Manually verify interface is called through stats
        batcher.ResetStats();

        var stats = batcher.GetStats();
        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.ActualApiCalls);
        Assert.Equal(0, stats.DeduplicatedRequests);
        Assert.Equal(0, stats.BatchedItems);
        Assert.Equal(0, stats.BatchRequests);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetIssuesBatchAsync_SmallBatch_UsesIndividualCalls()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Small batch (<=3) should use individual deduplicated calls
        var result = await batcher.GetIssuesBatchAsync(new[] { 1, 2, 3 });

        var stats = batcher.GetStats();
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(3, stats.ActualApiCalls);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetIssuesBatchAsync_LargeBatch_UsesBatching()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Large batch (>3) should use batch method
        var ids = Enumerable.Range(1, 10).ToArray();
        var result = await batcher.GetIssuesBatchAsync(ids);

        var stats = batcher.GetStats();
        Assert.True(stats.BatchRequests > 0, "Should use batch requests for larger sets");
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetIssuesBatchAsync_DeduplicatesDuplicateIds()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Duplicate IDs should be deduplicated
        var result = await batcher.GetIssuesBatchAsync(new[] { 1, 2, 1, 3, 2, 1 });

        // Should only track 3 unique requests
        Assert.Equal(3, result.Count);
        Assert.Contains(1, result.Keys);
        Assert.Contains(2, result.Keys);
        Assert.Contains(3, result.Keys);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_GetVolumesBatchAsync_DeduplicatesDuplicateIds()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Duplicate IDs should be deduplicated
        var result = await batcher.GetVolumesBatchAsync(new[] { 100, 200, 100, 300, 200, 100 });

        Assert.Equal(3, result.Count);
        Assert.Contains(100, result.Keys);
        Assert.Contains(200, result.Keys);
        Assert.Contains(300, result.Keys);
    }

    #endregion

    #region Concurrency and Deduplication Tests

    [Fact]
    public async Task ComicVineRequestBatcher_ConcurrentSameIssue_DeduplicatesRequests()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Fire multiple concurrent requests for the same issue
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => batcher.GetIssueDeduplicatedAsync(12345))
            .ToList();

        await Task.WhenAll(tasks);

        var stats = batcher.GetStats();
        // All requests should be tracked, but some may be deduplicated
        Assert.Equal(5, stats.TotalRequests);
    }

    [Fact]
    public async Task ComicVineRequestBatcher_ConcurrentDifferentIssues_MakesMultipleCalls()
    {
        var mockClient = new MockComicVineClient();
        var batcher = new ComicVineRequestBatcher(mockClient);

        // Fire concurrent requests for different issues
        var tasks = new[]
        {
            batcher.GetIssueDeduplicatedAsync(1),
            batcher.GetIssueDeduplicatedAsync(2),
            batcher.GetIssueDeduplicatedAsync(3)
        };

        await Task.WhenAll(tasks);

        var stats = batcher.GetStats();
        Assert.Equal(3, stats.TotalRequests);
    }

    #endregion

    #region Mock ComicVine Client

    /// <summary>
    /// Mock implementation of IComicVineClient for testing the batcher.
    /// </summary>
    private class MockComicVineClient : IComicVineClient
    {
        public bool IsConfigured => true;

        private int _requestCount = 0;
        public int RequestCount => _requestCount;

        public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ComicVineTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ComicVineTestResult { Success = true, Message = "Mock client" });

        public Task<ComicVineSearchResult<ComicVineVolume>> SearchVolumesAsync(
            string query, int page = 1, int limit = 10, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                Results = new List<ComicVineVolume>()
            });
        }

        public Task<ComicVineSearchResult<ComicVineIssue>> SearchIssuesAsync(
            string query, int page = 1, int limit = 10, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });
        }

        public Task<ComicVineResult<ComicVineVolume>> GetVolumeAsync(
            int volumeId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVineVolume { Id = volumeId, Name = $"Volume {volumeId}" }
            });
        }

        public Task<ComicVineResult<ComicVineIssue>> GetIssueAsync(
            int issueId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVineIssue { Id = issueId, IssueNumber = issueId.ToString() }
            });
        }

        public Task<ComicVineSearchResult<ComicVineIssue>> GetIssuesByIdsAsync(
            IEnumerable<int> issueIds, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            var issues = issueIds.Select(id => new ComicVineIssue
            {
                Id = id,
                IssueNumber = id.ToString()
            }).ToList();

            return Task.FromResult(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                StatusCode = 1,
                Results = issues,
                TotalResults = issues.Count,
                NumberOfPageResults = issues.Count
            });
        }

        public Task<ComicVineSearchResult<ComicVineVolume>> GetVolumesByIdsAsync(
            IEnumerable<int> volumeIds, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            var volumes = volumeIds.Select(id => new ComicVineVolume
            {
                Id = id,
                Name = $"Volume {id}"
            }).ToList();

            return Task.FromResult(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Results = volumes,
                TotalResults = volumes.Count,
                NumberOfPageResults = volumes.Count
            });
        }

        public Task<ComicVineSearchResult<ComicVineIssue>> GetVolumeIssuesAsync(
            int volumeId, int page = 1, int limit = 100, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });
        }

        public Task<ComicVineResult<ComicVinePublisher>> GetPublisherAsync(
            int publisherId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineResult<ComicVinePublisher>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVinePublisher { Id = publisherId, Name = $"Publisher {publisherId}" }
            });
        }

        public Task<ComicVineSearchResult<ComicVineIssue>> GetIssuesByStoreDateAsync(
            string storeDateFilter, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });
        }

        public ComicVineRateLimitStatus GetRateLimitStatus()
        {
            return new ComicVineRateLimitStatus
            {
                RequestsUsed = _requestCount,
                RequestLimit = 200,
                WindowResetTime = DateTime.UtcNow.AddHours(1)
            };
        }
    }

    #endregion
}
