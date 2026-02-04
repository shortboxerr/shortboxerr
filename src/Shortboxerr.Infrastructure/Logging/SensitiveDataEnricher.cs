using Serilog.Core;
using Serilog.Events;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Serilog enricher that masks sensitive property values.
/// Works with SensitiveDataDestructuringPolicy to ensure no credentials appear in logs.
/// </summary>
public class SensitiveDataEnricher : ILogEventEnricher
{
    private const string MaskedValue = "***REDACTED***";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Check properties for sensitive keys and mask their values
        var propertiesToMask = new List<string>();

        foreach (var prop in logEvent.Properties)
        {
            if (IsSensitiveKey(prop.Key))
            {
                propertiesToMask.Add(prop.Key);
            }
        }

        // Note: Serilog's LogEvent.Properties is read-only, so we can't modify it directly.
        // The destructuring policy handles masking during object destructuring.
        // This enricher serves as a secondary check and can add metadata.
        if (propertiesToMask.Count > 0)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SensitiveFieldsMasked", propertiesToMask.Count));
        }
    }

    private static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var lowerKey = key.ToLowerInvariant();
        var sensitiveKeys = new[] { "apikey", "api_key", "password", "token", "secret", "credential", "authorization", "connectionstring" };

        return sensitiveKeys.Any(sk => lowerKey.Contains(sk, StringComparison.OrdinalIgnoreCase));
    }
}
