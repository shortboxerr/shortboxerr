using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.LeagueOfComicGeeks;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Services;
using Xunit;

namespace Shortboxerr.Tests;

public class CoverFallbackServiceTests
{
    private readonly Mock<ILeagueOfComicGeeksClient> _locgClientMock;
    private readonly Mock<ILogger<CoverFallbackService>> _loggerMock;
    private readonly IMemoryCache _cache;

    public CoverFallbackServiceTests()
    {
        _locgClientMock = new Mock<ILeagueOfComicGeeksClient>();
        _loggerMock = new Mock<ILogger<CoverFallbackService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private CoverFallbackService CreateService()
    {
        return new CoverFallbackService(_locgClientMock.Object, _cache, _loggerMock.Object);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsLocgCover_WhenMatchFound()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 123, SeriesName = "Batman", IssueNumber = "100", CoverUrl = "https://example.com/cover.jpg" }
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "100");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
        Assert.Equal("https://example.com/cover.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsVolumeCover_WhenLocgFails()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = false,
                Error = "Service unavailable"
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "100", volumeCoverUrl: "https://comicvine.com/volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://comicvine.com/volume.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsNotFound_WhenNoCoversAvailable()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>()
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("NonExistentSeries", "999");

        Assert.False(result.Success);
        Assert.Equal(CoverSource.None, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsCachedResult_OnSecondCall()
    {
        var uniqueSeries = $"CacheTest_{Guid.NewGuid():N}";
        var callCount = 0;

        var locgClientMock = new Mock<ILeagueOfComicGeeksClient>();
        locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 456, SeriesName = uniqueSeries, IssueNumber = "50", CoverUrl = "https://example.com/spidey.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(locgClientMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverAsync(uniqueSeries, "50");
        
        Assert.True(result1.Success, $"First call failed: {result1.Error}");
        Assert.False(result1.FromCache, "First result should not be from cache");
        Assert.Equal(1, callCount);

        var result2 = await service.GetCoverAsync(uniqueSeries, "50");
        
        Assert.True(result2.Success, $"Second call failed: {result2.Error}");
        Assert.True(result2.FromCache, "Second result should be from cache");
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetCoverAsync_MatchesFuzzySeriesNames()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 789, SeriesName = "Absolute Wonder Woman", IssueNumber = "17", CoverUrl = "https://example.com/aww.jpg" }
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Absolute Wonder-Woman", "17");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesException_Gracefully()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "1", volumeCoverUrl: "https://fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://fallback.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_PrefersPublisherMatch()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Batman", IssueNumber = "100", Publisher = "Urban Comics", CoverUrl = "https://urban.jpg" },
                    new() { IssueId = 2, SeriesName = "Batman", IssueNumber = "100", Publisher = "DC Comics", CoverUrl = "https://dc.jpg" }
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "100", publisher: "DC");

        Assert.True(result.Success);
        Assert.Equal("https://dc.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsAccurateStatistics()
    {
        var locgClientMock = new Mock<ILeagueOfComicGeeksClient>();
        
        locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string seriesName, string? issueNum, CancellationToken _) => new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = seriesName, IssueNumber = issueNum ?? "1", CoverUrl = "https://test.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(locgClientMock.Object, freshCache, _loggerMock.Object);

        await service.GetCoverAsync("Batman", "1");
        await service.GetCoverAsync("Superman", "2");
        await service.GetCoverAsync("Wonder Woman", "3");

        var stats = await service.GetStatsAsync();

        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(3, stats.LocgHits);
        Assert.Equal(0, stats.Misses);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesCachedEntry()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "ClearTest", IssueNumber = "1", CoverUrl = "https://test.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_locgClientMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverAsync("ClearTest", "1");
        Assert.False(result1.FromCache);

        var result2 = await service.GetCoverAsync("ClearTest", "1");
        Assert.True(result2.FromCache);

        await service.ClearCacheAsync("ClearTest", "1");

        var result3 = await service.GetCoverAsync("ClearTest", "1");
        Assert.False(result3.FromCache);
    }

    [Fact]
    public async Task GetCoverAsync_NormalizesIssueNumber()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Test", IssueNumber = "10", CoverUrl = "https://test.jpg" }
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Test", "#10");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_FallsBackToVolume_WhenLocgReturnsEmpty()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>() // Empty list
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "999", volumeCoverUrl: "https://volume-fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://volume-fallback.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesNullIssuesList_Gracefully()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = null! // Null issues list
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Test", "1", volumeCoverUrl: "https://fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesIssueWithNullCoverUrl()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Test", IssueNumber = "1", CoverUrl = null } // No cover URL
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Test", "1", volumeCoverUrl: "https://volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_VerifiesPriorityOrder_LocgBeforeVolume()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Batman", IssueNumber = "1", CoverUrl = "https://locg.jpg" }
                }
            });

        var service = CreateService();

        // Both LOCG and volume cover available
        var result = await service.GetCoverAsync("Batman", "1", volumeCoverUrl: "https://volume.jpg");

        // LOCG should win
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
        Assert.Equal("https://locg.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesMalformedIssueNumber()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Test", IssueNumber = "½", CoverUrl = "https://test.jpg" }
                }
            });

        var service = CreateService();

        // Search with various malformed inputs
        var result = await service.GetCoverAsync("Test", "½");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_TracksResolutionTime()
    {
        _locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Test", IssueNumber = "1", CoverUrl = "https://test.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_locgClientMock.Object, freshCache, _loggerMock.Object);

        var result = await service.GetCoverAsync("Test", "1");

        Assert.True(result.ResolutionTimeMs >= 0);
    }

    [Fact]
    public async Task GetStatsAsync_ReportsCacheHitRatio()
    {
        var locgClientMock = new Mock<ILeagueOfComicGeeksClient>();
        locgClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { IssueId = 1, SeriesName = "Test", IssueNumber = "1", CoverUrl = "https://test.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(locgClientMock.Object, freshCache, _loggerMock.Object);

        // First call (cache miss)
        await service.GetCoverAsync("Test", "1");
        // Second call (cache hit)
        await service.GetCoverAsync("Test", "1");
        // Third call (cache hit)
        await service.GetCoverAsync("Test", "1");

        var stats = await service.GetStatsAsync();

        Assert.Equal(3, stats.TotalRequests);
        Assert.True(stats.CacheHitRatio >= 0.5); // At least 2/3 should be cache hits
    }
}
