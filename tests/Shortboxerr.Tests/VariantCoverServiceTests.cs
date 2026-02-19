using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

public class VariantCoverServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ILogger<VariantCoverService>> _mockLogger;
    private readonly VariantCoverService _service;

    public VariantCoverServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: $"VariantCoverTests_{Guid.NewGuid()}")
            .Options;

        _context = new ShortboxerrDbContext(options);
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockLogger = new Mock<ILogger<VariantCoverService>>();

        _service = new VariantCoverService(_context, _mockComicVineClient.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region DetectVariant Tests

    [Theory]
    [InlineData("Variant Cover", true, "Variant")]
    [InlineData("Cover B", true, "Variant B")]
    [InlineData("Cover C by Artist", true, "Variant C")]
    [InlineData("1:25 Incentive Cover", true, "1:25 Incentive")]
    [InlineData("1:50 Variant", true, "1:50 Incentive")]
    [InlineData("Virgin Cover Art", true, "Virgin")]
    [InlineData("Sketch variant edition", true, "Sketch")]
    [InlineData("SDCC Exclusive Cover", true, "SDCC Exclusive")]
    [InlineData("NYCC Variant Cover", true, "NYCC Exclusive")]
    [InlineData("Retailer Exclusive", true, "Retailer Exclusive")]
    [InlineData("Foil Cover", true, "Foil")]
    [InlineData("Lenticular motion cover", true, "Lenticular")]
    [InlineData("Second printing", true, "Second Printing")]
    [InlineData("3rd printing variant", true, "Third Printing")]
    [InlineData("Connecting cover part 1", true, "Connecting")]
    [InlineData("Blank cover only", true, "Blank")]
    public void DetectVariant_RecognizesVariantPatterns(string caption, bool expectedIsVariant, string expectedType)
    {
        var result = _service.DetectVariant(caption, null, null);

        Assert.Equal(expectedIsVariant, result.IsVariant);
        Assert.Equal(expectedType, result.VariantType);
        Assert.True(result.Confidence > 0);
    }

    [Theory]
    [InlineData("Main cover art")]
    [InlineData("Interior page 1")]
    [InlineData("Artist sketch of character")]
    [InlineData("Preview image")]
    [InlineData("Promotional art")]
    [InlineData("")]
    [InlineData(null)]
    public void DetectVariant_DoesNotMismatchNonVariants(string? caption)
    {
        var result = _service.DetectVariant(caption, null, null);

        Assert.False(result.IsVariant);
        Assert.Null(result.VariantType);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void DetectVariant_CombinesMultipleSources()
    {
        var result = _service.DetectVariant("Art", "variant cover", "filename.jpg");

        Assert.True(result.IsVariant);
        Assert.NotNull(result.VariantType);
    }

    [Fact]
    public void DetectVariant_HigherConfidenceForRarierVariants()
    {
        var result1to10 = _service.DetectVariant("1:10 variant", null, null);
        var result1to100 = _service.DetectVariant("1:100 variant", null, null);

        Assert.True(result1to100.Confidence > result1to10.Confidence);
    }

    [Fact]
    public void DetectVariant_MatchesMultiplePatterns()
    {
        var result = _service.DetectVariant("SDCC exclusive virgin variant cover", null, null);

        Assert.True(result.IsVariant);
        Assert.True(result.MatchedPatterns.Count >= 3);
    }

    #endregion

    #region GetVariantCoversAsync Tests

    [Fact]
    public async Task GetVariantCoversAsync_ReturnsEmptyForNoCovers()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var result = await _service.GetVariantCoversAsync(issue.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVariantCoversAsync_ReturnsCoversInCorrectOrder()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        _context.VariantCovers.AddRange(
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 1, ImageUrl = "url1", IsPrimaryCover = false, IsPreferred = false, VariantType = "Variant B" },
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 2, ImageUrl = "url2", IsPrimaryCover = true, IsPreferred = true, VariantType = null },
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 3, ImageUrl = "url3", IsPrimaryCover = false, IsPreferred = true, VariantType = "Variant A" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetVariantCoversAsync(issue.Id);

        Assert.Equal(3, result.Count);
        Assert.True(result[0].IsPrimaryCover);
        Assert.True(result[1].IsPreferred);
    }

    #endregion

    #region FetchVariantCoversAsync Tests

    [Fact]
    public async Task FetchVariantCoversAsync_ReturnsFailure_WhenIssueNotFound()
    {
        var result = await _service.FetchVariantCoversAsync(999);

        Assert.False(result.Success);
        Assert.Equal("Issue not found", result.Error);
    }

    [Fact]
    public async Task FetchVariantCoversAsync_ReturnsFailure_WhenNoComicVineId()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = null };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var result = await _service.FetchVariantCoversAsync(issue.Id);

        Assert.False(result.Success);
        Assert.Equal("Issue has no ComicVine ID", result.Error);
    }

    [Fact]
    public async Task FetchVariantCoversAsync_ReturnsFailure_WhenComicVineFails()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 12345 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        _mockComicVineClient.Setup(c => c.GetIssueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue> { Success = false, Error = "API error" });

        var result = await _service.FetchVariantCoversAsync(issue.Id);

        Assert.False(result.Success);
        Assert.Equal("API error", result.Error);
    }

    [Fact]
    public async Task FetchVariantCoversAsync_CreatesMainCover()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 12345 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var cvIssue = new ComicVineIssue
        {
            Id = 12345,
            IssueNumber = "1",
            Image = new ComicVineImage { OriginalUrl = "http://example.com/main.jpg" },
            AssociatedImages = new List<ComicVineAssociatedImage>()
        };

        _mockComicVineClient.Setup(c => c.GetIssueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue> { Success = true, Data = cvIssue });

        var result = await _service.FetchVariantCoversAsync(issue.Id);

        Assert.True(result.Success);
        Assert.Single(result.Variants);
        Assert.True(result.Variants[0].IsPrimaryCover);
        Assert.Equal("http://example.com/main.jpg", result.Variants[0].ImageUrl);
    }

    [Fact]
    public async Task FetchVariantCoversAsync_DetectsVariantsFromAssociatedImages()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 12345 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var cvIssue = new ComicVineIssue
        {
            Id = 12345,
            IssueNumber = "1",
            Image = new ComicVineImage { OriginalUrl = "http://example.com/main.jpg" },
            AssociatedImages = new List<ComicVineAssociatedImage>
            {
                new() { Id = 1, OriginalUrl = "http://example.com/variant1.jpg", Caption = "Variant Cover B" },
                new() { Id = 2, OriginalUrl = "http://example.com/variant2.jpg", Caption = "1:25 Incentive" },
                new() { Id = 3, OriginalUrl = "http://example.com/interior.jpg", Caption = "Interior art page" }
            }
        };

        _mockComicVineClient.Setup(c => c.GetIssueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue> { Success = true, Data = cvIssue });

        var result = await _service.FetchVariantCoversAsync(issue.Id);

        Assert.True(result.Success);
        Assert.Equal(3, result.Variants.Count);
        Assert.Equal(2, result.VariantsDetected);
        
        var variants = result.Variants.Where(v => !v.IsPrimaryCover).ToList();
        Assert.Equal("Variant B", variants.FirstOrDefault(v => v.Caption == "Variant Cover B")?.VariantType);
        Assert.Equal("1:25 Incentive", variants.FirstOrDefault(v => v.Caption == "1:25 Incentive")?.VariantType);
    }

    [Fact]
    public async Task FetchVariantCoversAsync_UpdatesExistingCovers()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 12345 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        _context.VariantCovers.Add(new VariantCoverEntity
        {
            IssueId = issue.Id,
            ComicVineImageId = 1,
            ImageUrl = "http://example.com/old.jpg",
            Caption = "Old caption",
            VariantType = "Variant",
            DetectedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var cvIssue = new ComicVineIssue
        {
            Id = 12345,
            IssueNumber = "1",
            Image = new ComicVineImage { OriginalUrl = "http://example.com/main.jpg" },
            AssociatedImages = new List<ComicVineAssociatedImage>
            {
                new() { Id = 1, OriginalUrl = "http://example.com/new.jpg", Caption = "SDCC Exclusive" }
            }
        };

        _mockComicVineClient.Setup(c => c.GetIssueAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineResult<ComicVineIssue> { Success = true, Data = cvIssue });

        var result = await _service.FetchVariantCoversAsync(issue.Id);

        Assert.True(result.Success);
        
        var dbCovers = await _context.VariantCovers.Where(v => v.ComicVineImageId == 1).ToListAsync();
        Assert.Single(dbCovers);
        Assert.Equal("http://example.com/new.jpg", dbCovers[0].ImageUrl);
        Assert.Equal("SDCC Exclusive", dbCovers[0].Caption);
        Assert.NotNull(dbCovers[0].UpdatedAt);
    }

    #endregion

    #region GetIssuesWithVariantsAsync Tests

    [Fact]
    public async Task GetIssuesWithVariantsAsync_ReturnsOnlyIssuesWithVariants()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue1 = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        var issue2 = new Issue { SeriesId = series.Id, IssueNumber = 2 };
        var issue3 = new Issue { SeriesId = series.Id, IssueNumber = 3 };
        _context.Issues.AddRange(issue1, issue2, issue3);
        await _context.SaveChangesAsync();

        _context.VariantCovers.Add(new VariantCoverEntity
        {
            IssueId = issue2.Id,
            ComicVineImageId = 1,
            ImageUrl = "url",
            IsPrimaryCover = false,
            VariantType = "Variant"
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetIssuesWithVariantsAsync(series.Id);

        Assert.Single(result);
        Assert.Equal(issue2.Id, result[0].IssueId);
    }

    [Fact]
    public async Task GetIssuesWithVariantsAsync_IncludesVariantCount()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        _context.VariantCovers.AddRange(
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 0, ImageUrl = "main", IsPrimaryCover = true },
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 1, ImageUrl = "v1", IsPrimaryCover = false },
            new VariantCoverEntity { IssueId = issue.Id, ComicVineImageId = 2, ImageUrl = "v2", IsPrimaryCover = false }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetIssuesWithVariantsAsync(series.Id);

        Assert.Single(result);
        Assert.Equal(2, result[0].VariantCount);
    }

    #endregion

    #region SetPreferredCoverAsync Tests

    [Fact]
    public async Task SetPreferredCoverAsync_SetsVariantAsPreferred()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var mainCover = new VariantCoverEntity
        {
            IssueId = issue.Id,
            ComicVineImageId = 0,
            ImageUrl = "main",
            IsPrimaryCover = true,
            IsPreferred = true
        };
        var variantCover = new VariantCoverEntity
        {
            IssueId = issue.Id,
            ComicVineImageId = 1,
            ImageUrl = "variant",
            IsPrimaryCover = false,
            IsPreferred = false
        };
        _context.VariantCovers.AddRange(mainCover, variantCover);
        await _context.SaveChangesAsync();

        await _service.SetPreferredCoverAsync(issue.Id, variantCover.Id);

        var covers = await _context.VariantCovers.Where(v => v.IssueId == issue.Id).ToListAsync();
        Assert.False(covers.First(c => c.IsPrimaryCover).IsPreferred);
        Assert.True(covers.First(c => !c.IsPrimaryCover).IsPreferred);
    }

    [Fact]
    public async Task SetPreferredCoverAsync_ResetsToMainCover_WhenNullPassed()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        var issue = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var mainCover = new VariantCoverEntity
        {
            IssueId = issue.Id,
            ComicVineImageId = 0,
            ImageUrl = "main",
            IsPrimaryCover = true,
            IsPreferred = false
        };
        var variantCover = new VariantCoverEntity
        {
            IssueId = issue.Id,
            ComicVineImageId = 1,
            ImageUrl = "variant",
            IsPrimaryCover = false,
            IsPreferred = true
        };
        _context.VariantCovers.AddRange(mainCover, variantCover);
        await _context.SaveChangesAsync();

        await _service.SetPreferredCoverAsync(issue.Id, null);

        var covers = await _context.VariantCovers.Where(v => v.IssueId == issue.Id).ToListAsync();
        Assert.True(covers.First(c => c.IsPrimaryCover).IsPreferred);
        Assert.False(covers.First(c => !c.IsPrimaryCover).IsPreferred);
    }

    #endregion

    #region GetSeriesStatsAsync Tests

    [Fact]
    public async Task GetSeriesStatsAsync_ReturnsCorrectStatistics()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue1 = new Issue { SeriesId = series.Id, IssueNumber = 1 };
        var issue2 = new Issue { SeriesId = series.Id, IssueNumber = 2 };
        var issue3 = new Issue { SeriesId = series.Id, IssueNumber = 3 };
        _context.Issues.AddRange(issue1, issue2, issue3);
        await _context.SaveChangesAsync();

        _context.VariantCovers.AddRange(
            new VariantCoverEntity { IssueId = issue1.Id, ComicVineImageId = 0, ImageUrl = "main1", IsPrimaryCover = true },
            new VariantCoverEntity { IssueId = issue1.Id, ComicVineImageId = 1, ImageUrl = "v1", IsPrimaryCover = false, VariantType = "Variant" },
            new VariantCoverEntity { IssueId = issue1.Id, ComicVineImageId = 2, ImageUrl = "v2", IsPrimaryCover = false, VariantType = "1:25 Incentive" },
            new VariantCoverEntity { IssueId = issue2.Id, ComicVineImageId = 3, ImageUrl = "v3", IsPrimaryCover = false, VariantType = "Variant" }
        );
        await _context.SaveChangesAsync();

        var stats = await _service.GetSeriesStatsAsync(series.Id);

        Assert.Equal(series.Id, stats.SeriesId);
        Assert.Equal(3, stats.TotalIssues);
        Assert.Equal(2, stats.IssuesWithVariants);
        Assert.Equal(3, stats.TotalVariants);
        Assert.Equal(1.5, stats.AverageVariantsPerIssue);
        Assert.Equal(2, stats.VariantsByType["Variant"]);
        Assert.Equal(1, stats.VariantsByType["1:25 Incentive"]);
    }

    [Fact]
    public async Task GetSeriesStatsAsync_HandlesEmptySeries()
    {
        var series = new Series { Title = "Empty Series" };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var stats = await _service.GetSeriesStatsAsync(series.Id);

        Assert.Equal(0, stats.TotalIssues);
        Assert.Equal(0, stats.IssuesWithVariants);
        Assert.Equal(0, stats.TotalVariants);
        Assert.Equal(0, stats.AverageVariantsPerIssue);
        Assert.Empty(stats.VariantsByType);
        Assert.Null(stats.LastFetchedAt);
    }

    #endregion

    #region FetchSeriesVariantCoversAsync Tests

    [Fact]
    public async Task FetchSeriesVariantCoversAsync_ReturnsFailure_WhenNoIssues()
    {
        var series = new Series { Title = "Empty Series" };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var result = await _service.FetchSeriesVariantCoversAsync(series.Id);

        Assert.False(result.Success);
        Assert.Contains("No issues", result.Error);
    }

    [Fact]
    public async Task FetchSeriesVariantCoversAsync_ProcessesAllIssues()
    {
        var series = new Series { Title = "Test Series" };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue1 = new Issue { SeriesId = series.Id, IssueNumber = 1, ComicVineId = 100 };
        var issue2 = new Issue { SeriesId = series.Id, IssueNumber = 2, ComicVineId = 101 };
        _context.Issues.AddRange(issue1, issue2);
        await _context.SaveChangesAsync();

        _mockComicVineClient.Setup(c => c.GetIssueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new ComicVineResult<ComicVineIssue>
            {
                Success = true,
                Data = new ComicVineIssue
                {
                    Id = id,
                    IssueNumber = "1",
                    Image = new ComicVineImage { OriginalUrl = $"http://example.com/{id}.jpg" },
                    AssociatedImages = new List<ComicVineAssociatedImage>()
                }
            });

        var result = await _service.FetchSeriesVariantCoversAsync(series.Id);

        Assert.True(result.Success);
        Assert.Equal(2, result.IssuesProcessed);
    }

    #endregion
}
