using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Indexers;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Tests;

public class RssIndexerTests
{
    private const string SampleRssFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Comic Releases Feed</title>
    <link>https://example.com/comics</link>
    <description>Latest comic releases</description>
    <item>
      <title>Batman 001 (2023) (Digital).cbz</title>
      <link>https://example.com/download/batman001.cbz</link>
      <guid>batman-001-2023</guid>
      <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
      <category>DC Comics</category>
    </item>
    <item>
      <title>Amazing Spider-Man 050 (2023) (Digital).cbz</title>
      <link>https://example.com/download/asm050.cbz</link>
      <guid>asm-050-2023</guid>
      <pubDate>Sun, 31 Dec 2023 12:00:00 GMT</pubDate>
      <category>Marvel</category>
    </item>
    <item>
      <title>Saga Vol. 01 TPB (2012) (Digital).cbz</title>
      <link>https://example.com/download/saga-vol1.cbz</link>
      <guid>saga-vol1-2012</guid>
      <pubDate>Sat, 30 Dec 2023 12:00:00 GMT</pubDate>
      <category>Image Comics</category>
    </item>
  </channel>
</rss>";

    private const string SampleAtomFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"">
  <title>Comic Releases Atom Feed</title>
  <link href=""https://example.com/comics""/>
  <entry>
    <title>X-Men 001 (2024) (Digital).cbz</title>
    <link href=""https://example.com/download/xmen001.cbz""/>
    <id>xmen-001-2024</id>
    <published>2024-01-15T12:00:00Z</published>
  </entry>
</feed>";

    [Fact]
    public async Task FetchFeed_WithValidRssFeed_ReturnsItems()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.FetchFeedAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, i => i.Title.Contains("Batman"));
        Assert.Contains(result.Items, i => i.Title.Contains("Spider-Man"));
        Assert.Contains(result.Items, i => i.Title.Contains("Saga"));
    }

    [Fact]
    public async Task FetchFeed_WithValidAtomFeed_ReturnsItems()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleAtomFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.FetchFeedAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Contains("X-Men", result.Items[0].Title);
    }

    [Fact]
    public async Task FetchFeed_With404Response_ReturnsFailed()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.NotFound, "");
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.FetchFeedAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("404", result.Error);
    }

    [Fact]
    public async Task GetLatest_ReturnsConvertedCandidates()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.GetLatestAsync(limit: 10);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Candidates.Count);
        
        // Check that candidates have parsed info
        var batmanCandidate = result.Candidates.FirstOrDefault(c => c.SeriesTitle?.Contains("Batman") == true);
        Assert.NotNull(batmanCandidate);
        Assert.Equal(1, batmanCandidate.IssueNumber);
        Assert.Equal(2023, batmanCandidate.Year);
    }

    [Fact]
    public async Task Search_FiltersResults()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        var query = new Shortboxerr.Core.Providers.IndexerSearchQuery
        {
            SeriesTitle = "Batman"
        };

        // Act
        var result = await indexer.SearchAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Contains("Batman", result.Candidates[0].ReleaseTitle);
    }

    [Fact]
    public async Task Search_WithYearFilter_FiltersCorrectly()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        var query = new Shortboxerr.Core.Providers.IndexerSearchQuery
        {
            Year = 2012
        };

        // Act
        var result = await indexer.SearchAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Contains("Saga", result.Candidates[0].ReleaseTitle);
    }

    [Fact]
    public async Task Test_ReturnsSuccessWithItemCount()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.TestAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Contains("3 items", result.Message);
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthyWhenConfigured()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Act
        var result = await indexer.GetHealthAsync();

        // Assert
        Assert.Equal(Shortboxerr.Core.Providers.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void SupportsRss_ReturnsTrue()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Assert
        Assert.True(indexer.SupportsRss);
    }

    [Fact]
    public void SupportsSearch_ReturnsFalse()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, SampleRssFeed);
        var httpClient = new HttpClient(mockHandler.Object);
        var indexer = CreateIndexer(httpClient);

        // Assert
        Assert.False(indexer.SupportsSearch);
    }

    private static Shortboxerr.Infrastructure.Indexers.RssIndexer CreateIndexer(HttpClient httpClient)
    {
        var filenameParser = new Shortboxerr.Core.Services.FilenameParser();
        var logger = Mock.Of<ILogger<Shortboxerr.Infrastructure.Indexers.RssIndexer>>();
        var settings = new RssIndexerSettings
        {
            Id = 1,
            Name = "Test RSS Indexer",
            FeedUrl = "https://example.com/feed.rss",
            PollIntervalMinutes = 30,
            MaxItemsPerPoll = 100
        };

        return new Shortboxerr.Infrastructure.Indexers.RssIndexer(
            httpClient,
            filenameParser,
            logger,
            settings);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return mockHandler;
    }
}

