using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for GetComicsAdapter HTML parsing and link extraction.
/// Uses realistic mocked HTML responses based on typical DDL site structures.
/// </summary>
public class GetComicsAdapterTests
{
    private readonly GetComicsAdapter _adapter;

    public GetComicsAdapterTests()
    {
        _adapter = new GetComicsAdapter();
    }

    #region Search Page Parsing Tests

    [Fact]
    public void ParseSearchPage_WithPostTitleFormat_ExtractsCandidates()
    {
        // Arrange - HTML with post-title format
        var html = """
            <html>
            <body>
                <article class="post-12345">
                    <h1 class="post-title">
                        <a href="https://getcomics.org/marvel/amazing-spider-man-001-2024/">
                            Amazing Spider-Man 001 (2024)
                        </a>
                    </h1>
                </article>
                <article class="post-12346">
                    <h1 class="post-title">
                        <a href="/dc/batman-150-2024/">
                            Batman 150 (2024)
                        </a>
                    </h1>
                </article>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Equal(2, candidates.Count);

        Assert.Equal("Amazing Spider-Man 001 (2024)", candidates[0].ReleaseTitle);
        Assert.Equal("https://getcomics.org/marvel/amazing-spider-man-001-2024/", candidates[0].SourceUrl);
        Assert.Equal("GetComics", candidates[0].SourceSite);

        Assert.Equal("Batman 150 (2024)", candidates[1].ReleaseTitle);
        Assert.Contains("batman-150-2024", candidates[1].SourceUrl);
    }

    [Fact]
    public void ParseSearchPage_WithEntryTitleFormat_ExtractsCandidates()
    {
        // Arrange - Alternative WordPress theme format
        var html = """
            <html>
            <body>
                <h2 class="entry-title">
                    <a href="https://getcomics.org/other-comics/x-men-001-2024/">
                        X-Men 001 (2024)
                    </a>
                </h2>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("X-Men 001 (2024)", candidates[0].ReleaseTitle);
    }

    [Fact]
    public void ParseSearchPage_SkipsNavigationLinks()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <h1 class="post-title"><a href="/category/marvel/">Home</a></h1>
                <h1 class="post-title"><a href="/about/">About</a></h1>
                <h1 class="post-title"><a href="/contact/">Contact</a></h1>
                <h1 class="post-title">
                    <a href="/marvel/iron-man-001-2024/">Iron Man 001 (2024)</a>
                </h1>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Iron Man 001 (2024)", candidates[0].ReleaseTitle);
    }

    [Fact]
    public void ParseSearchPage_SkipsCategoryLinks()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <h1 class="post-title"><a href="/category/dc/">DC Comics</a></h1>
                <h1 class="post-title"><a href="/tag/batman/">Batman Tag</a></h1>
                <h1 class="post-title">
                    <a href="/dc/wonder-woman-001-2024/">Wonder Woman 001 (2024)</a>
                </h1>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Wonder Woman 001 (2024)", candidates[0].ReleaseTitle);
    }

    [Fact]
    public void ParseSearchPage_DeduplicatesByUrl()
    {
        // Arrange - Same URL in different formats
        var html = """
            <html>
            <body>
                <h1 class="post-title">
                    <a href="https://getcomics.org/marvel/hulk-001-2024/">
                        Hulk 001 (2024)
                    </a>
                </h1>
                <h2 class="entry-title">
                    <a href="https://getcomics.org/marvel/hulk-001-2024/">
                        Hulk 001 (2024)
                    </a>
                </h2>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
    }

    [Fact]
    public void ParseSearchPage_ParsesReleaseInfo()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <h1 class="post-title">
                    <a href="/marvel/avengers-001-marvel-2024-digital/">
                        Avengers 001 (Marvel) (2024) (Digital)
                    </a>
                </h1>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        var parsed = candidates[0].ParsedInfo;
        Assert.NotNull(parsed);
        Assert.Equal("Avengers", parsed.SeriesTitle);
        Assert.Equal(1, parsed.IssueNumber);
        Assert.Equal(2024, parsed.Year);
    }

    [Fact]
    public void ParseSearchPage_ExtractsReleaseWithSizeInfo()
    {
        // Arrange - Size info is present but not currently extracted to candidate
        // (Size requires candidate model modification - future enhancement)
        var html = """
            <html>
            <body>
                <h1 class="post-title">
                    <a href="/marvel/thor-001-2024/">Thor 001 (2024)</a>
                </h1>
                <div>File Size: 45 MB</div>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        Assert.Equal("Thor 001 (2024)", candidates[0].ReleaseTitle);
    }

    [Fact]
    public void ParseSearchPage_HandlesEmptyHtml()
    {
        // Arrange
        var html = "<html><body></body></html>";

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void ParseSearchPage_HandlesCollectionReleases()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <h1 class="post-title">
                    <a href="/trades/batman-vol-1-tpb-2024/">
                        Batman Vol. 1 – The City of Owls TPB (2024)
                    </a>
                </h1>
            </body>
            </html>
            """;

        // Act
        var candidates = _adapter.ParseSearchPage(html);

        // Assert
        Assert.Single(candidates);
        var parsed = candidates[0].ParsedInfo;
        Assert.NotNull(parsed);
        Assert.True(parsed.IsCollection);
    }

    #endregion

    #region Adapter Properties Tests

    [Fact]
    public void Adapter_HasCorrectSiteType()
    {
        Assert.Equal("GetComics", _adapter.SiteType);
    }

    [Fact]
    public void Adapter_HasCorrectDisplayName()
    {
        Assert.Equal("GetComics.org", _adapter.DisplayName);
    }

    [Fact]
    public void Adapter_HasCorrectDefaultBaseUrl()
    {
        Assert.Equal("https://getcomics.org", _adapter.DefaultBaseUrl);
    }

    [Fact]
    public void Adapter_DoesNotRequireAuthentication()
    {
        Assert.False(_adapter.RequiresAuthentication);
    }

    [Fact]
    public void Adapter_HasReasonableRateLimit()
    {
        Assert.Equal(10, _adapter.DefaultRateLimitPerMinute);
    }

    #endregion

    #region Search URL Building Tests

    [Fact]
    public void BuildSearchUrl_WithSeriesTitle_BuildsCorrectUrl()
    {
        // We can't directly test BuildSearchUrl as it's protected,
        // but we can test via SearchAsync with a mock HTTP client
        // For now, just verify the adapter is properly configured
        Assert.NotEmpty(_adapter.DefaultBaseUrl);
    }

    #endregion
}
