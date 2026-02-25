using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Base class for DDL site adapters providing common functionality.
/// </summary>
public abstract class BaseDdlSiteAdapter : IDdlSiteAdapter
{
    private DdlSiteConfiguration _configuration = new();
    private HttpClient? _httpClient;
    
    /// <summary>
    /// Default browser User-Agent to avoid anti-bot detection.
    /// Matches Mylar3's Firefox user agent for compatibility.
    /// </summary>
    protected const string DefaultBrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0";
    
    public abstract string SiteType { get; }
    public abstract string DisplayName { get; }
    public abstract string DefaultBaseUrl { get; }
    public virtual bool RequiresAuthentication => false;
    public virtual int DefaultRateLimitPerMinute => 10;
    
    /// <summary>
    /// Gets the effective base URL (configured or default).
    /// </summary>
    protected string EffectiveBaseUrl => _configuration.BaseUrl ?? DefaultBaseUrl;
    
    /// <summary>
    /// Gets the HTTP client for making requests.
    /// </summary>
    protected HttpClient HttpClient => _httpClient ??= CreateHttpClient();

    public abstract Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    public abstract Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default);
    
    public abstract Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default);
    
    public virtual async Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public virtual async Task<DdlSiteTestResult> TestConnectionAsync(DdlSiteCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var warnings = new List<string>();
        
        try
        {
            // Test basic connectivity
            using var request = new HttpRequestMessage(HttpMethod.Get, EffectiveBaseUrl);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
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
            
            // If auth is required but no credentials provided
            if (RequiresAuthentication && credentials == null)
            {
                warnings.Add("Authentication required but no credentials provided");
            }
            
            // Try a sample search
            var searchResult = await SearchAsync(new DdlSearchQuery { Limit = 5, RawQuery = "test" }, cancellationToken);
            
            return new DdlSiteTestResult
            {
                Success = true,
                Message = "Connection successful",
                AuthenticationPassed = RequiresAuthentication ? credentials != null : null,
                SampleResultCount = searchResult.Candidates.Count,
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

    public virtual void Configure(DdlSiteConfiguration configuration)
    {
        _configuration = configuration;
        
        // Reset HTTP client to pick up new configuration
        _httpClient?.Dispose();
        _httpClient = null;
    }

    /// <summary>
    /// Creates a configured HTTP client.
    /// </summary>
    protected virtual HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = _configuration.FollowRedirects
        };
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds > 0 ? _configuration.TimeoutSeconds : 30)
        };
        
        // Use browser-like User-Agent to avoid anti-bot detection (matches Mylar3 behavior)
        // Default to Firefox user agent like Mylar3 does
        var userAgent = _configuration.UserAgent ?? DefaultBrowserUserAgent;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        
        // Add browser-like headers to avoid detection
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
        
        // Set Referer to the site's base URL (like Mylar3 does)
        if (!string.IsNullOrEmpty(_configuration.BaseUrl))
        {
            client.DefaultRequestHeaders.Referrer = new Uri(_configuration.BaseUrl);
        }
        
        // Add custom headers
        foreach (var (key, value) in _configuration.CustomHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }
        
        return client;
    }

    /// <summary>
    /// Builds a search URL from the query.
    /// </summary>
    protected virtual string BuildSearchUrl(DdlSearchQuery query)
    {
        // Override in derived classes for site-specific URL building
        return EffectiveBaseUrl;
    }

    /// <summary>
    /// Parses search results from HTML content.
    /// </summary>
    protected virtual IReadOnlyList<DdlCandidate> ParseSearchResults(string html, string sourceSite)
    {
        // Override in derived classes for site-specific HTML parsing
        return Array.Empty<DdlCandidate>();
    }
}



