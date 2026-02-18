using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that automatically searches for wanted issues.
/// Runs periodically based on AutoSearchIntervalHours setting.
/// </summary>
public class AutoSearchBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoSearchBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check settings every 15 mins

    public AutoSearchBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoSearchBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-search background service starting. Check interval: {Interval}", _checkInterval);

        // Initial delay to allow application to fully start
        _logger.LogDebug("Waiting 5 minutes before first auto-search check");
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        DateTime? lastSearchRun = null;
        var consecutiveErrors = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunAutoSearchAsync(lastSearchRun, stoppingToken);
                
                // Update last run time if search was executed
                using (var scope = _serviceProvider.CreateScope())
                {
                    var autoSearchService = scope.ServiceProvider.GetRequiredService<IAutoSearchService>();
                    var status = await autoSearchService.GetStatusAsync(stoppingToken);
                    if (status.LastRunAt.HasValue && (lastSearchRun == null || status.LastRunAt > lastSearchRun))
                    {
                        lastSearchRun = status.LastRunAt;
                    }
                }
                
                consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Auto-search cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error in auto-search background service (attempt {Attempt})", consecutiveErrors);
                
                if (consecutiveErrors >= 3)
                {
                    _logger.LogWarning("Multiple consecutive errors ({Count}). Will continue but may indicate a persistent issue.", consecutiveErrors);
                }
            }

            _logger.LogDebug("Next auto-search check in {Interval}", _checkInterval);
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Auto-search background service stopping");
    }

    private async Task CheckAndRunAutoSearchAsync(DateTime? lastSearchRun, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var autoSearchService = scope.ServiceProvider.GetRequiredService<IAutoSearchService>();

        // Get search settings
        var settings = await settingsService.GetAsync<SearchSettings>(SearchSettings.SettingsKey, new(), cancellationToken);

        // Check if auto-search is enabled
        if (!settings.AutoSearchEnabled)
        {
            _logger.LogDebug("Auto-search is disabled");
            return;
        }

        // Check if enough time has passed since last run
        if (lastSearchRun.HasValue)
        {
            var timeSinceLastRun = DateTime.UtcNow - lastSearchRun.Value;
            var requiredInterval = TimeSpan.FromHours(settings.AutoSearchIntervalHours);
            
            if (timeSinceLastRun < requiredInterval)
            {
                var nextRun = requiredInterval - timeSinceLastRun;
                _logger.LogDebug("Auto-search not due yet. Next run in {TimeRemaining}", nextRun);
                return;
            }
        }

        _logger.LogInformation("Starting auto-search run");

        // Get searchable issues count first
        var searchableIssues = await autoSearchService.GetSearchableIssuesAsync(cancellationToken: cancellationToken);
        
        if (searchableIssues.Count == 0)
        {
            _logger.LogDebug("No searchable issues found");
            return;
        }

        _logger.LogInformation("Found {Count} issues to search", searchableIssues.Count);

        // Run auto-search with a reasonable batch limit
        var maxIssuesPerRun = 50; // Configurable limit per run
        var result = await autoSearchService.SearchAllWantedAsync(maxIssuesPerRun, cancellationToken);

        if (result.TotalSearched > 0)
        {
            _logger.LogInformation(
                "Auto-search completed: {Searched} searched, {Found} found, {NotFound} not found, {Failed} failed",
                result.TotalSearched, result.SuccessCount, result.NotFoundCount, result.FailedCount);

            // Send notification if any issues were found
            if (result.SuccessCount > 0)
            {
                try
                {
                    await SendSearchResultNotificationAsync(scope, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send auto-search notification");
                }
            }
        }
    }

    private async Task SendSearchResultNotificationAsync(
        IServiceScope scope, 
        AutoSearchBatchResult result, 
        CancellationToken cancellationToken)
    {
        var notificationService = scope.ServiceProvider.GetService<INotificationService>();
        if (notificationService == null)
        {
            return;
        }

        var foundIssues = result.Results
            .Where(r => r.Success && r.CandidatesFound > 0)
            .Take(5) // Limit to first 5 for notification
            .ToList();

        if (foundIssues.Count == 0)
        {
            return;
        }

        var message = foundIssues.Count == 1
            ? $"Found: {foundIssues[0].SeriesTitle} #{foundIssues[0].IssueNumber}"
            : $"Found {result.SuccessCount} issues including {foundIssues[0].SeriesTitle} #{foundIssues[0].IssueNumber}";

        await notificationService.CreateAsync(
            new CreateNotificationRequest
            {
                Type = NotificationType.Info,
                Title = "Auto-Search Results",
                Message = message
            },
            cancellationToken);
    }

    /// <summary>
    /// Trigger an immediate auto-search run (called from API endpoint).
    /// </summary>
    public async Task TriggerSearchAsync(int? maxIssues = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual auto-search triggered");

        using var scope = _serviceProvider.CreateScope();
        var autoSearchService = scope.ServiceProvider.GetRequiredService<IAutoSearchService>();

        await autoSearchService.SearchAllWantedAsync(maxIssues, cancellationToken);
    }
}
