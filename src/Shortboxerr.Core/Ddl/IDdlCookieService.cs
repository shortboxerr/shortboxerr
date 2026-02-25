namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for managing persistent cookies for DDL sites.
/// Based on Mylar3's cookie receipt pattern that stores session cookies to disk.
/// </summary>
public interface IDdlCookieService
{
    /// <summary>
    /// Get cookies for a specific site.
    /// </summary>
    /// <param name="siteType">Site identifier (e.g., "GetComics")</param>
    /// <returns>Dictionary of cookie name to value</returns>
    Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save cookies for a specific site.
    /// </summary>
    /// <param name="siteType">Site identifier</param>
    /// <param name="cookies">Cookies to save</param>
    Task SaveCookiesAsync(string siteType, IReadOnlyDictionary<string, string> cookies, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear cookies for a specific site.
    /// </summary>
    /// <param name="siteType">Site identifier</param>
    Task ClearCookiesAsync(string siteType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if cookies exist and are still valid for a site.
    /// </summary>
    /// <param name="siteType">Site identifier</param>
    /// <returns>True if valid cookies exist</returns>
    Task<bool> HasValidCookiesAsync(string siteType, CancellationToken cancellationToken = default);
}
