using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;
using Series = Shortboxerr.Core.Entities.Series;
using SeriesStatus = Shortboxerr.Core.Entities.SeriesStatus;

namespace Shortboxerr.Tests;

/// <summary>
/// Integration tests for ComicVine full flow scenarios.
/// Tests the complete path: search → match → sync metadata → refresh cycle.
/// </summary>
public class ComicVineIntegrationTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<SeriesMetadataService>> _mockSeriesLogger;
    private readonly Mock<ILogger<IssueMetadataService>> _mockIssueLogger;
    private readonly Mock<ILogger<MetadataRefreshService>> _mockRefreshLogger;
    private readonly IMemoryCache _memoryCache;

    private readonly SeriesMetadataService _seriesMetadataService;
    private readonly IssueMetadataService _issueMetadataService;
    private readonly MetadataRefreshService _metadataRefreshService;

    public ComicVineIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSeriesLogger = new Mock<ILogger<SeriesMetadataService>>();
        _mockIssueLogger = new Mock<ILogger<IssueMetadataService>>();
        _mockRefreshLogger = new Mock<ILogger<MetadataRefreshService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Default settings
        SetupDefaultSettings();

        // Create services
        _seriesMetadataService = new SeriesMetadataService(
            _mockComicVineClient.Object,
            _dbContext,
            _mockSettingsService.Object,
            _mockSeriesLogger.Object);

        _issueMetadataService = new IssueMetadataService(
            _dbContext,
            _mockComicVineClient.Object,
            _mockSettingsService.Object,
            _mockIssueLogger.Object);

        // Create edition metadata service mock for refresh service
        var mockEditionMetadataService = new Mock<IEditionMetadataService>();

        _metadataRefreshService = new MetadataRefreshService(
            _seriesMetadataService,
            mockEditionMetadataService.Object,
            _dbContext,
            _mockSettingsService.Object,
            _mockRefreshLogger.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetupDefaultSettings()
    {
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings
            {
                ApiKey = "test-api-key",
                Enabled = true,
                AutoMatchThreshold = 85
            });

        _mockSettingsService
            .Setup(x => x.GetAsync<MetadataRefreshSettings>("metadata_refresh", It.IsAny<MetadataRefreshSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataRefreshSettings
            {
                ScheduledRefreshEnabled = true,
                RefreshInterval = TimeSpan.FromDays(7),
                MaxSeriesPerRun = 50
            });
    }

    #region Full Flow Integration Tests

    [Fact]
    public async Task FullFlow_SearchMatchSyncMetadata_CompletesSuccessfully()
    {
        // Arrange - Setup mock ComicVine responses
        var searchResults = new ComicVineSearchResult<ComicVineVolume>
        {
            Success = true,
            Results = new List<ComicVineVolume>
            {
                new ComicVineVolume
                {
                    Id = 12345,
                    Name = "Batman",
                    StartYear = 2016,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" },
                    Image = new ComicVineImage { SuperUrl = "https://example.com/batman.jpg" },
                    IssueCount = 50,
                    Description = "The Dark Knight returns!"
                }
            },
            TotalResults = 1
        };

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync("Batman", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var volumeResult = new ComicVineResult<ComicVineVolume>
        {
            Success = true,
            Data = searchResults.Results.First()
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(volumeResult);

        var issuesResult = new ComicVineSearchResult<ComicVineIssue>
        {
            Success = true,
            Results = new List<ComicVineIssue>
            {
                new ComicVineIssue { Id = 1001, IssueNumber = "1", Name = "I Am Gotham Part 1", CoverDate = new DateTime(2016, 8, 1) },
                new ComicVineIssue { Id = 1002, IssueNumber = "2", Name = "I Am Gotham Part 2", CoverDate = new DateTime(2016, 9, 1) },
                new ComicVineIssue { Id = 1003, IssueNumber = "3", Name = "I Am Gotham Part 3", CoverDate = new DateTime(2016, 10, 1) }
            },
            TotalResults = 3
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(12345, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuesResult);

        // Act - Step 1: Search
        var searchResult = await _seriesMetadataService.SearchSeriesAsync("Batman");

        // Assert Step 1
        Assert.True(searchResult.Success);
        Assert.Single(searchResult.Results);
        Assert.Equal("Batman", searchResult.Results.First().Title);

        // Act - Step 2: Add series from ComicVine
        var addResult = await _seriesMetadataService.AddSeriesByComicVineIdAsync(
            12345, monitored: true, monitoringMode: SeriesMonitoringMode.AllIssues);

        // Assert Step 2
        Assert.True(addResult.Success);
        Assert.NotNull(addResult.SeriesId);

        // Verify series was created
        var series = await _dbContext.Series.FindAsync(addResult.SeriesId);
        Assert.NotNull(series);
        Assert.Equal("Batman", series.Title);
        Assert.Equal(12345, series.ComicVineId);
        Assert.Equal("DC Comics", series.Publisher);

        // Act - Step 3: Sync issues
        var syncResult = await _seriesMetadataService.SyncIssuesFromComicVineAsync(addResult.SeriesId.Value);

        // Assert Step 3
        Assert.True(syncResult.Success);
        Assert.Equal(3, syncResult.TotalIssues);

        // Verify issues were created
        var issues = await _dbContext.Issues.Where(i => i.SeriesId == addResult.SeriesId).ToListAsync();
        Assert.Equal(3, issues.Count);
        Assert.Contains(issues, i => i.IssueNumber == 1);
        Assert.Contains(issues, i => i.IssueNumber == 2);
        Assert.Contains(issues, i => i.IssueNumber == 3);
    }

    [Fact]
    public async Task FullFlow_AutoMatchExistingSeries_MatchesAndSyncs()
    {
        // Arrange - Create local series first
        var localSeries = new Series
        {
            Title = "Spider-Man",
            Status = SeriesStatus.Continuing,
            StartYear = 2018
        };
        _dbContext.Series.Add(localSeries);
        await _dbContext.SaveChangesAsync();

        // Setup mock ComicVine responses
        var searchResults = new ComicVineSearchResult<ComicVineVolume>
        {
            Success = true,
            Results = new List<ComicVineVolume>
            {
                new ComicVineVolume
                {
                    Id = 67890,
                    Name = "Spider-Man",
                    StartYear = 2018,
                    Publisher = new ComicVinePublisherRef { Id = 31, Name = "Marvel" },
                    Image = new ComicVineImage { SuperUrl = "https://example.com/spiderman.jpg" },
                    IssueCount = 30,
                    Description = "Spider-Man's latest adventures"
                }
            },
            TotalResults = 1
        };

        // Use It.IsAny<int>() for limit since AutoMatchSeriesAsync uses limit=5
        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync("Spider-Man", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var volumeResult = new ComicVineResult<ComicVineVolume>
        {
            Success = true,
            Data = searchResults.Results.First()
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(67890, It.IsAny<CancellationToken>()))
            .ReturnsAsync(volumeResult);

        var issuesResult = new ComicVineSearchResult<ComicVineIssue>
        {
            Success = true,
            Results = new List<ComicVineIssue>
            {
                new ComicVineIssue { Id = 2001, IssueNumber = "1", Name = "Back to Basics", CoverDate = new DateTime(2018, 10, 1) }
            },
            TotalResults = 1
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(67890, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuesResult);

        // Act - Auto-match the series
        var autoMatchResult = await _seriesMetadataService.AutoMatchSeriesAsync(localSeries.Id);

        // Assert
        Assert.True(autoMatchResult.Success);
        Assert.Equal(67890, autoMatchResult.MatchedComicVineId);

        // Verify series was updated
        var updatedSeries = await _dbContext.Series.FindAsync(localSeries.Id);
        Assert.NotNull(updatedSeries);
        Assert.Equal(67890, updatedSeries.ComicVineId);
        Assert.Equal("Marvel", updatedSeries.Publisher);
    }

    #endregion

    #region Refresh Cycle Integration Tests

    [Fact]
    public async Task RefreshCycle_RefreshesStaleSeriesMetadata()
    {
        // Arrange - Create a series that needs refresh
        var staleSeries = new Series
        {
            Title = "X-Men",
            Status = SeriesStatus.Continuing,
            ComicVineId = 11111,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30) // Stale
        };
        _dbContext.Series.Add(staleSeries);
        await _dbContext.SaveChangesAsync();

        // Setup mock ComicVine responses
        var volumeResult = new ComicVineResult<ComicVineVolume>
        {
            Success = true,
            Data = new ComicVineVolume
            {
                Id = 11111,
                Name = "X-Men",
                StartYear = 2019,
                Publisher = new ComicVinePublisherRef { Id = 31, Name = "Marvel" },
                Description = "Updated description",
                IssueCount = 25
            }
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(11111, It.IsAny<CancellationToken>()))
            .ReturnsAsync(volumeResult);

        var issuesResult = new ComicVineSearchResult<ComicVineIssue>
        {
            Success = true,
            Results = new List<ComicVineIssue>
            {
                new ComicVineIssue { Id = 3001, IssueNumber = "1", Name = "Dawn of X" }
            },
            TotalResults = 1
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(11111, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuesResult);

        // Act - Refresh stale series
        var refreshResult = await _metadataRefreshService.RefreshStaleSeriesAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(refreshResult.Success);
        Assert.Equal(1, refreshResult.TotalProcessed);
        Assert.Equal(1, refreshResult.Refreshed);

        // Verify refresh event was logged
        var events = await _dbContext.MetadataRefreshEvents.ToListAsync();
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.ItemId == staleSeries.Id && e.Source == "Scheduled");
    }

    [Fact]
    public async Task RefreshCycle_SkipsFreshSeries()
    {
        // Arrange - Create a fresh series
        var freshSeries = new Series
        {
            Title = "Avengers",
            Status = SeriesStatus.Continuing,
            ComicVineId = 22222,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-1) // Fresh
        };
        _dbContext.Series.Add(freshSeries);
        await _dbContext.SaveChangesAsync();

        // Act - Attempt refresh with 7-day threshold
        var refreshResult = await _metadataRefreshService.RefreshStaleSeriesAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(refreshResult.Success);
        Assert.Equal(0, refreshResult.TotalProcessed); // No stale series
        Assert.Equal(0, refreshResult.Refreshed);

        // ComicVine should not have been called
        _mockComicVineClient.Verify(
            x => x.GetVolumeAsync(22222, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshCycle_DiscoversNewIssues()
    {
        // Arrange - Create series with existing issues
        var series = new Series
        {
            Title = "Justice League",
            Status = SeriesStatus.Continuing,
            ComicVineId = 33333,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-10)
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Add existing issue
        _dbContext.Issues.Add(new Core.Entities.Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Issue 1",
            ComicVineId = 4001
        });
        await _dbContext.SaveChangesAsync();

        // Setup mock - return more issues than we have locally
        var volumeResult = new ComicVineResult<ComicVineVolume>
        {
            Success = true,
            Data = new ComicVineVolume
            {
                Id = 33333,
                Name = "Justice League",
                IssueCount = 3
            }
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(33333, It.IsAny<CancellationToken>()))
            .ReturnsAsync(volumeResult);

        var issuesResult = new ComicVineSearchResult<ComicVineIssue>
        {
            Success = true,
            Results = new List<ComicVineIssue>
            {
                new ComicVineIssue { Id = 4001, IssueNumber = "1", Name = "Issue 1" },
                new ComicVineIssue { Id = 4002, IssueNumber = "2", Name = "Issue 2 (NEW)" },
                new ComicVineIssue { Id = 4003, IssueNumber = "3", Name = "Issue 3 (NEW)" }
            },
            TotalResults = 3
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(33333, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuesResult);

        // Act - Refresh series issues
        var refreshResult = await _metadataRefreshService.RefreshSeriesIssuesAsync(series.Id);

        // Assert
        Assert.True(refreshResult.Success);
        Assert.Equal(3, refreshResult.TotalIssues);
        Assert.Equal(2, refreshResult.NewIssuesDiscovered);

        // Verify new issues were added
        var allIssues = await _dbContext.Issues.Where(i => i.SeriesId == series.Id).ToListAsync();
        Assert.Equal(3, allIssues.Count);
    }

    #endregion

    #region Error Handling Integration Tests

    [Fact]
    public async Task FullFlow_HandlesComicVineApiFailure_Gracefully()
    {
        // Arrange - Setup API failure
        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = false,
                Error = "API rate limit exceeded"
            });

        // Act
        var searchResult = await _seriesMetadataService.SearchSeriesAsync("Batman");

        // Assert
        Assert.False(searchResult.Success);
        Assert.Contains("rate limit", searchResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshCycle_HandlesPartialFailure_ContinuesProcessing()
    {
        // Arrange - Create two stale series
        var series1 = new Series
        {
            Title = "Series A",
            Status = SeriesStatus.Continuing,
            ComicVineId = 44444,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30)
        };
        var series2 = new Series
        {
            Title = "Series B",
            Status = SeriesStatus.Continuing,
            ComicVineId = 55555,
            ComicVineLastUpdated = DateTime.UtcNow.AddDays(-30)
        };
        _dbContext.Series.AddRange(series1, series2);
        await _dbContext.SaveChangesAsync();

        // Setup - First series fails, second succeeds
        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(44444, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = false,
                Error = "Volume not found"
            });

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(55555, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume { Id = 55555, Name = "Series B" }
            });

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(55555, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>(),
                TotalResults = 0
            });

        // Act
        var result = await _metadataRefreshService.RefreshStaleSeriesAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.True(result.Success); // Overall operation succeeded
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.Errors); // One failed
        Assert.Equal(1, result.Refreshed); // One succeeded
    }

    #endregion

    #region Cover Integration Tests

    [Fact]
    public async Task CoverFlow_SeriesWithCoverUrl_CanBeRetrieved()
    {
        // Arrange - Create series with cover URL
        var series = new Series
        {
            Title = "Wonder Woman",
            Status = SeriesStatus.Continuing,
            ComicVineId = 66666,
            CoverImageUrl = "https://comicvine.gamespot.com/api/image/original/wonder-woman.jpg"
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Assert - Cover URL is stored and retrievable
        var storedSeries = await _dbContext.Series.FindAsync(series.Id);
        Assert.NotNull(storedSeries);
        Assert.NotNull(storedSeries.CoverImageUrl);
        Assert.Contains("wonder-woman", storedSeries.CoverImageUrl);
    }

    [Fact]
    public async Task CoverFlow_IssueWithCoverUrl_CanBeRetrieved()
    {
        // Arrange
        var series = new Series
        {
            Title = "Flash",
            Status = SeriesStatus.Continuing,
            ComicVineId = 77777
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Core.Entities.Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Lightning Strikes",
            ComicVineId = 8001,
            CoverImageUrl = "https://comicvine.gamespot.com/api/image/original/flash-1.jpg"
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Assert
        var storedIssue = await _dbContext.Issues.FindAsync(issue.Id);
        Assert.NotNull(storedIssue);
        Assert.NotNull(storedIssue.CoverImageUrl);
        Assert.Contains("flash-1", storedIssue.CoverImageUrl);
    }

    [Fact]
    public async Task CoverFlow_AddSeriesFromComicVine_StoresCoverUrl()
    {
        // Arrange
        var volumeResult = new ComicVineResult<ComicVineVolume>
        {
            Success = true,
            Data = new ComicVineVolume
            {
                Id = 88888,
                Name = "Green Lantern",
                StartYear = 2021,
                Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" },
                Image = new ComicVineImage 
                { 
                    // Service uses MediumUrl or SmallUrl for CoverImageUrl
                    MediumUrl = "https://comicvine.gamespot.com/api/image/scale_medium/green-lantern.jpg",
                    SmallUrl = "https://comicvine.gamespot.com/api/image/scale_small/green-lantern.jpg",
                    SuperUrl = "https://comicvine.gamespot.com/api/image/scale_large/green-lantern.jpg",
                    OriginalUrl = "https://comicvine.gamespot.com/api/image/original/green-lantern.jpg"
                },
                IssueCount = 10
            }
        };

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(88888, It.IsAny<CancellationToken>()))
            .ReturnsAsync(volumeResult);

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(88888, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>(),
                TotalResults = 0
            });

        // Act
        var result = await _seriesMetadataService.AddSeriesByComicVineIdAsync(88888, monitored: true);

        // Assert
        Assert.True(result.Success);
        var series = await _dbContext.Series.FindAsync(result.SeriesId);
        Assert.NotNull(series);
        Assert.NotNull(series.CoverImageUrl);
        Assert.Contains("green-lantern", series.CoverImageUrl);
    }

    #endregion
}
