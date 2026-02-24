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
    public async Task GetCoverAsync_ReturnsLocgCover_WhenFound()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new()
                    {
                        SeriesName = "Batman",
                        IssueNumber = "105",
                        CoverUrl = "https://s3.amazonaws.com/comicgeeks/comics/covers/large-12345.jpg",
                        Publisher = "DC Comics"
                    }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("Batman", "105", "DC Comics");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.LeagueOfComicGeeks, result.Source);
        Assert.Contains("comicgeeks", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsVolumeCover_WhenLocgFails()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult { Success = false, Error = "Not found" });

        var service = CreateService();
        var result = await service.GetCoverAsync(
            "UnknownSeries",
            "1",
            volumeCoverUrl: "https://comicvine.com/volume-cover.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Contains("volume-cover", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsNotFound_WhenAllSourcesFail()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult { Success = false });

        var service = CreateService();
        var result = await service.GetCoverAsync("UnknownSeries", "1");

        Assert.False(result.Success);
        Assert.Equal(CoverSource.None, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsCachedResult_OnSecondCall()
    {
        var uniqueSeries = $"CacheTestSeries_{Guid.NewGuid():N}";
        var locgMock = new Mock<ILeagueOfComicGeeksClient>();
        
        locgMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new()
                    {
                        SeriesName = uniqueSeries,
                        IssueNumber = "1",
                        CoverUrl = "https://s3.amazonaws.com/test.jpg"
                    }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(locgMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverAsync(uniqueSeries, "1");
        
        Assert.True(result1.Success, "First call should succeed");
        Assert.False(result1.FromCache, "First call should not be from cache");

        var result2 = await service.GetCoverAsync(uniqueSeries, "1");
        
        Assert.True(result2.Success, "Second call should succeed");
        Assert.True(result2.FromCache, "Second call should be from cache");
        
        locgMock.Verify(
            c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCoverAsync_MatchesIssueNumber_Correctly()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = "Spider-Man", IssueNumber = "1", CoverUrl = "https://test1.jpg" },
                    new() { SeriesName = "Spider-Man", IssueNumber = "10", CoverUrl = "https://test10.jpg" },
                    new() { SeriesName = "Spider-Man", IssueNumber = "100", CoverUrl = "https://test100.jpg" }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("Spider-Man", "10");

        Assert.True(result.Success);
        Assert.Contains("test10", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesIssueNumberWithHash()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = "X-Men", IssueNumber = "5", CoverUrl = "https://xmen5.jpg" }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("X-Men", "#5");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetCoverAsync_MatchesPublisher_WhenProvided()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = "Batman", IssueNumber = "1", CoverUrl = "https://urban.jpg", Publisher = "Urban Comics" },
                    new() { SeriesName = "Batman", IssueNumber = "1", CoverUrl = "https://dc.jpg", Publisher = "DC Comics" }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("Batman", "1", "DC Comics");

        Assert.True(result.Success);
        Assert.Contains("dc.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesLocgException_Gracefully()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService();
        var result = await service.GetCoverAsync(
            "Batman",
            "1",
            volumeCoverUrl: "https://volume-fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_TracksTiming()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult { Success = false });

        var service = CreateService();
        var result = await service.GetCoverAsync("Test", "1", volumeCoverUrl: "https://test.jpg");

        Assert.True(result.ResolutionTimeMs >= 0);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsValidStats()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult { Success = false });

        var service = CreateService();
        
        await service.GetCoverAsync("Test1", "1", volumeCoverUrl: "https://v1.jpg");
        await service.GetCoverAsync("Test2", "2");

        var stats = await service.GetStatsAsync();

        Assert.Equal(2, stats.TotalRequests);
        Assert.Equal(1, stats.ComicVineVolumeHits);
        Assert.Equal(1, stats.Misses);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesCachedEntry()
    {
        var uniqueSeries = $"ClearTest_{Guid.NewGuid():N}";
        
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = uniqueSeries, IssueNumber = "1", CoverUrl = "https://test.jpg" }
                }
            });

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_locgClientMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverAsync(uniqueSeries, "1");
        Assert.False(result1.FromCache);

        await service.ClearCacheAsync(uniqueSeries, "1");

        var result2 = await service.GetCoverAsync(uniqueSeries, "1");
        Assert.False(result2.FromCache);
        
        _locgClientMock.Verify(
            c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetCoverAsync_HandlesFuzzySeriesNameMatch()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = "The Amazing Spider-Man", IssueNumber = "5", CoverUrl = "https://asm.jpg" }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("Amazing Spider-Man", "5");

        Assert.True(result.Success);
        Assert.Contains("asm", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_RejectsLowSimilarityMatches()
    {
        _locgClientMock
            .Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocgSearchResult
            {
                Success = true,
                Issues = new List<LocgIssue>
                {
                    new() { SeriesName = "Completely Different Series", IssueNumber = "1", CoverUrl = "https://wrong.jpg" }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync("Batman", "1");

        Assert.False(result.Success);
    }
}
