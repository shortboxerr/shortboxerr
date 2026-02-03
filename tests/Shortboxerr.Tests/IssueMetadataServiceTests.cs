using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

public class IssueMetadataServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<IssueMetadataService>> _mockLogger;
    private readonly IssueMetadataService _service;

    public IssueMetadataServiceTests()
    {
        // Use in-memory SQLite for testing
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<IssueMetadataService>>();

        // Setup default settings
        _mockSettingsService.Setup(x => x.GetAsync<ComicVineSettings>(
                "comicvine", 
                It.IsAny<ComicVineSettings>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { RefreshIntervalDays = 7 });

        _service = new IssueMetadataService(
            _dbContext,
            _mockComicVineClient.Object,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetIssueByComicVineIdAsync_WithValidId_ReturnsIssueDetail()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.GetIssueAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                Data = new ComicVineIssue
                {
                    Id = 1234,
                    Name = "The Origin",
                    IssueNumber = "1",
                    Description = "First issue",
                    CoverDate = new DateTime(2024, 1, 1),
                    StoreDate = new DateTime(2024, 1, 3),
                    Image = new ComicVineImage { MediumUrl = "http://example.com/cover.jpg" },
                    StoryArcs = new List<ComicVineStoryArcRef>
                    {
                        new() { Id = 100, Name = "Crisis Arc" }
                    }
                }
            });

        // Act
        var result = await _service.GetIssueByComicVineIdAsync(1234);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Issue);
        Assert.Equal(1234, result.Issue.ComicVineId);
        Assert.Equal("The Origin", result.Issue.Name);
        Assert.Equal("1", result.Issue.IssueNumber);
        Assert.Single(result.Issue.StoryArcs);
        Assert.Equal("Crisis Arc", result.Issue.StoryArcs[0].Name);
    }

    [Fact]
    public async Task GetIssueByComicVineIdAsync_WithInvalidId_ReturnsError()
    {
        // Arrange
        _mockComicVineClient.Setup(x => x.GetIssueAsync(9999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue>
            {
                Success = false,
                Error = "Issue not found"
            });

        // Act
        var result = await _service.GetIssueByComicVineIdAsync(9999);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Issue not found", result.Error);
    }

    [Fact]
    public async Task RefreshIssueMetadataAsync_WithNonExistentIssue_ReturnsError()
    {
        // Act
        var result = await _service.RefreshIssueMetadataAsync(9999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RefreshIssueMetadataAsync_WithUnmatchedIssue_ReturnsError()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 1,
            ComicVineId = null // Not matched
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RefreshIssueMetadataAsync(issue.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not linked to ComicVine", result.Error);
    }

    [Fact]
    public async Task RefreshIssueMetadataAsync_WithMatchedIssue_UpdatesMetadata()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 1,
            ComicVineId = 1234,
            Title = "Old Title"
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.GetIssueAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                Data = new ComicVineIssue
                {
                    Id = 1234,
                    Name = "New Title",
                    IssueNumber = "1",
                    Description = "Updated description",
                    CoverDate = new DateTime(2024, 1, 1),
                    StoreDate = new DateTime(2024, 1, 3),
                    StoryArcs = new List<ComicVineStoryArcRef>()
                }
            });

        // Act
        var result = await _service.RefreshIssueMetadataAsync(issue.Id, force: true);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.WasUpdated);
        Assert.Contains("Title", result.UpdatedFields);
        
        // Verify database was updated
        var updatedIssue = await _dbContext.Issues.FindAsync(issue.Id);
        Assert.Equal("New Title", updatedIssue?.Title);
    }

    [Fact]
    public async Task SyncIssueStoryArcsAsync_AddsNewStoryArcs()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 1,
            ComicVineId = 1234
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.GetIssueAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                Data = new ComicVineIssue
                {
                    Id = 1234,
                    Name = "Issue 1",
                    IssueNumber = "1",
                    StoryArcs = new List<ComicVineStoryArcRef>
                    {
                        new() { Id = 100, Name = "Crisis on Infinite Earths" },
                        new() { Id = 101, Name = "Secret Wars" }
                    }
                }
            });

        // Act
        var result = await _service.SyncIssueStoryArcsAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.StoryArcsAdded);
        Assert.Equal(0, result.StoryArcsRemoved);
        Assert.Contains("Crisis on Infinite Earths", result.StoryArcNames);
        Assert.Contains("Secret Wars", result.StoryArcNames);

        // Verify database
        var arcs = await _dbContext.IssueStoryArcs.Where(a => a.IssueId == issue.Id).ToListAsync();
        Assert.Equal(2, arcs.Count);
    }

    [Fact]
    public async Task DetectSpecialIssuesAsync_DetectsAnnuals()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _dbContext.Issues.AddRange(new[]
        {
            new Issue { SeriesId = series.Id, IssueNumber = 1, IssueNumberText = "1" },
            new Issue { SeriesId = series.Id, IssueNumber = 2, IssueNumberText = "Annual 1" },
            new Issue { SeriesId = series.Id, IssueNumber = 3, IssueNumberText = "3" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DetectSpecialIssuesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.AnnualsDetected);
        Assert.Single(result.SpecialIssues.Where(i => i.IsAnnual));
    }

    [Fact]
    public async Task DetectSpecialIssuesAsync_DetectsSpecialTypes()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _dbContext.Issues.AddRange(new[]
        {
            new Issue { SeriesId = series.Id, IssueNumber = 1, IssueNumberText = "1" },
            new Issue { SeriesId = series.Id, IssueNumber = 0, Title = "Giant-Size Special" },
            new Issue { SeriesId = series.Id, IssueNumber = 0, IssueNumberText = "One-Shot" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DetectSpecialIssuesAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.SpecialsDetected);
    }

    [Theory]
    [InlineData("Annual 1", true, false)]
    [InlineData("Annual 2024", true, false)]
    [InlineData("1", false, false)]
    [InlineData("Giant-Size Special", false, true)]
    [InlineData("One-Shot", false, true)]
    [InlineData("Special Edition", false, true)]
    [InlineData("100-Page Giant", false, true)]
    public async Task DetectSpecialIssuesAsync_CorrectlyIdentifiesIssueTypes(
        string issueNumberText, bool expectedAnnual, bool expectedSpecial)
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 0, 
            IssueNumberText = issueNumberText 
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DetectSpecialIssuesAsync(series.Id);

        // Assert
        if (expectedAnnual || expectedSpecial)
        {
            Assert.Single(result.SpecialIssues);
            var detected = result.SpecialIssues[0];
            Assert.Equal(expectedAnnual, detected.IsAnnual);
            Assert.Equal(expectedSpecial, detected.IsSpecial);
        }
        else
        {
            Assert.Empty(result.SpecialIssues);
        }
    }

    [Fact]
    public async Task RefreshSeriesIssuesMetadataAsync_RefreshesAllMatchedIssues()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        _dbContext.Issues.AddRange(new[]
        {
            new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 1001 },
            new Issue { SeriesId = series.Id, IssueNumber = 2, ComicVineId = 1002 },
            new Issue { SeriesId = series.Id, IssueNumber = 3, ComicVineId = null } // Unmatched
        });
        await _dbContext.SaveChangesAsync();

        _mockComicVineClient.Setup(x => x.GetIssueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                Data = new ComicVineIssue
                {
                    Id = id,
                    Name = $"Issue {id}",
                    IssueNumber = id.ToString(),
                    StoryArcs = new List<ComicVineStoryArcRef>()
                }
            });

        // Act
        var result = await _service.RefreshSeriesIssuesMetadataAsync(series.Id, force: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalIssues); // Only matched issues
        Assert.Equal(2, result.IssuesRefreshed);
        Assert.Equal(0, result.IssuesFailed);
    }
}

