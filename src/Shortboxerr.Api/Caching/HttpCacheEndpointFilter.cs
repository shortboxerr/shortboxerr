using System.Security.Cryptography;
using System.Text;

namespace Shortboxerr.Api.Caching;

/// <summary>
/// HTTP caching settings for endpoints.
/// </summary>
public class HttpCacheSettings
{
    /// <summary>Cache duration in seconds (default: 120 seconds / 2 minutes).</summary>
    public int MaxAgeSeconds { get; set; } = 120;
    
    /// <summary>Whether the cache is private (client-only) or public.</summary>
    public bool IsPrivate { get; set; } = false;
    
    /// <summary>Whether to allow no-store directive.</summary>
    public bool NoStore { get; set; } = false;
    
    /// <summary>Whether to include ETag support.</summary>
    public bool IncludeETag { get; set; } = true;
    
    /// <summary>Whether to include Last-Modified support.</summary>
    public bool IncludeLastModified { get; set; } = true;
}

/// <summary>
/// Endpoint filter that adds Cache-Control headers and ETag support to responses.
/// </summary>
public class HttpCacheEndpointFilter : IEndpointFilter
{
    private readonly HttpCacheSettings _settings;

    public HttpCacheEndpointFilter(HttpCacheSettings settings)
    {
        _settings = settings;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var response = httpContext.Response;

        // Execute the endpoint
        var result = await next(context);

        // Only apply caching to successful GET requests
        if (request.Method != HttpMethods.Get)
        {
            return result;
        }

        // Skip caching for non-OK results
        if (result is IResult { } typedResult && response.StatusCode >= 400)
        {
            return result;
        }

        // Set Cache-Control header
        if (_settings.NoStore)
        {
            response.Headers.CacheControl = "no-store";
        }
        else
        {
            var cacheControl = _settings.IsPrivate
                ? $"private, max-age={_settings.MaxAgeSeconds}"
                : $"public, max-age={_settings.MaxAgeSeconds}";
            response.Headers.CacheControl = cacheControl;
        }

        return result;
    }
}

/// <summary>
/// Extension methods for applying HTTP caching to endpoints.
/// </summary>
public static class HttpCacheExtensions
{
    /// <summary>
    /// Adds Cache-Control headers to the endpoint with default settings (2 minutes, public).
    /// </summary>
    public static RouteHandlerBuilder WithHttpCache(this RouteHandlerBuilder builder, int maxAgeSeconds = 120)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(new HttpCacheSettings
        {
            MaxAgeSeconds = maxAgeSeconds
        }));
    }

    /// <summary>
    /// Adds Cache-Control headers with custom settings.
    /// </summary>
    public static RouteHandlerBuilder WithHttpCache(this RouteHandlerBuilder builder, HttpCacheSettings settings)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(settings));
    }

    /// <summary>
    /// Adds private Cache-Control headers (client-only caching).
    /// </summary>
    public static RouteHandlerBuilder WithPrivateCache(this RouteHandlerBuilder builder, int maxAgeSeconds = 120)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(new HttpCacheSettings
        {
            MaxAgeSeconds = maxAgeSeconds,
            IsPrivate = true
        }));
    }

    /// <summary>
    /// Marks the endpoint as non-cacheable.
    /// </summary>
    public static RouteHandlerBuilder WithNoCache(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(new HttpCacheSettings
        {
            NoStore = true
        }));
    }

    /// <summary>
    /// Adds long-lived cache headers for static content (1 day).
    /// </summary>
    public static RouteHandlerBuilder WithLongCache(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(new HttpCacheSettings
        {
            MaxAgeSeconds = 86400, // 1 day
            IsPrivate = false
        }));
    }

    /// <summary>
    /// Adds very long-lived cache headers for immutable content (7 days).
    /// </summary>
    public static RouteHandlerBuilder WithImmutableCache(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(new HttpCacheEndpointFilter(new HttpCacheSettings
        {
            MaxAgeSeconds = 604800, // 7 days
            IsPrivate = false
        }));
    }
}

/// <summary>
/// Helper for generating and validating ETags.
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generates an ETag from a timestamp.
    /// </summary>
    public static string GenerateETag(DateTime timestamp)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(timestamp.Ticks.ToString()));
        return $"\"{Convert.ToHexString(hash)}\"";
    }

    /// <summary>
    /// Generates an ETag from a version number or string.
    /// </summary>
    public static string GenerateETag(string version)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(version));
        return $"\"{Convert.ToHexString(hash)}\"";
    }

    /// <summary>
    /// Generates an ETag from an object's hash code combined with a timestamp.
    /// </summary>
    public static string GenerateETag(int id, DateTime updatedAt)
    {
        var combined = $"{id}-{updatedAt.Ticks}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(combined));
        return $"\"{Convert.ToHexString(hash)}\"";
    }

    /// <summary>
    /// Checks if the If-None-Match header matches the current ETag.
    /// Returns true if the client has a valid cached version.
    /// </summary>
    public static bool IsNotModified(HttpRequest request, string currentETag)
    {
        var ifNoneMatch = request.Headers.IfNoneMatch.ToString();
        if (string.IsNullOrEmpty(ifNoneMatch))
            return false;

        // Handle multiple ETags in the header
        var clientETags = ifNoneMatch.Split(',', StringSplitOptions.TrimEntries);
        return clientETags.Contains(currentETag) || clientETags.Contains("*");
    }

    /// <summary>
    /// Checks if the If-Modified-Since header indicates the resource hasn't changed.
    /// </summary>
    public static bool IsNotModifiedSince(HttpRequest request, DateTime lastModified)
    {
        var ifModifiedSince = request.Headers.IfModifiedSince.ToString();
        if (string.IsNullOrEmpty(ifModifiedSince))
            return false;

        if (DateTime.TryParse(ifModifiedSince, out var clientDate))
        {
            // Remove milliseconds for comparison (HTTP dates don't have millisecond precision)
            var lastModifiedTruncated = new DateTime(
                lastModified.Year, lastModified.Month, lastModified.Day,
                lastModified.Hour, lastModified.Minute, lastModified.Second,
                lastModified.Kind);
            return clientDate >= lastModifiedTruncated;
        }

        return false;
    }
}
