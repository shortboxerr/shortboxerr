using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Async;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Configuration helper for Serilog logging setup.
/// Container-first design: defaults to /config/logs when SHORTBOXERR_CONFIG is set.
/// Falls back to LocalApplicationData for non-container development.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Default output template optimized for human readability.
    /// Uses shortened source context and fixed-width level indicators.
    /// </summary>
    public const string DefaultOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ShortSourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Default output template with correlation ID for request tracing.
    /// Includes correlation ID after timestamp for request-scoped tracing.
    /// </summary>
    public const string CorrelationOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{CorrelationId}] [{ShortSourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Compact output template for space-constrained environments.
    /// </summary>
    public const string CompactOutputTemplate =
        "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}";

    /// <summary>
    /// Verbose output template for debugging with full context.
    /// Includes correlation ID, machine name, and all properties.
    /// </summary>
    public const string VerboseOutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] [{ShortSourceContext}] [{MachineName}] {Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// JSON output template for structured logging/log aggregation.
    /// Includes correlation ID for distributed tracing.
    /// </summary>
    public const string JsonOutputTemplate =
        "{{ \"timestamp\": \"{Timestamp:o}\", \"level\": \"{Level}\", \"correlationId\": \"{CorrelationId}\", \"source\": \"{SourceContext}\", \"message\": {Message:lj}, \"properties\": {Properties:j} }}{NewLine}";
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
    /// Gets the output template from environment variable or returns the default.
    /// Set SHORTBOXERR_LOG_TEMPLATE to customize, or use preset names:
    /// - "default" or empty: DefaultOutputTemplate
    /// - "correlation": CorrelationOutputTemplate (includes correlation ID)
    /// - "compact": CompactOutputTemplate
    /// - "verbose": VerboseOutputTemplate (includes correlation ID)
    /// - "json": JsonOutputTemplate (includes correlation ID)
    /// - Any other value: treated as a custom template string
    /// </summary>
    public static string GetOutputTemplate()
    {
        var template = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE");

        if (string.IsNullOrEmpty(template))
            return DefaultOutputTemplate;

        return template.ToLowerInvariant() switch
        {
            "default" => DefaultOutputTemplate,
            "correlation" => CorrelationOutputTemplate,
            "compact" => CompactOutputTemplate,
            "verbose" => VerboseOutputTemplate,
            "json" => JsonOutputTemplate,
            _ => template // Custom template string
        };
    }

    /// <summary>
    /// Configures Serilog with file and console sinks, including sensitive data protection.
    /// </summary>
    /// <param name="logDirectory">Log directory path (defaults to container-first path)</param>
    /// <param name="minimumLevel">Minimum log level (default: Information)</param>
    /// <param name="consoleLevel">Console-specific log level (defaults to minimumLevel)</param>
    /// <param name="maxFileSizeBytes">Max file size before rotation (default: 10MB)</param>
    /// <param name="retainedFileCount">Number of rotated files to keep (default: 5)</param>
    /// <param name="outputTemplate">Output template (defaults to environment variable or DefaultOutputTemplate)</param>
    /// <param name="httpContextAccessor">HTTP context accessor for correlation ID enrichment (optional)</param>
    public static LoggerConfiguration CreateLoggerConfiguration(
        string? logDirectory = null,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        LogEventLevel? consoleLevel = null,
        long maxFileSizeBytes = 10 * 1024 * 1024, // 10MB default
        int retainedFileCount = 5,
        string? outputTemplate = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        // Use container-first default
        logDirectory ??= GetLogDirectory();
        outputTemplate ??= GetOutputTemplate();

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
            .Enrich.With(new ShortSourceContextEnricher())
            .Enrich.With(new CorrelationIdEnricher(httpContextAccessor))
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy());

        // Console sink (with optional different level)
        var consoleLevelToUse = consoleLevel ?? minimumLevel;
        config.WriteTo.Console(
            restrictedToMinimumLevel: consoleLevelToUse,
            outputTemplate: outputTemplate,
            theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code);

        // File sink with rotation (async for performance)
        config.WriteTo.Async(a => a.File(
            path: logFilePath,
            restrictedToMinimumLevel: minimumLevel,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retainedFileCount,
            fileSizeLimitBytes: maxFileSizeBytes,
            rollOnFileSizeLimit: true,
            outputTemplate: outputTemplate,
            shared: false));

        return config;
    }
}
