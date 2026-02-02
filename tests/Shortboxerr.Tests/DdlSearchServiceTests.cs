using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DDL search service functionality.
/// </summary>
public class DdlSearchServiceTests
{
    private readonly IDdlSearchService _searchService;
    private readonly IDdlSiteAdapterFactory _adapterFactory;

    public DdlSearchServiceTests()
    {
        _adapterFactory = new DdlSiteAdapterFactory();
        _searchService = new DdlSearchService(_adapterFactory, new DdlReleaseParser());
    }

    #region SearchAllAsync Tests

    [Fact]
    public async Task SearchAllAsync_WithSeriesQuery_ReturnsMatchingCandidates()
    {
        var query = new DdlSearchQuery
        {
            SeriesTitle = "Spider-Man",
            Limit = 10
        };
        
        var result = await _searchService.SearchAllAsync(query);
        
        Assert.NotNull(result);
        Assert.True(result.AllCandidates.Count > 0);
        Assert.All(result.AllCandidates, c => 
            Assert.Contains("spider-man", c.ReleaseTitle.ToLowerInvariant()));
    }

    [Fact]
    public async Task SearchAllAsync_ReturnsAggregatedResults()
    {
        var query = new DdlSearchQuery
        {
            Limit = 20
        };
        
        var result = await _searchService.SearchAllAsync(query);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.SuccessfulSites);
        Assert.NotNull(result.ResultsBySite);
        Assert.True(result.TotalDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task SearchAllAsync_WithIssueFilter_ReturnsExactMatch()
    {
        var query = new DdlSearchQuery
        {
            SeriesTitle = "Batman",
            IssueNumber = 150,
            Limit = 10
        };
        
        var result = await _searchService.SearchAllAsync(query);
        
        Assert.NotNull(result);
        Assert.All(result.AllCandidates, c => 
            Assert.Equal(150, c.ParsedInfo.IssueNumber));
    }

    [Fact]
    public async Task SearchAllAsync_WithYearFilter_FiltersCorrectly()
    {
        var query = new DdlSearchQuery
        {
            Year = 2023,
            Limit = 10
        };
        
        var result = await _searchService.SearchAllAsync(query);
        
        Assert.NotNull(result);
        Assert.All(result.AllCandidates, c => 
            Assert.Equal(2023, c.ParsedInfo.Year));
    }

    [Fact]
    public async Task SearchAllAsync_CollectionsOnly_FiltersCollections()
    {
        var query = new DdlSearchQuery
        {
            CollectionsOnly = true,
            Limit = 20
        };
        
        var result = await _searchService.SearchAllAsync(query);
        
        Assert.NotNull(result);
        Assert.All(result.AllCandidates, c => 
            Assert.True(c.ParsedInfo.IsCollection));
    }

    #endregion

    #region SearchSiteAsync Tests

    [Fact]
    public async Task SearchSiteAsync_WithValidSite_ReturnsResults()
    {
        var query = new DdlSearchQuery { Limit = 5 };
        
        var result = await _searchService.SearchSiteAsync("MockDdl", query);
        
        Assert.True(result.Success);
        Assert.NotEmpty(result.Candidates);
        Assert.Equal("MockDdl", result.SourceSite);
    }

    [Fact]
    public async Task SearchSiteAsync_WithInvalidSite_ReturnsError()
    {
        var query = new DdlSearchQuery { Limit = 5 };
        
        var result = await _searchService.SearchSiteAsync("NonExistent", query);
        
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region GetLatestFromAllAsync Tests

    [Fact]
    public async Task GetLatestFromAllAsync_ReturnsRecentReleases()
    {
        var result = await _searchService.GetLatestFromAllAsync(10);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.AllCandidates);
        
        // Should be sorted by date (newest first)
        var dates = result.AllCandidates.Select(c => c.DateFound).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    #endregion

    #region GetAvailableSites Tests

    [Fact]
    public void GetAvailableSites_ReturnsRegisteredSites()
    {
        var sites = _searchService.GetAvailableSites();
        
        Assert.NotEmpty(sites);
        Assert.Contains(sites, s => s.SiteType == "MockDdl");
    }

    [Fact]
    public void GetAvailableSites_ContainsSiteInfo()
    {
        var sites = _searchService.GetAvailableSites();
        var mockSite = sites.First(s => s.SiteType == "MockDdl");
        
        Assert.NotNull(mockSite.DisplayName);
        Assert.NotNull(mockSite.DefaultBaseUrl);
        Assert.True(mockSite.DefaultRateLimitPerMinute > 0);
    }

    #endregion

    #region TestSiteAsync Tests

    [Fact]
    public async Task TestSiteAsync_WithMockSite_ReturnsSuccess()
    {
        var result = await _searchService.TestSiteAsync("MockDdl");
        
        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        Assert.True(result.SampleResultCount > 0);
    }

    [Fact]
    public async Task TestSiteAsync_WithInvalidSite_ReturnsFailure()
    {
        var result = await _searchService.TestSiteAsync("NonExistent");
        
        Assert.False(result.Success);
    }

    #endregion

    #region VerifyLinkAsync Tests

    [Fact]
    public async Task VerifyLinkAsync_WithMockLink_ReturnsTrue()
    {
        var url = "https://mock.ddl.local/download/test.cbz";
        
        var result = await _searchService.VerifyLinkAsync(url);
        
        // Mock always returns true for verification
        Assert.True(result);
    }

    #endregion
}

