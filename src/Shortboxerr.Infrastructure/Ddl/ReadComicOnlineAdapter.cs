using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// DDL site adapter for ReadComicOnline.
/// ReadComicOnline is a comic reading/download site that frequently changes domains.
/// This adapter handles dynamic homepage detection and standard HTML parsing.
/// </summary>
public partial class ReadComicOnlineAdapter : BaseDdlSiteAdapter
{
    private readonly DdlReleaseParser _parser = new();
    private readonly IRssFeedService? _rssFeedService;
    private readonly ILogger<ReadComicOnlineAdapter>? _logger;
    
    // Known domain variants - site frequently changes
    private static readonly string[] KnownDomains = new[]
    {
        "readcomiconline.li",
        "readcomiconline.to",
        "readcomiconline.org",
        "readcomiconline.cc"
    };

    public ReadComicOnlineAdapter(ILogger<ReadComicOnlineAdapter>? logger = null, IRssFeedService? rssFeedService = null)
    {
        _logger = logger;
        _rssFeedService = rssFeedService;
    }

    public override string SiteType => "ReadComicOnline";
    public override string DisplayName => "ReadComicOnline";
    public override string DefaultBaseUrl => "https://readcomiconline.li";
    public override bool RequiresAuthentication => false;
    public override int DefaultRateLimitPerMinute => 5; // More restrictive - site has aggressive protection

    /// <summary>
    /// Attempts to detect the current working homepage URL.
    /// ReadComicOnline frequently changes domains, so this method checks known domains
    /// and looks for "Go to Homepage" buttons on redirect pages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The detected homepage URL, or the default if detection fails</returns>
    public async Task<string> DetectHomepageAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Detecting ReadComicOnline homepage...");

        foreach (var domain in KnownDomains)
        {
            try
            {
                var testUrl = $"https://{domain}";
                _logger?.LogDebug("Testing domain: {Domain}", domain);

                using var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    // Check for "Go to Homepage" redirect button
                    var homepageMatch = HomepageButtonRegex().Match(html);
                    if (homepageMatch.Success)
                    {
                        var detectedUrl = homepageMatch.Groups["url"].Value;
                        _logger?.LogInformation("Detected homepage redirect: {Url}", detectedUrl);
                        return detectedUrl;
                    }
                    
                    // If page loaded successfully and has comic content, this is likely the correct domain
                    if (html.Contains("Comic List") || html.Contains("Latest Comics") || html.Contains("class=\"comic\""))
                    {
                        _logger?.LogInformation("Detected working domain: {Domain}", domain);
                        return testUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Domain {Domain} check failed", domain);
            }
        }

        _logger?.LogWarning("Could not detect ReadComicOnline homepage, using default: {Url}", DefaultBaseUrl);
        return DefaultBaseUrl;
    }

    public override async Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var searchUrl = BuildSearchUrl(query);
            _logger?.LogDebug("Searching ReadComicOnline: {Url}", searchUrl);

            var html = await FetchPageAsync(searchUrl, cancellationToken);
            var candidates = ParseSearchPage(html);

            stopwatch.Stop();
            _logger?.LogInformation("ReadComicOnline search completed: {Count} results in {Duration}ms",
                candidates.Count, stopwatch.ElapsedMilliseconds);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline search failed: {Message}", ex.Message);
            return DdlSearchResult.Error($"HTTP error: {ex.Message}", SiteType, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "ReadComicOnline search error");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    public override async Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // ReadComicOnline has a Latest Comics section
            var latestUrl = $"{EffectiveBaseUrl}/ComicList/LatestUpdate";
            var html = await FetchPageAsync(latestUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();

            stopwatch.Stop();
            _logger?.LogInformation("ReadComicOnline latest fetch: {Count} results", candidates.Count);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline latest fetch failed");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets comics from a specific category/publisher.
    /// </summary>
    public async Task<DdlSearchResult> GetCategoryAsync(string category, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // ReadComicOnline uses genre/category pages
            var categoryUrl = $"{EffectiveBaseUrl}/ComicList/Genre/{Uri.EscapeDataString(category)}";
            _logger?.LogDebug("Fetching ReadComicOnline category: {Category} from {Url}", category, categoryUrl);

            var html = await FetchPageAsync(categoryUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();

            foreach (var candidate in candidates)
            {
                candidate.Tags.Add(category.ToLowerInvariant());
            }

            stopwatch.Stop();
            _logger?.LogInformation("ReadComicOnline category {Category}: {Count} results", category, candidates.Count);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline category {Category} fetch failed", category);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets comics by publisher.
    /// </summary>
    public async Task<DdlSearchResult> GetPublisherAsync(string publisher, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // ReadComicOnline has publisher-specific pages
            var publisherSlug = publisher.ToLowerInvariant() switch
            {
                "dc" or "dc comics" => "DC-Comics",
                "marvel" or "marvel comics" => "Marvel",
                "image" or "image comics" => "Image",
                "dark horse" or "darkhorse" => "Dark-Horse",
                "idw" or "idw publishing" => "IDW",
                "boom" or "boom studios" => "BOOM-Studios",
                "dynamite" => "Dynamite-Entertainment",
                "valiant" => "Valiant",
                _ => publisher.Replace(" ", "-")
            };

            var publisherUrl = $"{EffectiveBaseUrl}/ComicList/Publisher/{publisherSlug}";
            _logger?.LogDebug("Fetching ReadComicOnline publisher: {Publisher} from {Url}", publisher, publisherUrl);

            var html = await FetchPageAsync(publisherUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();

            foreach (var candidate in candidates)
            {
                candidate.Tags.Add(publisher.ToLowerInvariant());
            }

            stopwatch.Stop();
            _logger?.LogInformation("ReadComicOnline publisher {Publisher}: {Count} results", publisher, candidates.Count);

            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline publisher {Publisher} fetch failed", publisher);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets latest releases from the RSS feed.
    /// ReadComicOnline may use different RSS feed paths - this method tries common patterns.
    /// Falls back to HTML scraping via GetLatestAsync if RSS is unavailable.
    /// </summary>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the RSS feed</returns>
    public async Task<DdlSearchResult> GetRssFeedAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Try common RSS feed paths used by similar sites
            var feedPaths = new[] { "/feed/", "/rss/", "/rss.xml", "/feed.xml", "/Rss" };
            
            foreach (var feedPath in feedPaths)
            {
                var feedUrl = $"{EffectiveBaseUrl}{feedPath}";
                _logger?.LogDebug("Trying ReadComicOnline RSS feed: {Url}", feedUrl);
                
                try
                {
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
                    
                    if (feedResult.Success && feedResult.Items.Any())
                    {
                        var candidates = feedResult.Items
                            .Take(limit)
                            .Select(item => CreateCandidateFromRssItem(item))
                            .ToList();
                        
                        stopwatch.Stop();
                        _logger?.LogInformation("ReadComicOnline RSS feed ({Path}): {Count} items", feedPath, candidates.Count);
                        
                        return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
                    }
                }
                catch (HttpRequestException)
                {
                    // Try next feed path
                    continue;
                }
            }
            
            // RSS not available - fall back to HTML scraping
            _logger?.LogDebug("ReadComicOnline RSS feed not available, falling back to HTML scraping");
            stopwatch.Stop();
            return await GetLatestAsync(limit, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline RSS feed fetch failed");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets releases from a category via its RSS feed.
    /// Falls back to GetCategoryAsync if RSS is unavailable.
    /// </summary>
    /// <param name="category">Category slug (e.g., "action", "superhero")</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the category RSS feed</returns>
    public async Task<DdlSearchResult> GetCategoryRssFeedAsync(string category, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Try common category RSS feed patterns
            var feedPaths = new[]
            {
                $"/ComicList/Genre/{Uri.EscapeDataString(category)}/feed/",
                $"/Genre/{Uri.EscapeDataString(category)}/rss/",
                $"/cat/{Uri.EscapeDataString(category)}/feed/"
            };
            
            foreach (var feedPath in feedPaths)
            {
                var feedUrl = $"{EffectiveBaseUrl}{feedPath}";
                _logger?.LogDebug("Trying ReadComicOnline category RSS feed: {Category} from {Url}", category, feedUrl);
                
                try
                {
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
                    
                    if (feedResult.Success && feedResult.Items.Any())
                    {
                        var candidates = feedResult.Items
                            .Take(limit)
                            .Select(item =>
                            {
                                var candidate = CreateCandidateFromRssItem(item);
                                candidate.Tags.Add(category.ToLowerInvariant());
                                return candidate;
                            })
                            .ToList();
                        
                        stopwatch.Stop();
                        _logger?.LogInformation("ReadComicOnline category RSS {Category}: {Count} items", category, candidates.Count);
                        
                        return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
                    }
                }
                catch (HttpRequestException)
                {
                    // Try next feed path
                    continue;
                }
            }
            
            // RSS not available - fall back to HTML scraping
            _logger?.LogDebug("ReadComicOnline category RSS not available for {Category}, falling back to HTML scraping", category);
            stopwatch.Stop();
            return await GetCategoryAsync(category, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline category RSS {Category} fetch failed", category);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    /// <summary>
    /// Gets releases from a publisher via its RSS feed.
    /// Falls back to GetPublisherAsync if RSS is unavailable.
    /// </summary>
    /// <param name="publisher">Publisher name (e.g., "DC", "Marvel")</param>
    /// <param name="limit">Maximum number of items to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result containing candidates from the publisher RSS feed</returns>
    public async Task<DdlSearchResult> GetPublisherRssFeedAsync(string publisher, int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var publisherSlug = publisher.ToLowerInvariant() switch
            {
                "dc" or "dc comics" => "DC-Comics",
                "marvel" or "marvel comics" => "Marvel",
                "image" or "image comics" => "Image",
                "dark horse" or "darkhorse" => "Dark-Horse",
                "idw" or "idw publishing" => "IDW",
                "boom" or "boom studios" => "BOOM-Studios",
                "dynamite" => "Dynamite-Entertainment",
                "valiant" => "Valiant",
                _ => publisher.Replace(" ", "-")
            };
            
            // Try common publisher RSS feed patterns
            var feedPaths = new[]
            {
                $"/ComicList/Publisher/{publisherSlug}/feed/",
                $"/Publisher/{publisherSlug}/rss/",
                $"/cat/{publisherSlug.ToLowerInvariant()}/feed/"
            };
            
            foreach (var feedPath in feedPaths)
            {
                var feedUrl = $"{EffectiveBaseUrl}{feedPath}";
                _logger?.LogDebug("Trying ReadComicOnline publisher RSS feed: {Publisher} from {Url}", publisher, feedUrl);
                
                try
                {
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
                    
                    if (feedResult.Success && feedResult.Items.Any())
                    {
                        var candidates = feedResult.Items
                            .Take(limit)
                            .Select(item =>
                            {
                                var candidate = CreateCandidateFromRssItem(item);
                                candidate.Tags.Add(publisher.ToLowerInvariant());
                                return candidate;
                            })
                            .ToList();
                        
                        stopwatch.Stop();
                        _logger?.LogInformation("ReadComicOnline publisher RSS {Publisher}: {Count} items", publisher, candidates.Count);
                        
                        return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
                    }
                }
                catch (HttpRequestException)
                {
                    // Try next feed path
                    continue;
                }
            }
            
            // RSS not available - fall back to HTML scraping
            _logger?.LogDebug("ReadComicOnline publisher RSS not available for {Publisher}, falling back to HTML scraping", publisher);
            stopwatch.Stop();
            return await GetPublisherAsync(publisher, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "ReadComicOnline publisher RSS {Publisher} fetch failed", publisher);
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    private DdlCandidate CreateCandidateFromRssItem(RssFeedItem item)
    {
        var parsed = _parser.Parse(item.Title);
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = item.Title,
            SourceSite = SiteType,
            SourceUrl = item.Link,
            ParsedInfo = parsed,
            DateFound = item.PubDate ?? DateTime.UtcNow,
            Description = item.Description,
            QualityScore = parsed.Confidence
        };
        
        // Add RSS categories as tags
        foreach (var cat in item.Categories)
        {
            candidate.Tags.Add(cat.ToLowerInvariant());
        }
        
        return candidate;
    }

    /// <summary>
    /// Gets all available categories with their display names.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAvailableCategories()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Publishers
            { "dc-comics", "DC Comics" },
            { "marvel", "Marvel Comics" },
            { "image", "Image Comics" },
            { "dark-horse", "Dark Horse Comics" },
            { "idw", "IDW Publishing" },
            { "boom-studios", "BOOM! Studios" },
            { "dynamite-entertainment", "Dynamite Entertainment" },
            { "valiant", "Valiant Comics" },
            { "vertigo", "Vertigo Comics" },
            // Genres
            { "action", "Action" },
            { "adventure", "Adventure" },
            { "comedy", "Comedy" },
            { "crime", "Crime" },
            { "drama", "Drama" },
            { "fantasy", "Fantasy" },
            { "horror", "Horror" },
            { "mystery", "Mystery" },
            { "romance", "Romance" },
            { "sci-fi", "Science Fiction" },
            { "superhero", "Superhero" },
            { "thriller", "Thriller" }
        };
    }

    public override async Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Extracting download links from: {Url}", pageUrl);

            var html = await FetchPageAsync(pageUrl, cancellationToken);
            var links = ParseDownloadLinks(html, pageUrl);

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
        // ReadComicOnline uses /Search/Comic path
        var searchTerm = query.RawQuery;

        if (string.IsNullOrEmpty(searchTerm) && !string.IsNullOrEmpty(query.SeriesTitle))
        {
            searchTerm = query.SeriesTitle;

            // RCO search is by comic name, issue numbers are handled differently
            // Don't append issue number to search
        }

        if (string.IsNullOrEmpty(searchTerm))
        {
            return $"{EffectiveBaseUrl}/ComicList";
        }

        var encodedSearch = HttpUtility.UrlEncode(searchTerm);
        var url = $"{EffectiveBaseUrl}/Search/Comic?keyword={encodedSearch}";

        return url;
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // RCO requires browser-like headers
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
        
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Parses the search results page HTML to extract comic candidates.
    /// </summary>
    internal List<DdlCandidate> ParseSearchPage(string html)
    {
        var candidates = new List<DdlCandidate>();

        // Pattern 1: Comic list items with title and link
        // <a href="/Comic/Series-Name" title="Series Name">Text</a>
        var comicListMatches = ComicListItemRegex().Matches(html);

        foreach (Match match in comicListMatches)
        {
            var releaseUrl = match.Groups["url"].Value;
            // Prefer title attribute, fall back to link text
            var releaseTitle = match.Groups["title"].Success && !string.IsNullOrWhiteSpace(match.Groups["title"].Value)
                ? HttpUtility.HtmlDecode(match.Groups["title"].Value.Trim())
                : HttpUtility.HtmlDecode(match.Groups["text"].Value?.Trim());

            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                continue;
            }

            // Skip navigation links
            if (IsNavigationLink(releaseUrl))
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

        // Pattern 2: Table-based comic list (common on search results)
        var tableMatches = ComicTableRowRegex().Matches(html);

        foreach (Match match in tableMatches)
        {
            var releaseUrl = match.Groups["url"].Value;
            var releaseTitle = HttpUtility.HtmlDecode(match.Groups["title"].Value?.Trim());

            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                continue;
            }

            if (IsNavigationLink(releaseUrl))
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

        // Enrich with additional metadata if available
        EnrichCandidatesWithMetadata(candidates, html);

        return candidates;
    }

    /// <summary>
    /// Parses download/reading links from a comic page.
    /// ReadComicOnline primarily offers reading but may have download options.
    /// </summary>
    internal List<DdlDownloadLink> ParseDownloadLinks(string html, string sourceUrl)
    {
        var links = new List<DdlDownloadLink>();
        var priority = 0;

        // Pattern 1: Issue/chapter links within a comic series page
        var issueMatches = IssueListRegex().Matches(html);

        foreach (Match match in issueMatches)
        {
            var url = match.Groups["url"].Value;
            
            if (!url.StartsWith("http"))
            {
                url = url.StartsWith("/")
                    ? $"{EffectiveBaseUrl}{url}"
                    : $"{EffectiveBaseUrl}/{url}";
            }

            links.Add(new DdlDownloadLink
            {
                Url = url,
                HostName = "ReadComicOnline",
                Priority = priority++,
                IsVerified = true // Internal links are always valid
            });
        }

        // Pattern 2: Direct download links (if available)
        var downloadMatches = DownloadButtonRegex().Matches(html);

        foreach (Match match in downloadMatches)
        {
            var url = match.Groups["url"].Value;
            
            if (!url.StartsWith("http"))
            {
                url = url.StartsWith("/")
                    ? $"{EffectiveBaseUrl}{url}"
                    : $"{EffectiveBaseUrl}/{url}";
            }

            var hostName = ExtractHostName(url);
            links.Add(new DdlDownloadLink
            {
                Url = url,
                HostName = hostName,
                Priority = priority++,
                IsVerified = false
            });
        }

        // Pattern 3: External file host links
        var hostMatches = KnownHostLinkRegex().Matches(html);

        foreach (Match match in hostMatches)
        {
            var url = match.Groups["url"].Value;
            var hostName = ExtractHostName(url);

            // Avoid duplicates
            if (!links.Any(l => l.Url == url))
            {
                links.Add(new DdlDownloadLink
                {
                    Url = url,
                    HostName = hostName,
                    Priority = priority++,
                    IsVerified = false
                });
            }
        }

        return links.OrderBy(l => GetHostPriority(l.HostName ?? "")).ToList();
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

    private bool IsNavigationLink(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        return lowerUrl.Contains("/genre/") && !lowerUrl.Contains("/comic/")
            || lowerUrl.Contains("/comiclist")
            || lowerUrl.Contains("/search")
            || lowerUrl.EndsWith("/")
            || lowerUrl.Contains("page=")
            || lowerUrl.Contains("/login")
            || lowerUrl.Contains("/register");
    }

    private void EnrichCandidatesWithMetadata(List<DdlCandidate> candidates, string html)
    {
        // Try to extract last updated date, issue count, etc.
        foreach (var candidate in candidates)
        {
            // Look for status info near the comic entry
            var statusMatch = StatusInfoRegex().Match(html);
            if (statusMatch.Success)
            {
                var status = statusMatch.Groups["status"].Value.ToLowerInvariant();
                candidate.Tags.Add(status == "ongoing" ? "ongoing" : "completed");
            }
        }
    }

    private static string ExtractHostName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            // Remove www. prefix
            if (host.StartsWith("www."))
            {
                host = host[4..];
            }

            // Get the main domain name
            var parts = host.Split('.');
            return parts.Length >= 2 ? parts[^2] : host;
        }
        catch
        {
            return "unknown";
        }
    }

    private static int GetHostPriority(string hostName)
    {
        return hostName.ToLowerInvariant() switch
        {
            "readcomiconline" => 0, // Internal links first
            "mega" => 1,
            "mediafire" => 2,
            "pixeldrain" => 3,
            "1fichier" => 4,
            "drive" or "google" => 5,
            "dropbox" => 6,
            _ => 10
        };
    }

    #region Regex Patterns

    // Homepage redirect button pattern
    [GeneratedRegex(@"<a[^>]+href=[""'](?<url>https?://[^""']+)[""'][^>]*>.*?(?:Go\s+to\s+)?Homepage", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HomepageButtonRegex();

    // Comic list item: <a href="/Comic/Name" title="Name">Title</a> or <a href="/Comic/Name">Title</a>
    // More flexible pattern that handles both with and without title attribute
    [GeneratedRegex(@"<a[^>]*href=[""'](?<url>/Comic/[^""'/]+(?:/[^""']*)?)[""'][^>]*(?:title=[""'](?<title>[^""']+)[""'])?[^>]*>(?<text>[^<]*)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex ComicListItemRegex();

    // Table row comic link - simplified
    [GeneratedRegex(@"<td[^>]*>\s*<a[^>]*href=[""'](?<url>/Comic/[^""']+)[""'][^>]*>(?<title>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ComicTableRowRegex();

    // Issue list links within a comic page
    [GeneratedRegex(@"<a[^>]*href=[""'](?<url>/Comic/[^/]+/Issue-[^""']+)[""'][^>]*>(?<title>[^<]*)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex IssueListRegex();

    // Download button links
    [GeneratedRegex(@"<a[^>]+href=[""'](?<url>[^""']+)[""'][^>]*class=[""'][^""']*(?:download|btn-download)[^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadButtonRegex();

    // Known file host links
    [GeneratedRegex(@"href=[""'](?<url>https?://(?:mega\.(?:nz|co\.nz)|mediafire\.com|pixeldrain\.com|1fichier\.com|drive\.google\.com|dropbox\.com)[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex KnownHostLinkRegex();

    // Status info (ongoing/completed)
    [GeneratedRegex(@"Status[:\s]*(?<status>Ongoing|Completed)", RegexOptions.IgnoreCase)]
    private static partial Regex StatusInfoRegex();

    #endregion
}
