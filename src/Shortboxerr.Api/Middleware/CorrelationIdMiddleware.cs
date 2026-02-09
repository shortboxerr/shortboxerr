using Microsoft.Extensions.Primitives;

namespace Shortboxerr.Api.Middleware;

/// <summary>
/// Middleware that ensures each request has a correlation ID for distributed tracing.
/// 
/// Behavior:
/// 1. Checks for existing correlation ID in X-Correlation-ID header
/// 2. Falls back to X-Request-ID header if present
/// 3. Generates a new GUID if no correlation ID is provided
/// 4. Sets HttpContext.TraceIdentifier for use by logging
/// 5. Adds the correlation ID to the response headers
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Standard header name for correlation IDs.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// Alternative header name (commonly used by proxies).
    /// </summary>
    public const string RequestIdHeader = "X-Request-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Set the trace identifier for use by other middleware/logging
        context.TraceIdentifier = correlationId;

        // Add correlation ID to response headers for client-side tracing
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Try X-Correlation-ID first
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues correlationId)
            && !StringValues.IsNullOrEmpty(correlationId))
        {
            var id = correlationId.ToString();
            _logger.LogDebug("Using correlation ID from {Header} header: {CorrelationId}", CorrelationIdHeader, id);
            return id;
        }

        // Fall back to X-Request-ID
        if (context.Request.Headers.TryGetValue(RequestIdHeader, out StringValues requestId)
            && !StringValues.IsNullOrEmpty(requestId))
        {
            var id = requestId.ToString();
            _logger.LogDebug("Using correlation ID from {Header} header: {CorrelationId}", RequestIdHeader, id);
            return id;
        }

        // Generate new correlation ID
        var newId = GenerateCorrelationId();
        _logger.LogDebug("Generated new correlation ID: {CorrelationId}", newId);
        return newId;
    }

    /// <summary>
    /// Generates a new correlation ID.
    /// Uses a short format for readability while maintaining uniqueness.
    /// Format: timestamp-random (e.g., "20260209143045-a1b2c3d4")
    /// </summary>
    internal static string GenerateCorrelationId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..8]; // First 8 chars of GUID
        return $"{timestamp}-{random}";
    }
}

/// <summary>
/// Extension methods for adding correlation ID middleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation ID middleware to the request pipeline.
    /// Should be added early in the pipeline, before request logging.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
