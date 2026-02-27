using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically checks and logs system health.
/// </summary>
public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;
    private readonly long _diskSpaceWarningThresholdBytes;

    public HealthCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HealthCheckBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Default to 5 minutes between health checks
        var intervalMinutes = configuration.GetValue("HealthCheck:IntervalMinutes", 5);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
        
        // Default to 1GB warning threshold
        _diskSpaceWarningThresholdBytes = configuration.GetValue("HealthCheck:DiskSpaceWarningGB", 1L) * 1024 * 1024 * 1024;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health check background service starting. Check interval: {Interval}", _checkInterval);

        // Initial delay to allow application to fully start
        _logger.LogDebug("Waiting 30 seconds before first health check");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Health check service cancelled during startup delay");
            return;
        }

        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting periodic health check");
                await RunHealthChecksAsync(stoppingToken);
                consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Health check cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error in health check background service (attempt {Attempt})", consecutiveErrors);
                
                if (consecutiveErrors >= 3)
                {
                    _logger.LogWarning("Multiple consecutive health check errors ({Count}). System may be unhealthy.", consecutiveErrors);
                }
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Health check background service stopping");
    }

    private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
    {
        var results = new List<HealthCheckResult>();

        // Database connectivity
        results.Add(await CheckDatabaseAsync(cancellationToken));

        // ComicVine API reachability
        results.Add(await CheckComicVineApiAsync(cancellationToken));

        // Disk space
        results.Add(CheckDiskSpace());

        // Log summary
        var healthy = results.Count(r => r.Status == HealthStatus.Healthy);
        var degraded = results.Count(r => r.Status == HealthStatus.Degraded);
        var unhealthy = results.Count(r => r.Status == HealthStatus.Unhealthy);

        if (unhealthy > 0)
        {
            _logger.LogWarning("Health check summary: {Healthy} healthy, {Degraded} degraded, {Unhealthy} unhealthy",
                healthy, degraded, unhealthy);
        }
        else if (degraded > 0)
        {
            _logger.LogInformation("Health check summary: {Healthy} healthy, {Degraded} degraded",
                healthy, degraded);
        }
        else
        {
            _logger.LogDebug("Health check summary: All {Count} checks healthy", results.Count);
        }

        // Log individual results
        foreach (var result in results)
        {
            switch (result.Status)
            {
                case HealthStatus.Healthy:
                    _logger.LogDebug("Health check [{Name}]: {Status} - {Description}",
                        result.Name, result.Status, result.Description);
                    break;
                case HealthStatus.Degraded:
                    _logger.LogWarning("Health check [{Name}]: {Status} - {Description}",
                        result.Name, result.Status, result.Description);
                    break;
                case HealthStatus.Unhealthy:
                    _logger.LogError("Health check [{Name}]: {Status} - {Description}",
                        result.Name, result.Status, result.Description);
                    break;
            }
        }
    }

    private async Task<HealthCheckResult> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();

            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                // Also check if we can execute a simple query
                var seriesCount = await db.Series.CountAsync(cancellationToken);
                return new HealthCheckResult
                {
                    Name = "Database",
                    Status = HealthStatus.Healthy,
                    Description = $"Connected. {seriesCount} series in database."
                };
            }
            
            return new HealthCheckResult
            {
                Name = "Database",
                Status = HealthStatus.Unhealthy,
                Description = "Cannot connect to database"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Name = "Database",
                Status = HealthStatus.Unhealthy,
                Description = $"Database error: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckResult> CheckComicVineApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var comicVineClient = scope.ServiceProvider.GetService<IComicVineClient>();

            if (comicVineClient == null)
            {
                return new HealthCheckResult
                {
                    Name = "ComicVine API",
                    Status = HealthStatus.Degraded,
                    Description = "ComicVine client not configured"
                };
            }

            // Try to get API status (a lightweight call)
            var testResult = await comicVineClient.TestConnectionAsync(cancellationToken);

            if (testResult.Success)
            {
                var latencyInfo = testResult.LatencyMs.HasValue ? $" ({testResult.LatencyMs}ms)" : "";
                return new HealthCheckResult
                {
                    Name = "ComicVine API",
                    Status = HealthStatus.Healthy,
                    Description = $"API reachable{latencyInfo}"
                };
            }

            return new HealthCheckResult
            {
                Name = "ComicVine API",
                Status = HealthStatus.Degraded,
                Description = $"API check failed: {testResult.Message}"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Name = "ComicVine API",
                Status = HealthStatus.Degraded,
                Description = $"API check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckDiskSpace()
    {
        try
        {
            // Use centralized container-first path logic
            var dataDirectory = Logging.SerilogConfiguration.GetDataDirectory();

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            var rootPath = Path.GetPathRoot(dataDirectory);
            if (string.IsNullOrEmpty(rootPath))
            {
                return new HealthCheckResult
                {
                    Name = "Disk Space",
                    Status = HealthStatus.Degraded,
                    Description = "Cannot determine disk root path"
                };
            }

            var driveInfo = new DriveInfo(rootPath);
            var freeBytes = driveInfo.AvailableFreeSpace;
            var totalBytes = driveInfo.TotalSize;
            var usedPercent = (double)(totalBytes - freeBytes) / totalBytes * 100;

            var freeGb = freeBytes / (1024.0 * 1024 * 1024);

            if (freeBytes < _diskSpaceWarningThresholdBytes)
            {
                return new HealthCheckResult
                {
                    Name = "Disk Space",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Low disk space: {freeGb:F1} GB free ({usedPercent:F1}% used)"
                };
            }

            if (freeBytes < _diskSpaceWarningThresholdBytes * 2)
            {
                return new HealthCheckResult
                {
                    Name = "Disk Space",
                    Status = HealthStatus.Degraded,
                    Description = $"Disk space warning: {freeGb:F1} GB free ({usedPercent:F1}% used)"
                };
            }

            return new HealthCheckResult
            {
                Name = "Disk Space",
                Status = HealthStatus.Healthy,
                Description = $"{freeGb:F1} GB free ({usedPercent:F1}% used)"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Name = "Disk Space",
                Status = HealthStatus.Degraded,
                Description = $"Disk check failed: {ex.Message}"
            };
        }
    }

    private class HealthCheckResult
    {
        public required string Name { get; set; }
        public HealthStatus Status { get; set; }
        public required string Description { get; set; }
    }

    private enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
}
