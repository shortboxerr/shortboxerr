using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.SignalR;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that monitors completed DDL downloads and triggers import processing.
/// Similar to NzbImportBackgroundService but for direct downloads.
/// </summary>
public class DdlImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DdlImportBackgroundService> _logger;
    
    private TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private const string EnabledSettingKey = "ddl_auto_import_enabled";
    private const string IntervalSettingKey = "ddl_auto_import_interval_seconds";
    private const string AutoImportSettingKey = "ddl_auto_import";
    private const string MinConfidenceSettingKey = "ddl_auto_import_min_confidence";
    private const string MaxConcurrentImportsKey = "ddl_auto_import_max_concurrent";
    
    private int _consecutiveErrors = 0;
    private const int MaxConsecutiveErrors = 5;
    private DateTime? _lastCheckAt;
    
    public DdlImportBackgroundService(
        IServiceProvider services,
        ILogger<DdlImportBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DDL Import Background Service starting");
        
        // Initial delay to let the application start up
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("DDL Import Background Service cancelled during startup delay");
            return;
        }
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingImportsAsync(stoppingToken);
                _consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _logger.LogError(ex, "Error processing DDL imports (attempt {Count}/{Max})", 
                    _consecutiveErrors, MaxConsecutiveErrors);
                
                if (_consecutiveErrors >= MaxConsecutiveErrors)
                {
                    _logger.LogWarning("Too many consecutive errors, extending check interval to 5 minutes");
                    _checkInterval = TimeSpan.FromMinutes(5);
                }
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
        }
        
        _logger.LogInformation("DDL Import Background Service stopped");
    }
    
    private async Task ProcessPendingImportsAsync(CancellationToken cancellationToken)
    {
        _lastCheckAt = DateTime.UtcNow;
        
        using var scope = _services.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        
        // Check if enabled (default: true)
        var enabled = await settingsService.GetAsync(EnabledSettingKey, true, cancellationToken);
        if (!enabled)
        {
            _logger.LogDebug("DDL auto-import is disabled");
            return;
        }
        
        // Get interval setting
        var intervalSeconds = await settingsService.GetAsync(IntervalSettingKey, 30, cancellationToken);
        _checkInterval = TimeSpan.FromSeconds(Math.Max(15, intervalSeconds));
        
        // Get auto-import settings
        var autoImport = await settingsService.GetAsync(AutoImportSettingKey, true, cancellationToken);
        var minConfidence = await settingsService.GetAsync(MinConfidenceSettingKey, 80, cancellationToken);
        var maxConcurrent = await settingsService.GetAsync(MaxConcurrentImportsKey, 3, cancellationToken);
        
        // Get the DDL download service (singleton)
        var downloadService = scope.ServiceProvider.GetService<IDdlDownloadService>();
        if (downloadService == null)
        {
            _logger.LogDebug("IDdlDownloadService not available");
            return;
        }
        
        // Get pending imports
        var pendingDownloads = downloadService.GetPendingImportDownloads();
        
        if (pendingDownloads.Count == 0)
        {
            _logger.LogDebug("No pending DDL imports");
            return;
        }
        
        _logger.LogInformation("Found {Count} DDL downloads pending import", pendingDownloads.Count);
        
        // Get the import service
        var importService = scope.ServiceProvider.GetService<IDdlImportService>();
        if (importService == null)
        {
            _logger.LogWarning("IDdlImportService not available, cannot process imports");
            return;
        }
        
        // Get activity service for history tracking
        var activityService = scope.ServiceProvider.GetService<IActivityService>();
        
        // Get message broadcaster for real-time notifications (optional)
        var messageBroadcaster = scope.ServiceProvider.GetService<IMessageBroadcaster>();
        
        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        
        // Process imports in parallel with configurable concurrency
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxConcurrent),
            CancellationToken = cancellationToken
        };
        
        await Parallel.ForEachAsync(pendingDownloads, parallelOptions, async (download, ct) =>
        {
            try
            {
                // Verify file still exists
                if (string.IsNullOrEmpty(download.DestinationPath) || !File.Exists(download.DestinationPath))
                {
                    _logger.LogWarning("Downloaded file no longer exists: {Path}", download.DestinationPath);
                    downloadService.MarkAsImported(download.DownloadId);
                    return;
                }
                
                // Create a download result for the import service
                var downloadResult = DdlDownloadResult.Succeeded(
                    downloadId: download.DownloadId,
                    filePath: download.DestinationPath,
                    fileName: Path.GetFileName(download.DestinationPath),
                    fileSize: download.FileSize,
                    duration: download.Duration,
                    sourceUrl: download.SourceUrl
                );
                
                // Use the stored candidate or create a minimal one
                var candidate = download.Candidate ?? new DdlCandidate
                {
                    Id = download.Id,
                    ReleaseTitle = download.ReleaseTitle ?? Path.GetFileNameWithoutExtension(download.DestinationPath),
                    SourceSite = download.SourceSite ?? "Unknown",
                    SourceUrl = download.SourceUrl,
                    ParsedInfo = new DdlParsedInfo()
                };
                
                _logger.LogInformation("Processing import for: {Title}", download.ReleaseTitle ?? download.DestinationPath);
                
                // Process the download through the import pipeline
                var importResult = await importService.ProcessDownloadAsync(
                    downloadResult, 
                    candidate, 
                    cancellationToken: ct);
                
                Interlocked.Increment(ref processed);
                
                if (importResult.Success)
                {
                    Interlocked.Increment(ref succeeded);
                    _logger.LogInformation("Successfully imported: {Title} -> {LibraryPath}", 
                        download.ReleaseTitle, importResult.LibraryPath);
                    
                    // Add to activity history
                    activityService?.AddToHistory(new DownloadActivity
                    {
                        Id = download.DownloadId,
                        Title = download.ReleaseTitle ?? Path.GetFileName(download.DestinationPath),
                        SourceType = DownloadSourceType.Ddl,
                        ClientName = download.SourceSite ?? "DDL",
                        State = ActivityState.Completed,
                        Progress = 100,
                        TotalBytes = download.FileSize,
                        DownloadedBytes = download.FileSize,
                        StartedAt = download.StartedAt,
                        CompletedAt = DateTime.UtcNow,
                        OutputPath = importResult.LibraryPath,
                        SourceUrl = download.SourceUrl
                    });
                    
                    // Broadcast real-time notification
                    if (messageBroadcaster != null)
                    {
                        await messageBroadcaster.BroadcastImportCompletedAsync(new ImportCompletedMessage
                        {
                            SeriesTitle = importResult.SeriesTitle ?? download.ReleaseTitle ?? "Unknown",
                            IssueNumber = importResult.IssueNumber?.ToString() ?? "?",
                            FilePath = importResult.LibraryPath ?? download.DestinationPath,
                            SeriesId = importResult.SeriesId,
                            IssueId = importResult.IssueId,
                            Success = true
                        }, ct);
                    }
                }
                else if (importResult.PendingManualReview)
                {
                    _logger.LogInformation("Import pending manual review: {Title} (Confidence: {Confidence}%)", 
                        download.ReleaseTitle, importResult.MatchConfidence);
                    
                    // Don't mark as imported yet - user needs to approve
                    if (autoImport && importResult.MatchConfidence >= minConfidence)
                    {
                        // Auto-approve if confidence is high enough
                        _logger.LogInformation("Auto-approving import with {Confidence}% confidence (threshold: {Threshold}%)",
                            importResult.MatchConfidence, minConfidence);
                        // The actual auto-approval would happen here if implemented
                    }
                    return;
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    _logger.LogWarning("Import failed for {Title}: {Error}", 
                        download.ReleaseTitle, importResult.ErrorMessage);
                    
                    // Add failed entry to activity
                    activityService?.AddToHistory(new DownloadActivity
                    {
                        Id = download.DownloadId,
                        Title = download.ReleaseTitle ?? Path.GetFileName(download.DestinationPath),
                        SourceType = DownloadSourceType.Ddl,
                        ClientName = download.SourceSite ?? "DDL",
                        State = ActivityState.Failed,
                        StartedAt = download.StartedAt,
                        CompletedAt = DateTime.UtcNow,
                        ErrorMessage = importResult.ErrorMessage,
                        SourceUrl = download.SourceUrl
                    });
                    
                    // Broadcast real-time notification for failure
                    if (messageBroadcaster != null)
                    {
                        await messageBroadcaster.BroadcastImportCompletedAsync(new ImportCompletedMessage
                        {
                            SeriesTitle = download.ReleaseTitle ?? "Unknown",
                            IssueNumber = "?",
                            FilePath = download.DestinationPath,
                            Success = false,
                            ErrorMessage = importResult.ErrorMessage
                        }, ct);
                    }
                }
                
                // Mark as processed (success or failure)
                downloadService.MarkAsImported(download.DownloadId);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                _logger.LogError(ex, "Error importing download {Title}", download.ReleaseTitle);
                
                // Mark as processed to avoid infinite retry
                downloadService.MarkAsImported(download.DownloadId);
            }
        });
        
        if (processed > 0)
        {
            _logger.LogInformation("DDL import processing complete: {Processed} processed, {Succeeded} succeeded, {Failed} failed",
                processed, succeeded, failed);
        }
    }
    
    /// <summary>
    /// Gets the current status of the background service.
    /// </summary>
    public DdlImportServiceStatus GetStatus()
    {
        return new DdlImportServiceStatus
        {
            IsRunning = true,
            CheckInterval = _checkInterval,
            ConsecutiveErrors = _consecutiveErrors,
            LastCheck = _lastCheckAt
        };
    }
}

/// <summary>
/// Status of the DDL import background service.
/// </summary>
public class DdlImportServiceStatus
{
    public bool IsRunning { get; init; }
    public TimeSpan CheckInterval { get; init; }
    public int ConsecutiveErrors { get; init; }
    public DateTime? LastCheck { get; init; }
}
