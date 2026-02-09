using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// DDL site adapter for GetComics.org.
/// GetComics is a primary DDL site for comic downloads, featuring releases from major publishers.
/// This adapter parses search results and extracts download links from various file hosts.
/// Supports RSS feed polling and category browsing.
/// </summary>
public partial class GetComicsAdapter : BaseDdlSiteAdapter
{
    private readonly DdlReleaseParser _parser = new();
    private readonly IRssFeedService? _rssFeedService;
    private readonly ILogger<GetComicsAdapter>? _logger;

    public GetComicsAdapter(ILogger<GetComicsAdapter>? logger = null, IRssFeedService? rssFeedService = null)
    {
        _logger = logger;
        _rssFeedService = rssFeedService;
    }

    public override string SiteType => "GetComics";
    public override string DisplayName => "GetComics.org";
    public override string DefaultBaseUrl => "https://getcomics.org";
    public override bool RequiresAuthentication => false;
    public override int DefaultRateLimitPerMinute => 10; // Conservative rate limit

    public override async Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var searchUrl = BuildSearchUrl(query);
            _logger?.LogDebug("Searching GetComics: {Url}", searchUrl);

            var html = await FetchPageAsync(searchUrl, cancellationToken);
            var candidates = ParseSearchPage(html);

            stopwatch.Stop();
            _logger?.LogInformation("GetComics search completed: {Count} results in {Duration}ms",
                candidates.Count, stopwatch.ElapsedMilliseconds);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics search failed: {Message}", ex.Message);
            return DdlSearchResult.Error($"HTTP error: {ex.Message}", SiteType, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "GetComics search error");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    public override async Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // GetComics homepage shows latest releases
            var html = await FetchPageAsync(EffectiveBaseUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();

            stopwatch.Stop();
            _logger?.LogInformation("GetComics latest fetch: {Count} results", candidates.Count);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics latest fetch failed");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets latest releases from the RSS feed.
    /// This is more efficient than scraping the HTML page and includes publication dates.
    /// </summary>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the RSS feed</returns>
    public async Task<DdlSearchResult> GetRssFeedAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var feedUrl = $"{EffectiveBaseUrl}/feed/";
            _logger?.LogDebug("Fetching GetComics RSS feed: {Url}", feedUrl);
            
            RssFeedResult feedResult;
            
            if (_rssFeedService != null)
            {
                feedResult = await _rssFeedService.FetchFeedAsync(feedUrl, cancellationToken);
            }
            else
            {
                // Fallback: fetch and parse manually if no RSS service injected
                var feedContent = await FetchPageAsync(feedUrl, cancellationToken);
                var rssFeedService = new RssFeedService(HttpClient, null);
                feedResult = rssFeedService.ParseFeed(feedContent);
            }
            
            if (!feedResult.Success)
            {
                stopwatch.Stop();
                return DdlSearchResult.Error(feedResult.Error ?? "Failed to parse RSS feed", SiteType, stopwatch.Elapsed);
            }
            
            var candidates = feedResult.Items
                .Take(limit)
                .Select(item => CreateCandidateFromRssItem(item))
                .ToList();
            
            stopwatch.Stop();
            _logger?.LogInformation("GetComics RSS feed: {Count} items", candidates.Count);
            
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics RSS feed fetch failed");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets releases from a specific category (publisher).
    /// </summary>
    /// <param name="category">Category slug (e.g., "dc", "marvel", "image")</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the category</returns>
    public async Task<DdlSearchResult> GetCategoryAsync(string category, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var categoryUrl = $"{EffectiveBaseUrl}/cat/{category}/";
            _logger?.LogDebug("Fetching GetComics category: {Category} from {Url}", category, categoryUrl);
            
            var html = await FetchPageAsync(categoryUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();
            
            // Tag candidates with category
            foreach (var candidate in candidates)
            {
                candidate.Tags.Add(category);
            }
            
            stopwatch.Stop();
            _logger?.LogInformation("GetComics category {Category}: {Count} results", category, candidates.Count);
            
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics category {Category} fetch failed", category);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets releases from a category via its RSS feed.
    /// </summary>
    /// <param name="category">Category slug (e.g., "dc", "marvel", "image")</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the category RSS feed</returns>
    public async Task<DdlSearchResult> GetCategoryRssFeedAsync(string category, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var feedUrl = $"{EffectiveBaseUrl}/cat/{category}/feed/";
            _logger?.LogDebug("Fetching GetComics category RSS feed: {Category} from {Url}", category, feedUrl);
            
            RssFeedResult feedResult;
            
            if (_rssFeedService != null)
            {
                feedResult = await _rssFeedService.FetchFeedAsync(feedUrl, cancellationToken);
            }
            else
            {
                var feedContent = await FetchPageAsync(feedUrl, cancellationToken);
                var rssFeedService = new RssFeedService(HttpClient, null);
                feedResult = rssFeedService.ParseFeed(feedContent);
            }
            
            if (!feedResult.Success)
            {
                stopwatch.Stop();
                return DdlSearchResult.Error(feedResult.Error ?? "Failed to parse RSS feed", SiteType, stopwatch.Elapsed);
            }
            
            var candidates = feedResult.Items
                .Take(limit)
                .Select(item =>
                {
                    var candidate = CreateCandidateFromRssItem(item);
                    candidate.Tags.Add(category);
                    return candidate;
                })
                .ToList();
            
            stopwatch.Stop();
            _logger?.LogInformation("GetComics category RSS {Category}: {Count} items", category, candidates.Count);
            
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics category RSS {Category} fetch failed", category);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets all available categories with their display names.
    /// </summary>
    /// <returns>Dictionary of category slug to display name</returns>
    public static IReadOnlyDictionary<string, string> GetAvailableCategories()
    {
        return new Dictionary<string, string>
        {
            // Publishers
            { DdlCategories.DC, DdlCategories.GetDisplayName(DdlCategories.DC) },
            { DdlCategories.Marvel, DdlCategories.GetDisplayName(DdlCategories.Marvel) },
            { DdlCategories.Image, DdlCategories.GetDisplayName(DdlCategories.Image) },
            { DdlCategories.DarkHorse, DdlCategories.GetDisplayName(DdlCategories.DarkHorse) },
            { DdlCategories.IDW, DdlCategories.GetDisplayName(DdlCategories.IDW) },
            { DdlCategories.Boom, DdlCategories.GetDisplayName(DdlCategories.Boom) },
            { DdlCategories.Dynamite, DdlCategories.GetDisplayName(DdlCategories.Dynamite) },
            { DdlCategories.Valiant, DdlCategories.GetDisplayName(DdlCategories.Valiant) },
            { DdlCategories.Aftershock, DdlCategories.GetDisplayName(DdlCategories.Aftershock) },
            { DdlCategories.Archie, DdlCategories.GetDisplayName(DdlCategories.Archie) },
            // Formats
            { DdlCategories.TPB, DdlCategories.GetDisplayName(DdlCategories.TPB) },
            { DdlCategories.Weekly, DdlCategories.GetDisplayName(DdlCategories.Weekly) },
            { DdlCategories.Collections, DdlCategories.GetDisplayName(DdlCategories.Collections) },
            // Other
            { DdlCategories.Indie, DdlCategories.GetDisplayName(DdlCategories.Indie) },
            { DdlCategories.European, DdlCategories.GetDisplayName(DdlCategories.European) },
            { DdlCategories.Manga, DdlCategories.GetDisplayName(DdlCategories.Manga) }
        };
    }
    
    private DdlCandidate CreateCandidateFromRssItem(RssFeedItem item)
    {
        var parsed = _parser.Parse(item.Title);
        
        var candidate = new DdlCandidate
        {
            Id = item.Guid ?? Guid.NewGuid().ToString(),
            ReleaseTitle = item.Title,
            SourceSite = SiteType,
            SourceUrl = item.Link,
            ParsedInfo = parsed,
            DateFound = item.PubDate ?? DateTime.UtcNow,
            QualityScore = parsed.Confidence,
            Description = item.Description
        };
        
        // Add RSS categories as tags
        foreach (var cat in item.Categories)
        {
            candidate.Tags.Add(cat.ToLowerInvariant());
        }
        
        return candidate;
    }

    public override async Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Extracting download links from: {Url}", pageUrl);

            var html = await FetchPageAsync(pageUrl, cancellationToken);
            var links = ParseDownloadLinks(html);

            _logger?.LogDebug("Found {Count} download links", links.Count);
            return links;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract links from {Url}", pageUrl);
            return Array.Empty<DdlDownloadLink>();
        }
    }

    protected override string BuildSearchUrl(DdlSearchQuery query)
    {
        // GetComics uses WordPress-style search: /?s=search+terms
        var searchTerm = query.RawQuery;

        if (string.IsNullOrEmpty(searchTerm) && !string.IsNullOrEmpty(query.SeriesTitle))
        {
            searchTerm = query.SeriesTitle;

            // Append issue number if provided
            if (query.IssueNumber.HasValue)
            {
                searchTerm += $" {query.IssueNumber:0.##}";
            }

            // Append year if provided
            if (query.Year.HasValue)
            {
                searchTerm += $" {query.Year}";
            }
        }

        if (string.IsNullOrEmpty(searchTerm))
        {
            return EffectiveBaseUrl;
        }

        var encodedSearch = HttpUtility.UrlEncode(searchTerm);
        var url = $"{EffectiveBaseUrl}/?s={encodedSearch}";

        // Add pagination if needed
        if (query.Offset > 0)
        {
            var page = (query.Offset / query.Limit) + 1;
            url += $"&paged={page}";
        }

        return url;
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Parses the search results page HTML to extract release candidates.
    /// GetComics uses article elements with post-title classes for releases.
    /// </summary>
    internal List<DdlCandidate> ParseSearchPage(string html)
    {
        var candidates = new List<DdlCandidate>();

        // Pattern 1: Article entries with post-header h1 containing link
        // <article class="post-*">...<h1 class="post-title"><a href="url">Title</a></h1>...</article>
        var articleMatches = ArticleTitleRegex().Matches(html);

        foreach (Match match in articleMatches)
        {
            var releaseUrl = match.Groups["url"].Value;
            var releaseTitle = HttpUtility.HtmlDecode(match.Groups["title"].Value?.Trim());

            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                continue;
            }

            // Skip navigation, category, and non-comic links
            if (IsNavigationOrCategoryLink(releaseTitle, releaseUrl))
            {
                continue;
            }

            var parsed = _parser.Parse(releaseTitle);
            var candidate = CreateCandidate(releaseTitle, releaseUrl, parsed);

            if (!candidates.Any(c => c.SourceUrl == candidate.SourceUrl))
            {
                candidates.Add(candidate);
            }
        }

        // Pattern 2: Entry-title class (alternative WordPress theme)
        var entryMatches = EntryTitleRegex().Matches(html);

        foreach (Match match in entryMatches)
        {
            var releaseUrl = match.Groups["url"].Value;
            var releaseTitle = HttpUtility.HtmlDecode(match.Groups["title"].Value?.Trim());

            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                continue;
            }

            if (IsNavigationOrCategoryLink(releaseTitle, releaseUrl))
            {
                continue;
            }

            var parsed = _parser.Parse(releaseTitle);
            var candidate = CreateCandidate(releaseTitle, releaseUrl, parsed);

            if (!candidates.Any(c => c.SourceUrl == candidate.SourceUrl))
            {
                candidates.Add(candidate);
            }
        }

        // Extract additional metadata (size, date) from article content if available
        EnrichCandidatesWithMetadata(candidates, html);

        return candidates;
    }

    /// <summary>
    /// Parses download links from a release page.
    /// GetComics typically hosts links on MediaFire, Mega, Pixeldrain, etc.
    /// </summary>
    internal List<DdlDownloadLink> ParseDownloadLinks(string html)
    {
        var links = new List<DdlDownloadLink>();
        var priority = 0;

        // Pattern 1: Download button links (commonly wrapped in download-button class)
        var buttonMatches = DownloadButtonRegex().Matches(html);

        foreach (Match match in buttonMatches)
        {
            var url = match.Groups["url"].Value;
            if (TryAddLink(links, url, ref priority))
            {
                _logger?.LogDebug("Found download button link: {Host}", ExtractHostName(url));
            }
        }

        // Pattern 2: Direct links to known file hosts
        var hostMatches = KnownHostLinkRegex().Matches(html);

        foreach (Match match in hostMatches)
        {
            var url = match.Groups["url"].Value;
            if (TryAddLink(links, url, ref priority))
            {
                _logger?.LogDebug("Found host link: {Host}", ExtractHostName(url));
            }
        }

        // Pattern 3: Links with "Download" text
        var downloadTextMatches = DownloadTextLinkRegex().Matches(html);

        foreach (Match match in downloadTextMatches)
        {
            var url = match.Groups["url"].Value;
            if (TryAddLink(links, url, ref priority))
            {
                _logger?.LogDebug("Found download text link: {Host}", ExtractHostName(url));
            }
        }

        // Sort by host priority (prefer direct/main server > mega > mediafire > others)
        return links.OrderBy(l => GetHostPriority(l.HostName)).ToList();
    }

    private DdlCandidate CreateCandidate(string releaseTitle, string releaseUrl, DdlParsedInfo parsed)
    {
        // Ensure URL is absolute
        if (!releaseUrl.StartsWith("http"))
        {
            releaseUrl = releaseUrl.StartsWith("/")
                ? $"{EffectiveBaseUrl}{releaseUrl}"
                : $"{EffectiveBaseUrl}/{releaseUrl}";
        }

        return new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = releaseTitle,
            SourceSite = SiteType,
            SourceUrl = releaseUrl,
            ParsedInfo = parsed,
            DateFound = DateTime.UtcNow,
            QualityScore = parsed.Confidence
        };
    }

    private bool TryAddLink(List<DdlDownloadLink> links, string url, ref int priority)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Normalize URL
        url = HttpUtility.HtmlDecode(url);

        // Skip if already added
        if (links.Any(l => l.Url == url))
        {
            return false;
        }

        // Skip non-download URLs
        if (!IsDownloadHost(url))
        {
            return false;
        }

        links.Add(new DdlDownloadLink
        {
            Url = url,
            LinkType = DetermineLinkType(url),
            HostName = ExtractHostName(url),
            Priority = priority++
        });

        return true;
    }

    private static bool IsDownloadHost(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        // Known file hosts
        var knownHosts = new[]
        {
            "mega.nz", "mega.co.nz", "mediafire.com", "pixeldrain.com",
            "drive.google.com", "dropbox.com", "1fichier.com", "uploadhaven.com",
            "userscloud.com", "usersdrive.com", "zippyshare.com", "send.cm",
            "main.server", "getcomics"
        };

        // Direct file extensions
        var directExtensions = new[] { ".cbz", ".cbr", ".zip", ".rar", ".pdf" };

        return knownHosts.Any(h => lowerUrl.Contains(h)) ||
               directExtensions.Any(ext => lowerUrl.EndsWith(ext));
    }

    private static bool IsNavigationOrCategoryLink(string title, string url)
    {
        var lowerTitle = title.ToLowerInvariant();
        var lowerUrl = url.ToLowerInvariant();

        // Skip category pages
        if (lowerUrl.Contains("/category/") || lowerUrl.Contains("/tag/"))
        {
            return true;
        }

        // Skip navigation links
        var navTerms = new[] { "home", "contact", "about", "privacy", "dmca", "request" };
        if (navTerms.Any(t => lowerTitle == t || lowerUrl.EndsWith($"/{t}") || lowerUrl.EndsWith($"/{t}/")))
        {
            return true;
        }

        return false;
    }

    private void EnrichCandidatesWithMetadata(List<DdlCandidate> candidates, string html)
    {
        // Try to extract file size from page
        var sizeMatches = FileSizeRegex().Matches(html);

        foreach (Match match in sizeMatches)
        {
            var sizeText = match.Groups["size"].Value;
            var unit = match.Groups["unit"].Value.ToUpperInvariant();

            if (decimal.TryParse(sizeText, out var size))
            {
                var bytes = unit switch
                {
                    "GB" => (long)(size * 1024 * 1024 * 1024),
                    "MB" => (long)(size * 1024 * 1024),
                    "KB" => (long)(size * 1024),
                    _ => 0
                };

                // Apply to first candidate without size (simplified heuristic)
                // Note: Size is init-only so we can't modify it after creation
                // This enrichment should happen during candidate creation instead
                _ = bytes; // Size calculated but not applied - enhancement for future
            }
        }
    }

    private static DdlLinkType DetermineLinkType(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        // Direct file downloads
        if (lowerUrl.EndsWith(".cbz") || lowerUrl.EndsWith(".cbr") ||
            lowerUrl.EndsWith(".zip") || lowerUrl.EndsWith(".rar") ||
            lowerUrl.EndsWith(".pdf"))
        {
            return DdlLinkType.Direct;
        }

        // Known file hosters
        if (lowerUrl.Contains("mega.") || lowerUrl.Contains("mediafire.") ||
            lowerUrl.Contains("pixeldrain.") || lowerUrl.Contains("drive.google.") ||
            lowerUrl.Contains("dropbox.") || lowerUrl.Contains("1fichier."))
        {
            return DdlLinkType.Hoster;
        }

        return DdlLinkType.Redirect;
    }

    private static string? ExtractHostName(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Replace("www.", "");
        }
        catch
        {
            return null;
        }
    }

    private static int GetHostPriority(string? hostName)
    {
        if (string.IsNullOrEmpty(hostName))
        {
            return 99;
        }

        var host = hostName.ToLowerInvariant();

        // Priority order (lower = better)
        return host switch
        {
            var h when h.Contains("getcomics") => 0,    // Main server
            var h when h.Contains("main") => 0,         // Main server
            var h when h.Contains("mega") => 1,         // Mega.nz (fast, reliable)
            var h when h.Contains("mediafire") => 2,    // MediaFire
            var h when h.Contains("pixeldrain") => 3,   // Pixeldrain
            var h when h.Contains("drive.google") => 4, // Google Drive
            var h when h.Contains("dropbox") => 5,      // Dropbox
            var h when h.Contains("1fichier") => 6,     // 1fichier
            _ => 10                                      // Unknown hosts
        };
    }

    // Regex patterns for HTML parsing

    /// <summary>
    /// Matches article post titles: &lt;h1 class="post-title"&gt;&lt;a href="url"&gt;Title&lt;/a&gt;&lt;/h1&gt;
    /// </summary>
    [GeneratedRegex(@"<h1[^>]*class=[""'][^""']*post-title[^""']*[""'][^>]*>\s*<a[^>]*href=[""'](?<url>[^""']+)[""'][^>]*>(?<title>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ArticleTitleRegex();

    /// <summary>
    /// Matches entry titles: &lt;h2 class="entry-title"&gt;&lt;a href="url"&gt;Title&lt;/a&gt;&lt;/h2&gt;
    /// </summary>
    [GeneratedRegex(@"<h[12][^>]*class=[""'][^""']*entry-title[^""']*[""'][^>]*>\s*<a[^>]*href=[""'](?<url>[^""']+)[""'][^>]*>(?<title>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EntryTitleRegex();

    /// <summary>
    /// Matches download button links: &lt;a class="download-button" href="url"&gt;
    /// </summary>
    [GeneratedRegex(@"<a[^>]*class=[""'][^""']*(?:download|button)[^""']*[""'][^>]*href=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadButtonRegex();

    /// <summary>
    /// Matches links to known file hosts.
    /// </summary>
    [GeneratedRegex(@"href=[""'](?<url>https?://(?:www\.)?(?:mega\.nz|mega\.co\.nz|mediafire\.com|pixeldrain\.com|drive\.google\.com|dropbox\.com|1fichier\.com|send\.cm|uploadhaven\.com)[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex KnownHostLinkRegex();

    /// <summary>
    /// Matches links with "Download" text nearby.
    /// </summary>
    [GeneratedRegex(@"<a[^>]*href=[""'](?<url>https?://[^""']+)[""'][^>]*>[^<]*(?:download|mirror|link)[^<]*</a>", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadTextLinkRegex();

    /// <summary>
    /// Matches file size patterns like "100 MB", "1.5 GB".
    /// </summary>
    [GeneratedRegex(@"(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?B)", RegexOptions.IgnoreCase)]
    private static partial Regex FileSizeRegex();
}
