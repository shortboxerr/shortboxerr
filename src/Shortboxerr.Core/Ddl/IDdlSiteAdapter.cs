namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Adapter interface for DDL (Direct Download Link) sites.
/// Each supported site has its own implementation handling site-specific parsing.
/// </summary>
public interface IDdlSiteAdapter
{
    /// <summary>
    /// Unique identifier for this site type.
    /// </summary>
    string SiteType { get; }
    
    /// <summary>
    /// Display name for the site.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Base URL pattern for this site (can be overridden per instance).
    /// </summary>
    string DefaultBaseUrl { get; }
    
    /// <summary>
    /// Whether this site requires authentication.
    /// </summary>
    bool RequiresAuthentication { get; }
    
    /// <summary>
    /// Default rate limit (requests per minute) to avoid being blocked.
    /// </summary>
    int DefaultRateLimitPerMinute { get; }
    
    /// <summary>
    /// Search for releases matching the query.
    /// </summary>
    Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest releases from the site (RSS-like).
    /// </summary>
    Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract download links from a release page URL.
    /// </summary>
    Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify if a download link is still valid/alive.
    /// </summary>
    Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test connectivity and optionally authentication to the site.
    /// </summary>
    Task<DdlSiteTestResult> TestConnectionAsync(DdlSiteCredentials? credentials = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Configure the adapter with site-specific settings.
    /// </summary>
    void Configure(DdlSiteConfiguration configuration);
}

/// <summary>
/// Query parameters for searching a DDL site.
/// </summary>
public class DdlSearchQuery
{
    /// <summary>
    /// Series title to search for.
    /// </summary>
    public string? SeriesTitle { get; set; }
    
    /// <summary>
    /// Specific issue number (optional).
    /// </summary>
    public decimal? IssueNumber { get; set; }
    
    /// <summary>
    /// Volume number (optional).
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// Year to filter by (optional).
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Free-text search query.
    /// </summary>
    public string? RawQuery { get; set; }
    
    /// <summary>
    /// Whether to search for collections only.
    /// </summary>
    public bool CollectionsOnly { get; set; }
    
    /// <summary>
    /// Maximum results to return.
    /// </summary>
    public int Limit { get; set; } = 50;
    
    /// <summary>
    /// Offset for pagination.
    /// </summary>
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Result of a DDL site search operation.
/// </summary>
public class DdlSearchResult
{
    /// <summary>
    /// Found candidates.
    /// </summary>
    public IReadOnlyList<DdlCandidate> Candidates { get; init; } = Array.Empty<DdlCandidate>();
    
    /// <summary>
    /// Whether the search was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if search failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Total results available (for pagination).
    /// </summary>
    public int TotalResults { get; init; }
    
    /// <summary>
    /// Whether more results are available.
    /// </summary>
    public bool HasMore { get; init; }
    
    /// <summary>
    /// Time taken to execute the search.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Source site identifier.
    /// </summary>
    public string SourceSite { get; init; } = string.Empty;
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static DdlSearchResult Ok(IReadOnlyList<DdlCandidate> candidates, string sourceSite, int totalResults = -1, TimeSpan? duration = null)
    {
        return new DdlSearchResult
        {
            Success = true,
            Candidates = candidates,
            TotalResults = totalResults < 0 ? candidates.Count : totalResults,
            HasMore = totalResults > candidates.Count,
            SourceSite = sourceSite,
            Duration = duration ?? TimeSpan.Zero
        };
    }
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static DdlSearchResult Error(string message, string sourceSite, TimeSpan? duration = null)
    {
        return new DdlSearchResult
        {
            Success = false,
            ErrorMessage = message,
            SourceSite = sourceSite,
            Duration = duration ?? TimeSpan.Zero
        };
    }
}

/// <summary>
/// Credentials for authenticating with a DDL site.
/// </summary>
public class DdlSiteCredentials
{
    /// <summary>
    /// Username for login.
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Password for login.
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// API key (if site uses API auth).
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Session cookies (for pre-authenticated sessions).
    /// </summary>
    public Dictionary<string, string> Cookies { get; set; } = new();
}

/// <summary>
/// Configuration for a DDL site adapter instance.
/// </summary>
public class DdlSiteConfiguration
{
    /// <summary>
    /// Base URL override (if different from default).
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// Authentication credentials.
    /// </summary>
    public DdlSiteCredentials? Credentials { get; set; }
    
    /// <summary>
    /// Rate limit override (requests per minute).
    /// </summary>
    public int? RateLimitPerMinute { get; set; }
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Custom User-Agent string.
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Additional headers to include in requests.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
    
    /// <summary>
    /// Whether to follow redirects automatically.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;
}

/// <summary>
/// Result of testing a DDL site connection.
/// </summary>
public class DdlSiteTestResult
{
    /// <summary>
    /// Whether the connection test passed.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether authentication was successful (if required).
    /// </summary>
    public bool? AuthenticationPassed { get; init; }
    
    /// <summary>
    /// Number of sample results found during test.
    /// </summary>
    public int? SampleResultCount { get; init; }
    
    /// <summary>
    /// Response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; init; }
    
    /// <summary>
    /// Any warnings or issues detected.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Detailed error if test failed.
    /// </summary>
    public string? ErrorDetails { get; init; }
}

