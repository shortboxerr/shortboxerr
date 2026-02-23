using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.WalkSoftly;
using Shortboxerr.Infrastructure.WalkSoftly;
using Xunit;

namespace Shortboxerr.Tests;

public class WalkSoftlyClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<WalkSoftlyClient>> _loggerMock;

    public WalkSoftlyClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://walksoftly.itsaninja.party")
        };
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<WalkSoftlyClient>>();
    }

    private WalkSoftlyClient CreateClient() =>
        new(_httpClient, _cache, _loggerMock.Object);

    private void SetupResponse(HttpStatusCode statusCode, string content)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    #region Response Parsing Tests

    [Fact]
    public async Task GetWeeklyReleasesAsync_ParsesValidResponse()
    {
        var releases = new[]
        {
            new
            {
                series = "Batman",
                alias = (string?)null,
                issue = "#150",
                publisher = "DC Comics",
                shipdate = "2026-02-18",
                coverdate = "2026-03-01",
                comicid = 12345,
                issueid = 67890,
                weeknumber = 8,
                year = 2026,
                volume = "3",
                seriesyear = "2024",
                link = (string?)null,
                type = "Comic"
            },
            new
            {
                series = "Amazing Spider-Man",
                alias = "ASM",
                issue = "#45",
                publisher = "Marvel",
                shipdate = "2026-02-18",
                coverdate = "2026-03-01",
                comicid = 11111,
                issueid = 22222,
                weeknumber = 8,
                year = 2026,
                volume = "6",
                seriesyear = "2022",
                link = (string?)null,
                type = "Comic"
            }
        };

        SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(releases));
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.True(result.Success);
        Assert.Equal(2, result.Releases.Count);
        
        var batman = result.Releases.First(r => r.Series == "Batman");
        Assert.Equal("#150", batman.Issue);
        Assert.Equal("DC Comics", batman.Publisher);
        Assert.Equal(12345, batman.ComicId);
        Assert.Equal(67890, batman.IssueId);
        Assert.Equal(new DateTime(2026, 2, 18), batman.ShipDate);
        Assert.Equal("3", batman.Volume);
        Assert.Equal("Comic", batman.Format);
        
        var spiderman = result.Releases.First(r => r.Series == "Amazing Spider-Man");
        Assert.Equal("ASM", spiderman.Alias);
        Assert.Equal("#45", spiderman.Issue);
        Assert.Equal("Marvel", spiderman.Publisher);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_ReturnsEmptyListOnEmptyResponse()
    {
        SetupResponse(HttpStatusCode.OK, "[]");
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.True(result.Success);
        Assert.Empty(result.Releases);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_HandlesNullFields()
    {
        var releases = new[]
        {
            new
            {
                series = "Test Series",
                alias = (string?)null,
                issue = "#1",
                publisher = "Test Publisher",
                shipdate = (string?)null,
                coverdate = (string?)null,
                comicid = (int?)null,
                issueid = (int?)null,
                weeknumber = 8,
                year = 2026,
                volume = (string?)null,
                seriesyear = (string?)null,
                link = (string?)null,
                type = (string?)null
            }
        };

        SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(releases));
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.True(result.Success);
        Assert.Single(result.Releases);
        var release = result.Releases[0];
        Assert.Equal("Test Series", release.Series);
        Assert.Null(release.ShipDate);
        Assert.Null(release.ComicId);
        Assert.Null(release.Format);
    }

    #endregion

    #region Error Handling Tests

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GetWeeklyReleasesAsync_HandlesHttpErrors(HttpStatusCode statusCode)
    {
        SetupResponse(statusCode, "Server error");
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        Assert.Equal((int)statusCode, result.StatusCode);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_HandlesNetworkError()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.False(result.Success);
        Assert.Contains("Network error", result.Error);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_HandlesCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request cancelled", null, cts.Token));
        
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWeeklyReleasesAsync_HandlesWalkSoftlyErrorCodes()
    {
        SetupResponse((HttpStatusCode)522, "");
        var client = CreateClient();

        var result = await client.GetWeeklyReleasesAsync(8, 2026);

        Assert.False(result.Success);
        Assert.Contains("offline", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetWeeklyReleasesAsync_CachesResponse()
    {
        var releases = new[] { new { series = "Batman", issue = "#1", publisher = "DC", shipdate = "2026-02-18", weeknumber = 8, year = 2026 } };
        SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(releases));
        var client = CreateClient();

        // First call
        var result1 = await client.GetWeeklyReleasesAsync(8, 2026);
        Assert.False(result1.FromCache);

        // Second call should be cached
        var result2 = await client.GetWeeklyReleasesAsync(8, 2026);
        Assert.True(result2.FromCache);
        
        // Verify only one HTTP call was made
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region IsAvailable Tests

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrueOnSuccess()
    {
        SetupResponse(HttpStatusCode.OK, "[]");
        var client = CreateClient();

        var result = await client.IsAvailableAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalseOnError()
    {
        SetupResponse(HttpStatusCode.ServiceUnavailable, "");
        var client = CreateClient();

        var result = await client.IsAvailableAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalseOnNetworkError()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        
        var client = CreateClient();

        var result = await client.IsAvailableAsync();

        Assert.False(result);
    }

    #endregion
}

public class PublisherFilterTests
{
    [Theory]
    [InlineData("DC Comics", new[] { "DC Comics" }, true)]
    [InlineData("Marvel", new[] { "DC Comics" }, false)]
    [InlineData("DC Comics", new[] { "dc comics" }, true)]  // Case insensitive
    [InlineData("Kodansha Comics", new[] { "*Manga*" }, false)]  // No match
    [InlineData("Yen Press Manga", new[] { "*Manga*" }, true)]  // Wildcard match
    [InlineData("Manga Plus", new[] { "*Manga*" }, true)]  // Wildcard match at start
    [InlineData("Seven Seas Entertainment", new[] { "Seven*" }, true)]  // Wildcard at end
    [InlineData("Dark Horse Comics", new[] { "*Horse*" }, true)]  // Wildcard in middle
    [InlineData(null, new[] { "DC Comics" }, false)]  // Null publisher
    [InlineData("DC Comics", null, false)]  // Null list
    public void ShouldIgnore_WorksCorrectly(string? publisher, string[]? ignored, bool expected)
    {
        var result = PublisherFilter.ShouldIgnore(publisher, ignored);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FilterByPublisher_RemovesIgnoredPublishers()
    {
        var releases = new List<WalkSoftlyRelease>
        {
            new() { Series = "Batman", Publisher = "DC Comics" },
            new() { Series = "Spider-Man", Publisher = "Marvel" },
            new() { Series = "Naruto", Publisher = "Viz Media Manga" },
            new() { Series = "One Piece", Publisher = "Shonen Jump Manga" }
        };
        
        var ignored = new[] { "DC Comics", "*Manga*" };

        var filtered = PublisherFilter.FilterByPublisher(releases, ignored);

        Assert.Single(filtered);
        Assert.Equal("Spider-Man", filtered[0].Series);
    }

    [Fact]
    public void FilterByPublisher_ReturnsAllWhenNoIgnored()
    {
        var releases = new List<WalkSoftlyRelease>
        {
            new() { Series = "Batman", Publisher = "DC Comics" },
            new() { Series = "Spider-Man", Publisher = "Marvel" }
        };

        var filtered = PublisherFilter.FilterByPublisher(releases, null);

        Assert.Equal(2, filtered.Count);
    }
}
