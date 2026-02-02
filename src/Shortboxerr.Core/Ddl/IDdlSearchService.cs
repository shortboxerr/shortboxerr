namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for searching across multiple DDL sites.
/// Coordinates site adapters, applies rate limiting, and aggregates results.
/// </summary>
public interface IDdlSearchService
{
    /// <summary>
    /// Search across all enabled DDL sites.
    /// </summary>
    Task<DdlAggregatedSearchResult> SearchAllAsync(DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search a specific DDL site by its type identifier.
    /// </summary>
    Task<DdlSearchResult> SearchSiteAsync(string siteType, DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get latest releases from all enabled sites.
    /// </summary>
    Task<DdlAggregatedSearchResult> GetLatestFromAllAsync(int limitPerSite = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract download links from a release page.
    /// </summary>
    Task<DdlLinkExtractionResult> ExtractLinksAsync(string siteType, string pageUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify if a download link is still valid.
    /// </summary>
    Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all registered site adapter types.
    /// </summary>
    IReadOnlyList<DdlSiteInfo> GetAvailableSites();
    
    /// <summary>
    /// Test connection to a specific site.
    /// </summary>
    Task<DdlSiteTestResult> TestSiteAsync(string siteType, DdlSiteConfiguration? config = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated results from searching multiple DDL sites.
/// </summary>
public class DdlAggregatedSearchResult
{
    /// <summary>
    /// All found candidates, merged and deduplicated.
    /// </summary>
    public IReadOnlyList<DdlCandidate> AllCandidates { get; init; } = Array.Empty<DdlCandidate>();
    
    /// <summary>
    /// Results broken down by site.
    /// </summary>
    public IReadOnlyDictionary<string, DdlSearchResult> ResultsBySite { get; init; } = new Dictionary<string, DdlSearchResult>();
    
    /// <summary>
    /// Sites that were successfully searched.
    /// </summary>
    public IReadOnlyList<string> SuccessfulSites { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Sites that failed to search.
    /// </summary>
    public IReadOnlyList<string> FailedSites { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Total candidates found (before deduplication).
    /// </summary>
    public int TotalRawCandidates { get; init; }
    
    /// <summary>
    /// Candidates removed due to deduplication.
    /// </summary>
    public int DuplicatesRemoved { get; init; }
    
    /// <summary>
    /// Total duration of all searches.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }
    
    /// <summary>
    /// Any warnings from the search operation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of link extraction from a release page.
/// </summary>
public class DdlLinkExtractionResult
{
    /// <summary>
    /// Extracted download links.
    /// </summary>
    public IReadOnlyList<DdlDownloadLink> Links { get; init; } = Array.Empty<DdlDownloadLink>();
    
    /// <summary>
    /// Whether extraction was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Source page URL.
    /// </summary>
    public string SourceUrl { get; init; } = string.Empty;
    
    /// <summary>
    /// Links that were found but appear dead/expired.
    /// </summary>
    public IReadOnlyList<string> DeadLinks { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a DDL site adapter.
/// </summary>
public class DdlSiteInfo
{
    /// <summary>
    /// Unique site type identifier.
    /// </summary>
    public required string SiteType { get; init; }
    
    /// <summary>
    /// Display name.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Default base URL.
    /// </summary>
    public required string DefaultBaseUrl { get; init; }
    
    /// <summary>
    /// Whether auth is required.
    /// </summary>
    public bool RequiresAuthentication { get; init; }
    
    /// <summary>
    /// Default rate limit.
    /// </summary>
    public int DefaultRateLimitPerMinute { get; init; }
    
    /// <summary>
    /// Description of the site.
    /// </summary>
    public string? Description { get; init; }
}

