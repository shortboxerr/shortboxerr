namespace Shortboxerr.Core.Notifications;

/// <summary>
/// Represents an external notification provider (webhook, email, etc.).
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Unique identifier for this provider type.
    /// </summary>
    string ProviderType { get; }
    
    /// <summary>
    /// Human-readable name for the provider.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Tests the connection to the provider.
    /// </summary>
    Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a notification through this provider.
    /// </summary>
    Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of testing a notification provider connection.
/// </summary>
public record NotificationProviderTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? ResponseCode { get; init; }
    public TimeSpan? Latency { get; init; }
    
    public static NotificationProviderTestResult Ok(string message = "Connection successful", TimeSpan? latency = null) =>
        new() { Success = true, Message = message, Latency = latency };
    
    public static NotificationProviderTestResult Failed(string message, int? responseCode = null) =>
        new() { Success = false, Message = message, ResponseCode = responseCode };
}

/// <summary>
/// Result of sending a notification.
/// </summary>
public record NotificationSendResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResponseId { get; init; }
    
    public static NotificationSendResult Ok(string? responseId = null) =>
        new() { Success = true, ResponseId = responseId };
    
    public static NotificationSendResult Failed(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Notification payload to send to external providers.
/// </summary>
public record ExternalNotification
{
    /// <summary>
    /// The event type that triggered this notification.
    /// </summary>
    public required NotificationEventType EventType { get; init; }
    
    /// <summary>
    /// Notification title.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// Notification message/body.
    /// </summary>
    public required string Message { get; init; }
    
    /// <summary>
    /// Optional URL to include in the notification.
    /// </summary>
    public string? Url { get; init; }
    
    /// <summary>
    /// Optional image URL (cover image, etc.).
    /// </summary>
    public string? ImageUrl { get; init; }
    
    /// <summary>
    /// Series title if applicable.
    /// </summary>
    public string? SeriesTitle { get; init; }
    
    /// <summary>
    /// Issue number if applicable.
    /// </summary>
    public decimal? IssueNumber { get; init; }
    
    /// <summary>
    /// Additional data specific to the event type.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }
    
    /// <summary>
    /// Timestamp of the notification.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Types of notification events that can trigger external notifications.
/// </summary>
public enum NotificationEventType
{
    /// <summary>Test notification.</summary>
    Test,
    
    /// <summary>New release available this week.</summary>
    NewRelease,
    
    /// <summary>Issue grabbed/downloaded.</summary>
    Grabbed,
    
    /// <summary>Issue imported to library.</summary>
    Imported,
    
    /// <summary>Weekly summary.</summary>
    WeeklySummary,
    
    /// <summary>Download failed.</summary>
    DownloadFailed,
    
    /// <summary>Series added to library.</summary>
    SeriesAdded,
    
    /// <summary>Application health issue.</summary>
    Health,
    
    /// <summary>Application updated.</summary>
    Update
}

/// <summary>
/// Base settings for notification providers.
/// </summary>
public class NotificationProviderSettings
{
    /// <summary>
    /// Unique ID for this provider instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// User-defined name for this provider instance.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider type (e.g., "Webhook", "Email").
    /// </summary>
    public string ProviderType { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this provider is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Event types that trigger this provider.
    /// </summary>
    public List<NotificationEventType> OnEvents { get; set; } = new()
    {
        NotificationEventType.Grabbed,
        NotificationEventType.NewRelease
    };
    
    /// <summary>
    /// Include series name in notifications.
    /// </summary>
    public bool IncludeSeries { get; set; } = true;
    
    /// <summary>
    /// Include cover images if available.
    /// </summary>
    public bool IncludeImages { get; set; } = true;
}

/// <summary>
/// Webhook-specific provider settings.
/// </summary>
public class WebhookProviderSettings : NotificationProviderSettings
{
    public WebhookProviderSettings()
    {
        ProviderType = "Webhook";
    }
    
    /// <summary>
    /// Webhook URL to POST to.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP method (typically POST).
    /// </summary>
    public string Method { get; set; } = "POST";
    
    /// <summary>
    /// Content type for the request body.
    /// </summary>
    public string ContentType { get; set; } = "application/json";
    
    /// <summary>
    /// Username for basic auth (optional).
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Password for basic auth (optional).
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Custom headers to include in requests.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
}
