using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Infrastructure.Activity;

/// <summary>
/// Aggregates download activity from all configured download providers.
/// Provides a unified view of DDL, NZB, and Torrent downloads.
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IProviderManager _providerManager;
    private readonly ILogger<ActivityService>? _logger;

    // In-memory history for recently completed/failed downloads
    // Static to share across all scoped instances (since DdlDownloadService resolves in different scopes)
    // In a production system, this would be persisted to the database
    private static readonly List<DownloadActivity> _history = new();
    private static readonly object _historyLock = new();
    private const int MaxHistoryItems = 100;

    public ActivityService(IProviderManager providerManager, ILogger<ActivityService>? logger = null)
    {
        _providerManager = providerManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DownloadActivity>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        var activities = new List<DownloadActivity>();

        try
        {
            // Get all enabled download providers
            var providers = await _providerManager.GetDownloadClientsAsync(cancellationToken);

            foreach (var provider in providers)
            {
                try
                {
                    var statuses = await provider.GetActiveDownloadsAsync(cancellationToken);
                    
                    foreach (var status in statuses)
                    {
                        activities.Add(MapToActivity(status, provider, provider.Name));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to get active downloads from {Provider}", provider.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get active downloads");
        }

        return activities.OrderByDescending(a => a.StartedAt).ToList();
    }

    public Task<IReadOnlyList<DownloadActivity>> GetRecentHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var result = _history
                .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<DownloadActivity>>(result);
        }
    }

    public async Task<DownloadActivity?> GetByIdAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        // Check active downloads first
        var active = await GetActiveDownloadsAsync(cancellationToken);
        var found = active.FirstOrDefault(a => a.Id == downloadId);
        if (found != null)
            return found;

        // Check history
        lock (_historyLock)
        {
            return _history.FirstOrDefault(a => a.Id == downloadId);
        }
    }

    public async Task<ActivitySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveDownloadsAsync(cancellationToken);
        
        IReadOnlyList<DownloadActivity> history;
        lock (_historyLock)
        {
            history = _history.ToList();
        }

        var downloading = active.Where(a => a.State == ActivityState.Downloading).ToList();

        return new ActivitySummary
        {
            ActiveCount = downloading.Count,
            QueuedCount = active.Count(a => a.State == ActivityState.Queued),
            CompletedCount = history.Count(a => a.State == ActivityState.Completed),
            FailedCount = history.Count(a => a.State == ActivityState.Failed),
            TotalSpeedBytesPerSecond = downloading.Sum(a => a.SpeedBytesPerSecond ?? 0),
            BySourceType = active
                .GroupBy(a => a.SourceType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<bool> PauseAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var provider = await FindProviderForDownloadAsync(downloadId, cancellationToken);
        if (provider == null)
            return false;

        // For now, only NZB/Torrent clients support pause
        // We'd need to add a pause method to IDownloadProvider interface
        _logger?.LogInformation("Pause requested for {DownloadId}", downloadId);
        return false; // Not implemented in base interface yet
    }

    public async Task<bool> ResumeAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var provider = await FindProviderForDownloadAsync(downloadId, cancellationToken);
        if (provider == null)
            return false;

        _logger?.LogInformation("Resume requested for {DownloadId}", downloadId);
        return false; // Not implemented in base interface yet
    }

    public async Task<bool> CancelAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        var provider = await FindProviderForDownloadAsync(downloadId, cancellationToken);
        if (provider == null)
            return false;

        try
        {
            var result = await provider.CancelAsync(downloadId, cancellationToken);
            if (result)
            {
                AddToHistory(new DownloadActivity
                {
                    Id = downloadId,
                    ClientName = provider.Name,
                    Title = downloadId,
                    State = ActivityState.Cancelled,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    SourceType = MapProviderType(provider.Type)
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to cancel download {DownloadId}", downloadId);
            return false;
        }
    }

    public Task<bool> RetryAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        // Retry would need to re-add the download to the queue
        // This requires knowing the original download details
        _logger?.LogInformation("Retry requested for {DownloadId} - not implemented yet", downloadId);
        return Task.FromResult(false);
    }

    public Task<bool> RemoveFromHistoryAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var item = _history.FirstOrDefault(a => a.Id == downloadId);
            if (item != null)
            {
                _history.Remove(item);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var completed = _history.Where(a => a.State == ActivityState.Completed).ToList();
            foreach (var item in completed)
            {
                _history.Remove(item);
            }
            return Task.FromResult(completed.Count);
        }
    }

    /// <summary>
    /// Adds a completed/failed activity to history.
    /// Called by download services when downloads complete.
    /// </summary>
    public void AddToHistory(DownloadActivity activity)
    {
        lock (_historyLock)
        {
            _history.Insert(0, activity);

            // Trim history if too large
            while (_history.Count > MaxHistoryItems)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
    }

    private async Task<IDownloadProvider?> FindProviderForDownloadAsync(string downloadId, CancellationToken cancellationToken)
    {
        var providers = await _providerManager.GetDownloadClientsAsync(cancellationToken);

        foreach (var provider in providers)
        {
            var status = await provider.GetStatusAsync(downloadId, cancellationToken);
            if (status != null && status.State != DownloadState.Unknown)
            {
                return provider;
            }
        }

        return null;
    }

    private static DownloadActivity MapToActivity(DownloadStatus status, IDownloadProvider provider, string providerName)
    {
        return new DownloadActivity
        {
            Id = status.DownloadId,
            SourceType = MapProviderType(provider.Type),
            ClientName = providerName,
            ProviderId = provider.Id,
            Title = status.CandidateTitle ?? status.DownloadId,
            State = MapState(status.State),
            Progress = status.Progress,
            TotalBytes = status.TotalBytes,
            DownloadedBytes = status.DownloadedBytes,
            SpeedBytesPerSecond = status.SpeedBytesPerSecond,
            EstimatedTimeRemaining = status.EstimatedTimeRemaining,
            StartedAt = status.StartedAt,
            CompletedAt = status.CompletedAt,
            ErrorMessage = status.Error,
            RetryCount = status.RetryCount,
            OutputPath = status.OutputPath,
            SourceUrl = status.SourceUrl
        };
    }

    private static DownloadSourceType MapProviderType(ProviderType type)
    {
        return type switch
        {
            ProviderType.Usenet => DownloadSourceType.Nzb,
            ProviderType.Torrent => DownloadSourceType.Torrent,
            _ => DownloadSourceType.Ddl
        };
    }

    private static ActivityState MapState(DownloadState state)
    {
        return state switch
        {
            DownloadState.Queued => ActivityState.Queued,
            DownloadState.Downloading => ActivityState.Downloading,
            DownloadState.Paused => ActivityState.Paused,
            DownloadState.Completed => ActivityState.Completed,
            DownloadState.Failed => ActivityState.Failed,
            DownloadState.Cancelled => ActivityState.Cancelled,
            DownloadState.Retrying => ActivityState.Retrying,
            DownloadState.Processing => ActivityState.Processing,
            DownloadState.Stalled => ActivityState.Stalled,
            DownloadState.Unknown => ActivityState.Warning,
            _ => ActivityState.Warning
        };
    }
}
