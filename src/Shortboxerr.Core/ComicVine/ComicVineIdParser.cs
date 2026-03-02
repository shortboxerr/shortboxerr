using System.Text.RegularExpressions;

namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Utility for parsing and detecting ComicVine IDs from user input.
/// ComicVine uses prefixed IDs: 4050-XXXXX (volumes), 4000-XXXXXX (issues), 4045-XXXXX (story arcs).
/// </summary>
public static partial class ComicVineIdParser
{
    /// <summary>
    /// ComicVine resource type prefixes.
    /// </summary>
    public static class Prefixes
    {
        public const string Volume = "4050";
        public const string Issue = "4000";
        public const string StoryArc = "4045";
        public const string Character = "4005";
        public const string Publisher = "4010";
    }

    // Regex patterns for full ComicVine ID format (e.g., "4050-12345")
    [GeneratedRegex(@"^4050-(\d+)$", RegexOptions.Compiled)]
    private static partial Regex VolumeIdRegex();

    [GeneratedRegex(@"^4000-(\d+)$", RegexOptions.Compiled)]
    private static partial Regex IssueIdRegex();

    [GeneratedRegex(@"^4045-(\d+)$", RegexOptions.Compiled)]
    private static partial Regex StoryArcIdRegex();

    // Regex for any ComicVine prefixed ID
    [GeneratedRegex(@"^(4050|4000|4045|4005|4010)-(\d+)$", RegexOptions.Compiled)]
    private static partial Regex AnyPrefixedIdRegex();

    // Regex for plain numeric ID (just digits)
    [GeneratedRegex(@"^\d+$", RegexOptions.Compiled)]
    private static partial Regex PlainNumericRegex();

    // Regex to extract ID from ComicVine URLs
    [GeneratedRegex(@"comicvine\.gamespot\.com/[^/]+/(4050|4000|4045)-(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ComicVineUrlRegex();

    /// <summary>
    /// Attempts to parse a ComicVine ID from user input.
    /// Supports formats: "4050-12345", "12345", and ComicVine URLs.
    /// </summary>
    /// <param name="input">User input string.</param>
    /// <returns>Parsed result with type and numeric ID, or null if not a valid ID.</returns>
    public static ComicVineIdParseResult? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        // Try full prefixed format first (4050-12345)
        var prefixedMatch = AnyPrefixedIdRegex().Match(input);
        if (prefixedMatch.Success)
        {
            var prefix = prefixedMatch.Groups[1].Value;
            var numericId = int.Parse(prefixedMatch.Groups[2].Value);
            var type = GetResourceType(prefix);
            return new ComicVineIdParseResult(type, numericId, $"{prefix}-{numericId}");
        }

        // Try ComicVine URL format
        var urlMatch = ComicVineUrlRegex().Match(input);
        if (urlMatch.Success)
        {
            var prefix = urlMatch.Groups[1].Value;
            var numericId = int.Parse(urlMatch.Groups[2].Value);
            var type = GetResourceType(prefix);
            return new ComicVineIdParseResult(type, numericId, $"{prefix}-{numericId}");
        }

        // Plain numeric is ambiguous - could be volume or issue
        // Return as Unknown type, caller must specify context
        if (PlainNumericRegex().IsMatch(input) && int.TryParse(input, out var plainId))
        {
            return new ComicVineIdParseResult(ComicVineResourceType.Unknown, plainId, input);
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse input as a specific resource type.
    /// For plain numeric IDs, assumes the specified type.
    /// </summary>
    public static ComicVineIdParseResult? TryParseAs(string? input, ComicVineResourceType expectedType)
    {
        var result = TryParse(input);
        if (result == null)
            return null;

        // If we got a specific type, it must match
        if (result.Type != ComicVineResourceType.Unknown && result.Type != expectedType)
            return null;

        // For unknown (plain numeric), assume the expected type
        if (result.Type == ComicVineResourceType.Unknown)
        {
            var prefix = GetPrefix(expectedType);
            return new ComicVineIdParseResult(expectedType, result.NumericId, $"{prefix}-{result.NumericId}");
        }

        return result;
    }

    /// <summary>
    /// Checks if input looks like a ComicVine ID (vs a search term).
    /// </summary>
    public static bool IsComicVineId(string? input)
    {
        return TryParse(input) != null;
    }

    /// <summary>
    /// Checks if input is specifically a volume ID.
    /// </summary>
    public static bool IsVolumeId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        return VolumeIdRegex().IsMatch(input.Trim());
    }

    /// <summary>
    /// Checks if input is specifically an issue ID.
    /// </summary>
    public static bool IsIssueId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        return IssueIdRegex().IsMatch(input.Trim());
    }

    /// <summary>
    /// Checks if input is specifically a story arc ID.
    /// </summary>
    public static bool IsStoryArcId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        return StoryArcIdRegex().IsMatch(input.Trim());
    }

    private static ComicVineResourceType GetResourceType(string prefix) => prefix switch
    {
        Prefixes.Volume => ComicVineResourceType.Volume,
        Prefixes.Issue => ComicVineResourceType.Issue,
        Prefixes.StoryArc => ComicVineResourceType.StoryArc,
        Prefixes.Character => ComicVineResourceType.Character,
        Prefixes.Publisher => ComicVineResourceType.Publisher,
        _ => ComicVineResourceType.Unknown
    };

    private static string GetPrefix(ComicVineResourceType type) => type switch
    {
        ComicVineResourceType.Volume => Prefixes.Volume,
        ComicVineResourceType.Issue => Prefixes.Issue,
        ComicVineResourceType.StoryArc => Prefixes.StoryArc,
        ComicVineResourceType.Character => Prefixes.Character,
        ComicVineResourceType.Publisher => Prefixes.Publisher,
        _ => ""
    };
}

/// <summary>
/// Result of parsing a ComicVine ID.
/// </summary>
public record ComicVineIdParseResult(
    ComicVineResourceType Type,
    int NumericId,
    string FullId
);

/// <summary>
/// Types of ComicVine resources.
/// </summary>
public enum ComicVineResourceType
{
    Unknown,
    Volume,
    Issue,
    StoryArc,
    Character,
    Publisher
}
