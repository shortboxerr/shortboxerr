using Shortboxerr.Core.Services;

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
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The issue with cover URL if found</returns>
    Task<MetronIssueResult> GetIssueByCvIdAsync(
        int comicVineIssueId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a series by its ComicVine volume ID.
    /// Used to find the Metron series ID for subsequent issue lookups.
    /// </summary>
    /// <param name="comicVineVolumeId">The ComicVine volume ID</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The series if found</returns>
    Task<MetronSeriesResult> GetSeriesByCvIdAsync(
        int comicVineVolumeId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an issue by Metron series ID and issue number.
    /// More reliable than series name search when CV volume ID mapping is available.
    /// </summary>
    /// <param name="metronSeriesId">The Metron series ID</param>
    /// <param name="issueNumber">The issue number (will be normalized)</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The issue with cover URL if found</returns>
    Task<MetronIssueResult> GetIssueBySeriesIdAsync(
        int metronSeriesId,
        string issueNumber,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for an issue by series name and issue number.
    /// Use as fallback when ComicVine ID is not available.
    /// </summary>
    /// <param name="seriesName">The series name</param>
    /// <param name="issueNumber">The issue number</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    Task<MetronSearchResult> SearchIssueAsync(
        string seriesName,
        string issueNumber,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all issues for a Metron series by series ID.
    /// Returns a list of issues with basic info including cover images.
    /// More efficient than individual issue lookups when you need multiple issues.
    /// </summary>
    /// <param name="metronSeriesId">The Metron series ID</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of issues for the series</returns>
    Task<MetronIssueListResult> GetSeriesIssueListAsync(
        int metronSeriesId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full issue details by Metron issue ID.
    /// Returns complete metadata including title, description, price, credits, etc.
    /// Use this when you need more than just the cover image from issue_list.
    /// </summary>
    /// <param name="metronIssueId">The Metron issue ID</param>
    /// <param name="bypassCache">If true, bypasses the cache and forces a fresh lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full issue details if found</returns>
    Task<MetronIssueResult> GetIssueByIdAsync(
        int metronIssueId,
        bool bypassCache = false,
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
/// Result of a Metron series lookup by ComicVine volume ID.
/// </summary>
public class MetronSeriesResult
{
    /// <summary>Whether the lookup was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the lookup failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>The series data if found.</summary>
    public MetronSeries? Series { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }

    public static MetronSeriesResult NotFound(string? error = null) => new()
    {
        Success = false,
        Error = error ?? "Series not found",
        StatusCode = 404
    };

    public static MetronSeriesResult Found(MetronSeries series, bool fromCache = false) => new()
    {
        Success = true,
        Series = series,
        StatusCode = 200,
        FromCache = fromCache
    };

    public static MetronSeriesResult Failed(string error, int statusCode = 0) => new()
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
/// Result of fetching all issues for a Metron series.
/// </summary>
public class MetronIssueListResult
{
    /// <summary>Whether the lookup was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the lookup failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>The Metron series ID this list is for.</summary>
    public int MetronSeriesId { get; set; }

    /// <summary>List of issues in the series.</summary>
    public List<MetronIssue> Issues { get; set; } = new();

    /// <summary>Total count of issues in the series.</summary>
    public int TotalCount { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }

    public static MetronIssueListResult NotFound(int seriesId, string? error = null) => new()
    {
        Success = false,
        Error = error ?? $"Series {seriesId} not found",
        StatusCode = 404,
        MetronSeriesId = seriesId
    };

    public static MetronIssueListResult Found(int seriesId, List<MetronIssue> issues, int totalCount, bool fromCache = false) => new()
    {
        Success = true,
        MetronSeriesId = seriesId,
        Issues = issues,
        TotalCount = totalCount,
        StatusCode = 200,
        FromCache = fromCache
    };

    public static MetronIssueListResult Failed(int seriesId, string error, int statusCode = 0) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode,
        MetronSeriesId = seriesId
    };
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

    /// <summary>Issue title (usually empty for single-issue stories).</summary>
    public string? Title { get; set; }

    /// <summary>Story names/arc parts (e.g., "Season of the Witch, Part 2 of 5").</summary>
    public List<string> StoryNames { get; set; } = new();

    /// <summary>Cover date (printed on cover).</summary>
    public DateTime? CoverDate { get; set; }

    /// <summary>Store date (actual release date).</summary>
    public DateTime? StoreDate { get; set; }

    /// <summary>Cover image URL.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Cover price (e.g., "4.99").</summary>
    public string? Price { get; set; }

    /// <summary>ComicVine issue ID (for cross-reference).</summary>
    public int? CvId { get; set; }

    /// <summary>Grand Comics Database ID.</summary>
    public int? GcdId { get; set; }

    /// <summary>Issue description/solicitation text.</summary>
    public string? Description { get; set; }

    /// <summary>Display name like "Absolute Wonder Woman (2024) #17".</summary>
    public string? DisplayName { get; set; }
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

    /// <summary>ComicVine volume ID (for cross-reference).</summary>
    public int? CvId { get; set; }
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

    /// <summary>Metron password (encrypted at rest).</summary>
    [SensitiveCredential]
    public string? Password { get; set; }

    /// <summary>Cache TTL in hours (default: 24 hours).</summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Minimum confidence (0-100) required for ID-less Metron issue matching.
    /// Used when WalkSoftly does not provide a ComicVine issue ID.
    /// </summary>
    public int MinMatchConfidence { get; set; } = 85;

    /// <summary>Request timeout in seconds (hardcoded, not user-configurable).</summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>Maximum requests per minute (hardcoded to Metron's limit, not user-configurable).</summary>
    public int MaxRequestsPerMinute { get; set; } = DefaultMaxRequestsPerMinute;

    /// <summary>Whether credentials are configured.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
