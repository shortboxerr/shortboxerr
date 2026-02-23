using System.Reflection;

namespace Shortboxerr.Infrastructure.Http;

/// <summary>
/// Default configuration values for HTTP clients.
/// </summary>
public static class HttpClientDefaults
{
    /// <summary>
    /// Application name used in User-Agent header.
    /// </summary>
    public const string ApplicationName = "Shortboxerr";

    /// <summary>
    /// Gets the application version from the assembly.
    /// </summary>
    public static string Version
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.1.0";
        }
    }

    /// <summary>
    /// Gets the default User-Agent header value.
    /// Format: "Shortboxerr/0.1.0 (+https://github.com/shortboxerr/shortboxerr)"
    /// </summary>
    public static string UserAgent => $"{ApplicationName}/{Version} (+https://github.com/shortboxerr/shortboxerr)";

    /// <summary>
    /// Default timeout for HTTP requests in seconds.
    /// </summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Default timeout for long-running HTTP requests (e.g., downloads) in seconds.
    /// </summary>
    public const int LongTimeoutSeconds = 300;
}
