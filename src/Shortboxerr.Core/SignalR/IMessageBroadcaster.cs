namespace Shortboxerr.Core.SignalR;

/// <summary>
/// Service for broadcasting real-time messages to connected clients.
/// Inject this into background services to push updates.
/// </summary>
public interface IMessageBroadcaster
{
    Task BroadcastDownloadStartedAsync(DownloadStartedMessage message, CancellationToken cancellationToken = default);
    Task BroadcastDownloadCompletedAsync(DownloadCompletedMessage message, CancellationToken cancellationToken = default);
    Task BroadcastImportCompletedAsync(ImportCompletedMessage message, CancellationToken cancellationToken = default);
    Task BroadcastSearchResultsAsync(SearchResultsMessage message, CancellationToken cancellationToken = default);
    Task BroadcastQueueUpdateAsync(QueueUpdateMessage message, CancellationToken cancellationToken = default);
    Task BroadcastSystemStatusAsync(SystemStatusMessage message, CancellationToken cancellationToken = default);
}

#region Message Types

/// <summary>
/// Base class for all SignalR messages.
/// </summary>
public abstract class SignalRMessage
{
    public string Type => GetType().Name;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Sent when a download starts.
/// </summary>
public class DownloadStartedMessage : SignalRMessage
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? SeriesTitle { get; init; }
    public string? IssueNumber { get; init; }
    public string? DownloadClient { get; init; }
    public long? SizeBytes { get; init; }
}

/// <summary>
/// Sent when a download completes.
/// </summary>
public class DownloadCompletedMessage : SignalRMessage
{
    public required string Title { get; init; }
    public required string FilePath { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long? SizeBytes { get; init; }
    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// Sent when an import completes.
/// </summary>
public class ImportCompletedMessage : SignalRMessage
{
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public required string FilePath { get; init; }
    public int? SeriesId { get; init; }
    public int? IssueId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Sent when search results are available.
/// </summary>
public class SearchResultsMessage : SignalRMessage
{
    public required string SearchQuery { get; init; }
    public int ResultCount { get; init; }
    public string? SeriesTitle { get; init; }
    public int? SeriesId { get; init; }
}

/// <summary>
/// Sent when the download queue changes.
/// </summary>
public class QueueUpdateMessage : SignalRMessage
{
    public required string Action { get; init; } // Added, Removed, Updated, Completed
    public int QueueCount { get; init; }
    public int? ItemId { get; init; }
    public string? ItemTitle { get; init; }
}

/// <summary>
/// Sent for system status updates (health, indexers, etc.).
/// </summary>
public class SystemStatusMessage : SignalRMessage
{
    public required string Status { get; init; } // Healthy, Warning, Error
    public string? Message { get; init; }
    public string? Component { get; init; }
}

#endregion
