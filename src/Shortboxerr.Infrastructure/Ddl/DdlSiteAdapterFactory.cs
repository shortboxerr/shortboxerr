using System.Collections.Concurrent;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Factory for creating and managing DDL site adapters.
/// </summary>
public class DdlSiteAdapterFactory : IDdlSiteAdapterFactory
{
    private readonly ConcurrentDictionary<string, Func<IDdlSiteAdapter>> _adapterFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IDdlSiteAdapter> _adapterCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabledSites = new(StringComparer.OrdinalIgnoreCase);
    
    public DdlSiteAdapterFactory()
    {
        // Register built-in adapters
        RegisterBuiltInAdapters();
    }

    public IDdlSiteAdapter GetAdapter(string siteType)
    {
        if (!_adapterFactories.TryGetValue(siteType, out var factory))
        {
            throw new ArgumentException($"Unknown site type: {siteType}", nameof(siteType));
        }
        
        // Cache adapters for reuse
        return _adapterCache.GetOrAdd(siteType, _ => factory());
    }

    public IReadOnlyList<string> GetRegisteredSiteTypes()
    {
        return _adapterFactories.Keys.ToList();
    }

    public IReadOnlyList<string> GetEnabledSites()
    {
        // Return all registered sites for now; in production this would be filtered
        // by provider configurations from the database
        return _enabledSites.Count > 0 
            ? _enabledSites.ToList() 
            : _adapterFactories.Keys.ToList();
    }

    public IReadOnlyList<DdlSiteInfo> GetAvailableSiteInfos()
    {
        return _adapterFactories.Keys
            .Select(siteType =>
            {
                var adapter = GetAdapter(siteType);
                return new DdlSiteInfo
                {
                    SiteType = adapter.SiteType,
                    DisplayName = adapter.DisplayName,
                    DefaultBaseUrl = adapter.DefaultBaseUrl,
                    RequiresAuthentication = adapter.RequiresAuthentication,
                    DefaultRateLimitPerMinute = adapter.DefaultRateLimitPerMinute
                };
            })
            .ToList();
    }

    public string? GetSiteTypeFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }
        
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        var host = uri.IsAbsoluteUri ? uri.Host.ToLowerInvariant() : url.ToLowerInvariant();
        
        foreach (var siteType in _adapterFactories.Keys)
        {
            var adapter = GetAdapter(siteType);
            if (!string.IsNullOrEmpty(adapter.DefaultBaseUrl))
            {
                try
                {
                    var baseUri = new Uri(adapter.DefaultBaseUrl);
                    if (host.Contains(baseUri.Host.ToLowerInvariant()))
                    {
                        return siteType;
                    }
                }
                catch
                {
                    // Ignore invalid URLs
                }
            }
        }
        
        return null;
    }

    public void RegisterAdapter(string siteType, Func<IDdlSiteAdapter> factory)
    {
        _adapterFactories[siteType] = factory;
    }

    public bool IsRegistered(string siteType)
    {
        return _adapterFactories.ContainsKey(siteType);
    }

    /// <summary>
    /// Enable a site for searching.
    /// </summary>
    public void EnableSite(string siteType)
    {
        if (_adapterFactories.ContainsKey(siteType))
        {
            _enabledSites.Add(siteType);
        }
    }

    /// <summary>
    /// Disable a site from searching.
    /// </summary>
    public void DisableSite(string siteType)
    {
        _enabledSites.Remove(siteType);
    }

    /// <summary>
    /// Check if a site is currently enabled.
    /// </summary>
    public bool IsSiteEnabled(string siteType)
    {
        return _enabledSites.Contains(siteType);
    }

    /// <summary>
    /// Get site information including enabled status.
    /// </summary>
    public IReadOnlyList<DdlSiteStatus> GetSiteStatuses()
    {
        return _adapterFactories.Keys
            .Select(siteType =>
            {
                var adapter = GetAdapter(siteType);
                return new DdlSiteStatus
                {
                    SiteType = adapter.SiteType,
                    DisplayName = adapter.DisplayName,
                    DefaultBaseUrl = adapter.DefaultBaseUrl,
                    RequiresAuthentication = adapter.RequiresAuthentication,
                    DefaultRateLimitPerMinute = adapter.DefaultRateLimitPerMinute,
                    IsEnabled = _enabledSites.Contains(siteType),
                    Priority = GetSitePriority(siteType)
                };
            })
            .OrderBy(s => s.Priority)
            .ToList();
    }

    /// <summary>
    /// Set enabled sites from a list (replaces current enabled set).
    /// </summary>
    public void SetEnabledSites(IEnumerable<string> siteTypes)
    {
        _enabledSites.Clear();
        foreach (var siteType in siteTypes.Where(s => _adapterFactories.ContainsKey(s)))
        {
            _enabledSites.Add(siteType);
        }
    }

    /// <summary>
    /// Get the priority of a site (lower = higher priority).
    /// </summary>
    private int GetSitePriority(string siteType)
    {
        // Default priorities - can be overridden via settings later
        return siteType switch
        {
            "GetComics" => 1,        // Primary - most comprehensive
            "ReadComicOnline" => 2,  // Secondary - good backup
            "GettyComics" => 3,      // Legacy/test
            "MockDdl" => 99,         // Test only
            _ => 50
        };
    }

    private void RegisterBuiltInAdapters()
    {
        // Register mock/sample adapters for testing
        RegisterAdapter("MockDdl", () => new MockDdlSiteAdapter());
        RegisterAdapter("GettyComics", () => new GettyComicsSiteAdapter());
        
        // Register real DDL site adapters
        RegisterAdapter("GetComics", () => new GetComicsAdapter());
        RegisterAdapter("ReadComicOnline", () => new ReadComicOnlineAdapter());
        
        // Enable real DDL sites by default for production use
        EnableSite("GetComics");
        EnableSite("ReadComicOnline");
        
        // Note: MockDdl is available but not enabled by default
        // Enable it via settings or environment variable for testing:
        // Environment.GetEnvironmentVariable("SHORTBOXERR_ENABLE_MOCK_DDL") == "true"
        if (Environment.GetEnvironmentVariable("SHORTBOXERR_ENABLE_MOCK_DDL") == "true")
        {
            EnableSite("MockDdl");
        }
    }
}

/// <summary>
/// Extended site information including runtime status.
/// </summary>
public class DdlSiteStatus : DdlSiteInfo
{
    /// <summary>
    /// Whether this site is currently enabled for searches.
    /// </summary>
    public bool IsEnabled { get; init; }
    
    /// <summary>
    /// Priority order for multi-site searches (lower = higher priority).
    /// </summary>
    public int Priority { get; init; }
    
    /// <summary>
    /// Last health check result.
    /// </summary>
    public DdlSiteHealth Health { get; init; } = DdlSiteHealth.Unknown;
    
    /// <summary>
    /// Last error message if unhealthy.
    /// </summary>
    public string? LastError { get; init; }
    
    /// <summary>
    /// When the last successful search was performed.
    /// </summary>
    public DateTime? LastSuccessfulSearch { get; init; }
}

/// <summary>
/// Health status for a DDL site.
/// </summary>
public enum DdlSiteHealth
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3
}



