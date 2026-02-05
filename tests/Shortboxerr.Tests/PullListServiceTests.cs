using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Caching;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.PullList;
using Xunit;

namespace Shortboxerr.Tests;

public class PullListServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly PullListService _service;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly ICacheService _cacheService;
    private readonly Mock<ILogger<PullListService>> _mockLogger;

    public PullListServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _cacheService = new CacheService(
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<CacheService>>().Object,
            Options.Create(new CacheSettings()));
        _mockLogger = new Mock<ILogger<PullListService>>();
        _service = new PullListService(
            _dbContext, 
            _mockSettingsService.Object, 
            _mockComicVineClient.Object, 
            _mockSeriesMetadataService.Object, 
            _cacheService, 
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Weekly Releases Tests

    [Fact]
    public async Task GetThisWeekAsync_ReturnsIssuesForCurrentWeek()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Add issue releasing this week (Wednesday)
        var today = DateTime.Today;
        var wednesday = today.AddDays((int)DayOfWeek.Wednesday - (int)today.DayOfWeek);
        
        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Test Issue",
            StoreDate = wednesday,
            Status = IssueStatus.Wanted
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetThisWeekAsync();

        // Assert
        Assert.Single(result.Issues);
        Assert.Equal("Test Series", result.Issues[0].SeriesTitle);
        Assert.Equal(1, result.Issues[0].IssueNumber);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_ReturnsEmptyForNoReleases()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(DateTime.Today.AddYears(1));

        // Assert
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task GetUpcomingReleasesAsync_ReturnsCorrectNumberOfWeeks()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUpcomingReleasesAsync(weeks: 3);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetPastReleasesAsync_ReturnsCorrectNumberOfWeeks()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPastReleasesAsync(weeks: 2);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region Issue Status Tests

    [Fact]
    public async Task MarkAsWantedAsync_UpdatesIssueStatus()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Status = IssueStatus.Missing
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.MarkAsWantedAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IssueStatus.Wanted, result.NewStatus);
        
        var updated = await _dbContext.Issues.FindAsync(issue.Id);
        Assert.Equal(IssueStatus.Wanted, updated!.Status);
        Assert.True(updated.Monitored);
    }

    [Fact]
    public async Task MarkAsOwnedAsync_UpdatesIssueStatus()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Status = IssueStatus.Wanted
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.MarkAsOwnedAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IssueStatus.Owned, result.NewStatus);
    }

    [Fact]
    public async Task MarkAsSkippedAsync_UpdatesIssueStatus()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Status = IssueStatus.Wanted
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.MarkAsSkippedAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IssueStatus.Skipped, result.NewStatus);
    }

    [Fact]
    public async Task MarkAsWantedAsync_NonExistentIssue_ReturnsError()
    {
        // Act
        var result = await _service.MarkAsWantedAsync(9999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_UpdatesMultipleIssues()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issues = new List<Issue>
        {
            new Issue { SeriesId = series.Id, IssueNumber = 1, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 3, Status = IssueStatus.Wanted }
        };
        _dbContext.Issues.AddRange(issues);
        await _dbContext.SaveChangesAsync();

        var issueIds = issues.Select(i => i.Id).ToList();

        // Act
        var result = await _service.BulkUpdateStatusAsync(issueIds, IssueStatus.Owned);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
    }

    #endregion

    #region Monitoring Mode Tests

    [Fact]
    public async Task GetSeriesMonitoringModeAsync_ReturnsCorrectMode()
    {
        // Arrange
        var series = new Series { Title = "Test Series", MonitoringMode = SeriesMonitoringMode.FutureIssues };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var mode = await _service.GetSeriesMonitoringModeAsync(series.Id);

        // Assert
        Assert.Equal(SeriesMonitoringMode.FutureIssues, mode);
    }

    [Fact]
    public async Task SetSeriesMonitoringModeAsync_UpdatesMode()
    {
        // Arrange
        var series = new Series { Title = "Test Series", MonitoringMode = SeriesMonitoringMode.AllIssues };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SetSeriesMonitoringModeAsync(series.Id, SeriesMonitoringMode.Manual);

        // Assert
        Assert.True(result.Success);
        
        var updated = await _dbContext.Series.FindAsync(series.Id);
        Assert.Equal(SeriesMonitoringMode.Manual, updated!.MonitoringMode);
    }

    [Fact]
    public async Task SetSeriesMonitoringModeAsync_NoneMode_SetsMonitoredFalse()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.SetSeriesMonitoringModeAsync(series.Id, SeriesMonitoringMode.None);

        // Assert
        var updated = await _dbContext.Series.FindAsync(series.Id);
        Assert.False(updated!.Monitored);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var series1 = new Series { Title = "Series 1", Monitored = true };
        var series2 = new Series { Title = "Series 2", Monitored = true };
        var series3 = new Series { Title = "Series 3", Monitored = false };
        _dbContext.Series.AddRange(series1, series2, series3);
        await _dbContext.SaveChangesAsync();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series1.Id, IssueNumber = 1, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 2, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 3, Status = IssueStatus.Owned },
            new Issue { SeriesId = series2.Id, IssueNumber = 1, Status = IssueStatus.Skipped }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var stats = await _service.GetStatsAsync();

        // Assert
        Assert.Equal(2, stats.TotalMonitoredSeries);
        Assert.Equal(2, stats.TotalWantedIssues);
        Assert.Equal(1, stats.TotalOwnedIssues);
        Assert.Equal(1, stats.TotalSkippedIssues);
    }

    #endregion

    #region Filter Tests

    [Fact]
    public async Task GetWeeklyReleasesAsync_WithFilter_FiltersCorrectly()
    {
        // Arrange
        var series1 = new Series { Title = "Marvel Series", Publisher = "Marvel", Monitored = true };
        var series2 = new Series { Title = "DC Series", Publisher = "DC Comics", Monitored = true };
        _dbContext.Series.AddRange(series1, series2);
        await _dbContext.SaveChangesAsync();

        var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);
        
        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series1.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series2.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { Publishers = new List<string> { "Marvel" } };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.Equal("Marvel Series", result.Issues[0].SeriesTitle);
    }

    #endregion

    #region Calendar Tests

    [Fact]
    public async Task GetCalendarAsync_ReturnsCorrectDayStructure()
    {
        // Arrange
        var series = new Series { Title = "Test Series", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var startDate = DateTime.Today;
        var endDate = DateTime.Today.AddDays(7);

        _dbContext.Issues.Add(new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            StoreDate = startDate.AddDays(2),
            Status = IssueStatus.Wanted
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var calendar = await _service.GetCalendarAsync(startDate, endDate);

        // Assert
        Assert.Equal(7, calendar.Days.Count);
        Assert.Single(calendar.Days.Where(d => d.Issues.Any()));
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultSettings_WhenNoneStored()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<PullListSettings>("pulllist", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PullListSettings?)null);

        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartDay);
        Assert.Equal(DayOfWeek.Wednesday, settings.ReleaseDay);
        Assert.Equal(SeriesMonitoringMode.FutureIssues, settings.DefaultMonitoringMode);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsStoredSettings()
    {
        // Arrange
        var storedSettings = new PullListSettings
        {
            WeekStartDay = DayOfWeek.Monday,
            SearchDelayHours = 12,
            AutoAddToWanted = false
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<PullListSettings>("pulllist", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedSettings);

        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        Assert.Equal(DayOfWeek.Monday, settings.WeekStartDay);
        Assert.Equal(12, settings.SearchDelayHours);
        Assert.False(settings.AutoAddToWanted);
    }

    [Fact]
    public async Task UpdateSettingsAsync_SavesSettings()
    {
        // Arrange
        var newSettings = new PullListSettings
        {
            WeekStartDay = DayOfWeek.Monday,
            SearchDelayHours = 8
        };

        _mockSettingsService
            .Setup(s => s.SetAsync("pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSettingsAsync(newSettings);

        // Assert
        Assert.True(result.Success);
        _mockSettingsService.Verify(
            s => s.SetAsync("pulllist", It.Is<PullListSettings>(p => 
                p.WeekStartDay == DayOfWeek.Monday && p.SearchDelayHours == 8), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetSeriesSettingsAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<Dictionary<int, SeriesPullListSettings>>(
                "pulllist_series", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<int, SeriesPullListSettings>?)null);

        // Act
        var settings = await _service.GetSeriesSettingsAsync(999);

        // Assert
        Assert.Null(settings);
    }

    [Fact]
    public async Task GetSeriesSettingsAsync_ReturnsSettings_WhenFound()
    {
        // Arrange
        var storedSettings = new Dictionary<int, SeriesPullListSettings>
        {
            [1] = new SeriesPullListSettings 
            { 
                SeriesId = 1, 
                SearchPriority = 5,
                IncludeAnnuals = false 
            }
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<Dictionary<int, SeriesPullListSettings>>(
                "pulllist_series", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedSettings);

        // Act
        var settings = await _service.GetSeriesSettingsAsync(1);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(1, settings.SeriesId);
        Assert.Equal(5, settings.SearchPriority);
        Assert.False(settings.IncludeAnnuals);
    }

    [Fact]
    public async Task UpdateSeriesSettingsAsync_SavesSeriesSettings()
    {
        // Arrange
        var existingSettings = new Dictionary<int, SeriesPullListSettings>();
        var newSettings = new SeriesPullListSettings
        {
            SeriesId = 5,
            SearchPriority = 10
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<Dictionary<int, SeriesPullListSettings>>(
                "pulllist_series", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSettings);

        _mockSettingsService
            .Setup(s => s.SetAsync("pulllist_series", 
                It.IsAny<Dictionary<int, SeriesPullListSettings>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSeriesSettingsAsync(newSettings);

        // Assert
        Assert.True(result.Success);
        _mockSettingsService.Verify(
            s => s.SetAsync("pulllist_series", 
                It.Is<Dictionary<int, SeriesPullListSettings>>(d => 
                    d.ContainsKey(5) && d[5].SearchPriority == 10), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region Weekly Export Tests

    [Fact]
    public async Task ExportCurrentWeekAsync_WhenExportDisabled_ReturnsError()
    {
        // Arrange
        var settings = new PullListSettings
        {
            ExportWeeklyPullList = false
        };
        _mockSettingsService
            .Setup(s => s.GetAsync<PullListSettings>(
                "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.ExportCurrentWeekAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Weekly export is not enabled in settings.", result.Error);
    }

    [Fact]
    public async Task ExportCurrentWeekAsync_WhenDirectoryNotConfigured_ReturnsError()
    {
        // Arrange
        var settings = new PullListSettings
        {
            ExportWeeklyPullList = true,
            WeeklyExportDirectory = null
        };
        _mockSettingsService
            .Setup(s => s.GetAsync<PullListSettings>(
                "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.ExportCurrentWeekAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Weekly export directory is not configured.", result.Error);
    }

    [Fact]
    public async Task ExportWeekAsync_WithValidSettings_CreatesExportFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"pulllist_export_test_{Guid.NewGuid()}");
        try
        {
            var settings = new PullListSettings
            {
                ExportWeeklyPullList = true,
                WeeklyExportDirectory = tempDir,
                WeeklyExportFormat = WeeklyExportFormat.Json
            };
            _mockSettingsService
                .Setup(s => s.GetAsync<PullListSettings>(
                    "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Add test data
            var series = new Series { Title = "Test Series", Publisher = "Marvel", Monitored = true };
            _dbContext.Series.Add(series);
            await _dbContext.SaveChangesAsync();

            var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);
            _dbContext.Issues.Add(new Issue 
            { 
                SeriesId = series.Id, 
                IssueNumber = 1, 
                StoreDate = wednesday, 
                Status = IssueStatus.Wanted 
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.ExportWeekAsync(wednesday);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ExportFilePath);
            Assert.True(File.Exists(result.ExportFilePath));
            Assert.Equal(1, result.TotalIssues);
            Assert.Equal(1, result.WantedIssues);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExportWeekAsync_JsonFormat_GeneratesValidJson()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"pulllist_export_test_{Guid.NewGuid()}");
        try
        {
            var settings = new PullListSettings
            {
                ExportWeeklyPullList = true,
                WeeklyExportDirectory = tempDir,
                WeeklyExportFormat = WeeklyExportFormat.Json
            };
            _mockSettingsService
                .Setup(s => s.GetAsync<PullListSettings>(
                    "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Add test data
            var series = new Series { Title = "Batman", Publisher = "DC Comics", Monitored = true };
            _dbContext.Series.Add(series);
            await _dbContext.SaveChangesAsync();

            var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);
            _dbContext.Issues.Add(new Issue 
            { 
                SeriesId = series.Id, 
                IssueNumber = 100, 
                StoreDate = wednesday, 
                Status = IssueStatus.Owned 
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.ExportWeekAsync(wednesday);

            // Assert
            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(result.ExportFilePath!);
            Assert.Contains("Batman", content);
            Assert.Contains("DC Comics", content);
            Assert.Contains("Owned", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExportWeekAsync_CsvFormat_GeneratesValidCsv()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"pulllist_export_test_{Guid.NewGuid()}");
        try
        {
            var settings = new PullListSettings
            {
                ExportWeeklyPullList = true,
                WeeklyExportDirectory = tempDir,
                WeeklyExportFormat = WeeklyExportFormat.Csv
            };
            _mockSettingsService
                .Setup(s => s.GetAsync<PullListSettings>(
                    "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Add test data
            var series = new Series { Title = "Spider-Man", Publisher = "Marvel", Monitored = true };
            _dbContext.Series.Add(series);
            await _dbContext.SaveChangesAsync();

            var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);
            _dbContext.Issues.Add(new Issue 
            { 
                SeriesId = series.Id, 
                IssueNumber = 50, 
                StoreDate = wednesday, 
                Status = IssueStatus.Wanted 
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.ExportWeekAsync(wednesday);

            // Assert
            Assert.True(result.Success);
            Assert.EndsWith(".csv", result.ExportFilePath);
            var content = await File.ReadAllTextAsync(result.ExportFilePath!);
            Assert.Contains("SeriesTitle,IssueNumber", content); // Header
            Assert.Contains("Spider-Man", content);
            Assert.Contains("Marvel", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExportWeekAsync_TextFormat_GeneratesHumanReadableText()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"pulllist_export_test_{Guid.NewGuid()}");
        try
        {
            var settings = new PullListSettings
            {
                ExportWeeklyPullList = true,
                WeeklyExportDirectory = tempDir,
                WeeklyExportFormat = WeeklyExportFormat.Text
            };
            _mockSettingsService
                .Setup(s => s.GetAsync<PullListSettings>(
                    "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Add test data
            var series = new Series { Title = "X-Men", Publisher = "Marvel", Monitored = true };
            _dbContext.Series.Add(series);
            await _dbContext.SaveChangesAsync();

            var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);
            _dbContext.Issues.Add(new Issue 
            { 
                SeriesId = series.Id, 
                IssueNumber = 25, 
                StoreDate = wednesday, 
                Status = IssueStatus.Wanted 
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _service.ExportWeekAsync(wednesday);

            // Assert
            Assert.True(result.Success);
            Assert.EndsWith(".txt", result.ExportFilePath);
            var content = await File.ReadAllTextAsync(result.ExportFilePath!);
            Assert.Contains("Weekly Pull List", content);
            Assert.Contains("[Marvel]", content);
            Assert.Contains("X-Men", content);
            Assert.Contains("[Wanted]", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetExportHistoryAsync_WhenDirectoryNotConfigured_ReturnsEmptyList()
    {
        // Arrange
        var settings = new PullListSettings
        {
            WeeklyExportDirectory = null
        };
        _mockSettingsService
            .Setup(s => s.GetAsync<PullListSettings>(
                "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetExportHistoryAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExportWeekAsync_CreatesCorrectDirectoryStructure()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"pulllist_export_test_{Guid.NewGuid()}");
        try
        {
            var settings = new PullListSettings
            {
                ExportWeeklyPullList = true,
                WeeklyExportDirectory = tempDir,
                WeeklyExportFormat = WeeklyExportFormat.Json
            };
            _mockSettingsService
                .Setup(s => s.GetAsync<PullListSettings>(
                    "pulllist", It.IsAny<PullListSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Add test data
            var series = new Series { Title = "Test", Monitored = true };
            _dbContext.Series.Add(series);
            await _dbContext.SaveChangesAsync();

            var wednesday = DateTime.Today.AddDays((int)DayOfWeek.Wednesday - (int)DateTime.Today.DayOfWeek);

            // Act
            var result = await _service.ExportWeekAsync(wednesday);

            // Assert
            Assert.True(result.Success);
            // Verify directory format is YYYY-WW
            var dirName = Path.GetFileName(result.ExportDirectory);
            Assert.Matches(@"^\d{4}-\d{2}$", dirName);
            Assert.Equal(result.Year.ToString(), dirName!.Split('-')[0]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Caching Integration Tests

    [Fact]
    public async Task GetStatsAsync_SecondCallUsesCache()
    {
        // Arrange - Add some test data
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Test Issue",
            Status = IssueStatus.Wanted
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act - First call should hit DB
        var stats1 = await _service.GetStatsAsync();
        
        // Modify DB directly (bypassing service)
        _dbContext.Issues.Remove(issue);
        await _dbContext.SaveChangesAsync();
        
        // Second call should use cache
        var stats2 = await _service.GetStatsAsync();

        // Assert - Both calls should return same result (cached)
        Assert.Equal(stats1.TotalWantedIssues, stats2.TotalWantedIssues);
        Assert.Equal(1, stats2.TotalWantedIssues); // Still shows 1 from cache
    }

    [Fact]
    public async Task MarkAsOwnedAsync_InvalidatesStatsCache()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Test Issue",
            Status = IssueStatus.Wanted
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act - Get stats first (populates cache)
        var statsBefore = await _service.GetStatsAsync();
        
        // Change status (should invalidate cache)
        await _service.MarkAsOwnedAsync(issue.Id);
        
        // Get stats again (should reflect change)
        var statsAfter = await _service.GetStatsAsync();

        // Assert
        Assert.Equal(1, statsBefore.TotalWantedIssues);
        Assert.Equal(0, statsAfter.TotalWantedIssues);
        Assert.Equal(1, statsAfter.TotalOwnedIssues);
    }

    [Fact]
    public async Task BulkUpdateStatusAsync_InvalidatesStatsCache()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issues = new[]
        {
            new Issue { SeriesId = series.Id, IssueNumber = 1, Title = "Issue 1", Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, Title = "Issue 2", Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 3, Title = "Issue 3", Status = IssueStatus.Wanted }
        };
        _dbContext.Issues.AddRange(issues);
        await _dbContext.SaveChangesAsync();

        // Act - Get stats first (populates cache)
        var statsBefore = await _service.GetStatsAsync();
        
        // Bulk update (should invalidate cache)
        await _service.BulkUpdateStatusAsync(issues.Select(i => i.Id), IssueStatus.Owned);
        
        // Get stats again (should reflect change)
        var statsAfter = await _service.GetStatsAsync();

        // Assert
        Assert.Equal(3, statsBefore.TotalWantedIssues);
        Assert.Equal(0, statsAfter.TotalWantedIssues);
        Assert.Equal(3, statsAfter.TotalOwnedIssues);
    }

    [Fact]
    public async Task GetWeeklyDiscoveryAsync_UsesCache()
    {
        // Arrange
        var weekOf = new DateTime(2026, 2, 4);
        var mockResult = new ComicVineSearchResult<ComicVineIssue>
        {
            Success = true,
            Results = new List<ComicVineIssue>
            {
                new ComicVineIssue
                {
                    Id = 1001,
                    Name = "Test Issue",
                    IssueNumber = "1",
                    StoreDate = new DateTime(2026, 2, 4),
                    Volume = new ComicVineVolumeRef { Id = 1, Name = "Test Volume" }
                }
            },
            TotalResults = 1
        };
        
        _mockComicVineClient
            .Setup(c => c.GetIssuesByStoreDateAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);
        
        _mockComicVineClient
            .Setup(c => c.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - First call should hit API
        var result1 = await _service.GetWeeklyDiscoveryAsync(weekOf);
        
        // Second call should use cache (API won't be called again)
        var result2 = await _service.GetWeeklyDiscoveryAsync(weekOf);

        // Assert
        Assert.Equal(result1.Issues.Count, result2.Issues.Count);
        
        // API should only be called once due to caching
        _mockComicVineClient.Verify(
            c => c.GetIssuesByStoreDateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    #endregion
}
