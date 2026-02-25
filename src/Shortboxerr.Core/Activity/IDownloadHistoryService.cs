using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Activity;

/// <summary>
/// Service for persisting and managing download history.
/// </summary>
public interface IDownloadHistoryService
{
    /// <summary>
    /// Add a new download history entry.
    /// </summary>
    Task AddAsync(DownloadHistory entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent download history.
    /// </summary>
    Task<IReadOnlyList<DownloadHistory>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get history for a specific source type (DDL, NZB, Torrent).
    /// </summary>
    Task<IReadOnlyList<DownloadHistory>> GetBySourceTypeAsync(DownloadSourceType sourceType, int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific history entry by download ID.
    /// </summary>
    Task<DownloadHistory?> GetByDownloadIdAsync(string downloadId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear all download history.
    /// </summary>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear completed downloads from history.
    /// </summary>
    Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear history for a specific source type.
    /// </summary>
    Task<int> ClearBySourceTypeAsync(DownloadSourceType sourceType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a specific entry from history.
    /// </summary>
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get count of history entries.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
