using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// Service for monitoring download client health and managing failover.
/// </summary>
public class DownloadClientHealthService : IDownloadClientHealthService
{
    private readonly IProviderManager _providerManager;
    private readonly ILogger<DownloadClientHealthService>? _logger;

    private readonly ConcurrentDictionary<int, ClientHealthData> _healthData = new();

    private const int MaxSamples = 100;
    private const int DegradedDownloadTimeSeconds = 300;
    private const int ConsecutiveFailuresForOffline = 3;
    private static readonly TimeSpan TrackingWindow = TimeSpan.FromHours(24);

    public DownloadClientHealthService(
        IProviderManager providerManager,
        ILogger<DownloadClientHealthService>? logger = null)
    {
        _providerManager = providerManager;
        _logger = logger;
    }

    public async Task<DownloadClientHealthStatus> GetHealthAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providerManager.GetByIdAsync(providerId, cancellationToken);
        if (provider == null)
        {
            throw new InvalidOperationException($"Download client with ID {providerId} not found");
        }

        return GetHealthStatus(providerId, provider.Name, provider.Type);
    }

    public async Task<IReadOnlyList<DownloadClientHealthStatus>> GetAllHealthAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerManager.GetByCategoryAsync(ProviderCategory.DownloadClient, cancellationToken);
        var statuses = new List<DownloadClientHealthStatus>();

        foreach (var provider in providers)
        {
            statuses.Add(GetHealthStatus(provider.Id, provider.Name, provider.Type));
        }

        return statuses;
    }

    public Task RecordSuccessAsync(int providerId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var data = GetOrCreateHealthData(providerId);

        lock (data)
        {
            data.DownloadTimes.Add((DateTime.UtcNow, duration.TotalSeconds));
            TrimOldSamples(data.DownloadTimes);

            data.SuccessTimestamps.Add(DateTime.UtcNow);
            TrimOldTimestamps(data.SuccessTimestamps);

            data.LastSuccessAt = DateTime.UtcNow;
            data.LastDownloadTimeSeconds = duration.TotalSeconds;
            data.ConsecutiveFailures = 0;
            data.LastUpdatedAt = DateTime.UtcNow;
        }

        _logger?.LogDebug("Recorded download success for client {ProviderId}: {Duration}s", providerId, duration.TotalSeconds);
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(int providerId, string errorMessage, bool isTransient = false, CancellationToken cancellationToken = default)
    {
        var data = GetOrCreateHealthData(providerId);

        lock (data)
        {
            data.FailureTimestamps.Add(DateTime.UtcNow);
            TrimOldTimestamps(data.FailureTimestamps);

            data.LastFailureAt = DateTime.UtcNow;
            data.LastErrorMessage = errorMessage;
            data.ConsecutiveFailures++;
            data.LastUpdatedAt = DateTime.UtcNow;
        }

        _logger?.LogWarning("Recorded download failure for client {ProviderId}: {Error} (consecutive: {Count})",
            providerId, errorMessage, data.ConsecutiveFailures);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<IDownloadProvider>> GetHealthyClientsAsync(ProviderType? type = null, CancellationToken cancellationToken = default)
    {
        var allClients = await _providerManager.GetDownloadClientsAsync(cancellationToken);
        var healthyClients = new List<IDownloadProvider>();

        foreach (var client in allClients)
        {
            if (type.HasValue && client.Type != type.Value)
                continue;

            var status = GetHealthStatus(client.Id, client.Name, client.Type);

            if (status.IsHealthy)
            {
                healthyClients.Add(client);
            }
            else
            {
                _logger?.LogDebug("Skipping unhealthy download client {ClientName}: State={State}",
                    client.Name, status.State);
            }
        }

        return healthyClients.OrderBy(c => c.Priority).ToList();
    }

    public Task<bool> IsAvailableAsync(int providerId, CancellationToken cancellationToken = default)
    {
        if (_healthData.TryGetValue(providerId, out var data))
        {
            lock (data)
            {
                return Task.FromResult(data.ConsecutiveFailures < ConsecutiveFailuresForOffline);
            }
        }

        return Task.FromResult(true);
    }

    public async Task<DownloadClientCheckResult> CheckHealthAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providerManager.GetByIdAsync(providerId, cancellationToken);
        if (provider == null)
        {
            return new DownloadClientCheckResult
            {
                ProviderId = providerId,
                ProviderName = "Unknown",
                Success = false,
                ErrorMessage = "Download client not found",
                CheckedAt = DateTime.UtcNow
            };
        }

        return await PerformHealthCheck(providerId, provider, cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadClientCheckResult>> CheckAllHealthAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerManager.GetEnabledAsync(ProviderCategory.DownloadClient, cancellationToken);
        var results = new List<DownloadClientCheckResult>();

        foreach (var provider in providers)
        {
            var result = await PerformHealthCheck(provider.Id, provider, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public Task ResetHealthAsync(int providerId, CancellationToken cancellationToken = default)
    {
        if (_healthData.TryRemove(providerId, out _))
        {
            _logger?.LogInformation("Reset health data for download client {ProviderId}", providerId);
        }

        return Task.CompletedTask;
    }

    public async Task<DownloadClientHealthSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        var allProviders = await _providerManager.GetByCategoryAsync(ProviderCategory.DownloadClient, cancellationToken);
        var enabledProviders = allProviders.Where(p => p.IsEnabled).ToList();

        var healthyCount = 0;
        var degradedCount = 0;
        var unavailableCount = 0;
        var offlineCount = 0;
        var totalDownloadTime = 0.0;
        var downloadSampleCount = 0;

        foreach (var provider in enabledProviders)
        {
            var status = GetHealthStatus(provider.Id, provider.Name, provider.Type);

            switch (status.State)
            {
                case DownloadClientState.Healthy:
                    healthyCount++;
                    break;
                case DownloadClientState.Degraded:
                    degradedCount++;
                    break;
                case DownloadClientState.Unavailable:
                    unavailableCount++;
                    break;
                case DownloadClientState.Offline:
                    offlineCount++;
                    break;
            }

            if (status.AverageDownloadTimeSeconds > 0)
            {
                totalDownloadTime += status.AverageDownloadTimeSeconds;
                downloadSampleCount++;
            }
        }

        return new DownloadClientHealthSummary
        {
            TotalClients = allProviders.Count,
            EnabledClients = enabledProviders.Count,
            HealthyClients = healthyCount,
            DegradedClients = degradedCount,
            UnavailableClients = unavailableCount,
            OfflineClients = offlineCount,
            AverageDownloadTimeSeconds = downloadSampleCount > 0 ? totalDownloadTime / downloadSampleCount : 0,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<FailoverDownloadResult> DownloadWithFailoverAsync(
        Candidate candidate,
        ProviderType? preferredType = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<FailoverAttempt>();

        var healthyClients = await GetHealthyClientsAsync(preferredType, cancellationToken);

        if (healthyClients.Count == 0)
        {
            _logger?.LogWarning("No healthy download clients available for failover");
            return new FailoverDownloadResult
            {
                Success = false,
                AttemptsCount = 0,
                FinalErrorMessage = "No healthy download clients available",
                TotalDuration = stopwatch.Elapsed
            };
        }

        _logger?.LogInformation("Attempting download with failover across {Count} clients", healthyClients.Count);

        foreach (var client in healthyClients)
        {
            var attemptStopwatch = Stopwatch.StartNew();

            try
            {
                _logger?.LogDebug("Attempting download with {ClientName}", client.Name);

                var result = await client.DownloadAsync(candidate, cancellationToken);
                attemptStopwatch.Stop();

                if (result.Success)
                {
                    await RecordSuccessAsync(client.Id, attemptStopwatch.Elapsed, cancellationToken);

                    attempts.Add(new FailoverAttempt
                    {
                        ProviderId = client.Id,
                        ProviderName = client.Name,
                        Success = true,
                        Duration = attemptStopwatch.Elapsed
                    });

                    _logger?.LogInformation("Download succeeded with {ClientName} in {Duration}s",
                        client.Name, attemptStopwatch.Elapsed.TotalSeconds);

                    return new FailoverDownloadResult
                    {
                        Success = true,
                        DownloadId = result.DownloadId,
                        UsedProviderId = client.Id,
                        UsedProviderName = client.Name,
                        AttemptsCount = attempts.Count,
                        Attempts = attempts,
                        TotalDuration = stopwatch.Elapsed
                    };
                }
                else
                {
                    await RecordFailureAsync(client.Id, result.Error ?? "Unknown error", isTransient: true, cancellationToken);

                    attempts.Add(new FailoverAttempt
                    {
                        ProviderId = client.Id,
                        ProviderName = client.Name,
                        Success = false,
                        ErrorMessage = result.Error,
                        Duration = attemptStopwatch.Elapsed
                    });

                    _logger?.LogWarning("Download failed with {ClientName}: {Error}, trying next client",
                        client.Name, result.Error);
                }
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                await RecordFailureAsync(client.Id, ex.Message, isTransient: true, cancellationToken);

                attempts.Add(new FailoverAttempt
                {
                    ProviderId = client.Id,
                    ProviderName = client.Name,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = attemptStopwatch.Elapsed
                });

                _logger?.LogWarning(ex, "Download exception with {ClientName}, trying next client", client.Name);
            }
        }

        stopwatch.Stop();

        var finalError = attempts.LastOrDefault()?.ErrorMessage ?? "All download clients failed";
        _logger?.LogError("Download failover exhausted all {Count} clients, final error: {Error}",
            healthyClients.Count, finalError);

        return new FailoverDownloadResult
        {
            Success = false,
            AttemptsCount = attempts.Count,
            Attempts = attempts,
            FinalErrorMessage = finalError,
            TotalDuration = stopwatch.Elapsed
        };
    }

    private async Task<DownloadClientCheckResult> PerformHealthCheck(int providerId, ProviderDefinition provider, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug("Performing health check on download client {ClientName}", provider.Name);
            var testResult = await _providerManager.TestAsync(providerId, cancellationToken);
            stopwatch.Stop();

            if (testResult.Success)
            {
                await RecordSuccessAsync(providerId, stopwatch.Elapsed, cancellationToken);
            }
            else
            {
                await RecordFailureAsync(providerId, testResult.Message ?? "Test failed", isTransient: false, cancellationToken);
            }

            return new DownloadClientCheckResult
            {
                ProviderId = providerId,
                ProviderName = provider.Name,
                Type = provider.Type,
                Success = testResult.Success,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = testResult.Success ? null : testResult.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await RecordFailureAsync(providerId, ex.Message, isTransient: false, cancellationToken);

            return new DownloadClientCheckResult
            {
                ProviderId = providerId,
                ProviderName = provider.Name,
                Type = provider.Type,
                Success = false,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    private DownloadClientHealthStatus GetHealthStatus(int providerId, string providerName, ProviderType type)
    {
        var data = GetOrCreateHealthData(providerId);

        lock (data)
        {
            var now = DateTime.UtcNow;
            var windowStart = now - TrackingWindow;

            var successCount = data.SuccessTimestamps.Count(t => t >= windowStart);
            var failureCount = data.FailureTimestamps.Count(t => t >= windowStart);

            var recentDownloadTimes = data.DownloadTimes
                .Where(r => r.Timestamp >= windowStart)
                .Select(r => r.DownloadTimeSeconds)
                .ToList();

            var avgDownloadTime = recentDownloadTimes.Count > 0 ? recentDownloadTimes.Average() : 0;
            var state = DetermineHealthState(successCount, failureCount, data.ConsecutiveFailures, avgDownloadTime);

            return new DownloadClientHealthStatus
            {
                ProviderId = providerId,
                ProviderName = providerName,
                Type = type,
                State = state,
                AverageDownloadTimeSeconds = avgDownloadTime,
                LastDownloadTimeSeconds = data.LastDownloadTimeSeconds,
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

    private static DownloadClientState DetermineHealthState(
        int successCount,
        int failureCount,
        int consecutiveFailures,
        double avgDownloadTime)
    {
        if (consecutiveFailures >= ConsecutiveFailuresForOffline)
        {
            return DownloadClientState.Offline;
        }

        if (successCount + failureCount == 0)
        {
            return DownloadClientState.Unknown;
        }

        var successRate = (double)successCount / (successCount + failureCount);

        if (successRate < 0.5)
        {
            return DownloadClientState.Unavailable;
        }

        if (successRate < 0.8 || avgDownloadTime > DegradedDownloadTimeSeconds)
        {
            return DownloadClientState.Degraded;
        }

        return DownloadClientState.Healthy;
    }

    private ClientHealthData GetOrCreateHealthData(int providerId)
    {
        return _healthData.GetOrAdd(providerId, _ => new ClientHealthData());
    }

    private static void TrimOldSamples(List<(DateTime Timestamp, double DownloadTimeSeconds)> samples)
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

    private class ClientHealthData
    {
        public List<(DateTime Timestamp, double DownloadTimeSeconds)> DownloadTimes { get; } = new();
        public List<DateTime> SuccessTimestamps { get; } = new();
        public List<DateTime> FailureTimestamps { get; } = new();
        public DateTime? LastSuccessAt { get; set; }
        public DateTime? LastFailureAt { get; set; }
        public double? LastDownloadTimeSeconds { get; set; }
        public string? LastErrorMessage { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
