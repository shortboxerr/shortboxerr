using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that monitors download clients for completed NZB downloads
/// and triggers import processing.
/// </summary>
public class NzbImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NzbImportBackgroundService> _logger;
    
    // Default check interval (can be overridden via settings)
    private TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private TimeSpan _noClientCheckInterval = TimeSpan.FromMinutes(5);
    
    // Settings keys
    private const string EnabledSettingKey = "nzb_import_enabled";
    private const string IntervalSettingKey = "nzb_import_interval_seconds";
    private const string AutoImportSettingKey = "nzb_auto_import";
    private const string MinConfidenceSettingKey = "nzb_auto_import_min_confidence";
    private const string CategoriesSettingKey = "nzb_import_categories";
    
    private int _consecutiveErrors = 0;
    private const int MaxConsecutiveErrors = 5;
    private bool _noClientWarningLogged;
    
    public NzbImportBackgroundService(
        IServiceProvider services,
        ILogger<NzbImportBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NZB Import Background Service starting");
        
        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCompletedDownloadsAsync(stoppingToken);
                _consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _logger.LogError(ex, "Error processing completed downloads (attempt {Count}/{Max})", 
                    _consecutiveErrors, MaxConsecutiveErrors);
                
                if (_consecutiveErrors >= MaxConsecutiveErrors)
                {
                    _logger.LogWarning("Too many consecutive errors, extending check interval");
                    _checkInterval = TimeSpan.FromMinutes(5);
                }
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
        }
        
        _logger.LogInformation("NZB Import Background Service stopped");
    }
    
    private async Task ProcessCompletedDownloadsAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        
        // Check if enabled
        var enabled = await settingsService.GetAsync(EnabledSettingKey, true, cancellationToken);
        if (!enabled)
        {
            _logger.LogDebug("NZB import processing is disabled");
            return;
        }
        
        // Check if any download client is configured
        var downloadClients = scope.ServiceProvider.GetServices<INzbDownloadClient>();
        var configuredClients = downloadClients.Where(c => c.IsConfigured).ToList();
        
        if (configuredClients.Count == 0)
        {
            if (!_noClientWarningLogged)
            {
                _logger.LogInformation("No download clients configured, skipping import check (will check again in {Interval} minutes)", 
                    _noClientCheckInterval.TotalMinutes);
                _noClientWarningLogged = true;
            }
            else
            {
                _logger.LogDebug("No download clients configured, skipping import check");
            }
            
            _checkInterval = _noClientCheckInterval;
            return;
        }
        
        // Reset the warning flag if we have clients now
        if (_noClientWarningLogged)
        {
            _logger.LogInformation("Download client now configured, resuming normal import checks");
            _noClientWarningLogged = false;
        }
        
        // Get interval setting
        var intervalSeconds = await settingsService.GetAsync(IntervalSettingKey, 60, cancellationToken);
        _checkInterval = TimeSpan.FromSeconds(Math.Max(30, intervalSeconds));
        
        // Get import options from settings
        var options = new NzbImportOptions
        {
            AutoImport = await settingsService.GetAsync(AutoImportSettingKey, true, cancellationToken),
            MinAutoImportConfidence = await settingsService.GetAsync(MinConfidenceSettingKey, 80, cancellationToken),
            Categories = await settingsService.GetAsync(CategoriesSettingKey, new List<string>(), cancellationToken) ?? new()
        };
        
        // Get the import service
        var importService = scope.ServiceProvider.GetService<INzbImportService>();
        if (importService == null)
        {
            _logger.LogWarning("INzbImportService not registered, skipping");
            return;
        }
        
        // Process completed downloads
        var results = await importService.ProcessAllCompletedAsync(options, cancellationToken);
        
        if (results.Count > 0)
        {
            var successful = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);
            
            _logger.LogInformation("Processed {Total} completed downloads: {Success} successful, {Failed} failed",
                results.Count, successful, failed);
            
            foreach (var result in results.Where(r => !r.Success))
            {
                _logger.LogWarning("Failed to process download {Name}: {Error}", 
                    result.DownloadName, result.ErrorMessage);
            }
        }
    }
    
    /// <summary>
    /// Gets the current status of the background service.
    /// </summary>
    public NzbImportServiceStatus GetStatus()
    {
        return new NzbImportServiceStatus
        {
            IsRunning = true,
            CheckInterval = _checkInterval,
            ConsecutiveErrors = _consecutiveErrors,
            LastCheck = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Status of the NZB import background service.
/// </summary>
public class NzbImportServiceStatus
{
    public bool IsRunning { get; init; }
    public TimeSpan CheckInterval { get; init; }
    public int ConsecutiveErrors { get; init; }
    public DateTime LastCheck { get; init; }
}
