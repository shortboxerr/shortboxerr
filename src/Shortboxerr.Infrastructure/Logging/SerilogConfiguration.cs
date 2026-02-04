using Serilog;
using Serilog.Events;
using Serilog.Sinks.Async;
using Shortboxerr.Infrastructure.Logging;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Configuration helper for Serilog logging setup.
/// </summary>
public static class SerilogConfiguration
{
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
        // Default log directory
        logDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "shortboxerr",
            "logs");

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
