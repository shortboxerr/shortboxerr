using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Services;
using Xunit;

namespace Shortboxerr.Tests;

public class CoverFallbackServiceTests
{
    private readonly Mock<IMetronClient> _metronClientMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<ILogger<CoverFallbackService>> _loggerMock;
    private readonly IMemoryCache _cache;

    public CoverFallbackServiceTests()
    {
        _metronClientMock = new Mock<IMetronClient>();
        _metronClientMock.Setup(c => c.IsConfigured).Returns(true);
        _settingsServiceMock = new Mock<ISettingsService>();
        _settingsServiceMock
            .Setup(s => s.GetAsync<MetronSettings>("metron", It.IsAny<MetronSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSettings { MinMatchConfidence = 85 });
        _loggerMock = new Mock<ILogger<CoverFallbackService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private CoverFallbackService CreateService()
    {
        return new CoverFallbackService(_metronClientMock.Object, _settingsServiceMock.Object, _cache, _loggerMock.Object);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsMetronCover_WhenFound()
    {
        var metronIssue = new MetronIssue
        {
            Id = 12345,
            CvId = 67890,
            ImageUrl = "https://metron.cloud/media/issue/cover.jpg",
            Series = new MetronSeries { Name = "Batman" },
            Number = "100"
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(67890, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(67890);

        Assert.True(result.Success);
        Assert.Equal(CoverSource.Metron, result.Source);
        Assert.Equal("https://metron.cloud/media/issue/cover.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsVolumeCover_WhenMetronFails()
    {
        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.NotFound("Not found"));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(12345, volumeCoverUrl: "https://comicvine.com/volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://comicvine.com/volume.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsNotFound_WhenNoCoversAvailable()
    {
        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.NotFound());

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(12345);

        Assert.False(result.Success);
        Assert.Equal(CoverSource.None, result.Source);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsCachedResult_OnSecondCall()
    {
        var metronIssue = new MetronIssue
        {
            Id = 12345,
            CvId = 67890,
            ImageUrl = "https://metron.cloud/media/issue/cover.jpg"
        };

        var callCount = 0;
        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(67890, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_metronClientMock.Object, _settingsServiceMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverByCvIdAsync(67890);
        
        Assert.True(result1.Success, $"First call failed: {result1.Error}");
        Assert.False(result1.FromCache, "First result should not be from cache");
        Assert.Equal(1, callCount);

        var result2 = await service.GetCoverByCvIdAsync(67890);
        
        Assert.True(result2.Success, $"Second call failed: {result2.Error}");
        Assert.True(result2.FromCache, "Second result should be from cache");
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetCoverAsync_ReturnsMetronCover_WhenMatchFound()
    {
        var metronIssue = new MetronIssue
        {
            Id = 789,
            ImageUrl = "https://metron.cloud/media/issue/aww.jpg",
            Series = new MetronSeries { Name = "Absolute Wonder Woman" },
            Number = "17"
        };

        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSearchResult
            {
                Success = true,
                Issues = new List<MetronIssue> { metronIssue }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Absolute Wonder Woman", "17");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.Metron, result.Source);
    }

    [Fact]
    public async Task GetCoverAsync_HandlesException_Gracefully()
    {
        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "1", volumeCoverUrl: "https://fallback.jpg", comicVineVolumeId: null);

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://fallback.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverAsync_PrefersPublisherMatch()
    {
        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSearchResult
            {
                Success = true,
                Issues = new List<MetronIssue>
                {
                    new() 
                    { 
                        Id = 1, 
                        Number = "100", 
                        ImageUrl = "https://urban.jpg",
                        Series = new MetronSeries 
                        { 
                            Name = "Batman", 
                            Publisher = new MetronPublisher { Name = "Urban Comics" }
                        }
                    },
                    new() 
                    { 
                        Id = 2, 
                        Number = "100", 
                        ImageUrl = "https://dc.jpg",
                        Series = new MetronSeries 
                        { 
                            Name = "Batman", 
                            Publisher = new MetronPublisher { Name = "DC Comics" }
                        }
                    }
                }
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "100", comicVineVolumeId: null, publisher: "DC");

        Assert.True(result.Success);
        Assert.Equal("https://dc.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsAccurateStatistics()
    {
        var metronClientMock = new Mock<IMetronClient>();
        metronClientMock.Setup(c => c.IsConfigured).Returns(true);
        metronClientMock.Setup(c => c.GetIssueByCvIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int cvId, bool _, CancellationToken __) => MetronIssueResult.Found(new MetronIssue
            {
                Id = cvId,
                CvId = cvId,
                ImageUrl = "https://test.jpg"
            }));

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(metronClientMock.Object, _settingsServiceMock.Object, freshCache, _loggerMock.Object);

        await service.GetCoverByCvIdAsync(1);
        await service.GetCoverByCvIdAsync(2);
        await service.GetCoverByCvIdAsync(3);

        var stats = await service.GetStatsAsync();

        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(3, stats.MetronHits);
        Assert.Equal(0, stats.Misses);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesCachedEntry()
    {
        var metronIssue = new MetronIssue
        {
            Id = 1,
            CvId = 12345,
            ImageUrl = "https://test.jpg"
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(12345, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_metronClientMock.Object, _settingsServiceMock.Object, freshCache, _loggerMock.Object);

        var result1 = await service.GetCoverByCvIdAsync(12345);
        Assert.False(result1.FromCache);

        var result2 = await service.GetCoverByCvIdAsync(12345);
        Assert.True(result2.FromCache);

        await service.ClearCacheAsync(12345);

        var result3 = await service.GetCoverByCvIdAsync(12345);
        Assert.False(result3.FromCache);
    }

    [Fact]
    public async Task GetCoverAsync_FallsBackToVolume_WhenMetronReturnsEmpty()
    {
        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSearchResult
            {
                Success = true,
                Issues = new List<MetronIssue>()
            });

        var service = CreateService();

        var result = await service.GetCoverAsync("Batman", "999", comicVineVolumeId: null, volumeCoverUrl: "https://volume-fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://volume-fallback.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsNotFound_WhenMetronNotConfigured()
    {
        var metronClientMock = new Mock<IMetronClient>();
        metronClientMock.Setup(c => c.IsConfigured).Returns(false);

        var service = new CoverFallbackService(metronClientMock.Object, _settingsServiceMock.Object, _cache, _loggerMock.Object);

        var result = await service.GetCoverByCvIdAsync(12345, volumeCoverUrl: "https://volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_HandlesIssueWithNullImageUrl()
    {
        var metronIssue = new MetronIssue
        {
            Id = 1,
            CvId = 12345,
            ImageUrl = null
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(12345, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(12345, volumeCoverUrl: "https://volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_VerifiesPriorityOrder_MetronBeforeVolume()
    {
        var metronIssue = new MetronIssue
        {
            Id = 1,
            CvId = 12345,
            ImageUrl = "https://metron.jpg"
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(12345, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(12345, volumeCoverUrl: "https://volume.jpg");

        Assert.Equal(CoverSource.Metron, result.Source);
        Assert.Equal("https://metron.jpg", result.CoverUrl);
    }

    /// <summary>
    /// EPIC 14.7.2: Verifies cover source order - when CV issue ID lookup fails,
    /// fallback uses Metron by CV volume ID + issue number before volume URL.
    /// </summary>
    [Fact]
    public async Task GetCoverByCvIdAsync_ReturnsMetronCover_WhenFoundViaVolumeIdAndNumber()
    {
        const int cvIssueId = 99999;
        const int cvVolumeId = 4050;
        const string issueNumber = "5";

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(cvIssueId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.NotFound("Not in Metron by CV id"));

        _metronClientMock.Setup(c => c.GetSeriesByCvIdAsync(cvVolumeId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronSeriesResult.Found(new MetronSeries { Id = 100, Name = "Test Series" }));

        var metronIssue = new MetronIssue
        {
            Id = 200,
            CvId = cvIssueId,
            Number = issueNumber,
            ImageUrl = "https://metron.cloud/volume-issue-cover.jpg",
            Series = new MetronSeries { Name = "Test Series" }
        };

        _metronClientMock.Setup(c => c.GetIssueBySeriesIdAsync(100, issueNumber, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(cvIssueId, comicVineVolumeId: cvVolumeId, issueNumber: issueNumber, volumeCoverUrl: "https://volume-fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.Metron, result.Source);
        Assert.Equal("https://metron.cloud/volume-issue-cover.jpg", result.CoverUrl);
    }

    [Fact]
    public async Task GetCoverByCvIdAsync_TracksResolutionTime()
    {
        var metronIssue = new MetronIssue
        {
            Id = 1,
            CvId = 12345,
            ImageUrl = "https://test.jpg"
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(12345, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_metronClientMock.Object, _settingsServiceMock.Object, freshCache, _loggerMock.Object);

        var result = await service.GetCoverByCvIdAsync(12345);

        Assert.True(result.ResolutionTimeMs >= 0);
    }

    [Fact]
    public async Task GetStatsAsync_ReportsCacheHitRatio()
    {
        var metronIssue = new MetronIssue
        {
            Id = 1,
            CvId = 12345,
            ImageUrl = "https://test.jpg"
        };

        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(12345, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Found(metronIssue));

        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var service = new CoverFallbackService(_metronClientMock.Object, _settingsServiceMock.Object, freshCache, _loggerMock.Object);

        // First call (cache miss)
        await service.GetCoverByCvIdAsync(12345);
        // Second call (cache hit)
        await service.GetCoverByCvIdAsync(12345);
        // Third call (cache hit)
        await service.GetCoverByCvIdAsync(12345);

        var stats = await service.GetStatsAsync();

        Assert.Equal(3, stats.TotalRequests);
        Assert.True(stats.CacheHitRatio >= 0.5);
    }

    [Fact]
    public async Task GetCoverAsync_RejectsIdLessMatch_BelowConfidenceThreshold()
    {
        _settingsServiceMock
            .Setup(s => s.GetAsync<MetronSettings>("metron", It.IsAny<MetronSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSettings { MinMatchConfidence = 95 });

        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSearchResult
            {
                Success = true,
                Issues = new List<MetronIssue>
                {
                    new()
                    {
                        Id = 1,
                        Number = "17",
                        ImageUrl = "https://metron.cloud/aww17.jpg",
                        StoreDate = DateTime.UtcNow.AddMonths(8),
                        Series = new MetronSeries
                        {
                            Name = "Absolute Wonder Woman",
                            Publisher = new MetronPublisher { Name = "DC Comics" }
                        }
                    }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync(
            "Absolute Wonder Woman",
            "17",
            comicVineVolumeId: null,
            publisher: "DC Comics",
            expectedStoreDate: DateTime.UtcNow,
            volumeCoverUrl: "https://comicvine/volume.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.True(result.WasConfidenceRejected);
    }

    [Fact]
    public async Task GetCoverAsync_UsesIdLessMetronMatch_WhenConfidencePassesThreshold()
    {
        _settingsServiceMock
            .Setup(s => s.GetAsync<MetronSettings>("metron", It.IsAny<MetronSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSettings { MinMatchConfidence = 70 });

        _metronClientMock.Setup(c => c.SearchIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetronSearchResult
            {
                Success = true,
                Issues = new List<MetronIssue>
                {
                    new()
                    {
                        Id = 1,
                        Number = "17",
                        ImageUrl = "https://metron.cloud/aww17.jpg",
                        StoreDate = DateTime.UtcNow.Date.AddDays(1),
                        Series = new MetronSeries
                        {
                            Name = "Absolute Wonder Woman",
                            Publisher = new MetronPublisher { Name = "DC Comics" }
                        }
                    }
                }
            });

        var service = CreateService();
        var result = await service.GetCoverAsync(
            "Absolute Wonder Woman",
            "17",
            comicVineVolumeId: null,
            publisher: "DC Comics",
            expectedStoreDate: DateTime.UtcNow.Date);

        Assert.True(result.Success);
        Assert.Equal(CoverSource.Metron, result.Source);
        Assert.Equal("IdLessHeuristic", result.MatchMethod);
        Assert.NotNull(result.MatchConfidence);
        Assert.True(result.MatchConfidence > 0.70);
    }

    /// <summary>
    /// EPIC 14.7.5: Edge case - when Metron returns 429 Rate Limited, fall back to volume cover URL.
    /// </summary>
    [Fact]
    public async Task GetCoverByCvIdAsync_WhenMetronReturns429_FallsBackToVolumeCover()
    {
        _metronClientMock.Setup(c => c.GetIssueByCvIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetronIssueResult.Failed("Rate limited", 429));

        var service = CreateService();

        var result = await service.GetCoverByCvIdAsync(12345, volumeCoverUrl: "https://volume-fallback.jpg");

        Assert.True(result.Success);
        Assert.Equal(CoverSource.ComicVineVolume, result.Source);
        Assert.Equal("https://volume-fallback.jpg", result.CoverUrl);
    }
}
