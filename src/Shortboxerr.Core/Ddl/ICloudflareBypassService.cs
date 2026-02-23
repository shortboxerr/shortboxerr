namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for bypassing Cloudflare protection on websites.
/// Uses FlareSolverr (or similar) to solve JavaScript challenges and return valid session cookies.
/// </summary>
public interface ICloudflareBypassService
{
    /// <summary>
    /// Tests connectivity to the bypass service (e.g., FlareSolverr).
    /// </summary>
    Task<CloudflareBypassTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to bypass Cloudflare protection and get valid session data.
    /// </summary>
    Task<CloudflareBypassResult> BypassAsync(string url, CloudflareBypassOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached session cookies for a domain (if available and not expired).
    /// </summary>
    Task<CloudflareCookieSession?> GetCachedSessionAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears cached session for a domain.
    /// </summary>
    Task ClearSessionAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current service settings.
    /// </summary>
    Task<CloudflareBypassSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves service settings.
    /// </summary>
    Task SaveSettingsAsync(CloudflareBypassSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of testing the bypass service connection.
/// </summary>
public record CloudflareBypassTestResult
{
    /// <summary>
    /// Whether the service is available and responding.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Service version (if available).
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; init; }
}

/// <summary>
/// Result of a Cloudflare bypass attempt.
/// </summary>
public record CloudflareBypassResult
{
    /// <summary>
    /// Whether the bypass was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Session cookies that can be used for subsequent requests.
    /// </summary>
    public CloudflareCookieSession? Session { get; init; }

    /// <summary>
    /// The HTML content of the page (if requested).
    /// </summary>
    public string? HtmlContent { get; init; }

    /// <summary>
    /// The final URL after any redirects.
    /// </summary>
    public string? FinalUrl { get; init; }

    /// <summary>
    /// User-Agent that was used (should be reused in subsequent requests).
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Failure reason if bypass failed.
    /// </summary>
    public CloudflareBypassFailureReason FailureReason { get; init; }

    /// <summary>
    /// Error message if bypass failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// How long the bypass took.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CloudflareBypassResult Succeeded(CloudflareCookieSession session, string? userAgent = null, string? htmlContent = null, string? finalUrl = null)
    {
        return new CloudflareBypassResult
        {
            Success = true,
            Session = session,
            UserAgent = userAgent,
            HtmlContent = htmlContent,
            FinalUrl = finalUrl,
            FailureReason = CloudflareBypassFailureReason.None
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static CloudflareBypassResult Failed(CloudflareBypassFailureReason reason, string errorMessage)
    {
        return new CloudflareBypassResult
        {
            Success = false,
            FailureReason = reason,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Session cookies from a successful Cloudflare bypass.
/// </summary>
public class CloudflareCookieSession
{
    /// <summary>
    /// The domain these cookies are valid for.
    /// </summary>
    public string Domain { get; set; } = "";

    /// <summary>
    /// Cookies to include in requests.
    /// </summary>
    public Dictionary<string, string> Cookies { get; set; } = new();

    /// <summary>
    /// User-Agent that was used (must match for cookies to work).
    /// </summary>
    public string UserAgent { get; set; } = "";

    /// <summary>
    /// When this session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this session expires (estimated).
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(2);

    /// <summary>
    /// Whether the session has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Gets the cf_clearance cookie value (main Cloudflare session cookie).
    /// </summary>
    public string? CfClearance => Cookies.TryGetValue("cf_clearance", out var value) ? value : null;
}

/// <summary>
/// Options for Cloudflare bypass requests.
/// </summary>
public class CloudflareBypassOptions
{
    /// <summary>
    /// Maximum time to wait for the bypass (includes solving time).
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether to return the HTML content of the page.
    /// </summary>
    public bool ReturnHtmlContent { get; set; } = false;

    /// <summary>
    /// Custom User-Agent to use. If null, FlareSolverr will use its default.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// HTTP method to use (GET or POST).
    /// </summary>
    public string HttpMethod { get; set; } = "GET";

    /// <summary>
    /// POST data (if HttpMethod is POST).
    /// </summary>
    public string? PostData { get; set; }

    /// <summary>
    /// Additional headers to include in the request.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Maximum number of redirects to follow.
    /// </summary>
    public int MaxRedirects { get; set; } = 5;
}

/// <summary>
/// Settings for the Cloudflare bypass service.
/// </summary>
public class CloudflareBypassSettings
{
    /// <summary>
    /// Whether Cloudflare bypass is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// FlareSolverr server URL (e.g., "http://localhost:8191").
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:8191";

    /// <summary>
    /// Default timeout for bypass requests in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// How long to cache session cookies (in minutes).
    /// </summary>
    public int SessionCacheMinutes { get; set; } = 120;

    /// <summary>
    /// Maximum concurrent sessions (browser instances).
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 2;

    /// <summary>
    /// Whether to automatically retry on challenge failure.
    /// </summary>
    public bool AutoRetry { get; set; } = true;

    /// <summary>
    /// Number of retry attempts on failure.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Reasons for Cloudflare bypass failure.
/// </summary>
public enum CloudflareBypassFailureReason
{
    /// <summary>
    /// No failure.
    /// </summary>
    None = 0,

    /// <summary>
    /// The bypass service (FlareSolverr) is not available.
    /// </summary>
    ServiceUnavailable = 1,

    /// <summary>
    /// Connection to FlareSolverr failed.
    /// </summary>
    ConnectionFailed = 2,

    /// <summary>
    /// The challenge could not be solved (timeout or unsupported challenge type).
    /// </summary>
    ChallengeFailed = 3,

    /// <summary>
    /// CAPTCHA required (cannot be solved automatically).
    /// </summary>
    CaptchaRequired = 4,

    /// <summary>
    /// The request timed out.
    /// </summary>
    Timeout = 5,

    /// <summary>
    /// The target URL is invalid or inaccessible.
    /// </summary>
    InvalidUrl = 6,

    /// <summary>
    /// The service returned an unexpected response.
    /// </summary>
    InvalidResponse = 7,

    /// <summary>
    /// The bypass feature is disabled in settings.
    /// </summary>
    Disabled = 8,

    /// <summary>
    /// Too many concurrent requests.
    /// </summary>
    TooManyRequests = 9,

    /// <summary>
    /// Unknown error.
    /// </summary>
    Unknown = 99
}
