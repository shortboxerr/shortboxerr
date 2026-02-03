namespace Shortboxerr.Core.Entities;

/// <summary>
/// User-facing notification that can be dismissed/read.
/// </summary>
public class Notification
{
    public int Id { get; set; }
    
    /// <summary>
    /// Type of notification (determines icon and styling).
    /// </summary>
    public NotificationType Type { get; set; }
    
    /// <summary>
    /// Short title of the notification.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Detailed message body.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// Optional link/route to navigate to when clicked.
    /// </summary>
    public string? Link { get; set; }
    
    /// <summary>
    /// Whether the user has read/acknowledged this notification.
    /// </summary>
    public bool IsRead { get; set; } = false;
    
    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the notification was read (null if unread).
    /// </summary>
    public DateTime? ReadAt { get; set; }
    
    /// <summary>
    /// Related series ID (for navigation).
    /// </summary>
    public int? SeriesId { get; set; }
    
    /// <summary>
    /// Related issue ID (for navigation).
    /// </summary>
    public int? IssueId { get; set; }
    
    /// <summary>
    /// JSON data with additional context.
    /// </summary>
    public string? Data { get; set; }
    
    // Navigation properties
    public Series? Series { get; set; }
    public Issue? Issue { get; set; }
}

/// <summary>
/// Categories of notifications.
/// </summary>
public enum NotificationType
{
    /// <summary>General information.</summary>
    Info = 0,
    
    /// <summary>Successful operation (download complete, etc.).</summary>
    Success = 1,
    
    /// <summary>Warning that requires attention.</summary>
    Warning = 2,
    
    /// <summary>Error that requires action.</summary>
    Error = 3,
    
    /// <summary>New release notification.</summary>
    NewRelease = 10,
    
    /// <summary>Issue successfully grabbed/downloaded.</summary>
    Grabbed = 11,
    
    /// <summary>Download completed and imported.</summary>
    Downloaded = 12,
    
    /// <summary>Weekly pull list summary.</summary>
    WeeklySummary = 13,
    
    /// <summary>System health alert.</summary>
    Health = 20,
    
    /// <summary>Application update available.</summary>
    Update = 21
}
