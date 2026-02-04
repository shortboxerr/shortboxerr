using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.ComicVine;

namespace Shortboxerr.Tests;

public class SeriesStatusDeterminerTests
{
    private readonly SeriesStatusDeterminer _determiner;

    public SeriesStatusDeterminerTests()
    {
        _determiner = new SeriesStatusDeterminer();
    }

    #region DetermineStatusFromComicVine Tests

    [Fact]
    public void DetermineStatusFromComicVine_NoIssues_ReturnsContinuing()
    {
        // Arrange
        var volumeName = "New Series";
        int? issueCount = 0;

        // Act
        var (status, source, reasons) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2024, issueCount, null, null, null);

        // Assert
        Assert.Equal(SeriesStatus.Continuing, status);
        Assert.Equal(StatusSource.ComicVine, source);
        Assert.Contains(reasons, r => r.Contains("No issues"));
    }

    [Fact]
    public void DetermineStatusFromComicVine_RecentLastIssue_ReturnsContinuing()
    {
        // Arrange
        var volumeName = "Ongoing Series";
        int issueCount = 50;
        var lastIssueDate = DateTime.UtcNow.AddMonths(-3); // 3 months ago

        // Act
        var (status, source, reasons) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2020, issueCount, null, lastIssueDate, null);

        // Assert
        Assert.Equal(SeriesStatus.Continuing, status);
        Assert.Contains(reasons, r => r.Contains("Recent activity") || r.Contains("actively publishing"));
    }

    [Fact]
    public void DetermineStatusFromComicVine_OldLastIssue_ReturnsEnded()
    {
        // Arrange
        var volumeName = "Ended Series";
        int issueCount = 100;
        var lastIssueDate = DateTime.UtcNow.AddYears(-3); // 3 years ago

        // Act
        var (status, source, reasons) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2015, issueCount, null, lastIssueDate, null);

        // Assert
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Contains(reasons, r => r.Contains("years ago"));
    }

    [Fact]
    public void DetermineStatusFromComicVine_MiniSeriesNoActivity_ReturnsEnded()
    {
        // Arrange - 6 issue mini-series with no recent activity
        var volumeName = "Mini-Series";
        int issueCount = 6;
        var lastIssueDate = DateTime.UtcNow.AddMonths(-24); // 2 years ago

        // Act
        var (status, source, reasons) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2022, issueCount, null, lastIssueDate, null);

        // Assert
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Contains(reasons, r => r.Contains("Mini-series") || r.Contains("years ago"));
    }

    [Fact]
    public void DetermineStatusFromComicVine_CustomThreshold_UsesProvidedValue()
    {
        // Arrange - 18 months ago, default threshold is 2 years but we use 1 year
        var volumeName = "Edge Case Series";
        int issueCount = 30;
        var lastIssueDate = DateTime.UtcNow.AddMonths(-18);

        // Act
        var (status, _, _) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2020, issueCount, null, lastIssueDate, null, yearsThreshold: 1);

        // Assert - Should be ended with 1 year threshold
        Assert.Equal(SeriesStatus.Ended, status);
    }

    #endregion

    #region DetermineStatus Tests (with Series entity)

    [Fact]
    public void DetermineStatus_ManuallySet_DoesNotOverride()
    {
        // Arrange
        var series = new Series
        {
            Title = "User Override Series",
            Status = SeriesStatus.Ended,
            StatusSource = StatusSource.Manual
        };

        // Act
        var (status, source, reasons) = _determiner.DetermineStatus(
            series, DateTime.UtcNow.AddMonths(-1), 50, null);

        // Assert - Should keep manual status even though last issue is recent
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Equal(StatusSource.Manual, source);
        Assert.Contains(reasons, r => r.Contains("manually set"));
    }

    [Fact]
    public void DetermineStatus_EndYearSet_ReturnsEnded()
    {
        // Arrange
        var series = new Series
        {
            Title = "Known Ended Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto,
            EndYear = 2020
        };

        // Act
        var (status, source, reasons) = _determiner.DetermineStatus(
            series, null, null, null);

        // Assert
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Contains(reasons, r => r.Contains("end year"));
    }

    [Fact]
    public void DetermineStatus_FutureEndYear_DoesNotMarkEnded()
    {
        // Arrange - End year in the future (series planned to end)
        var series = new Series
        {
            Title = "Planned Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto,
            EndYear = DateTime.UtcNow.Year + 1
        };

        // Act
        var (status, _, _) = _determiner.DetermineStatus(
            series, DateTime.UtcNow.AddMonths(-1), 12, null);

        // Assert - Should still be continuing since end year hasn't passed
        Assert.Equal(SeriesStatus.Continuing, status);
    }

    [Fact]
    public void DetermineStatus_NoDataAvailable_DefaultsToContinuing()
    {
        // Arrange
        var series = new Series
        {
            Title = "Unknown Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto
        };

        // Act
        var (status, source, reasons) = _determiner.DetermineStatus(
            series, null, null, null);

        // Assert
        Assert.Equal(SeriesStatus.Continuing, status);
        Assert.Equal(StatusSource.Auto, source);
        Assert.Contains(reasons, r => r.Contains("No definitive ended indicators"));
    }

    [Fact]
    public void DetermineStatus_IssueCountMatchesExpected_IncludesInReasons()
    {
        // Arrange - All issues present and old
        var series = new Series
        {
            Title = "Complete Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto,
            Issues = Enumerable.Range(1, 24).Select(i => new Issue { Id = i }).ToList()
        };

        // Act
        var (status, _, reasons) = _determiner.DetermineStatus(
            series, DateTime.UtcNow.AddYears(-5), 24, null);

        // Assert
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Contains(reasons, r => r.Contains("24 expected issues"));
    }

    [Fact]
    public void DetermineStatus_StaleComicVineWithMatchingCount_ReturnsEnded()
    {
        // Arrange
        var series = new Series
        {
            Title = "Stale Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto,
            Issues = Enumerable.Range(1, 50).Select(i => new Issue { Id = i }).ToList()
        };
        var comicVineLastUpdated = DateTime.UtcNow.AddYears(-4);

        // Act
        var (status, _, reasons) = _determiner.DetermineStatus(
            series, null, 50, comicVineLastUpdated);

        // Assert
        Assert.Equal(SeriesStatus.Ended, status);
        Assert.Contains(reasons, r => r.Contains("ComicVine") || r.Contains("stale"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetermineStatusFromComicVine_ExactlyAtThreshold_ReturnsContinuing()
    {
        // Arrange - Exactly at 2 year boundary
        var volumeName = "Boundary Series";
        int issueCount = 20;
        var lastIssueDate = DateTime.UtcNow.AddYears(-2).AddDays(1); // Just under 2 years

        // Act
        var (status, _, _) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2020, issueCount, null, lastIssueDate, null);

        // Assert - Should still be continuing (not quite 2 years)
        Assert.Equal(SeriesStatus.Continuing, status);
    }

    [Fact]
    public void DetermineStatusFromComicVine_LargeMiniSeries_NotTreatedAsMini()
    {
        // Arrange - 15 issues is too many for mini-series detection
        var volumeName = "Not Mini Series";
        int issueCount = 15;
        var lastIssueDate = DateTime.UtcNow.AddYears(-1);

        // Act
        var (status, _, reasons) = _determiner.DetermineStatusFromComicVine(
            volumeName, 2022, issueCount, null, lastIssueDate, null);

        // Assert - Not treated as mini-series, so should be continuing
        Assert.Equal(SeriesStatus.Continuing, status);
        Assert.DoesNotContain(reasons, r => r.Contains("Mini-series"));
    }

    [Fact]
    public void DetermineStatus_ReturnsReasonsList()
    {
        // Arrange
        var series = new Series
        {
            Title = "Test Series",
            Status = SeriesStatus.Continuing,
            StatusSource = StatusSource.Auto
        };

        // Act
        var (_, _, reasons) = _determiner.DetermineStatus(
            series, DateTime.UtcNow.AddYears(-3), 50, null);

        // Assert - Should always have at least one reason
        Assert.NotEmpty(reasons);
    }

    #endregion
}
