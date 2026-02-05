using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically refreshes ComicVine discovery data.
/// Ensures fresh release schedules are available for automation (auto-add to wanted list)
/// even when users don't visit the UI.
/// 
/// This provides Mylar3 parity - Mylar3 refreshes its weekly releases every ~4 hours.
/// 
/// On startup, this service pre-populates the cache for:
/// - Current week
/// - Past weeks (based on PastWeeksToShow setting)
/// - Upcoming weeks (based on DiscoveryRefreshWeeksAhead setting)
/// </summary>
public class ComicVineRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComicVineRefreshBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 mins
    private readonly TimeSpan _betweenWeeksDelay = TimeSpan.FromSeconds(3); // Delay between fetching weeks
    private DateTime _lastRefresh = DateTime.MinValue;
    private int _lastKnownPastWeeksToShow = 0; // Track setting changes
    private bool _initialCachePopulationDone = false;

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

        // Initial delay to allow application to fully start and migrations to run
        _logger.LogDebug("Waiting 30 seconds before initial cache population");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // On first run, populate cache for all configured weeks
                if (!_initialCachePopulationDone)
                {
                    _logger.LogInformation("Starting initial cache population for pull list discovery");
                    await PopulateMissingCacheAsync(stoppingToken);
                    _initialCachePopulationDone = true;
                }

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

    /// <summary>
    /// Populates missing cache entries for all configured weeks.
    /// Order: Current week first, then past weeks (most recent to oldest).
    /// </summary>
    private async Task PopulateMissingCacheAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();
        var pullListService = scope.ServiceProvider.GetRequiredService<IPullListService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        
        // Check if ComicVine is configured
        if (!await comicVineClient.IsConfiguredAsync(cancellationToken))
        {
            _logger.LogWarning("ComicVine API is not configured, skipping initial cache population");
            return;
        }

        var pullListSettings = await pullListService.GetSettingsAsync(cancellationToken) ?? new PullListSettings();
        var comicVineSettings = await settingsService.GetAsync<ComicVineSettings>("comicvine", new(), cancellationToken);
        
        // Track the setting for change detection
        _lastKnownPastWeeksToShow = pullListSettings.PastWeeksToShow;
        
        // Build list of weeks to cache: current week first, then past weeks
        var weeksToCache = new List<DateTime>();
        
        // Current week
        var currentWeekStart = GetWeekStart(DateTime.Today);
        weeksToCache.Add(currentWeekStart);
        
        // Past weeks (most recent first)
        for (var i = 1; i <= pullListSettings.PastWeeksToShow; i++)
        {
            weeksToCache.Add(currentWeekStart.AddDays(-7 * i));
        }
        
        // Upcoming weeks
        for (var i = 1; i < comicVineSettings.DiscoveryRefreshWeeksAhead; i++)
        {
            weeksToCache.Add(currentWeekStart.AddDays(7 * i));
        }
        
        // Get existing cached weeks from database
        var existingCachedWeeks = await dbContext.CachedDiscoveryWeeks
            .Select(c => c.WeekStart.Date)
            .ToListAsync(cancellationToken);
        
        // Find missing weeks
        var missingWeeks = weeksToCache.Where(w => !existingCachedWeeks.Contains(w.Date)).ToList();
        
        if (missingWeeks.Count == 0)
        {
            _logger.LogInformation("All {Count} weeks already cached in database", weeksToCache.Count);
            return;
        }
        
        _logger.LogInformation("Found {Missing} weeks to populate out of {Total} configured weeks", 
            missingWeeks.Count, weeksToCache.Count);
        
        var populatedCount = 0;
        var errorCount = 0;
        
        foreach (var weekStart in missingWeeks)
        {
            try
            {
                _logger.LogDebug("Populating cache for week of {Date}", weekStart);
                
                // This will fetch from ComicVine and persist to database
                await pullListService.GetWeeklyDiscoveryAsync(weekStart, null, cancellationToken);
                populatedCount++;
                
                // Delay between fetches to respect rate limits
                if (populatedCount < missingWeeks.Count)
                {
                    await Task.Delay(_betweenWeeksDelay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate cache for week of {Date}", weekStart);
                errorCount++;
            }
        }
        
        _logger.LogInformation("Initial cache population completed: {Populated} populated, {Errors} errors", 
            populatedCount, errorCount);
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

        var pullListService = scope.ServiceProvider.GetRequiredService<IPullListService>();
        var pullListSettings = await pullListService.GetSettingsAsync(cancellationToken) ?? new PullListSettings();

        // Check if PastWeeksToShow changed - populate newly needed weeks
        if (pullListSettings.PastWeeksToShow > _lastKnownPastWeeksToShow && _lastKnownPastWeeksToShow > 0)
        {
            _logger.LogInformation("PastWeeksToShow increased from {Old} to {New}, populating new weeks",
                _lastKnownPastWeeksToShow, pullListSettings.PastWeeksToShow);
            
            await PopulateNewlyNeededWeeksAsync(
                _lastKnownPastWeeksToShow, 
                pullListSettings.PastWeeksToShow, 
                pullListService, 
                cancellationToken);
        }
        _lastKnownPastWeeksToShow = pullListSettings.PastWeeksToShow;

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
                    await Task.Delay(_betweenWeeksDelay, cancellationToken);
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
                    
                    await Task.Delay(_betweenWeeksDelay, cancellationToken);
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
    /// Populates weeks that are newly needed due to PastWeeksToShow setting increase.
    /// </summary>
    private async Task PopulateNewlyNeededWeeksAsync(
        int oldPastWeeks, 
        int newPastWeeks,
        IPullListService pullListService,
        CancellationToken cancellationToken)
    {
        var currentWeekStart = GetWeekStart(DateTime.Today);
        var populatedCount = 0;
        
        // Only fetch the newly needed weeks (from old+1 to new)
        for (var i = oldPastWeeks + 1; i <= newPastWeeks; i++)
        {
            var weekStart = currentWeekStart.AddDays(-7 * i);
            
            try
            {
                _logger.LogDebug("Populating newly needed week {Date} (week -{WeekOffset})", weekStart, i);
                await pullListService.GetWeeklyDiscoveryAsync(weekStart, null, cancellationToken);
                populatedCount++;
                
                if (i < newPastWeeks)
                {
                    await Task.Delay(_betweenWeeksDelay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate newly needed week {Date}", weekStart);
            }
        }
        
        _logger.LogInformation("Populated {Count} newly needed past weeks", populatedCount);
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

    /// <summary>
    /// Gets the start of the week (Sunday) for a given date.
    /// </summary>
    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
