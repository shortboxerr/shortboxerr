using Moq;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Providers;
using Xunit;

namespace Shortboxerr.Tests;

public class DownloadClientHealthServiceTests
{
    private readonly Mock<IProviderManager> _mockProviderManager;
    private readonly DownloadClientHealthService _service;

    private readonly ProviderDefinition _testClient1 = new()
    {
        Id = 1,
        Name = "SABnzbd",
        Implementation = "SABnzbd",
        Type = ProviderType.Usenet,
        Category = ProviderCategory.DownloadClient,
        IsEnabled = true,
        Priority = 10
    };

    private readonly ProviderDefinition _testClient2 = new()
    {
        Id = 2,
        Name = "qBittorrent",
        Implementation = "qBittorrent",
        Type = ProviderType.Torrent,
        Category = ProviderCategory.DownloadClient,
        IsEnabled = true,
        Priority = 20
    };

    public DownloadClientHealthServiceTests()
    {
        _mockProviderManager = new Mock<IProviderManager>();
        _service = new DownloadClientHealthService(_mockProviderManager.Object);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsStatus_ForExistingClient()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(1, status.ProviderId);
        Assert.Equal("SABnzbd", status.ProviderName);
        Assert.Equal(DownloadClientState.Unknown, status.State);
    }

    [Fact]
    public async Task GetHealthAsync_ThrowsException_ForNonexistentClient()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderDefinition?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetHealthAsync(999));
    }

    [Fact]
    public async Task RecordSuccessAsync_UpdatesHealthStatus()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(15));
        var status = await _service.GetHealthAsync(1);

        Assert.Equal(DownloadClientState.Healthy, status.State);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(0, status.FailureCount);
        Assert.Equal(15, status.AverageDownloadTimeSeconds);
        Assert.NotNull(status.LastSuccessAt);
    }

    [Fact]
    public async Task RecordFailureAsync_UpdatesHealthStatus()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordFailureAsync(1, "Connection timeout");
        var status = await _service.GetHealthAsync(1);

        Assert.Equal(0, status.SuccessCount);
        Assert.Equal(1, status.FailureCount);
        Assert.Equal(1, status.ConsecutiveFailures);
        Assert.Equal("Connection timeout", status.LastErrorMessage);
        Assert.NotNull(status.LastFailureAt);
    }

    [Fact]
    public async Task ConsecutiveFailures_TriggersOfflineState()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        for (int i = 0; i < 3; i++)
        {
            await _service.RecordFailureAsync(1, $"Error {i}");
        }

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(DownloadClientState.Offline, status.State);
        Assert.Equal(3, status.ConsecutiveFailures);
    }

    [Fact]
    public async Task SuccessResetsConsecutiveFailures()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordFailureAsync(1, "Error 1");
        await _service.RecordFailureAsync(1, "Error 2");
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(2, status.FailureCount);
    }

    [Fact]
    public async Task GetAllHealthAsync_ReturnsStatusForAllClients()
    {
        _mockProviderManager.Setup(p => p.GetByCategoryAsync(ProviderCategory.DownloadClient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderDefinition> { _testClient1, _testClient2 });

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordFailureAsync(2, "Error");

        var statuses = await _service.GetAllHealthAsync();

        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, s => s.ProviderId == 1);
        Assert.Contains(statuses, s => s.ProviderId == 2);
    }

    [Fact]
    public async Task GetHealthyClientsAsync_ExcludesOfflineClients()
    {
        var mockClient1 = new Mock<IDownloadProvider>();
        mockClient1.Setup(c => c.Id).Returns(1);
        mockClient1.Setup(c => c.Name).Returns("SABnzbd");
        mockClient1.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient1.Setup(c => c.Priority).Returns(10);

        var mockClient2 = new Mock<IDownloadProvider>();
        mockClient2.Setup(c => c.Id).Returns(2);
        mockClient2.Setup(c => c.Name).Returns("qBittorrent");
        mockClient2.Setup(c => c.Type).Returns(ProviderType.Torrent);
        mockClient2.Setup(c => c.Priority).Returns(20);

        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockClient1.Object, mockClient2.Object });

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        for (int i = 0; i < 3; i++)
        {
            await _service.RecordFailureAsync(2, "Error");
        }

        var healthy = await _service.GetHealthyClientsAsync();

        Assert.Single(healthy);
        Assert.Equal(1, healthy[0].Id);
    }

    [Fact]
    public async Task GetHealthyClientsAsync_FiltersByType()
    {
        var mockClient1 = new Mock<IDownloadProvider>();
        mockClient1.Setup(c => c.Id).Returns(1);
        mockClient1.Setup(c => c.Name).Returns("SABnzbd");
        mockClient1.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient1.Setup(c => c.Priority).Returns(10);

        var mockClient2 = new Mock<IDownloadProvider>();
        mockClient2.Setup(c => c.Id).Returns(2);
        mockClient2.Setup(c => c.Name).Returns("qBittorrent");
        mockClient2.Setup(c => c.Type).Returns(ProviderType.Torrent);
        mockClient2.Setup(c => c.Priority).Returns(20);

        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockClient1.Object, mockClient2.Object });

        var usenetClients = await _service.GetHealthyClientsAsync(ProviderType.Usenet);

        Assert.Single(usenetClients);
        Assert.Equal(ProviderType.Usenet, usenetClients[0].Type);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_ForHealthyClient()
    {
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));

        var isAvailable = await _service.IsAvailableAsync(1);

        Assert.True(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_ForOfflineClient()
    {
        for (int i = 0; i < 3; i++)
        {
            await _service.RecordFailureAsync(1, "Error");
        }

        var isAvailable = await _service.IsAvailableAsync(1);

        Assert.False(isAvailable);
    }

    [Fact]
    public async Task ResetHealthAsync_ClearsHealthData()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.ResetHealthAsync(1);
        var status = await _service.GetHealthAsync(1);

        Assert.Equal(DownloadClientState.Unknown, status.State);
        Assert.Equal(0, status.SuccessCount);
        Assert.Equal(0, status.FailureCount);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_ReturnsCorrectCounts()
    {
        var client3 = new ProviderDefinition
        {
            Id = 3,
            Name = "NZBGet",
            Implementation = "NZBGet",
            Type = ProviderType.Usenet,
            Category = ProviderCategory.DownloadClient,
            IsEnabled = true,
            Priority = 30
        };
        var disabledClient = new ProviderDefinition
        {
            Id = 4,
            Name = "Disabled Client",
            Implementation = "Other",
            Type = ProviderType.Usenet,
            Category = ProviderCategory.DownloadClient,
            IsEnabled = false,
            Priority = 40
        };

        _mockProviderManager.Setup(p => p.GetByCategoryAsync(ProviderCategory.DownloadClient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderDefinition> { _testClient1, _testClient2, client3, disabledClient });

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordFailureAsync(2, "Rate limit");
        for (int i = 0; i < 3; i++)
        {
            await _service.RecordFailureAsync(3, "Error");
        }

        var summary = await _service.GetHealthSummaryAsync();

        Assert.Equal(4, summary.TotalClients);
        Assert.Equal(3, summary.EnabledClients);
        Assert.Equal(1, summary.HealthyClients);
        Assert.Equal(1, summary.OfflineClients);
        Assert.Equal(10, summary.AverageDownloadTimeSeconds);
    }

    [Fact]
    public async Task SuccessRate_CalculatesCorrectly()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordFailureAsync(1, "Error");

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(3, status.SuccessCount);
        Assert.Equal(1, status.FailureCount);
        Assert.Equal(75, status.SuccessRate);
    }

    [Fact]
    public async Task DegradedState_TriggeredBySlowDownloadTime()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(400));

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(DownloadClientState.Degraded, status.State);
        Assert.True(status.IsHealthy);
    }

    [Fact]
    public async Task AverageDownloadTime_CalculatesCorrectly()
    {
        _mockProviderManager.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testClient1);

        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(10));
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(20));
        await _service.RecordSuccessAsync(1, TimeSpan.FromSeconds(30));

        var status = await _service.GetHealthAsync(1);

        Assert.Equal(20, status.AverageDownloadTimeSeconds);
        Assert.Equal(30, status.LastDownloadTimeSeconds);
    }

    [Fact]
    public async Task DownloadWithFailoverAsync_ReturnsNoClients_WhenNoneAvailable()
    {
        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        var candidate = CreateTestCandidate();

        var result = await _service.DownloadWithFailoverAsync(candidate);

        Assert.False(result.Success);
        Assert.Equal(0, result.AttemptsCount);
        Assert.Equal("No healthy download clients available", result.FinalErrorMessage);
    }

    [Fact]
    public async Task DownloadWithFailoverAsync_SucceedsOnFirstClient()
    {
        var mockClient = new Mock<IDownloadProvider>();
        mockClient.Setup(c => c.Id).Returns(1);
        mockClient.Setup(c => c.Name).Returns("SABnzbd");
        mockClient.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient.Setup(c => c.Priority).Returns(10);
        mockClient.Setup(c => c.DownloadAsync(It.IsAny<Candidate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadResult { Success = true, DownloadId = "test-id" });

        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockClient.Object });

        var candidate = CreateTestCandidate();

        var result = await _service.DownloadWithFailoverAsync(candidate);

        Assert.True(result.Success);
        Assert.Equal("test-id", result.DownloadId);
        Assert.Equal(1, result.UsedProviderId);
        Assert.Equal("SABnzbd", result.UsedProviderName);
        Assert.Single(result.Attempts);
    }

    [Fact]
    public async Task DownloadWithFailoverAsync_FailsOverToNextClient()
    {
        var mockClient1 = new Mock<IDownloadProvider>();
        mockClient1.Setup(c => c.Id).Returns(1);
        mockClient1.Setup(c => c.Name).Returns("SABnzbd");
        mockClient1.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient1.Setup(c => c.Priority).Returns(10);
        mockClient1.Setup(c => c.DownloadAsync(It.IsAny<Candidate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadResult { Success = false, Error = "Queue full" });

        var mockClient2 = new Mock<IDownloadProvider>();
        mockClient2.Setup(c => c.Id).Returns(2);
        mockClient2.Setup(c => c.Name).Returns("NZBGet");
        mockClient2.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient2.Setup(c => c.Priority).Returns(20);
        mockClient2.Setup(c => c.DownloadAsync(It.IsAny<Candidate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadResult { Success = true, DownloadId = "nzbget-id" });

        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockClient1.Object, mockClient2.Object });

        var candidate = CreateTestCandidate();

        var result = await _service.DownloadWithFailoverAsync(candidate);

        Assert.True(result.Success);
        Assert.Equal("nzbget-id", result.DownloadId);
        Assert.Equal(2, result.UsedProviderId);
        Assert.Equal("NZBGet", result.UsedProviderName);
        Assert.Equal(2, result.AttemptsCount);
        Assert.False(result.Attempts[0].Success);
        Assert.True(result.Attempts[1].Success);
    }

    [Fact]
    public async Task DownloadWithFailoverAsync_AllClientsFail()
    {
        var mockClient1 = new Mock<IDownloadProvider>();
        mockClient1.Setup(c => c.Id).Returns(1);
        mockClient1.Setup(c => c.Name).Returns("SABnzbd");
        mockClient1.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient1.Setup(c => c.Priority).Returns(10);
        mockClient1.Setup(c => c.DownloadAsync(It.IsAny<Candidate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadResult { Success = false, Error = "Queue full" });

        var mockClient2 = new Mock<IDownloadProvider>();
        mockClient2.Setup(c => c.Id).Returns(2);
        mockClient2.Setup(c => c.Name).Returns("NZBGet");
        mockClient2.Setup(c => c.Type).Returns(ProviderType.Usenet);
        mockClient2.Setup(c => c.Priority).Returns(20);
        mockClient2.Setup(c => c.DownloadAsync(It.IsAny<Candidate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadResult { Success = false, Error = "Connection refused" });

        _mockProviderManager.Setup(p => p.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockClient1.Object, mockClient2.Object });

        var candidate = CreateTestCandidate();

        var result = await _service.DownloadWithFailoverAsync(candidate);

        Assert.False(result.Success);
        Assert.Equal(2, result.AttemptsCount);
        Assert.Equal("Connection refused", result.FinalErrorMessage);
    }

    private static Candidate CreateTestCandidate() => new()
    {
        Id = Guid.NewGuid().ToString(),
        ReleaseTitle = "Test Release",
        Source = "test-source"
    };
}
