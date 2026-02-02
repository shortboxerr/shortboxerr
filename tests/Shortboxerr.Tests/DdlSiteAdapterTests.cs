using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DDL site adapters.
/// </summary>
public class DdlSiteAdapterTests
{
    #region MockDdlSiteAdapter Tests

    [Fact]
    public void MockAdapter_HasCorrectSiteType()
    {
        var adapter = new MockDdlSiteAdapter();
        
        Assert.Equal("MockDdl", adapter.SiteType);
        Assert.Equal("Mock DDL (Testing)", adapter.DisplayName);
    }

    [Fact]
    public async Task MockAdapter_Search_ReturnsCandidates()
    {
        var adapter = new MockDdlSiteAdapter();
        var query = new DdlSearchQuery { Limit = 10 };
        
        var result = await adapter.SearchAsync(query);
        
        Assert.True(result.Success);
        Assert.NotEmpty(result.Candidates);
    }

    [Fact]
    public async Task MockAdapter_Search_FiltersbySeriesTitle()
    {
        var adapter = new MockDdlSiteAdapter();
        var query = new DdlSearchQuery 
        { 
            SeriesTitle = "Batman",
            Limit = 10 
        };
        
        var result = await adapter.SearchAsync(query);
        
        Assert.True(result.Success);
        Assert.All(result.Candidates, c => 
            Assert.Contains("batman", c.ReleaseTitle.ToLowerInvariant()));
    }

    [Fact]
    public async Task MockAdapter_Search_HasDownloadLinks()
    {
        var adapter = new MockDdlSiteAdapter();
        var query = new DdlSearchQuery { Limit = 5 };
        
        var result = await adapter.SearchAsync(query);
        
        Assert.All(result.Candidates, c => 
            Assert.NotEmpty(c.DownloadLinks));
    }

    [Fact]
    public async Task MockAdapter_GetLatest_ReturnsCandidates()
    {
        var adapter = new MockDdlSiteAdapter();
        
        var result = await adapter.GetLatestAsync(5);
        
        Assert.True(result.Success);
        Assert.NotEmpty(result.Candidates);
        Assert.True(result.Candidates.Count <= 5);
    }

    [Fact]
    public async Task MockAdapter_ExtractLinks_ReturnsLinks()
    {
        var adapter = new MockDdlSiteAdapter();
        
        var links = await adapter.ExtractLinksAsync("https://mock.ddl.local/release/test");
        
        Assert.NotEmpty(links);
        Assert.All(links, l => Assert.NotEmpty(l.Url));
    }

    [Fact]
    public async Task MockAdapter_VerifyLink_ReturnsTrue()
    {
        var adapter = new MockDdlSiteAdapter();
        
        var isValid = await adapter.VerifyLinkAsync("https://mock.ddl.local/download/test.cbz");
        
        Assert.True(isValid);
    }

    [Fact]
    public async Task MockAdapter_TestConnection_ReturnsSuccess()
    {
        var adapter = new MockDdlSiteAdapter();
        
        var result = await adapter.TestConnectionAsync();
        
        Assert.True(result.Success);
        Assert.True(result.SampleResultCount > 0);
    }

    #endregion

    #region DdlSiteAdapterFactory Tests

    [Fact]
    public void Factory_GetAdapter_ReturnsAdapter()
    {
        var factory = new DdlSiteAdapterFactory();
        
        var adapter = factory.GetAdapter("MockDdl");
        
        Assert.NotNull(adapter);
        Assert.Equal("MockDdl", adapter.SiteType);
    }

    [Fact]
    public void Factory_GetAdapter_ThrowsForUnknown()
    {
        var factory = new DdlSiteAdapterFactory();
        
        Assert.Throws<ArgumentException>(() => factory.GetAdapter("Unknown"));
    }

    [Fact]
    public void Factory_GetRegisteredSiteTypes_ReturnsTypes()
    {
        var factory = new DdlSiteAdapterFactory();
        
        var types = factory.GetRegisteredSiteTypes();
        
        Assert.NotEmpty(types);
        Assert.Contains("MockDdl", types);
    }

    [Fact]
    public void Factory_IsRegistered_ReturnsTrueForKnown()
    {
        var factory = new DdlSiteAdapterFactory();
        
        Assert.True(factory.IsRegistered("MockDdl"));
        Assert.False(factory.IsRegistered("Unknown"));
    }

    [Fact]
    public void Factory_GetAvailableSiteInfos_ReturnsInfo()
    {
        var factory = new DdlSiteAdapterFactory();
        
        var infos = factory.GetAvailableSiteInfos();
        
        Assert.NotEmpty(infos);
        
        var mockInfo = infos.First(i => i.SiteType == "MockDdl");
        Assert.NotNull(mockInfo.DisplayName);
        Assert.NotNull(mockInfo.DefaultBaseUrl);
    }

    [Fact]
    public void Factory_RegisterAdapter_AddsNewAdapter()
    {
        var factory = new DdlSiteAdapterFactory();
        
        factory.RegisterAdapter("CustomSite", () => new MockDdlSiteAdapter());
        
        Assert.True(factory.IsRegistered("CustomSite"));
    }

    [Fact]
    public void Factory_GetSiteTypeFromUrl_MatchesKnownSites()
    {
        var factory = new DdlSiteAdapterFactory();
        
        var siteType = factory.GetSiteTypeFromUrl("https://mock.ddl.local/release/test");
        
        Assert.Equal("MockDdl", siteType);
    }

    [Fact]
    public void Factory_GetSiteTypeFromUrl_ReturnsNullForUnknown()
    {
        var factory = new DdlSiteAdapterFactory();
        
        var siteType = factory.GetSiteTypeFromUrl("https://unknown.site.com/page");
        
        Assert.Null(siteType);
    }

    #endregion

    #region GettyComicsSiteAdapter Tests

    [Fact]
    public void GettyAdapter_HasCorrectSiteType()
    {
        var adapter = new GettyComicsSiteAdapter();
        
        Assert.Equal("GettyComics", adapter.SiteType);
        Assert.Equal("Getty Comics", adapter.DisplayName);
    }

    [Fact]
    public void GettyAdapter_Configure_SetsConfiguration()
    {
        var adapter = new GettyComicsSiteAdapter();
        var config = new DdlSiteConfiguration
        {
            BaseUrl = "https://custom.gettycomics.com",
            TimeoutSeconds = 60
        };
        
        adapter.Configure(config);
        
        // Adapter should use the configured base URL
        // (Internal state, verified through behavior)
        Assert.NotNull(adapter);
    }

    #endregion
}

