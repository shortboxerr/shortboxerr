using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Middleware;

/// <summary>
/// Middleware that enforces API key authentication on all API endpoints.
///
/// Behavior:
/// - Skips authentication for exempt paths: /health, /ping, /swagger, /signalr, static files, and the setup/bootstrap endpoints
/// - Accepts the API key via:
///   1. X-Api-Key header
///   2. apikey query parameter (Newznab/indexer compat)
/// - If API key auth is disabled in settings, all requests pass through
/// - Returns 401 Unauthorized with a JSON body on failure
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    // Paths that do not require authentication
    private static readonly string[] ExemptPrefixes =
    [
        "/health",
        "/ping",
        "/swagger",
        "/signalr",
        "/api/v1/setup",   // Bootstrap/first-run setup must be accessible before a key exists
    ];

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISettingsService settingsService)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for exempt paths and static files (no /api prefix = UI assets)
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        // Check if API key auth is enabled
        var keyInfo = await settingsService.GetApiKeyAsync(includeFull: false, context.RequestAborted);
        if (!keyInfo.IsEnabled)
        {
            await _next(context);
            return;
        }

        // Extract key from header or query string
        var providedKey = ExtractApiKey(context);

        if (string.IsNullOrEmpty(providedKey))
        {
            _logger.LogWarning("API request to {Path} rejected: no API key provided", path);
            await WriteUnauthorized(context, "API key required. Provide it via X-Api-Key header or ?apikey= query parameter.");
            return;
        }

        var isValid = await settingsService.ValidateApiKeyAsync(providedKey, context.RequestAborted);
        if (!isValid)
        {
            _logger.LogWarning("API request to {Path} rejected: invalid API key", path);
            await WriteUnauthorized(context, "Invalid API key.");
            return;
        }

        await _next(context);
    }

    private static bool IsExemptPath(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? ExtractApiKey(HttpContext context)
    {
        // 1. X-Api-Key header (preferred)
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var headerKey)
            && !string.IsNullOrWhiteSpace(headerKey))
        {
            return headerKey.ToString().Trim();
        }

        // 2. apikey query parameter (Newznab/indexer compat)
        if (context.Request.Query.TryGetValue("apikey", out var queryKey)
            && !string.IsNullOrWhiteSpace(queryKey))
        {
            return queryKey.ToString().Trim();
        }

        return null;
    }

    private static async Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = message }));
    }
}

/// <summary>
/// Extension methods for adding API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the request pipeline.
    /// Should be added after correlation ID and before request logging.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
