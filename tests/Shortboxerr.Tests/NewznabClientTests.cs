using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class NewznabClientTests
{
    private readonly Mock<ILogger<NewznabClient>> _loggerMock;

    public NewznabClientTests()
    {
        _loggerMock = new Mock<ILogger<NewznabClient>>();
    }

    private static NewznabIndexer CreateTestIndexer() => new()
    {
        Id = "test-indexer",
        Name = "Test Indexer",
        BaseUrl = "https://api.test-indexer.com",
        ApiKey = "test-api-key-12345",
        Categories = new List<int> { 7030, 7000 }
    };

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/xml")
            });

        return new HttpClient(handlerMock.Object);
    }

    #region Search Tests

    [Fact]
    public async Task SearchAsync_WithValidResponse_ReturnsReleases()
    {
        // Arrange
        var xml = GetValidSearchResponseXml();
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery { Query = "Batman" };

        // Act
        var result = await client.SearchAsync(indexer, query);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Releases.Count);
        Assert.Equal(2, result.TotalResults);
    }

    [Fact]
    public async Task SearchAsync_WithValidResponse_ParsesReleaseCorrectly()
    {
        // Arrange
        var xml = GetValidSearchResponseXml();
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery { Query = "Batman" };

        // Act
        var result = await client.SearchAsync(indexer, query);
        var release = result.Releases[0];

        // Assert
        Assert.Equal("release-guid-1", release.Guid);
        Assert.Equal("Batman 001 (2024)", release.Title);
        Assert.Equal("https://api.test-indexer.com/nzb/release-guid-1", release.NzbUrl);
        Assert.Equal(52428800, release.Size); // 50MB
        Assert.Equal("Test Indexer", release.IndexerName);
    }

    [Fact]
    public async Task SearchAsync_WithHttpError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "");
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery { Query = "Batman" };

        // Act
        var result = await client.SearchAsync(indexer, query);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("500", result.ErrorMessage);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_WithApiError_ReturnsErrorMessage()
    {
        // Arrange
        var xml = GetApiErrorXml(100, "Invalid API Key");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery { Query = "Batman" };

        // Act
        var result = await client.SearchAsync(indexer, query);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid API Key", result.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_BuildsCorrectUrl()
    {
        // Arrange
        string? capturedUrl = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(GetEmptySearchResponseXml(), Encoding.UTF8, "application/xml")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery
        {
            Query = "Batman 001",
            Categories = new List<int> { 7030 },
            Limit = 50,
            MaxAge = 30
        };

        // Act
        await client.SearchAsync(indexer, query);

        // Assert
        Assert.NotNull(capturedUrl);
        Assert.Contains("t=search", capturedUrl);
        Assert.Contains("apikey=test-api-key-12345", capturedUrl);
        Assert.Contains("q=Batman", capturedUrl); // Check for query presence (encoding may vary)
        Assert.Contains("cat=7030", capturedUrl);
        Assert.Contains("limit=50", capturedUrl);
        Assert.Contains("maxage=30", capturedUrl);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var xml = GetEmptySearchResponseXml();
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();
        var query = new NewznabSearchQuery { Query = "NonExistent Comic" };

        // Act
        var result = await client.SearchAsync(indexer, query);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Releases);
        Assert.Equal(0, result.TotalResults);
    }

    #endregion

    #region Capabilities Tests

    [Fact]
    public async Task GetCapabilitiesAsync_WithValidResponse_ParsesCorrectly()
    {
        // Arrange
        var xml = GetValidCapsResponseXml();
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.GetCapabilitiesAsync(indexer);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Server);
        Assert.Equal("1.0", result.Server.Version);
        Assert.Equal("Test Indexer", result.Server.Title);
        Assert.True(result.Searching.SearchAvailable);
        Assert.True(result.Searching.BookSearchAvailable);
        Assert.Equal(100, result.Limits.Max);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ParsesCategories()
    {
        // Arrange
        var xml = GetValidCapsResponseXml();
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, xml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.GetCapabilitiesAsync(indexer);

        // Assert
        Assert.True(result.Success);
        var booksCategory = result.Categories.FirstOrDefault(c => c.Id == 7000);
        Assert.NotNull(booksCategory);
        Assert.Equal("Books", booksCategory.Name);
        Assert.Single(booksCategory.SubCategories);
        Assert.Equal(7030, booksCategory.SubCategories[0].Id);
        Assert.Equal("Comics", booksCategory.SubCategories[0].Name);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_WithHttpError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, "");
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.GetCapabilitiesAsync(indexer);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("503", result.ErrorMessage);
    }

    #endregion

    #region TestConnection Tests

    [Fact]
    public async Task TestConnectionAsync_WithValidIndexer_ReturnsSuccess()
    {
        // Arrange
        var capsXml = GetValidCapsResponseXml();
        var searchXml = GetEmptySearchResponseXml();

        var requestCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                requestCount++;
                var content = requestCount == 1 ? capsXml : searchXml;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(content, Encoding.UTF8, "application/xml")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.TestConnectionAsync(indexer);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Connected successfully", result.Message);
        Assert.NotNull(result.Capabilities);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidApiKey_ReturnsFailure()
    {
        // Arrange
        var errorXml = GetApiErrorXml(100, "Invalid API Key");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, errorXml);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.TestConnectionAsync(indexer);

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region DownloadNzb Tests

    [Fact]
    public async Task DownloadNzbAsync_WithGuid_BuildsCorrectUrl()
    {
        // Arrange
        string? capturedUrl = null;
        var nzbContent = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><nzb></nzb>");
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(nzbContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        await client.DownloadNzbAsync(indexer, "release-guid-123");

        // Assert
        Assert.NotNull(capturedUrl);
        Assert.Contains("t=get", capturedUrl);
        Assert.Contains("id=release-guid-123", capturedUrl);
        Assert.Contains("apikey=test-api-key-12345", capturedUrl);
    }

    [Fact]
    public async Task DownloadNzbAsync_WithFullUrl_UsesUrlDirectly()
    {
        // Arrange
        string? capturedUrl = null;
        var nzbContent = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><nzb></nzb>");
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(nzbContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        await client.DownloadNzbAsync(indexer, "https://api.test-indexer.com/nzb/release-guid-123");

        // Assert
        Assert.NotNull(capturedUrl);
        Assert.StartsWith("https://api.test-indexer.com/nzb/release-guid-123", capturedUrl);
        Assert.Contains("apikey=", capturedUrl); // Should append API key
    }

    [Fact]
    public async Task DownloadNzbAsync_ReturnsNzbContent()
    {
        // Arrange
        var nzbContent = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><nzb><file></file></nzb>");
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(nzbContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NewznabClient(httpClient, _loggerMock.Object);
        var indexer = CreateTestIndexer();

        // Act
        var result = await client.DownloadNzbAsync(indexer, "release-guid-123");

        // Assert
        Assert.Equal(nzbContent, result);
    }

    #endregion

    #region Helper Methods

    private static string GetValidSearchResponseXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">
              <channel>
                <title>Test Indexer</title>
                <newznab:response offset="0" total="2"/>
                <item>
                  <title>Batman 001 (2024)</title>
                  <guid>release-guid-1</guid>
                  <link>https://api.test-indexer.com/nzb/release-guid-1</link>
                  <pubDate>Mon, 01 Jan 2024 12:00:00 +0000</pubDate>
                  <enclosure url="https://api.test-indexer.com/nzb/release-guid-1" length="52428800" type="application/x-nzb"/>
                  <newznab:attr name="size" value="52428800"/>
                  <newznab:attr name="grabs" value="100"/>
                  <newznab:attr name="category" value="7030"/>
                </item>
                <item>
                  <title>Batman 002 (2024)</title>
                  <guid>release-guid-2</guid>
                  <link>https://api.test-indexer.com/nzb/release-guid-2</link>
                  <pubDate>Mon, 08 Jan 2024 12:00:00 +0000</pubDate>
                  <enclosure url="https://api.test-indexer.com/nzb/release-guid-2" length="48000000" type="application/x-nzb"/>
                  <newznab:attr name="size" value="48000000"/>
                  <newznab:attr name="grabs" value="50"/>
                  <newznab:attr name="category" value="7030"/>
                </item>
              </channel>
            </rss>
            """;
    }

    private static string GetEmptySearchResponseXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">
              <channel>
                <title>Test Indexer</title>
                <newznab:response offset="0" total="0"/>
              </channel>
            </rss>
            """;
    }

    private static string GetApiErrorXml(int code, string description)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <error code="{code}" description="{description}"/>
            """;
    }

    private static string GetValidCapsResponseXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <caps>
              <server version="1.0" title="Test Indexer" strapline="Test NZB Indexer" email="test@test.com" url="https://test-indexer.com"/>
              <limits max="100" default="100"/>
              <searching>
                <search available="yes"/>
                <tv-search available="yes"/>
                <movie-search available="no"/>
                <music-search available="no"/>
                <book-search available="yes"/>
                <audio-search available="no"/>
              </searching>
              <categories>
                <category id="7000" name="Books">
                  <subcat id="7030" name="Comics"/>
                </category>
              </categories>
            </caps>
            """;
    }

    #endregion
}
