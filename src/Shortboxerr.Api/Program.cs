using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using Shortboxerr.Api.Endpoints;
using Shortboxerr.Infrastructure;
using Shortboxerr.Infrastructure.Logging;
using Shortboxerr.Infrastructure.Persistence;
using System.Text.Json;

// Configure Serilog early (before WebApplication builder)
var logDirectory = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "shortboxerr", "logs");

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
Log.Information("Debug mode: {DebugMode}", isDebug);
Log.Information("Log directory: {LogDirectory}", logDirectory);
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
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add services to the container
var connectionString = Environment.GetEnvironmentVariable("SHORTBOXERR_DB")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=shortboxerr.db";
builder.Services.AddInfrastructure(connectionString, enableDebugMode: isDebug);

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
app.MapPullListEndpoints();
app.MapNotificationEndpoints();
app.MapCacheEndpoints();
app.MapSettingsEndpoints();
app.MapComicVineEndpoints();
app.MapSystemEndpoints();

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
public partial class Program { }
