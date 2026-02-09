using System.Xml.Linq;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for parsing and fetching RSS feeds from DDL sites.
/// </summary>
public interface IRssFeedService
{
    /// <summary>
    /// Fetches and parses an RSS feed from a URL.
    /// </summary>
    /// <param name="feedUrl">The RSS feed URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed RSS feed result</returns>
    Task<RssFeedResult> FetchFeedAsync(string feedUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Parses RSS feed XML content.
    /// </summary>
    /// <param name="feedContent">The raw XML content</param>
    /// <returns>Parsed RSS feed result</returns>
    RssFeedResult ParseFeed(string feedContent);
    
    /// <summary>
    /// Parses RSS feed from XDocument.
    /// </summary>
    /// <param name="document">The parsed XML document</param>
    /// <returns>Parsed RSS feed result</returns>
    RssFeedResult ParseFeed(XDocument document);
}

/// <summary>
/// Result of fetching/parsing an RSS feed.
/// </summary>
public class RssFeedResult
{
    /// <summary>
    /// Whether the fetch/parse was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if not successful.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Feed title.
    /// </summary>
    public string? Title { get; init; }
    
    /// <summary>
    /// Feed description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Feed link/URL.
    /// </summary>
    public string? Link { get; init; }
    
    /// <summary>
    /// Last build date of the feed.
    /// </summary>
    public DateTime? LastBuildDate { get; init; }
    
    /// <summary>
    /// Feed items/entries.
    /// </summary>
    public IReadOnlyList<RssFeedItem> Items { get; init; } = Array.Empty<RssFeedItem>();
    
    /// <summary>
    /// How long the fetch took.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    public static RssFeedResult Ok(string? title, IReadOnlyList<RssFeedItem> items, TimeSpan duration)
    {
        return new RssFeedResult
        {
            Success = true,
            Title = title,
            Items = items,
            Duration = duration
        };
    }
    
    public static RssFeedResult Fail(string error, TimeSpan duration = default)
    {
        return new RssFeedResult
        {
            Success = false,
            Error = error,
            Duration = duration
        };
    }
}

/// <summary>
/// A single item from an RSS feed.
/// </summary>
public class RssFeedItem
{
    /// <summary>
    /// Item title.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// Item link/URL.
    /// </summary>
    public required string Link { get; init; }
    
    /// <summary>
    /// Item description/summary.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Publication date.
    /// </summary>
    public DateTime? PubDate { get; init; }
    
    /// <summary>
    /// Item GUID (unique identifier).
    /// </summary>
    public string? Guid { get; init; }
    
    /// <summary>
    /// Categories/tags.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Author name.
    /// </summary>
    public string? Author { get; init; }
    
    /// <summary>
    /// Enclosure URL (for media).
    /// </summary>
    public string? EnclosureUrl { get; init; }
    
    /// <summary>
    /// Enclosure type (MIME type).
    /// </summary>
    public string? EnclosureType { get; init; }
    
    /// <summary>
    /// Enclosure length in bytes.
    /// </summary>
    public long? EnclosureLength { get; init; }
}

/// <summary>
/// Known comic publisher categories for DDL sites.
/// </summary>
public static class DdlCategories
{
    // Major publishers
    public const string DC = "dc";
    public const string Marvel = "marvel";
    public const string Image = "image";
    public const string DarkHorse = "dark-horse";
    public const string IDW = "idw";
    public const string Boom = "boom-studios";
    public const string Dynamite = "dynamite";
    public const string Valiant = "valiant";
    public const string Aftershock = "aftershock";
    public const string Archie = "archie";
    
    // Formats/types
    public const string TPB = "tp-hc";
    public const string Weekly = "weekly";
    public const string Collections = "collections";
    
    // Other
    public const string Indie = "indie";
    public const string European = "european";
    public const string Manga = "manga";
    
    /// <summary>
    /// Gets all known category slugs.
    /// </summary>
    public static IReadOnlyList<string> AllCategories => new[]
    {
        DC, Marvel, Image, DarkHorse, IDW, Boom, Dynamite, Valiant, Aftershock, Archie,
        TPB, Weekly, Collections,
        Indie, European, Manga
    };
    
    /// <summary>
    /// Gets display name for a category slug.
    /// </summary>
    public static string GetDisplayName(string slug)
    {
        return slug switch
        {
            DC => "DC Comics",
            Marvel => "Marvel Comics",
            Image => "Image Comics",
            DarkHorse => "Dark Horse",
            IDW => "IDW Publishing",
            Boom => "BOOM! Studios",
            Dynamite => "Dynamite Entertainment",
            Valiant => "Valiant Comics",
            Aftershock => "AfterShock Comics",
            Archie => "Archie Comics",
            TPB => "Trade Paperbacks & Hardcovers",
            Weekly => "Weekly Releases",
            Collections => "Collections",
            Indie => "Independent",
            European => "European Comics",
            Manga => "Manga",
            _ => slug.Replace("-", " ").ToTitleCase()
        };
    }
}

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to title case.
    /// </summary>
    public static string ToTitleCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLowerInvariant());
    }
}
