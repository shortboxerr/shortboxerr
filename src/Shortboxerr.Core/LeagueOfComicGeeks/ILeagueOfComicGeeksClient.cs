namespace Shortboxerr.Core.LeagueOfComicGeeks;

/// <summary>
/// Client interface for League of Comic Geeks cover image lookups.
/// 
/// IMPORTANT: LOCG has no official API. This client uses unofficial HTML scraping
/// patterns derived from community libraries (comicgeeks, leagueofcomicgeeks).
/// The implementation may break if the site structure changes.
/// 
/// Internal endpoint: https://leagueofcomicgeeks.com/comic/get_comics
/// Returns JSON with HTML in the "list" field, which must be parsed.
/// Cover images are hosted on S3: https://s3.amazonaws.com/comicgeeks/comics/covers/large-{id}.jpg
/// </summary>
public interface ILeagueOfComicGeeksClient
{
    /// <summary>
    /// Searches for a comic issue by series name and issue number.
    /// Returns potential matches that must be fuzzy-matched by the caller.
    /// </summary>
    /// <param name="seriesName">The name of the comic series</param>
    /// <param name="issueNumber">The issue number (e.g., "1", "17", "Annual 2")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing potential matches</returns>
    Task<LocgSearchResult> SearchIssueAsync(
        string seriesName,
        string? issueNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the weekly releases for a given date.
    /// </summary>
    /// <param name="date">The date to get releases for (uses the release week)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing issues released that week</returns>
    Task<LocgSearchResult> GetWeeklyReleasesAsync(
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the LOCG service is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is reachable and responding</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a League of Comic Geeks search.
/// </summary>
public class LocgSearchResult
{
    /// <summary>Whether the request was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if request failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>List of matching issues found.</summary>
    public List<LocgIssue> Issues { get; set; } = new();

    /// <summary>Total count from the response.</summary>
    public int TotalCount { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }

    /// <summary>When the data was fetched/cached.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A comic issue from League of Comic Geeks.
/// </summary>
public class LocgIssue
{
    /// <summary>LOCG internal issue ID (not ComicVine ID).</summary>
    public int IssueId { get; set; }

    /// <summary>Full issue name (e.g., "Batman #105").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Series name extracted from the full name.</summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>Issue number extracted from the full name.</summary>
    public string? IssueNumber { get; set; }

    /// <summary>Publisher name.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Cover image URL (S3 hosted).</summary>
    public string? CoverUrl { get; set; }

    /// <summary>Relative URL on LOCG site.</summary>
    public string? Url { get; set; }

    /// <summary>Price if available.</summary>
    public decimal? Price { get; set; }

    /// <summary>Release/store date if available.</summary>
    public DateTime? StoreDate { get; set; }

    /// <summary>Community pull count.</summary>
    public int? PullCount { get; set; }

    /// <summary>Community rating (0-100).</summary>
    public int? Rating { get; set; }

    /// <summary>Brief description if available.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Settings for League of Comic Geeks integration.
/// </summary>
public class LocgSettings
{
    /// <summary>Whether LOCG integration is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cache TTL in hours for LOCG responses (default: 24 hours).</summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum requests per minute (conservative to avoid blocks).</summary>
    public int MaxRequestsPerMinute { get; set; } = 30;

    /// <summary>Minimum delay between requests in milliseconds.</summary>
    public int MinDelayBetweenRequestsMs { get; set; } = 2000;
}
