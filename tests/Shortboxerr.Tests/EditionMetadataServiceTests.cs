using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for EditionMetadataService.
/// </summary>
public class EditionMetadataServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<EditionMetadataService>> _mockLogger;
    private readonly EditionMetadataService _service;

    public EditionMetadataServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<EditionMetadataService>>();

        // Default settings
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings 
            { 
                ApiKey = "test-key", 
                Enabled = true,
                AutoMatchThreshold = 85
            });

        _service = new EditionMetadataService(
            _mockComicVineClient.Object,
            _dbContext,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Search Tests

    [Fact]
    public async Task SearchEditionsAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var searchResults = CreateMockSearchResults(
            ("Batman Vol. 1: I Am Gotham", 2016, "DC Comics", 6),
            ("Batman: The Court of Owls", 2012, "DC Comics", 7));

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _service.SearchEditionsAsync("Batman Vol");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public async Task SearchEditionsAsync_WithNoApiKey_ReturnsError()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { ApiKey = "" });

        // Act
        var result = await _service.SearchEditionsAsync("Batman");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Error?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public async Task SearchEditionsAsync_DetectsOmnibusType()
    {
        // Arrange
        var searchResults = CreateMockSearchResults(
            ("Batman Omnibus Vol. 1", 2020, "DC Comics", 50));

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _service.SearchEditionsAsync("Batman Omnibus");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.Equal(EditionType.Omnibus, result.Results.First().DetectedEditionType);
    }

    [Fact]
    public async Task SearchEditionsAsync_DetectsAbsoluteType()
    {
        // Arrange
        var searchResults = CreateMockSearchResults(
            ("Absolute Batman", 2024, "DC Comics", 6));

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _service.SearchEditionsAsync("Absolute Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.Equal(EditionType.AbsoluteEdition, result.Results.First().DetectedEditionType);
    }

    #endregion

    #region Get By ComicVine ID Tests

    [Fact]
    public async Task GetEditionByComicVineIdAsync_WithValidId_ReturnsCandidate()
    {
        // Arrange
        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVineVolume
                {
                    Id = 12345,
                    Name = "Batman Vol. 1",
                    StartYear = 2016,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" },
                    IssueCount = 6
                }
            });

        // Act
        var result = await _service.GetEditionByComicVineIdAsync(12345);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12345, result.ComicVineId);
        Assert.Equal("Batman Vol. 1", result.Title);
    }

    [Fact]
    public async Task GetEditionByComicVineIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(99999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = false,
                Error = "Not found"
            });

        // Act
        var result = await _service.GetEditionByComicVineIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// When search endpoint receives ComicVine volume ID (e.g. 4050-12345), it parses and calls
    /// GetEditionByComicVineIdAsync; this test verifies that path returns the edition (14.11).
    /// </summary>
    [Fact]
    public async Task GetEditionByComicVineIdAsync_WhenQueryIsVolumeId_ReturnsEditionForDirectLookup()
    {
        var parsed = ComicVineIdParser.TryParseAs("4050-12345", ComicVineResourceType.Volume);
        Assert.NotNull(parsed);
        Assert.Equal(12345, parsed.NumericId);

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVineVolume
                {
                    Id = 12345,
                    Name = "Batman: The Dark Knight Returns",
                    StartYear = 1986,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" },
                    IssueCount = 4
                }
            });

        var result = await _service.GetEditionByComicVineIdAsync(parsed.NumericId);

        Assert.NotNull(result);
        Assert.Equal(12345, result.ComicVineId);
        Assert.Equal("Batman: The Dark Knight Returns", result.Title);
    }

    #endregion

    #region Match Tests

    [Fact]
    public async Task MatchEditionAsync_WithValidIds_SetsComicVineId()
    {
        // Arrange
        var edition = new EditionTitle { Title = "Batman Vol. 1" };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = CreateMockVolume(12345, "Batman Vol. 1", 2016, "DC Comics", 6)
            });

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(12345, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });

        // Act
        var result = await _service.MatchEditionAsync(edition.Id, 12345);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(edition.Id, result.EditionId);
        Assert.Equal(12345, result.ComicVineId);

        // Verify the edition was updated
        var updatedEdition = await _dbContext.EditionTitles.FindAsync(edition.Id);
        Assert.Equal(12345, updatedEdition?.ComicVineId);
    }

    [Fact]
    public async Task MatchEditionAsync_WithInvalidEditionId_ReturnsError()
    {
        // Act
        var result = await _service.MatchEditionAsync(99999, 12345);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public async Task MatchEditionAsync_WithInvalidComicVineId_ReturnsError()
    {
        // Arrange
        var edition = new EditionTitle { Title = "Batman Vol. 1" };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(99999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = false,
                Error = "Not found"
            });

        // Act
        var result = await _service.MatchEditionAsync(edition.Id, 99999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error?.ToLowerInvariant() ?? "");
    }

    #endregion

    #region Unmatch Tests

    [Fact]
    public async Task UnmatchEditionAsync_WithMatchedEdition_ClearsComicVineId()
    {
        // Arrange
        var edition = new EditionTitle 
        { 
            Title = "Batman Vol. 1",
            ComicVineId = 12345,
            ComicVineUrl = "https://comicvine.gamespot.com/batman-vol-1/4050-12345/"
        };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.UnmatchEditionAsync(edition.Id);

        // Assert
        Assert.True(result);

        var updatedEdition = await _dbContext.EditionTitles.FindAsync(edition.Id);
        Assert.Null(updatedEdition?.ComicVineId);
        Assert.Null(updatedEdition?.ComicVineUrl);
    }

    [Fact]
    public async Task UnmatchEditionAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _service.UnmatchEditionAsync(99999);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Auto-Match Tests

    [Fact]
    public async Task AutoMatchEditionAsync_WithHighConfidence_MatchesAutomatically()
    {
        // Arrange
        var edition = new EditionTitle { Title = "Batman Vol. 1", Publisher = "DC Comics" };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        var searchResults = CreateMockSearchResults(
            ("Batman Vol. 1", 2016, "DC Comics", 6));

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = CreateMockVolume(12345, "Batman Vol. 1", 2016, "DC Comics", 6)
            });

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>()
            });

        // Act
        var result = await _service.AutoMatchEditionAsync(edition.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.RequiresManualReview);
        Assert.NotNull(result.MatchedComicVineId);
    }

    [Fact]
    public async Task AutoMatchEditionAsync_WithNoResults_ReturnsFailure()
    {
        // Arrange
        var edition = new EditionTitle { Title = "Nonexistent Comic TPB" };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                Results = new List<ComicVineVolume>(),
                TotalResults = 0
            });

        // Act
        var result = await _service.AutoMatchEditionAsync(edition.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No matches found", result.Error);
    }

    #endregion

    #region Content Sync Tests

    [Fact]
    public async Task SyncEditionContentsAsync_WithMatchedEdition_SyncsIssues()
    {
        // Arrange
        var edition = new EditionTitle 
        { 
            Title = "Batman Vol. 1",
            ComicVineId = 12345
        };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(12345, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>
                {
                    new() { Id = 1001, IssueNumber = "1", Name = "Issue 1" },
                    new() { Id = 1002, IssueNumber = "2", Name = "Issue 2" },
                    new() { Id = 1003, IssueNumber = "3", Name = "Issue 3" }
                },
                TotalResults = 3
            });

        // Act
        var result = await _service.SyncEditionContentsAsync(edition.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.IssuesFound);
        Assert.Equal(3, result.Mappings.Count);

        // Verify contents were created in database
        var contents = await _dbContext.EditionContents
            .Where(c => c.EditionTitleId == edition.Id)
            .ToListAsync();
        Assert.Equal(3, contents.Count);
    }

    [Fact]
    public async Task SyncEditionContentsAsync_WithUnmatchedEdition_ReturnsError()
    {
        // Arrange
        var edition = new EditionTitle 
        { 
            Title = "Batman Vol. 1",
            ComicVineId = null // Not matched
        };
        _dbContext.EditionTitles.Add(edition);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SyncEditionContentsAsync(edition.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not matched", result.Error?.ToLowerInvariant() ?? "");
    }

    #endregion

    #region Helper Methods

    private static ComicVineSearchResult<ComicVineVolume> CreateMockSearchResults(
        params (string Name, int Year, string Publisher, int IssueCount)[] volumes)
    {
        return new ComicVineSearchResult<ComicVineVolume>
        {
            Success = true,
            StatusCode = 1,
            Results = volumes.Select((v, i) => new ComicVineVolume
            {
                Id = 12345 + i,
                Name = v.Name,
                StartYear = v.Year,
                Publisher = new ComicVinePublisherRef { Id = 10, Name = v.Publisher },
                IssueCount = v.IssueCount,
                Image = new ComicVineImage { MediumUrl = $"https://example.com/{i}.jpg" },
                SiteDetailUrl = $"https://comicvine.gamespot.com/volume/{12345 + i}/"
            }).ToList(),
            TotalResults = volumes.Length
        };
    }

    private static ComicVineVolume CreateMockVolume(int id, string name, int year, string publisher, int issueCount)
    {
        return new ComicVineVolume
        {
            Id = id,
            Name = name,
            StartYear = year,
            Publisher = new ComicVinePublisherRef { Id = 10, Name = publisher },
            IssueCount = issueCount,
            Image = new ComicVineImage 
            { 
                MediumUrl = $"https://example.com/{id}.jpg",
                OriginalUrl = $"https://example.com/original/{id}.jpg"
            },
            SiteDetailUrl = $"https://comicvine.gamespot.com/{name.ToLower().Replace(" ", "-")}/4050-{id}/"
        };
    }

    #endregion
}

