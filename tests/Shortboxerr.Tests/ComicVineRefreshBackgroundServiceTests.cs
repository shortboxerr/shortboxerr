using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

public class ComicVineRefreshBackgroundServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<IPullListService> _mockPullListService;
    private readonly Mock<ILogger<ComicVineRefreshBackgroundService>> _mockLogger;
    private readonly ServiceProvider _serviceProvider;

    public ComicVineRefreshBackgroundServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockPullListService = new Mock<IPullListService>();
        _mockLogger = new Mock<ILogger<ComicVineRefreshBackgroundService>>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockSettingsService.Object);
        services.AddSingleton(_mockComicVineClient.Object);
        services.AddSingleton(_mockPullListService.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenDisabled_DoesNotRefresh()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { DiscoveryRefreshEnabled = false });

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenApiNotConfigured_DoesNotRefresh()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { DiscoveryRefreshEnabled = true });

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenEnabled_RefreshesMultipleWeeks()
    {
        // Arrange
        var settings = new ComicVineSettings
        {
            DiscoveryRefreshEnabled = true,
            DiscoveryRefreshIntervalHours = 4,
            DiscoveryRefreshWeeksAhead = 3,
            DiscoveryRefreshAllowedHours = new List<int>() // All hours allowed
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockPullListService
            .Setup(p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyDiscoveryList());

        _mockSettingsService
            .Setup(s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert - should refresh 3 weeks (current + 2 ahead)
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        
        _mockSettingsService.Verify(
            s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenOutsideAllowedHours_DoesNotRefresh()
    {
        // Arrange
        var currentHour = DateTime.Now.Hour;
        var nonAllowedHours = Enumerable.Range(0, 24)
            .Where(h => h != currentHour)
            .ToList();

        var settings = new ComicVineSettings
        {
            DiscoveryRefreshEnabled = true,
            DiscoveryRefreshIntervalHours = 4,
            DiscoveryRefreshWeeksAhead = 3,
            DiscoveryRefreshAllowedHours = nonAllowedHours
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TriggerRefreshAsync_WhenWithinAllowedHours_DoesRefresh()
    {
        // Arrange
        var currentHour = DateTime.Now.Hour;
        var settings = new ComicVineSettings
        {
            DiscoveryRefreshEnabled = true,
            DiscoveryRefreshIntervalHours = 4,
            DiscoveryRefreshWeeksAhead = 2,
            DiscoveryRefreshAllowedHours = new List<int> { currentHour }
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockPullListService
            .Setup(p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyDiscoveryList());

        _mockSettingsService
            .Setup(s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert - should refresh 2 weeks
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TriggerRefreshAsync_WithDefaultSettings_RefreshesFourWeeks()
    {
        // Arrange - use default settings (4 hours, 4 weeks)
        var settings = new ComicVineSettings
        {
            DiscoveryRefreshEnabled = true
            // Defaults: DiscoveryRefreshIntervalHours = 4, DiscoveryRefreshWeeksAhead = 4
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockPullListService
            .Setup(p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyDiscoveryList());

        _mockSettingsService
            .Setup(s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert - should refresh 4 weeks (default)
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task TriggerRefreshAsync_ContinuesOnPartialFailure()
    {
        // Arrange
        var settings = new ComicVineSettings
        {
            DiscoveryRefreshEnabled = true,
            DiscoveryRefreshWeeksAhead = 3
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var callCount = 0;
        _mockPullListService
            .Setup(p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2) throw new Exception("Simulated failure");
                return new WeeklyDiscoveryList();
            });

        _mockSettingsService
            .Setup(s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ComicVineRefreshBackgroundService(_serviceProvider, _mockLogger.Object);

        // Act
        await service.TriggerRefreshAsync();

        // Assert - all 3 weeks should be attempted despite one failure
        _mockPullListService.Verify(
            p => p.GetWeeklyDiscoveryAsync(It.IsAny<DateTime>(), It.IsAny<DiscoveryFilter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        
        // Last refresh should still be saved
        _mockSettingsService.Verify(
            s => s.SetAsync("comicvine_discovery_last_refresh", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
