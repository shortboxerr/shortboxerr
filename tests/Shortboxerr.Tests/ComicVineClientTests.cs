using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;

namespace Shortboxerr.Tests;

/// <summary>
/// Conformance tests for the ComicVine API client.
/// Tests mock ComicVine responses, rate limiting, and error handling.
/// </summary>
public class ComicVineClientTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<ComicVineClient>> _mockLogger;
    private const string TestApiKey = "test-api-key-12345";

    public ComicVineClientTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<ComicVineClient>>();

        // Default setup: configured with API key
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { ApiKey = TestApiKey, Enabled = true });
    }

    #region Test Connection Tests

    [Fact]
    public async Task TestConnectionAsync_WithValidApiKey_ReturnsSuccess()
    {
        // Arrange
        var mockResponse = CreateMockApiResponse(1, new List<object>());
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Contains("successful", result.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task TestConnectionAsync_WithNoApiKey_ReturnsFailure()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { ApiKey = "", Enabled = false });

        var client = CreateClientWithMockedHttp(CreateMockApiResponse<List<object>>(1, new List<object>()));

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidApiKey_ReturnsError()
    {
        // Arrange: ComicVine returns status code 100 for invalid key
        var mockResponse = CreateMockApiResponse<object>(100, null, "Invalid API Key");
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid API Key", result.Message);
    }

    #endregion

    #region Search Volumes Tests

    [Fact]
    public async Task SearchVolumesAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var volumes = new List<object>
        {
            new { id = 12345, name = "Batman", start_year = "2016", count_of_issues = 100 },
            new { id = 12346, name = "Batman: Rebirth", start_year = "2016", count_of_issues = 1 }
        };
        var mockResponse = CreateMockSearchResponse(1, volumes, 2);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.SearchVolumesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalResults);
        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public async Task SearchVolumesAsync_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var mockResponse = CreateMockSearchResponse(1, new List<object>(), 0);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.SearchVolumesAsync("xyznonexistent123");

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalResults);
    }

    [Fact]
    public async Task SearchVolumesAsync_WithEmptyQuery_ReturnsEmptyResults()
    {
        // Arrange - API still processes empty query but returns no results
        var mockResponse = CreateMockSearchResponse(1, new List<object>(), 0);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.SearchVolumesAsync("");

        // Assert - The API doesn't reject empty queries, just returns empty results
        Assert.True(result.Success);
        Assert.Empty(result.Results);
    }

    #endregion

    #region Get Volume Tests

    [Fact]
    public async Task GetVolumeAsync_WithValidId_ReturnsVolume()
    {
        // Arrange
        var volume = CreateMockVolumeResponse(12345, "Batman", 2016, "DC Comics", 100);
        var mockResponse = CreateMockSingleResponse(1, volume);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.GetVolumeAsync(12345);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Batman", result.Data.Name);
        Assert.Equal(2016, result.Data.StartYear);
    }

    [Fact]
    public async Task GetVolumeAsync_WithInvalidId_ReturnsNotFound()
    {
        // Arrange: ComicVine returns status 101 for "Object not found"
        var mockResponse = CreateMockApiResponse<object>(101, null, "Object Not Found");
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.GetVolumeAsync(99999999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error?.ToLowerInvariant() ?? "");
    }

    #endregion

    #region Get Issue Tests

    [Fact]
    public async Task GetIssueAsync_WithValidId_ReturnsIssue()
    {
        // Arrange
        var issue = CreateMockIssueResponse(54321, "1", "Pilot", 12345, "Batman");
        var mockResponse = CreateMockSingleResponse(1, issue);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.GetIssueAsync(54321);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("1", result.Data.IssueNumber);
        Assert.Equal("Pilot", result.Data.Name);
    }

    [Fact]
    public async Task GetIssueAsync_WithDecimalIssueNumber_ParsesCorrectly()
    {
        // Arrange
        var issue = CreateMockIssueResponse(54321, "0.5", "Half Issue", 12345, "Batman");
        var mockResponse = CreateMockSingleResponse(1, issue);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.GetIssueAsync(54321);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("0.5", result.Data.IssueNumber);
    }

    #endregion

    #region Get Volume Issues Tests

    [Fact]
    public async Task GetVolumeIssuesAsync_WithValidVolumeId_ReturnsIssues()
    {
        // Arrange
        var issues = new List<object>
        {
            CreateMockIssueResponse(1, "1", null, 12345, "Batman"),
            CreateMockIssueResponse(2, "2", "Chapter Two", 12345, "Batman"),
            CreateMockIssueResponse(3, "3", null, 12345, "Batman")
        };
        var mockResponse = CreateMockSearchResponse(1, issues, 3);
        var client = CreateClientWithMockedHttp(mockResponse);

        // Act
        var result = await client.GetVolumeIssuesAsync(12345);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalResults);
        Assert.NotEmpty(result.Results);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ApiCall_With404Response_ThrowsHttpRequestException()
    {
        // Arrange
        var client = CreateClientWithMockedHttpStatus(HttpStatusCode.NotFound);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetVolumeAsync(12345));
    }

    [Fact]
    public async Task ApiCall_With500Response_ThrowsHttpRequestException()
    {
        // Arrange
        var client = CreateClientWithMockedHttpStatus(HttpStatusCode.InternalServerError);

        // Act & Assert
        // The client throws HttpRequestException for HTTP 5xx errors
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchVolumesAsync("test"));
    }

    [Fact]
    public async Task ApiCall_WithNetworkError_ThrowsHttpRequestException()
    {
        // Arrange
        var client = CreateClientWithNetworkError();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchVolumesAsync("test"));
    }

    [Fact]
    public async Task ApiCall_WithRateLimitResponse_ThrowsHttpRequestException()
    {
        // Arrange: ComicVine uses HTTP 420 for rate limiting
        var client = CreateClientWithMockedHttpStatus((HttpStatusCode)420);

        // Act & Assert
        // HTTP 420 is a non-success status code, so it throws
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchVolumesAsync("test"));
    }

    [Fact]
    public async Task ApiCall_WithMalformedJson_ThrowsException()
    {
        // Arrange
        var client = CreateClientWithMockedHttp("{ invalid json }}}");

        // Act & Assert
        // The client throws JsonException when parsing fails
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.SearchVolumesAsync("test"));
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public void GetRateLimitStatus_ReturnsValidStatus()
    {
        // Arrange
        var client = CreateClientWithMockedHttp(CreateMockApiResponse(1, new List<object>()));

        // Act
        var status = client.GetRateLimitStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(200, status.RequestLimit);
        Assert.True(status.WindowResetTime >= DateTime.UtcNow);
    }

    [Fact]
    public async Task IsConfigured_AfterSuccessfulRequest_ReturnsTrue()
    {
        // Arrange
        var client = CreateClientWithMockedHttp(CreateMockSearchResponse(1, new List<object> { new { id = 1 } }, 1));

        // Act - Make a request first to populate cache
        await client.SearchVolumesAsync("test");

        // Assert - After a request, cached key should be set
        Assert.True(client.IsConfigured);
    }

    [Fact]
    public void IsConfigured_BeforeAnyRequest_ReturnsFalse()
    {
        // Arrange - Fresh client, no requests made
        var client = CreateClientWithMockedHttp(CreateMockSearchResponse(1, new List<object>(), 0));

        // Act & Assert - No cached key yet
        Assert.False(client.IsConfigured);
    }

    #endregion

    #region Golden Test Fixtures

    /// <summary>
    /// Test that parsing a realistic ComicVine volume response works correctly.
    /// This is a "golden test" using a representative response structure.
    /// </summary>
    [Fact]
    public async Task ParseVolumeResponse_GoldenTest_Batman2016()
    {
        // Arrange: Realistic response based on actual ComicVine API structure
        var goldenResponse = @"{
            ""error"": ""OK"",
            ""limit"": 1,
            ""offset"": 0,
            ""number_of_page_results"": 1,
            ""number_of_total_results"": 1,
            ""status_code"": 1,
            ""results"": {
                ""id"": 92483,
                ""name"": ""Batman"",
                ""start_year"": ""2016"",
                ""count_of_issues"": 137,
                ""description"": ""<p>The Rebirth era Batman series...</p>"",
                ""deck"": ""The Dark Knight protects Gotham City."",
                ""publisher"": {
                    ""id"": 10,
                    ""name"": ""DC Comics"",
                    ""api_detail_url"": ""https://comicvine.gamespot.com/api/publisher/4010-10/""
                },
                ""image"": {
                    ""icon_url"": ""https://comicvine.gamespot.com/a/uploads/square_avatar/11/110017/5404879-01.jpg"",
                    ""medium_url"": ""https://comicvine.gamespot.com/a/uploads/scale_medium/11/110017/5404879-01.jpg"",
                    ""small_url"": ""https://comicvine.gamespot.com/a/uploads/scale_small/11/110017/5404879-01.jpg"",
                    ""original_url"": ""https://comicvine.gamespot.com/a/uploads/original/11/110017/5404879-01.jpg""
                },
                ""first_issue"": {
                    ""id"": 541165,
                    ""name"": ""I Am Gotham, Part One"",
                    ""issue_number"": ""1""
                },
                ""last_issue"": {
                    ""id"": 830517,
                    ""name"": ""The Bat-Man of Gotham, Part One"",
                    ""issue_number"": ""137""
                },
                ""api_detail_url"": ""https://comicvine.gamespot.com/api/volume/4050-92483/"",
                ""site_detail_url"": ""https://comicvine.gamespot.com/batman/4050-92483/"",
                ""date_added"": ""2016-05-25 14:41:03"",
                ""date_last_updated"": ""2024-01-15 08:23:45""
            },
            ""version"": ""1.0""
        }";

        var client = CreateClientWithMockedHttp(goldenResponse);

        // Act
        var result = await client.GetVolumeAsync(92483);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(92483, result.Data.Id);
        Assert.Equal("Batman", result.Data.Name);
        Assert.Equal(2016, result.Data.StartYear);
        Assert.Equal(137, result.Data.IssueCount);
        Assert.Equal("DC Comics", result.Data.Publisher?.Name);
        Assert.NotNull(result.Data.Image?.OriginalUrl);
        Assert.Equal("1", result.Data.FirstIssue?.IssueNumber);
        Assert.Equal("137", result.Data.LastIssue?.IssueNumber);
    }

    /// <summary>
    /// Test that parsing a realistic ComicVine issue response works correctly.
    /// </summary>
    [Fact]
    public async Task ParseIssueResponse_GoldenTest_Batman1()
    {
        // Arrange: Realistic response based on actual ComicVine API structure
        var goldenResponse = @"{
            ""error"": ""OK"",
            ""limit"": 1,
            ""offset"": 0,
            ""number_of_page_results"": 1,
            ""number_of_total_results"": 1,
            ""status_code"": 1,
            ""results"": {
                ""id"": 541165,
                ""name"": ""I Am Gotham, Part One"",
                ""issue_number"": ""1"",
                ""description"": ""<p>Batman faces a new threat in Gotham...</p>"",
                ""cover_date"": ""2016-08-01"",
                ""store_date"": ""2016-06-15"",
                ""volume"": {
                    ""id"": 92483,
                    ""name"": ""Batman"",
                    ""api_detail_url"": ""https://comicvine.gamespot.com/api/volume/4050-92483/""
                },
                ""image"": {
                    ""icon_url"": ""https://comicvine.gamespot.com/a/uploads/square_avatar/11/110017/5404879-01.jpg"",
                    ""original_url"": ""https://comicvine.gamespot.com/a/uploads/original/11/110017/5404879-01.jpg""
                },
                ""api_detail_url"": ""https://comicvine.gamespot.com/api/issue/4000-541165/"",
                ""site_detail_url"": ""https://comicvine.gamespot.com/batman-1-i-am-gotham-part-one/4000-541165/"",
                ""story_arc_credits"": [
                    {
                        ""id"": 56789,
                        ""name"": ""I Am Gotham"",
                        ""api_detail_url"": ""https://comicvine.gamespot.com/api/story_arc/4045-56789/""
                    }
                ],
                ""date_added"": ""2016-05-25 14:41:03"",
                ""date_last_updated"": ""2023-11-20 10:15:30""
            },
            ""version"": ""1.0""
        }";

        var client = CreateClientWithMockedHttp(goldenResponse);

        // Act
        var result = await client.GetIssueAsync(541165);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(541165, result.Data.Id);
        Assert.Equal("I Am Gotham, Part One", result.Data.Name);
        Assert.Equal("1", result.Data.IssueNumber);
        Assert.Equal(92483, result.Data.Volume?.Id);
        Assert.NotNull(result.Data.CoverDate);
        Assert.NotNull(result.Data.StoreDate);
    }

    /// <summary>
    /// Test search response with multiple results.
    /// </summary>
    [Fact]
    public async Task SearchVolumes_GoldenTest_BatmanResults()
    {
        // Arrange
        var goldenResponse = @"{
            ""error"": ""OK"",
            ""limit"": 10,
            ""offset"": 0,
            ""number_of_page_results"": 3,
            ""number_of_total_results"": 156,
            ""status_code"": 1,
            ""results"": [
                {
                    ""id"": 92483,
                    ""name"": ""Batman"",
                    ""start_year"": ""2016"",
                    ""count_of_issues"": 137,
                    ""publisher"": { ""id"": 10, ""name"": ""DC Comics"" }
                },
                {
                    ""id"": 796,
                    ""name"": ""Batman"",
                    ""start_year"": ""1940"",
                    ""count_of_issues"": 713,
                    ""publisher"": { ""id"": 10, ""name"": ""DC Comics"" }
                },
                {
                    ""id"": 18216,
                    ""name"": ""Batman"",
                    ""start_year"": ""2011"",
                    ""count_of_issues"": 52,
                    ""publisher"": { ""id"": 10, ""name"": ""DC Comics"" }
                }
            ],
            ""version"": ""1.0""
        }";

        var client = CreateClientWithMockedHttp(goldenResponse);

        // Act
        var result = await client.SearchVolumesAsync("Batman");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(156, result.TotalResults);
        Assert.Equal(3, result.NumberOfPageResults);
        Assert.Equal(3, result.Results.Count);
        
        // Verify we got different series with same name but different years
        var years = result.Results.Select(v => v.StartYear).OrderBy(y => y).ToList();
        Assert.Contains(1940, years);
        Assert.Contains(2011, years);
        Assert.Contains(2016, years);
    }

    #endregion

    #region Helper Methods

    private ComicVineClient CreateClientWithMockedHttp(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
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
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://comicvine.gamespot.com/api/")
        };

        return new ComicVineClient(
            httpClient,
            _mockSettingsService.Object,
            _memoryCache,
            _mockLogger.Object);
    }

    private ComicVineClient CreateClientWithMockedHttpStatus(HttpStatusCode statusCode)
    {
        var errorBody = statusCode switch
        {
            (HttpStatusCode)420 => @"{""error"":""Rate limit exceeded"",""status_code"":107}",
            HttpStatusCode.NotFound => @"{""error"":""Not found"",""status_code"":101}",
            _ => @"{""error"":""Server error"",""status_code"":102}"
        };

        return CreateClientWithMockedHttp(errorBody, statusCode);
    }

    private ComicVineClient CreateClientWithNetworkError()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://comicvine.gamespot.com/api/")
        };

        return new ComicVineClient(
            httpClient,
            _mockSettingsService.Object,
            _memoryCache,
            _mockLogger.Object);
    }

    private static string CreateMockApiResponse<T>(int statusCode, T? results, string? error = null)
    {
        var response = new
        {
            error = error ?? "OK",
            limit = 10,
            offset = 0,
            number_of_page_results = results is IList<object> list ? list.Count : (results != null ? 1 : 0),
            number_of_total_results = results is IList<object> listTotal ? listTotal.Count : (results != null ? 1 : 0),
            status_code = statusCode,
            results = results,
            version = "1.0"
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
        });
    }

    private static string CreateMockSearchResponse<T>(int statusCode, List<T> results, int totalResults)
    {
        var response = new
        {
            error = "OK",
            limit = 10,
            offset = 0,
            number_of_page_results = results.Count,
            number_of_total_results = totalResults,
            status_code = statusCode,
            results = results,
            version = "1.0"
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
        });
    }

    private static string CreateMockSingleResponse<T>(int statusCode, T result)
    {
        var response = new
        {
            error = "OK",
            limit = 1,
            offset = 0,
            number_of_page_results = 1,
            number_of_total_results = 1,
            status_code = statusCode,
            results = result,
            version = "1.0"
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
        });
    }

    private static object CreateMockVolumeResponse(int id, string name, int startYear, string publisher, int issueCount)
    {
        return new
        {
            id = id,
            name = name,
            start_year = startYear.ToString(),
            count_of_issues = issueCount,
            description = $"<p>Description of {name}</p>",
            deck = $"The {name} series",
            publisher = new { id = 10, name = publisher },
            image = new
            {
                icon_url = $"https://example.com/icon/{id}.jpg",
                original_url = $"https://example.com/original/{id}.jpg"
            },
            first_issue = new { id = id * 1000 + 1, name = "First Issue", issue_number = "1" },
            last_issue = new { id = id * 1000 + issueCount, name = "Last Issue", issue_number = issueCount.ToString() },
            api_detail_url = $"https://comicvine.gamespot.com/api/volume/4050-{id}/",
            site_detail_url = $"https://comicvine.gamespot.com/{name.ToLower().Replace(" ", "-")}/4050-{id}/",
            date_added = "2020-01-01 00:00:00",
            date_last_updated = "2024-01-01 00:00:00"
        };
    }

    private static object CreateMockIssueResponse(int id, string issueNumber, string? name, int volumeId, string volumeName)
    {
        return new
        {
            id = id,
            name = name,
            issue_number = issueNumber,
            description = $"<p>Issue {issueNumber} description</p>",
            cover_date = "2020-01-01",
            store_date = "2019-12-18",
            volume = new { id = volumeId, name = volumeName },
            image = new
            {
                icon_url = $"https://example.com/icon/{id}.jpg",
                original_url = $"https://example.com/original/{id}.jpg"
            },
            api_detail_url = $"https://comicvine.gamespot.com/api/issue/4000-{id}/",
            site_detail_url = $"https://comicvine.gamespot.com/issue/4000-{id}/",
            story_arc_credits = new List<object>(),
            date_added = "2020-01-01 00:00:00",
            date_last_updated = "2024-01-01 00:00:00"
        };
    }

    #endregion
}
