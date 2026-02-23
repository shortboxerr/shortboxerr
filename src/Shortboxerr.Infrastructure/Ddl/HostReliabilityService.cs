using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Tracks and analyzes download host reliability statistics.
/// </summary>
public class HostReliabilityService : IHostReliabilityService
{
    private readonly ILogger<HostReliabilityService>? _logger;
    private readonly ISettingsService _settingsService;
    private readonly IHostBlacklistService? _blacklistService;

    private readonly ConcurrentDictionary<string, List<HostDownloadRecord>> _records = new();
    private HostReliabilitySettings _settings = new();

    private const string SettingsKey = "host_reliability";
    private const string RecordsKey = "host_reliability_records";

    private static readonly Dictionary<string, string> HostDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "mediafire", "MediaFire" },
        { "mega", "Mega" },
        { "pixeldrain", "Pixeldrain" },
        { "gdrive", "Google Drive" },
        { "dropbox", "Dropbox" },
        { "direct", "Direct Download" },
        { "zippyshare", "Zippyshare" },
        { "uploadhaven", "UploadHaven" },
        { "1fichier", "1Fichier" },
        { "turbobit", "Turbobit" },
        { "nitroflare", "Nitroflare" },
        { "rapidgator", "Rapidgator" },
        { "uploaded", "Uploaded" }
    };

    public HostReliabilityService(
        ISettingsService settingsService,
        IHostBlacklistService? blacklistService = null,
        ILogger<HostReliabilityService>? logger = null)
    {
        _settingsService = settingsService;
        _blacklistService = blacklistService;
        _logger = logger;
    }

    public async Task RecordSuccessAsync(
        string hostId,
        string ddlSiteId,
        long bytesDownloaded,
        TimeSpan downloadDuration,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.TrackingEnabled)
            return;

        var record = new HostDownloadRecord
        {
            HostId = hostId.ToLowerInvariant(),
            DdlSiteId = ddlSiteId,
            Success = true,
            BytesDownloaded = bytesDownloaded,
            Duration = downloadDuration,
            Timestamp = DateTime.UtcNow
        };

        AddRecord(record);
        _logger?.LogDebug(
            "Recorded success for {Host} on {Site}: {Bytes} bytes in {Duration:F1}s ({Speed:F1} KB/s)",
            hostId, ddlSiteId, bytesDownloaded, downloadDuration.TotalSeconds, record.SpeedBps / 1024);
    }

    public async Task RecordFailureAsync(
        string hostId,
        string ddlSiteId,
        HostResolverFailureReason failureReason,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.TrackingEnabled)
            return;

        var record = new HostDownloadRecord
        {
            HostId = hostId.ToLowerInvariant(),
            DdlSiteId = ddlSiteId,
            Success = false,
            FailureReason = failureReason,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        AddRecord(record);
        _logger?.LogDebug(
            "Recorded failure for {Host} on {Site}: {Reason}",
            hostId, ddlSiteId, failureReason);
    }

    public Task<HostReliabilityStats?> GetHostStatsAsync(
        string hostId,
        CancellationToken cancellationToken = default)
    {
        var key = hostId.ToLowerInvariant();
        var records = GetRecordsForHost(key, null);

        if (records.Count == 0)
            return Task.FromResult<HostReliabilityStats?>(null);

        var stats = CalculateStats(key, null, records);
        return Task.FromResult<HostReliabilityStats?>(stats);
    }

    public Task<HostReliabilityStats?> GetHostStatsAsync(
        string hostId,
        string ddlSiteId,
        CancellationToken cancellationToken = default)
    {
        var key = hostId.ToLowerInvariant();
        var records = GetRecordsForHost(key, ddlSiteId);

        if (records.Count == 0)
            return Task.FromResult<HostReliabilityStats?>(null);

        var stats = CalculateStats(key, ddlSiteId, records);
        return Task.FromResult<HostReliabilityStats?>(stats);
    }

    public Task<IReadOnlyList<HostReliabilityStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var allStats = new List<HostReliabilityStats>();

        foreach (var (hostKey, records) in _records)
        {
            if (records.Count > 0)
            {
                allStats.Add(CalculateStats(hostKey, null, records));
            }
        }

        return Task.FromResult<IReadOnlyList<HostReliabilityStats>>(
            allStats.OrderByDescending(s => s.ReliabilityScore).ToList());
    }

    public Task<IReadOnlyList<HostReliabilityStats>> GetStatsBySiteAsync(
        string ddlSiteId,
        CancellationToken cancellationToken = default)
    {
        var siteStats = new List<HostReliabilityStats>();

        foreach (var (hostKey, records) in _records)
        {
            var siteRecords = records.Where(r => r.DdlSiteId == ddlSiteId).ToList();
            if (siteRecords.Count > 0)
            {
                siteStats.Add(CalculateStats(hostKey, ddlSiteId, siteRecords));
            }
        }

        return Task.FromResult<IReadOnlyList<HostReliabilityStats>>(
            siteStats.OrderByDescending(s => s.ReliabilityScore).ToList());
    }

    public async Task<IReadOnlyList<HostReliabilityRanking>> GetHostRankingsAsync(
        string ddlSiteId,
        CancellationToken cancellationToken = default)
    {
        var stats = await GetStatsBySiteAsync(ddlSiteId, cancellationToken);
        return CreateRankings(stats, ddlSiteId);
    }

    public async Task<IReadOnlyList<HostReliabilityRanking>> GetGlobalHostRankingsAsync(
        CancellationToken cancellationToken = default)
    {
        var stats = await GetAllStatsAsync(cancellationToken);
        return CreateRankings(stats, null);
    }

    public Task<double> CalculateReliabilityScoreAsync(
        string hostId,
        string ddlSiteId,
        CancellationToken cancellationToken = default)
    {
        var key = hostId.ToLowerInvariant();
        var records = GetRecordsForHost(key, ddlSiteId);

        if (records.Count < _settings.MinAttemptsForScore)
            return Task.FromResult(50.0); // Default score for insufficient data

        var score = CalculateReliabilityScore(records);
        return Task.FromResult(score);
    }

    public async Task<IReadOnlyList<string>> GetRecommendedHostOrderAsync(
        string ddlSiteId,
        IEnumerable<string> availableHosts,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.UseForHostOrdering)
        {
            return availableHosts.ToList();
        }

        var rankings = await GetHostRankingsAsync(ddlSiteId, cancellationToken);
        var rankingDict = rankings.ToDictionary(r => r.HostId, r => r.ReliabilityScore, StringComparer.OrdinalIgnoreCase);

        var ordered = availableHosts
            .OrderByDescending(h => rankingDict.TryGetValue(h, out var score) ? score : 50.0)
            .ThenBy(h => _blacklistService?.IsBlacklisted(h) ?? false)
            .ToList();

        return ordered;
    }

    public Task<HostReliabilitySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var allRecords = _records.Values.SelectMany(r => r).ToList();
        var successes = allRecords.Where(r => r.Success).ToList();
        var failures = allRecords.Where(r => !r.Success).ToList();

        var hostStats = _records.Keys
            .Select(h => CalculateStats(h, null, _records[h]))
            .ToList();

        var summary = new HostReliabilitySummary
        {
            TotalHostsTracked = _records.Count,
            TotalSitesTracked = allRecords.Select(r => r.DdlSiteId).Distinct().Count(),
            TotalSuccesses = successes.Count,
            TotalFailures = failures.Count,
            OverallSuccessRate = allRecords.Count > 0
                ? (double)successes.Count / allRecords.Count * 100
                : 0,
            TotalBytesDownloaded = successes.Sum(r => r.BytesDownloaded),
            OverallAverageSpeedBps = successes.Count > 0
                ? successes.Average(r => r.SpeedBps)
                : 0,
            MostReliableHost = hostStats
                .Where(s => s.TotalAttempts >= _settings.MinAttemptsForScore)
                .OrderByDescending(s => s.ReliabilityScore)
                .FirstOrDefault()?.HostId,
            FastestHost = hostStats
                .Where(s => s.TotalSuccesses >= _settings.MinAttemptsForScore)
                .OrderByDescending(s => s.AverageSpeedBps)
                .FirstOrDefault()?.HostId,
            LeastReliableHost = hostStats
                .Where(s => s.TotalAttempts >= _settings.MinAttemptsForScore)
                .OrderBy(s => s.ReliabilityScore)
                .FirstOrDefault()?.HostId,
            TrackingSince = allRecords.Count > 0
                ? allRecords.Min(r => r.Timestamp)
                : null,
            LastActivityTime = allRecords.Count > 0
                ? allRecords.Max(r => r.Timestamp)
                : null
        };

        return Task.FromResult(summary);
    }

    public Task ClearHostStatsAsync(string hostId, CancellationToken cancellationToken = default)
    {
        var key = hostId.ToLowerInvariant();
        _records.TryRemove(key, out _);
        _logger?.LogInformation("Cleared reliability stats for host: {Host}", hostId);
        return Task.CompletedTask;
    }

    public Task ClearSiteStatsAsync(string ddlSiteId, CancellationToken cancellationToken = default)
    {
        foreach (var (hostKey, records) in _records)
        {
            var filtered = records.Where(r => r.DdlSiteId != ddlSiteId).ToList();
            _records[hostKey] = filtered;
        }
        _logger?.LogInformation("Cleared reliability stats for site: {Site}", ddlSiteId);
        return Task.CompletedTask;
    }

    public Task ClearAllStatsAsync(CancellationToken cancellationToken = default)
    {
        _records.Clear();
        _logger?.LogInformation("Cleared all reliability stats");
        return Task.CompletedTask;
    }

    public async Task<int> PurgeOldStatsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - settings.RetentionPeriod;
        var purgedCount = 0;

        foreach (var (hostKey, records) in _records)
        {
            var oldCount = records.Count;
            var filtered = records.Where(r => r.Timestamp >= cutoff).ToList();
            purgedCount += oldCount - filtered.Count;
            _records[hostKey] = filtered;
        }

        // Remove hosts with no remaining records
        var emptyHosts = _records.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
        foreach (var host in emptyHosts)
        {
            _records.TryRemove(host, out _);
        }

        if (purgedCount > 0)
        {
            _logger?.LogInformation("Purged {Count} old reliability records", purgedCount);
        }

        return purgedCount;
    }

    public async Task<HostReliabilitySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsService.GetAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return _settings;
        }

        try
        {
            _settings = System.Text.Json.JsonSerializer.Deserialize<HostReliabilitySettings>(json)
                        ?? new HostReliabilitySettings();
            return _settings;
        }
        catch
        {
            return _settings;
        }
    }

    public async Task SaveSettingsAsync(
        HostReliabilitySettings settings,
        CancellationToken cancellationToken = default)
    {
        _settings = settings;
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        await _settingsService.SetAsync(SettingsKey, json, cancellationToken);
    }

    #region Private Methods

    private void AddRecord(HostDownloadRecord record)
    {
        var key = record.HostId.ToLowerInvariant();
        var records = _records.GetOrAdd(key, _ => new List<HostDownloadRecord>());

        lock (records)
        {
            records.Add(record);
        }
    }

    private List<HostDownloadRecord> GetRecordsForHost(string hostKey, string? ddlSiteId)
    {
        if (!_records.TryGetValue(hostKey, out var records))
            return new List<HostDownloadRecord>();

        lock (records)
        {
            if (ddlSiteId == null)
                return records.ToList();

            return records.Where(r => r.DdlSiteId == ddlSiteId).ToList();
        }
    }

    private HostReliabilityStats CalculateStats(string hostKey, string? ddlSiteId, List<HostDownloadRecord> records)
    {
        var successes = records.Where(r => r.Success).ToList();
        var failures = records.Where(r => !r.Success).ToList();

        var speeds = successes
            .Where(r => r.SpeedBps > 0)
            .Select(r => r.SpeedBps)
            .OrderBy(s => s)
            .ToList();

        var failuresByReason = failures
            .Where(f => f.FailureReason.HasValue)
            .GroupBy(f => f.FailureReason!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return new HostReliabilityStats
        {
            HostId = hostKey,
            DisplayName = GetDisplayName(hostKey),
            DdlSiteId = ddlSiteId,
            TotalSuccesses = successes.Count,
            TotalFailures = failures.Count,
            TotalBytesDownloaded = successes.Sum(r => r.BytesDownloaded),
            AverageSpeedBps = speeds.Count > 0 ? speeds.Average() : 0,
            MedianSpeedBps = speeds.Count > 0 ? GetMedian(speeds) : 0,
            SuccessRate = records.Count > 0 ? (double)successes.Count / records.Count * 100 : 0,
            ReliabilityScore = CalculateReliabilityScore(records),
            LastSuccessTime = successes.Count > 0 ? successes.Max(r => r.Timestamp) : null,
            LastFailureTime = failures.Count > 0 ? failures.Max(r => r.Timestamp) : null,
            LastFailureReason = failures.OrderByDescending(r => r.Timestamp).FirstOrDefault()?.FailureReason,
            FailuresByReason = failuresByReason,
            TrackingSince = records.Min(r => r.Timestamp),
            LastActivityTime = records.Max(r => r.Timestamp)
        };
    }

    private double CalculateReliabilityScore(List<HostDownloadRecord> records)
    {
        if (records.Count < _settings.MinAttemptsForScore)
            return 50.0; // Default neutral score

        var successes = records.Where(r => r.Success).ToList();
        var successRate = (double)successes.Count / records.Count;

        // Normalize speed to 0-1 (assuming 10 MB/s is excellent)
        var avgSpeed = successes.Count > 0
            ? successes.Where(r => r.SpeedBps > 0).DefaultIfEmpty().Average(r => r?.SpeedBps ?? 0)
            : 0;
        var speedScore = Math.Min(avgSpeed / (10 * 1024 * 1024), 1.0); // Cap at 10 MB/s

        // Recency bonus (higher if recent activity was successful)
        var recentRecords = records.OrderByDescending(r => r.Timestamp).Take(5).ToList();
        var recencyScore = recentRecords.Count > 0
            ? (double)recentRecords.Count(r => r.Success) / recentRecords.Count
            : 0.5;

        // Weighted combination
        var score = (successRate * _settings.SuccessRateWeight +
                     speedScore * _settings.SpeedWeight +
                     recencyScore * _settings.RecencyWeight) * 100;

        return Math.Round(Math.Max(0, Math.Min(100, score)), 1);
    }

    private ReliabilityTrend CalculateTrend(List<HostDownloadRecord> records)
    {
        if (records.Count < _settings.TrendWindowSize * 2)
            return ReliabilityTrend.Unknown;

        var ordered = records.OrderByDescending(r => r.Timestamp).ToList();
        var recent = ordered.Take(_settings.TrendWindowSize).ToList();
        var previous = ordered.Skip(_settings.TrendWindowSize).Take(_settings.TrendWindowSize).ToList();

        var recentSuccessRate = recent.Count > 0 ? (double)recent.Count(r => r.Success) / recent.Count * 100 : 0;
        var previousSuccessRate = previous.Count > 0 ? (double)previous.Count(r => r.Success) / previous.Count * 100 : 0;

        var change = recentSuccessRate - previousSuccessRate;

        if (Math.Abs(change) < _settings.TrendChangeThreshold)
            return ReliabilityTrend.Stable;

        return change > 0 ? ReliabilityTrend.Improving : ReliabilityTrend.Declining;
    }

    private IReadOnlyList<HostReliabilityRanking> CreateRankings(IReadOnlyList<HostReliabilityStats> stats, string? ddlSiteId)
    {
        var ranked = stats
            .OrderByDescending(s => s.ReliabilityScore)
            .ThenByDescending(s => s.SuccessRate)
            .ThenByDescending(s => s.AverageSpeedBps)
            .ToList();

        return ranked.Select((s, i) => new HostReliabilityRanking
        {
            Rank = i + 1,
            HostId = s.HostId,
            DisplayName = s.DisplayName,
            DdlSiteId = ddlSiteId,
            ReliabilityScore = s.ReliabilityScore,
            SuccessRate = s.SuccessRate,
            AverageSpeedBps = s.AverageSpeedBps,
            TotalAttempts = s.TotalAttempts,
            IsBlacklisted = _blacklistService?.IsBlacklisted(s.HostId) ?? false,
            Trend = CalculateTrend(GetRecordsForHost(s.HostId, ddlSiteId))
        }).ToList();
    }

    private static string GetDisplayName(string hostId)
    {
        return HostDisplayNames.TryGetValue(hostId, out var name)
            ? name
            : hostId;
    }

    private static double GetMedian(List<double> sortedValues)
    {
        if (sortedValues.Count == 0) return 0;
        var mid = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[mid - 1] + sortedValues[mid]) / 2
            : sortedValues[mid];
    }

    #endregion
}
