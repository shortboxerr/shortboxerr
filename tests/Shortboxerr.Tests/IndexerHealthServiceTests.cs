using Moq;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class IndexerHealthServiceTests
{
    private readonly Mock<INzbIndexerProvider> _mockIndexerProvider;
    private readonly Mock<INewznabClient> _mockNewznabClient;
    private readonly IndexerHealthService _service;

    private readonly NewznabIndexer _testIndexer1 = new()
    {
        Id = "indexer-1",
        Name = "Test Indexer 1",
        BaseUrl = "https://test1.example.com",
        ApiKey = "api-key-1",
        Enabled = true,
        Priority = 10
    };

    private readonly NewznabIndexer _testIndexer2 = new()
    {
        Id = "indexer-2",
        Name = "Test Indexer 2",
        BaseUrl = "https://test2.example.com",
        ApiKey = "api-key-2",
        Enabled = true,
        Priority = 20
    };

    public IndexerHealthServiceTests()
    {
        _mockIndexerProvider = new Mock<INzbIndexerProvider>();
        _mockNewznabClient = new Mock<INewznabClient>();
        _service = new IndexerHealthService(_mockIndexerProvider.Object, _mockNewznabClient.Object);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsStatus_ForExistingIndexer()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal("indexer-1", status.IndexerId);
        Assert.Equal("Test Indexer 1", status.IndexerName);
        Assert.Equal(IndexerHealthState.Unknown, status.State);
    }

    [Fact]
    public async Task GetHealthAsync_ThrowsException_ForNonexistentIndexer()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetHealthAsync("nonexistent"));
    }

    [Fact]
    public async Task RecordSuccessAsync_UpdatesHealthStatus()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(150));
        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(IndexerHealthState.Healthy, status.State);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(0, status.FailureCount);
        Assert.Equal(150, status.AverageResponseTimeMs);
        Assert.NotNull(status.LastSuccessAt);
    }

    [Fact]
    public async Task RecordFailureAsync_UpdatesHealthStatus()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordFailureAsync("indexer-1", "Connection timeout");
        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(0, status.SuccessCount);
        Assert.Equal(1, status.FailureCount);
        Assert.Equal(1, status.ConsecutiveFailures);
        Assert.Equal("Connection timeout", status.LastErrorMessage);
        Assert.NotNull(status.LastFailureAt);
    }

    [Fact]
    public async Task RecordFailureAsync_SetsRateLimited_WhenFlagged()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordFailureAsync("indexer-1", "Rate limit exceeded", isRateLimited: true);
        var status = await _service.GetHealthAsync("indexer-1");

        Assert.True(status.IsRateLimited);
        Assert.NotNull(status.RateLimitExpiresAt);
        Assert.True(status.RateLimitExpiresAt > DateTime.UtcNow);
        Assert.Equal(IndexerHealthState.Unavailable, status.State);
    }

    [Fact]
    public async Task IsRateLimitedAsync_ReturnsTrue_WhenRateLimited()
    {
        await _service.RecordFailureAsync("indexer-1", "Rate limit", isRateLimited: true);

        var isRateLimited = await _service.IsRateLimitedAsync("indexer-1");

        Assert.True(isRateLimited);
    }

    [Fact]
    public async Task IsRateLimitedAsync_ReturnsFalse_WhenNotRateLimited()
    {
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));

        var isRateLimited = await _service.IsRateLimitedAsync("indexer-1");

        Assert.False(isRateLimited);
    }

    [Fact]
    public async Task ConsecutiveFailures_TriggersOfflineState()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        for (int i = 0; i < 5; i++)
        {
            await _service.RecordFailureAsync("indexer-1", $"Error {i}");
        }

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(IndexerHealthState.Offline, status.State);
        Assert.Equal(5, status.ConsecutiveFailures);
    }

    [Fact]
    public async Task SuccessResetsConsecutiveFailures()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordFailureAsync("indexer-1", "Error 1");
        await _service.RecordFailureAsync("indexer-1", "Error 2");
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(2, status.FailureCount);
    }

    [Fact]
    public async Task GetAllHealthAsync_ReturnsStatusForAllIndexers()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewznabIndexer> { _testIndexer1, _testIndexer2 });

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordFailureAsync("indexer-2", "Error");

        var statuses = await _service.GetAllHealthAsync();

        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, s => s.IndexerId == "indexer-1");
        Assert.Contains(statuses, s => s.IndexerId == "indexer-2");
    }

    [Fact]
    public async Task GetHealthyIndexersAsync_ExcludesRateLimitedIndexers()
    {
        _mockIndexerProvider.Setup(p => p.GetEnabledIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewznabIndexer> { _testIndexer1, _testIndexer2 });

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordFailureAsync("indexer-2", "Rate limit", isRateLimited: true);

        var healthy = await _service.GetHealthyIndexersAsync();

        Assert.Single(healthy);
        Assert.Equal("indexer-1", healthy[0].Id);
    }

    [Fact]
    public async Task GetHealthyIndexersAsync_ExcludesOfflineIndexers()
    {
        _mockIndexerProvider.Setup(p => p.GetEnabledIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewznabIndexer> { _testIndexer1, _testIndexer2 });

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        for (int i = 0; i < 5; i++)
        {
            await _service.RecordFailureAsync("indexer-2", "Error");
        }

        var healthy = await _service.GetHealthyIndexersAsync();

        Assert.Single(healthy);
        Assert.Equal("indexer-1", healthy[0].Id);
    }

    [Fact]
    public async Task ResetHealthAsync_ClearsHealthData()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.ResetHealthAsync("indexer-1");
        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(IndexerHealthState.Unknown, status.State);
        Assert.Equal(0, status.SuccessCount);
        Assert.Equal(0, status.FailureCount);
    }

    [Fact]
    public async Task CheckHealthAsync_RecordsSuccess_OnSuccessfulTest()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);
        _mockNewznabClient.Setup(c => c.TestConnectionAsync(_testIndexer1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabTestResult.Ok("Connection successful", responseTimeMs: 120));

        var result = await _service.CheckHealthAsync("indexer-1");

        Assert.True(result.Success);
        Assert.Equal(120, result.ResponseTimeMs);

        var status = await _service.GetHealthAsync("indexer-1");
        Assert.Equal(IndexerHealthState.Healthy, status.State);
    }

    [Fact]
    public async Task CheckHealthAsync_RecordsFailure_OnFailedTest()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);
        _mockNewznabClient.Setup(c => c.TestConnectionAsync(_testIndexer1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabTestResult.Failed("Connection refused", statusCode: 503));

        var result = await _service.CheckHealthAsync("indexer-1");

        Assert.False(result.Success);
        Assert.Equal("Connection refused", result.ErrorMessage);

        var status = await _service.GetHealthAsync("indexer-1");
        Assert.Equal(1, status.FailureCount);
    }

    [Fact]
    public async Task CheckHealthAsync_DetectsRateLimiting_FromStatusCode()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);
        _mockNewznabClient.Setup(c => c.TestConnectionAsync(_testIndexer1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabTestResult.Failed("Too many requests", statusCode: 429));

        var result = await _service.CheckHealthAsync("indexer-1");

        Assert.True(result.IsRateLimited);

        var status = await _service.GetHealthAsync("indexer-1");
        Assert.True(status.IsRateLimited);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsNotFound_ForNonexistentIndexer()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer?)null);

        var result = await _service.CheckHealthAsync("nonexistent");

        Assert.False(result.Success);
        Assert.Equal("Indexer not found", result.ErrorMessage);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_ReturnsCorrectCounts()
    {
        var indexer3 = new NewznabIndexer
        {
            Id = "indexer-3",
            Name = "Test Indexer 3",
            BaseUrl = "https://test3.example.com",
            ApiKey = "api-key-3",
            Enabled = true,
            Priority = 30
        };
        var disabledIndexer = new NewznabIndexer
        {
            Id = "indexer-disabled",
            Name = "Disabled Indexer",
            BaseUrl = "https://disabled.example.com",
            ApiKey = "api-key-disabled",
            Enabled = false,
            Priority = 40
        };

        _mockIndexerProvider.Setup(p => p.GetIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewznabIndexer> { _testIndexer1, _testIndexer2, indexer3, disabledIndexer });

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordFailureAsync("indexer-2", "Rate limit", isRateLimited: true);
        for (int i = 0; i < 5; i++)
        {
            await _service.RecordFailureAsync("indexer-3", "Error");
        }

        var summary = await _service.GetHealthSummaryAsync();

        Assert.Equal(4, summary.TotalIndexers);
        Assert.Equal(3, summary.EnabledIndexers);
        Assert.Equal(1, summary.HealthyIndexers);
        Assert.Equal(1, summary.UnavailableIndexers);
        Assert.Equal(1, summary.OfflineIndexers);
        Assert.Equal(1, summary.RateLimitedIndexers);
        Assert.Equal(100, summary.AverageResponseTimeMs);
    }

    [Fact]
    public async Task SuccessRate_CalculatesCorrectly()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordFailureAsync("indexer-1", "Error");

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(3, status.SuccessCount);
        Assert.Equal(1, status.FailureCount);
        Assert.Equal(75, status.SuccessRate);
    }

    [Fact]
    public async Task DegradedState_TriggeredBySlowResponseTime()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(6000));

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(IndexerHealthState.Degraded, status.State);
        Assert.True(status.IsHealthy);
    }

    [Fact]
    public async Task DegradedState_TriggeredByLowSuccessRate()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordFailureAsync("indexer-1", "Error 1");

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(IndexerHealthState.Degraded, status.State);
    }

    [Fact]
    public async Task AverageResponseTime_CalculatesCorrectly()
    {
        _mockIndexerProvider.Setup(p => p.GetIndexerAsync("indexer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testIndexer1);

        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(100));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(200));
        await _service.RecordSuccessAsync("indexer-1", TimeSpan.FromMilliseconds(300));

        var status = await _service.GetHealthAsync("indexer-1");

        Assert.Equal(200, status.AverageResponseTimeMs);
        Assert.Equal(300, status.LastResponseTimeMs);
    }
}
