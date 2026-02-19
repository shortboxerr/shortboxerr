using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Services;
using Xunit;

namespace Shortboxerr.Tests;

public class SetupStatusServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ILogger<SetupStatusService>> _mockLogger;
    private readonly SetupStatusService _service;

    public SetupStatusServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: $"SetupStatusTests_{Guid.NewGuid()}")
            .Options;

        _context = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockLogger = new Mock<ILogger<SetupStatusService>>();

        // Default setup - nothing configured
        _mockSettingsService.Setup(s => s.GetAsync<bool>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "/comics" });
        _mockComicVineClient.Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _service = new SetupStatusService(
            _context,
            _mockSettingsService.Object,
            _mockComicVineClient.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_NothingConfigured_ReturnsIncomplete()
    {
        var status = await _service.GetStatusAsync();

        Assert.False(status.IsComplete);
        Assert.False(status.IsDismissed);
        Assert.True(status.ShouldShowOnboarding);
        Assert.Equal(0, status.CompletionPercentage);
        Assert.Equal(SetupStep.ConfigureComicVine, status.CurrentStep);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAllSteps()
    {
        var status = await _service.GetStatusAsync();

        Assert.Equal(5, status.Steps.Count);
        Assert.Contains(status.Steps, s => s.Step == SetupStep.ConfigureComicVine);
        Assert.Contains(status.Steps, s => s.Step == SetupStep.ConfigureRootFolder);
        Assert.Contains(status.Steps, s => s.Step == SetupStep.AddSeries);
        Assert.Contains(status.Steps, s => s.Step == SetupStep.ConfigureDownloadClient);
        Assert.Contains(status.Steps, s => s.Step == SetupStep.ConfigureIndexer);
    }

    [Fact]
    public async Task GetStatusAsync_StepsInCorrectOrder()
    {
        var status = await _service.GetStatusAsync();

        var orderedSteps = status.Steps.OrderBy(s => s.Order).ToList();
        Assert.Equal(SetupStep.ConfigureComicVine, orderedSteps[0].Step);
        Assert.Equal(SetupStep.ConfigureRootFolder, orderedSteps[1].Step);
        Assert.Equal(SetupStep.AddSeries, orderedSteps[2].Step);
        Assert.Equal(SetupStep.ConfigureDownloadClient, orderedSteps[3].Step);
        Assert.Equal(SetupStep.ConfigureIndexer, orderedSteps[4].Step);
    }

    [Fact]
    public async Task GetStatusAsync_RequiredStepsMarked()
    {
        var status = await _service.GetStatusAsync();

        // ComicVine, RootFolder, and AddSeries are required
        Assert.True(status.Steps.First(s => s.Step == SetupStep.ConfigureComicVine).IsRequired);
        Assert.True(status.Steps.First(s => s.Step == SetupStep.ConfigureRootFolder).IsRequired);
        Assert.True(status.Steps.First(s => s.Step == SetupStep.AddSeries).IsRequired);
        
        // Download client and indexer are optional
        Assert.False(status.Steps.First(s => s.Step == SetupStep.ConfigureDownloadClient).IsRequired);
        Assert.False(status.Steps.First(s => s.Step == SetupStep.ConfigureIndexer).IsRequired);
    }

    [Fact]
    public async Task GetStatusAsync_ComicVineConfigured_StepComplete()
    {
        _mockComicVineClient.Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var status = await _service.GetStatusAsync();

        var cvStep = status.Steps.First(s => s.Step == SetupStep.ConfigureComicVine);
        Assert.True(cvStep.IsComplete);
        Assert.Equal("API key configured", cvStep.Details);
        Assert.Equal(SetupStep.ConfigureRootFolder, status.CurrentStep);
    }

    [Fact]
    public async Task GetStatusAsync_RootFolderConfigured_StepComplete()
    {
        _mockComicVineClient.Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "/mnt/comics" });

        var status = await _service.GetStatusAsync();

        var folderStep = status.Steps.First(s => s.Step == SetupStep.ConfigureRootFolder);
        Assert.True(folderStep.IsComplete);
        Assert.Equal("/mnt/comics", folderStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_DefaultRootFolder_NotComplete()
    {
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "/comics" });

        var status = await _service.GetStatusAsync();

        var folderStep = status.Steps.First(s => s.Step == SetupStep.ConfigureRootFolder);
        Assert.False(folderStep.IsComplete);
        Assert.Equal("Using default path", folderStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_SeriesAdded_StepComplete()
    {
        _context.Series.Add(new Series { Title = "Test Series", Monitored = true });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var seriesStep = status.Steps.First(s => s.Step == SetupStep.AddSeries);
        Assert.True(seriesStep.IsComplete);
        Assert.Equal("1 series monitored", seriesStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_MultipleSeriesAdded_ShowsCount()
    {
        _context.Series.AddRange(
            new Series { Title = "Series 1", Monitored = true },
            new Series { Title = "Series 2", Monitored = true },
            new Series { Title = "Series 3", Monitored = false }
        );
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var seriesStep = status.Steps.First(s => s.Step == SetupStep.AddSeries);
        Assert.True(seriesStep.IsComplete);
        Assert.Equal("2 series monitored", seriesStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_UnmonitoredSeriesOnly_NotComplete()
    {
        _context.Series.Add(new Series { Title = "Test Series", Monitored = false });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var seriesStep = status.Steps.First(s => s.Step == SetupStep.AddSeries);
        Assert.False(seriesStep.IsComplete);
    }

    [Fact]
    public async Task GetStatusAsync_DownloadClientConfigured_StepComplete()
    {
        _context.Providers.Add(new ProviderDefinition
        {
            Name = "SABnzbd",
            Implementation = "Sabnzbd",
            Category = ProviderCategory.DownloadClient,
            IsEnabled = true
        });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var clientStep = status.Steps.First(s => s.Step == SetupStep.ConfigureDownloadClient);
        Assert.True(clientStep.IsComplete);
        Assert.Equal("SABnzbd", clientStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_DisabledDownloadClient_NotComplete()
    {
        _context.Providers.Add(new ProviderDefinition
        {
            Name = "SABnzbd",
            Implementation = "Sabnzbd",
            Category = ProviderCategory.DownloadClient,
            IsEnabled = false
        });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var clientStep = status.Steps.First(s => s.Step == SetupStep.ConfigureDownloadClient);
        Assert.False(clientStep.IsComplete);
    }

    [Fact]
    public async Task GetStatusAsync_IndexerConfigured_StepComplete()
    {
        _context.Providers.Add(new ProviderDefinition
        {
            Name = "NZBGeek",
            Implementation = "Newznab",
            Category = ProviderCategory.Indexer,
            IsEnabled = true
        });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var indexerStep = status.Steps.First(s => s.Step == SetupStep.ConfigureIndexer);
        Assert.True(indexerStep.IsComplete);
        Assert.Equal("NZBGeek", indexerStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_AllRequiredComplete_IsComplete()
    {
        // Configure all required steps
        _mockComicVineClient.Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "/mnt/comics" });
        _context.Series.Add(new Series { Title = "Test Series", Monitored = true });
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        Assert.True(status.IsComplete);
        Assert.False(status.ShouldShowOnboarding);
    }

    [Fact]
    public async Task GetStatusAsync_CalculatesCompletionPercentage()
    {
        // Configure 2 out of 5 steps
        _mockComicVineClient.Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "/mnt/comics" });

        var status = await _service.GetStatusAsync();

        Assert.Equal(40, status.CompletionPercentage); // 2/5 = 40%
    }

    [Fact]
    public async Task GetStatusAsync_Dismissed_ShouldNotShowOnboarding()
    {
        _mockSettingsService.Setup(s => s.GetAsync<bool>("Setup:OnboardingDismissed", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var status = await _service.GetStatusAsync();

        Assert.True(status.IsDismissed);
        Assert.False(status.ShouldShowOnboarding);
    }

    [Fact]
    public async Task GetStatusAsync_ManuallyCompletedStep_MarksComplete()
    {
        _mockSettingsService.Setup(s => s.GetAsync<bool>("Setup:StepCompleted:ConfigureComicVine", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var status = await _service.GetStatusAsync();

        var cvStep = status.Steps.First(s => s.Step == SetupStep.ConfigureComicVine);
        Assert.True(cvStep.IsComplete);
    }

    #endregion

    #region DismissOnboardingAsync Tests

    [Fact]
    public async Task DismissOnboardingAsync_SetsFlag()
    {
        await _service.DismissOnboardingAsync();

        _mockSettingsService.Verify(
            s => s.SetAsync("Setup:OnboardingDismissed", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResetOnboardingAsync Tests

    [Fact]
    public async Task ResetOnboardingAsync_ClearsFlag()
    {
        await _service.ResetOnboardingAsync();

        _mockSettingsService.Verify(
            s => s.SetAsync("Setup:OnboardingDismissed", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CompleteStepAsync Tests

    [Fact]
    public async Task CompleteStepAsync_SetsStepFlag()
    {
        await _service.CompleteStepAsync(SetupStep.ConfigureComicVine);

        _mockSettingsService.Verify(
            s => s.SetAsync("Setup:StepCompleted:ConfigureComicVine", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Step Details Tests

    [Fact]
    public async Task GetStatusAsync_NoSeries_ShowsNoSeriesAdded()
    {
        var status = await _service.GetStatusAsync();

        var seriesStep = status.Steps.First(s => s.Step == SetupStep.AddSeries);
        Assert.Equal("No series added", seriesStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_NoDownloadClients_ShowsNoneConfigured()
    {
        var status = await _service.GetStatusAsync();

        var clientStep = status.Steps.First(s => s.Step == SetupStep.ConfigureDownloadClient);
        Assert.Equal("No download clients configured", clientStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_MultipleDownloadClients_ShowsCount()
    {
        _context.Providers.AddRange(
            new ProviderDefinition { Name = "SABnzbd", Implementation = "Sabnzbd", Category = ProviderCategory.DownloadClient, IsEnabled = true },
            new ProviderDefinition { Name = "NZBGet", Implementation = "Nzbget", Category = ProviderCategory.DownloadClient, IsEnabled = true }
        );
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var clientStep = status.Steps.First(s => s.Step == SetupStep.ConfigureDownloadClient);
        Assert.Equal("2 clients configured", clientStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_NoIndexers_ShowsNoneConfigured()
    {
        var status = await _service.GetStatusAsync();

        var indexerStep = status.Steps.First(s => s.Step == SetupStep.ConfigureIndexer);
        Assert.Equal("No indexers configured", indexerStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_MultipleIndexers_ShowsCount()
    {
        _context.Providers.AddRange(
            new ProviderDefinition { Name = "NZBGeek", Implementation = "Newznab", Category = ProviderCategory.Indexer, IsEnabled = true },
            new ProviderDefinition { Name = "DrunkenSlug", Implementation = "Newznab", Category = ProviderCategory.Indexer, IsEnabled = true }
        );
        await _context.SaveChangesAsync();

        var status = await _service.GetStatusAsync();

        var indexerStep = status.Steps.First(s => s.Step == SetupStep.ConfigureIndexer);
        Assert.Equal("2 indexers configured", indexerStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_ComicVineNotConfigured_ShowsNoApiKey()
    {
        var status = await _service.GetStatusAsync();

        var cvStep = status.Steps.First(s => s.Step == SetupStep.ConfigureComicVine);
        Assert.Equal("No API key set", cvStep.Details);
    }

    [Fact]
    public async Task GetStatusAsync_EmptyRootFolder_ShowsNotConfigured()
    {
        _mockSettingsService.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { ComicLibraryPath = "" });

        var status = await _service.GetStatusAsync();

        var folderStep = status.Steps.First(s => s.Step == SetupStep.ConfigureRootFolder);
        Assert.Equal("No folder configured", folderStep.Details);
    }

    #endregion

    #region SettingsPath Tests

    [Fact]
    public async Task GetStatusAsync_AllStepsHaveSettingsPaths()
    {
        var status = await _service.GetStatusAsync();

        foreach (var step in status.Steps)
        {
            Assert.False(string.IsNullOrEmpty(step.SettingsPath), 
                $"Step {step.Step} should have a SettingsPath");
        }
    }

    #endregion
}
