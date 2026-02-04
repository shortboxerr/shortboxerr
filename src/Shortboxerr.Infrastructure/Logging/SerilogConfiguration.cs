using Serilog;
using Serilog.Events;
using Serilog.Sinks.Async;
using Shortboxerr.Infrastructure.Logging;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Configuration helper for Serilog logging setup.
/// Container-first design: defaults to /config/logs when SHORTBOXERR_CONFIG is set.
/// Falls back to LocalApplicationData for non-container development.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Gets the config directory following *arr stack conventions.
    /// Container mode: /config (set via SHORTBOXERR_CONFIG env var)
    /// Non-container: ~/.local/share/shortboxerr (Linux) or equivalent
    /// </summary>
    public static string GetConfigDirectory()
    {
        // Check for container-style config path first (like Sonarr/Radarr/Mylar3)
        var configDir = Environment.GetEnvironmentVariable("SHORTBOXERR_CONFIG");
        if (!string.IsNullOrEmpty(configDir))
        {
            return configDir;
        }

        // Fallback for non-container development
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "shortboxerr");
    }

    /// <summary>
    /// Gets the logs directory.
    /// Container mode: /config/logs
    /// Non-container: ~/.local/share/shortboxerr/logs
    /// Can be overridden with SHORTBOXERR_LOG_DIR
    /// </summary>
    public static string GetLogDirectory()
    {
        // Allow explicit override
        var logDir = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_DIR");
        if (!string.IsNullOrEmpty(logDir))
        {
            return logDir;
        }

        // Default: {config}/logs
        return Path.Combine(GetConfigDirectory(), "logs");
    }

    /// <summary>
    /// Gets the data directory (for database, cache, etc.).
    /// Container mode: /config
    /// Non-container: ~/.local/share/shortboxerr
    /// </summary>
    public static string GetDataDirectory()
    {
        var dataDir = Environment.GetEnvironmentVariable("SHORTBOXERR_DATA");
        if (!string.IsNullOrEmpty(dataDir))
        {
            return dataDir;
        }

        return GetConfigDirectory();
    }

    /// <summary>
    /// Configures Serilog with file and console sinks, including sensitive data protection.
    /// </summary>
    public static LoggerConfiguration CreateLoggerConfiguration(
        string? logDirectory = null,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        LogEventLevel? consoleLevel = null,
        long maxFileSizeBytes = 10 * 1024 * 1024, // 10MB default
        int retainedFileCount = 5)
    {
        // Use container-first default
        logDirectory ??= GetLogDirectory();

        // Ensure log directory exists
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "shortboxerr.log");

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy());

        // Console sink (with optional different level)
        var consoleLevelToUse = consoleLevel ?? minimumLevel;
        config.WriteTo.Console(
            restrictedToMinimumLevel: consoleLevelToUse,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        // File sink with rotation (async for performance)
        config.WriteTo.Async(a => a.File(
            path: logFilePath,
            restrictedToMinimumLevel: minimumLevel,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retainedFileCount,
            fileSizeLimitBytes: maxFileSizeBytes,
            rollOnFileSizeLimit: true,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
            shared: false));

        return config;
    }
}
