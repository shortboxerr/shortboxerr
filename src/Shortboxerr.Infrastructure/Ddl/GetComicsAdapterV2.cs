using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Enhanced GetComics adapter with full Mylar3 parity.
/// Implements session cookies, FlareSolverr integration, multiple search formats,
/// pagination, HD/SD quality variants, link prioritization, and pack detection.
/// </summary>
public partial class GetComicsAdapterV2 : IDdlSiteAdapter
{
    private readonly DdlReleaseParser _parser = new();
    private readonly IRssFeedService? _rssFeedService;
    private readonly ICloudflareBypassService? _cloudflareService;
    private readonly IDdlCookieService? _cookieService;
    private readonly ILogger<GetComicsAdapterV2>? _logger;
    
    private GetComicsSettings _settings = new();
    private HttpClient? _httpClient;
    private CookieContainer? _cookieContainer;
    private DateTime _lastSearchTime = DateTime.MinValue;
    
    /// <summary>
    /// Default browser User-Agent matching Mylar3's Firefox agent.
    /// </summary>
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";
    
    /// <summary>
    /// Mylar3's search format patterns.
    /// Order: quoted exact, unquoted exact, no year, no issue.
    /// </summary>
    private static readonly string[] SearchFormats = 
    {
        "\"{0} #{1} ({2})\"",  // Quoted exact: "Batman #1 (2024)"
        "{0} #{1} ({2})",      // Unquoted exact: Batman #1 (2024)
        "{0} #{1}",            // No year: Batman #1
        "{0} {1}"              // Simple: Batman 1
    };
    
    /// <summary>
    /// Patterns indicating paywall/ad links that should be skipped.
    /// </summary>
    private static readonly string[] PaywallPatterns = { "sh.st", "adf.ly", "bc.vc", "ouo.io" };
    
    /// <summary>
    /// Text patterns indicating we hit an error/donation page instead of the actual download.
    /// </summary>
    private static readonly string[] ErrorPagePatterns = 
    { 
        "support and donation", 
        "cloudflare", 
        "just a moment",
        "checking your browser",
        "access denied"
    };
    
    public GetComicsAdapterV2(
        ILogger<GetComicsAdapterV2>? logger = null, 
        IRssFeedService? rssFeedService = null,
        ICloudflareBypassService? cloudflareService = null,
        IDdlCookieService? cookieService = null)
    {
        _logger = logger;
        _rssFeedService = rssFeedService;
        _cloudflareService = cloudflareService;
        _cookieService = cookieService;
    }
    
    #region IDdlSiteAdapter Implementation
    
    public string SiteType => "GetComics";
    public string DisplayName => "GetComics.org";
    public string DefaultBaseUrl => "https://getcomics.org";
    public bool RequiresAuthentication => false;
    public int DefaultRateLimitPerMinute => 10;
    
    public void Configure(DdlSiteConfiguration configuration)
    {
        _settings = new GetComicsSettings
        {
            BaseUrl = configuration.BaseUrl ?? DefaultBaseUrl,
            UserAgent = configuration.UserAgent,
            TimeoutSeconds = configuration.TimeoutSeconds > 0 ? configuration.TimeoutSeconds : 30
        };
        
        // Reset HTTP client to pick up new configuration
        _httpClient?.Dispose();
        _httpClient = null;
        _cookieContainer = null;
    }
    
    /// <summary>
    /// Configure with GetComics-specific settings.
    /// </summary>
    public void Configure(GetComicsSettings settings)
    {
        _settings = settings;
        _httpClient?.Dispose();
        _httpClient = null;
        _cookieContainer = null;
    }
    
    public async Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Load cookies for session persistence (like Mylar3's cookie_receipt)
            await LoadCookiesAsync(cancellationToken);
            
            var allCandidates = new List<DdlCandidate>();
            var seenUrls = new HashSet<string>();
            
            // Build search terms using Mylar3's format patterns
            var searchTerms = BuildSearchTerms(query);
            
            foreach (var searchTerm in searchTerms)
            {
                _logger?.LogDebug("[DDL-QUERY] Query set to: {Query}", searchTerm);
                
                // Search with pagination
                var (candidates, _) = await SearchWithPaginationAsync(searchTerm, seenUrls, cancellationToken);
                
                if (candidates.Count > 0)
                {
                    allCandidates.AddRange(candidates);
                    
                    // If we found results with the more specific format, use them
                    // (Mylar3 behavior: break on first match)
                    if (!_settings.PreferPacks || candidates.Any(c => !c.ParsedInfo.IsCollection))
                    {
                        _logger?.LogDebug("Found {Count} results with search term: {Term}", candidates.Count, searchTerm);
                        break;
                    }
                }
                
                // Rate limiting between search format attempts
                await ApplyQueryDelayAsync(cancellationToken);
            }
            
            // Sort by pack preference if enabled (Mylar3's PACK_PRIORITY)
            if (_settings.PreferPacks)
            {
                allCandidates = allCandidates
                    .OrderByDescending(c => c.ParsedInfo.IsCollection)
                    .ToList();
            }
            
            stopwatch.Stop();
            _logger?.LogInformation("GetComics search completed: {Count} results in {Duration}ms",
                allCandidates.Count, stopwatch.ElapsedMilliseconds);
            
            return DdlSearchResult.Ok(allCandidates, SiteType, allCandidates.Count, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex) when (IsCloudflareError(ex))
        {
            stopwatch.Stop();
            _logger?.LogWarning("Cloudflare block detected, attempting bypass");
            
            // Try with FlareSolverr if available
            if (_cloudflareService != null && _settings.UseFlareSolverr)
            {
                return await SearchWithCloudflareBypassAsync(query, stopwatch, cancellationToken);
            }
            
            return DdlSearchResult.Error($"Cloudflare block: {ex.Message}", SiteType, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "GetComics search error");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    public async Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await LoadCookiesAsync(cancellationToken);
            
            var html = await FetchPageAsync(_settings.BaseUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();
            
            stopwatch.Stop();
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "GetComics latest fetch failed");
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }
    
    public async Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Extracting download links from: {Url}", pageUrl);
            
            await LoadCookiesAsync(cancellationToken);
            var html = await FetchPageAsync(pageUrl, cancellationToken);
            
            // Check for error pages
            if (IsErrorPage(html))
            {
                _logger?.LogWarning("Detected error/donation page instead of release page");
                
                // Try FlareSolverr if available
                if (_cloudflareService != null && _settings.UseFlareSolverr)
                {
                    html = await FetchWithCloudflareBypassAsync(pageUrl, cancellationToken);
                    if (html == null || IsErrorPage(html))
                    {
                        return Array.Empty<DdlDownloadLink>();
                    }
                }
                else
                {
                    return Array.Empty<DdlDownloadLink>();
                }
            }
            
            var links = ParseDownloadLinksMylar3Style(html);
            
            _logger?.LogDebug("Found {Count} download links", links.Count);
            return links;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract links from {Url}", pageUrl);
            return Array.Empty<DdlDownloadLink>();
        }
    }
    
    public async Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        // Skip paywall links
        if (IsPaywallLink(downloadUrl))
        {
            return false;
        }
        
        try
        {
            var client = GetHttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<DdlSiteTestResult> TestConnectionAsync(DdlSiteCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var warnings = new List<string>();
        
        try
        {
            await LoadCookiesAsync(cancellationToken);
            
            var client = GetHttpClient();
            using var response = await client.GetAsync(_settings.BaseUrl, cancellationToken);
            
            stopwatch.Stop();
            
            if (!response.IsSuccessStatusCode)
            {
                return new DdlSiteTestResult
                {
                    Success = false,
                    Message = $"Site returned HTTP {(int)response.StatusCode}",
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            
            // Check for Cloudflare
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsErrorPage(content))
            {
                warnings.Add("Site may be behind Cloudflare protection");
            }
            
            return new DdlSiteTestResult
            {
                Success = true,
                Message = "Connection successful",
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new DdlSiteTestResult
            {
                Success = false,
                Message = "Connection failed",
                ErrorDetails = ex.Message,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    #endregion
    
    #region Search Methods
    
    /// <summary>
    /// Build search terms using Mylar3's format patterns.
    /// </summary>
    private List<string> BuildSearchTerms(DdlSearchQuery query)
    {
        var terms = new List<string>();
        
        // If raw query provided, use it directly
        if (!string.IsNullOrEmpty(query.RawQuery))
        {
            terms.Add(query.RawQuery);
            return terms;
        }
        
        var seriesTitle = query.SeriesTitle ?? "";
        var issueNumber = query.IssueNumber?.ToString("0.##") ?? "";
        var year = query.Year?.ToString() ?? "";
        
        // Clean up series title (Mylar3 removes special characters)
        seriesTitle = CleanSeriesTitle(seriesTitle);
        
        // If pack priority enabled, add pack search first (Mylar3 behavior)
        if (_settings.PreferPacks && !string.IsNullOrEmpty(year))
        {
            terms.Add($"{seriesTitle} {year}");
        }
        
        // Build terms using format patterns
        foreach (var format in SearchFormats)
        {
            var formatCount = format.Count(c => c == '{');
            
            string term;
            if (formatCount == 3 && !string.IsNullOrEmpty(issueNumber) && !string.IsNullOrEmpty(year))
            {
                term = string.Format(format, seriesTitle, issueNumber, year);
            }
            else if (formatCount == 2 && !string.IsNullOrEmpty(issueNumber))
            {
                term = string.Format(format, seriesTitle, issueNumber);
            }
            else if (formatCount == 1)
            {
                term = string.Format(format, seriesTitle);
            }
            else
            {
                continue;
            }
            
            if (!string.IsNullOrWhiteSpace(term) && !terms.Contains(term))
            {
                terms.Add(term);
            }
        }
        
        return terms;
    }
    
    private static string CleanSeriesTitle(string title)
    {
        // Mylar3 removes: & : ? , / -
        title = Regex.Replace(title, @"[\&\:\?\,\/\-]", "");
        // Remove "and" and "the" (Mylar3 behavior)
        title = Regex.Replace(title, @"\band\b", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\bthe\b", "", RegexOptions.IgnoreCase);
        // Normalize whitespace
        title = Regex.Replace(title, @"\s+", " ").Trim();
        return title;
    }
    
    /// <summary>
    /// Search with pagination following Mylar3's pattern.
    /// </summary>
    private async Task<(List<DdlCandidate> candidates, string? nextPageUrl)> SearchWithPaginationAsync(
        string searchTerm, 
        HashSet<string> seenUrls,
        CancellationToken cancellationToken)
    {
        var allCandidates = new List<DdlCandidate>();
        var currentUrl = BuildSearchUrl(searchTerm);
        var pagesSearched = 0;
        
        while (!string.IsNullOrEmpty(currentUrl) && pagesSearched < _settings.MaxSearchPages)
        {
            await ApplyQueryDelayAsync(cancellationToken);
            
            var html = await FetchPageAsync(currentUrl, cancellationToken);
            
            // Check for Cloudflare block
            if (IsErrorPage(html))
            {
                _logger?.LogWarning("Search request returned error page (possible Cloudflare block)");
                break;
            }
            
            var (candidates, nextUrl, totalPages) = ParseSearchResultPage(html);
            
            _logger?.LogInformation("Found {Count} results on page {Page} (of {Total})", 
                candidates.Count, pagesSearched + 1, totalPages ?? pagesSearched + 1);
            
            foreach (var candidate in candidates)
            {
                // Skip already seen URLs
                if (candidate.SourceUrl != null && !seenUrls.Add(candidate.SourceUrl))
                {
                    continue;
                }
                
                // Skip weekly packs unless searching for packs
                if (!_settings.PreferPacks && IsWeeklyPack(candidate.ReleaseTitle))
                {
                    continue;
                }
                
                allCandidates.Add(candidate);
            }
            
            currentUrl = nextUrl;
            pagesSearched++;
        }
        
        return (allCandidates, null);
    }
    
    private async Task<DdlSearchResult> SearchWithCloudflareBypassAsync(
        DdlSearchQuery query, 
        System.Diagnostics.Stopwatch stopwatch, 
        CancellationToken cancellationToken)
    {
        if (_cloudflareService == null)
        {
            return DdlSearchResult.Error("Cloudflare bypass service not available", SiteType, stopwatch.Elapsed);
        }
        
        var searchUrl = BuildSearchUrl(query.RawQuery ?? query.SeriesTitle ?? "");
        var bypassResult = await _cloudflareService.BypassAsync(searchUrl, new CloudflareBypassOptions
        {
            ReturnHtmlContent = true,
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        }, cancellationToken);
        
        if (!bypassResult.Success || string.IsNullOrEmpty(bypassResult.HtmlContent))
        {
            stopwatch.Stop();
            return DdlSearchResult.Error($"Cloudflare bypass failed: {bypassResult.ErrorMessage}", SiteType, stopwatch.Elapsed);
        }
        
        // Save cookies from bypass for future requests
        if (bypassResult.Session != null && _cookieService != null)
        {
            await _cookieService.SaveCookiesAsync(SiteType, bypassResult.Session.Cookies, cancellationToken);
        }
        
        var candidates = ParseSearchPage(bypassResult.HtmlContent);
        
        stopwatch.Stop();
        return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
    }
    
    #endregion
    
    #region Link Extraction (Mylar3 Style)
    
    /// <summary>
    /// Parse download links using Mylar3's exact algorithm.
    /// Handles HD/SD variants, multiple link sections, and link type detection.
    /// </summary>
    private List<DdlDownloadLink> ParseDownloadLinksMylar3Style(string html)
    {
        var links = new List<DdlDownloadLink>();
        var validLinks = new Dictionary<string, GetComicsLinkSection>();
        
        // Find all download link sections (text-align: center paragraphs with download buttons)
        var sections = ParseLinkSections(html);
        
        foreach (var section in sections)
        {
            validLinks[section.QualityVariant] = section;
        }
        
        // Build final link list with prioritization
        var orderedLinks = SelectLinksWithPriority(validLinks);
        
        // Convert to DdlDownloadLink with proper ordering
        var priority = 0;
        foreach (var link in orderedLinks)
        {
            // Skip paywall links
            if (IsPaywallLink(link.Url))
            {
                _logger?.LogDebug("[Paywall-link detected] Skipping {Url}", link.Url);
                continue;
            }
            
            links.Add(new DdlDownloadLink
            {
                Url = link.Url,
                LinkType = MapLinkType(link.LinkType),
                HostName = GetHostNameFromLinkType(link.LinkType),
                Priority = priority++
            });
        }
        
        return links;
    }
    
    /// <summary>
    /// Parse link sections from HTML, identifying HD/SD variants.
    /// </summary>
    private List<GetComicsLinkSection> ParseLinkSections(string html)
    {
        var sections = new List<GetComicsLinkSection>();
        
        // Pattern for download link buttons: <a class="aio-pulse" href="url" title="Link Type">
        var buttonMatches = DownloadButtonMylar3Regex().Matches(html);
        
        var currentSection = new GetComicsLinkSection { QualityVariant = "normal" };
        var currentQuality = "normal";
        
        foreach (Match match in buttonMatches)
        {
            var url = HttpUtility.HtmlDecode(match.Groups["url"].Value);
            var title = match.Groups["title"].Value.ToLowerInvariant();
            
            // Detect quality variant from surrounding context
            var contextStart = Math.Max(0, match.Index - 500);
            var context = html.Substring(contextStart, Math.Min(600, html.Length - contextStart)).ToLowerInvariant();
            
            if (context.Contains("hd-upscaled") || context.Contains("hd upscaled"))
            {
                currentQuality = "HD-Upscaled";
            }
            else if (context.Contains("sd-digital") || context.Contains("sd digital"))
            {
                currentQuality = "SD-Digital";
            }
            else if (context.Contains("hd-digital") || context.Contains("hd digital"))
            {
                currentQuality = "HD-Digital";
            }
            
            // Determine link type from title
            var linkType = DetermineLinkTypeFromTitle(title);
            
            var linkInfo = new GetComicsLink
            {
                Url = url,
                LinkType = linkType,
                QualityVariant = ParseQualityVariant(currentQuality)
            };
            
            // Find or create section for this quality
            var section = sections.FirstOrDefault(s => s.QualityVariant == currentQuality);
            if (section == null)
            {
                section = new GetComicsLinkSection { QualityVariant = currentQuality };
                sections.Add(section);
            }
            section.Links.Add(linkInfo);
        }
        
        // Also try the alternate pattern for direct links to known file hosts
        var directMatches = KnownHostLinkMylar3Regex().Matches(html);
        foreach (Match match in directMatches)
        {
            var url = HttpUtility.HtmlDecode(match.Groups["url"].Value);
            var host = match.Groups["host"].Value.ToLowerInvariant();
            
            var linkType = DetectLinkTypeFromHost(host);
            
            if (linkType != GetComicsLinkType.Unknown)
            {
                AddLinkToSection(sections, url, linkType);
            }
        }
        
        // Also try GetComics redirect links (getcomics.org/dlds/ pattern)
        var redirectMatches = GetComicsRedirectLinkRegex().Matches(html);
        foreach (Match match in redirectMatches)
        {
            var url = HttpUtility.HtmlDecode(match.Groups["url"].Value);
            // These are encrypted redirects - treat as Main type
            AddLinkToSection(sections, url, GetComicsLinkType.Main);
            _logger?.LogDebug("Found GetComics redirect link: {Url}", url);
        }
        
        return sections;
    }
    
    private static GetComicsLinkType DetectLinkTypeFromHost(string host)
    {
        return host switch
        {
            var h when h.Contains("mega") => GetComicsLinkType.Mega,
            var h when h.Contains("mediafire") => GetComicsLinkType.MediaFire,
            var h when h.Contains("pixeldrain") => GetComicsLinkType.Pixeldrain,
            var h when h.Contains("terabox") => GetComicsLinkType.Terabox,
            var h when h.Contains("rootz") => GetComicsLinkType.Rootz,
            var h when h.Contains("vikingfile") => GetComicsLinkType.VikingFile,
            var h when h.Contains("zippyshare") => GetComicsLinkType.Zippyshare,
            _ => GetComicsLinkType.Other  // Return Other instead of Unknown to include it
        };
    }
    
    private static void AddLinkToSection(List<GetComicsLinkSection> sections, string url, GetComicsLinkType linkType)
    {
        var section = sections.FirstOrDefault(s => s.QualityVariant == "normal") 
            ?? new GetComicsLinkSection { QualityVariant = "normal" };
        
        if (!sections.Contains(section))
        {
            sections.Add(section);
        }
        
        if (!section.Links.Any(l => l.Url == url))
        {
            section.Links.Add(new GetComicsLink
            {
                Url = url,
                LinkType = linkType,
                QualityVariant = GetComicsQualityVariant.Normal
            });
        }
    }
    
    /// <summary>
    /// Select links based on quality preference and link priority (Mylar3's algorithm).
    /// </summary>
    private List<GetComicsLink> SelectLinksWithPriority(Dictionary<string, GetComicsLinkSection> validLinks)
    {
        var result = new List<GetComicsLink>();
        
        // Process each link type in priority order
        foreach (var linkPref in _settings.LinkPriority)
        {
            var targetLinkType = linkPref.ToLowerInvariant() switch
            {
                "mega" => GetComicsLinkType.Mega,
                "pixeldrain" => GetComicsLinkType.Pixeldrain,
                "mediafire" => GetComicsLinkType.MediaFire,
                "main" => GetComicsLinkType.Main,
                "mirror" => GetComicsLinkType.Mirror,
                "terabox" => GetComicsLinkType.Terabox,
                "rootz" => GetComicsLinkType.Rootz,
                "vikingfile" => GetComicsLinkType.VikingFile,
                "zippyshare" => GetComicsLinkType.Zippyshare,
                "other" => GetComicsLinkType.Other,
                _ => GetComicsLinkType.Unknown
            };
            
            if (targetLinkType == GetComicsLinkType.Unknown)
            {
                continue;
            }
            
            // Check quality preferences in order
            foreach (var qualityPref in _settings.QualityPreference)
            {
                var sectionKey = qualityPref.ToLowerInvariant() switch
                {
                    "sd-digital" => "SD-Digital",
                    "hd-digital" => "HD-Digital",
                    "hd-upscaled" => "HD-Upscaled",
                    _ => "normal"
                };
                
                if (validLinks.TryGetValue(sectionKey, out var section))
                {
                    var matchingLinks = section.Links.Where(l => l.LinkType == targetLinkType).ToList();
                    if (matchingLinks.Any())
                    {
                        result.AddRange(matchingLinks);
                    }
                }
            }
            
            // Also check "normal" section if not already covered
            if (validLinks.TryGetValue("normal", out var normalSection))
            {
                var matchingLinks = normalSection.Links
                    .Where(l => l.LinkType == targetLinkType && !result.Any(r => r.Url == l.Url))
                    .ToList();
                result.AddRange(matchingLinks);
            }
        }
        
        // Add any remaining links not yet added
        foreach (var section in validLinks.Values)
        {
            foreach (var link in section.Links)
            {
                if (!result.Any(r => r.Url == link.Url))
                {
                    result.Add(link);
                }
            }
        }
        
        return result;
    }
    
    private static GetComicsLinkType DetermineLinkTypeFromTitle(string title)
    {
        title = title.ToLowerInvariant();
        
        if (title.Contains("main server") || title.Contains("download now"))
        {
            return GetComicsLinkType.Main;
        }
        if (title.Contains("mirror"))
        {
            return GetComicsLinkType.Mirror;
        }
        if (title.Contains("mega"))
        {
            return GetComicsLinkType.Mega;
        }
        if (title.Contains("mediafire"))
        {
            return GetComicsLinkType.MediaFire;
        }
        if (title.Contains("pixel") || title.Contains("pixeldrain"))
        {
            return GetComicsLinkType.Pixeldrain;
        }
        
        return GetComicsLinkType.Unknown;
    }
    
    private static GetComicsQualityVariant ParseQualityVariant(string variant)
    {
        return variant.ToUpperInvariant() switch
        {
            "SD-DIGITAL" => GetComicsQualityVariant.SdDigital,
            "HD-DIGITAL" => GetComicsQualityVariant.HdDigital,
            "HD-UPSCALED" => GetComicsQualityVariant.HdUpscaled,
            _ => GetComicsQualityVariant.Normal
        };
    }
    
    private static DdlLinkType MapLinkType(GetComicsLinkType gcType)
    {
        return gcType switch
        {
            GetComicsLinkType.Main => DdlLinkType.Direct,
            GetComicsLinkType.Mirror => DdlLinkType.Direct,
            GetComicsLinkType.Mega or GetComicsLinkType.MediaFire or GetComicsLinkType.Pixeldrain => DdlLinkType.Hoster,
            _ => DdlLinkType.Redirect
        };
    }
    
    private static string? GetHostNameFromLinkType(GetComicsLinkType linkType)
    {
        return linkType switch
        {
            GetComicsLinkType.Main => "getcomics.org",
            GetComicsLinkType.Mirror => "getcomics.org",
            GetComicsLinkType.Mega => "mega.nz",
            GetComicsLinkType.MediaFire => "mediafire.com",
            GetComicsLinkType.Pixeldrain => "pixeldrain.com",
            _ => null
        };
    }
    
    #endregion
    
    #region Page Parsing
    
    private List<DdlCandidate> ParseSearchPage(string html)
    {
        var (candidates, _, _) = ParseSearchResultPage(html);
        return candidates;
    }
    
    /// <summary>
    /// Parse search results page with pagination info.
    /// Based on Mylar3's parse_search_result method.
    /// </summary>
    private (List<DdlCandidate> candidates, string? nextPageUrl, int? totalPages) ParseSearchResultPage(string html)
    {
        var candidates = new List<DdlCandidate>();
        string? nextPageUrl = null;
        int? totalPages = null;
        
        // Parse pagination
        var pageListMatch = PageListRegex().Match(html);
        if (pageListMatch.Success)
        {
            var pageNumbers = PageNumberRegex().Matches(pageListMatch.Value);
            if (pageNumbers.Count > 0)
            {
                var lastPage = pageNumbers[^1].Groups["num"].Value;
                if (int.TryParse(lastPage, out var total))
                {
                    totalPages = total;
                }
            }
            
            // Find next page link
            var nextMatch = NextPageRegex().Match(pageListMatch.Value);
            if (nextMatch.Success)
            {
                nextPageUrl = HttpUtility.HtmlDecode(nextMatch.Groups["url"].Value);
            }
        }
        
        // Parse article entries (Mylar3 pattern)
        var articleMatches = ArticleRegex().Matches(html);
        
        foreach (Match match in articleMatches)
        {
            var id = match.Groups["id"].Value;
            var link = HttpUtility.HtmlDecode(match.Groups["url"].Value);
            var title = HttpUtility.HtmlDecode(match.Groups["title"].Value).Trim();
            
            // Clean up title (Mylar3: replace unicode dash)
            title = title.Replace('\u2013', '-');
            
            if (string.IsNullOrWhiteSpace(title) || IsNavigationOrCategoryLink(title, link))
            {
                continue;
            }
            
            var parsed = _parser.Parse(title);
            var candidate = new DdlCandidate
            {
                Id = id,
                ReleaseTitle = title,
                SourceSite = SiteType,
                SourceUrl = EnsureAbsoluteUrl(link),
                ParsedInfo = parsed,
                DateFound = DateTime.UtcNow,
                QualityScore = parsed.Confidence
            };
            
            candidates.Add(candidate);
        }
        
        return (candidates, nextPageUrl, totalPages);
    }
    
    #endregion
    
    #region Helper Methods
    
    private string BuildSearchUrl(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return _settings.BaseUrl;
        }
        
        var encodedSearch = HttpUtility.UrlEncode(searchTerm);
        return $"{_settings.BaseUrl}/?s={encodedSearch}";
    }
    
    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        var client = GetHttpClient();
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
    
    private async Task<string?> FetchWithCloudflareBypassAsync(string url, CancellationToken cancellationToken)
    {
        if (_cloudflareService == null)
        {
            return null;
        }
        
        var result = await _cloudflareService.BypassAsync(url, new CloudflareBypassOptions
        {
            ReturnHtmlContent = true,
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        }, cancellationToken);
        
        if (result.Success && result.Session != null && _cookieService != null)
        {
            await _cookieService.SaveCookiesAsync(SiteType, result.Session.Cookies, cancellationToken);
        }
        
        return result.HtmlContent;
    }
    
    private HttpClient GetHttpClient()
    {
        if (_httpClient == null)
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                AllowAutoRedirect = true,
                UseCookies = true
            };
            
            // Configure proxy if set
            if (!string.IsNullOrEmpty(_settings.HttpProxy))
            {
                handler.Proxy = new WebProxy(_settings.HttpProxy);
                handler.UseProxy = true;
            }
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
            };
            
            // Set Mylar3-compatible headers
            var userAgent = _settings.UserAgent ?? DefaultUserAgent;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
        }
        
        return _httpClient;
    }
    
    private async Task LoadCookiesAsync(CancellationToken cancellationToken)
    {
        if (_cookieService == null || _cookieContainer == null)
        {
            return;
        }
        
        try
        {
            var cookies = await _cookieService.GetCookiesAsync(SiteType, cancellationToken);
            var baseUri = new Uri(_settings.BaseUrl);
            
            foreach (var (name, value) in cookies)
            {
                _cookieContainer.Add(baseUri, new Cookie(name, value));
            }
            
            if (cookies.Count > 0)
            {
                _logger?.LogDebug("[GC_Cookie_Loader] Successfully loaded {Count} cookies from file", cookies.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[GC_Cookie_Loader] Unable to load cookies from file");
        }
    }
    
    private async Task ApplyQueryDelayAsync(CancellationToken cancellationToken)
    {
        var timeSinceLastSearch = DateTime.UtcNow - _lastSearchTime;
        var requiredDelay = TimeSpan.FromSeconds(_settings.QueryDelaySeconds);
        
        if (timeSinceLastSearch < requiredDelay)
        {
            var delay = requiredDelay - timeSinceLastSearch;
            _logger?.LogDebug("[PROVIDER-SEARCH-DELAY][DDL] Waiting {Seconds:F1} seconds before next request", delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }
        
        _lastSearchTime = DateTime.UtcNow;
    }
    
    private static bool IsPaywallLink(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        return PaywallPatterns.Any(p => lowerUrl.Contains(p));
    }
    
    private static bool IsErrorPage(string html)
    {
        var lowerHtml = html.ToLowerInvariant();
        return ErrorPagePatterns.Any(p => lowerHtml.Contains(p));
    }
    
    private static bool IsCloudflareError(HttpRequestException ex)
    {
        return ex.StatusCode == HttpStatusCode.Forbidden || 
               ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
               ex.Message.Contains("cloudflare", StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsWeeklyPack(string title)
    {
        var patterns = new[] { "Marvel Week+", "INDIE Week+", "Image Week", "DC Week+" };
        return patterns.Any(p => title.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
    
    private static bool IsNavigationOrCategoryLink(string title, string url)
    {
        var lowerTitle = title.ToLowerInvariant();
        var lowerUrl = url.ToLowerInvariant();
        
        if (lowerUrl.Contains("/category/") || lowerUrl.Contains("/tag/"))
        {
            return true;
        }
        
        var navTerms = new[] { "home", "contact", "about", "privacy", "dmca", "request" };
        return navTerms.Any(t => lowerTitle == t || lowerUrl.EndsWith($"/{t}") || lowerUrl.EndsWith($"/{t}/"));
    }
    
    private string EnsureAbsoluteUrl(string url)
    {
        if (url.StartsWith("http"))
        {
            return url;
        }
        
        return url.StartsWith("/") 
            ? $"{_settings.BaseUrl}{url}" 
            : $"{_settings.BaseUrl}/{url}";
    }
    
    #endregion
    
    #region Regex Patterns
    
    /// <summary>
    /// Matches article elements with id, link, and title.
    /// </summary>
    [GeneratedRegex(@"<article[^>]*id=[""'](?<id>[^""']+)[""'][^>]*>.*?<h1[^>]*class=[""'][^""']*post-title[^""']*[""'][^>]*>\s*<a[^>]*href=[""'](?<url>[^""']+)[""'][^>]*>(?<title>[^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ArticleRegex();
    
    /// <summary>
    /// Matches pagination list.
    /// </summary>
    [GeneratedRegex(@"<ul[^>]*class=[""'][^""']*page-numbers[^""']*[""'][^>]*>.*?</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PageListRegex();
    
    /// <summary>
    /// Matches page numbers in pagination.
    /// </summary>
    [GeneratedRegex(@">(?<num>\d+)<", RegexOptions.IgnoreCase)]
    private static partial Regex PageNumberRegex();
    
    /// <summary>
    /// Matches next page link.
    /// </summary>
    [GeneratedRegex(@"<a[^>]*class=[""'][^""']*next[^""']*[""'][^>]*href=[""'](?<url>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex NextPageRegex();
    
    /// <summary>
    /// Matches download buttons (aio-pulse class) with title attribute.
    /// </summary>
    [GeneratedRegex(@"<a[^>]*class=[""'][^""']*aio-pulse[^""']*[""'][^>]*href=[""'](?<url>[^""']+)[""'][^>]*title=[""'](?<title>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadButtonMylar3Regex();
    
    /// <summary>
    /// Matches links to known file hosts (expanded list based on actual GetComics pages).
    /// Includes: mega, mediafire, pixeldrain, terabox, rootz, vikingfile, zippyshare, etc.
    /// </summary>
    [GeneratedRegex(@"href=[""'](?<url>https?://(?:www\.)?(?:[\w-]+\.)?(?<host>mega\.nz|mega\.co\.nz|mediafire\.com|pixeldrain\.com|terabox\.com|1024terabox\.com|rootz\.so|vikingfile\.com|zippyshare\.com|userscloud\.com|uploadhaven\.com|racaty\.io|racaty\.net|dropapk\.to|katfile\.com|nitroflare\.com|turbobit\.net|uploaded\.net|rapidgator\.net|uploadgig\.com)[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex KnownHostLinkMylar3Regex();
    
    /// <summary>
    /// Matches GetComics encrypted redirect links (getcomics.org/dlds/ pattern).
    /// These are internal redirects that eventually lead to file hosts.
    /// </summary>
    [GeneratedRegex(@"href=[""'](?<url>https?://getcomics\.(?:org|info)/dlds/[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex GetComicsRedirectLinkRegex();
    
    #endregion
    
    #region Helper Classes
    
    private class GetComicsLinkSection
    {
        public string QualityVariant { get; set; } = "normal";
        public string? Series { get; set; }
        public string? Year { get; set; }
        public string? Size { get; set; }
        public List<GetComicsLink> Links { get; } = new();
    }
    
    #endregion
}
