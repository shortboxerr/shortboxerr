using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Models;
using PendingMatchEntity = Shortboxerr.Core.Entities.PendingMatch;
using PendingMatchStatus = Shortboxerr.Core.Entities.PendingMatchStatus;
using Series = Shortboxerr.Core.Entities.Series;
using SeriesStatus = Shortboxerr.Core.Entities.SeriesStatus;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for AutoMatchService.
/// </summary>
public class AutoMatchServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly Mock<IEditionMetadataService> _mockEditionMetadataService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<AutoMatchService>> _mockLogger;
    private readonly AutoMatchService _service;

    public AutoMatchServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _mockEditionMetadataService = new Mock<IEditionMetadataService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<AutoMatchService>>();

        // Default settings
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings 
            { 
                ApiKey = "test-key", 
                Enabled = true,
                AutoMatchThreshold = 85
            });

        _service = new AutoMatchService(
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

    #region AutoMatchStagedItem Tests

    [Fact]
    public async Task AutoMatchStagedItemAsync_WithNoParsedInfo_ReturnsError()
    {
        // Arrange
        var stagedItem = new StagedItem
        {
            Path = "/staging/test.cbz",
            FileName = "test.cbz",
            Extension = ".cbz",
            ParsedInfo = null
        };

        // Act
        var result = await _service.AutoMatchStagedItemAsync(stagedItem);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unable to parse", result.Error);
    }

    [Fact]
    public async Task AutoMatchStagedItemAsync_WithExistingLocalSeries_ReturnsExistingMatch()
    {
        // Arrange
        var series = new Series { Title = "Batman", StartYear = 2016, Status = SeriesStatus.Continuing };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var stagedItem = new StagedItem
        {
            Path = "/staging/Batman 001 (2016).cbz",
            FileName = "Batman 001 (2016).cbz",
            Extension = ".cbz",
            ParsedInfo = new ParsedComicInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016
            }
        };

        // Act
        var result = await _service.AutoMatchStagedItemAsync(stagedItem);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.AutoMatched);
        Assert.Equal(series.Id, result.MatchedSeriesId);
        Assert.Equal(100, result.ConfidenceScore);
    }

    [Fact]
    public async Task AutoMatchStagedItemAsync_WithHighConfidenceMatch_AutoMatches()
    {
        // Arrange
        var stagedItem = new StagedItem
        {
            Path = "/staging/Batman 001 (2016).cbz",
            FileName = "Batman 001 (2016).cbz",
            Extension = ".cbz",
            ParsedInfo = new ParsedComicInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016
            }
        };

        _mockSeriesMetadataService
            .Setup(x => x.SearchSeriesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesSearchResult
            {
                Success = true,
                Results = new List<SeriesMatchCandidate>
                {
                    new SeriesMatchCandidate
                    {
                        ComicVineId = 12345,
                        Title = "Batman",
                        StartYear = 2016,
                        Publisher = "DC Comics",
                        ConfidenceScore = 95
                    }
                }
            });

        // Act
        var result = await _service.AutoMatchStagedItemAsync(stagedItem);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.AutoMatched);
        Assert.False(result.RequiresReview);
        Assert.Equal(95, result.ConfidenceScore);
    }

    [Fact]
    public async Task AutoMatchStagedItemAsync_WithLowConfidenceMatch_RequiresReview()
    {
        // Arrange
        var stagedItem = new StagedItem
        {
            Path = "/staging/Batman 001 (2016).cbz",
            FileName = "Batman 001 (2016).cbz",
            Extension = ".cbz",
            ParsedInfo = new ParsedComicInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016
            }
        };

        _mockSeriesMetadataService
            .Setup(x => x.SearchSeriesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesSearchResult
            {
                Success = true,
                Results = new List<SeriesMatchCandidate>
                {
                    new SeriesMatchCandidate
                    {
                        ComicVineId = 12345,
                        Title = "Batman Chronicles",
                        StartYear = 2016,
                        Publisher = "DC Comics",
                        ConfidenceScore = 70  // Below threshold
                    }
                }
            });

        // Act
        var result = await _service.AutoMatchStagedItemAsync(stagedItem);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.AutoMatched);
        Assert.True(result.RequiresReview);
        Assert.Equal(70, result.ConfidenceScore);
    }

    [Fact]
    public async Task AutoMatchStagedItemAsync_ForCollection_UsesEditionService()
    {
        // Arrange
        var stagedItem = new StagedItem
        {
            Path = "/staging/Batman Vol 1 TPB.cbz",
            FileName = "Batman Vol 1 TPB.cbz",
            Extension = ".cbz",
            ParsedInfo = new ParsedComicInfo
            {
                SeriesTitle = "Batman",
                VolumeNumber = 1,
                EditionIndicator = "TPB"
            }
        };

        _mockEditionMetadataService
            .Setup(x => x.SearchEditionsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EditionSearchResult
            {
                Success = true,
                Results = new List<EditionMatchCandidate>
                {
                    new EditionMatchCandidate
                    {
                        ComicVineId = 54321,
                        Title = "Batman Vol. 1",
                        StartYear = 2016,
                        Publisher = "DC Comics",
                        ConfidenceScore = 90
                    }
                }
            });

        // Act
        var result = await _service.AutoMatchStagedItemAsync(stagedItem);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.AutoMatched);
        _mockEditionMetadataService.Verify(
            x => x.SearchEditionsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Bulk Auto-Match Tests

    [Fact]
    public async Task AutoMatchAllUnmatchedSeriesAsync_WithNoUnmatchedSeries_ReturnsEmptyResult()
    {
        // Act
        var result = await _service.AutoMatchAllUnmatchedSeriesAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.TotalProcessed);
    }

    [Fact]
    public async Task AutoMatchAllUnmatchedSeriesAsync_MatchesUnmatchedSeries()
    {
        // Arrange
        _dbContext.Series.AddRange(
            new Series { Title = "Batman", Status = SeriesStatus.Continuing },
            new Series { Title = "Superman", Status = SeriesStatus.Continuing }
        );
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.SearchSeriesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesSearchResult
            {
                Success = true,
                Results = new List<SeriesMatchCandidate>
                {
                    new SeriesMatchCandidate
                    {
                        ComicVineId = 12345,
                        Title = "Test",
                        ConfidenceScore = 95
                    }
                }
            });

        _mockSeriesMetadataService
            .Setup(x => x.MatchSeriesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesMatchResult { Success = true });

        // Act
        var result = await _service.AutoMatchAllUnmatchedSeriesAsync(matchImmediately: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.AutoMatched);
    }

    [Fact]
    public async Task AutoMatchAllUnmatchedSeriesAsync_QueuesLowConfidenceMatches()
    {
        // Arrange
        _dbContext.Series.Add(new Series { Title = "Unknown Comic", Status = SeriesStatus.Continuing });
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.SearchSeriesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesSearchResult
            {
                Success = true,
                Results = new List<SeriesMatchCandidate>
                {
                    new SeriesMatchCandidate
                    {
                        ComicVineId = 12345,
                        Title = "Something Else",
                        ConfidenceScore = 60  // Below threshold
                    }
                }
            });

        // Act
        var result = await _service.AutoMatchAllUnmatchedSeriesAsync(matchImmediately: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.AutoMatched);
        Assert.Equal(1, result.QueuedForReview);

        // Verify pending match was created
        var pendingMatches = await _dbContext.PendingMatches.ToListAsync();
        Assert.Single(pendingMatches);
        Assert.Equal("Series", pendingMatches[0].ItemType);
    }

    #endregion

    #region Pending Match Tests

    [Fact]
    public async Task GetPendingMatchesAsync_ReturnsPendingMatches()
    {
        // Arrange
        _dbContext.PendingMatches.AddRange(
            new PendingMatchEntity
            {
                ItemType = "Series",
                ItemId = 1,
                ItemTitle = "Batman",
                TopConfidenceScore = 70,
                Status = PendingMatchStatus.Pending,
                CandidatesJson = "[]"
            },
            new PendingMatchEntity
            {
                ItemType = "Series",
                ItemId = 2,
                ItemTitle = "Superman",
                TopConfidenceScore = 65,
                Status = PendingMatchStatus.Accepted,
                CandidatesJson = "[]"
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var pending = await _service.GetPendingMatchesAsync();

        // Assert
        Assert.Single(pending);
        Assert.Equal("Batman", pending[0].ItemTitle);
    }

    [Fact]
    public async Task AcceptPendingMatchAsync_MatchesAndResolvesStatus()
    {
        // Arrange
        var series = new Series { Title = "Batman", Status = SeriesStatus.Continuing };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var pendingMatch = new PendingMatchEntity
        {
            ItemType = "Series",
            ItemId = series.Id,
            ItemTitle = "Batman",
            TopConfidenceScore = 80,
            Status = PendingMatchStatus.Pending,
            CandidatesJson = "[{\"ComicVineId\":12345,\"Title\":\"Batman\",\"ConfidenceScore\":80}]"
        };
        _dbContext.PendingMatches.Add(pendingMatch);
        await _dbContext.SaveChangesAsync();

        _mockSeriesMetadataService
            .Setup(x => x.MatchSeriesAsync(series.Id, 12345, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeriesMatchResult { Success = true, SeriesId = series.Id, ComicVineId = 12345 });

        // Act
        var result = await _service.AcceptPendingMatchAsync(pendingMatch.Id);

        // Assert
        Assert.True(result);
        
        var updated = await _dbContext.PendingMatches.FindAsync(pendingMatch.Id);
        Assert.Equal(PendingMatchStatus.Accepted, updated!.Status);
        Assert.Equal(12345, updated.SelectedComicVineId);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task RejectPendingMatchAsync_SetsRejectedStatus()
    {
        // Arrange
        var pendingMatch = new PendingMatchEntity
        {
            ItemType = "Series",
            ItemId = 1,
            ItemTitle = "Batman",
            Status = PendingMatchStatus.Pending,
            CandidatesJson = "[]"
        };
        _dbContext.PendingMatches.Add(pendingMatch);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RejectPendingMatchAsync(pendingMatch.Id);

        // Assert
        Assert.True(result);
        
        var updated = await _dbContext.PendingMatches.FindAsync(pendingMatch.Id);
        Assert.Equal(PendingMatchStatus.Rejected, updated!.Status);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task AcceptPendingMatchAsync_WithNonexistentId_ReturnsFalse()
    {
        // Act
        var result = await _service.AcceptPendingMatchAsync(99999);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsConfiguredSettings()
    {
        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        Assert.Equal(85, settings.ConfidenceThreshold);
        Assert.True(settings.AutoMatchOnImport);
        Assert.True(settings.CreateMissingItems);
    }

    #endregion
}

