using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Providers;

/// <summary>
/// Registry and manager for all configured providers.
/// Handles CRUD operations, priority ordering, and lifecycle.
/// </summary>
public interface IProviderManager
{
    /// <summary>
    /// Get all provider definitions.
    /// </summary>
    Task<IReadOnlyList<ProviderDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get provider definitions by category.
    /// </summary>
    Task<IReadOnlyList<ProviderDefinition>> GetByCategoryAsync(ProviderCategory category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a provider definition by ID.
    /// </summary>
    Task<ProviderDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get enabled providers by category, ordered by priority.
    /// </summary>
    Task<IReadOnlyList<ProviderDefinition>> GetEnabledAsync(ProviderCategory category, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new provider definition.
    /// </summary>
    Task<ProviderDefinition> CreateAsync(ProviderDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing provider definition.
    /// </summary>
    Task<ProviderDefinition> UpdateAsync(ProviderDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a provider definition.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enable or disable a provider.
    /// </summary>
    Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update provider priority.
    /// </summary>
    Task<bool> SetPriorityAsync(int id, int priority, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reorder providers by updating their priorities.
    /// </summary>
    Task ReorderAsync(ProviderCategory category, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test a provider configuration.
    /// </summary>
    Task<ProviderTestResult> TestAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test a provider configuration before saving.
    /// </summary>
    Task<ProviderTestResult> TestAsync(ProviderDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update the health status of a provider.
    /// </summary>
    Task UpdateHealthAsync(int id, HealthStatus status, string? error = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all enabled indexer providers, instantiated and ready to use.
    /// </summary>
    Task<IReadOnlyList<IIndexerProvider>> GetIndexersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all enabled download providers, instantiated and ready to use.
    /// </summary>
    Task<IReadOnlyList<IDownloadProvider>> GetDownloadClientsAsync(CancellationToken cancellationToken = default);
}

