using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Infrastructure.Activity;

/// <summary>
/// Aggregates download activity from all configured download providers.
/// Provides a unified view of DDL, NZB, and Torrent downloads.
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IProviderManager _providerManager;
    private readonly IDdlDownloadService? _ddlDownloadService;
    private readonly IDownloadHistoryService? _downloadHistoryService;
    private readonly ILogger<ActivityService>? _logger;

    // In-memory history for session-only items (will be backed by DB for persistence)
    private static readonly List<DownloadActivity> _sessionHistory = new();
    private static readonly object _historyLock = new();
    private const int MaxHistoryItems = 100;

    public ActivityService(
        IProviderManager providerManager, 
        IDdlDownloadService? ddlDownloadService = null,
        IDownloadHistoryService? downloadHistoryService = null,
        ILogger<ActivityService>? logger = null)
    {
        _providerManager = providerManager;
        _ddlDownloadService = ddlDownloadService;
        _downloadHistoryService = downloadHistoryService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DownloadActivity>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        var activities = new List<DownloadActivity>();

        try
        {
            // Get DDL active downloads
            if (_ddlDownloadService != null)
            {
                try
                {
                    var ddlDownloads = _ddlDownloadService.GetActiveDownloads();
                    foreach (var ddl in ddlDownloads)
                    {
                        activities.Add(MapDdlToActivity(ddl));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to get active DDL downloads");
                }
            }

            // Get all enabled download providers (NZB, Torrent)
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

    public async Task<IReadOnlyList<DownloadActivity>> GetRecentHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var allHistory = new List<DownloadActivity>();
        
        // Include session history (non-persisted items)
        lock (_historyLock)
        {
            allHistory.AddRange(_sessionHistory);
        }
        
        // Include persisted database history
        if (_downloadHistoryService != null)
        {
            try
            {
                var dbHistory = await _downloadHistoryService.GetRecentAsync(limit, cancellationToken);
                foreach (var entry in dbHistory)
                {
                    allHistory.Add(MapDbHistoryToActivity(entry));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get persisted download history");
            }
        }
        
        var result = allHistory
            .DistinctBy(a => a.Id)
            .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
            .Take(limit)
            .ToList();
        return result;
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
            return _sessionHistory.FirstOrDefault(a => a.Id == downloadId);
        }
    }

    public async Task<ActivitySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveDownloadsAsync(cancellationToken);
        
        IReadOnlyList<DownloadActivity> history;
        lock (_historyLock)
        {
            history = _sessionHistory.ToList();
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

    public async Task<bool> RemoveFromHistoryAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        // Remove from session history
        lock (_historyLock)
        {
            var item = _sessionHistory.FirstOrDefault(a => a.Id == downloadId);
            if (item != null)
            {
                _sessionHistory.Remove(item);
            }
        }
        
        // Also try to find and remove from persisted history
        if (_downloadHistoryService != null)
        {
            var entry = await _downloadHistoryService.GetByDownloadIdAsync(downloadId, cancellationToken);
            if (entry != null)
            {
                await _downloadHistoryService.RemoveAsync(entry.Id, cancellationToken);
                return true;
            }
        }
        return false;
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        
        // Clear session history
        lock (_historyLock)
        {
            var completed = _sessionHistory.Where(a => a.State == ActivityState.Completed).ToList();
            foreach (var item in completed)
            {
                _sessionHistory.Remove(item);
            }
            count = completed.Count;
        }
        
        // Clear persisted history
        if (_downloadHistoryService != null)
        {
            count += await _downloadHistoryService.ClearCompletedAsync(cancellationToken);
        }
        
        return count;
    }

    public async Task<int> ClearAllHistoryAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        
        // Clear all session history
        lock (_historyLock)
        {
            count = _sessionHistory.Count;
            _sessionHistory.Clear();
        }
        
        // Clear all persisted history
        if (_downloadHistoryService != null)
        {
            count += await _downloadHistoryService.ClearAllAsync(cancellationToken);
        }
        
        return count;
    }

    /// <summary>
    /// Adds a completed/failed activity to session history.
    /// Called by download services when downloads complete.
    /// Note: Persistence to DB is handled by the download service directly.
    /// </summary>
    public void AddToHistory(DownloadActivity activity)
    {
        lock (_historyLock)
        {
            _sessionHistory.Insert(0, activity);

            // Trim session history if too large
            while (_sessionHistory.Count > MaxHistoryItems)
            {
                _sessionHistory.RemoveAt(_sessionHistory.Count - 1);
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

    private static DownloadActivity MapDdlToActivity(DdlDownloadStatus ddl)
    {
        var state = ddl.State switch
        {
            DdlDownloadState.Queued => ActivityState.Queued,
            DdlDownloadState.Downloading => ActivityState.Downloading,
            DdlDownloadState.Completed => ActivityState.Completed,
            DdlDownloadState.Failed => ActivityState.Failed,
            DdlDownloadState.Cancelled => ActivityState.Cancelled,
            DdlDownloadState.Retrying => ActivityState.Retrying,
            DdlDownloadState.Stalled => ActivityState.Stalled,
            _ => ActivityState.Downloading
        };

        // Calculate speed and ETA
        long? speed = ddl.BytesPerSecond > 0 ? (long)ddl.BytesPerSecond : null;
        TimeSpan? eta = null;
        if (ddl.BytesPerSecond > 0 && ddl.TotalBytes.HasValue && ddl.TotalBytes.Value > 0 && ddl.BytesDownloaded < ddl.TotalBytes.Value)
        {
            var remaining = ddl.TotalBytes.Value - ddl.BytesDownloaded;
            eta = TimeSpan.FromSeconds(remaining / ddl.BytesPerSecond);
        }

        return new DownloadActivity
        {
            Id = ddl.DownloadId,
            SourceType = DownloadSourceType.Ddl,
            ClientName = "DDL",
            Title = System.IO.Path.GetFileNameWithoutExtension(ddl.DestinationPath) ?? ddl.DownloadId,
            State = state,
            Progress = ddl.ProgressPercent,
            TotalBytes = ddl.TotalBytes,
            DownloadedBytes = ddl.BytesDownloaded,
            SpeedBytesPerSecond = speed,
            EstimatedTimeRemaining = eta,
            StartedAt = ddl.StartedAt,
            ErrorMessage = ddl.LastError,
            RetryCount = ddl.CurrentRetry,
            OutputPath = ddl.DestinationPath,
            SourceUrl = ddl.SourceUrl
        };
    }

    private static DownloadActivity MapDdlHistoryToActivity(DdlDownloadHistoryEntry entry)
    {
        var state = entry.Success ? ActivityState.Completed : 
            (entry.FailureReason == DdlDownloadFailureReason.Cancelled ? ActivityState.Cancelled : ActivityState.Failed);

        return new DownloadActivity
        {
            Id = entry.DownloadId,
            SourceType = DownloadSourceType.Ddl,
            ClientName = entry.SourceSite ?? "DDL",
            Title = entry.ReleaseTitle ?? System.IO.Path.GetFileNameWithoutExtension(entry.DestinationPath) ?? entry.DownloadId,
            State = state,
            Progress = entry.Success ? 100 : 0,
            TotalBytes = entry.FileSize > 0 ? entry.FileSize : null,
            DownloadedBytes = entry.Success ? entry.FileSize : 0,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt,
            ErrorMessage = entry.ErrorMessage,
            RetryCount = entry.RetryAttempts,
            OutputPath = entry.DestinationPath,
            SourceUrl = entry.SourceUrl
        };
    }

    private static DownloadActivity MapDbHistoryToActivity(DownloadHistory entry)
    {
        var state = entry.State switch
        {
            DownloadHistoryState.Completed => ActivityState.Completed,
            DownloadHistoryState.Failed => ActivityState.Failed,
            DownloadHistoryState.Cancelled => ActivityState.Cancelled,
            _ => ActivityState.Failed
        };

        return new DownloadActivity
        {
            Id = entry.DownloadId,
            SourceType = entry.SourceType,
            ClientName = entry.SourceSite ?? entry.SourceType.ToString(),
            Title = entry.Title,
            State = state,
            Progress = entry.Success ? 100 : 0,
            TotalBytes = entry.FileSize > 0 ? entry.FileSize : null,
            DownloadedBytes = entry.Success ? entry.FileSize : 0,
            SpeedBytesPerSecond = entry.AverageSpeedBytesPerSecond.HasValue ? (long)entry.AverageSpeedBytesPerSecond.Value : null,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt,
            ErrorMessage = entry.ErrorMessage,
            RetryCount = entry.RetryAttempts,
            OutputPath = entry.DestinationPath,
            SourceUrl = entry.SourceUrl,
            SeriesId = entry.SeriesId,
            IssueId = entry.IssueId
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
