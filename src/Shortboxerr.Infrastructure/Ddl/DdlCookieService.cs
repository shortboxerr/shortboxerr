using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for managing persistent cookies for DDL sites.
/// Stores cookies as JSON files (like Mylar3's .gc_cookies.dat pattern).
/// </summary>
public class DdlCookieService : IDdlCookieService
{
    private readonly ILogger<DdlCookieService>? _logger;
    private readonly string _cookieDirectory;
    private readonly TimeSpan _cookieExpiry;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public DdlCookieService(ILogger<DdlCookieService>? logger = null, string? cookieDirectory = null, TimeSpan? cookieExpiry = null)
    {
        _logger = logger;
        _cookieDirectory = cookieDirectory ?? GetDefaultCookieDirectory();
        _cookieExpiry = cookieExpiry ?? TimeSpan.FromDays(7); // Default 7 day expiry like Mylar3
        
        Directory.CreateDirectory(_cookieDirectory);
    }
    
    public async Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(string siteType, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCookieFilePath(siteType);
            
            if (!File.Exists(filePath))
            {
                _logger?.LogDebug("No cookie file exists for {Site}", siteType);
                return new Dictionary<string, string>();
            }
            
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var cookieData = JsonSerializer.Deserialize<CookieFileData>(json);
            
            if (cookieData == null)
            {
                _logger?.LogWarning("Failed to deserialize cookie file for {Site}", siteType);
                return new Dictionary<string, string>();
            }
            
            // Check if cookies have expired
            if (DateTime.UtcNow > cookieData.ExpiresAt)
            {
                _logger?.LogDebug("Cookies for {Site} have expired, clearing", siteType);
                await ClearCookiesInternalAsync(filePath);
                return new Dictionary<string, string>();
            }
            
            _logger?.LogDebug("Loaded {Count} cookies for {Site}", cookieData.Cookies.Count, siteType);
            return cookieData.Cookies;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error loading cookies for {Site}", siteType);
            return new Dictionary<string, string>();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SaveCookiesAsync(string siteType, IReadOnlyDictionary<string, string> cookies, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCookieFilePath(siteType);
            
            var cookieData = new CookieFileData
            {
                SiteType = siteType,
                Cookies = cookies.ToDictionary(kv => kv.Key, kv => kv.Value),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_cookieExpiry)
            };
            
            var json = JsonSerializer.Serialize(cookieData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            _logger?.LogDebug("Saved {Count} cookies for {Site}", cookies.Count, siteType);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error saving cookies for {Site}", siteType);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task ClearCookiesAsync(string siteType, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetCookieFilePath(siteType);
            await ClearCookiesInternalAsync(filePath);
            _logger?.LogDebug("Cleared cookies for {Site}", siteType);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<bool> HasValidCookiesAsync(string siteType, CancellationToken cancellationToken = default)
    {
        var cookies = await GetCookiesAsync(siteType, cancellationToken);
        return cookies.Count > 0;
    }
    
    private string GetCookieFilePath(string siteType)
    {
        var safeFileName = string.Join("_", siteType.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cookieDirectory, $".{safeFileName.ToLowerInvariant()}_cookies.json");
    }
    
    private static Task ClearCookiesInternalAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
    
    private static string GetDefaultCookieDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "shortboxerr", "cookies");
    }
    
    private class CookieFileData
    {
        public string SiteType { get; set; } = string.Empty;
        public Dictionary<string, string> Cookies { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
