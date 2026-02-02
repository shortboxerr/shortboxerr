using Shortboxerr.Core.Services;
using System.Text.Json;

namespace Shortboxerr.Api.Middleware;

/// <summary>
/// Middleware that enforces the API enabled setting.
/// When API is disabled, external API calls are rejected with 503 Service Unavailable.
/// </summary>
public class ApiAccessMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Paths that are always allowed (even when API is disabled)
    private static readonly string[] AllowedPaths = new[]
    {
        "/health",
        "/ping",
        "/swagger",
        "/api/v1/settings/apikey",  // Allow managing API key settings to re-enable
        "/api/v1/settings/ui",       // Allow UI settings for the app itself
    };

    // Paths that serve the UI (static files, SPA routes)
    private static readonly string[] UiPaths = new[]
    {
        "/index.html",
        "/assets/",
        "/favicon",
    };

    public ApiAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISettingsService settingsService)
    {
        var path = context.Request.Path.Value ?? "";

        // Always allow UI paths (static files, SPA)
        if (IsUiPath(path))
        {
            await _next(context);
            return;
        }

        // Always allow certain API paths
        if (IsAllowedPath(path))
        {
            await _next(context);
            return;
        }

        // Check if this is an API request
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            // Check if API is enabled
            var apiKeyInfo = await settingsService.GetApiKeyAsync(includeFull: false);
            
            if (!apiKeyInfo.IsEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    error = "API access is disabled",
                    message = "The API has been disabled by the administrator. Enable API access in Settings > Security to allow external integrations.",
                    code = "API_DISABLED"
                };
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
                return;
            }
        }

        await _next(context);
    }

    private static bool IsAllowedPath(string path)
    {
        foreach (var allowedPath in AllowedPaths)
        {
            if (path.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsUiPath(string path)
    {
        // Empty path or root goes to UI
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return true;
        }

        // Check explicit UI paths
        foreach (var uiPath in UiPaths)
        {
            if (path.StartsWith(uiPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // If it doesn't start with /api/, /swagger/, /health, /ping, it's probably a UI route
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/ping", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Extension methods for registering the API access middleware.
/// </summary>
public static class ApiAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseApiAccessControl(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiAccessMiddleware>();
    }
}

