using Serilog.Core;
using Serilog.Events;

namespace Shortboxerr.Infrastructure.Logging;

/// <summary>
/// Enriches log events with a shortened source context for improved readability.
/// Extracts only the class name from fully-qualified type names.
/// Example: "Shortboxerr.Infrastructure.ComicVine.ComicVineClient" → "ComicVineClient"
/// </summary>
public class ShortSourceContextEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name for the shortened source context.
    /// </summary>
    public const string ShortSourceContextPropertyName = "ShortSourceContext";

    /// <summary>
    /// Maximum length for the short source context (for alignment).
    /// Longer names will be truncated with "..." suffix.
    /// </summary>
    public int MaxLength { get; init; } = 25;

    /// <summary>
    /// Whether to pad shorter names to MaxLength for column alignment.
    /// </summary>
    public bool PadToMaxLength { get; init; } = true;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue))
        {
            // No source context - add a placeholder for alignment
            var placeholder = PadToMaxLength
                ? new string(' ', MaxLength)
                : "";
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ShortSourceContextPropertyName, placeholder));
            return;
        }

        var sourceContext = sourceContextValue switch
        {
            ScalarValue { Value: string str } => str,
            _ => sourceContextValue.ToString().Trim('"')
        };

        var shortName = ExtractShortName(sourceContext);
        var formattedName = FormatName(shortName);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ShortSourceContextPropertyName, formattedName));
    }

    /// <summary>
    /// Extracts the short class name from a fully-qualified type name.
    /// </summary>
    internal static string ExtractShortName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "";

        // Handle generic types: "MyClass`1" → "MyClass"
        var genericIndex = fullName.IndexOf('`');
        if (genericIndex > 0)
        {
            fullName = fullName[..genericIndex];
        }

        // Get the last segment after the last dot
        var lastDotIndex = fullName.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            // No dot - return as-is
            return fullName;
        }

        if (lastDotIndex >= fullName.Length - 1)
        {
            // Ends with dot - return empty
            return "";
        }

        return fullName[(lastDotIndex + 1)..];
    }

    private string FormatName(string name)
    {
        if (name.Length > MaxLength)
        {
            // Truncate with ellipsis
            return name[..(MaxLength - 3)] + "...";
        }

        if (PadToMaxLength)
        {
            // Pad to max length for alignment
            return name.PadRight(MaxLength);
        }

        return name;
    }
}
