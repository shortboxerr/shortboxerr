namespace Shortboxerr.Core.Metron;

/// <summary>
/// Client interface for Metron comic database API.
/// 
/// Metron is a community-maintained comic database with an official REST API.
/// It provides direct ComicVine ID mapping, eliminating the need for fuzzy matching.
/// 
/// API Base: https://metron.cloud/api/
/// Authentication: Basic Auth (username:password)
/// Rate Limits: 30 requests/minute, 10,000 requests/day
/// 
/// Key advantage: Direct ComicVine ID lookup via cv_id field.
/// </summary>
public interface IMetronClient
{
    /// <summary>
    /// Gets an issue by its ComicVine ID.
    /// This is the preferred lookup method as it provides exact matching.
    /// </summary>
    /// <param name="comicVineIssueId">The ComicVine issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The issue with cover URL if found</returns>
    Task<MetronIssueResult> GetIssueByCvIdAsync(
        int comicVineIssueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for an issue by series name and issue number.
    /// Use as fallback when ComicVine ID is not available.
    /// </summary>
    /// <param name="seriesName">The series name</param>
    /// <param name="issueNumber">The issue number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    Task<MetronSearchResult> SearchIssueAsync(
        string seriesName,
        string issueNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the Metron service is available and credentials are valid.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is available and authenticated</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the client is configured with credentials.
    /// </summary>
    bool IsConfigured { get; }
}

/// <summary>
/// Result of a Metron issue lookup by ComicVine ID.
/// </summary>
public class MetronIssueResult
{
    /// <summary>Whether the lookup was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the lookup failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>The issue data if found.</summary>
    public MetronIssue? Issue { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }

    public static MetronIssueResult NotFound(string? error = null) => new()
    {
        Success = false,
        Error = error ?? "Issue not found",
        StatusCode = 404
    };

    public static MetronIssueResult Found(MetronIssue issue, bool fromCache = false) => new()
    {
        Success = true,
        Issue = issue,
        StatusCode = 200,
        FromCache = fromCache
    };

    public static MetronIssueResult Failed(string error, int statusCode = 0) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}

/// <summary>
/// Result of a Metron issue search.
/// </summary>
public class MetronSearchResult
{
    /// <summary>Whether the search was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the search failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>List of matching issues.</summary>
    public List<MetronIssue> Issues { get; set; } = new();

    /// <summary>Total count from the API.</summary>
    public int TotalCount { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }
}

/// <summary>
/// A comic issue from Metron.
/// </summary>
public class MetronIssue
{
    /// <summary>Metron internal issue ID.</summary>
    public int Id { get; set; }

    /// <summary>Series information.</summary>
    public MetronSeries? Series { get; set; }

    /// <summary>Issue number (as string, e.g., "1", "Annual 2").</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Cover date (printed on cover).</summary>
    public DateTime? CoverDate { get; set; }

    /// <summary>Store date (actual release date).</summary>
    public DateTime? StoreDate { get; set; }

    /// <summary>Cover image URL.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>ComicVine issue ID (for cross-reference).</summary>
    public int? CvId { get; set; }

    /// <summary>Grand Comics Database ID.</summary>
    public int? GcdId { get; set; }

    /// <summary>Issue description.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Series information from Metron.
/// </summary>
public class MetronSeries
{
    /// <summary>Metron internal series ID.</summary>
    public int Id { get; set; }

    /// <summary>Series name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Volume number.</summary>
    public int? Volume { get; set; }

    /// <summary>Year the series began.</summary>
    public int? YearBegan { get; set; }

    /// <summary>Publisher information.</summary>
    public MetronPublisher? Publisher { get; set; }
}

/// <summary>
/// Publisher information from Metron.
/// </summary>
public class MetronPublisher
{
    /// <summary>Metron internal publisher ID.</summary>
    public int Id { get; set; }

    /// <summary>Publisher name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Settings for Metron API integration.
/// </summary>
public class MetronSettings
{
    /// <summary>Hardcoded timeout (not user-configurable).</summary>
    public const int DefaultTimeoutSeconds = 30;
    
    /// <summary>Hardcoded rate limit per Metron API spec (not user-configurable).</summary>
    public const int DefaultMaxRequestsPerMinute = 30;

    /// <summary>Whether Metron integration is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Metron username.</summary>
    public string? Username { get; set; }

    /// <summary>Metron password.</summary>
    public string? Password { get; set; }

    /// <summary>Cache TTL in hours (default: 24 hours).</summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>Request timeout in seconds (hardcoded, not user-configurable).</summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>Maximum requests per minute (hardcoded to Metron's limit, not user-configurable).</summary>
    public int MaxRequestsPerMinute { get; set; } = DefaultMaxRequestsPerMinute;

    /// <summary>Whether credentials are configured.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
