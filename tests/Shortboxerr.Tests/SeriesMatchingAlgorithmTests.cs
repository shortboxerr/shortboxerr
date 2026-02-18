using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for series matching algorithm and confidence scoring.
/// </summary>
public class SeriesMatchingAlgorithmTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<SeriesMetadataService>> _mockLogger;
    private readonly SeriesMetadataService _service;

    public SeriesMatchingAlgorithmTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<SeriesMetadataService>>();

        // Default settings
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings 
            { 
                ApiKey = "test-key", 
                Enabled = true,
                AutoMatchThreshold = 85
            });
        
        // Pull list settings for annual integration
        _mockSettingsService.Setup(x => x.GetAsync<PullListSettings>(
            "pulllist", It.IsAny<PullListSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullListSettings { EnableSeriesAnnualIntegration = true });

        _service = new SeriesMetadataService(
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

    #region Confidence Score Tests

    [Fact]
    public async Task Search_ExactTitleMatch_ReturnsHighConfidence()
    {
        // Arrange: Searching for "Batman" should match "Batman" with high confidence
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 100));

        SetupMockSearch(searchResults);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.True(result.Results.First().ConfidenceScore >= 90, 
            $"Exact match should have high confidence. Got: {result.Results.First().ConfidenceScore}");
    }

    [Fact]
    public async Task Search_TitleStartsWithQuery_ReturnsMediumConfidence()
    {
        // Arrange: Searching for "Batman" should match "Batman: Rebirth" with medium confidence
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman: Rebirth", 2016, "DC Comics", 50));

        SetupMockSearch(searchResults);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        var confidence = result.Results.First().ConfidenceScore;
        Assert.True(confidence >= 70 && confidence < 90,
            $"Starts-with match should have medium confidence. Got: {confidence}");
    }

    [Fact]
    public async Task Search_TitleContainsQuery_ReturnsLowerConfidence()
    {
        // Arrange: Searching for "Batman" should match "Detective Comics featuring Batman" with lower confidence
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Detective Comics featuring Batman", 2016, "DC Comics", 50));

        SetupMockSearch(searchResults);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        var confidence = result.Results.First().ConfidenceScore;
        Assert.True(confidence >= 60 && confidence < 80,
            $"Contains match should have lower confidence. Got: {confidence}");
    }

    [Fact]
    public async Task Search_YearMatch_IncreasesConfidence()
    {
        // Arrange: Search without year filter, then compare with year filter
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 100));

        SetupMockSearch(searchResults);

        // Act - First search without year
        var resultNoYear = await _service.SearchSeriesAsync("Batman");
        var confidenceNoYear = resultNoYear.Results.First().ConfidenceScore;

        // Act - Search with year filter
        var result = await _service.SearchSeriesAsync("Batman", yearStart: 2016, yearEnd: 2016);
        var confidenceWithYear = result.Results.First().ConfidenceScore;

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        // Year filter should maintain or increase confidence
        Assert.True(confidenceWithYear >= confidenceNoYear,
            $"Year match should maintain or increase confidence. With: {confidenceWithYear}, Without: {confidenceNoYear}");
    }

    [Fact]
    public async Task Search_PublisherMatch_IncreasesConfidence()
    {
        // Arrange
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 100));

        SetupMockSearch(searchResults);

        // Act - Search without publisher
        var resultNoPublisher = await _service.SearchSeriesAsync("Batman");
        var confidenceNoPublisher = resultNoPublisher.Results.First().ConfidenceScore;

        // Act - Search with publisher filter
        var result = await _service.SearchSeriesAsync("Batman", publisher: "DC");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        var confidenceWithPublisher = result.Results.First().ConfidenceScore;
        Assert.True(confidenceWithPublisher >= confidenceNoPublisher,
            $"Publisher match should maintain or increase confidence. With: {confidenceWithPublisher}, Without: {confidenceNoPublisher}");
    }

    [Fact]
    public async Task Search_MultipleResults_SortedByConfidence()
    {
        // Arrange: Multiple results with different confidence levels
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 100),          // Exact match
            ("Batman: Rebirth", 2016, "DC Comics", 50),  // Starts with
            ("The Batman Chronicles", 2016, "DC Comics", 30)); // Contains

        SetupMockSearch(searchResults);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Results.Count);
        
        // Verify sorted by confidence descending
        var scores = result.Results.Select(r => r.ConfidenceScore).ToList();
        for (int i = 0; i < scores.Count - 1; i++)
        {
            Assert.True(scores[i] >= scores[i + 1],
                $"Results should be sorted by confidence descending. Got: {string.Join(", ", scores)}");
        }
    }

    [Fact]
    public async Task Search_LargeIssueCount_IncreasesConfidence()
    {
        // Arrange: Series with large issue count should get bonus
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 150)); // Large issue count

        SetupMockSearch(searchResults);

        // Act
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Results);
        // Large issue count (>50) should add +5 to base score
        var confidence = result.Results.First().ConfidenceScore;
        Assert.True(confidence >= 95, 
            $"Large issue count should increase confidence. Got: {confidence}");
    }

    #endregion

    #region Edge Case Tests - Same Name Different Years

    [Fact]
    public async Task Search_SameNameDifferentYears_ReturnsAllWithoutYearFilter()
    {
        // Arrange: Multiple "Batman" series from different years
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 137),
            ("Batman", 2011, "DC Comics", 52),
            ("Batman", 1940, "DC Comics", 713));

        SetupMockSearch(searchResults);

        // Act - Search without year filter to get all results
        var result = await _service.SearchSeriesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Results.Count);
        
        // All should have high confidence (exact match)
        Assert.All(result.Results, r => 
            Assert.True(r.ConfidenceScore >= 90, $"Exact match should have high confidence: {r.StartYear} got {r.ConfidenceScore}"));
    }

    [Fact]
    public async Task Search_WithYearFilter_FiltersResults()
    {
        // Arrange: Multiple "Batman" series from different years
        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 137),
            ("Batman", 2011, "DC Comics", 52),
            ("Batman", 1940, "DC Comics", 713));

        SetupMockSearch(searchResults);

        // Act - Search with year filter
        var result = await _service.SearchSeriesAsync("Batman", yearStart: 2010, yearEnd: 2020);

        // Assert - Only 2011 and 2016 should be returned (within year range)
        Assert.True(result.Success);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => 
            Assert.True(r.StartYear >= 2010 && r.StartYear <= 2020, 
                $"Only results within year range expected: got {r.StartYear}"));
    }

    #endregion

    #region Auto-Match Tests

    [Fact]
    public async Task AutoMatch_NonexistentSeries_ReturnsError()
    {
        // Act
        var result = await _service.AutoMatchSeriesAsync(99999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public async Task AutoMatch_NoResults_ReturnsFailure()
    {
        // Arrange
        var series = new Series { Title = "NonexistentComic123456", Status = SeriesStatus.Continuing };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Results = new List<ComicVineVolume>(),
                TotalResults = 0
            });

        // Act
        var result = await _service.AutoMatchSeriesAsync(series.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No matches found", result.Error);
    }

    [Fact]
    public async Task AutoMatch_WithResults_ReturnsConfidenceScore()
    {
        // Arrange
        var series = new Series { Title = "Batman", StartYear = 2016, Status = SeriesStatus.Continuing };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var searchResults = CreateSearchResultsForConfidenceTest(
            ("Batman", 2016, "DC Comics", 137));

        SetupMockSearch(searchResults);

        // Mock GetVolumeAsync for the match operation
        _mockComicVineClient
            .Setup(x => x.GetVolumeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineVolume>
            {
                Success = true,
                StatusCode = 1,
                Data = new ComicVineVolume
                {
                    Id = 12345,
                    Name = "Batman",
                    StartYear = 2016,
                    Publisher = new ComicVinePublisherRef { Id = 10, Name = "DC Comics" },
                    IssueCount = 137
                }
            });

        // Mock GetVolumeIssuesAsync (needed for sync)
        _mockComicVineClient
            .Setup(x => x.GetVolumeIssuesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                StatusCode = 1,
                Results = new List<ComicVineIssue>(),
                TotalResults = 0
            });

        // Act
        var result = await _service.AutoMatchSeriesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ConfidenceScore > 0, "Should have a confidence score");
        // High confidence match should not require review
        Assert.False(result.RequiresManualReview);
    }

    #endregion

    #region Helper Methods

    private ComicVineSearchResult<ComicVineVolume> CreateSearchResultsForConfidenceTest(
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

    private void SetupMockSearch(ComicVineSearchResult<ComicVineVolume> results)
    {
        _mockComicVineClient
            .Setup(x => x.SearchVolumesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);
    }

    #endregion
}
