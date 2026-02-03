using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

public class SeriesMetadataServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly SeriesMetadataService _service;

    public SeriesMetadataServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSettingsService = new Mock<ISettingsService>();

        // Default settings
        _mockSettingsService.Setup(x => x.GetAsync<ComicVineSettings>(
            It.IsAny<string>(), It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { AutoMatchThreshold = 85 });

        _service = new SeriesMetadataService(
            _mockComicVineClient.Object,
            _dbContext,
            _mockSettingsService.Object,
            NullLogger<SeriesMetadataService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task SearchSeriesAsync_WithConfiguredClient_ReturnsResults()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.SearchVolumesAsync(
            "Batman", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                TotalResults = 1,
                Results = new List<ComicVineVolume>
                {
                    new ComicVineVolume
                    {
                        Id = 796,
                        Name = "Batman",
                        StartYear = 1940,
                        IssueCount = 700,
                        Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                    }
                }
            });

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.Equal("Batman", result.Results[0].Title);
        Assert.Equal(796, result.Results[0].ComicVineId);
    }

    [Fact]
    public async Task SearchSeriesAsync_WithNoApiKey_ReturnsError()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(false);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ComicVine API key not configured", result.Error);
    }

    [Fact]
    public async Task GetSeriesByComicVineIdAsync_WithValidId_ReturnsCandidate()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.GetVolumeAsync(796, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume
                {
                    Id = 796,
                    Name = "Batman",
                    StartYear = 1940,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                }
            });

        // Act
        var result = await _service.GetSeriesByComicVineIdAsync(796);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(796, result.ComicVineId);
        Assert.Equal("Batman", result.Title);
        Assert.Equal("DC Comics", result.Publisher);
    }

    [Fact]
    public async Task MatchSeriesAsync_WithValidIds_UpdatesSeries()
    {
        // Arrange
        var series = new Series { Title = "Batman" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.GetVolumeAsync(796, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume
                {
                    Id = 796,
                    Name = "Batman",
                    StartYear = 1940,
                    Description = "The Dark Knight",
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                }
            });
        _mockComicVineClient.Setup(x => x.GetVolumeIssuesAsync(
            796, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });

        // Act
        var result = await _service.MatchSeriesAsync(series.Id, 796);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(796, result.ComicVineId);

        var updatedSeries = await _dbContext.Series.FindAsync(series.Id);
        Assert.Equal(796, updatedSeries!.ComicVineId);
        Assert.Equal("ComicVine", updatedSeries.ExternalSource);
    }

    [Fact]
    public async Task MatchSeriesAsync_WithNonExistentSeries_ReturnsError()
    {
        // Act
        var result = await _service.MatchSeriesAsync(999, 796);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task AutoMatchSeriesAsync_WithHighConfidenceMatch_MatchesAutomatically()
    {
        // Arrange
        var series = new Series { Title = "Batman", Publisher = "DC Comics", StartYear = 1940 };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.SearchVolumesAsync(
            "Batman", 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                Results = new List<ComicVineVolume>
                {
                    new ComicVineVolume
                    {
                        Id = 796,
                        Name = "Batman",
                        StartYear = 1940,
                        Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                    }
                }
            });
        _mockComicVineClient.Setup(x => x.GetVolumeAsync(796, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume
                {
                    Id = 796,
                    Name = "Batman",
                    StartYear = 1940,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                }
            });
        _mockComicVineClient.Setup(x => x.GetVolumeIssuesAsync(
            796, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });

        // Act
        var result = await _service.AutoMatchSeriesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.RequiresManualReview);
        Assert.Equal(796, result.MatchedComicVineId);
    }

    [Fact]
    public async Task AutoMatchSeriesAsync_WithLowConfidenceMatch_RequiresManualReview()
    {
        // Arrange
        var series = new Series { Title = "Something Random" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.SearchVolumesAsync(
            "Something Random", 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                Results = new List<ComicVineVolume>
                {
                    new ComicVineVolume
                    {
                        Id = 123,
                        Name = "Something Else",
                        StartYear = 2020
                    }
                }
            });

        // Act
        var result = await _service.AutoMatchSeriesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.RequiresManualReview);
        Assert.Null(result.MatchedComicVineId);
    }

    [Fact]
    public async Task UnmatchSeriesAsync_WithMatchedSeries_ClearsComicVineId()
    {
        // Arrange
        var series = new Series
        {
            Title = "Batman",
            ComicVineId = 796,
            ExternalId = "796",
            ExternalSource = "ComicVine"
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.UnmatchSeriesAsync(series.Id);

        // Assert
        Assert.True(result);

        var updatedSeries = await _dbContext.Series.FindAsync(series.Id);
        Assert.Null(updatedSeries!.ComicVineId);
        Assert.Null(updatedSeries.ExternalSource);
    }

    [Fact]
    public async Task AddSeriesByComicVineIdAsync_WithValidId_CreatesSeries()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.GetVolumeAsync(796, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume
                {
                    Id = 796,
                    Name = "Batman",
                    StartYear = 1940,
                    Description = "The Dark Knight",
                    IssueCount = 700,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" }
                }
            });
        _mockComicVineClient.Setup(x => x.GetVolumeIssuesAsync(
            796, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>
                {
                    new ComicVineIssue { Id = 1001, IssueNumber = "1", Name = "First Issue" },
                    new ComicVineIssue { Id = 1002, IssueNumber = "2", Name = "Second Issue" }
                }
            });

        // Act
        var result = await _service.AddSeriesByComicVineIdAsync(796);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.SeriesId);
        Assert.Equal(796, result.ComicVineId);
        Assert.Equal("Batman", result.Title);
        Assert.Equal(2, result.IssuesCreated);

        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .FirstOrDefaultAsync(s => s.Id == result.SeriesId);
        Assert.NotNull(series);
        Assert.Equal(796, series.ComicVineId);
        Assert.Equal(2, series.Issues.Count);
    }

    [Fact]
    public async Task AddSeriesByComicVineIdAsync_WithDuplicate_ReturnsConflict()
    {
        // Arrange
        var existingSeries = new Series { Title = "Batman", ComicVineId = 796 };
        _dbContext.Series.Add(existingSeries);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AddSeriesByComicVineIdAsync(796);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.AlreadyExists);
        Assert.Equal(existingSeries.Id, result.ExistingSeriesId);
    }

    [Fact]
    public async Task RefreshSeriesMetadataAsync_WithMatchedSeries_UpdatesMetadata()
    {
        // Arrange
        var series = new Series
        {
            Title = "Batman",
            ComicVineId = 796,
            Overview = "Old description"
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.GetVolumeAsync(796, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                Data = new ComicVineVolume
                {
                    Id = 796,
                    Name = "Batman",
                    Description = "New description"
                }
            });
        _mockComicVineClient.Setup(x => x.GetVolumeIssuesAsync(
            796, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });

        // Act
        var result = await _service.RefreshSeriesMetadataAsync(series.Id, forceRefresh: true);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.MetadataChanged);

        var updatedSeries = await _dbContext.Series.FindAsync(series.Id);
        Assert.Equal("New description", updatedSeries!.Overview);
    }

    [Fact]
    public async Task RefreshSeriesMetadataAsync_WithUnmatchedSeries_ReturnsError()
    {
        // Arrange
        var series = new Series { Title = "Batman" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RefreshSeriesMetadataAsync(series.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not matched", result.Error);
    }

    [Fact]
    public async Task SyncIssuesFromComicVineAsync_WithNewIssues_AddsToDatabase()
    {
        // Arrange
        var series = new Series { Title = "Batman", ComicVineId = 796 };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.GetVolumeIssuesAsync(
            796, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>
                {
                    new ComicVineIssue { Id = 1001, IssueNumber = "1" },
                    new ComicVineIssue { Id = 1002, IssueNumber = "2" },
                    new ComicVineIssue { Id = 1003, IssueNumber = "3" }
                }
            });

        // Act
        var result = await _service.SyncIssuesFromComicVineAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.IssuesAdded);

        var issues = await _dbContext.Issues.Where(i => i.SeriesId == series.Id).ToListAsync();
        Assert.Equal(3, issues.Count);
    }

    [Fact]
    public async Task ConfidenceScore_ExactTitleMatch_GivesHighScore()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.IsConfigured).Returns(true);
        _mockComicVineClient.Setup(x => x.SearchVolumesAsync(
            "Batman", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                Results = new List<ComicVineVolume>
                {
                    new ComicVineVolume { Id = 796, Name = "Batman" }
                }
            });

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        // Exact match should give 50 (base) + 40 (exact title) = 90
        Assert.True(result.Results[0].ConfidenceScore >= 90);
    }
}

