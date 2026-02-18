using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Search;

namespace Shortboxerr.Tests;

public class AutoSearchServiceTests
{
    private ShortboxerrDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ShortboxerrDbContext(options);
    }

    [Fact]
    public async Task SearchIssueAsync_WhenIssueNotFound_ReturnsFailedResult()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockDdlSearch = new Mock<IDdlSearchService>();
        var mockSettings = new Mock<ISettingsService>();
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var result = await service.SearchIssueAsync(999);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal("Issue not found", result.Error);
        Assert.Equal(999, result.IssueId);
    }

    [Fact]
    public async Task SearchIssueAsync_WhenCandidatesFound_ReturnsSuccessResult()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Batman", Monitored = true };
        var issue = new Issue 
        { 
            Id = 1, 
            SeriesId = 1, 
            Series = series,
            IssueNumber = 100,
            IssueNumberText = "100",
            Status = IssueStatus.Wanted
        };
        context.Series.Add(series);
        context.Issues.Add(issue);
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        mockDdlSearch.Setup(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlAggregatedSearchResult
            {
                AllCandidates = new List<DdlCandidate>
                {
                    new DdlCandidate 
                    { 
                        Id = Guid.NewGuid().ToString(),
                        ReleaseTitle = "Batman 100 (2024)",
                        SourceSite = "TestSite",
                        ParsedInfo = new DdlParsedInfo { SeriesTitle = "Batman", IssueNumber = 100 },
                        DownloadLinks = new List<DdlDownloadLink>()
                    }
                },
                ResultsBySite = new Dictionary<string, DdlSearchResult>(),
                SuccessfulSites = new List<string> { "TestSite" },
                FailedSites = new List<string>(),
                TotalRawCandidates = 1,
                DuplicatesRemoved = 0,
                TotalDuration = TimeSpan.FromSeconds(1),
                Warnings = new List<string>()
            });
        
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var result = await service.SearchIssueAsync(1);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.CandidatesFound);
        Assert.Equal("Batman 100 (2024)", result.SelectedCandidateTitle);
        Assert.Equal("Batman", result.SeriesTitle);
        Assert.Equal("100", result.IssueNumber);
    }

    [Fact]
    public async Task SearchIssueAsync_WhenNoCandidatesFound_ReturnsNotFoundResult()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Obscure Comic", Monitored = true };
        var issue = new Issue 
        { 
            Id = 1, 
            SeriesId = 1, 
            Series = series,
            IssueNumber = 1,
            Status = IssueStatus.Wanted
        };
        context.Series.Add(series);
        context.Issues.Add(issue);
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        mockDdlSearch.Setup(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlAggregatedSearchResult
            {
                AllCandidates = new List<DdlCandidate>(),
                ResultsBySite = new Dictionary<string, DdlSearchResult>(),
                SuccessfulSites = new List<string>(),
                FailedSites = new List<string>(),
                TotalRawCandidates = 0,
                DuplicatesRemoved = 0,
                TotalDuration = TimeSpan.FromSeconds(1),
                Warnings = new List<string>()
            });
        
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var result = await service.SearchIssueAsync(1);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.CandidatesFound);
    }

    [Fact]
    public async Task SearchIssueAsync_UpdatesLastSearchedAtAndAttempts()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Test Comic", Monitored = true };
        var issue = new Issue 
        { 
            Id = 1, 
            SeriesId = 1, 
            Series = series,
            IssueNumber = 1,
            Status = IssueStatus.Wanted,
            SearchAttempts = 0,
            LastSearchedAt = null
        };
        context.Series.Add(series);
        context.Issues.Add(issue);
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        mockDdlSearch.Setup(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlAggregatedSearchResult
            {
                AllCandidates = new List<DdlCandidate>(),
                ResultsBySite = new Dictionary<string, DdlSearchResult>(),
                SuccessfulSites = new List<string>(),
                FailedSites = new List<string>(),
                TotalRawCandidates = 0,
                DuplicatesRemoved = 0,
                TotalDuration = TimeSpan.FromSeconds(1),
                Warnings = new List<string>()
            });
        
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        await service.SearchIssueAsync(1);
        
        // Assert
        var updatedIssue = await context.Issues.FindAsync(1);
        Assert.NotNull(updatedIssue!.LastSearchedAt);
        Assert.Equal(1, updatedIssue.SearchAttempts);
    }

    [Fact]
    public async Task GetSearchableIssuesAsync_ReturnsOnlyWantedMonitoredIssues()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var monitoredSeries = new Series { Id = 1, Title = "Monitored Series", Monitored = true };
        var unmonitoredSeries = new Series { Id = 2, Title = "Unmonitored Series", Monitored = false };
        
        context.Series.AddRange(monitoredSeries, unmonitoredSeries);
        
        // Wanted issue in monitored series (should be returned)
        context.Issues.Add(new Issue 
        { 
            Id = 1, SeriesId = 1, Series = monitoredSeries, IssueNumber = 1, 
            Status = IssueStatus.Wanted, LastSearchedAt = null
        });
        
        // Owned issue (should NOT be returned)
        context.Issues.Add(new Issue 
        { 
            Id = 2, SeriesId = 1, Series = monitoredSeries, IssueNumber = 2, 
            Status = IssueStatus.Owned, LastSearchedAt = null
        });
        
        // Wanted issue in unmonitored series (should NOT be returned)
        context.Issues.Add(new Issue 
        { 
            Id = 3, SeriesId = 2, Series = unmonitoredSeries, IssueNumber = 1, 
            Status = IssueStatus.Wanted, LastSearchedAt = null
        });
        
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { StaleSearchThresholdDays = 7 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var searchable = await service.GetSearchableIssuesAsync();
        
        // Assert
        Assert.Single(searchable);
        Assert.Equal(1, searchable[0].IssueId);
        Assert.Equal("Monitored Series", searchable[0].SeriesTitle);
    }

    [Fact]
    public async Task GetSearchableIssuesAsync_IncludesStaleSearchedIssues()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Test Series", Monitored = true };
        context.Series.Add(series);
        
        // Recently searched (should NOT be returned)
        context.Issues.Add(new Issue 
        { 
            Id = 1, SeriesId = 1, Series = series, IssueNumber = 1, 
            Status = IssueStatus.Wanted, 
            LastSearchedAt = DateTime.UtcNow.AddDays(-1) // 1 day ago
        });
        
        // Stale search (should be returned)
        context.Issues.Add(new Issue 
        { 
            Id = 2, SeriesId = 1, Series = series, IssueNumber = 2, 
            Status = IssueStatus.Wanted, 
            LastSearchedAt = DateTime.UtcNow.AddDays(-10) // 10 days ago
        });
        
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { StaleSearchThresholdDays = 7 }); // Re-search after 7 days
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var searchable = await service.GetSearchableIssuesAsync();
        
        // Assert
        Assert.Single(searchable);
        Assert.Equal(2, searchable[0].IssueId); // Stale issue
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectCounts()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Test Series", Monitored = true };
        context.Series.Add(series);
        
        // 3 wanted issues
        context.Issues.Add(new Issue { Id = 1, SeriesId = 1, Series = series, IssueNumber = 1, Status = IssueStatus.Wanted });
        context.Issues.Add(new Issue { Id = 2, SeriesId = 1, Series = series, IssueNumber = 2, Status = IssueStatus.Wanted });
        context.Issues.Add(new Issue { Id = 3, SeriesId = 1, Series = series, IssueNumber = 3, Status = IssueStatus.Wanted });
        // 1 owned issue
        context.Issues.Add(new Issue { Id = 4, SeriesId = 1, Series = series, IssueNumber = 4, Status = IssueStatus.Owned });
        
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { AutoSearchEnabled = true, AutoSearchIntervalHours = 24 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var status = await service.GetStatusAsync();
        
        // Assert
        Assert.True(status.Enabled);
        Assert.Equal(3, status.WantedIssuesCount);
        Assert.Equal(3, status.SearchableCount); // All wanted issues are searchable (never searched)
    }

    [Fact]
    public async Task SearchAllWantedAsync_SearchesMultipleIssues()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var series = new Series { Id = 1, Title = "Test Series", Monitored = true };
        context.Series.Add(series);
        context.Issues.Add(new Issue { Id = 1, SeriesId = 1, Series = series, IssueNumber = 1, Status = IssueStatus.Wanted });
        context.Issues.Add(new Issue { Id = 2, SeriesId = 1, Series = series, IssueNumber = 2, Status = IssueStatus.Wanted });
        await context.SaveChangesAsync();
        
        var mockDdlSearch = new Mock<IDdlSearchService>();
        mockDdlSearch.Setup(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlAggregatedSearchResult
            {
                AllCandidates = new List<DdlCandidate>(),
                ResultsBySite = new Dictionary<string, DdlSearchResult>(),
                SuccessfulSites = new List<string>(),
                FailedSites = new List<string>(),
                TotalRawCandidates = 0,
                DuplicatesRemoved = 0,
                TotalDuration = TimeSpan.FromSeconds(1),
                Warnings = new List<string>()
            });
        
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { SearchDelaySeconds = 0, StaleSearchThresholdDays = 7 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var result = await service.SearchAllWantedAsync(maxIssues: 10);
        
        // Assert
        Assert.Equal(2, result.TotalSearched);
        Assert.Equal(2, result.Results.Count);
        mockDdlSearch.Verify(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
