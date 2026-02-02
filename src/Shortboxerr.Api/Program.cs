using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
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

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shortboxerr API v1");
    c.RoutePrefix = "swagger";
});

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

app.Run();

// Make Program class accessible for testing
public partial class Program { }
