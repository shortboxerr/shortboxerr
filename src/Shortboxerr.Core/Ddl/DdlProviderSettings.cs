using System.Text.Json;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Settings specific to a DDL provider instance.
/// Stored as JSON in ProviderDefinition.Settings.
/// </summary>
public class DdlProviderSettings
{
    /// <summary>
    /// Site type identifier (e.g., "GettyComics", "ReadComicOnline").
    /// </summary>
    public required string SiteType { get; set; }
    
    /// <summary>
    /// Rate limit: maximum requests per minute.
    /// Mylar3 default: 10 requests/minute for most sites.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 10;
    
    /// <summary>
    /// Request timeout in seconds.
    /// Mylar3 default: 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Download timeout in seconds.
    /// Mylar3 default: 300 seconds (5 minutes).
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// Maximum retry count for failed requests.
    /// Mylar3 default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Delay between retries in milliseconds.
    /// Mylar3 default: 1000ms (1 second).
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Whether to use exponential backoff for retries.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
    
    /// <summary>
    /// Custom User-Agent string (null = use default rotation).
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Whether to enable cookies/session handling.
    /// </summary>
    public bool EnableCookies { get; set; } = true;
    
    /// <summary>
    /// Custom cookies to send with requests (JSON object).
    /// </summary>
    public string? CustomCookies { get; set; }
    
    /// <summary>
    /// Custom headers to send with requests (JSON object).
    /// </summary>
    public string? CustomHeaders { get; set; }
    
    /// <summary>
    /// Whether authentication is required for this site.
    /// </summary>
    public bool RequiresAuth { get; set; }
    
    /// <summary>
    /// Authentication method (None, Basic, Cookie, ApiKey).
    /// </summary>
    public DdlAuthMethod AuthMethod { get; set; } = DdlAuthMethod.None;
    
    /// <summary>
    /// Login URL for cookie-based authentication.
    /// </summary>
    public string? LoginUrl { get; set; }
    
    /// <summary>
    /// Whether to auto-grab releases matching criteria.
    /// </summary>
    public bool AutoGrabEnabled { get; set; } = true;
    
    /// <summary>
    /// Minimum score for auto-grab (0-100).
    /// </summary>
    public int AutoGrabMinScore { get; set; } = 80;
    
    /// <summary>
    /// Whether to search for collections (TPB, HC, etc.).
    /// </summary>
    public bool SearchCollections { get; set; } = true;
    
    /// <summary>
    /// Whether to search for single issues.
    /// </summary>
    public bool SearchSingles { get; set; } = true;
    
    /// <summary>
    /// Preferred download format order (first = most preferred).
    /// </summary>
    public List<string> FormatPreference { get; set; } = new() { "cbz", "cbr" };
    
    /// <summary>
    /// Words that should reject a release.
    /// </summary>
    public List<string> BannedWords { get; set; } = new() { "sample", "preview" };
    
    /// <summary>
    /// Words that must be present in a release.
    /// </summary>
    public List<string> RequiredWords { get; set; } = new();
    
    /// <summary>
    /// Minimum file size for singles in bytes.
    /// </summary>
    public long MinSizeSingles { get; set; } = 1_000_000; // 1MB
    
    /// <summary>
    /// Maximum file size for singles in bytes.
    /// </summary>
    public long MaxSizeSingles { get; set; } = 200_000_000; // 200MB
    
    /// <summary>
    /// Minimum file size for collections in bytes.
    /// </summary>
    public long MinSizeCollections { get; set; } = 5_000_000; // 5MB
    
    /// <summary>
    /// Maximum file size for collections in bytes.
    /// </summary>
    public long MaxSizeCollections { get; set; } = 2_000_000_000; // 2GB
    
    /// <summary>
    /// Serialize to JSON for storage.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });
    
    /// <summary>
    /// Deserialize from JSON.
    /// </summary>
    public static DdlProviderSettings? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<DdlProviderSettings>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
    
    /// <summary>
    /// Create settings with Mylar3 defaults for a specific site type.
    /// </summary>
    public static DdlProviderSettings CreateMylar3Default(string siteType)
    {
        return siteType.ToLowerInvariant() switch
        {
            "gettycomics" => new DdlProviderSettings
            {
                SiteType = "GettyComics",
                RateLimitPerMinute = 10,
                TimeoutSeconds = 30,
                DownloadTimeoutSeconds = 300,
                MaxRetries = 3,
                RequiresAuth = false
            },
            "readcomiconline" => new DdlProviderSettings
            {
                SiteType = "ReadComicOnline",
                RateLimitPerMinute = 5, // More restrictive
                TimeoutSeconds = 45,
                DownloadTimeoutSeconds = 600,
                MaxRetries = 3,
                RequiresAuth = false,
                EnableCookies = true // RCO uses cookies
            },
            _ => new DdlProviderSettings
            {
                SiteType = siteType,
                RateLimitPerMinute = 10,
                TimeoutSeconds = 30,
                DownloadTimeoutSeconds = 300,
                MaxRetries = 3
            }
        };
    }
}

/// <summary>
/// Authentication methods for DDL sites.
/// </summary>
public enum DdlAuthMethod
{
    /// <summary>
    /// No authentication required.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// HTTP Basic authentication.
    /// </summary>
    Basic = 1,
    
    /// <summary>
    /// Cookie-based authentication (login form).
    /// </summary>
    Cookie = 2,
    
    /// <summary>
    /// API key authentication.
    /// </summary>
    ApiKey = 3,
    
    /// <summary>
    /// OAuth2 authentication.
    /// </summary>
    OAuth2 = 4
}

