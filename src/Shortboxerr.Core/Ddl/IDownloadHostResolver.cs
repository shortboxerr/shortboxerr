namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Interface for resolving file hosting service URLs to direct download URLs.
/// Each supported file host has its own implementation.
/// </summary>
public interface IDownloadHostResolver
{
    /// <summary>
    /// Unique identifier for this host resolver.
    /// </summary>
    string HostId { get; }

    /// <summary>
    /// Display name for the file host.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// URL patterns that this resolver can handle (e.g., "mediafire.com", "mega.nz").
    /// </summary>
    IReadOnlyList<string> SupportedHosts { get; }

    /// <summary>
    /// Priority for this resolver (lower = preferred).
    /// Used when multiple resolvers could handle a URL.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Whether this resolver is currently available/functional.
    /// Some hosts may become defunct (e.g., Zippyshare).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Checks if this resolver can handle the given URL.
    /// </summary>
    bool CanResolve(string url);

    /// <summary>
    /// Resolves a file host URL to a direct download URL and metadata.
    /// </summary>
    Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies if a URL is still valid (file exists, not expired).
    /// </summary>
    Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of resolving a file host URL.
/// </summary>
public record HostResolverResult
{
    /// <summary>
    /// Whether the resolution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The direct download URL (if successful).
    /// </summary>
    public string? DirectUrl { get; init; }

    /// <summary>
    /// Filename extracted from the host page (if available).
    /// </summary>
    public string? Filename { get; init; }

    /// <summary>
    /// File size in bytes (if available).
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    /// Content type/MIME type (if available).
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Error message if resolution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Failure reason category.
    /// </summary>
    public HostResolverFailureReason FailureReason { get; init; }

    /// <summary>
    /// Additional headers required for the download.
    /// </summary>
    public Dictionary<string, string> RequiredHeaders { get; init; } = new();

    /// <summary>
    /// Cookies required for the download.
    /// </summary>
    public Dictionary<string, string> RequiredCookies { get; init; } = new();

    /// <summary>
    /// How long the resolved URL is valid (if known).
    /// </summary>
    public TimeSpan? UrlExpiry { get; init; }

    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static HostResolverResult Succeeded(string directUrl, string? filename = null, long? fileSize = null)
    {
        return new HostResolverResult
        {
            Success = true,
            DirectUrl = directUrl,
            Filename = filename,
            FileSize = fileSize,
            FailureReason = HostResolverFailureReason.None
        };
    }

    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static HostResolverResult Failed(HostResolverFailureReason reason, string errorMessage)
    {
        return new HostResolverResult
        {
            Success = false,
            FailureReason = reason,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of verifying a file host URL.
/// </summary>
public record HostVerifyResult
{
    /// <summary>
    /// Whether the file is available for download.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Filename (if available).
    /// </summary>
    public string? Filename { get; init; }

    /// <summary>
    /// File size in bytes (if available).
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    /// Error/status message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Reason for unavailability.
    /// </summary>
    public HostResolverFailureReason FailureReason { get; init; }
}

/// <summary>
/// Options for URL resolution.
/// </summary>
public class HostResolverOptions
{
    /// <summary>
    /// Timeout for resolution in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Custom User-Agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Credentials for premium hosts (username/password or API key).
    /// </summary>
    public HostCredentials? Credentials { get; set; }

    /// <summary>
    /// Whether to follow redirects automatically.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;
}

/// <summary>
/// Credentials for file hosting services.
/// </summary>
public class HostCredentials
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
}

/// <summary>
/// Reasons for host resolver failures.
/// </summary>
public enum HostResolverFailureReason
{
    /// <summary>
    /// No failure.
    /// </summary>
    None = 0,

    /// <summary>
    /// The file was not found (deleted or never existed).
    /// </summary>
    FileNotFound = 1,

    /// <summary>
    /// The link has expired.
    /// </summary>
    LinkExpired = 2,

    /// <summary>
    /// The host requires authentication/premium account.
    /// </summary>
    AuthenticationRequired = 3,

    /// <summary>
    /// Rate limited by the host.
    /// </summary>
    RateLimited = 4,

    /// <summary>
    /// The host service is unavailable/defunct.
    /// </summary>
    HostUnavailable = 5,

    /// <summary>
    /// Failed to parse the host page.
    /// </summary>
    ParseError = 6,

    /// <summary>
    /// Network/connection error.
    /// </summary>
    NetworkError = 7,

    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout = 8,

    /// <summary>
    /// CAPTCHA or other verification required.
    /// </summary>
    CaptchaRequired = 9,

    /// <summary>
    /// The URL is not supported by this resolver.
    /// </summary>
    NotSupported = 10,

    /// <summary>
    /// Unknown error.
    /// </summary>
    Unknown = 99
}
