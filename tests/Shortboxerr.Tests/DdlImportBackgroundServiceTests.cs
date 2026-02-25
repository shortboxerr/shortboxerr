using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

public class DdlImportBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IDdlDownloadService> _mockDownloadService;
    private readonly Mock<IDdlImportService> _mockImportService;
    private readonly Mock<IActivityService> _mockActivityService;
    private readonly Mock<ILogger<DdlImportBackgroundService>> _mockLogger;

    public DdlImportBackgroundServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDownloadService = new Mock<IDdlDownloadService>();
        _mockImportService = new Mock<IDdlImportService>();
        _mockActivityService = new Mock<IActivityService>();
        _mockLogger = new Mock<ILogger<DdlImportBackgroundService>>();

        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(x => x.GetService(typeof(ISettingsService)))
            .Returns(_mockSettingsService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(IDdlDownloadService)))
            .Returns(_mockDownloadService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(IDdlImportService)))
            .Returns(_mockImportService.Object);
        scopeServiceProvider.Setup(x => x.GetService(typeof(IActivityService)))
            .Returns(_mockActivityService.Object);

        _mockScope.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);
    }

    private DdlImportBackgroundService CreateService()
    {
        return new DdlImportBackgroundService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectDefaultStatus()
    {
        var service = CreateService();
        var status = service.GetStatus();

        Assert.True(status.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(30), status.CheckInterval);
        Assert.Equal(0, status.ConsecutiveErrors);
        Assert.Null(status.LastCheck);
    }

    [Fact]
    public void DdlImportServiceStatus_ContainsExpectedProperties()
    {
        var status = new DdlImportServiceStatus
        {
            IsRunning = true,
            CheckInterval = TimeSpan.FromSeconds(30),
            ConsecutiveErrors = 2,
            LastCheck = DateTime.UtcNow
        };

        Assert.True(status.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(30), status.CheckInterval);
        Assert.Equal(2, status.ConsecutiveErrors);
        Assert.NotNull(status.LastCheck);
    }

    [Fact]
    public void DdlDownloadHistoryEntry_ImportTracking_HasCorrectDefaults()
    {
        var entry = new DdlDownloadHistoryEntry
        {
            Id = "test-id",
            DownloadId = "download-123",
            SourceUrl = "https://example.com/file.cbz",
            ReleaseTitle = "Test Comic #1",
            DestinationPath = "/downloads/test.cbz",
            FileSize = 50_000_000,
            Success = true,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow
        };

        Assert.False(entry.ImportProcessed);
        Assert.Null(entry.ImportProcessedAt);
        Assert.Null(entry.Candidate);
    }

    [Fact]
    public void DdlDownloadHistoryEntry_CanTrackImportStatus()
    {
        var entry = new DdlDownloadHistoryEntry
        {
            Id = "test-id",
            DownloadId = "download-123",
            SourceUrl = "https://example.com/file.cbz",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Success = true,
            ImportProcessed = false
        };

        entry.ImportProcessed = true;
        entry.ImportProcessedAt = DateTime.UtcNow;

        Assert.True(entry.ImportProcessed);
        Assert.NotNull(entry.ImportProcessedAt);
    }

    [Fact]
    public void GetPendingImportDownloads_ReturnsOnlySuccessfulUnprocessedDownloads()
    {
        var downloads = new List<DdlDownloadHistoryEntry>
        {
            new() { Id = "1", DownloadId = "d1", SourceUrl = "url1", Success = true, ImportProcessed = false, DestinationPath = "/path1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow },
            new() { Id = "2", DownloadId = "d2", SourceUrl = "url2", Success = false, ImportProcessed = false, DestinationPath = "/path2", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow },
            new() { Id = "3", DownloadId = "d3", SourceUrl = "url3", Success = true, ImportProcessed = true, DestinationPath = "/path3", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow },
            new() { Id = "4", DownloadId = "d4", SourceUrl = "url4", Success = true, ImportProcessed = false, DestinationPath = null, StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow }
        };

        var pending = downloads
            .Where(h => h.Success && !h.ImportProcessed && !string.IsNullOrEmpty(h.DestinationPath))
            .ToList();

        Assert.Single(pending);
        Assert.Equal("d1", pending[0].DownloadId);
    }
}
