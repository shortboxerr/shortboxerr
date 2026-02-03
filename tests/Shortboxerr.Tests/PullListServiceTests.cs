using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.PullList;
using Xunit;

namespace Shortboxerr.Tests;

public class PullListServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly PullListService _service;
    private readonly Mock<ILogger<PullListService>> _mockLogger;

    public PullListServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockLogger = new Mock<ILogger<PullListService>>();
        _service = new PullListService(_dbContext, _mockLogger.Object);
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
}
