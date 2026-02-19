using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using Shortboxerr.Api.Endpoints;
using Shortboxerr.Api.Middleware;
using Shortboxerr.Infrastructure;
using Shortboxerr.Infrastructure.Logging;
using Shortboxerr.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

// Configure Serilog early (before WebApplication builder)
// Container-first: uses /config/logs when SHORTBOXERR_CONFIG is set
var configDirectory = SerilogConfiguration.GetConfigDirectory();
var logDirectory = SerilogConfiguration.GetLogDirectory();
var dataDirectory = SerilogConfiguration.GetDataDirectory();

var minimumLevel = Enum.TryParse<LogEventLevel>(
    Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_LEVEL") ?? "Information",
    out var level) ? level : LogEventLevel.Information;

// Check for debug mode
var isDebug = Environment.GetEnvironmentVariable("SHORTBOXERR_DEBUG") == "true"
    || args.Contains("--debug") || args.Contains("-d");

if (isDebug)
{
    minimumLevel = LogEventLevel.Debug;
}

Log.Logger = SerilogConfiguration.CreateLoggerConfiguration(
    logDirectory: logDirectory,
    minimumLevel: minimumLevel,
    consoleLevel: isDebug ? LogEventLevel.Debug : minimumLevel)
    .CreateLogger();

// Log startup banner immediately after logger configuration
Log.Information("=== Shortboxerr Starting ===");
Log.Information("Version: 0.1.0");
Log.Information("Runtime: {Runtime}", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
Log.Information("OS: {OS}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
Log.Information("Architecture: {Arch}", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
Log.Information("Config directory: {ConfigDirectory}", configDirectory);
Log.Information("Data directory: {DataDirectory}", dataDirectory);
Log.Information("Log directory: {LogDirectory}", logDirectory);
Log.Information("Debug mode: {DebugMode}", isDebug);
Log.Information("Log level: {LogLevel}", minimumLevel);

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Log configuration sources
Log.Debug("Configuration sources loaded: {Sources}", 
    string.Join(", ", builder.Configuration.AsEnumerable().Select(c => c.Key).Take(10)));

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000", 
                "http://localhost:5173",
                "http://localhost:8585",
                "http://172.16.11.63:8585",
                "http://172.16.11.63:5000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add services to the container
// Database path: container-first uses /config/shortboxerr.db
var dbPath = Path.Combine(dataDirectory, "shortboxerr.db");
var connectionString = Environment.GetEnvironmentVariable("SHORTBOXERR_DB")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={dbPath}";
builder.Services.AddInfrastructure(connectionString, enableDebugMode: isDebug);

// Configure JSON serialization to use string enums and include null values
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    // Include null values in JSON output (important for nullable settings like EnableSeriesAnnualIntegration)
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
});

// Add HttpContextAccessor for correlation ID enrichment
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Shortboxerr API",
        Version = "v1",
        Description = "Arr-like comic book management with Mylar3 behavioral parity"
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShortboxerrDbContext>("database");

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
    
    Log.Information("Database connection string configured: {DatabasePath}", 
        connectionString.Contains("Source=") ? connectionString.Split("Source=").Last().Split(";").First() : "configured");
    
    var pendingMigrations = db.Database.GetPendingMigrations().ToList();
    if (pendingMigrations.Count > 0)
    {
        Log.Information("Applying {Count} pending database migrations: {Migrations}", 
            pendingMigrations.Count, string.Join(", ", pendingMigrations));
    }
    else
    {
        Log.Debug("No pending database migrations");
    }
    
    db.Database.Migrate();
    
    var appliedMigrations = db.Database.GetAppliedMigrations().ToList();
    Log.Information("Database ready with {Count} migrations applied", appliedMigrations.Count);
}

// Register application lifetime logging
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
    Log.Information("Application started. Now listening for requests."));
lifetime.ApplicationStopping.Register(() =>
    Log.Information("Application stopping. Graceful shutdown initiated."));
lifetime.ApplicationStopped.Register(() =>
    Log.Information("=== Shortboxerr Stopped ==="));

// Configure the HTTP request pipeline
app.UseCors(); // Enable CORS for development

// Add correlation ID middleware (must be before request logging)
app.UseCorrelationId();

// Add Serilog request logging with sensitive data masking
app.UseSerilogRequestLogging(options =>
{
    // Customize the message template
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    
    // Emit debug level logs for successful requests, information for others
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // Don't log health checks at information level (too noisy)
        if (httpContext.Request.Path.StartsWithSegments("/health") || 
            httpContext.Request.Path.StartsWithSegments("/ping"))
        {
            return LogEventLevel.Debug;
        }
        
        // Errors and slow requests at Warning or higher
        if (ex != null || httpContext.Response.StatusCode >= 500)
            return LogEventLevel.Error;
        if (httpContext.Response.StatusCode >= 400 || elapsed > 3000)
            return LogEventLevel.Warning;
        
        return LogEventLevel.Information;
    };
    
    // Enrich log events with additional properties
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        
        // Mask sensitive query parameters
        var query = httpContext.Request.QueryString.Value ?? "";
        var maskedQuery = MaskSensitiveQueryParams(query);
        if (!string.IsNullOrEmpty(maskedQuery))
        {
            diagnosticContext.Set("QueryString", maskedQuery);
        }
    };
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shortboxerr API v1");
    c.RoutePrefix = "swagger";
});

// Serve static files from wwwroot (React UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

// Ping endpoint (simple liveness check)
app.MapGet("/ping", () => Results.Ok("pong"))
    .WithName("Ping")
    .WithOpenApi();

// Domain endpoints
app.MapSeriesEndpoints();
app.MapSeriesMetadataEndpoints();
app.MapIssueMetadataEndpoints();
app.MapCoverEndpoints();
app.MapEditionEndpoints();
app.MapEditionMetadataEndpoints();
app.MapAutoMatchEndpoints();
app.MapMetadataRefreshEndpoints();
app.MapMylar3ImportEndpoints();
app.MapManualImportEndpoints();
app.MapDecisionEngineEndpoints();
app.MapProviderEndpoints();
app.MapDdlImportEndpoints();
app.MapDdlSiteEndpoints();
app.MapSiteHealthEndpoints();
app.MapSearchSettingsEndpoints();
app.MapPullListEndpoints();
app.MapWantedEndpoints();
app.MapNotificationEndpoints();
app.MapActivityEndpoints();
app.MapCacheEndpoints();
app.MapSettingsEndpoints();
app.MapComicVineEndpoints();
app.MapSystemEndpoints();
app.MapNzbEndpoints();
app.MapAutoSearchEndpoints();
app.MapIndexerHealthEndpoints();
app.MapDownloadClientHealthEndpoints();
app.MapHostBlacklistEndpoints();
app.MapVariantCoverEndpoints();

// SPA fallback - serve index.html for client-side routes
app.MapFallbackToFile("index.html");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for testing
public partial class Program 
{
    /// <summary>
    /// Masks sensitive query parameters (apikey, token, password, secret, etc.)
    /// </summary>
    internal static string MaskSensitiveQueryParams(string queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return queryString;
        
        var sensitiveParams = new[] { "apikey", "api_key", "token", "password", "secret", "key", "credential", "authorization" };
        var result = queryString;
        
        foreach (var param in sensitiveParams)
        {
            // Match param=value patterns (case-insensitive)
            var pattern = $@"({param})=([^&]+)";
            result = System.Text.RegularExpressions.Regex.Replace(
                result, 
                pattern, 
                "$1=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return result;
    }
}
