using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically checks the health of NZB indexers.
/// </summary>
public class IndexerHealthBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexerHealthBackgroundService> _logger;

    private const int CheckIntervalMinutes = 15;
    private DateTime _lastCheckTime = DateTime.MinValue;
    private bool _isRunning;

    public IndexerHealthBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<IndexerHealthBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether a health check is currently in progress.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// The last time a health check was performed.
    /// </summary>
    public DateTime LastCheckTime => _lastCheckTime;

    /// <summary>
    /// Manually triggers a health check.
    /// </summary>
    public async Task<IReadOnlyList<IndexerHealthCheckResult>> TriggerHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual health check triggered");
        return await PerformHealthCheckAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Indexer health monitoring service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextCheckTime = _lastCheckTime.AddMinutes(CheckIntervalMinutes);
                var now = DateTime.UtcNow;

                if (now >= nextCheckTime)
                {
                    await PerformHealthCheckAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in indexer health monitoring loop");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Indexer health monitoring service stopped");
    }

    private async Task<IReadOnlyList<IndexerHealthCheckResult>> PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            _logger.LogDebug("Health check already in progress, skipping");
            return Array.Empty<IndexerHealthCheckResult>();
        }

        _isRunning = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var healthService = scope.ServiceProvider.GetRequiredService<IIndexerHealthService>();

            _logger.LogDebug("Starting indexer health check");
            var results = await healthService.CheckAllHealthAsync(cancellationToken);
            _lastCheckTime = DateTime.UtcNow;

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            if (failureCount > 0)
            {
                _logger.LogWarning("Indexer health check completed: {Success} healthy, {Failure} unhealthy",
                    successCount, failureCount);

                foreach (var failed in results.Where(r => !r.Success))
                {
                    _logger.LogWarning("Indexer {IndexerName} is unhealthy: {Error}",
                        failed.IndexerName, failed.ErrorMessage);
                }
            }
            else
            {
                _logger.LogInformation("Indexer health check completed: all {Count} indexers healthy", successCount);
            }

            var summary = await healthService.GetHealthSummaryAsync(cancellationToken);
            _logger.LogDebug(
                "Health summary: {Healthy} healthy, {Degraded} degraded, {Unavailable} unavailable, {Offline} offline, {RateLimited} rate-limited",
                summary.HealthyIndexers,
                summary.DegradedIndexers,
                summary.UnavailableIndexers,
                summary.OfflineIndexers,
                summary.RateLimitedIndexers);

            return results;
        }
        finally
        {
            _isRunning = false;
        }
    }
}
