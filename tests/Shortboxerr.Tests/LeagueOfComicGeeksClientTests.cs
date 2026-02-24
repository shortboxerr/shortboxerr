using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.LeagueOfComicGeeks;
using Shortboxerr.Infrastructure.LeagueOfComicGeeks;
using Xunit;

namespace Shortboxerr.Tests;

public class LeagueOfComicGeeksClientTests
{
    private readonly Mock<ILogger<LeagueOfComicGeeksClient>> _loggerMock;
    private readonly IMemoryCache _cache;

    public LeagueOfComicGeeksClientTests()
    {
        _loggerMock = new Mock<ILogger<LeagueOfComicGeeksClient>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private LeagueOfComicGeeksClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new LeagueOfComicGeeksClient(httpClient, _cache, _loggerMock.Object);
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsSuccess_WhenValidResponse()
    {
        var jsonResponse = @"{
            ""count"": 2,
            ""list"": ""<ul id='comic-list-block'><li data-pulls='1234' data-community='90'><a href='/comic/123456/batman-105'><img data-src='https://s3.amazonaws.com/comicgeeks/comics/covers/large-123456.jpg' /></a><div class='title'>Batman #105</div><div class='publisher'>DC Comics</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Batman", "105");

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.SeriesName.Contains("Batman"));
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsEmptyList_WhenNoResults()
    {
        var jsonResponse = @"{""count"": 0, ""list"": """"}";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("NonExistentSeries12345");

        Assert.True(result.Success);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsError_WhenHttpFails()
    {
        var handler = CreateMockHandler("", HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Batman");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsCachedResult_OnSecondCall()
    {
        var uniqueQuery = $"CacheTest_{Guid.NewGuid():N}";
        var jsonResponse = $@"{{""count"": 1, ""list"": ""<ul><li><a href='/comic/123/test'></a><div class='title'>{uniqueQuery} #1</div></li></ul>""}}";

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => callCount++)
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LeagueOfComicGeeksClient(httpClient, freshCache, _loggerMock.Object);

        var result1 = await client.SearchIssueAsync(uniqueQuery, "1");
        
        Assert.True(result1.Success, $"First call failed: {result1.Error}");
        Assert.False(result1.FromCache, "First result should not be from cache");
        Assert.Equal(1, callCount);

        var result2 = await client.SearchIssueAsync(uniqueQuery, "1");
        
        Assert.True(result2.Success, $"Second call failed: {result2.Error}");
        Assert.True(result2.FromCache, "Second result should be from cache");
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task SearchIssueAsync_ParsesIssueName_Correctly()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<ul><li><a href='/comic/999/daredevil-8'></a><div class='title'>Daredevil #8</div><div class='publisher'>Marvel Comics</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Daredevil", "8");

        Assert.True(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal("Daredevil", result.Issues[0].SeriesName);
        Assert.Equal("8", result.Issues[0].IssueNumber);
        Assert.Equal("Marvel Comics", result.Issues[0].Publisher);
    }

    [Fact]
    public async Task SearchIssueAsync_ExtractsCoverUrl_FromDataSrc()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<ul><li><a href='/comic/12345/test-1'><img data-src='https://s3.amazonaws.com/comicgeeks/comics/covers/large-12345.jpg?t=123' /></a><div class='title'>Test #1</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Test", "1");

        Assert.True(result.Success);
        Assert.Single(result.Issues);
        Assert.Contains("s3.amazonaws.com/comicgeeks", result.Issues[0].CoverUrl);
        Assert.Contains("12345", result.Issues[0].CoverUrl);
    }

    [Fact]
    public async Task SearchIssueAsync_GeneratesFallbackCoverUrl_WhenNoImage()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<ul><li><a href='/comic/99999/test-1'></a><div class='title'>Test #1</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Test", "1");

        Assert.True(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal("https://s3.amazonaws.com/comicgeeks/comics/covers/large-99999.jpg", result.Issues[0].CoverUrl);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_ReturnsReleases()
    {
        var jsonResponse = @"{
            ""count"": 3,
            ""list"": ""<ul><li data-pulls='5000' data-community='95'><a href='/comic/111/batman-200'></a><div class='title'>Batman #200</div><div class='publisher'>DC Comics</div><div class='date' data-date='1708905600'></div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.GetWeeklyReleasesAsync(new DateTime(2024, 2, 26));

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_ParsesPullCountAndRating()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<ul><li data-pulls='9500' data-community='88'><a href='/comic/222/spider-man-50'></a><div class='title'>Spider-Man #50</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.GetWeeklyReleasesAsync(DateTime.Now);

        Assert.True(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal(9500, result.Issues[0].PullCount);
        Assert.Equal(88, result.Issues[0].Rating);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenServiceResponds()
    {
        var handler = CreateMockHandler(@"{""count"":0,""list"":""""}", HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenServiceFails()
    {
        var handler = CreateMockHandler("", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        var result = await client.IsAvailableAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task SearchIssueAsync_HandlesInvalidJson_Gracefully()
    {
        var handler = CreateMockHandler("not valid json", HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Test");

        Assert.False(result.Success);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchIssueAsync_HandlesChangedHtmlStructure_Gracefully()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<div class='new-structure-we-dont-know'><span>Unknown format</span></div>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Test");

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task SearchIssueAsync_ParsesPrice_Correctly()
    {
        var jsonResponse = @"{
            ""count"": 1,
            ""list"": ""<ul><li><a href='/comic/333/test-1'></a><div class='title'>Test #1</div><div class='price'>Cover A · $4.99</div></li></ul>""
        }";

        var handler = CreateMockHandler(jsonResponse, HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SearchIssueAsync("Test", "1");

        Assert.True(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal(4.99m, result.Issues[0].Price);
    }

    private static HttpMessageHandler CreateMockHandler(string content, HttpStatusCode statusCode)
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
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        return handlerMock.Object;
    }
}
