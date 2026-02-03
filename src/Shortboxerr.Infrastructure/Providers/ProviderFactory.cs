using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// Default provider factory implementation.
/// Uses service provider for dependency injection into provider instances.
/// </summary>
public class ProviderFactory : IProviderFactory
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Func<ProviderDefinition, IProvider>> _factories = new();
    private readonly List<ProviderImplementation> _implementations = new();

    public ProviderFactory(IServiceProvider services)
    {
        _services = services;
        RegisterBuiltInProviders();
    }

    private void RegisterBuiltInProviders()
    {
        // NOTE: DDL Indexers are NOT user-configurable providers.
        // They are built-in services with Mylar3 parity, handling:
        // - Site adapters (GetComics, etc.) - configured via DDL Settings
        // - Release parsing, filtering, candidate normalization
        // - Search aggregation across multiple sites
        // See: IDdlSearchService, IDdlSiteAdapter, IDdlFilter registered as services in DI.
        // 225+ unit tests cover DDL functionality.

        // NOTE: HTTP Download Client is also a built-in internal service.
        // See: IHttpDownloadClient registered as a singleton service in DI.

        // Register RSS Provider for external RSS/Atom feeds
        // This IS user-configurable since users provide their own feed URLs
        RegisterProvider(
            name: "RssIndexer",
            displayName: "RSS Feed",
            description: "RSS/Atom feed indexer for new releases",
            category: ProviderCategory.Indexer,
            type: ProviderType.Rss,
            requiresBaseUrl: true,
            requiresCredentials: false,
            factory: def => new NullIndexerProvider(def)
        );
    }

    private void RegisterProvider(
        string name,
        string displayName,
        string description,
        ProviderCategory category,
        ProviderType type,
        bool requiresBaseUrl,
        bool requiresCredentials,
        Func<ProviderDefinition, IProvider> factory,
        bool requiresApiKey = false,
        string? settingsSchema = null)
    {
        _implementations.Add(new ProviderImplementation
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Category = category,
            Type = type,
            RequiresBaseUrl = requiresBaseUrl,
            RequiresCredentials = requiresCredentials,
            RequiresApiKey = requiresApiKey,
            SettingsSchema = settingsSchema
        });
        
        _factories[name] = factory;
    }

    public IProvider? Create(ProviderDefinition definition)
    {
        if (_factories.TryGetValue(definition.Implementation, out var factory))
        {
            return factory(definition);
        }
        
        return null;
    }

    public IReadOnlyList<ProviderImplementation> GetImplementations()
    {
        return _implementations.AsReadOnly();
    }

    public ProviderImplementation? GetImplementation(string name)
    {
        return _implementations.FirstOrDefault(i => i.Name == name);
    }
}

/// <summary>
/// Null indexer provider for placeholder implementations.
/// </summary>
internal class NullIndexerProvider : IIndexerProvider
{
    private readonly ProviderDefinition _definition;

    public NullIndexerProvider(ProviderDefinition definition)
    {
        _definition = definition;
    }

    public int Id => _definition.Id;
    public string Name => _definition.Name;
    public ProviderType Type => _definition.Type;
    public bool IsEnabled { get => _definition.IsEnabled; set => _definition.IsEnabled = value; }
    public int Priority { get => _definition.Priority; set => _definition.Priority = value; }
    public bool SupportsRss => false;
    public bool SupportsSearch => true;

    public Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProviderTestResult.Fail(
            $"Provider '{_definition.Implementation}' is not yet implemented"));
    }

    public Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderHealth
        {
            Status = HealthStatus.Unknown,
            Message = "Not implemented"
        });
    }

    public Task<IndexerSearchResult> SearchAsync(IndexerSearchQuery query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IndexerSearchResult.Fail("Not implemented"));
    }

    public Task<IndexerSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IndexerSearchResult.Fail("Not implemented"));
    }
}

/// <summary>
/// Null download provider for placeholder implementations.
/// </summary>
internal class NullDownloadProvider : IDownloadProvider
{
    private readonly ProviderDefinition _definition;

    public NullDownloadProvider(ProviderDefinition definition)
    {
        _definition = definition;
    }

    public int Id => _definition.Id;
    public string Name => _definition.Name;
    public ProviderType Type => _definition.Type;
    public bool IsEnabled { get => _definition.IsEnabled; set => _definition.IsEnabled = value; }
    public int Priority { get => _definition.Priority; set => _definition.Priority = value; }
    public IReadOnlyList<string> SupportedProtocols => new[] { "http", "https" };

    public Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProviderTestResult.Fail(
            $"Provider '{_definition.Implementation}' is not yet implemented"));
    }

    public Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderHealth
        {
            Status = HealthStatus.Unknown,
            Message = "Not implemented"
        });
    }

    public Task<Core.Providers.DownloadResult> DownloadAsync(Core.Models.Candidate candidate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Core.Providers.DownloadResult.Fail("Not implemented"));
    }

    public Task<DownloadStatus> GetStatusAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DownloadStatus
        {
            DownloadId = downloadId,
            State = DownloadState.Failed,
            Error = "Not implemented"
        });
    }

    public Task<bool> CancelAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DownloadStatus>>(Array.Empty<DownloadStatus>());
    }
}

