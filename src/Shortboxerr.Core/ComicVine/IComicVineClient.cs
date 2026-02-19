namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Client for interacting with the ComicVine API.
/// </summary>
public interface IComicVineClient
{
    /// <summary>
    /// Tests the connection and validates the API key.
    /// </summary>
    Task<ComicVineTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for volumes (series) by name.
    /// </summary>
    Task<ComicVineSearchResult<ComicVineVolume>> SearchVolumesAsync(
        string query,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for issues.
    /// </summary>
    Task<ComicVineSearchResult<ComicVineIssue>> SearchIssuesAsync(
        string query,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a volume (series) by its ComicVine ID.
    /// </summary>
    Task<ComicVineResult<ComicVineVolume>> GetVolumeAsync(
        int volumeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an issue by its ComicVine ID.
    /// </summary>
    Task<ComicVineResult<ComicVineIssue>> GetIssueAsync(
        int issueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all issues for a volume (series).
    /// </summary>
    Task<ComicVineSearchResult<ComicVineIssue>> GetVolumeIssuesAsync(
        int volumeId,
        int page = 1,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a publisher by its ComicVine ID.
    /// </summary>
    Task<ComicVineResult<ComicVinePublisher>> GetPublisherAsync(
        int publisherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets issues by store date (release date) filter.
    /// </summary>
    /// <param name="storeDateFilter">Date filter in format "YYYY-MM-DD|YYYY-MM-DD" for range.</param>
    /// <param name="offset">Offset for pagination.</param>
    /// <param name="limit">Maximum results to return (max 100).</param>
    Task<ComicVineSearchResult<ComicVineIssue>> GetIssuesByStoreDateAsync(
        string storeDateFilter,
        int offset = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current rate limit status.
    /// </summary>
    ComicVineRateLimitStatus GetRateLimitStatus();

    /// <summary>
    /// Gets whether the client is configured (has API key) - uses cached value.
    /// Note: May return false if cache is stale. Use IsConfiguredAsync for reliable check.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Asynchronously checks whether the client is configured (has API key).
    /// This method fetches the API key from settings if needed, ensuring an accurate result.
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);
}

#region Result Types

/// <summary>
/// Result of a ComicVine API test.
/// </summary>
public class ComicVineTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int? LatencyMs { get; set; }
    public string? ApiVersion { get; set; }
}

/// <summary>
/// Wrapper for single-item ComicVine API responses.
/// </summary>
public class ComicVineResult<T>
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
    public T? Data { get; set; }
}

/// <summary>
/// Wrapper for search/list ComicVine API responses.
/// </summary>
public class ComicVineSearchResult<T>
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
    public List<T> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int NumberOfPageResults { get; set; }
}

/// <summary>
/// Rate limit status for the ComicVine API.
/// </summary>
public class ComicVineRateLimitStatus
{
    /// <summary>
    /// Number of requests made in the current window.
    /// </summary>
    public int RequestsUsed { get; set; }

    /// <summary>
    /// Maximum requests allowed per window (typically 200/hour).
    /// </summary>
    public int RequestLimit { get; set; } = 200;

    /// <summary>
    /// When the current rate limit window resets.
    /// </summary>
    public DateTime WindowResetTime { get; set; }

    /// <summary>
    /// Whether we're currently rate limited.
    /// </summary>
    public bool IsRateLimited => RequestsUsed >= RequestLimit;

    /// <summary>
    /// Time until the rate limit window resets.
    /// </summary>
    public TimeSpan TimeUntilReset => WindowResetTime > DateTime.UtcNow
        ? WindowResetTime - DateTime.UtcNow
        : TimeSpan.Zero;
}

#endregion

#region ComicVine Models

/// <summary>
/// ComicVine Volume (Series) data.
/// </summary>
public class ComicVineVolume
{
    /// <summary>
    /// ComicVine volume ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Volume name (series title).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Alternate names/aliases for the volume.
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    /// Start year of the volume.
    /// </summary>
    public int? StartYear { get; set; }

    /// <summary>
    /// Description/summary of the volume.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Short deck/tagline.
    /// </summary>
    public string? Deck { get; set; }

    /// <summary>
    /// Publisher information.
    /// </summary>
    public ComicVinePublisherRef? Publisher { get; set; }

    /// <summary>
    /// Total number of issues in the volume.
    /// </summary>
    public int IssueCount { get; set; }

    /// <summary>
    /// Cover image URLs.
    /// </summary>
    public ComicVineImage? Image { get; set; }

    /// <summary>
    /// First issue reference.
    /// </summary>
    public ComicVineIssueRef? FirstIssue { get; set; }

    /// <summary>
    /// Last issue reference.
    /// </summary>
    public ComicVineIssueRef? LastIssue { get; set; }

    /// <summary>
    /// ComicVine API detail URL.
    /// </summary>
    public string? ApiDetailUrl { get; set; }

    /// <summary>
    /// ComicVine site detail URL.
    /// </summary>
    public string? SiteDetailUrl { get; set; }

    /// <summary>
    /// Date added to ComicVine (string format: "YYYY-MM-DD HH:MM:SS").
    /// </summary>
    public string? DateAdded { get; set; }

    /// <summary>
    /// Date last updated on ComicVine (string format: "YYYY-MM-DD HH:MM:SS").
    /// </summary>
    public string? DateLastUpdated { get; set; }
}

/// <summary>
/// ComicVine Issue data.
/// </summary>
public class ComicVineIssue
{
    /// <summary>
    /// ComicVine issue ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Issue name/title (may be null for untitled issues).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Issue number (as string to handle decimals, specials like "½").
    /// </summary>
    public string IssueNumber { get; set; } = "";

    /// <summary>
    /// Description/summary of the issue.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Cover date (month/year on cover).
    /// </summary>
    public DateTime? CoverDate { get; set; }

    /// <summary>
    /// Store date (actual release date).
    /// </summary>
    public DateTime? StoreDate { get; set; }

    /// <summary>
    /// Volume (series) this issue belongs to.
    /// </summary>
    public ComicVineVolumeRef? Volume { get; set; }

    /// <summary>
    /// Cover image URLs.
    /// </summary>
    public ComicVineImage? Image { get; set; }

    /// <summary>
    /// ComicVine API detail URL.
    /// </summary>
    public string? ApiDetailUrl { get; set; }

    /// <summary>
    /// ComicVine site detail URL.
    /// </summary>
    public string? SiteDetailUrl { get; set; }

    /// <summary>
    /// Date added to ComicVine (string format: "YYYY-MM-DD HH:MM:SS").
    /// </summary>
    public string? DateAdded { get; set; }

    /// <summary>
    /// Date last updated on ComicVine (string format: "YYYY-MM-DD HH:MM:SS").
    /// </summary>
    public string? DateLastUpdated { get; set; }

    /// <summary>
    /// Story arcs this issue is part of.
    /// </summary>
    public List<ComicVineStoryArcRef> StoryArcs { get; set; } = new();

    /// <summary>
    /// Associated images (variant covers, promotional images, etc.).
    /// </summary>
    public List<ComicVineAssociatedImage> AssociatedImages { get; set; } = new();
}

/// <summary>
/// Associated image from ComicVine (variant covers, promotional images, etc.)
/// </summary>
public class ComicVineAssociatedImage
{
    /// <summary>
    /// ComicVine image ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Original image URL.
    /// </summary>
    public string? OriginalUrl { get; set; }

    /// <summary>
    /// Caption/description of the image.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Tags associated with the image.
    /// </summary>
    public string? ImageTags { get; set; }

    /// <summary>
    /// Whether this image appears to be a variant cover.
    /// </summary>
    public bool IsVariantCover { get; set; }

    /// <summary>
    /// Detected variant type (e.g., "variant", "incentive", "virgin", "sketch").
    /// </summary>
    public string? VariantType { get; set; }
}

/// <summary>
/// ComicVine Publisher data.
/// </summary>
public class ComicVinePublisher
{
    /// <summary>
    /// ComicVine publisher ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Publisher name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Alternate names/aliases.
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    /// Description of the publisher.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Publisher image/logo.
    /// </summary>
    public ComicVineImage? Image { get; set; }

    /// <summary>
    /// ComicVine site detail URL.
    /// </summary>
    public string? SiteDetailUrl { get; set; }
}

#endregion

#region Reference Types (for nested objects)

/// <summary>
/// Reference to a publisher (minimal data).
/// </summary>
public class ComicVinePublisherRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ApiDetailUrl { get; set; }
}

/// <summary>
/// Reference to a volume (minimal data).
/// </summary>
public class ComicVineVolumeRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ApiDetailUrl { get; set; }
}

/// <summary>
/// Reference to an issue (minimal data).
/// </summary>
public class ComicVineIssueRef
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string IssueNumber { get; set; } = "";
    public string? ApiDetailUrl { get; set; }
}

/// <summary>
/// Reference to a story arc.
/// </summary>
public class ComicVineStoryArcRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ApiDetailUrl { get; set; }
}

/// <summary>
/// ComicVine image URLs at different sizes.
/// </summary>
public class ComicVineImage
{
    /// <summary>
    /// Icon size (35px).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Medium size (~160px).
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Screen size (~320px).
    /// </summary>
    public string? ScreenUrl { get; set; }

    /// <summary>
    /// Screen large size (~480px).
    /// </summary>
    public string? ScreenLargeUrl { get; set; }

    /// <summary>
    /// Small size (~90px).
    /// </summary>
    public string? SmallUrl { get; set; }

    /// <summary>
    /// Super size (~640px).
    /// </summary>
    public string? SuperUrl { get; set; }

    /// <summary>
    /// Thumb size (~100px).
    /// </summary>
    public string? ThumbUrl { get; set; }

    /// <summary>
    /// Tiny size (~30px).
    /// </summary>
    public string? TinyUrl { get; set; }

    /// <summary>
    /// Original full-size image.
    /// </summary>
    public string? OriginalUrl { get; set; }
}

#endregion

#region Settings

/// <summary>
/// ComicVine-specific settings.
/// </summary>
public class ComicVineSettings
{
    /// <summary>
    /// ComicVine API key.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Whether ComicVine integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Cache TTL for metadata in hours (default: 24).
    /// </summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Directory for caching cover images.
    /// </summary>
    public string CoverCacheDirectory { get; set; } = "/config/covers";

    /// <summary>
    /// Minimum confidence score for auto-matching (0-100).
    /// </summary>
    public int AutoMatchThreshold { get; set; } = 85;

    /// <summary>
    /// Whether to automatically refresh metadata on a schedule.
    /// </summary>
    public bool AutoRefreshEnabled { get; set; } = true;

    /// <summary>
    /// How often to refresh metadata in days.
    /// </summary>
    public int RefreshIntervalDays { get; set; } = 7;

    #region Discovery Refresh Settings (Mylar3 Parity)

    /// <summary>
    /// Whether to enable automatic background refresh of ComicVine discovery data.
    /// When enabled, release schedules are refreshed periodically even if the user
    /// doesn't visit the UI (useful for automation like auto-add to wanted list).
    /// </summary>
    public bool DiscoveryRefreshEnabled { get; set; } = true;

    /// <summary>
    /// How often to refresh discovery data (in hours). Default: 4 hours (Mylar3 parity).
    /// ComicVine typically updates release schedules weekly, but publishers may
    /// adjust dates. 4 hours balances freshness with API rate limits.
    /// </summary>
    public int DiscoveryRefreshIntervalHours { get; set; } = 4;

    /// <summary>
    /// Hours during which discovery refresh is allowed.
    /// Empty means all hours are allowed.
    /// Example: [6, 7, 8, 12, 18] = 6am, 7am, 8am, 12pm, 6pm
    /// </summary>
    public List<int> DiscoveryRefreshAllowedHours { get; set; } = new();

    /// <summary>
    /// Number of weeks to pre-fetch in discovery refresh.
    /// Default: 4 weeks (current + 3 future).
    /// </summary>
    public int DiscoveryRefreshWeeksAhead { get; set; } = 4;

    #endregion
}

#endregion

