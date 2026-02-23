namespace Shortboxerr.Core.WalkSoftly;

/// <summary>
/// Client interface for the WalkSoftly comic release aggregator service.
/// WalkSoftly provides weekly comic release data with pre-mapped ComicVine IDs,
/// offering fresher data than direct ComicVine queries.
/// </summary>
public interface IWalkSoftlyClient
{
    /// <summary>
    /// Gets weekly comic releases for a specific week.
    /// </summary>
    /// <param name="weekNumber">ISO week number (1-52)</param>
    /// <param name="year">Year</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing list of releases for the week</returns>
    Task<WalkSoftlyResult> GetWeeklyReleasesAsync(
        int weekNumber,
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the WalkSoftly service is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is reachable and responding</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a WalkSoftly API call.
/// </summary>
public class WalkSoftlyResult
{
    /// <summary>Whether the request was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if request failed.</summary>
    public string? Error { get; set; }

    /// <summary>HTTP status code returned.</summary>
    public int StatusCode { get; set; }

    /// <summary>List of releases for the requested week.</summary>
    public List<WalkSoftlyRelease> Releases { get; set; } = new();

    /// <summary>Week number requested.</summary>
    public int WeekNumber { get; set; }

    /// <summary>Year requested.</summary>
    public int Year { get; set; }

    /// <summary>Whether data came from cache.</summary>
    public bool FromCache { get; set; }

    /// <summary>When the data was fetched/cached.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A comic release from WalkSoftly.
/// Maps directly to the JSON response from walksoftly.itsaninja.party/newcomics.php
/// </summary>
public class WalkSoftlyRelease
{
    /// <summary>Series/volume name.</summary>
    public string Series { get; set; } = string.Empty;

    /// <summary>Alternative name for the series (for matching).</summary>
    public string? Alias { get; set; }

    /// <summary>Issue number (e.g., "#1", "Annual #2").</summary>
    public string Issue { get; set; } = string.Empty;

    /// <summary>Publisher name.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>In-store ship date (release date).</summary>
    public DateTime? ShipDate { get; set; }

    /// <summary>Cover date printed on the comic.</summary>
    public DateTime? CoverDate { get; set; }

    /// <summary>ComicVine volume (series) ID.</summary>
    public int? ComicId { get; set; }

    /// <summary>ComicVine issue ID.</summary>
    public int? IssueId { get; set; }

    /// <summary>Week number this release belongs to.</summary>
    public int WeekNumber { get; set; }

    /// <summary>Year this release belongs to.</summary>
    public int Year { get; set; }

    /// <summary>Volume number (e.g., "1", "2").</summary>
    public string? Volume { get; set; }

    /// <summary>Series start year.</summary>
    public string? SeriesYear { get; set; }

    /// <summary>Link for annuals (relates to parent series).</summary>
    public string? AnnualLink { get; set; }

    /// <summary>Format type (e.g., "Comic", "Hardcover", "Trade Paperback").</summary>
    public string? Format { get; set; }
}

/// <summary>
/// Settings for WalkSoftly integration.
/// </summary>
public class WalkSoftlySettings
{
    /// <summary>Whether to use WalkSoftly as the primary data source.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether to fall back to ComicVine if WalkSoftly is unavailable.</summary>
    public bool FallbackToComicVine { get; set; } = true;

    /// <summary>Cache TTL in minutes for WalkSoftly responses (default: 240 = 4 hours like Mylar3).</summary>
    public int CacheTtlMinutes { get; set; } = 240;

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Publishers to ignore/exclude from pull list.
    /// Supports wildcards: "*Manga*" matches any publisher containing "Manga".
    /// </summary>
    public List<string> IgnoredPublishers { get; set; } = new();
}
