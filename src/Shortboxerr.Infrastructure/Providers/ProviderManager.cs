using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// Provider manager implementation with EF Core persistence.
/// </summary>
public class ProviderManager : IProviderManager
{
    private readonly ShortboxerrDbContext _context;
    private readonly IProviderFactory _providerFactory;

    public ProviderManager(ShortboxerrDbContext context, IProviderFactory providerFactory)
    {
        _context = context;
        _providerFactory = providerFactory;
    }

    public async Task<IReadOnlyList<ProviderDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Providers
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderDefinition>> GetByCategoryAsync(ProviderCategory category, CancellationToken cancellationToken = default)
    {
        return await _context.Providers
            .Where(p => p.Category == category)
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Providers.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderDefinition>> GetEnabledAsync(ProviderCategory category, CancellationToken cancellationToken = default)
    {
        return await _context.Providers
            .Where(p => p.Category == category && p.IsEnabled)
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderDefinition> CreateAsync(ProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        // Set initial timestamps
        definition.CreatedAt = DateTime.UtcNow;
        definition.UpdatedAt = DateTime.UtcNow;
        
        // Calculate next priority if not set
        if (definition.Priority == 0)
        {
            var maxPriority = await _context.Providers
                .Where(p => p.Category == definition.Category)
                .MaxAsync(p => (int?)p.Priority, cancellationToken) ?? 0;
            definition.Priority = maxPriority + 1;
        }
        
        _context.Providers.Add(definition);
        await _context.SaveChangesAsync(cancellationToken);
        
        return definition;
    }

    public async Task<ProviderDefinition> UpdateAsync(ProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Providers.FindAsync(new object[] { definition.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Provider with ID {definition.Id} not found");
        }
        
        // Update fields
        existing.Name = definition.Name;
        existing.IsEnabled = definition.IsEnabled;
        existing.Priority = definition.Priority;
        existing.Settings = definition.Settings;
        existing.BaseUrl = definition.BaseUrl;
        existing.ApiKey = definition.ApiKey;
        existing.Username = definition.Username;
        existing.Password = definition.Password;
        existing.Tags = definition.Tags;
        existing.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _context.Providers.FindAsync(new object[] { id }, cancellationToken);
        if (provider == null)
        {
            return false;
        }
        
        _context.Providers.Remove(provider);
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var provider = await _context.Providers.FindAsync(new object[] { id }, cancellationToken);
        if (provider == null)
        {
            return false;
        }
        
        provider.IsEnabled = enabled;
        provider.UpdatedAt = DateTime.UtcNow;
        
        if (!enabled)
        {
            provider.LastHealthStatus = HealthStatus.Disabled;
        }
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<bool> SetPriorityAsync(int id, int priority, CancellationToken cancellationToken = default)
    {
        var provider = await _context.Providers.FindAsync(new object[] { id }, cancellationToken);
        if (provider == null)
        {
            return false;
        }
        
        provider.Priority = priority;
        provider.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task ReorderAsync(ProviderCategory category, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        var providers = await _context.Providers
            .Where(p => p.Category == category)
            .ToListAsync(cancellationToken);
        
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var provider = providers.FirstOrDefault(p => p.Id == orderedIds[i]);
            if (provider != null)
            {
                provider.Priority = i + 1;
                provider.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderTestResult> TestAsync(int id, CancellationToken cancellationToken = default)
    {
        var definition = await GetByIdAsync(id, cancellationToken);
        if (definition == null)
        {
            return ProviderTestResult.Fail($"Provider with ID {id} not found");
        }
        
        var result = await TestInternalAsync(definition, cancellationToken);
        
        // Update health status based on test result
        await UpdateHealthAsync(
            id, 
            result.Success ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            result.Success ? null : result.Message,
            cancellationToken);
        
        return result;
    }

    public async Task<ProviderTestResult> TestAsync(ProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        // For unsaved providers, just run the test without updating health
        return await TestInternalAsync(definition, cancellationToken);
    }

    private async Task<ProviderTestResult> TestInternalAsync(ProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = _providerFactory.Create(definition);
            if (provider == null)
            {
                return ProviderTestResult.Fail($"Unable to create provider instance for type: {definition.Implementation}");
            }
            
            return await provider.TestAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return ProviderTestResult.Fail($"Test failed: {ex.Message}", ex.ToString());
        }
    }

    public async Task UpdateHealthAsync(int id, HealthStatus status, string? error = null, CancellationToken cancellationToken = default)
    {
        var provider = await _context.Providers.FindAsync(new object[] { id }, cancellationToken);
        if (provider == null)
        {
            return;
        }
        
        provider.LastHealthStatus = status;
        provider.LastHealthCheck = DateTime.UtcNow;
        provider.LastError = error;
        
        if (status == HealthStatus.Healthy)
        {
            provider.FailureCount = 0;
        }
        else if (status == HealthStatus.Unhealthy)
        {
            provider.FailureCount++;
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IIndexerProvider>> GetIndexersAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await GetEnabledAsync(ProviderCategory.Indexer, cancellationToken);
        var indexers = new List<IIndexerProvider>();
        
        foreach (var def in definitions)
        {
            var provider = _providerFactory.Create(def);
            if (provider is IIndexerProvider indexer)
            {
                indexers.Add(indexer);
            }
        }
        
        return indexers;
    }

    public async Task<IReadOnlyList<IDownloadProvider>> GetDownloadClientsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await GetEnabledAsync(ProviderCategory.DownloadClient, cancellationToken);
        var clients = new List<IDownloadProvider>();
        
        foreach (var def in definitions)
        {
            var provider = _providerFactory.Create(def);
            if (provider is IDownloadProvider client)
            {
                clients.Add(client);
            }
        }
        
        return clients;
    }
}



