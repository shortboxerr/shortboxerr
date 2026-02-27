using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes release day automation.
/// On the configured release day (default: Wednesday), automatically adds new issues 
/// to the wanted list based on each series' monitoring mode.
/// 
/// This provides Mylar3 parity for automatic wanted list management.
/// </summary>
public class ReleaseDayBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReleaseDayBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30); // Check every 30 mins
    private const string LastProcessedSettingKey = "pulllist_release_day_last_processed";

    public ReleaseDayBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReleaseDayBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Release day background service starting. Check interval: {Interval}", _checkInterval);

        // Initial delay to allow application to fully start
        _logger.LogDebug("Waiting 3 minutes before first release day check");
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Release day service cancelled during startup delay");
            return;
        }

        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting release day processing check");
                await CheckAndProcessReleaseDayAsync(stoppingToken);
                consecutiveErrors = 0; // Reset on success
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                _logger.LogDebug("Release day processing cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error in release day background service (attempt {Attempt})", consecutiveErrors);
                
                if (consecutiveErrors >= 3)
                {
                    _logger.LogWarning("Multiple consecutive errors ({Count}). Will continue trying but may indicate a persistent issue.", consecutiveErrors);
                }
            }

            _logger.LogDebug("Next release day check in {Interval}", _checkInterval);
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Release day background service stopping");
    }

    private async Task CheckAndProcessReleaseDayAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var pullListService = scope.ServiceProvider.GetRequiredService<IPullListService>();

        // Get pull list settings (null-coalesce for safety even though we provide a default)
        var settings = await settingsService.GetAsync<PullListSettings>("pulllist", new(), cancellationToken) ?? new PullListSettings();

        // Check if auto-add is enabled
        if (!settings.AutoAddToWanted)
        {
            _logger.LogDebug("Auto-add to wanted list is disabled");
            return;
        }

        // Check if today is the release day
        var today = DateTime.Today;
        if (today.DayOfWeek != settings.ReleaseDay)
        {
            _logger.LogDebug("Today ({Today}) is not release day ({ReleaseDay})", 
                today.DayOfWeek, settings.ReleaseDay);
            return;
        }

        // Check if we're within the allowed processing hours (if configured)
        var currentHour = DateTime.Now.Hour;
        if (settings.ReleaseDayProcessingHours.Count > 0 && 
            !settings.ReleaseDayProcessingHours.Contains(currentHour))
        {
            _logger.LogDebug("Current hour {Hour} is not in allowed processing hours", currentHour);
            return;
        }

        // Check if we've already processed today
        var lastProcessed = await settingsService.GetAsync<DateTime?>(LastProcessedSettingKey, null, cancellationToken);
        if (lastProcessed.HasValue && lastProcessed.Value.Date == today.Date)
        {
            _logger.LogDebug("Already processed release day for {Date}", today.ToShortDateString());
            return;
        }

        _logger.LogInformation("Processing release day for {Date}", today.ToShortDateString());

        // Process the release day
        var result = await pullListService.ProcessReleaseDayAsync(today, cancellationToken);

        if (result.Success)
        {
            // Save the last processed date
            await settingsService.SetAsync(LastProcessedSettingKey, DateTime.UtcNow, cancellationToken);

            _logger.LogInformation(
                "Release day processing completed: {SeriesCount} series processed, {IssuesAdded} issues added to wanted list",
                result.SeriesProcessed, result.IssuesAdded);

            // Send notification if any issues were added
            if (result.IssuesAdded > 0)
            {
                try
                {
                    var notificationService = scope.ServiceProvider.GetService<INotificationService>();
                    if (notificationService != null)
                    {
                        await notificationService.SendWeeklySummaryAsync(
                            new WeeklySummaryRequest
                            {
                                WeekOf = today,
                                TotalReleases = result.SeriesProcessed,
                                WantedCount = result.IssuesAdded
                            },
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send release day notification");
                }
            }
        }
        else
        {
            _logger.LogWarning("Release day processing failed: {Error}", result.Error);
        }
    }

    /// <summary>
    /// Trigger an immediate release day processing (called from API endpoint).
    /// </summary>
    public async Task TriggerProcessingAsync(DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var processDate = date ?? DateTime.Today;
        _logger.LogInformation("Manual release day processing triggered for {Date}", processDate.ToShortDateString());

        using var scope = _serviceProvider.CreateScope();
        var pullListService = scope.ServiceProvider.GetRequiredService<IPullListService>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        var result = await pullListService.ProcessReleaseDayAsync(processDate, cancellationToken);

        if (result.Success)
        {
            await settingsService.SetAsync(LastProcessedSettingKey, DateTime.UtcNow, cancellationToken);
            
            _logger.LogInformation(
                "Manual release day processing completed: {SeriesCount} series, {IssuesAdded} issues added",
                result.SeriesProcessed, result.IssuesAdded);
        }
        else
        {
            _logger.LogWarning("Manual release day processing failed: {Error}", result.Error);
        }
    }
}
