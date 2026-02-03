using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;

namespace Shortboxerr.Tests;

public class ComicVineClientTests
{
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<ILogger<ComicVineClient>> _loggerMock;
    private readonly IMemoryCache _cache;
    
    public ComicVineClientTests()
    {
        _settingsServiceMock = new Mock<ISettingsService>();
        _loggerMock = new Mock<ILogger<ComicVineClient>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private ComicVineClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://comicvine.gamespot.com/api")
        };
        return new ComicVineClient(httpClient, _settingsServiceMock.Object, _cache, _loggerMock.Object);
    }

    private Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return handlerMock;
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidApiKey_ReturnsSuccess()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "limit": 1,
            "offset": 0,
            "number_of_page_results": 0,
            "number_of_total_results": 0,
            "status_code": 1,
            "results": [],
            "version": "1.0"
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Contains("successful", result.Message.ToLower());
        Assert.NotNull(result.LatencyMs);
        Assert.Equal("1.0", result.ApiVersion);
    }

    [Fact]
    public async Task TestConnectionAsync_WithNoApiKey_ReturnsFailure()
    {
        // Arrange
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComicVineSettings?)null);

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Message.ToLower());
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidApiKey_ReturnsError()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "invalid-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "Invalid API Key",
            "limit": 0,
            "offset": 0,
            "number_of_page_results": 0,
            "number_of_total_results": 0,
            "status_code": 100,
            "results": []
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid API Key", result.Message);
    }

    [Fact]
    public async Task SearchVolumesAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "limit": 10,
            "offset": 0,
            "number_of_page_results": 2,
            "number_of_total_results": 2,
            "status_code": 1,
            "results": [
                {
                    "id": 18166,
                    "name": "Batman",
                    "start_year": 2011,
                    "count_of_issues": 52,
                    "publisher": { "id": 10, "name": "DC Comics" },
                    "image": { "medium_url": "https://example.com/batman.jpg" }
                },
                {
                    "id": 796,
                    "name": "Batman",
                    "start_year": 1940,
                    "count_of_issues": 713,
                    "publisher": { "id": 10, "name": "DC Comics" }
                }
            ]
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.SearchVolumesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("Batman", result.Results[0].Name);
        Assert.Equal(2011, result.Results[0].StartYear);
        Assert.Equal(52, result.Results[0].IssueCount);
        var publisher = result.Results[0].Publisher;
        Assert.NotNull(publisher);
        Assert.Equal("DC Comics", publisher!.Name);
    }

    [Fact]
    public async Task SearchVolumesAsync_WithNoApiKey_ReturnsError()
    {
        // Arrange
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComicVineSettings?)null);

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.SearchVolumesAsync("Batman");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Error?.ToLower() ?? "");
    }

    [Fact]
    public async Task GetVolumeAsync_WithValidId_ReturnsVolume()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "limit": 1,
            "offset": 0,
            "number_of_page_results": 1,
            "number_of_total_results": 1,
            "status_code": 1,
            "results": {
                "id": 18166,
                "name": "Batman",
                "aliases": "The Dark Knight\nCaped Crusader",
                "start_year": 2011,
                "description": "<p>Batman is a DC comic series.</p>",
                "deck": "The New 52 Batman series",
                "publisher": { "id": 10, "name": "DC Comics", "api_detail_url": "https://comicvine.gamespot.com/api/publisher/4010-10/" },
                "count_of_issues": 52,
                "first_issue": { "id": 324500, "name": "Court of Owls", "issue_number": "1" },
                "last_issue": { "id": 484834, "name": "Superheavy", "issue_number": "52" },
                "image": { 
                    "medium_url": "https://example.com/batman_medium.jpg",
                    "original_url": "https://example.com/batman.jpg"
                }
            }
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.GetVolumeAsync(18166);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(18166, result.Data.Id);
        Assert.Equal("Batman", result.Data.Name);
        Assert.Equal(2011, result.Data.StartYear);
        Assert.Equal(52, result.Data.IssueCount);
        Assert.Contains("The Dark Knight", result.Data.Aliases);
        Assert.Contains("Caped Crusader", result.Data.Aliases);
        Assert.Equal("Batman is a DC comic series.", result.Data.Description); // HTML stripped
        Assert.Equal("The New 52 Batman series", result.Data.Deck);
        Assert.NotNull(result.Data.Publisher);
        Assert.Equal("DC Comics", result.Data.Publisher.Name);
        Assert.NotNull(result.Data.FirstIssue);
        Assert.Equal("1", result.Data.FirstIssue.IssueNumber);
        Assert.NotNull(result.Data.LastIssue);
        Assert.Equal("52", result.Data.LastIssue.IssueNumber);
    }

    [Fact]
    public async Task GetIssueAsync_WithValidId_ReturnsIssue()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "limit": 1,
            "offset": 0,
            "number_of_page_results": 1,
            "number_of_total_results": 1,
            "status_code": 1,
            "results": {
                "id": 324500,
                "name": "Court of Owls",
                "issue_number": "1",
                "description": "<p>Batman discovers a secret society.</p>",
                "cover_date": "2011-11-01",
                "store_date": "2011-09-21",
                "volume": { "id": 18166, "name": "Batman" },
                "image": { "medium_url": "https://example.com/issue1.jpg" },
                "story_arc_credits": [
                    { "id": 55766, "name": "Night of the Owls" }
                ]
            }
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.GetIssueAsync(324500);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(324500, result.Data.Id);
        Assert.Equal("Court of Owls", result.Data.Name);
        Assert.Equal("1", result.Data.IssueNumber);
        Assert.NotNull(result.Data.CoverDate);
        Assert.NotNull(result.Data.StoreDate);
        Assert.NotNull(result.Data.Volume);
        Assert.Equal("Batman", result.Data.Volume.Name);
        Assert.Single(result.Data.StoryArcs);
        Assert.Equal("Night of the Owls", result.Data.StoryArcs[0].Name);
    }

    [Fact]
    public async Task GetVolumeIssuesAsync_WithValidVolumeId_ReturnsIssues()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "limit": 100,
            "offset": 0,
            "number_of_page_results": 3,
            "number_of_total_results": 52,
            "status_code": 1,
            "results": [
                { "id": 324500, "issue_number": "1", "name": "Court of Owls" },
                { "id": 324501, "issue_number": "2", "name": "Trust Fall" },
                { "id": 324502, "issue_number": "3", "name": "The Thirteenth Hour" }
            ]
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.GetVolumeIssuesAsync(18166);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Results.Count);
        Assert.Equal(52, result.TotalResults);
        Assert.Equal("1", result.Results[0].IssueNumber);
        Assert.Equal("2", result.Results[1].IssueNumber);
        Assert.Equal("3", result.Results[2].IssueNumber);
    }

    [Fact]
    public async Task GetPublisherAsync_WithValidId_ReturnsPublisher()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "error": "OK",
            "status_code": 1,
            "results": {
                "id": 10,
                "name": "DC Comics",
                "aliases": "Detective Comics\nDC",
                "description": "<p>DC Comics is a major American comic book publisher.</p>",
                "image": { "medium_url": "https://example.com/dc.jpg" },
                "site_detail_url": "https://comicvine.gamespot.com/dc-comics/4010-10/"
            }
        }
        """;

        var handlerMock = CreateMockHandler(HttpStatusCode.OK, response);
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.GetPublisherAsync(10);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.Id);
        Assert.Equal("DC Comics", result.Data.Name);
        Assert.Contains("Detective Comics", result.Data.Aliases);
        Assert.Contains("DC", result.Data.Aliases);
    }

    [Fact]
    public void GetRateLimitStatus_ReturnsStatus()
    {
        // Arrange
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handlerMock.Object);

        // Act
        var status = client.GetRateLimitStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(200, status.RequestLimit);
        Assert.True(status.RequestsUsed >= 0);
        Assert.True(status.WindowResetTime > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task SearchVolumesAsync_CachesResults()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var response = """
        {
            "status_code": 1,
            "results": [{ "id": 1, "name": "Test" }],
            "number_of_page_results": 1,
            "number_of_total_results": 1
        }
        """;

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response)
                };
            });

        var client = CreateClient(handlerMock.Object);

        // Act
        await client.SearchVolumesAsync("Test");
        await client.SearchVolumesAsync("Test");
        await client.SearchVolumesAsync("Test");

        // Assert - Should only make one HTTP request due to caching
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task TestConnectionAsync_WithRateLimitResponse_ThrowsRateLimitException()
    {
        // Arrange
        var settings = new ComicVineSettings { ApiKey = "test-api-key", Enabled = true };
        _settingsServiceMock.Setup(s => s.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var handlerMock = CreateMockHandler(HttpStatusCode.TooManyRequests, "Rate limit exceeded");
        var client = CreateClient(handlerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rate limit", result.Message.ToLower());
    }
}

