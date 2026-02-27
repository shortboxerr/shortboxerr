using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Logging;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for compressing rotated log files.
/// Scans the log directory for old log files and compresses them to .gz format.
/// </summary>
public class LogCompressionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogCompressionBackgroundService> _logger;

    private DateTime _lastRun = DateTime.MinValue;

    public LogCompressionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LogCompressionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Log compression background service started");

        // Initial delay to let the application start up
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Log compression service cancelled during startup delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunCompressionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in log compression background service");
            }

            // Check every 6 hours
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }

        _logger.LogInformation("Log compression background service stopped");
    }

    private async Task CheckAndRunCompressionAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        // Read settings using the same key pattern as SettingsEndpoints
        var compressOldLogsStr = await settingsService.GetAsync("Logging:CompressOldLogs", cancellationToken);
        var compressOldLogs = string.IsNullOrEmpty(compressOldLogsStr) || (bool.TryParse(compressOldLogsStr, out var compress) && compress);
        var compressLogsOlderThanDays = int.TryParse(await settingsService.GetAsync("Logging:CompressLogsOlderThanDays", cancellationToken), out var days) ? days : 1;

        var settings = new LogSettings
        {
            CompressOldLogs = compressOldLogs,
            CompressLogsOlderThanDays = compressLogsOlderThanDays
        };

        if (!settings.CompressOldLogs)
        {
            _logger.LogDebug("Log compression is disabled");
            return;
        }

        // Only run once per day
        if ((DateTime.UtcNow - _lastRun).TotalHours < 24)
        {
            return;
        }

        _logger.LogInformation("Running log compression");

        var result = await CompressLogsAsync(settings, cancellationToken);
        _lastRun = DateTime.UtcNow;

        if (result.FilesCompressed > 0)
        {
            _logger.LogInformation(
                "Log compression completed: compressed {Count} files, saved {Bytes} bytes",
                result.FilesCompressed, result.BytesSaved);
        }
        else
        {
            _logger.LogDebug("Log compression completed: no files to compress");
        }
    }

    private async Task<LogCompressionResult> CompressLogsAsync(LogSettings settings, CancellationToken cancellationToken)
    {
        var result = new LogCompressionResult();
        var logDirectory = SerilogConfiguration.GetLogDirectory();

        if (!Directory.Exists(logDirectory))
        {
            return result;
        }

        // Find rotated log files (e.g., shortboxerr20260217.log, shortboxerr.log.1, etc.)
        // Don't compress the current log file or already compressed files
        var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(logDirectory, "*.txt", SearchOption.TopDirectoryOnly))
            .Where(f => !IsCurrentLogFile(f) && !IsAlreadyCompressed(f))
            .Where(f => ShouldCompress(f, settings.CompressLogsOlderThanDays))
            .ToList();

        foreach (var logFile in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var originalSize = new FileInfo(logFile).Length;
                var compressedPath = logFile + ".gz";

                // Skip if compressed version already exists
                if (File.Exists(compressedPath))
                {
                    continue;
                }

                await CompressFileAsync(logFile, compressedPath, cancellationToken);

                var compressedSize = new FileInfo(compressedPath).Length;
                result.FilesCompressed++;
                result.BytesSaved += originalSize - compressedSize;

                // Delete the original file after successful compression
                File.Delete(logFile);

                _logger.LogDebug(
                    "Compressed log file {File}: {Original} -> {Compressed} bytes",
                    Path.GetFileName(logFile), originalSize, compressedSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compress log file {File}", logFile);
            }
        }

        return result;
    }

    private static bool IsCurrentLogFile(string path)
    {
        var fileName = Path.GetFileName(path);
        // The current log file is "shortboxerr.log" (without date suffix)
        return fileName.Equals("shortboxerr.log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyCompressed(string path)
    {
        return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldCompress(string path, int daysOld)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            var age = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
            return age.TotalDays >= daysOld;
        }
        catch
        {
            return false;
        }
    }

    private static async Task CompressFileAsync(string sourcePath, string destPath, CancellationToken cancellationToken)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destStream = File.Create(destPath);
        await using var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal);
        await sourceStream.CopyToAsync(gzipStream, cancellationToken);
    }

    /// <summary>
    /// Triggers immediate log compression (for testing or manual invocation).
    /// </summary>
    public async Task<LogCompressionResult> TriggerCompressionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual log compression triggered");

        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        // Read settings using the same key pattern as SettingsEndpoints
        var compressLogsOlderThanDays = int.TryParse(await settingsService.GetAsync("Logging:CompressLogsOlderThanDays", cancellationToken), out var days) ? days : 1;

        var settings = new LogSettings
        {
            CompressOldLogs = true, // Always compress when manually triggered
            CompressLogsOlderThanDays = compressLogsOlderThanDays
        };

        var result = await CompressLogsAsync(settings, cancellationToken);
        _lastRun = DateTime.UtcNow;

        _logger.LogInformation(
            "Manual log compression completed: compressed {Count} files, saved {Bytes} bytes",
            result.FilesCompressed, result.BytesSaved);

        return result;
    }
}

/// <summary>
/// Settings for logging configuration.
/// </summary>
public class LogSettings
{
    /// <summary>
    /// Whether to compress old rotated log files.
    /// </summary>
    public bool CompressOldLogs { get; set; } = true;

    /// <summary>
    /// Compress logs older than this many days. Default: 1 day.
    /// </summary>
    public int CompressLogsOlderThanDays { get; set; } = 1;

    /// <summary>
    /// Log level (Trace, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Maximum log file size in megabytes before rotation.
    /// </summary>
    public int MaxLogFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Number of rotated log files to keep.
    /// </summary>
    public int RetainedFileCount { get; set; } = 5;
}

/// <summary>
/// Result of a log compression operation.
/// </summary>
public class LogCompressionResult
{
    public int FilesCompressed { get; set; }
    public long BytesSaved { get; set; }
}
