namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Factory for creating and managing download host resolvers.
/// Provides the appropriate resolver for a given URL.
/// </summary>
public interface IDownloadHostResolverFactory
{
    /// <summary>
    /// Gets the best resolver for the given URL.
    /// Returns null if no resolver can handle the URL.
    /// </summary>
    IDownloadHostResolver? GetResolver(string url);

    /// <summary>
    /// Gets all resolvers that can handle the given URL, ordered by priority.
    /// </summary>
    IReadOnlyList<IDownloadHostResolver> GetResolvers(string url);

    /// <summary>
    /// Gets all registered resolvers.
    /// </summary>
    IReadOnlyList<IDownloadHostResolver> GetAllResolvers();

    /// <summary>
    /// Gets all available (non-defunct) resolvers.
    /// </summary>
    IReadOnlyList<IDownloadHostResolver> GetAvailableResolvers();

    /// <summary>
    /// Checks if any resolver can handle the given URL.
    /// </summary>
    bool CanResolve(string url);

    /// <summary>
    /// Registers a custom resolver.
    /// </summary>
    void RegisterResolver(IDownloadHostResolver resolver);

    /// <summary>
    /// Gets information about all registered hosts.
    /// </summary>
    IReadOnlyList<HostInfo> GetHostInfos();
}

/// <summary>
/// Information about a supported file host.
/// </summary>
public class HostInfo
{
    /// <summary>
    /// Unique identifier for the host.
    /// </summary>
    public required string HostId { get; init; }

    /// <summary>
    /// Display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// URL patterns this host handles.
    /// </summary>
    public required IReadOnlyList<string> SupportedHosts { get; init; }

    /// <summary>
    /// Priority (lower = better).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Whether this host is currently available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Note about this host (e.g., "Defunct since 2023").
    /// </summary>
    public string? Note { get; init; }
}
