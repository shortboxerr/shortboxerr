namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for searching across multiple DDL sites.
/// </summary>
public interface IDdlSearchService
{
    /// <summary>
    /// Search across all enabled DDL sites.
    /// </summary>
    Task<DdlAggregatedSearchResult> SearchAllAsync(DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search a specific site.
    /// </summary>
    Task<DdlSearchResult> SearchSiteAsync(string siteType, DdlSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get latest releases from all enabled sites.
    /// </summary>
    Task<DdlAggregatedSearchResult> GetLatestFromAllAsync(int limitPerSite = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract download links from a page.
    /// </summary>
    Task<DdlLinkExtractionResult> ExtractLinksAsync(string siteType, string pageUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify if a download link is still valid.
    /// </summary>
    Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get information about all available sites.
    /// </summary>
    IReadOnlyList<DdlSiteInfo> GetAvailableSites();
    
    /// <summary>
    /// Test connection to a specific site.
    /// </summary>
    Task<DdlSiteTestResult> TestSiteAsync(string siteType, DdlSiteConfiguration? config = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated search results from multiple DDL sites.
/// </summary>
public class DdlAggregatedSearchResult
{
    /// <summary>
    /// All candidates from all sites, deduplicated.
    /// </summary>
    public IReadOnlyList<DdlCandidate> AllCandidates { get; init; } = Array.Empty<DdlCandidate>();
    
    /// <summary>
    /// Results by site type.
    /// </summary>
    public IReadOnlyDictionary<string, DdlSearchResult> ResultsBySite { get; init; } = new Dictionary<string, DdlSearchResult>();
    
    /// <summary>
    /// Sites that returned results successfully.
    /// </summary>
    public IReadOnlyList<string> SuccessfulSites { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Sites that failed to search.
    /// </summary>
    public IReadOnlyList<string> FailedSites { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Total raw candidate count before deduplication.
    /// </summary>
    public int TotalRawCandidates { get; init; }
    
    /// <summary>
    /// Number of duplicates removed.
    /// </summary>
    public int DuplicatesRemoved { get; init; }
    
    /// <summary>
    /// Total duration for all searches.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }
    
    /// <summary>
    /// Any warnings generated during search.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether the overall search was successful (at least one site succeeded).
    /// </summary>
    public bool Success => SuccessfulSites.Count > 0;
}

/// <summary>
/// Result of extracting download links from a page.
/// </summary>
public class DdlLinkExtractionResult
{
    /// <summary>
    /// Whether extraction was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Extracted download links.
    /// </summary>
    public IReadOnlyList<DdlDownloadLink> Links { get; init; } = Array.Empty<DdlDownloadLink>();
    
    /// <summary>
    /// Source URL the links were extracted from.
    /// </summary>
    public string SourceUrl { get; init; } = string.Empty;
    
    /// <summary>
    /// Links that were detected but appear dead.
    /// </summary>
    public IReadOnlyList<string> DeadLinks { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
