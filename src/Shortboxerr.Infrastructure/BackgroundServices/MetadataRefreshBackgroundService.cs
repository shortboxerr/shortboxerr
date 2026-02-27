using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically refreshes stale metadata from ComicVine.
/// </summary>
public class MetadataRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetadataRefreshBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public MetadataRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MetadataRefreshBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metadata refresh background service starting. Check interval: {Interval}", _checkInterval);

        // Initial delay to allow application to fully start
        _logger.LogDebug("Waiting 5 minutes before first metadata refresh check");
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Metadata refresh service cancelled during startup delay");
            return;
        }

        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting scheduled metadata refresh check");
                await CheckAndRefreshAsync(stoppingToken);
                consecutiveErrors = 0; // Reset on success
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                _logger.LogDebug("Metadata refresh cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error in metadata refresh background service (attempt {Attempt})", consecutiveErrors);
                
                if (consecutiveErrors >= 3)
                {
                    _logger.LogWarning("Multiple consecutive errors ({Count}). Will continue trying but may indicate a persistent issue.", consecutiveErrors);
                }
            }

            _logger.LogDebug("Next metadata refresh check in {Interval}", _checkInterval);
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Metadata refresh background service stopping");
    }

    private async Task CheckAndRefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<IMetadataRefreshService>();

        var settings = await refreshService.GetSettingsAsync(cancellationToken);
        
        // Check if scheduled refresh is enabled
        if (!settings.ScheduledRefreshEnabled)
        {
            _logger.LogDebug("Scheduled metadata refresh is disabled");
            return;
        }

        // Check if we're in an allowed hour
        var currentHour = DateTime.Now.Hour;
        if (!settings.AllowedHours.Contains(currentHour))
        {
            _logger.LogDebug("Current hour {Hour} is not in allowed hours for refresh", currentHour);
            return;
        }

        // Check if there are stale series to refresh
        var staleCount = await refreshService.GetStaleSeriesCountAsync(cancellationToken);
        if (staleCount == 0)
        {
            _logger.LogDebug("No stale series to refresh");
            return;
        }

        _logger.LogInformation("Starting scheduled metadata refresh for {Count} stale series", staleCount);

        var result = await refreshService.RefreshStaleSeriesAsync(
            settings.RefreshInterval,
            progress: null,
            cancellationToken);

        _logger.LogInformation(
            "Scheduled metadata refresh completed: {Refreshed} refreshed, {Errors} errors, {NewIssues} new issues, in {Duration}",
            result.Refreshed, result.Errors, result.NewIssuesDiscovered, result.Duration);
    }
}

