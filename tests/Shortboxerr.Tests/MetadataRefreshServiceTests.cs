using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;
using MetadataRefreshEventEntity = Shortboxerr.Core.Entities.MetadataRefreshEvent;
using Series = Shortboxerr.Core.Entities.Series;
using SeriesStatus = Shortboxerr.Core.Entities.SeriesStatus;
using EditionTitle = Shortboxerr.Core.Entities.EditionTitle;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for MetadataRefreshService.
/// </summary>
public class MetadataRefreshServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly Mock<IEditionMetadataService> _mockEditionMetadataService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<MetadataRefreshService>> _mockLogger;
    private readonly MetadataRefreshService _service;

    public MetadataRefreshServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _mockEditionMetadataService = new Mock<IEditionMetadataService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<MetadataRefreshService>>();

        // Default settings
        _mockSettingsService
            .Setup(x => x.GetAsync<MetadataRefreshSettings>("metadata_refresh", It.IsAny<MetadataRefreshSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataRefreshSettings
            {
                ScheduledRefreshEnabled = true,
                RefreshInterval = TimeSpan.FromDays(7),
                RefreshCovers = true,
                MaxSeriesPerRun = 50
            });

        _service = new MetadataRefreshService(
            _mockSeriesMetadataService.Object,
            _mockEditionMetadataService.Object,
            _dbContext,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region RefreshSeriesAsync Tests

    [Fact]
    public async Task RefreshSeriesAsync_WithNonExistentSeries_ReturnsError()
    {
        // Act
        var result = await _service.RefreshSeriesAsync(99999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RefreshSeriesAsync_WithUnmatchedSeries_ReturnsError()
    {
        // Arrange
        var series = new Series { Title = "Batman", Status = SeriesStatus.Continuing };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RefreshSeriesAsync(series.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not matched", result.Error);
    }

    [Fact]
    public async Task RefreshSeriesAsync_WithMatchedSeries_RefreshesSuccessfully()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Batman", 
            Status = SeriesStatus.Continuing,
            ComicVineId = 12345,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-8)
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.RefreshSeriesMetadataAsync(series.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesRefreshResult { Success = true, MetadataChanged = true });

        _mockSeriesMetadataService
            .Setup(x => x.SyncIssuesFromComicVineAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSyncResult { Success = true, IssuesAdded = 2, TotalIssues = 50 });

        // Act
        var result = await _service.RefreshSeriesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(series.Title, result.ItemTitle);

        // Verify event was logged
        var events = await _dbContext.MetadataRefreshEvents.ToListAsync();
        Assert.Single(events);
        Assert.Equal("Manual", events[0].Source);
    }

    [Fact]
    public async Task RefreshSeriesAsync_SkipsIfRecentlyRefreshed_UnlessForced()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Batman", 
            Status = SeriesStatus.Continuing,
            ComicVineId = 12345,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-1) // Recently refreshed
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RefreshSeriesAsync(series.Id, force: false);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.MetadataChanged); // Skipped, no change

        // Verify no refresh was called
        _mockSeriesMetadataService.Verify(
            x => x.RefreshSeriesMetadataAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshSeriesAsync_RefreshesWhenForced()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Batman", 
            Status = SeriesStatus.Continuing,
            ComicVineId = 12345,
            ComicVineLastUpdated = DateTime.UtcNow // Just now
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.RefreshSeriesMetadataAsync(series.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesRefreshResult { Success = true, MetadataChanged = true });

        _mockSeriesMetadataService
            .Setup(x => x.SyncIssuesFromComicVineAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSyncResult { Success = true });

        // Act
        var result = await _service.RefreshSeriesAsync(series.Id, force: true);

        // Assert
        Assert.True(result.Success);
        _mockSeriesMetadataService.Verify(
            x => x.RefreshSeriesMetadataAsync(series.Id, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region RefreshAllSeriesAsync Tests

    [Fact]
    public async Task RefreshAllSeriesAsync_RefreshesAllMatchedSeries()
    {
        // Arrange
        var series1 = new Series { Title = "Batman", Status = SeriesStatus.Continuing, ComicVineId = 1, ComicVineLastUpdated = DateTime.UtcNow.AddDays(-10) };
        var series2 = new Series { Title = "Superman", Status = SeriesStatus.Continuing, ComicVineId = 2, ComicVineLastUpdated = DateTime.UtcNow.AddDays(-10) };
        var unmatchedSeries = new Series { Title = "Unknown", Status = SeriesStatus.Continuing };
        _dbContext.Series.AddRange(series1, series2, unmatchedSeries);
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.RefreshSeriesMetadataAsync(It.IsAny<int>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesRefreshResult { Success = true });

        _mockSeriesMetadataService
            .Setup(x => x.SyncIssuesFromComicVineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSyncResult { Success = true });

        // Act
        var result = await _service.RefreshAllSeriesAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed); // Only matched series
        Assert.Equal(2, result.Refreshed);
    }

    #endregion

    #region RefreshStaleSeriesAsync Tests

    [Fact]
    public async Task RefreshStaleSeriesAsync_OnlyRefreshesStale()
    {
        // Arrange
        var staleSeries = new Series 
        { 
            Title = "Stale Series", 
            Status = SeriesStatus.Continuing, 
            ComicVineId = 1, 
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30)
        };
        var freshSeries = new Series 
        { 
            Title = "Fresh Series", 
            Status = SeriesStatus.Continuing, 
            ComicVineId = 2, 
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Series.AddRange(staleSeries, freshSeries);
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.RefreshSeriesMetadataAsync(staleSeries.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesRefreshResult { Success = true });

        _mockSeriesMetadataService
            .Setup(x => x.SyncIssuesFromComicVineAsync(staleSeries.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSyncResult { Success = true });

        // Act
        var result = await _service.RefreshStaleSeriesAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(1, result.Refreshed);
    }

    [Fact]
    public async Task RefreshStaleSeriesAsync_RespectsMaxPerRun()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _dbContext.Series.Add(new Series 
            { 
                Title = $"Series {i}", 
                Status = SeriesStatus.Continuing, 
                ComicVineId = i, 
                ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30)
            });
        }
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.RefreshSeriesMetadataAsync(It.IsAny<int>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesRefreshResult { Success = true });

        _mockSeriesMetadataService
            .Setup(x => x.SyncIssuesFromComicVineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSyncResult { Success = true });

        // Act
        var result = await _service.RefreshStaleSeriesAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(50, result.TotalProcessed); // MaxSeriesPerRun = 50
        Assert.Equal(50, result.Skipped); // Remaining 50 skipped
    }

    #endregion

    #region RefreshEditionAsync Tests

    [Fact]
    public async Task RefreshEditionAsync_WithMatchedEdition_RefreshesSuccessfully()
    {
        // Arrange
        var edition = new EditionTitle 
        { 
            Title = "Batman Vol 1", 
            ComicVineId = 12345
        };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        _mockEditionMetadataService
            .Setup(x => x.RefreshEditionMetadataAsync(edition.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionMatchResult { Success = true, MetadataSynced = true });

        // Act
        var result = await _service.RefreshEditionAsync(edition.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Edition", result.ItemType);
    }

    [Fact]
    public async Task RefreshEditionAsync_WithUnmatchedEdition_ReturnsError()
    {
        // Arrange
        var edition = new EditionTitle { Title = "Unmatched Edition" };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RefreshEditionAsync(edition.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not matched", result.Error);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task GetSeriesRefreshHistoryAsync_ReturnsHistory()
    {
        // Arrange
        var events = new[]
        {
            new MetadataRefreshEventEntity { ItemType = "Series", ItemId = 1, ItemTitle = "Batman", Success = true, Source = "Manual" },
            new MetadataRefreshEventEntity { ItemType = "Series", ItemId = 1, ItemTitle = "Batman", Success = true, Source = "Scheduled" },
            new MetadataRefreshEventEntity { ItemType = "Series", ItemId = 2, ItemTitle = "Superman", Success = true, Source = "Manual" }
        };
        _dbContext.MetadataRefreshEvents.AddRange(events);
        await _dbContext.SaveChangesAsync();

        // Act
        var history = await _service.GetSeriesRefreshHistoryAsync(1);

        // Assert
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal(1, h.ItemId));
    }

    [Fact]
    public async Task GetRecentRefreshEventsAsync_ReturnsRecentEvents()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _dbContext.MetadataRefreshEvents.Add(new MetadataRefreshEventEntity 
            { 
                ItemType = "Series", 
                ItemId = i, 
                ItemTitle = $"Series {i}", 
                Success = true, 
                Source = "Test" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var events = await _service.GetRecentRefreshEventsAsync(25);

        // Assert
        Assert.Equal(25, events.Count);
    }

    #endregion

    #region Settings & Stats Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettings()
    {
        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        Assert.True(settings.ScheduledRefreshEnabled);
        Assert.Equal(TimeSpan.FromDays(7), settings.RefreshInterval);
    }

    [Fact]
    public async Task GetStaleSeriesCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        _dbContext.Series.AddRange(
            new Series { Title = "Stale 1", Status = SeriesStatus.Continuing, ComicVineId = 1, ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30) },
            new Series { Title = "Stale 2", Status = SeriesStatus.Continuing, ComicVineId = 2, ComicVineLastUpdated = null },
            new Series { Title = "Fresh", Status = SeriesStatus.Continuing, ComicVineId = 3, ComicVineLastUpdated = DateTime.UtcNow },
            new Series { Title = "Unmatched", Status = SeriesStatus.Continuing } // No ComicVineId
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var count = await _service.GetStaleSeriesCountAsync();

        // Assert
        Assert.Equal(2, count); // Stale1 (old), Stale2 (never refreshed)
    }

    #endregion
}

