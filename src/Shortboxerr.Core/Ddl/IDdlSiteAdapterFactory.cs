namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Factory for creating and managing DDL site adapters.
/// </summary>
public interface IDdlSiteAdapterFactory
{
    /// <summary>
    /// Get an adapter by site type.
    /// </summary>
    IDdlSiteAdapter GetAdapter(string siteType);
    
    /// <summary>
    /// Get all registered site types.
    /// </summary>
    IReadOnlyList<string> GetRegisteredSiteTypes();
    
    /// <summary>
    /// Get all enabled site types (based on provider configuration).
    /// </summary>
    IReadOnlyList<string> GetEnabledSites();
    
    /// <summary>
    /// Get information about all available sites.
    /// </summary>
    IReadOnlyList<DdlSiteInfo> GetAvailableSiteInfos();
    
    /// <summary>
    /// Try to determine site type from a URL.
    /// </summary>
    string? GetSiteTypeFromUrl(string url);
    
    /// <summary>
    /// Register an adapter for a site type.
    /// </summary>
    void RegisterAdapter(string siteType, Func<IDdlSiteAdapter> factory);
    
    /// <summary>
    /// Check if a site type is registered.
    /// </summary>
    bool IsRegistered(string siteType);
}



