using System.Text.RegularExpressions;
using System.Web;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// DDL site adapter for GettyComics-style sites.
/// Handles HTML parsing for comic DDL pages.
/// This is a sample implementation showing the adapter pattern.
/// </summary>
public partial class GettyComicsSiteAdapter : BaseDdlSiteAdapter
{
    public override string SiteType => "GettyComics";
    public override string DisplayName => "Getty Comics";
    public override string DefaultBaseUrl => "https://gettycomics.example.com";
    public override bool RequiresAuthentication => false;
    public override int DefaultRateLimitPerMinute => 10; // Mylar3 default
    
    private readonly DdlReleaseParser _parser = new();

    public override async Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var searchUrl = BuildSearchUrl(query);
            var html = await FetchPageAsync(searchUrl, cancellationToken);
            var candidates = ParseSearchPage(html);
            
            stopwatch.Stop();
            
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    public override async Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var latestUrl = $"{EffectiveBaseUrl}/latest";
            var html = await FetchPageAsync(latestUrl, cancellationToken);
            var candidates = ParseSearchPage(html).Take(limit).ToList();
            
            stopwatch.Stop();
            
            return DdlSearchResult.Ok(candidates, SiteType, candidates.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed);
        }
    }

    public override async Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await FetchPageAsync(pageUrl, cancellationToken);
            return ParseDownloadLinks(html);
        }
        catch
        {
            return Array.Empty<DdlDownloadLink>();
        }
    }

    protected override string BuildSearchUrl(DdlSearchQuery query)
    {
        var baseUrl = $"{EffectiveBaseUrl}/search";
        var queryParams = new List<string>();
        
        if (!string.IsNullOrEmpty(query.SeriesTitle))
        {
            queryParams.Add($"q={HttpUtility.UrlEncode(query.SeriesTitle)}");
        }
        else if (!string.IsNullOrEmpty(query.RawQuery))
        {
            queryParams.Add($"q={HttpUtility.UrlEncode(query.RawQuery)}");
        }
        
        if (query.Year.HasValue)
        {
            queryParams.Add($"year={query.Year}");
        }
        
        if (query.CollectionsOnly)
        {
            queryParams.Add("type=tpb");
        }
        
        queryParams.Add($"limit={query.Limit}");
        queryParams.Add($"offset={query.Offset}");
        
        return queryParams.Count > 0 
            ? $"{baseUrl}?{string.Join("&", queryParams)}" 
            : baseUrl;
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private List<DdlCandidate> ParseSearchPage(string html)
    {
        var candidates = new List<DdlCandidate>();
        
        // Parse release links from HTML
        // Pattern: <a href="/release/..." class="release-link">Release Title</a>
        var linkMatches = ReleaseLinkRegex().Matches(html);
        
        foreach (Match match in linkMatches)
        {
            var releaseUrl = match.Groups["url"].Value;
            var releaseTitle = HttpUtility.HtmlDecode(match.Groups["title"].Value);
            
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                continue;
            }
            
            var parsed = _parser.Parse(releaseTitle);
            
            candidates.Add(new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = releaseTitle,
                SourceSite = SiteType,
                SourceUrl = releaseUrl.StartsWith("http") ? releaseUrl : $"{EffectiveBaseUrl}{releaseUrl}",
                ParsedInfo = parsed,
                DateFound = DateTime.UtcNow,
                QualityScore = parsed.Confidence
            });
        }
        
        // Also try alternative pattern for different HTML structures
        var altMatches = AlternativeReleaseLinkRegex().Matches(html);
        
        foreach (Match match in altMatches)
        {
            var releaseTitle = HttpUtility.HtmlDecode(match.Groups["title"].Value);
            
            if (string.IsNullOrWhiteSpace(releaseTitle) || 
                candidates.Any(c => c.ReleaseTitle == releaseTitle))
            {
                continue;
            }
            
            var parsed = _parser.Parse(releaseTitle);
            
            candidates.Add(new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = releaseTitle,
                SourceSite = SiteType,
                SourceUrl = EffectiveBaseUrl,
                ParsedInfo = parsed,
                DateFound = DateTime.UtcNow,
                QualityScore = parsed.Confidence
            });
        }
        
        return candidates;
    }

    private List<DdlDownloadLink> ParseDownloadLinks(string html)
    {
        var links = new List<DdlDownloadLink>();
        var priority = 0;
        
        // Parse download links
        // Pattern: <a href="https://..." class="download-link">Download</a>
        var downloadMatches = DownloadLinkRegex().Matches(html);
        
        foreach (Match match in downloadMatches)
        {
            var url = match.Groups["url"].Value;
            
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }
            
            var linkType = DetermineLinkType(url);
            var hostName = ExtractHostName(url);
            
            links.Add(new DdlDownloadLink
            {
                Url = url,
                LinkType = linkType,
                HostName = hostName,
                Priority = priority++
            });
        }
        
        // Also check for direct links in common patterns
        var directMatches = DirectLinkRegex().Matches(html);
        
        foreach (Match match in directMatches)
        {
            var url = match.Groups["url"].Value;
            
            if (string.IsNullOrWhiteSpace(url) || links.Any(l => l.Url == url))
            {
                continue;
            }
            
            links.Add(new DdlDownloadLink
            {
                Url = url,
                LinkType = DdlLinkType.Direct,
                Priority = priority++
            });
        }
        
        return links;
    }

    private static DdlLinkType DetermineLinkType(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        
        // Check for known file hosters
        if (lowerUrl.Contains("mega.") || 
            lowerUrl.Contains("mediafire.") ||
            lowerUrl.Contains("zippyshare.") ||
            lowerUrl.Contains("1fichier."))
        {
            return DdlLinkType.Hoster;
        }
        
        // Check for redirectors
        if (lowerUrl.Contains("bit.ly") ||
            lowerUrl.Contains("goo.gl") ||
            lowerUrl.Contains("tinyurl."))
        {
            return DdlLinkType.Redirect;
        }
        
        // Check for direct file extensions
        if (lowerUrl.EndsWith(".cbz") ||
            lowerUrl.EndsWith(".cbr") ||
            lowerUrl.EndsWith(".zip") ||
            lowerUrl.EndsWith(".rar"))
        {
            return DdlLinkType.Direct;
        }
        
        return DdlLinkType.Redirect; // Default to redirect
    }

    private static string? ExtractHostName(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    // Regex patterns for HTML parsing
    [GeneratedRegex(@"<a[^>]*href=[""'](?<url>[^""']*)[""'][^>]*class=[""'][^""']*release-link[^""']*[""'][^>]*>(?<title>[^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseLinkRegex();
    
    [GeneratedRegex(@"<div[^>]*class=[""'][^""']*release-title[^""']*[""'][^>]*>(?<title>[^<]+)</div>", RegexOptions.IgnoreCase)]
    private static partial Regex AlternativeReleaseLinkRegex();
    
    [GeneratedRegex(@"<a[^>]*href=[""'](?<url>https?://[^""']+)[""'][^>]*class=[""'][^""']*download[^""']*[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadLinkRegex();
    
    [GeneratedRegex(@"href=[""'](?<url>https?://[^""']+\.(?:cbz|cbr|zip|rar))[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DirectLinkRegex();
}

