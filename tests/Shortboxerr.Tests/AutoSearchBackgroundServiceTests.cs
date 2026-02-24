using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

public class AutoSearchBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IAutoSearchService> _mockAutoSearchService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<AutoSearchBackgroundService>> _mockLogger;

    public AutoSearchBackgroundServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockAutoSearchService = new Mock<IAutoSearchService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<AutoSearchBackgroundService>>();

        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(ISettingsService)))
            .Returns(_mockSettingsService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(IAutoSearchService)))
            .Returns(_mockAutoSearchService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(INotificationService)))
            .Returns(_mockNotificationService.Object);

        _mockScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);
    }

    private AutoSearchBackgroundService CreateService()
    {
        return new AutoSearchBackgroundService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task TriggerSearchAsync_CallsAutoSearchService()
    {
        var service = CreateService();

        _mockAutoSearchService.Setup(a => a.SearchAllWantedAsync(
            It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoSearchBatchResult
            {
                TotalSearched = 5,
                SuccessCount = 3,
                FailedCount = 0,
                NotFoundCount = 2,
                Results = Array.Empty<AutoSearchResult>()
            });

        await service.TriggerSearchAsync(10);

        _mockAutoSearchService.Verify(
            a => a.SearchAllWantedAsync(10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerSearchAsync_WithNoLimit_CallsWithNull()
    {
        var service = CreateService();

        _mockAutoSearchService.Setup(a => a.SearchAllWantedAsync(
            It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutoSearchBatchResult.Empty);

        await service.TriggerSearchAsync();

        _mockAutoSearchService.Verify(
            a => a.SearchAllWantedAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SearchSettings_HasCorrectDefaults()
    {
        var settings = new SearchSettings();

        Assert.False(settings.AutoSearchEnabled);
        Assert.Equal(24, settings.AutoSearchIntervalHours);
    }

    [Fact]
    public void SearchSettings_Key_IsCorrect()
    {
        Assert.Equal("Search:Settings", SearchSettings.SettingsKey);
    }

    [Fact]
    public void AutoSearchBatchResult_CalculatesTotalsCorrectly()
    {
        var result = new AutoSearchBatchResult
        {
            Results = new List<AutoSearchResult>
            {
                new() { IssueId = 1, Success = true, CandidatesFound = 3, SeriesTitle = "Batman", IssueNumber = "1" },
                new() { IssueId = 2, Success = true, CandidatesFound = 5, SeriesTitle = "Superman", IssueNumber = "10" },
                new() { IssueId = 3, Success = false, CandidatesFound = 0, Error = "Not found", SeriesTitle = "Unknown", IssueNumber = "1" }
            },
            TotalSearched = 3,
            SuccessCount = 2,
            NotFoundCount = 1,
            FailedCount = 0
        };

        Assert.Equal(3, result.TotalSearched);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.NotFoundCount);
    }

    [Fact]
    public void AutoSearchResult_ContainsExpectedProperties()
    {
        var result = new AutoSearchResult
        {
            IssueId = 123,
            SeriesTitle = "Amazing Spider-Man",
            IssueNumber = "50",
            Success = true,
            CandidatesFound = 10,
            SelectedCandidateTitle = "NZBgeek - ASM.050.cbz",
            Duration = TimeSpan.FromMilliseconds(1500)
        };

        Assert.Equal(123, result.IssueId);
        Assert.Equal("Amazing Spider-Man", result.SeriesTitle);
        Assert.Equal("50", result.IssueNumber);
        Assert.True(result.Success);
        Assert.Equal(10, result.CandidatesFound);
    }

    [Fact]
    public void AutoSearchStatus_ContainsExpectedProperties()
    {
        var status = new AutoSearchStatus
        {
            Enabled = true,
            IsRunning = true,
            WantedIssuesCount = 100,
            SearchableCount = 50,
            LastRunAt = DateTime.UtcNow.AddHours(-2),
            TodaySearchCount = 30,
            TodayFoundCount = 15
        };

        Assert.True(status.IsRunning);
        Assert.True(status.Enabled);
        Assert.NotNull(status.LastRunAt);
        Assert.Equal(100, status.WantedIssuesCount);
        Assert.Equal(50, status.SearchableCount);
    }

    [Fact]
    public async Task TriggerSearchAsync_HandlesEmptyResults()
    {
        var service = CreateService();

        _mockAutoSearchService.Setup(a => a.SearchAllWantedAsync(
            It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutoSearchBatchResult.Empty);

        await service.TriggerSearchAsync(10);

        _mockAutoSearchService.Verify(
            a => a.SearchAllWantedAsync(10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerSearchAsync_HandlesCancellation()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();

        _mockAutoSearchService.Setup(a => a.SearchAllWantedAsync(
            It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.TriggerSearchAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public void AutoSearchBatchResult_Empty_ReturnsCorrectValues()
    {
        var empty = AutoSearchBatchResult.Empty;

        Assert.Equal(0, empty.TotalSearched);
        Assert.Equal(0, empty.SuccessCount);
        Assert.Equal(0, empty.FailedCount);
        Assert.Equal(0, empty.NotFoundCount);
        Assert.Empty(empty.Results);
    }
}
