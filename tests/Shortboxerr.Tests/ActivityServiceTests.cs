using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Activity;
using Xunit;

namespace Shortboxerr.Tests;

public class ActivityServiceTests : IAsyncLifetime
{
    private readonly Mock<IProviderManager> _providerManagerMock;
    private readonly Mock<ILogger<ActivityService>> _loggerMock;
    private readonly ActivityService _service;

    public ActivityServiceTests()
    {
        _providerManagerMock = new Mock<IProviderManager>();
        _loggerMock = new Mock<ILogger<ActivityService>>();
        _service = new ActivityService(
            _providerManagerMock.Object, 
            ddlDownloadService: null, 
            downloadHistoryService: null,
            _loggerMock.Object);
    }

    public async Task InitializeAsync()
    {
        // Clear static history before each test to ensure isolation
        await _service.ClearAllHistoryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region GetActiveDownloadsAsync Tests

    [Fact]
    public async Task GetActiveDownloadsAsync_WithNoProviders_ReturnsEmptyList()
    {
        // Arrange
        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        // Act
        var result = await _service.GetActiveDownloadsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveDownloadsAsync_WithProviderDownloads_ReturnsActivities()
    {
        // Arrange
        var mockProvider = new Mock<IDownloadProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestClient");
        mockProvider.Setup(p => p.Id).Returns(1);
        mockProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        mockProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus
                {
                    DownloadId = "test-123",
                    State = DownloadState.Downloading,
                    Progress = 50,
                    TotalBytes = 100 * 1024 * 1024,
                    DownloadedBytes = 50 * 1024 * 1024,
                    SpeedBytesPerSecond = 1024 * 1024,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CandidateTitle = "Test Comic #1"
                }
            });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockProvider.Object });

        // Act
        var result = await _service.GetActiveDownloadsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("test-123", result[0].Id);
        Assert.Equal("Test Comic #1", result[0].Title);
        Assert.Equal(ActivityState.Downloading, result[0].State);
        Assert.Equal(50, result[0].Progress);
        Assert.Equal(DownloadSourceType.Nzb, result[0].SourceType);
    }

    [Fact]
    public async Task GetActiveDownloadsAsync_WithMultipleProviders_AggregatesResults()
    {
        // Arrange
        var nzbProvider = new Mock<IDownloadProvider>();
        nzbProvider.Setup(p => p.Name).Returns("SABnzbd");
        nzbProvider.Setup(p => p.Id).Returns(1);
        nzbProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        nzbProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus { DownloadId = "nzb-1", State = DownloadState.Downloading, StartedAt = DateTime.UtcNow }
            });

        var torrentProvider = new Mock<IDownloadProvider>();
        torrentProvider.Setup(p => p.Name).Returns("qBittorrent");
        torrentProvider.Setup(p => p.Id).Returns(2);
        torrentProvider.Setup(p => p.Type).Returns(ProviderType.Torrent);
        torrentProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus { DownloadId = "torrent-1", State = DownloadState.Queued, StartedAt = DateTime.UtcNow }
            });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { nzbProvider.Object, torrentProvider.Object });

        // Act
        var result = await _service.GetActiveDownloadsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Id == "nzb-1" && a.SourceType == DownloadSourceType.Nzb);
        Assert.Contains(result, a => a.Id == "torrent-1" && a.SourceType == DownloadSourceType.Torrent);
    }

    [Fact]
    public async Task GetActiveDownloadsAsync_WithProviderError_ContinuesToOtherProviders()
    {
        // Arrange
        var failingProvider = new Mock<IDownloadProvider>();
        failingProvider.Setup(p => p.Name).Returns("Failing");
        failingProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provider error"));

        var workingProvider = new Mock<IDownloadProvider>();
        workingProvider.Setup(p => p.Name).Returns("Working");
        workingProvider.Setup(p => p.Id).Returns(1);
        workingProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        workingProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus { DownloadId = "working-1", State = DownloadState.Downloading, StartedAt = DateTime.UtcNow }
            });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { failingProvider.Object, workingProvider.Object });

        // Act
        var result = await _service.GetActiveDownloadsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("working-1", result[0].Id);
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var mockProvider = new Mock<IDownloadProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestClient");
        mockProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        mockProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus { DownloadId = "1", State = DownloadState.Downloading, SpeedBytesPerSecond = 1024 * 1024, StartedAt = DateTime.UtcNow },
                new DownloadStatus { DownloadId = "2", State = DownloadState.Downloading, SpeedBytesPerSecond = 512 * 1024, StartedAt = DateTime.UtcNow },
                new DownloadStatus { DownloadId = "3", State = DownloadState.Queued, StartedAt = DateTime.UtcNow }
            });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockProvider.Object });

        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        Assert.Equal(2, summary.ActiveCount);
        Assert.Equal(1, summary.QueuedCount);
        Assert.True(summary.TotalSpeedBytesPerSecond > 0);
        Assert.True(summary.IsDownloading);
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoDownloads_ReturnsZeroCounts()
    {
        // Arrange
        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        // Act
        var summary = await _service.GetSummaryAsync();

        // Assert
        Assert.Equal(0, summary.ActiveCount);
        Assert.Equal(0, summary.QueuedCount);
        Assert.False(summary.IsDownloading);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task GetRecentHistoryAsync_ReturnsEmptyWhenNoHistory()
    {
        // Act
        var result = await _service.GetRecentHistoryAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddToHistory_And_GetRecentHistoryAsync_Works()
    {
        // Arrange
        var activity = new DownloadActivity
        {
            Id = "hist-1",
            ClientName = "Test",
            Title = "Test Comic",
            State = ActivityState.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Nzb
        };

        // Act
        _service.AddToHistory(activity);
        var history = await _service.GetRecentHistoryAsync();

        // Assert
        Assert.Single(history);
        Assert.Equal("hist-1", history[0].Id);
    }

    [Fact]
    public async Task RemoveFromHistoryAsync_RemovesItem()
    {
        // Arrange
        _service.AddToHistory(new DownloadActivity
        {
            Id = "to-remove",
            ClientName = "Test",
            Title = "Test Comic",
            State = ActivityState.Completed,
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Nzb
        });

        // Act
        var result = await _service.RemoveFromHistoryAsync("to-remove");
        var history = await _service.GetRecentHistoryAsync();

        // Assert
        Assert.True(result);
        Assert.Empty(history);
    }

    [Fact]
    public async Task RemoveFromHistoryAsync_ReturnsFalseWhenNotFound()
    {
        // Act
        var result = await _service.RemoveFromHistoryAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ClearCompletedAsync_RemovesOnlyCompleted()
    {
        // Arrange
        _service.AddToHistory(new DownloadActivity
        {
            Id = "completed-1",
            ClientName = "Test",
            Title = "Completed",
            State = ActivityState.Completed,
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Nzb
        });
        _service.AddToHistory(new DownloadActivity
        {
            Id = "failed-1",
            ClientName = "Test",
            Title = "Failed",
            State = ActivityState.Failed,
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Nzb
        });

        // Act
        var removed = await _service.ClearCompletedAsync();
        var history = await _service.GetRecentHistoryAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.Single(history);
        Assert.Equal("failed-1", history[0].Id);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_FindsActiveDownload()
    {
        // Arrange
        var mockProvider = new Mock<IDownloadProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestClient");
        mockProvider.Setup(p => p.Id).Returns(1);
        mockProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        mockProvider.Setup(p => p.GetActiveDownloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadStatus>
            {
                new DownloadStatus { DownloadId = "active-1", State = DownloadState.Downloading, StartedAt = DateTime.UtcNow }
            });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockProvider.Object });

        // Act
        var result = await _service.GetByIdAsync("active-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("active-1", result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_FindsHistoryItem()
    {
        // Arrange
        _service.AddToHistory(new DownloadActivity
        {
            Id = "hist-item",
            ClientName = "Test",
            Title = "History Item",
            State = ActivityState.Completed,
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Nzb
        });

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        // Act
        var result = await _service.GetByIdAsync("hist-item");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hist-item", result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        // Act
        var result = await _service.GetByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task CancelAsync_CancelsDownload()
    {
        // Arrange
        var mockProvider = new Mock<IDownloadProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestClient");
        mockProvider.Setup(p => p.Id).Returns(1);
        mockProvider.Setup(p => p.Type).Returns(ProviderType.Usenet);
        mockProvider.Setup(p => p.GetStatusAsync("cancel-me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadStatus { DownloadId = "cancel-me", State = DownloadState.Downloading, StartedAt = DateTime.UtcNow });
        mockProvider.Setup(p => p.CancelAsync("cancel-me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider> { mockProvider.Object });

        // Act
        var result = await _service.CancelAsync("cancel-me");

        // Assert
        Assert.True(result);
        mockProvider.Verify(p => p.CancelAsync("cancel-me", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ReturnsFalseWhenProviderNotFound()
    {
        // Arrange
        _providerManagerMock
            .Setup(m => m.GetDownloadClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IDownloadProvider>());

        // Act
        var result = await _service.CancelAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    #endregion
}

public class DownloadActivityTests
{
    [Fact]
    public void ProgressDisplay_FormatsBytesCorrectly()
    {
        // Arrange
        var activity = new DownloadActivity
        {
            Id = "test",
            ClientName = "Test",
            Title = "Test",
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Ddl,
            TotalBytes = 100 * 1024 * 1024, // 100 MB
            DownloadedBytes = 50 * 1024 * 1024 // 50 MB
        };

        // Assert
        Assert.Contains("50", activity.ProgressDisplay);
        Assert.Contains("MB", activity.ProgressDisplay);
        Assert.Contains("100", activity.ProgressDisplay);
    }

    [Fact]
    public void SpeedDisplay_FormatsSpeedCorrectly()
    {
        // Arrange
        var activity = new DownloadActivity
        {
            Id = "test",
            ClientName = "Test",
            Title = "Test",
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Ddl,
            SpeedBytesPerSecond = 5 * 1024 * 1024 // 5 MB/s
        };

        // Assert
        Assert.Contains("MB/s", activity.SpeedDisplay);
    }

    [Fact]
    public void EtaDisplay_FormatsTimeCorrectly()
    {
        // Arrange
        var activity = new DownloadActivity
        {
            Id = "test",
            ClientName = "Test",
            Title = "Test",
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Ddl,
            EstimatedTimeRemaining = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30)
        };

        // Assert
        Assert.Contains("5m", activity.EtaDisplay);
        Assert.Contains("30s", activity.EtaDisplay);
    }

    [Fact]
    public void EtaDisplay_HandlesHours()
    {
        // Arrange
        var activity = new DownloadActivity
        {
            Id = "test",
            ClientName = "Test",
            Title = "Test",
            StartedAt = DateTime.UtcNow,
            SourceType = DownloadSourceType.Ddl,
            EstimatedTimeRemaining = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15)
        };

        // Assert
        Assert.Contains("2h", activity.EtaDisplay);
        Assert.Contains("15m", activity.EtaDisplay);
    }
}

public class ActivitySummaryTests
{
    [Fact]
    public void TotalSpeedDisplay_FormatsCorrectly()
    {
        // Arrange
        var summary = new ActivitySummary
        {
            TotalSpeedBytesPerSecond = 10 * 1024 * 1024 // 10 MB/s
        };

        // Assert
        Assert.Contains("MB/s", summary.TotalSpeedDisplay);
    }

    [Fact]
    public void IsDownloading_TrueWhenActive()
    {
        // Arrange
        var summary = new ActivitySummary
        {
            ActiveCount = 2
        };

        // Assert
        Assert.True(summary.IsDownloading);
    }

    [Fact]
    public void HasFailures_TrueWhenFailed()
    {
        // Arrange
        var summary = new ActivitySummary
        {
            FailedCount = 1
        };

        // Assert
        Assert.True(summary.HasFailures);
    }
}
