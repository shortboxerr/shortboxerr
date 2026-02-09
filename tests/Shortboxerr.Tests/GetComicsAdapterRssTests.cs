using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using System.Net;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for GetComicsAdapter RSS and category features.
/// </summary>
public class GetComicsAdapterRssTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    
    public GetComicsAdapterRssTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
    }
    
    private GetComicsAdapter CreateAdapterWithMockedHttp()
    {
        var httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://getcomics.org")
        };
        
        var rssFeedService = new RssFeedService(httpClient);
        var adapter = new GetComicsAdapter(NullLogger<GetComicsAdapter>.Instance, rssFeedService);
        
        // Use reflection to set the private _httpClient field
        var field = typeof(BaseDdlSiteAdapter).GetField("_httpClient", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(adapter, httpClient);
        
        return adapter;
    }
    
    #region RSS Feed Tests
    
    [Fact]
    public async Task GetRssFeedAsync_ReturnsItems_WhenFeedIsValid()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>GetComics.info – Downloads last 24 hours Comics and Magazines for FREE</title>
    <item>
      <title>Batman #150 (2024)</title>
      <link>https://getcomics.org/dc/batman-150-2024</link>
      <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
      <category>DC</category>
    </item>
    <item>
      <title>Amazing Spider-Man #50</title>
      <link>https://getcomics.org/marvel/amazing-spider-man-50</link>
      <pubDate>Mon, 01 Jan 2024 11:00:00 GMT</pubDate>
      <category>Marvel</category>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.True(result.Success);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("Batman #150 (2024)", result.Candidates[0].ReleaseTitle);
        Assert.Equal("Amazing Spider-Man #50", result.Candidates[1].ReleaseTitle);
    }
    
    [Fact]
    public async Task GetRssFeedAsync_ParsesTags_FromCategories()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Batman #100</title>
      <link>https://getcomics.org/dc/batman-100</link>
      <category>DC</category>
      <category>Batman</category>
      <category>Weekly</category>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Contains("dc", result.Candidates[0].Tags);
        Assert.Contains("batman", result.Candidates[0].Tags);
        Assert.Contains("weekly", result.Candidates[0].Tags);
    }
    
    [Fact]
    public async Task GetRssFeedAsync_ParsesDescription()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Batman #100</title>
      <link>https://getcomics.org/dc/batman-100</link>
      <description><![CDATA[The 100th issue of Batman! Download now.]]></description>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.True(result.Success);
        Assert.Equal("The 100th issue of Batman! Download now.", result.Candidates[0].Description);
    }
    
    [Fact]
    public async Task GetRssFeedAsync_RespectsLimit()
    {
        var items = string.Join("", Enumerable.Range(1, 20).Select(i => $@"
    <item>
      <title>Comic #{i}</title>
      <link>https://getcomics.org/comic-{i}</link>
    </item>"));
        
        var rssFeed = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    {items}
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(5);
        
        Assert.True(result.Success);
        Assert.Equal(5, result.Candidates.Count);
    }
    
    [Fact]
    public async Task GetRssFeedAsync_ReturnsError_WhenFeedInvalid()
    {
        SetupMockResponse("https://getcomics.org/feed/", "not valid xml");
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
    
    #endregion
    
    #region Category Tests
    
    [Fact]
    public async Task GetCategoryAsync_ReturnsItems_FromCategoryPage()
    {
        var html = @"
<html>
<body>
  <article class=""post-123"">
    <h1 class=""post-title"">
      <a href=""https://getcomics.org/dc/batman-100"">Batman #100 (2024)</a>
    </h1>
  </article>
  <article class=""post-456"">
    <h1 class=""post-title"">
      <a href=""https://getcomics.org/dc/superman-50"">Superman #50 (2024)</a>
    </h1>
  </article>
</body>
</html>";
        
        SetupMockResponse("https://getcomics.org/cat/dc/", html);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetCategoryAsync("dc", 50);
        
        Assert.True(result.Success);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Contains("dc", c.Tags));
    }
    
    [Fact]
    public async Task GetCategoryRssFeedAsync_ReturnsItems_FromCategoryFeed()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>DC Comics - GetComics</title>
    <item>
      <title>Batman #100</title>
      <link>https://getcomics.org/dc/batman-100</link>
    </item>
    <item>
      <title>Superman #50</title>
      <link>https://getcomics.org/dc/superman-50</link>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/cat/dc/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetCategoryRssFeedAsync("dc", 50);
        
        Assert.True(result.Success);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Contains("dc", c.Tags));
    }
    
    [Fact]
    public void GetAvailableCategories_ReturnsKnownCategories()
    {
        var categories = GetComicsAdapter.GetAvailableCategories();
        
        Assert.True(categories.Count >= 10);
        Assert.True(categories.ContainsKey("dc"));
        Assert.True(categories.ContainsKey("marvel"));
        Assert.True(categories.ContainsKey("image"));
        Assert.Equal("DC Comics", categories["dc"]);
        Assert.Equal("Marvel Comics", categories["marvel"]);
    }
    
    #endregion
    
    #region Parsed Info Tests
    
    [Fact]
    public async Task GetRssFeedAsync_ParsesReleaseInfo()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Batman Vol. 1 - The Court of Owls (2012) TPB</title>
      <link>https://getcomics.org/dc/batman-vol-1</link>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        
        var candidate = result.Candidates[0];
        Assert.NotNull(candidate.ParsedInfo);
        Assert.Contains("Batman", candidate.ParsedInfo.SeriesTitle ?? "");
        Assert.True(candidate.ParsedInfo.IsCollection);
    }
    
    [Fact]
    public async Task GetRssFeedAsync_SetsDateFound_FromPubDate()
    {
        var rssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Batman #100</title>
      <link>https://getcomics.org/dc/batman-100</link>
      <pubDate>Wed, 15 Jan 2025 10:00:00 GMT</pubDate>
    </item>
  </channel>
</rss>";
        
        SetupMockResponse("https://getcomics.org/feed/", rssFeed);
        var adapter = CreateAdapterWithMockedHttp();
        
        var result = await adapter.GetRssFeedAsync(50);
        
        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal(2025, result.Candidates[0].DateFound.Year);
        Assert.Equal(1, result.Candidates[0].DateFound.Month);
        Assert.Equal(15, result.Candidates[0].DateFound.Day);
    }
    
    #endregion
    
    #region Helper Methods
    
    private void SetupMockResponse(string url, string content)
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().StartsWith(url.TrimEnd('/'))),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
    }
    
    #endregion
}
