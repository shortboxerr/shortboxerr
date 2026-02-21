using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for periodic cover cache cleanup.
/// Enforces retention policy and cache size limits.
/// </summary>
public class CoverCacheCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CoverCacheCleanupBackgroundService> _logger;
    
    private DateTime _lastCleanup = DateTime.MinValue;

    public CoverCacheCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CoverCacheCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cover cache cleanup background service started");

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cover cache cleanup background service");
            }

            // Check every hour if cleanup is needed
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("Cover cache cleanup background service stopped");
    }

    private async Task CheckAndRunCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var coverService = scope.ServiceProvider.GetRequiredService<ICoverService>();

        var settings = await settingsService.GetAsync<CoverSettings>("covers", new CoverSettings(), cancellationToken)
            ?? new CoverSettings();

        // Check if cleanup is enabled and interval has passed
        if (settings.CleanupIntervalHours <= 0)
        {
            _logger.LogDebug("Cover cache cleanup is disabled (interval = 0)");
            return;
        }

        var timeSinceLastCleanup = DateTime.UtcNow - _lastCleanup;
        if (timeSinceLastCleanup.TotalHours < settings.CleanupIntervalHours)
        {
            _logger.LogDebug(
                "Cover cache cleanup not due yet ({Hours:F1}h since last, interval is {Interval}h)",
                timeSinceLastCleanup.TotalHours, settings.CleanupIntervalHours);
            return;
        }

        _logger.LogInformation("Running scheduled cover cache cleanup");

        var result = await coverService.CleanupCacheAsync(cancellationToken);
        _lastCleanup = DateTime.UtcNow;

        if (result.Success)
        {
            if (result.TotalEvicted > 0)
            {
                _logger.LogInformation(
                    "Scheduled cover cache cleanup completed: evicted {Total} covers, freed {Bytes} bytes",
                    result.TotalEvicted, result.BytesFreed);
            }
            else
            {
                _logger.LogDebug("Scheduled cover cache cleanup completed: no evictions needed");
            }
        }
        else
        {
            _logger.LogWarning("Scheduled cover cache cleanup failed: {Error}", result.Error);
        }
    }

    /// <summary>
    /// Triggers an immediate cleanup (for testing or manual invocation).
    /// </summary>
    public async Task TriggerCleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual cover cache cleanup triggered");

        using var scope = _scopeFactory.CreateScope();
        var coverService = scope.ServiceProvider.GetRequiredService<ICoverService>();

        var result = await coverService.CleanupCacheAsync(cancellationToken);
        _lastCleanup = DateTime.UtcNow;

        if (result.Success)
        {
            _logger.LogInformation(
                "Manual cover cache cleanup completed: evicted {Total} covers, freed {Bytes} bytes",
                result.TotalEvicted, result.BytesFreed);
        }
        else
        {
            _logger.LogWarning("Manual cover cache cleanup failed: {Error}", result.Error);
        }
    }
}
