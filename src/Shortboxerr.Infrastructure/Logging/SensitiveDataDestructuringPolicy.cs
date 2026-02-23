using Serilog.Core;
using Serilog.Events;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Serilog destructuring policy that automatically masks sensitive data fields.
/// Prevents API keys, passwords, tokens, and other credentials from appearing in logs.
/// </summary>
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly string[] SensitiveFieldNames = new[]
    {
        "apikey", "api_key", "apiKey", "apikey",
        "password", "passwd", "pwd",
        "token", "access_token", "refresh_token",
        "secret", "secretkey", "secret_key",
        "credential", "credentials",
        "authorization", "auth",
        "connectionstring", "connection_string", "connectionString",
        "bearer"
    };

    private const string MaskedValue = "***REDACTED***";

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;

        // Handle dictionaries (e.g., query strings, headers)
        if (value is System.Collections.IDictionary dict)
        {
            var maskedDict = new Dictionary<object, object?>();
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Key is null) continue;
                
                var key = entry.Key.ToString() ?? "";
                var val = entry.Value;

                if (IsSensitiveKey(key))
                {
                    maskedDict[entry.Key] = MaskedValue;
                }
                else
                {
                    maskedDict[entry.Key] = val;
                }
            }
            result = propertyValueFactory.CreatePropertyValue(maskedDict, true);
            return true;
        }

        // Handle objects with properties (using reflection)
        if (value != null && value.GetType().IsClass && !value.GetType().IsPrimitive && value is not string)
        {
            var type = value.GetType();
            var maskedProps = new Dictionary<string, object?>();

            foreach (var prop in type.GetProperties())
            {
                var propName = prop.Name;
                object? propValue = null;

                try
                {
                    propValue = prop.GetValue(value);
                }
                catch
                {
                    // Ignore properties that can't be read
                    continue;
                }

                if (IsSensitiveKey(propName))
                {
                    maskedProps[propName] = MaskedValue;
                }
                else
                {
                    maskedProps[propName] = propValue;
                }
            }

            result = propertyValueFactory.CreatePropertyValue(maskedProps, true);
            return true;
        }

        return false;
    }

    private static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var lowerKey = key.ToLowerInvariant();

        // Check exact matches
        if (SensitiveFieldNames.Contains(lowerKey))
            return true;

        // Check if key contains sensitive patterns
        foreach (var pattern in SensitiveFieldNames)
        {
            if (lowerKey.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
