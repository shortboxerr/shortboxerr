using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

public class HostReliabilityServiceTests
{
    private readonly MockSettingsService _settingsService = new();

    private HostReliabilityService CreateService() => new(_settingsService);

    #region RecordSuccessAsync Tests

    [Fact]
    public async Task RecordSuccessAsync_AddsRecord()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("mediafire", "readcomiconline", 1024 * 1024, TimeSpan.FromSeconds(10));

        var stats = await service.GetHostStatsAsync("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalSuccesses);
        Assert.Equal(0, stats.TotalFailures);
        Assert.Equal(1024 * 1024, stats.TotalBytesDownloaded);
    }

    [Fact]
    public async Task RecordSuccessAsync_NormalizesHostId()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("MediaFire", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("MEDIAFIRE", "site1", 1000, TimeSpan.FromSeconds(1));

        var stats = await service.GetHostStatsAsync("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(2, stats.TotalSuccesses);
    }

    [Fact]
    public async Task RecordSuccessAsync_CalculatesSpeed()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("mega", "site1", 10 * 1024 * 1024, TimeSpan.FromSeconds(10));

        var stats = await service.GetHostStatsAsync("mega");
        Assert.NotNull(stats);
        Assert.Equal(1024 * 1024, stats.AverageSpeedBps, 1); // 1 MB/s
    }

    #endregion

    #region RecordFailureAsync Tests

    [Fact]
    public async Task RecordFailureAsync_AddsRecord()
    {
        var service = CreateService();

        await service.RecordFailureAsync("pixeldrain", "site1", HostResolverFailureReason.FileNotFound, "404");

        var stats = await service.GetHostStatsAsync("pixeldrain");
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalSuccesses);
        Assert.Equal(1, stats.TotalFailures);
        Assert.Equal(HostResolverFailureReason.FileNotFound, stats.LastFailureReason);
    }

    [Fact]
    public async Task RecordFailureAsync_TracksFailuresByReason()
    {
        var service = CreateService();

        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.FileNotFound);
        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.FileNotFound);
        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.Timeout);

        var stats = await service.GetHostStatsAsync("host1");
        Assert.NotNull(stats);
        Assert.Equal(3, stats.TotalFailures);
        Assert.Equal(2, stats.FailuresByReason[HostResolverFailureReason.FileNotFound]);
        Assert.Equal(1, stats.FailuresByReason[HostResolverFailureReason.Timeout]);
    }

    #endregion

    #region GetHostStatsAsync Tests

    [Fact]
    public async Task GetHostStatsAsync_ReturnsNullForUnknownHost()
    {
        var service = CreateService();

        var stats = await service.GetHostStatsAsync("unknown");

        Assert.Null(stats);
    }

    [Fact]
    public async Task GetHostStatsAsync_CalculatesSuccessRate()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.NetworkError);

        var stats = await service.GetHostStatsAsync("host1");
        Assert.NotNull(stats);
        Assert.Equal(66.67, stats.SuccessRate, 1); // 2/3 = 66.67%
    }

    [Fact]
    public async Task GetHostStatsAsync_FiltersBySite()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host1", "site2", 2000, TimeSpan.FromSeconds(1));
        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.NetworkError);

        var site1Stats = await service.GetHostStatsAsync("host1", "site1");
        var site2Stats = await service.GetHostStatsAsync("host1", "site2");

        Assert.NotNull(site1Stats);
        Assert.Equal(1, site1Stats.TotalSuccesses);
        Assert.Equal(1, site1Stats.TotalFailures);

        Assert.NotNull(site2Stats);
        Assert.Equal(1, site2Stats.TotalSuccesses);
        Assert.Equal(0, site2Stats.TotalFailures);
    }

    #endregion

    #region GetAllStatsAsync Tests

    [Fact]
    public async Task GetAllStatsAsync_ReturnsAllHosts()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host2", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordFailureAsync("host3", "site1", HostResolverFailureReason.NetworkError);

        var allStats = await service.GetAllStatsAsync();

        Assert.Equal(3, allStats.Count);
    }

    [Fact]
    public async Task GetAllStatsAsync_SortsbyReliabilityScore()
    {
        var service = CreateService();

        // Host with poor reliability
        for (int i = 0; i < 10; i++)
            await service.RecordFailureAsync("poorhost", "site1", HostResolverFailureReason.NetworkError);

        // Host with good reliability
        for (int i = 0; i < 10; i++)
            await service.RecordSuccessAsync("goodhost", "site1", 1000, TimeSpan.FromSeconds(1));

        var allStats = await service.GetAllStatsAsync();

        Assert.Equal("goodhost", allStats[0].HostId);
        Assert.Equal("poorhost", allStats[1].HostId);
    }

    #endregion

    #region GetStatsBySiteAsync Tests

    [Fact]
    public async Task GetStatsBySiteAsync_FiltersBySite()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host2", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host1", "site2", 1000, TimeSpan.FromSeconds(1));

        var site1Stats = await service.GetStatsBySiteAsync("site1");
        var site2Stats = await service.GetStatsBySiteAsync("site2");

        Assert.Equal(2, site1Stats.Count);
        Assert.Single(site2Stats);
    }

    #endregion

    #region GetHostRankingsAsync Tests

    [Fact]
    public async Task GetHostRankingsAsync_RanksHosts()
    {
        var service = CreateService();

        // Create hosts with different reliability
        for (int i = 0; i < 10; i++)
            await service.RecordSuccessAsync("reliable", "site1", 1000, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
        {
            await service.RecordSuccessAsync("medium", "site1", 1000, TimeSpan.FromSeconds(1));
            await service.RecordFailureAsync("medium", "site1", HostResolverFailureReason.NetworkError);
        }

        var rankings = await service.GetHostRankingsAsync("site1");

        Assert.Equal(2, rankings.Count);
        Assert.Equal(1, rankings[0].Rank);
        Assert.Equal("reliable", rankings[0].HostId);
        Assert.Equal(2, rankings[1].Rank);
        Assert.Equal("medium", rankings[1].HostId);
    }

    #endregion

    #region GetRecommendedHostOrderAsync Tests

    [Fact]
    public async Task GetRecommendedHostOrderAsync_OrdersByReliability()
    {
        var service = CreateService();

        // Create hosts with different reliability
        for (int i = 0; i < 10; i++)
            await service.RecordSuccessAsync("best", "site1", 1000, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 10; i++)
            await service.RecordFailureAsync("worst", "site1", HostResolverFailureReason.NetworkError);

        var availableHosts = new[] { "worst", "unknown", "best" };
        var ordered = await service.GetRecommendedHostOrderAsync("site1", availableHosts);

        Assert.Equal("best", ordered[0]);
        Assert.Equal("unknown", ordered[1]); // Unknown gets default score
        Assert.Equal("worst", ordered[2]);
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ReturnsAggregateStats()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host2", "site2", 2000, TimeSpan.FromSeconds(2));
        await service.RecordFailureAsync("host1", "site1", HostResolverFailureReason.NetworkError);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(2, summary.TotalHostsTracked);
        Assert.Equal(2, summary.TotalSitesTracked);
        Assert.Equal(2, summary.TotalSuccesses);
        Assert.Equal(1, summary.TotalFailures);
        Assert.Equal(66.67, summary.OverallSuccessRate, 1);
        Assert.Equal(3000, summary.TotalBytesDownloaded);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task ClearHostStatsAsync_RemovesHostData()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host2", "site1", 1000, TimeSpan.FromSeconds(1));

        await service.ClearHostStatsAsync("host1");

        var host1 = await service.GetHostStatsAsync("host1");
        var host2 = await service.GetHostStatsAsync("host2");

        Assert.Null(host1);
        Assert.NotNull(host2);
    }

    [Fact]
    public async Task ClearSiteStatsAsync_RemovesSiteData()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host1", "site2", 1000, TimeSpan.FromSeconds(1));

        await service.ClearSiteStatsAsync("site1");

        var stats = await service.GetHostStatsAsync("host1");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalSuccesses); // Only site2 data remains
    }

    [Fact]
    public async Task ClearAllStatsAsync_RemovesEverything()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("host2", "site2", 1000, TimeSpan.FromSeconds(1));

        await service.ClearAllStatsAsync();

        var allStats = await service.GetAllStatsAsync();
        Assert.Empty(allStats);
    }

    #endregion

    #region PurgeOldStatsAsync Tests

    [Fact]
    public async Task PurgeOldStatsAsync_RemovesOldRecords()
    {
        var service = CreateService();

        // Add some records
        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));

        // With default 30 day retention, recent records should not be purged
        var purgedCount = await service.PurgeOldStatsAsync();
        Assert.Equal(0, purgedCount);

        var stats = await service.GetHostStatsAsync("host1");
        Assert.NotNull(stats);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultSettings()
    {
        var service = CreateService();

        var settings = await service.GetSettingsAsync();

        Assert.True(settings.TrackingEnabled);
        Assert.Equal(TimeSpan.FromDays(30), settings.RetentionPeriod);
        Assert.Equal(5, settings.MinAttemptsForScore);
        Assert.Equal(0.6, settings.SuccessRateWeight);
        Assert.Equal(0.3, settings.SpeedWeight);
        Assert.Equal(0.1, settings.RecencyWeight);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsSettings()
    {
        var service = CreateService();

        var newSettings = new HostReliabilitySettings
        {
            TrackingEnabled = false,
            MinAttemptsForScore = 10,
            RetentionPeriod = TimeSpan.FromDays(7)
        };

        await service.SaveSettingsAsync(newSettings);
        var loaded = await service.GetSettingsAsync();

        Assert.False(loaded.TrackingEnabled);
        Assert.Equal(10, loaded.MinAttemptsForScore);
    }

    [Fact]
    public async Task RecordSuccessAsync_RespectsTrackingEnabled()
    {
        var service = CreateService();

        await service.SaveSettingsAsync(new HostReliabilitySettings { TrackingEnabled = false });

        await service.RecordSuccessAsync("host1", "site1", 1000, TimeSpan.FromSeconds(1));

        var stats = await service.GetHostStatsAsync("host1");
        Assert.Null(stats); // Should not have recorded anything
    }

    #endregion

    #region HostReliabilityStats Tests

    [Fact]
    public void HostReliabilityStats_TotalAttempts()
    {
        var stats = new HostReliabilityStats
        {
            TotalSuccesses = 7,
            TotalFailures = 3
        };

        Assert.Equal(10, stats.TotalAttempts);
    }

    [Fact]
    public void HostReliabilityStats_AverageFileSizeBytes()
    {
        var stats = new HostReliabilityStats
        {
            TotalSuccesses = 5,
            TotalBytesDownloaded = 5000
        };

        Assert.Equal(1000, stats.AverageFileSizeBytes);
    }

    [Fact]
    public void HostReliabilityStats_AverageFileSizeBytes_ZeroSuccesses()
    {
        var stats = new HostReliabilityStats
        {
            TotalSuccesses = 0,
            TotalBytesDownloaded = 0
        };

        Assert.Equal(0, stats.AverageFileSizeBytes);
    }

    #endregion

    #region HostReliabilityRanking Tests

    [Fact]
    public void HostReliabilityRanking_Properties()
    {
        var ranking = new HostReliabilityRanking
        {
            Rank = 1,
            HostId = "mega",
            DisplayName = "Mega",
            ReliabilityScore = 95.5,
            SuccessRate = 98.0,
            AverageSpeedBps = 1024 * 1024,
            TotalAttempts = 100,
            IsBlacklisted = false,
            Trend = ReliabilityTrend.Improving
        };

        Assert.Equal(1, ranking.Rank);
        Assert.Equal("mega", ranking.HostId);
        Assert.Equal(95.5, ranking.ReliabilityScore);
        Assert.Equal(ReliabilityTrend.Improving, ranking.Trend);
    }

    #endregion

    #region ReliabilityTrend Tests

    [Fact]
    public void ReliabilityTrend_Values()
    {
        Assert.Equal(0, (int)ReliabilityTrend.Unknown);
        Assert.Equal(1, (int)ReliabilityTrend.Improving);
        Assert.Equal(2, (int)ReliabilityTrend.Stable);
        Assert.Equal(3, (int)ReliabilityTrend.Declining);
    }

    #endregion

    #region HostReliabilitySettings Tests

    [Fact]
    public void HostReliabilitySettings_DefaultValues()
    {
        var settings = new HostReliabilitySettings();

        Assert.True(settings.TrackingEnabled);
        Assert.Equal(TimeSpan.FromDays(30), settings.RetentionPeriod);
        Assert.Equal(5, settings.MinAttemptsForScore);
        Assert.Equal(0.6, settings.SuccessRateWeight);
        Assert.Equal(0.3, settings.SpeedWeight);
        Assert.Equal(0.1, settings.RecencyWeight);
        Assert.True(settings.UseForHostOrdering);
        Assert.Equal(10, settings.TrendWindowSize);
        Assert.Equal(10.0, settings.TrendChangeThreshold);
    }

    [Fact]
    public void HostReliabilitySettings_WeightsSumToOne()
    {
        var settings = new HostReliabilitySettings();

        var totalWeight = settings.SuccessRateWeight + settings.SpeedWeight + settings.RecencyWeight;
        Assert.Equal(1.0, totalWeight, 3);
    }

    #endregion

    #region HostDownloadRecord Tests

    [Fact]
    public void HostDownloadRecord_SpeedBps_Calculation()
    {
        var record = new HostDownloadRecord
        {
            BytesDownloaded = 10 * 1024 * 1024, // 10 MB
            Duration = TimeSpan.FromSeconds(10)
        };

        Assert.Equal(1024 * 1024, record.SpeedBps, 1); // 1 MB/s
    }

    [Fact]
    public void HostDownloadRecord_SpeedBps_ZeroDuration()
    {
        var record = new HostDownloadRecord
        {
            BytesDownloaded = 1000,
            Duration = TimeSpan.Zero
        };

        Assert.Equal(0, record.SpeedBps);
    }

    [Fact]
    public void HostDownloadRecord_HasDefaultId()
    {
        var record = new HostDownloadRecord();
        Assert.NotNull(record.Id);
        Assert.NotEmpty(record.Id);
    }

    [Fact]
    public void HostDownloadRecord_HasDefaultTimestamp()
    {
        var before = DateTime.UtcNow;
        var record = new HostDownloadRecord();
        var after = DateTime.UtcNow;

        Assert.InRange(record.Timestamp, before, after);
    }

    #endregion

    #region HostReliabilitySummary Tests

    [Fact]
    public void HostReliabilitySummary_Properties()
    {
        var summary = new HostReliabilitySummary
        {
            TotalHostsTracked = 5,
            TotalSitesTracked = 2,
            TotalSuccesses = 100,
            TotalFailures = 10,
            OverallSuccessRate = 90.9,
            TotalBytesDownloaded = 1024 * 1024 * 100,
            OverallAverageSpeedBps = 1024 * 512,
            MostReliableHost = "mega",
            FastestHost = "direct",
            LeastReliableHost = "zippyshare"
        };

        Assert.Equal(5, summary.TotalHostsTracked);
        Assert.Equal("mega", summary.MostReliableHost);
        Assert.Equal("direct", summary.FastestHost);
        Assert.Equal("zippyshare", summary.LeastReliableHost);
    }

    #endregion

    #region Display Name Tests

    [Fact]
    public async Task DisplayName_MapsKnownHosts()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("mediafire", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("mega", "site1", 1000, TimeSpan.FromSeconds(1));
        await service.RecordSuccessAsync("pixeldrain", "site1", 1000, TimeSpan.FromSeconds(1));

        var stats = await service.GetAllStatsAsync();

        Assert.Contains(stats, s => s.DisplayName == "MediaFire");
        Assert.Contains(stats, s => s.DisplayName == "Mega");
        Assert.Contains(stats, s => s.DisplayName == "Pixeldrain");
    }

    [Fact]
    public async Task DisplayName_UsesHostIdForUnknown()
    {
        var service = CreateService();

        await service.RecordSuccessAsync("unknownhost", "site1", 1000, TimeSpan.FromSeconds(1));

        var stats = await service.GetHostStatsAsync("unknownhost");
        Assert.NotNull(stats);
        Assert.Equal("unknownhost", stats.DisplayName);
    }

    #endregion

    #region Mock Settings Service

    private class MockSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string> _settings = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _settings.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
        {
            if (_settings.TryGetValue(key, out var value))
            {
                return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(value));
            }
            return Task.FromResult(defaultValue);
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _settings[key] = value;
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _settings[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task<IDictionary<string, string>> GetAllAsync(string? prefix = null, CancellationToken cancellationToken = default)
        {
            IDictionary<string, string> result = prefix == null
                ? _settings
                : _settings.Where(kv => kv.Key.StartsWith(prefix)).ToDictionary(kv => kv.Key, kv => kv.Value);
            return Task.FromResult(result);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _settings.Remove(key);
            return Task.CompletedTask;
        }

        public Task<UiSettings> GetUiSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UiSettings());

        public Task SetUiSettingsAsync(UiSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GeneralSettings());

        public Task SetGeneralSettingsAsync(GeneralSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ApiKeyInfo> GetApiKeyAsync(bool includeFull = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = true, MaskedKey = "****" });

        public Task<ApiKeyInfo> RegenerateApiKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = true, MaskedKey = "****" });

        public Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ApiKeyInfo> SetApiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = enabled, MaskedKey = "****" });

        public Task<AutoMatchSettings> GetAutoMatchSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AutoMatchSettings());

        public Task SetAutoMatchSettingsAsync(AutoMatchSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    #endregion
}
