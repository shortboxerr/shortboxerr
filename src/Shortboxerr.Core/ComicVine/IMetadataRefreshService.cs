namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for refreshing metadata from ComicVine.
/// Supports scheduled and manual refresh operations.
/// </summary>
public interface IMetadataRefreshService
{
    /// <summary>
    /// Refresh metadata for a single series from ComicVine.
    /// </summary>
    Task<RefreshResult> RefreshSeriesAsync(
        int seriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh metadata for all matched series.
    /// </summary>
    Task<BulkRefreshResult> RefreshAllSeriesAsync(
        bool force = false,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh metadata for series that haven't been refreshed within the interval.
    /// </summary>
    Task<BulkRefreshResult> RefreshStaleSeriesAsync(
        TimeSpan maxAge,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh issues for a series, discovering any new issues.
    /// </summary>
    Task<SeriesIssueRefreshResult> RefreshSeriesIssuesAsync(
        int seriesId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh metadata for a single edition from ComicVine.
    /// </summary>
    Task<RefreshResult> RefreshEditionAsync(
        int editionId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get refresh history for a series.
    /// </summary>
    Task<IReadOnlyList<MetadataRefreshEvent>> GetSeriesRefreshHistoryAsync(
        int seriesId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all recent refresh events.
    /// </summary>
    Task<IReadOnlyList<MetadataRefreshEvent>> GetRecentRefreshEventsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get refresh settings.
    /// </summary>
    Task<MetadataRefreshSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if any series need refreshing based on settings.
    /// </summary>
    Task<int> GetStaleSeriesCountAsync(CancellationToken cancellationToken = default);
}

#region Result Types

/// <summary>
/// Result of a metadata refresh operation.
/// </summary>
public class RefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ItemId { get; set; }
    public required string ItemType { get; set; }
    public required string ItemTitle { get; set; }
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether any metadata was actually updated.
    /// </summary>
    public bool MetadataChanged { get; set; }
    
    /// <summary>
    /// Fields that were updated.
    /// </summary>
    public List<string> UpdatedFields { get; set; } = new();
}

/// <summary>
/// Result of refreshing series issues.
/// </summary>
public class SeriesIssueRefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int SeriesId { get; set; }
    public int TotalIssues { get; set; }
    public int NewIssuesDiscovered { get; set; }
    public int IssuesUpdated { get; set; }
    public List<int> NewIssueIds { get; set; } = new();
}

/// <summary>
/// Result of a bulk refresh operation.
/// </summary>
public class BulkRefreshResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalProcessed { get; set; }
    public int Refreshed { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int NewIssuesDiscovered { get; set; }
    public TimeSpan Duration { get; set; }
    public List<RefreshResult> Results { get; set; } = new();
}

/// <summary>
/// Progress update during refresh.
/// </summary>
public class RefreshProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentItem { get; set; } = "";
    public int Refreshed { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
}

/// <summary>
/// Record of a metadata refresh event.
/// </summary>
public class MetadataRefreshEvent
{
    public int Id { get; set; }
    public required string ItemType { get; set; }
    public int ItemId { get; set; }
    public required string ItemTitle { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool MetadataChanged { get; set; }
    public string? UpdatedFieldsJson { get; set; }
    public int NewIssuesDiscovered { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Source { get; set; } // "Manual", "Scheduled", "Import"
}

/// <summary>
/// Metadata refresh settings.
/// </summary>
public class MetadataRefreshSettings
{
    /// <summary>
    /// Whether scheduled refresh is enabled.
    /// </summary>
    public bool ScheduledRefreshEnabled { get; set; } = true;
    
    /// <summary>
    /// Interval between scheduled refreshes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(7);
    
    /// <summary>
    /// Whether to refresh covers during metadata refresh.
    /// </summary>
    public bool RefreshCovers { get; set; } = true;
    
    /// <summary>
    /// Maximum series to refresh per scheduled run.
    /// </summary>
    public int MaxSeriesPerRun { get; set; } = 50;
    
    /// <summary>
    /// Hours of the day when scheduled refresh can run (24h format).
    /// </summary>
    public List<int> AllowedHours { get; set; } = new() { 2, 3, 4 }; // 2-4 AM
}

#endregion

