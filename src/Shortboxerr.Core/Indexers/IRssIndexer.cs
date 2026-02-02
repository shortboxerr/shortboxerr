using Shortboxerr.Core.Providers;

namespace Shortboxerr.Core.Indexers;

/// <summary>
/// Specialized indexer interface for RSS/Atom feed-based discovery.
/// Extends IIndexerProvider with RSS-specific functionality.
/// </summary>
public interface IRssIndexer : IIndexerProvider
{
    /// <summary>
    /// The RSS feed URL.
    /// </summary>
    string FeedUrl { get; }
    
    /// <summary>
    /// Poll interval for automatic feed checking.
    /// </summary>
    TimeSpan PollInterval { get; }
    
    /// <summary>
    /// Last time the feed was successfully polled.
    /// </summary>
    DateTime? LastPolledAt { get; }
    
    /// <summary>
    /// Parse and return items from the RSS feed.
    /// </summary>
    Task<RssFeedResult> FetchFeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for an RSS indexer.
/// </summary>
public class RssIndexerSettings
{
    /// <summary>
    /// Unique identifier for this indexer.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Display name for this indexer.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// RSS/Atom feed URL.
    /// </summary>
    public required string FeedUrl { get; init; }
    
    /// <summary>
    /// How often to poll the feed (in minutes). Default: 30.
    /// </summary>
    public int PollIntervalMinutes { get; init; } = 30;
    
    /// <summary>
    /// Whether this indexer is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Priority for this indexer (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// Maximum items to fetch per poll.
    /// </summary>
    public int MaxItemsPerPoll { get; init; } = 100;
    
    /// <summary>
    /// Custom User-Agent for feed requests.
    /// </summary>
    public string? UserAgent { get; init; }
    
    /// <summary>
    /// Basic auth username (if required).
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Basic auth password (if required).
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
    
    /// <summary>
    /// Categories/tags to filter by (empty = all).
    /// </summary>
    public List<string> FilterCategories { get; init; } = new();
    
    /// <summary>
    /// Download link extraction pattern (regex).
    /// Used to extract direct download URLs from feed items.
    /// </summary>
    public string? DownloadLinkPattern { get; init; }
}

/// <summary>
/// Result of fetching an RSS feed.
/// </summary>
public class RssFeedResult
{
    /// <summary>
    /// Whether the feed was fetched successfully.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if fetch failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Feed title.
    /// </summary>
    public string? FeedTitle { get; init; }
    
    /// <summary>
    /// Feed description.
    /// </summary>
    public string? FeedDescription { get; init; }
    
    /// <summary>
    /// Items from the feed.
    /// </summary>
    public List<RssFeedItem> Items { get; init; } = new();
    
    /// <summary>
    /// When the feed was last updated.
    /// </summary>
    public DateTime? LastUpdated { get; init; }
    
    /// <summary>
    /// When we fetched this feed.
    /// </summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// HTTP status code from the request.
    /// </summary>
    public int? StatusCode { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static RssFeedResult Ok(List<RssFeedItem> items, string? title = null) => new()
    {
        Success = true,
        FeedTitle = title,
        Items = items
    };
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static RssFeedResult Fail(string error, int? statusCode = null) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}

/// <summary>
/// A single item from an RSS/Atom feed.
/// </summary>
public class RssFeedItem
{
    /// <summary>
    /// Unique identifier for this item (guid from feed).
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Item title.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// Item description/summary.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Link to the item page.
    /// </summary>
    public string? Link { get; init; }
    
    /// <summary>
    /// Direct download link (if available).
    /// </summary>
    public string? DownloadLink { get; init; }
    
    /// <summary>
    /// File size in bytes (if specified).
    /// </summary>
    public long? Size { get; init; }
    
    /// <summary>
    /// When this item was published.
    /// </summary>
    public DateTime? PublishedAt { get; init; }
    
    /// <summary>
    /// Categories/tags for this item.
    /// </summary>
    public List<string> Categories { get; init; } = new();
    
    /// <summary>
    /// Author of the item.
    /// </summary>
    public string? Author { get; init; }
    
    /// <summary>
    /// Enclosure URL (for podcast-style feeds).
    /// </summary>
    public string? EnclosureUrl { get; init; }
    
    /// <summary>
    /// Enclosure MIME type.
    /// </summary>
    public string? EnclosureType { get; init; }
    
    /// <summary>
    /// Raw XML content for custom parsing.
    /// </summary>
    public string? RawContent { get; init; }
}

