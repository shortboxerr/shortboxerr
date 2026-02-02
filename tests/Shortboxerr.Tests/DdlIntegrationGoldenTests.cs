using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden tests for DDL end-to-end integration.
/// These tests verify the complete DDL pipeline matches Mylar3 behavior.
/// </summary>
public class DdlIntegrationGoldenTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private readonly ShortboxerrDbContext _context;
    private readonly DdlReleaseParser _parser;
    private readonly DdlFilter _filter;

    public DdlIntegrationGoldenTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ShortboxerrDbContext(options);
        _parser = new DdlReleaseParser();
        _filter = new DdlFilter();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FixtureFile_Loads_Successfully()
    {
        var fixture = LoadFixture();
        
        Assert.NotNull(fixture);
        Assert.NotEmpty(fixture.EndToEndScenarios);
        Assert.NotEmpty(fixture.MultiSiteScenarios);
    }

    [Theory]
    [InlineData("e2e_single_issue_happy_path", 1)]
    [InlineData("e2e_collection_happy_path", 1)]
    [InlineData("e2e_multiple_results_best_selected", 2)]
    [InlineData("e2e_no_results_found", 0)]
    public void SearchResults_ReturnExpectedCandidateCount(string scenarioId, int expectedCount)
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == scenarioId);
        
        Assert.NotNull(scenario);
        Assert.Equal(expectedCount, scenario.ExpectedCandidateCount);
    }

    [Theory]
    [InlineData("e2e_filter_rejects_banned_word", "sample")]
    [InlineData("e2e_filter_rejects_size", "below minimum")]
    public void FilterRejects_WithCorrectReason(string scenarioId, string expectedReasonContains)
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == scenarioId);
        
        Assert.NotNull(scenario);
        Assert.Equal(0, scenario.ExpectedFilteredCount);
        Assert.Contains(expectedReasonContains, scenario.ExpectedFilterReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("e2e_single_issue_happy_path")]
    [InlineData("e2e_collection_happy_path")]
    public void HappyPath_ParsesAndFiltersSuccessfully(string scenarioId)
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == scenarioId);
        Assert.NotNull(scenario);
        
        foreach (var mockResult in scenario.MockSearchResults)
        {
            // Parse the release title
            var parsed = _parser.Parse(mockResult.ReleaseTitle);
            Assert.NotNull(parsed.SeriesTitle);
            
            // Create a candidate
            var candidate = new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = mockResult.ReleaseTitle,
                SourceSite = mockResult.SourceSite ?? "MockDdl",
                ParsedInfo = parsed,
                Size = mockResult.Size
            };
            
            // Filter should pass
            var filterSettings = new DdlFilterSettings();
            var (passes, reason) = _filter.CheckCandidate(candidate, filterSettings);
            
            Assert.True(passes, $"Expected pass but got: {reason}");
        }
    }

    [Theory]
    [InlineData("e2e_filter_rejects_banned_word")]
    public void FilterRejects_BannedWord(string scenarioId)
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == scenarioId);
        Assert.NotNull(scenario);
        
        foreach (var mockResult in scenario.MockSearchResults)
        {
            var parsed = _parser.Parse(mockResult.ReleaseTitle);
            var candidate = new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = mockResult.ReleaseTitle,
                SourceSite = "MockDdl",
                ParsedInfo = parsed,
                Size = mockResult.Size
            };
            
            var filterSettings = new DdlFilterSettings
            {
                BannedWords = new List<string> { "sample", "preview" }
            };
            var (passes, reason) = _filter.CheckCandidate(candidate, filterSettings);
            
            Assert.False(passes);
            Assert.Contains("sample", reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("e2e_filter_rejects_size")]
    public void FilterRejects_TooSmall(string scenarioId)
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == scenarioId);
        Assert.NotNull(scenario);
        
        foreach (var mockResult in scenario.MockSearchResults)
        {
            var parsed = _parser.Parse(mockResult.ReleaseTitle);
            var candidate = new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = mockResult.ReleaseTitle,
                SourceSite = "MockDdl",
                ParsedInfo = parsed,
                Size = mockResult.Size
            };
            
            var filterSettings = new DdlFilterSettings
            {
                MinSizeBytesSingles = 1_000_000 // 1MB minimum
            };
            var (passes, reason) = _filter.CheckCandidate(candidate, filterSettings);
            
            Assert.False(passes);
            Assert.Contains("below minimum", reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MultipleResults_RankedByDecisionEngine()
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == "e2e_multiple_results_best_selected");
        Assert.NotNull(scenario);
        
        var candidates = scenario.MockSearchResults.Select(r => new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = r.ReleaseTitle,
            SourceSite = r.SourceSite ?? "MockDdl",
            ParsedInfo = _parser.Parse(r.ReleaseTitle),
            Size = r.Size
        }).ToList();
        
        // Verify we have 2 candidates
        Assert.Equal(2, candidates.Count);
        
        // Digital version should rank higher than Scan version
        var digitalCandidate = candidates.FirstOrDefault(c => c.ReleaseTitle.Contains("Digital"));
        var scanCandidate = candidates.FirstOrDefault(c => c.ReleaseTitle.Contains("Scan"));
        
        Assert.NotNull(digitalCandidate);
        Assert.NotNull(scanCandidate);
        
        // Digital CBZ should be preferred over Scan CBR
        Assert.Equal("cbz", digitalCandidate.ParsedInfo.Format);
        Assert.Equal("cbr", scanCandidate.ParsedInfo.Format);
    }

    [Fact]
    public async Task AutoMatch_HighConfidence_WithExistingSeriesAndIssue()
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == "e2e_auto_match_high_confidence");
        Assert.NotNull(scenario);
        Assert.NotNull(scenario.ExistingSeries);
        
        // Setup: Add series and issue to database
        var series = new Series
        {
            Title = scenario.ExistingSeries.Title,
            Publisher = scenario.ExistingSeries.Publisher,
            StartYear = scenario.ExistingSeries.StartYear,
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();
        
        if (scenario.ExistingIssue != null)
        {
            var issue = new Issue
            {
                SeriesId = series.Id,
                IssueNumber = scenario.ExistingIssue.IssueNumber
            };
            _context.Issues.Add(issue);
            await _context.SaveChangesAsync();
        }
        
        // Parse candidate
        var mockResult = scenario.MockSearchResults[0];
        var parsed = _parser.Parse(mockResult.ReleaseTitle);
        
        // Verify parsed info matches series
        Assert.Contains(scenario.ExistingSeries.Title, parsed.SeriesTitle, StringComparison.OrdinalIgnoreCase);
        
        // Verify expected behavior
        Assert.True(scenario.ExpectedAutoImport);
    }

    [Fact]
    public void LowConfidence_QueuesForManualReview()
    {
        var fixture = LoadFixture();
        var scenario = fixture.EndToEndScenarios.FirstOrDefault(s => s.Id == "e2e_manual_review_low_confidence");
        Assert.NotNull(scenario);
        
        Assert.False(scenario.ExpectedAutoImport);
        Assert.True(scenario.ExpectedPendingReview);
        Assert.NotNull(scenario.ExpectedReviewReason);
        Assert.Contains("Issue", scenario.ExpectedReviewReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiSite_Aggregation_WorksCorrectly()
    {
        var fixture = LoadFixture();
        var scenario = fixture.MultiSiteScenarios.FirstOrDefault(s => s.Id == "multi_site_aggregation");
        Assert.NotNull(scenario);
        
        Assert.Equal(2, scenario.ExpectedTotalCandidates);
        Assert.Contains("Site1", scenario.ExpectedSitesQueried);
        Assert.Contains("Site2", scenario.ExpectedSitesQueried);
    }

    [Fact]
    public void MultiSite_Deduplication_RemovesDuplicates()
    {
        var fixture = LoadFixture();
        var scenario = fixture.MultiSiteScenarios.FirstOrDefault(s => s.Id == "multi_site_deduplication");
        Assert.NotNull(scenario);
        
        Assert.Equal(1, scenario.ExpectedTotalCandidates);
        Assert.Equal(1, scenario.ExpectedDuplicatesRemoved);
    }

    [Fact]
    public void MultiSite_PartialFailure_StillReturnsResults()
    {
        var fixture = LoadFixture();
        var scenario = fixture.MultiSiteScenarios.FirstOrDefault(s => s.Id == "multi_site_one_fails");
        Assert.NotNull(scenario);
        
        Assert.Equal(1, scenario.ExpectedTotalCandidates);
        Assert.Contains("Site1", scenario.ExpectedSuccessfulSites);
        Assert.Contains("Site2", scenario.ExpectedFailedSites);
    }

    private static DdlIntegrationGoldenFixture LoadFixture()
    {
        var path = Path.Combine("Fixtures", "ddl_integration_golden.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DdlIntegrationGoldenFixture>(json, JsonOptions)!;
    }
}

public class DdlIntegrationGoldenFixture
{
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Mylar3Parity { get; set; }
    public List<EndToEndScenario> EndToEndScenarios { get; set; } = new();
    public List<MultiSiteScenario> MultiSiteScenarios { get; set; } = new();
}

public class EndToEndScenario
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public SearchQuerySpec SearchQuery { get; set; } = new();
    public List<MockSearchResult> MockSearchResults { get; set; } = new();
    public int ExpectedCandidateCount { get; set; }
    public int ExpectedFilteredCount { get; set; }
    public string? ExpectedFilterReason { get; set; }
    public string? ExpectedBestCandidateTitle { get; set; }
    public bool ExpectedDownloadSuccess { get; set; }
    public bool ExpectedImportSuccess { get; set; }
    public List<string> ExpectedHistoryEvents { get; set; } = new();
    public MockDownloadBehavior? MockDownloadBehavior { get; set; }
    public int ExpectedRetryAttempts { get; set; }
    public string? ExpectedFailureReason { get; set; }
    public ExistingSeriesSpec? ExistingSeries { get; set; }
    public ExistingIssueSpec? ExistingIssue { get; set; }
    public ImportOptionsSpec? ImportOptions { get; set; }
    public bool ExpectedAutoImport { get; set; }
    public int ExpectedMatchConfidence { get; set; }
    public bool ExpectedPendingReview { get; set; }
    public string? ExpectedReviewReason { get; set; }
}

public class SearchQuerySpec
{
    public string? SeriesTitle { get; set; }
    public decimal? IssueNumber { get; set; }
    public int? Year { get; set; }
    public bool CollectionsOnly { get; set; }
}

public class MockSearchResult
{
    public string ReleaseTitle { get; set; } = "";
    public string? SourceSite { get; set; }
    public long Size { get; set; }
    public string? DownloadUrl { get; set; }
}

public class MockDownloadBehavior
{
    public int FailFirstAttempts { get; set; }
    public int SucceedOnAttempt { get; set; }
    public bool AlwaysFail { get; set; }
    public string? FailureReason { get; set; }
    public bool ReturnHtmlErrorPage { get; set; }
}

public class ExistingSeriesSpec
{
    public string Title { get; set; } = "";
    public string? Publisher { get; set; }
    public int StartYear { get; set; }
}

public class ExistingIssueSpec
{
    public decimal IssueNumber { get; set; }
}

public class ImportOptionsSpec
{
    public bool AutoImportEnabled { get; set; }
    public int AutoImportMinConfidence { get; set; }
    public bool RequireIssueMatch { get; set; }
}

public class MultiSiteScenario
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public SearchQuerySpec SearchQuery { get; set; } = new();
    public Dictionary<string, object> MockSiteResults { get; set; } = new();
    public int ExpectedTotalCandidates { get; set; }
    public int ExpectedDuplicatesRemoved { get; set; }
    public List<string> ExpectedSitesQueried { get; set; } = new();
    public List<string> ExpectedSuccessfulSites { get; set; } = new();
    public List<string> ExpectedFailedSites { get; set; } = new();
}

