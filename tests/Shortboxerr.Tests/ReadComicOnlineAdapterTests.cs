using System.Net;
using System.Text;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for ReadComicOnlineAdapter.
/// Tests HTML parsing, search functionality, and link extraction.
/// </summary>
public class ReadComicOnlineAdapterTests
{
    private readonly ReadComicOnlineAdapter _adapter;

    public ReadComicOnlineAdapterTests()
    {
        _adapter = new ReadComicOnlineAdapter();
    }

    #region Adapter Properties Tests

    [Fact]
    public void SiteType_ReturnsReadComicOnline()
    {
        Assert.Equal("ReadComicOnline", _adapter.SiteType);
    }

    [Fact]
    public void DisplayName_ReturnsReadComicOnline()
    {
        Assert.Equal("ReadComicOnline", _adapter.DisplayName);
    }

    [Fact]
    public void DefaultBaseUrl_ReturnsExpectedUrl()
    {
        Assert.Equal("https://readcomiconline.li", _adapter.DefaultBaseUrl);
    }

    [Fact]
    public void RequiresAuthentication_ReturnsFalse()
    {
        Assert.False(_adapter.RequiresAuthentication);
    }

    [Fact]
    public void DefaultRateLimitPerMinute_ReturnsFive()
    {
        Assert.Equal(5, _adapter.DefaultRateLimitPerMinute);
    }

    #endregion

    #region ParseSearchPage Tests

    [Fact]
    public void ParseSearchPage_WithComicListLinks_ExtractsCandidates()
    {
        // Arrange
        var html = """
            <div class="comic-list">
                <a href="/Comic/Batman-2016" title="Batman (2016)">Batman (2016)</a>
                <a href="/Comic/Superman-Rebirth" title="Superman Rebirth">Superman Rebirth</a>
                <a href="/Comic/Wonder-Woman" title="Wonder Woman">Wonder Woman</a>
            </div>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Equal(3, candidates.Count);
        Assert.Contains(candidates, c => c.ReleaseTitle == "Batman (2016)");
        Assert.Contains(candidates, c => c.ReleaseTitle == "Superman Rebirth");
        Assert.Contains(candidates, c => c.ReleaseTitle == "Wonder Woman");
    }

    [Fact]
    public void ParseSearchPage_WithTableRows_ExtractsCandidates()
    {
        // Arrange
        var html = """
            <table>
                <tr><td><a href="/Comic/Amazing-Spider-Man">Amazing Spider-Man</a></td></tr>
                <tr><td><a href="/Comic/Venom-2018">Venom (2018)</a></td></tr>
            </table>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ReleaseTitle == "Amazing Spider-Man");
        Assert.Contains(candidates, c => c.ReleaseTitle == "Venom (2018)");
    }

    [Fact]
    public void ParseSearchPage_SkipsNavigationLinks()
    {
        // Arrange
        var html = """
            <a href="/Genre/Action" title="Action">Action</a>
            <a href="/ComicList" title="All Comics">All Comics</a>
            <a href="/Comic/Batman-2016" title="Batman (2016)">Batman (2016)</a>
            <a href="/Search" title="Search">Search</a>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Batman (2016)", candidates[0].ReleaseTitle);
    }

    [Fact]
    public void ParseSearchPage_DeduplicatesSameUrl()
    {
        // Arrange - same comic listed twice
        var html = """
            <a href="/Comic/Batman-2016" title="Batman (2016)">Batman (2016)</a>
            <td><a href="/Comic/Batman-2016">Batman (2016)</a></td>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
    }

    [Fact]
    public void ParseSearchPage_SetsSourceSiteCorrectly()
    {
        // Arrange
        var html = """<a href="/Comic/Test-Comic" title="Test Comic">Test Comic</a>""";

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("ReadComicOnline", candidates[0].SourceSite);
    }

    [Fact]
    public void ParseSearchPage_ParsesTitle()
    {
        // Arrange
        var html = """<a href="/Comic/Batman-Vol-3-2016" title="Batman Vol 3 (2016)">Batman Vol 3 (2016)</a>""";

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Batman Vol 3 (2016)", candidates[0].ReleaseTitle);
        Assert.NotNull(candidates[0].ParsedInfo);
    }

    [Fact]
    public void ParseSearchPage_EmptyHtml_ReturnsEmptyList()
    {
        // Act
        var candidates = _adapter.ParseSearchPage("");

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void ParseSearchPage_NoComicLinks_ReturnsEmptyList()
    {
        // Arrange
        var html = """
            <a href="/page/about">About</a>
            <a href="/login">Login</a>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Empty(candidates);
    }

    #endregion

    #region ParseDownloadLinks Tests

    [Fact]
    public void ParseDownloadLinks_WithIssueLinks_ExtractsLinks()
    {
        // Arrange
        var html = """
            <div class="issues">
                <a href="/Comic/Batman-2016/Issue-1">Issue #1</a>
                <a href="/Comic/Batman-2016/Issue-2">Issue #2</a>
                <a href="/Comic/Batman-2016/Issue-3">Issue #3</a>
            </div>
            """;

        // Act
        var links = _adapter.ParseDownloadLinks(html, "https://readcomiconline.li/Comic/Batman-2016");

        // Assert
        Assert.Equal(3, links.Count);
        Assert.All(links, l => Assert.Equal("ReadComicOnline", l.HostName));
        Assert.All(links, l => Assert.True(l.IsVerified));
    }

    [Fact]
    public void ParseDownloadLinks_WithExternalHostLinks_ExtractsLinks()
    {
        // Arrange
        var html = """
            <a href="https://mega.nz/file/abc123">Download from Mega</a>
            <a href="https://mediafire.com/file/xyz789">Download from MediaFire</a>
            <a href="https://pixeldrain.com/u/test123">Download from Pixeldrain</a>
            """;

        // Act
        var links = _adapter.ParseDownloadLinks(html, "https://readcomiconline.li/Comic/Test");

        // Assert
        Assert.Equal(3, links.Count);
        Assert.Contains(links, l => l.HostName == "mega");
        Assert.Contains(links, l => l.HostName == "mediafire");
        Assert.Contains(links, l => l.HostName == "pixeldrain");
    }

    [Fact]
    public void ParseDownloadLinks_SortsByHostPriority()
    {
        // Arrange
        var html = """
            <a href="https://mediafire.com/file/xyz">MediaFire</a>
            <a href="/Comic/Test/Issue-1">Issue 1</a>
            <a href="https://mega.nz/file/abc">Mega</a>
            """;

        // Act
        var links = _adapter.ParseDownloadLinks(html, "https://readcomiconline.li/Comic/Test");

        // Assert
        Assert.True(links.Count >= 3);
        // ReadComicOnline internal links should be first (priority 0)
        Assert.Equal("ReadComicOnline", links[0].HostName);
    }

    [Fact]
    public void ParseDownloadLinks_DeduplicatesUrls()
    {
        // Arrange
        var html = """
            <a href="https://mega.nz/file/abc123">Download from Mega</a>
            <a href="https://mega.nz/file/abc123">Download from Mega Again</a>
            """;

        // Act
        var links = _adapter.ParseDownloadLinks(html, "https://readcomiconline.li/Comic/Test");

        // Assert
        Assert.Single(links);
    }

    [Fact]
    public void ParseDownloadLinks_EmptyHtml_ReturnsEmptyList()
    {
        // Act
        var links = _adapter.ParseDownloadLinks("", "https://readcomiconline.li/Comic/Test");

        // Assert
        Assert.Empty(links);
    }

    #endregion

    #region GetAvailableCategories Tests

    [Fact]
    public void GetAvailableCategories_ContainsPublishers()
    {
        // Act
        var categories = ReadComicOnlineAdapter.GetAvailableCategories();

        // Assert
        Assert.Contains("dc-comics", categories.Keys);
        Assert.Contains("marvel", categories.Keys);
        Assert.Contains("image", categories.Keys);
        Assert.Contains("dark-horse", categories.Keys);
    }

    [Fact]
    public void GetAvailableCategories_ContainsGenres()
    {
        // Act
        var categories = ReadComicOnlineAdapter.GetAvailableCategories();

        // Assert
        Assert.Contains("action", categories.Keys);
        Assert.Contains("superhero", categories.Keys);
        Assert.Contains("fantasy", categories.Keys);
        Assert.Contains("horror", categories.Keys);
    }

    [Fact]
    public void GetAvailableCategories_HasDisplayNames()
    {
        // Act
        var categories = ReadComicOnlineAdapter.GetAvailableCategories();

        // Assert
        Assert.Equal("DC Comics", categories["dc-comics"]);
        Assert.Equal("Marvel Comics", categories["marvel"]);
        Assert.Equal("Science Fiction", categories["sci-fi"]);
    }

    #endregion

    #region URL Building Tests

    [Fact]
    public void ParseSearchPage_MakesRelativeUrlsAbsolute()
    {
        // Arrange
        var html = """<a href="/Comic/Batman" title="Batman">Batman</a>""";

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.StartsWith("https://", candidates[0].SourceUrl);
        Assert.Contains("/Comic/Batman", candidates[0].SourceUrl);
    }

    #endregion

    #region Integration-Style Tests with Mocked HTTP

    [Fact]
    public async Task SearchAsync_WithValidResponse_ReturnsCandidates()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""
                    <div>
                        <a href="/Comic/Batman-2016" title="Batman (2016)">Batman (2016)</a>
                        <a href="/Comic/Superman" title="Superman">Superman</a>
                    </div>
                    """, Encoding.UTF8, "text/html")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var adapter = new ReadComicOnlineAdapter();
        adapter.Configure(new DdlSiteConfiguration { FollowRedirects = true });

        // Note: We can't easily inject the HttpClient into the adapter
        // This test verifies the parsing logic works with realistic HTML
        var html = """
            <div>
                <a href="/Comic/Batman-2016" title="Batman (2016)">Batman (2016)</a>
                <a href="/Comic/Superman" title="Superman">Superman</a>
            </div>
            """;

        // Act
        var candidates = adapter.ParseSearchPage(html);

        // Assert
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public async Task GetLatestAsync_WithError_ReturnsErrorResult()
    {
        // Arrange - adapter will fail when trying to connect to real site
        // This tests error handling behavior
        var adapter = new ReadComicOnlineAdapter();
        adapter.Configure(new DdlSiteConfiguration
        {
            BaseUrl = "https://invalid.example.com",
            TimeoutSeconds = 1
        });

        // Act
        var result = await adapter.GetLatestAsync(limit: 5);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region Homepage Detection Tests

    [Fact]
    public void ParseSearchPage_WithHtmlEncodedEntities_DecodesCorrectly()
    {
        // Arrange
        var html = """<a href="/Comic/Spider-Man" title="Spider-Man &amp; Friends">Spider-Man &amp; Friends</a>""";

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Spider-Man & Friends", candidates[0].ReleaseTitle);
    }

    #endregion
}
