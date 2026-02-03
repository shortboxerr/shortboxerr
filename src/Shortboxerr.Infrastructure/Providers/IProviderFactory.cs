using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// Factory for creating provider instances from definitions.
/// </summary>
public interface IProviderFactory
{
    /// <summary>
    /// Create a provider instance from a definition.
    /// </summary>
    IProvider? Create(ProviderDefinition definition);
    
    /// <summary>
    /// Get all available provider implementations.
    /// </summary>
    IReadOnlyList<ProviderImplementation> GetImplementations();
    
    /// <summary>
    /// Get implementation info by name.
    /// </summary>
    ProviderImplementation? GetImplementation(string name);
}

/// <summary>
/// Information about an available provider implementation.
/// </summary>
public class ProviderImplementation
{
    /// <summary>
    /// Implementation name (used in ProviderDefinition.Implementation).
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Display name for UI.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Description of this provider type.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Provider category.
    /// </summary>
    public ProviderCategory Category { get; init; }
    
    /// <summary>
    /// Provider type.
    /// </summary>
    public ProviderType Type { get; init; }
    
    /// <summary>
    /// Schema for the settings JSON.
    /// </summary>
    public string? SettingsSchema { get; init; }
    
    /// <summary>
    /// Whether this implementation requires a base URL.
    /// </summary>
    public bool RequiresBaseUrl { get; init; }
    
    /// <summary>
    /// Whether this implementation requires API key auth.
    /// </summary>
    public bool RequiresApiKey { get; init; }
    
    /// <summary>
    /// Whether this implementation requires username/password auth.
    /// </summary>
    public bool RequiresCredentials { get; init; }
}



