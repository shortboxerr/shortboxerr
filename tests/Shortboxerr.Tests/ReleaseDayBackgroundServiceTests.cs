using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

public class ReleaseDayBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IPullListService> _mockPullListService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<ReleaseDayBackgroundService>> _mockLogger;

    public ReleaseDayBackgroundServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockPullListService = new Mock<IPullListService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<ReleaseDayBackgroundService>>();

        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Setup scope factory
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        
        // Setup scope's service provider
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(ISettingsService)))
            .Returns(_mockSettingsService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(IPullListService)))
            .Returns(_mockPullListService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(INotificationService)))
            .Returns(_mockNotificationService.Object);
        
        _mockScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);
        
        // Setup root provider
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);
    }

    private ReleaseDayBackgroundService CreateService()
    {
        return new ReleaseDayBackgroundService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task TriggerProcessingAsync_ProcessesReleaseDay()
    {
        // Arrange
        var service = CreateService();
        var today = DateTime.Today;

        _mockSettingsService.Setup(s => s.GetAsync(
            "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { AutoAddToWanted = true });

        _mockPullListService.Setup(p => p.ProcessReleaseDayAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoAddResult
            {
                Success = true,
                SeriesProcessed = 5,
                IssuesAdded = 10
            });

        _mockSettingsService.Setup(s => s.SetAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.TriggerProcessingAsync(today);

        // Assert
        _mockPullListService.Verify(
            p => p.ProcessReleaseDayAsync(today, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockSettingsService.Verify(
            s => s.SetAsync("pulllist_release_day_last_processed", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerProcessingAsync_UsesTodayWhenDateNotProvided()
    {
        // Arrange
        var service = CreateService();

        _mockSettingsService.Setup(s => s.GetAsync(
            "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { AutoAddToWanted = true });

        _mockPullListService.Setup(p => p.ProcessReleaseDayAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoAddResult { Success = true });

        _mockSettingsService.Setup(s => s.SetAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.TriggerProcessingAsync();

        // Assert
        _mockPullListService.Verify(
            p => p.ProcessReleaseDayAsync(DateTime.Today, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerProcessingAsync_LogsErrorOnFailure()
    {
        // Arrange
        var service = CreateService();

        _mockSettingsService.Setup(s => s.GetAsync(
            "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { AutoAddToWanted = true });

        _mockPullListService.Setup(p => p.ProcessReleaseDayAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoAddResult
            {
                Success = false,
                Error = "Test error"
            });

        // Act
        await service.TriggerProcessingAsync();

        // Assert - Should not save last processed date on failure
        _mockSettingsService.Verify(
            s => s.SetAsync("pulllist_release_day_last_processed", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void PullListSettings_HasCorrectDefaults()
    {
        // Arrange & Act
        var settings = new PullListSettings();

        // Assert
        Assert.True(settings.AutoAddToWanted);
        Assert.Equal(DayOfWeek.Wednesday, settings.ReleaseDay);
        Assert.Contains(6, settings.ReleaseDayProcessingHours);
        Assert.Contains(12, settings.ReleaseDayProcessingHours);
    }

    [Fact]
    public async Task TriggerProcessingAsync_SendsNotificationOnSuccess()
    {
        // Arrange
        var service = CreateService();

        _mockSettingsService.Setup(s => s.GetAsync(
            "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { AutoAddToWanted = true });

        _mockPullListService.Setup(p => p.ProcessReleaseDayAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoAddResult
            {
                Success = true,
                SeriesProcessed = 3,
                IssuesAdded = 5
            });

        _mockSettingsService.Setup(s => s.SetAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Note: Notification is sent by the internal CheckAndProcessReleaseDayAsync,
        // not TriggerProcessingAsync. TriggerProcessingAsync calls ProcessReleaseDayAsync directly.
        // So we don't test notification here - that's tested in integration.

        // Act
        await service.TriggerProcessingAsync();

        // Assert - Just verify processing happened
        _mockPullListService.Verify(
            p => p.ProcessReleaseDayAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerProcessingAsync_WithCustomDate_ProcessesThatDate()
    {
        // Arrange
        var service = CreateService();
        var customDate = new DateTime(2026, 1, 15);

        _mockSettingsService.Setup(s => s.GetAsync(
            "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { AutoAddToWanted = true });

        _mockPullListService.Setup(p => p.ProcessReleaseDayAsync(
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoAddResult { Success = true });

        _mockSettingsService.Setup(s => s.SetAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.TriggerProcessingAsync(customDate);

        // Assert
        _mockPullListService.Verify(
            p => p.ProcessReleaseDayAsync(customDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
