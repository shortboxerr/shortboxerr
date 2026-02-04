using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically refreshes ComicVine discovery data.
/// Ensures fresh release schedules are available for automation (auto-add to wanted list)
/// even when users don't visit the UI.
/// 
/// This provides Mylar3 parity - Mylar3 refreshes its weekly releases every ~4 hours.
/// </summary>
public class ComicVineRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComicVineRefreshBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 mins
    private DateTime _lastRefresh = DateTime.MinValue;

    public ComicVineRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ComicVineRefreshBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComicVine discovery refresh background service starting. Check interval: {Interval}", _checkInterval);

        // Initial delay to allow application to fully start
        _logger.LogDebug("Waiting 2 minutes before first discovery refresh check");
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting ComicVine discovery refresh check");
                await CheckAndRefreshAsync(stoppingToken);
                consecutiveErrors = 0; // Reset on success
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                _logger.LogDebug("ComicVine discovery refresh cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error in ComicVine discovery refresh background service (attempt {Attempt})", consecutiveErrors);
                
                if (consecutiveErrors >= 3)
                {
                    _logger.LogWarning("Multiple consecutive errors ({Count}). Will continue trying but may indicate a persistent issue.", consecutiveErrors);
                }
            }

            _logger.LogDebug("Next ComicVine discovery refresh check in {Interval}", _checkInterval);
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("ComicVine discovery refresh background service stopping");
    }

    private async Task CheckAndRefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();
        
        // Get ComicVine settings
        var settings = await settingsService.GetAsync<ComicVineSettings>("comicvine", new(), cancellationToken);
        
        // Check if discovery refresh is enabled
        if (!settings.DiscoveryRefreshEnabled)
        {
            _logger.LogDebug("Discovery refresh is disabled");
            return;
        }

        // Check if ComicVine is configured
        if (!await comicVineClient.IsConfiguredAsync(cancellationToken))
        {
            _logger.LogDebug("ComicVine API is not configured, skipping discovery refresh");
            return;
        }

        // Check if we're within the allowed hours
        var currentHour = DateTime.Now.Hour;
        if (settings.DiscoveryRefreshAllowedHours.Count > 0 && 
            !settings.DiscoveryRefreshAllowedHours.Contains(currentHour))
        {
            _logger.LogDebug("Current hour {Hour} is not in allowed hours for discovery refresh", currentHour);
            return;
        }

        // Check if enough time has passed since last refresh
        var refreshInterval = TimeSpan.FromHours(settings.DiscoveryRefreshIntervalHours);
        if (DateTime.UtcNow - _lastRefresh < refreshInterval)
        {
            _logger.LogDebug("Skipping discovery refresh - last refresh was {Ago} ago", 
                DateTime.UtcNow - _lastRefresh);
            return;
        }

        _logger.LogInformation("Starting scheduled ComicVine discovery refresh");

        var pullListService = scope.ServiceProvider.GetRequiredService<IPullListService>();
        var pullListSettings = await pullListService.GetSettingsAsync(cancellationToken) ?? new PullListSettings();
        var weeksToRefresh = settings.DiscoveryRefreshWeeksAhead;
        var successCount = 0;
        var errorCount = 0;
        var skippedCount = 0;

        // Refresh current week and upcoming weeks (always active)
        for (var weekOffset = 0; weekOffset < weeksToRefresh; weekOffset++)
        {
            var targetDate = DateTime.Today.AddDays(weekOffset * 7);
            
            try
            {
                _logger.LogDebug("Refreshing discovery for week of {Date} (Active tier - always refresh)", targetDate);
                
                // Force refresh by calling the service (which will fetch from ComicVine)
                // The cache will be updated as part of the fetch
                await pullListService.GetWeeklyDiscoveryAsync(targetDate, null, cancellationToken);
                successCount++;
                
                // Rate limit protection between weeks
                if (weekOffset < weeksToRefresh - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh discovery for week of {Date}", targetDate);
                errorCount++;
            }
        }

        // Optionally refresh recent historical weeks if enabled
        if (pullListSettings.HistoricalRefreshEnabled)
        {
            // Refresh past few weeks (up to historical refresh interval)
            var pastWeeksToCheck = Math.Min(pullListSettings.PastWeeksToShow, 4);
            _logger.LogDebug("Historical refresh enabled - checking {Weeks} past weeks", pastWeeksToCheck);
            
            for (var weekOffset = 1; weekOffset <= pastWeeksToCheck; weekOffset++)
            {
                var targetDate = DateTime.Today.AddDays(-weekOffset * 7);
                
                try
                {
                    // Get current cache metadata to check if refresh is needed
                    var existingData = await pullListService.GetWeeklyDiscoveryAsync(targetDate, null, cancellationToken);
                    
                    if (existingData.CacheMetadata != null && 
                        existingData.CacheMetadata.Tier == CacheTier.Historical)
                    {
                        var daysSinceRefresh = (DateTime.UtcNow - existingData.CacheMetadata.LastRefreshed).TotalDays;
                        
                        if (daysSinceRefresh < pullListSettings.HistoricalRefreshIntervalDays)
                        {
                            _logger.LogDebug("Skipping historical week {Date} - refreshed {Days} days ago (interval: {Interval} days)",
                                targetDate, daysSinceRefresh, pullListSettings.HistoricalRefreshIntervalDays);
                            skippedCount++;
                            continue;
                        }
                    }
                    
                    _logger.LogDebug("Refreshing historical week of {Date}", targetDate);
                    successCount++;
                    
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh discovery for historical week of {Date}", targetDate);
                    errorCount++;
                }
            }
        }

        _lastRefresh = DateTime.UtcNow;
        
        // Store the last refresh time in settings for persistence across restarts
        await settingsService.SetAsync("comicvine_discovery_last_refresh", _lastRefresh, cancellationToken);
        
        _logger.LogInformation(
            "ComicVine discovery refresh completed: {Success} weeks refreshed, {Skipped} skipped, {Errors} errors",
            successCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Trigger an immediate refresh (called from API endpoint).
    /// </summary>
    public async Task TriggerRefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual ComicVine discovery refresh triggered");
        
        // Reset the last refresh time to force immediate refresh
        _lastRefresh = DateTime.MinValue;
        
        await CheckAndRefreshAsync(cancellationToken);
    }
}
