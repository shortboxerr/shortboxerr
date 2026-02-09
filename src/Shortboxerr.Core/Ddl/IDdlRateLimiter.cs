namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Rate limiter for DDL site requests.
/// Prevents exceeding site-specific request limits to avoid bans.
/// </summary>
public interface IDdlRateLimiter
{
    /// <summary>
    /// Acquires permission to make a request to the specified site.
    /// Blocks until the request can be made within rate limits.
    /// </summary>
    /// <param name="siteType">The DDL site type identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A disposable token that should be disposed when the request completes</returns>
    Task<IDisposable> AcquireAsync(string siteType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire permission without blocking.
    /// Returns immediately with success/failure.
    /// </summary>
    /// <param name="siteType">The DDL site type identifier</param>
    /// <param name="token">The acquired token if successful</param>
    /// <returns>True if permission was acquired, false if rate limited</returns>
    bool TryAcquire(string siteType, out IDisposable? token);

    /// <summary>
    /// Gets the current rate limit status for a site.
    /// </summary>
    RateLimitStatus GetStatus(string siteType);

    /// <summary>
    /// Gets rate limit status for all configured sites.
    /// </summary>
    IReadOnlyDictionary<string, RateLimitStatus> GetAllStatuses();

    /// <summary>
    /// Configures the rate limit for a specific site.
    /// </summary>
    /// <param name="siteType">The DDL site type identifier</param>
    /// <param name="requestsPerMinute">Maximum requests allowed per minute</param>
    /// <param name="minDelayMs">Minimum delay between requests in milliseconds</param>
    void Configure(string siteType, int requestsPerMinute, int minDelayMs = 0);

    /// <summary>
    /// Reports a request failure (e.g., 429 Too Many Requests).
    /// Triggers backoff for the site.
    /// </summary>
    /// <param name="siteType">The DDL site type identifier</param>
    /// <param name="retryAfter">Optional retry-after duration from server</param>
    void ReportRateLimited(string siteType, TimeSpan? retryAfter = null);

    /// <summary>
    /// Resets the rate limiter state for a site.
    /// </summary>
    void Reset(string siteType);

    /// <summary>
    /// Resets all rate limiter state.
    /// </summary>
    void ResetAll();
}

/// <summary>
/// Status information for a site's rate limiter.
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// Site type identifier.
    /// </summary>
    public required string SiteType { get; init; }

    /// <summary>
    /// Configured maximum requests per minute.
    /// </summary>
    public int RequestsPerMinute { get; init; }

    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// </summary>
    public int MinDelayMs { get; init; }

    /// <summary>
    /// Number of requests made in the current window.
    /// </summary>
    public int RequestsInWindow { get; init; }

    /// <summary>
    /// Number of requests remaining in the current window.
    /// </summary>
    public int RequestsRemaining { get; init; }

    /// <summary>
    /// When the current rate limit window resets.
    /// </summary>
    public DateTime WindowResetTime { get; init; }

    /// <summary>
    /// Time remaining until window resets.
    /// </summary>
    public TimeSpan TimeUntilReset => WindowResetTime > DateTime.UtcNow 
        ? WindowResetTime - DateTime.UtcNow 
        : TimeSpan.Zero;

    /// <summary>
    /// Whether the site is currently in backoff due to rate limiting.
    /// </summary>
    public bool IsInBackoff { get; init; }

    /// <summary>
    /// When backoff expires (if in backoff).
    /// </summary>
    public DateTime? BackoffUntil { get; init; }

    /// <summary>
    /// Last request time.
    /// </summary>
    public DateTime? LastRequestTime { get; init; }

    /// <summary>
    /// Total requests made to this site.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Total rate limit violations for this site.
    /// </summary>
    public int RateLimitViolations { get; init; }
}
