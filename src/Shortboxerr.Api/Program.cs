using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shortboxerr.Api.Endpoints;
using Shortboxerr.Infrastructure;
using Shortboxerr.Infrastructure.Persistence;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddInfrastructure(connectionString);

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
    db.Database.Migrate();
}

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

// API info endpoint
app.MapGet("/api/v1/system/status", () => Results.Ok(new
{
    appName = "Shortboxerr",
    version = "0.1.0",
    startTime = DateTime.UtcNow
}))
    .WithName("SystemStatus")
    .WithOpenApi();

// Domain endpoints
app.MapSeriesEndpoints();
app.MapEditionEndpoints();
app.MapManualImportEndpoints();
app.MapDecisionEngineEndpoints();
app.MapProviderEndpoints();
app.MapDdlImportEndpoints();
app.MapMylar3ImportEndpoints();
app.MapSettingsEndpoints();
app.MapComicVineEndpoints();

// SPA fallback - serve index.html for client-side routes
app.MapFallbackToFile("index.html");

app.Run();

// Make Program class accessible for testing
public partial class Program { }
