using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Metron;
using Shortboxerr.Infrastructure.Metron;
using Xunit;

namespace Shortboxerr.Tests;

public class MetronClientTests
{
    private readonly Mock<ILogger<MetronClient>> _loggerMock;
    private readonly IMemoryCache _cache;
    private readonly MetronSettings _defaultSettings;

    public MetronClientTests()
    {
        _loggerMock = new Mock<ILogger<MetronClient>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _defaultSettings = new MetronSettings
        {
            Enabled = true,
            Username = "testuser",
            Password = "testpass",
            CacheTtlHours = 24,
            TimeoutSeconds = 30
        };
    }

    private MetronClient CreateClient(HttpClient httpClient, MetronSettings? settings = null)
    {
        var options = Options.Create(settings ?? _defaultSettings);
        return new MetronClient(httpClient, _cache, options, _loggerMock.Object);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string? content = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content != null ? new StringContent(content) : null
            });

        return new HttpClient(handlerMock.Object);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenCredentialsProvided()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient);

        Assert.True(client.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenCredentialsMissing()
    {
        var settings = new MetronSettings
        {
            Enabled = true,
            Username = null,
            Password = null
        };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient, settings);

        Assert.False(client.IsConfigured);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ReturnsIssue_WhenFound()
    {
        var apiResponse = new
        {
            count = 1,
            results = new[]
            {
                new
                {
                    id = 12345,
                    number = "100",
                    cover_date = "2026-01-01",
                    store_date = "2026-01-10",
                    image = "https://metron.cloud/media/issue/cover.jpg",
                    cv_id = 67890,
                    series = new
                    {
                        id = 100,
                        name = "Batman",
                        volume = 3,
                        year_began = 2016,
                        publisher = new { id = 1, name = "DC Comics" }
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(67890);

        Assert.True(result.Success);
        Assert.NotNull(result.Issue);
        Assert.Equal(12345, result.Issue.Id);
        Assert.Equal("100", result.Issue.Number);
        Assert.Equal("https://metron.cloud/media/issue/cover.jpg", result.Issue.ImageUrl);
        Assert.Equal(67890, result.Issue.CvId);
        Assert.NotNull(result.Issue.Series);
        Assert.Equal("Batman", result.Issue.Series.Name);
        Assert.Equal("DC Comics", result.Issue.Series.Publisher?.Name);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ReturnsNotFound_WhenNoResults()
    {
        var apiResponse = new { count = 0, results = Array.Empty<object>() };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(99999);

        Assert.False(result.Success);
        Assert.Contains("No issue found", result.Error);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ReturnsFailed_WhenUnauthorized()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(12345);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Authentication failed", result.Error);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ReturnsFailed_WhenDisabled()
    {
        var settings = new MetronSettings
        {
            Enabled = false,
            Username = "user",
            Password = "pass"
        };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient, settings);

        var result = await client.GetIssueByCvIdAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ReturnsFailed_WhenNotConfigured()
    {
        var settings = new MetronSettings
        {
            Enabled = true,
            Username = null,
            Password = null
        };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient, settings);

        var result = await client.GetIssueByCvIdAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("not configured", result.Error);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_UsesCachedResult_OnSecondCall()
    {
        var apiResponse = new
        {
            count = 1,
            results = new[]
            {
                new
                {
                    id = 12345,
                    number = "100",
                    image = "https://metron.cloud/media/issue/cover.jpg",
                    cv_id = 67890
                }
            }
        };

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => callCount++)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(apiResponse))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(_defaultSettings);
        var client = new MetronClient(httpClient, freshCache, options, _loggerMock.Object);

        var result1 = await client.GetIssueByCvIdAsync(67890);
        Assert.True(result1.Success);
        Assert.False(result1.FromCache);
        Assert.Equal(1, callCount);

        var result2 = await client.GetIssueByCvIdAsync(67890);
        Assert.True(result2.Success);
        Assert.True(result2.FromCache);
        Assert.Equal(1, callCount); // Should not make another HTTP call
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsIssues_WhenFound()
    {
        var apiResponse = new
        {
            count = 2,
            results = new[]
            {
                new
                {
                    id = 1,
                    number = "1",
                    image = "https://metron.cloud/media/issue/cover1.jpg",
                    series = new { id = 100, name = "Batman", publisher = new { id = 1, name = "DC Comics" } }
                },
                new
                {
                    id = 2,
                    number = "1",
                    image = "https://metron.cloud/media/issue/cover2.jpg",
                    series = new { id = 200, name = "Batman", publisher = new { id = 2, name = "Urban Comics" } }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.SearchIssueAsync("Batman", "1");

        Assert.True(result.Success);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsEmpty_WhenNoResults()
    {
        var apiResponse = new { count = 0, results = Array.Empty<object>() };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.SearchIssueAsync("NonExistentSeries", "999");

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchIssueAsync_ReturnsFailed_WhenUnauthorized()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);
        var client = CreateClient(httpClient);

        var result = await client.SearchIssueAsync("Batman", "1");

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task SearchIssueAsync_TrimsHashFromIssueNumber()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"count\": 0, \"results\": []}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = CreateClient(httpClient);

        await client.SearchIssueAsync("Batman", "#100");

        Assert.NotNull(capturedRequest);
        Assert.Contains("number=100", capturedRequest.RequestUri?.Query);
        Assert.DoesNotContain("%23", capturedRequest.RequestUri?.Query); // %23 is URL-encoded #
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_HandlesNetworkError_Gracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object);
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("Network error", result.Error);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenServiceResponds()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{\"count\": 0, \"results\": []}");
        var client = CreateClient(httpClient);

        var result = await client.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenNotConfigured()
    {
        var settings = new MetronSettings
        {
            Enabled = true,
            Username = null,
            Password = null
        };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient, settings);

        var result = await client.IsAvailableAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenDisabled()
    {
        var settings = new MetronSettings
        {
            Enabled = false,
            Username = "user",
            Password = "pass"
        };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var client = CreateClient(httpClient, settings);

        var result = await client.IsAvailableAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_ParsesDates_Correctly()
    {
        var apiResponse = new
        {
            count = 1,
            results = new[]
            {
                new
                {
                    id = 12345,
                    number = "100",
                    cover_date = "2026-06-15",
                    store_date = "2026-06-10",
                    image = "https://metron.cloud/media/issue/cover.jpg",
                    cv_id = 67890
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(67890);

        Assert.True(result.Success);
        Assert.NotNull(result.Issue);
        Assert.Equal(new DateTime(2026, 6, 15), result.Issue.CoverDate);
        Assert.Equal(new DateTime(2026, 6, 10), result.Issue.StoreDate);
    }

    [Fact]
    public async Task GetIssueByCvIdAsync_HandlesNullDates()
    {
        var apiResponse = new
        {
            count = 1,
            results = new[]
            {
                new
                {
                    id = 12345,
                    number = "100",
                    cover_date = (string?)null,
                    store_date = (string?)null,
                    image = "https://metron.cloud/media/issue/cover.jpg",
                    cv_id = 67890
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(apiResponse));
        var client = CreateClient(httpClient);

        var result = await client.GetIssueByCvIdAsync(67890);

        Assert.True(result.Success);
        Assert.NotNull(result.Issue);
        Assert.Null(result.Issue.CoverDate);
        Assert.Null(result.Issue.StoreDate);
    }
}
