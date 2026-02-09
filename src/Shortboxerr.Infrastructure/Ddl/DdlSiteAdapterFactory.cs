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

    private void RegisterBuiltInAdapters()
    {
        // Register mock/sample adapters for testing
        RegisterAdapter("MockDdl", () => new MockDdlSiteAdapter());
        RegisterAdapter("GettyComics", () => new GettyComicsSiteAdapter());
        
        // Register real DDL site adapters
        RegisterAdapter("GetComics", () => new GetComicsAdapter());
        RegisterAdapter("ReadComicOnline", () => new ReadComicOnlineAdapter());
        
        // Enable mock by default for development/testing
        // GetComics, ReadComicOnline, and real adapters should be enabled via configuration/UI in production
        EnableSite("MockDdl");
    }
}



