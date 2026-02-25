using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
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

    private static (Mock<IDdlDownloadService>, Mock<IDdlImportService>, Mock<IActivityService>, Mock<IDecisionEngine>) CreateMockDependencies()
    {
        var mockDownload = new Mock<IDdlDownloadService>();
        var mockImport = new Mock<IDdlImportService>();
        var mockActivity = new Mock<IActivityService>();
        var mockDecision = new Mock<IDecisionEngine>();
        
        // Default behavior: no auto-grab
        mockDecision.Setup(d => d.EvaluateAndRank(It.IsAny<IEnumerable<Candidate>>(), It.IsAny<CandidateTarget>()))
            .Returns(Array.Empty<CandidateEvaluation>());
        mockDecision.Setup(d => d.CheckAutoGrab(It.IsAny<IReadOnlyList<CandidateEvaluation>>()))
            .Returns((false, "No candidates"));
        
        return (mockDownload, mockImport, mockActivity, mockDecision);
    }

    [Fact]
    public async Task SearchIssueAsync_WhenIssueNotFound_ReturnsFailedResult()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var mockDdlSearch = new Mock<IDdlSearchService>();
        var (mockDownload, mockImport, mockActivity, mockDecision) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload.Object, mockImport.Object, mockActivity.Object, mockDecision.Object, mockSettings.Object, mockLogger.Object);
        
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
        
        var (mockDownload2, mockImport2, mockActivity2, mockDecision2) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        mockSettings.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload2.Object, mockImport2.Object, mockActivity2.Object, mockDecision2.Object, mockSettings.Object, mockLogger.Object);
        
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
        
        var (mockDownload3, mockImport3, mockActivity3, mockDecision3) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload3.Object, mockImport3.Object, mockActivity3.Object, mockDecision3.Object, mockSettings.Object, mockLogger.Object);
        
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
        
        var (mockDownload4, mockImport4, mockActivity4, mockDecision4) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings());
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload4.Object, mockImport4.Object, mockActivity4.Object, mockDecision4.Object, mockSettings.Object, mockLogger.Object);
        
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
        var (mockDownload5, mockImport5, mockActivity5, mockDecision5) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { StaleSearchThresholdDays = 7 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload5.Object, mockImport5.Object, mockActivity5.Object, mockDecision5.Object, mockSettings.Object, mockLogger.Object);
        
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
        var (mockDownload6, mockImport6, mockActivity6, mockDecision6) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { StaleSearchThresholdDays = 7 }); // Re-search after 7 days
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload6.Object, mockImport6.Object, mockActivity6.Object, mockDecision6.Object, mockSettings.Object, mockLogger.Object);
        
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
        var (mockDownload7, mockImport7, mockActivity7, mockDecision7) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { AutoSearchEnabled = true, AutoSearchIntervalHours = 24 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload7.Object, mockImport7.Object, mockActivity7.Object, mockDecision7.Object, mockSettings.Object, mockLogger.Object);
        
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
        
        var (mockDownload8, mockImport8, mockActivity8, mockDecision8) = CreateMockDependencies();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetAsync<SearchSettings>(It.IsAny<string>(), It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchSettings { SearchDelaySeconds = 0, StaleSearchThresholdDays = 7 });
        
        var mockLogger = new Mock<ILogger<AutoSearchService>>();
        
        var service = new AutoSearchService(context, mockDdlSearch.Object, mockDownload8.Object, mockImport8.Object, mockActivity8.Object, mockDecision8.Object, mockSettings.Object, mockLogger.Object);
        
        // Act
        var result = await service.SearchAllWantedAsync(maxIssues: 10);
        
        // Assert
        Assert.Equal(2, result.TotalSearched);
        Assert.Equal(2, result.Results.Count);
        mockDdlSearch.Verify(s => s.SearchAllAsync(It.IsAny<DdlSearchQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
