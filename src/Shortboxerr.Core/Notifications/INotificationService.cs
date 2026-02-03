using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Notifications;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public interface INotificationService
{
    #region Notification Management
    
    /// <summary>
    /// Gets all notifications, optionally filtered.
    /// </summary>
    Task<List<Notification>> GetNotificationsAsync(
        NotificationFilter? filter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a single notification by ID.
    /// </summary>
    Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a notification.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes all read notifications.
    /// </summary>
    Task<int> DeleteReadAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes notifications older than the specified date.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Notification Creation
    
    /// <summary>
    /// Creates a new notification.
    /// </summary>
    Task<Notification> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a new release notification for this week's releases.
    /// </summary>
    Task<Notification?> SendNewReleaseNotificationAsync(
        NewReleaseNotificationRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a grabbed notification when an issue is grabbed/downloaded.
    /// </summary>
    Task<Notification?> SendGrabbedNotificationAsync(
        GrabbedNotificationRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a weekly summary notification.
    /// </summary>
    Task<Notification?> SendWeeklySummaryAsync(
        WeeklySummaryRequest request,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Settings
    
    /// <summary>
    /// Gets notification settings.
    /// </summary>
    Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates notification settings.
    /// </summary>
    Task UpdateSettingsAsync(NotificationSettings settings, CancellationToken cancellationToken = default);
    
    #endregion
}

#region Models

/// <summary>
/// Filter options for querying notifications.
/// </summary>
public class NotificationFilter
{
    /// <summary>Only show unread notifications.</summary>
    public bool? UnreadOnly { get; set; }
    
    /// <summary>Filter by notification types.</summary>
    public List<NotificationType>? Types { get; set; }
    
    /// <summary>Filter by related series.</summary>
    public int? SeriesId { get; set; }
    
    /// <summary>Maximum number of notifications to return.</summary>
    public int Limit { get; set; } = 50;
    
    /// <summary>Offset for pagination.</summary>
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Request to create a generic notification.
/// </summary>
public class CreateNotificationRequest
{
    public NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? Link { get; set; }
    public int? SeriesId { get; set; }
    public int? IssueId { get; set; }
    public string? Data { get; set; }
}

/// <summary>
/// Request for new release notification.
/// </summary>
public class NewReleaseNotificationRequest
{
    /// <summary>Date of the releases (typically Wednesday).</summary>
    public DateTime ReleaseDate { get; set; }
    
    /// <summary>Number of issues releasing from monitored series.</summary>
    public int IssueCount { get; set; }
    
    /// <summary>Series titles with releases this week.</summary>
    public List<string> SeriesTitles { get; set; } = new();
}

/// <summary>
/// Request for grabbed notification.
/// </summary>
public class GrabbedNotificationRequest
{
    public int SeriesId { get; set; }
    public int IssueId { get; set; }
    public required string SeriesTitle { get; set; }
    public decimal IssueNumber { get; set; }
    public string? DownloadSource { get; set; }
}

/// <summary>
/// Request for weekly summary notification.
/// </summary>
public class WeeklySummaryRequest
{
    public DateTime WeekOf { get; set; }
    public int TotalReleases { get; set; }
    public int WantedCount { get; set; }
    public int OwnedCount { get; set; }
    public int SkippedCount { get; set; }
}

/// <summary>
/// Notification settings.
/// </summary>
public class NotificationSettings
{
    /// <summary>Enable in-app notifications.</summary>
    public bool EnableInApp { get; set; } = true;
    
    /// <summary>Send new release notifications.</summary>
    public bool NewReleaseNotifications { get; set; } = true;
    
    /// <summary>Send grabbed notifications.</summary>
    public bool GrabbedNotifications { get; set; } = true;
    
    /// <summary>Send weekly summary notifications.</summary>
    public bool WeeklySummaryNotifications { get; set; } = false;
    
    /// <summary>Day to send weekly summary (default: Tuesday before release).</summary>
    public DayOfWeek SummaryNotificationDay { get; set; } = DayOfWeek.Tuesday;
    
    /// <summary>Whether to aggregate new releases into a single notification vs. individual.</summary>
    public bool AggregateReleaseNotifications { get; set; } = true;
    
    /// <summary>Auto-delete read notifications after this many days (0 = never).</summary>
    public int AutoDeleteReadAfterDays { get; set; } = 30;
    
    /// <summary>Maximum notifications to keep (oldest deleted first, 0 = unlimited).</summary>
    public int MaxNotifications { get; set; } = 500;
}

#endregion
