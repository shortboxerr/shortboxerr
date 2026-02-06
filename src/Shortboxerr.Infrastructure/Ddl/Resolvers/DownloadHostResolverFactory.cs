using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Factory for creating and managing download host resolvers.
/// Provides the appropriate resolver based on URL patterns.
/// </summary>
public class DownloadHostResolverFactory : IDownloadHostResolverFactory
{
    private readonly ConcurrentDictionary<string, IDownloadHostResolver> _resolvers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILoggerFactory? _loggerFactory;

    public DownloadHostResolverFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        RegisterBuiltInResolvers();
    }

    public IDownloadHostResolver? GetResolver(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return _resolvers.Values
            .Where(r => r.IsAvailable && r.CanResolve(url))
            .OrderBy(r => r.Priority)
            .FirstOrDefault();
    }

    public IReadOnlyList<IDownloadHostResolver> GetResolvers(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Array.Empty<IDownloadHostResolver>();
        }

        return _resolvers.Values
            .Where(r => r.IsAvailable && r.CanResolve(url))
            .OrderBy(r => r.Priority)
            .ToList();
    }

    public IReadOnlyList<IDownloadHostResolver> GetAllResolvers()
    {
        return _resolvers.Values.OrderBy(r => r.Priority).ToList();
    }

    public IReadOnlyList<IDownloadHostResolver> GetAvailableResolvers()
    {
        return _resolvers.Values
            .Where(r => r.IsAvailable)
            .OrderBy(r => r.Priority)
            .ToList();
    }

    public bool CanResolve(string url)
    {
        return GetResolver(url) != null;
    }

    public void RegisterResolver(IDownloadHostResolver resolver)
    {
        _resolvers[resolver.HostId] = resolver;
    }

    public IReadOnlyList<HostInfo> GetHostInfos()
    {
        return _resolvers.Values
            .Select(r => new HostInfo
            {
                HostId = r.HostId,
                DisplayName = r.DisplayName,
                SupportedHosts = r.SupportedHosts,
                Priority = r.Priority,
                IsAvailable = r.IsAvailable
            })
            .OrderBy(h => h.Priority)
            .ToList();
    }

    private void RegisterBuiltInResolvers()
    {
        // Register resolvers in priority order
        // Lower priority = better (tried first)

        // Direct downloads - highest priority (handles direct file links)
        RegisterResolver(new DirectDownloadResolver(
            _loggerFactory?.CreateLogger<DirectDownloadResolver>()));

        // Mega.nz - Priority 1 (very reliable, fast)
        // Note: Mega resolver requires special handling (encryption) - not implemented yet
        // RegisterResolver(new MegaResolver(_loggerFactory?.CreateLogger<MegaResolver>()));

        // MediaFire - Priority 2 (common, reliable)
        RegisterResolver(new MediaFireResolver(
            _loggerFactory?.CreateLogger<MediaFireResolver>()));

        // Pixeldrain - Priority 3 (simple API, good speeds)
        RegisterResolver(new PixeldrainResolver(
            _loggerFactory?.CreateLogger<PixeldrainResolver>()));

        // Future resolvers (not yet implemented):
        // - Google Drive (Priority 4)
        // - Dropbox (Priority 5)
        // - 1fichier (Priority 6)
        // - Zippyshare (defunct, detect and skip)
    }

    /// <summary>
    /// Gets a resolver by its host ID.
    /// </summary>
    public IDownloadHostResolver? GetResolverById(string hostId)
    {
        return _resolvers.TryGetValue(hostId, out var resolver) ? resolver : null;
    }
}
