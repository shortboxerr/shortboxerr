using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for parsing and fetching RSS feeds.
/// </summary>
public class RssFeedService : IRssFeedService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RssFeedService>? _logger;
    
    public RssFeedService(HttpClient httpClient, ILogger<RssFeedService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<RssFeedResult> FetchFeedAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger?.LogDebug("Fetching RSS feed: {Url}", feedUrl);
            
            using var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = ParseFeed(content);
            
            stopwatch.Stop();
            
            _logger?.LogInformation("Fetched RSS feed: {Count} items in {Duration}ms", 
                result.Items.Count, stopwatch.ElapsedMilliseconds);
            
            return new RssFeedResult
            {
                Success = result.Success,
                Error = result.Error,
                Title = result.Title,
                Description = result.Description,
                Link = result.Link,
                LastBuildDate = result.LastBuildDate,
                Items = result.Items,
                Duration = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "Failed to fetch RSS feed: {Url}", feedUrl);
            return RssFeedResult.Fail($"HTTP error: {ex.Message}", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Error fetching RSS feed: {Url}", feedUrl);
            return RssFeedResult.Fail(ex.Message, stopwatch.Elapsed);
        }
    }
    
    /// <inheritdoc />
    public RssFeedResult ParseFeed(string feedContent)
    {
        try
        {
            var document = XDocument.Parse(feedContent);
            return ParseFeed(document);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse RSS feed XML");
            return RssFeedResult.Fail($"XML parse error: {ex.Message}");
        }
    }
    
    /// <inheritdoc />
    public RssFeedResult ParseFeed(XDocument document)
    {
        try
        {
            var items = new List<RssFeedItem>();
            
            // Try RSS 2.0 format first
            var channel = document.Descendants("channel").FirstOrDefault();
            if (channel != null)
            {
                return ParseRss2Feed(channel);
            }
            
            // Try Atom format
            XNamespace atom = "http://www.w3.org/2005/Atom";
            var feed = document.Element(atom + "feed");
            if (feed != null)
            {
                return ParseAtomFeed(feed, atom);
            }
            
            return RssFeedResult.Fail("Unknown feed format");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse feed");
            return RssFeedResult.Fail($"Parse error: {ex.Message}");
        }
    }
    
    private RssFeedResult ParseRss2Feed(XElement channel)
    {
        var items = new List<RssFeedItem>();
        
        // Parse channel metadata
        var title = channel.Element("title")?.Value;
        var description = channel.Element("description")?.Value;
        var link = channel.Element("link")?.Value;
        var lastBuildDateStr = channel.Element("lastBuildDate")?.Value;
        
        DateTime? lastBuildDate = null;
        if (!string.IsNullOrEmpty(lastBuildDateStr))
        {
            lastBuildDate = ParseRssDate(lastBuildDateStr);
        }
        
        // Parse items
        foreach (var item in channel.Elements("item"))
        {
            var itemTitle = item.Element("title")?.Value;
            var itemLink = item.Element("link")?.Value;
            
            if (string.IsNullOrWhiteSpace(itemTitle) || string.IsNullOrWhiteSpace(itemLink))
            {
                continue;
            }
            
            var pubDateStr = item.Element("pubDate")?.Value;
            DateTime? pubDate = null;
            if (!string.IsNullOrEmpty(pubDateStr))
            {
                pubDate = ParseRssDate(pubDateStr);
            }
            
            // Parse categories
            var categories = item.Elements("category")
                .Select(c => c.Value)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
            
            // Parse enclosure (for media)
            var enclosure = item.Element("enclosure");
            string? enclosureUrl = null;
            string? enclosureType = null;
            long? enclosureLength = null;
            
            if (enclosure != null)
            {
                enclosureUrl = enclosure.Attribute("url")?.Value;
                enclosureType = enclosure.Attribute("type")?.Value;
                if (long.TryParse(enclosure.Attribute("length")?.Value, out var len))
                {
                    enclosureLength = len;
                }
            }
            
            items.Add(new RssFeedItem
            {
                Title = itemTitle.Trim(),
                Link = itemLink.Trim(),
                Description = item.Element("description")?.Value?.Trim(),
                PubDate = pubDate,
                Guid = item.Element("guid")?.Value,
                Categories = categories,
                Author = item.Element("author")?.Value ?? item.Element("creator")?.Value,
                EnclosureUrl = enclosureUrl,
                EnclosureType = enclosureType,
                EnclosureLength = enclosureLength
            });
        }
        
        return new RssFeedResult
        {
            Success = true,
            Title = title,
            Description = description,
            Link = link,
            LastBuildDate = lastBuildDate,
            Items = items
        };
    }
    
    private RssFeedResult ParseAtomFeed(XElement feed, XNamespace ns)
    {
        var items = new List<RssFeedItem>();
        
        // Parse feed metadata
        var title = feed.Element(ns + "title")?.Value;
        var subtitle = feed.Element(ns + "subtitle")?.Value;
        var link = feed.Elements(ns + "link")
            .FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate" || l.Attribute("rel") == null)
            ?.Attribute("href")?.Value;
        
        var updatedStr = feed.Element(ns + "updated")?.Value;
        DateTime? lastBuildDate = null;
        if (!string.IsNullOrEmpty(updatedStr))
        {
            lastBuildDate = ParseAtomDate(updatedStr);
        }
        
        // Parse entries
        foreach (var entry in feed.Elements(ns + "entry"))
        {
            var entryTitle = entry.Element(ns + "title")?.Value;
            var entryLink = entry.Elements(ns + "link")
                .FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate" || l.Attribute("rel") == null)
                ?.Attribute("href")?.Value;
            
            if (string.IsNullOrWhiteSpace(entryTitle) || string.IsNullOrWhiteSpace(entryLink))
            {
                continue;
            }
            
            var publishedStr = entry.Element(ns + "published")?.Value 
                            ?? entry.Element(ns + "updated")?.Value;
            DateTime? pubDate = null;
            if (!string.IsNullOrEmpty(publishedStr))
            {
                pubDate = ParseAtomDate(publishedStr);
            }
            
            // Parse categories
            var categories = entry.Elements(ns + "category")
                .Select(c => c.Attribute("term")?.Value ?? c.Value)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
            
            items.Add(new RssFeedItem
            {
                Title = entryTitle.Trim(),
                Link = entryLink.Trim(),
                Description = entry.Element(ns + "summary")?.Value?.Trim() 
                           ?? entry.Element(ns + "content")?.Value?.Trim(),
                PubDate = pubDate,
                Guid = entry.Element(ns + "id")?.Value,
                Categories = categories,
                Author = entry.Element(ns + "author")?.Element(ns + "name")?.Value
            });
        }
        
        return new RssFeedResult
        {
            Success = true,
            Title = title,
            Description = subtitle,
            Link = link,
            LastBuildDate = lastBuildDate,
            Items = items
        };
    }
    
    private static DateTime? ParseRssDate(string dateStr)
    {
        // RSS 2.0 date formats
        var formats = new[]
        {
            "ddd, dd MMM yyyy HH:mm:ss zzz",
            "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
            "ddd, dd MMM yyyy HH:mm:ss 'UTC'",
            "ddd, dd MMM yyyy HH:mm:ss",
            "dd MMM yyyy HH:mm:ss zzz",
            "dd MMM yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss"
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, 
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var date))
            {
                return date.ToUniversalTime();
            }
        }
        
        // Fallback to general parse
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, 
            DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }
        
        return null;
    }
    
    private static DateTime? ParseAtomDate(string dateStr)
    {
        // Atom uses ISO 8601 format
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, 
            DateTimeStyles.RoundtripKind, out var date))
        {
            return date.ToUniversalTime();
        }
        
        return null;
    }
}
