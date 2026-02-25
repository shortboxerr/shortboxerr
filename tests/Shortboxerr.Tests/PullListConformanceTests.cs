using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.WalkSoftly;
using Shortboxerr.Infrastructure.Caching;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.PullList;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Conformance tests for Pull List calendar generation and status calculations.
/// Tests cover EPIC 11.7 acceptance criteria.
/// </summary>
public class PullListConformanceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly PullListService _service;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<IWalkSoftlyClient> _mockWalkSoftlyClient;
    private readonly Mock<IMetronClient> _mockMetronClient;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly Mock<ICoverService> _mockCoverService;
    private readonly ICacheService _cacheService;
    private readonly Mock<ILogger<PullListService>> _mockLogger;

    public PullListConformanceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockWalkSoftlyClient = new Mock<IWalkSoftlyClient>();
        _mockMetronClient = new Mock<IMetronClient>();
        _mockCoverService = new Mock<ICoverService>();
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _cacheService = new CacheService(
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<CacheService>>().Object,
            Options.Create(new CacheSettings()));
        _mockLogger = new Mock<ILogger<PullListService>>();
        
        // Default WalkSoftly setup - returns empty result
        _mockWalkSoftlyClient
            .Setup(x => x.GetWeeklyReleasesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WalkSoftlyResult { Success = false, Error = "Mock - not configured" });
        
        _service = new PullListService(
            _dbContext, 
            _mockSettingsService.Object, 
            _mockComicVineClient.Object,
            _mockWalkSoftlyClient.Object,
            _mockMetronClient.Object,
            _mockSeriesMetadataService.Object, 
            _cacheService,
            _mockCoverService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Week Boundary Calculations

    [Theory]
    [InlineData("2026-02-01", "2026-02-01", "2026-02-08")] // Sunday -> same Sunday to next Sunday (exclusive)
    [InlineData("2026-02-02", "2026-02-01", "2026-02-08")] // Monday -> previous Sunday
    [InlineData("2026-02-04", "2026-02-01", "2026-02-08")] // Wednesday -> previous Sunday
    [InlineData("2026-02-07", "2026-02-01", "2026-02-08")] // Saturday -> same week
    [InlineData("2026-02-08", "2026-02-08", "2026-02-15")] // Next Sunday -> new week
    public async Task WeekBoundaries_CalculatedCorrectly(string inputDate, string expectedStart, string expectedEnd)
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var date = DateTime.Parse(inputDate);
        var expStart = DateTime.Parse(expectedStart);
        var expEnd = DateTime.Parse(expectedEnd);

        // Act
        var result = await _service.GetWeeklyReleasesAsync(date);

        // Assert
        Assert.Equal(expStart, result.WeekStart);
        Assert.Equal(expEnd, result.WeekEnd);
    }

    [Fact]
    public async Task WeekBoundaries_ReleaseDayIsWednesday()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(DateTime.Today);

        // Assert
        Assert.Equal(DayOfWeek.Wednesday, result.ReleaseDay.DayOfWeek);
    }

    [Fact]
    public async Task WeekBoundaries_SpansSevenDays()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(DateTime.Today);

        // Assert
        Assert.Equal(7, (result.WeekEnd - result.WeekStart).Days);
    }

    #endregion

    #region Release Date Grouping

    [Fact]
    public async Task ReleaseGrouping_GroupsByStoreDate()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var startDate = new DateTime(2026, 2, 1);
        var endDate = new DateTime(2026, 2, 8);

        // Add issues on different days
        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = startDate.AddDays(0), Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = startDate.AddDays(0), Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 3, StoreDate = startDate.AddDays(2), Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 4, StoreDate = startDate.AddDays(4), Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var calendar = await _service.GetCalendarAsync(startDate, endDate);

        // Assert
        Assert.Equal(7, calendar.Days.Count);
        
        var day0 = calendar.Days.First(d => d.Date == startDate.AddDays(0));
        Assert.Equal(2, day0.Issues.Count);
        
        var day2 = calendar.Days.First(d => d.Date == startDate.AddDays(2));
        Assert.Single(day2.Issues);
        
        var day4 = calendar.Days.First(d => d.Date == startDate.AddDays(4));
        Assert.Single(day4.Issues);
    }

    [Fact]
    public async Task ReleaseGrouping_GroupsByPublisher()
    {
        // Arrange
        var series1 = new Series { Title = "DC Series", Publisher = "DC", Monitored = true };
        var series2 = new Series { Title = "Marvel Series", Publisher = "Marvel", Monitored = true };
        _dbContext.Series.AddRange(series1, series2);
        await _dbContext.SaveChangesAsync();

        var releaseDate = new DateTime(2026, 2, 4); // Wednesday

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series1.Id, IssueNumber = 1, StoreDate = releaseDate, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 2, StoreDate = releaseDate, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series2.Id, IssueNumber = 1, StoreDate = releaseDate, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var calendar = await _service.GetCalendarAsync(releaseDate, releaseDate.AddDays(1));

        // Assert
        Assert.Equal(2, calendar.ByPublisher.Count);
        Assert.Equal(2, calendar.ByPublisher["DC"].Count);
        Assert.Single(calendar.ByPublisher["Marvel"]);
    }

    [Fact]
    public async Task ReleaseGrouping_GroupsBySeries()
    {
        // Arrange
        var series1 = new Series { Title = "Batman", Monitored = true };
        var series2 = new Series { Title = "X-Men", Monitored = true };
        _dbContext.Series.AddRange(series1, series2);
        await _dbContext.SaveChangesAsync();

        var releaseDate = new DateTime(2026, 2, 4);

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series1.Id, IssueNumber = 1, StoreDate = releaseDate, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 2, StoreDate = releaseDate, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series2.Id, IssueNumber = 1, StoreDate = releaseDate, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var calendar = await _service.GetCalendarAsync(releaseDate, releaseDate.AddDays(1));

        // Assert
        Assert.Equal(2, calendar.BySeries.Count);
        Assert.Equal(2, calendar.BySeries[series1.Id].Count);
        Assert.Single(calendar.BySeries[series2.Id]);
    }

    [Fact]
    public async Task ReleaseGrouping_MarksReleaseDayCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var startDate = new DateTime(2026, 2, 1); // Sunday
        var endDate = new DateTime(2026, 2, 8);   // Saturday + 1

        // Act
        var calendar = await _service.GetCalendarAsync(startDate, endDate);

        // Assert
        var wednesday = calendar.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Wednesday);
        Assert.True(wednesday.IsReleaseDay);

        var otherDays = calendar.Days.Where(d => d.Date.DayOfWeek != DayOfWeek.Wednesday);
        Assert.All(otherDays, d => Assert.False(d.IsReleaseDay));
    }

    #endregion

    #region Status Calculations

    [Fact]
    public async Task StatusCalculation_CountsWantedCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 3, StoreDate = wednesday, Status = IssueStatus.Owned }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal(2, result.WantedCount);
    }

    [Fact]
    public async Task StatusCalculation_CountsOwnedCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Owned },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Owned },
            new Issue { SeriesId = series.Id, IssueNumber = 3, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal(2, result.OwnedCount);
    }

    [Fact]
    public async Task StatusCalculation_CountsSkippedCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Skipped },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task StatusCalculation_TotalCountIncludesAll()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Owned },
            new Issue { SeriesId = series.Id, IssueNumber = 3, StoreDate = wednesday, Status = IssueStatus.Skipped },
            new Issue { SeriesId = series.Id, IssueNumber = 4, StoreDate = wednesday, Status = IssueStatus.Missing }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task StatusCalculation_MissedIssuesCountedInStats()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var pastDate = DateTime.Today.AddDays(-14); // 2 weeks ago

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = pastDate, Status = IssueStatus.Wanted }, // Missed
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = pastDate, Status = IssueStatus.Owned }  // Got it
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var stats = await _service.GetStatsAsync();

        // Assert
        Assert.Equal(1, stats.MissedIssues);
    }

    #endregion

    #region Filtering

    [Fact]
    public async Task Filtering_ByPublisher_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        var dcSeries = new Series { Title = "Batman", Publisher = "DC", Monitored = true };
        var marvelSeries = new Series { Title = "X-Men", Publisher = "Marvel", Monitored = true };
        _dbContext.Series.AddRange(dcSeries, marvelSeries);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = dcSeries.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = marvelSeries.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { Publishers = new List<string> { "DC" } };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.Equal("Batman", result.Issues[0].SeriesTitle);
    }

    [Fact]
    public async Task Filtering_ByStatus_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Owned },
            new Issue { SeriesId = series.Id, IssueNumber = 3, StoreDate = wednesday, Status = IssueStatus.Skipped }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { Statuses = new List<IssueStatus> { IssueStatus.Wanted } };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.Equal(IssueStatus.Wanted, result.Issues[0].Status);
    }

    [Fact]
    public async Task Filtering_ExcludeAnnuals_FiltersCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted, IsAnnual = false },
            new Issue { SeriesId = series.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Wanted, IsAnnual = true }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { IncludeAnnuals = false };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.False(result.Issues[0].IsAnnual);
    }

    [Fact]
    public async Task Filtering_ExcludeSpecials_FiltersCorrectly()
    {
        // Arrange
        var series = new Series { Title = "Test", Monitored = true };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted, IsSpecial = false },
            new Issue { SeriesId = series.Id, IssueNumber = 0, StoreDate = wednesday, Status = IssueStatus.Wanted, IsSpecial = true, SpecialType = "One-Shot" }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { IncludeSpecials = false };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.False(result.Issues[0].IsSpecial);
    }

    [Fact]
    public async Task Filtering_MonitoredOnly_ExcludesUnmonitoredSeries()
    {
        // Arrange
        var monitoredSeries = new Series { Title = "Monitored", Monitored = true };
        var unmonitoredSeries = new Series { Title = "Not Monitored", Monitored = false };
        _dbContext.Series.AddRange(monitoredSeries, unmonitoredSeries);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = monitoredSeries.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = unmonitoredSeries.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        var filter = new PullListFilter { MonitoredOnly = true };

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday, filter);

        // Assert
        Assert.Single(result.Issues);
        Assert.Equal("Monitored", result.Issues[0].SeriesTitle);
    }

    #endregion

    #region Multi-Series Pull List

    [Fact]
    public async Task MultiSeries_ReturnsIssuesFromAllSeries()
    {
        // Arrange
        var series1 = new Series { Title = "Batman", Publisher = "DC", Monitored = true };
        var series2 = new Series { Title = "Spider-Man", Publisher = "Marvel", Monitored = true };
        var series3 = new Series { Title = "Invincible", Publisher = "Image", Monitored = true };
        _dbContext.Series.AddRange(series1, series2, series3);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series1.Id, IssueNumber = 100, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series2.Id, IssueNumber = 50, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series3.Id, IssueNumber = 144, StoreDate = wednesday, Status = IssueStatus.Owned }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Contains(result.Issues, i => i.SeriesTitle == "Batman");
        Assert.Contains(result.Issues, i => i.SeriesTitle == "Spider-Man");
        Assert.Contains(result.Issues, i => i.SeriesTitle == "Invincible");
    }

    [Fact]
    public async Task MultiSeries_OrdersByStoreDateThenSeriesThenIssueNumber()
    {
        // Arrange
        var series1 = new Series { Title = "AAA Series", Monitored = true };
        var series2 = new Series { Title = "BBB Series", Monitored = true };
        _dbContext.Series.AddRange(series1, series2);
        await _dbContext.SaveChangesAsync();

        var wednesday = GetNextWednesday();

        _dbContext.Issues.AddRange(
            new Issue { SeriesId = series2.Id, IssueNumber = 3, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 2, StoreDate = wednesday, Status = IssueStatus.Wanted },
            new Issue { SeriesId = series1.Id, IssueNumber = 1, StoreDate = wednesday, Status = IssueStatus.Wanted }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetWeeklyReleasesAsync(wednesday);

        // Assert
        Assert.Equal("AAA Series", result.Issues[0].SeriesTitle);
        Assert.Equal(1, result.Issues[0].IssueNumber);
        Assert.Equal("AAA Series", result.Issues[1].SeriesTitle);
        Assert.Equal(2, result.Issues[1].IssueNumber);
        Assert.Equal("BBB Series", result.Issues[2].SeriesTitle);
    }

    #endregion

    #region Helpers

    private static DateTime GetNextWednesday()
    {
        var today = DateTime.Today;
        var daysUntilWednesday = ((int)DayOfWeek.Wednesday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilWednesday == 0) daysUntilWednesday = 7;
        return today.AddDays(daysUntilWednesday);
    }

    #endregion
}
