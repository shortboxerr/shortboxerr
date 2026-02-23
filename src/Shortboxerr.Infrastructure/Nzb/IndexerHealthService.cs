using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Service for monitoring and tracking the health of NZB indexers.
/// </summary>
public class IndexerHealthService : IIndexerHealthService
{
    private readonly INzbIndexerProvider _indexerProvider;
    private readonly INewznabClient _newznabClient;
    private readonly ILogger<IndexerHealthService>? _logger;

    private readonly ConcurrentDictionary<string, IndexerHealthData> _healthData = new();

    private const int MaxSamples = 100;
    private const int DegradedResponseTimeMs = 5000;
    private const int ConsecutiveFailuresForOffline = 5;
    private const int DefaultRateLimitMinutes = 15;
    private static readonly TimeSpan TrackingWindow = TimeSpan.FromHours(24);

    public IndexerHealthService(
        INzbIndexerProvider indexerProvider,
        INewznabClient newznabClient,
        ILogger<IndexerHealthService>? logger = null)
    {
        _indexerProvider = indexerProvider;
        _newznabClient = newznabClient;
        _logger = logger;
    }

    public async Task<IndexerHealthStatus> GetHealthAsync(string indexerId, CancellationToken cancellationToken = default)
    {
        var indexer = await _indexerProvider.GetIndexerAsync(indexerId, cancellationToken);
        if (indexer == null)
        {
            throw new InvalidOperationException($"Indexer with ID {indexerId} not found");
        }

        return GetHealthStatus(indexerId, indexer.Name);
    }

    public async Task<IReadOnlyList<IndexerHealthStatus>> GetAllHealthAsync(CancellationToken cancellationToken = default)
    {
        var indexers = await _indexerProvider.GetIndexersAsync(cancellationToken);
        var statuses = new List<IndexerHealthStatus>();

        foreach (var indexer in indexers)
        {
            statuses.Add(GetHealthStatus(indexer.Id, indexer.Name));
        }

        return statuses;
    }

    public Task RecordSuccessAsync(string indexerId, TimeSpan responseTime, CancellationToken cancellationToken = default)
    {
        var data = GetOrCreateHealthData(indexerId);

        lock (data)
        {
            data.ResponseTimes.Add((DateTime.UtcNow, responseTime.TotalMilliseconds));
            TrimOldSamples(data.ResponseTimes);

            data.SuccessTimestamps.Add(DateTime.UtcNow);
            TrimOldTimestamps(data.SuccessTimestamps);

            data.LastSuccessAt = DateTime.UtcNow;
            data.LastResponseTimeMs = responseTime.TotalMilliseconds;
            data.ConsecutiveFailures = 0;
            data.LastUpdatedAt = DateTime.UtcNow;

            // Clear rate limit if response was successful
            if (data.RateLimitExpiresAt.HasValue && data.RateLimitExpiresAt.Value <= DateTime.UtcNow)
            {
                data.RateLimitExpiresAt = null;
            }
        }

        _logger?.LogDebug("Recorded success for indexer {IndexerId}: {ResponseTimeMs}ms", indexerId, responseTime.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(string indexerId, string errorMessage, bool isRateLimited = false, CancellationToken cancellationToken = default)
    {
        var data = GetOrCreateHealthData(indexerId);

        lock (data)
        {
            data.FailureTimestamps.Add(DateTime.UtcNow);
            TrimOldTimestamps(data.FailureTimestamps);

            data.LastFailureAt = DateTime.UtcNow;
            data.LastErrorMessage = errorMessage;
            data.ConsecutiveFailures++;
            data.LastUpdatedAt = DateTime.UtcNow;

            if (isRateLimited)
            {
                data.RateLimitExpiresAt = DateTime.UtcNow.AddMinutes(DefaultRateLimitMinutes);
                _logger?.LogWarning("Indexer {IndexerId} is rate limited until {ExpiresAt}", indexerId, data.RateLimitExpiresAt);
            }
        }

        _logger?.LogWarning("Recorded failure for indexer {IndexerId}: {ErrorMessage} (consecutive: {Count})",
            indexerId, errorMessage, data.ConsecutiveFailures);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<NewznabIndexer>> GetHealthyIndexersAsync(CancellationToken cancellationToken = default)
    {
        var enabledIndexers = await _indexerProvider.GetEnabledIndexersAsync(cancellationToken);
        var healthyIndexers = new List<NewznabIndexer>();

        foreach (var indexer in enabledIndexers)
        {
            var status = GetHealthStatus(indexer.Id, indexer.Name);

            if (status.IsHealthy && !status.IsRateLimited)
            {
                healthyIndexers.Add(indexer);
            }
            else
            {
                _logger?.LogDebug("Skipping indexer {IndexerName}: State={State}, RateLimited={RateLimited}",
                    indexer.Name, status.State, status.IsRateLimited);
            }
        }

        return healthyIndexers;
    }

    public Task<bool> IsRateLimitedAsync(string indexerId, CancellationToken cancellationToken = default)
    {
        if (_healthData.TryGetValue(indexerId, out var data))
        {
            lock (data)
            {
                return Task.FromResult(data.RateLimitExpiresAt.HasValue && data.RateLimitExpiresAt.Value > DateTime.UtcNow);
            }
        }

        return Task.FromResult(false);
    }

    public async Task<IndexerHealthCheckResult> CheckHealthAsync(string indexerId, CancellationToken cancellationToken = default)
    {
        var indexer = await _indexerProvider.GetIndexerAsync(indexerId, cancellationToken);
        if (indexer == null)
        {
            return new IndexerHealthCheckResult
            {
                IndexerId = indexerId,
                IndexerName = "Unknown",
                Success = false,
                ErrorMessage = "Indexer not found",
                CheckedAt = DateTime.UtcNow
            };
        }

        return await PerformHealthCheck(indexer, cancellationToken);
    }

    public async Task<IReadOnlyList<IndexerHealthCheckResult>> CheckAllHealthAsync(CancellationToken cancellationToken = default)
    {
        var indexers = await _indexerProvider.GetEnabledIndexersAsync(cancellationToken);
        var results = new List<IndexerHealthCheckResult>();

        foreach (var indexer in indexers)
        {
            var result = await PerformHealthCheck(indexer, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public Task ResetHealthAsync(string indexerId, CancellationToken cancellationToken = default)
    {
        if (_healthData.TryRemove(indexerId, out _))
        {
            _logger?.LogInformation("Reset health data for indexer {IndexerId}", indexerId);
        }

        return Task.CompletedTask;
    }

    public async Task<IndexerHealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        var allIndexers = await _indexerProvider.GetIndexersAsync(cancellationToken);
        var enabledIndexers = allIndexers.Where(i => i.Enabled).ToList();

        var healthyCount = 0;
        var degradedCount = 0;
        var unavailableCount = 0;
        var offlineCount = 0;
        var rateLimitedCount = 0;
        var totalResponseTime = 0.0;
        var responseSampleCount = 0;

        foreach (var indexer in enabledIndexers)
        {
            var status = GetHealthStatus(indexer.Id, indexer.Name);

            switch (status.State)
            {
                case IndexerHealthState.Healthy:
                    healthyCount++;
                    break;
                case IndexerHealthState.Degraded:
                    degradedCount++;
                    break;
                case IndexerHealthState.Unavailable:
                    unavailableCount++;
                    break;
                case IndexerHealthState.Offline:
                    offlineCount++;
                    break;
            }

            if (status.IsRateLimited)
            {
                rateLimitedCount++;
            }

            if (status.AverageResponseTimeMs > 0)
            {
                totalResponseTime += status.AverageResponseTimeMs;
                responseSampleCount++;
            }
        }

        return new IndexerHealthSummary
        {
            TotalIndexers = allIndexers.Count,
            EnabledIndexers = enabledIndexers.Count,
            HealthyIndexers = healthyCount,
            DegradedIndexers = degradedCount,
            UnavailableIndexers = unavailableCount,
            OfflineIndexers = offlineCount,
            RateLimitedIndexers = rateLimitedCount,
            AverageResponseTimeMs = responseSampleCount > 0 ? totalResponseTime / responseSampleCount : 0,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<IndexerHealthCheckResult> PerformHealthCheck(NewznabIndexer indexer, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Performing health check on indexer {IndexerName}", indexer.Name);
            var result = await _newznabClient.TestConnectionAsync(indexer, cancellationToken);

            var isRateLimited = result.StatusCode == 429 ||
                               (result.Message?.Contains("rate", StringComparison.OrdinalIgnoreCase) ?? false);

            if (result.Success)
            {
                await RecordSuccessAsync(indexer.Id, TimeSpan.FromMilliseconds(result.ResponseTimeMs), cancellationToken);
            }
            else
            {
                await RecordFailureAsync(indexer.Id, result.Message ?? "Unknown error", isRateLimited, cancellationToken);
            }

            return new IndexerHealthCheckResult
            {
                IndexerId = indexer.Id,
                IndexerName = indexer.Name,
                Success = result.Success,
                ResponseTimeMs = result.ResponseTimeMs,
                ErrorMessage = result.Success ? null : result.Message,
                StatusCode = result.StatusCode,
                IsRateLimited = isRateLimited,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(indexer.Id, ex.Message ?? "Unknown error", false, cancellationToken);

            return new IndexerHealthCheckResult
            {
                IndexerId = indexer.Id,
                IndexerName = indexer.Name,
                Success = false,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    private IndexerHealthStatus GetHealthStatus(string indexerId, string indexerName)
    {
        var data = GetOrCreateHealthData(indexerId);

        lock (data)
        {
            var now = DateTime.UtcNow;
            var windowStart = now - TrackingWindow;

            var successCount = data.SuccessTimestamps.Count(t => t >= windowStart);
            var failureCount = data.FailureTimestamps.Count(t => t >= windowStart);

            var recentResponseTimes = data.ResponseTimes
                .Where(r => r.Timestamp >= windowStart)
                .Select(r => r.ResponseTimeMs)
                .ToList();

            var avgResponseTime = recentResponseTimes.Count > 0 ? recentResponseTimes.Average() : 0;

            var isRateLimited = data.RateLimitExpiresAt.HasValue && data.RateLimitExpiresAt.Value > now;
            var state = DetermineHealthState(successCount, failureCount, data.ConsecutiveFailures, avgResponseTime, isRateLimited);

            return new IndexerHealthStatus
            {
                IndexerId = indexerId,
                IndexerName = indexerName,
                State = state,
                IsRateLimited = isRateLimited,
                RateLimitExpiresAt = isRateLimited ? data.RateLimitExpiresAt : null,
                AverageResponseTimeMs = avgResponseTime,
                LastResponseTimeMs = data.LastResponseTimeMs,
                SuccessCount = successCount,
                FailureCount = failureCount,
                LastSuccessAt = data.LastSuccessAt,
                LastFailureAt = data.LastFailureAt,
                LastErrorMessage = data.LastErrorMessage,
                ConsecutiveFailures = data.ConsecutiveFailures,
                LastUpdatedAt = data.LastUpdatedAt
            };
        }
    }

    private static IndexerHealthState DetermineHealthState(
        int successCount,
        int failureCount,
        int consecutiveFailures,
        double avgResponseTime,
        bool isRateLimited)
    {
        if (isRateLimited)
        {
            return IndexerHealthState.Unavailable;
        }

        if (consecutiveFailures >= ConsecutiveFailuresForOffline)
        {
            return IndexerHealthState.Offline;
        }

        if (successCount + failureCount == 0)
        {
            return IndexerHealthState.Unknown;
        }

        var successRate = (double)successCount / (successCount + failureCount);

        if (successRate < 0.5)
        {
            return IndexerHealthState.Offline;
        }

        if (successRate < 0.8 || avgResponseTime > DegradedResponseTimeMs)
        {
            return IndexerHealthState.Degraded;
        }

        return IndexerHealthState.Healthy;
    }

    private IndexerHealthData GetOrCreateHealthData(string indexerId)
    {
        return _healthData.GetOrAdd(indexerId, _ => new IndexerHealthData());
    }

    private static void TrimOldSamples(List<(DateTime Timestamp, double ResponseTimeMs)> samples)
    {
        while (samples.Count > MaxSamples)
        {
            samples.RemoveAt(0);
        }

        var cutoff = DateTime.UtcNow - TrackingWindow;
        samples.RemoveAll(s => s.Timestamp < cutoff);
    }

    private static void TrimOldTimestamps(List<DateTime> timestamps)
    {
        var cutoff = DateTime.UtcNow - TrackingWindow;
        timestamps.RemoveAll(t => t < cutoff);
    }

    private class IndexerHealthData
    {
        public List<(DateTime Timestamp, double ResponseTimeMs)> ResponseTimes { get; } = new();
        public List<DateTime> SuccessTimestamps { get; } = new();
        public List<DateTime> FailureTimestamps { get; } = new();
        public DateTime? LastSuccessAt { get; set; }
        public DateTime? LastFailureAt { get; set; }
        public double? LastResponseTimeMs { get; set; }
        public string? LastErrorMessage { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? RateLimitExpiresAt { get; set; }
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
