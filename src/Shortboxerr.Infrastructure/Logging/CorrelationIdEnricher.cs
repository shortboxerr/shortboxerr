using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Serilog enricher that adds the correlation ID from HttpContext to log events.
/// 
/// This enricher reads the TraceIdentifier from the current HttpContext,
/// which should be set by the CorrelationIdMiddleware.
/// 
/// The correlation ID is added as the "CorrelationId" property to all log events
/// within a request context.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name for the correlation ID in log events.
    /// </summary>
    public const string CorrelationIdPropertyName = "CorrelationId";

    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Creates a new CorrelationIdEnricher.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor to get the current request context.</param>
    public CorrelationIdEnricher(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = GetCorrelationId();
        
        // Always add correlation ID property (even if empty for consistent output)
        var value = string.IsNullOrEmpty(correlationId) ? "-" : correlationId;
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(CorrelationIdPropertyName, value));
    }

    private string? GetCorrelationId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        
        if (httpContext == null)
        {
            return null;
        }

        // TraceIdentifier is set by CorrelationIdMiddleware
        return httpContext.TraceIdentifier;
    }
}
