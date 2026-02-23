using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Pushover notification provider for sending push notifications via pushover.net.
/// </summary>
public class PushoverNotificationProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PushoverNotificationProvider>? _logger;
    
    private const string PushoverApiUrl = "https://api.pushover.net/1/messages.json";
    private const string PushoverValidateUrl = "https://api.pushover.net/1/users/validate.json";
    
    public string ProviderType => "Pushover";
    public string DisplayName => "Pushover";
    
    public PushoverNotificationProvider(HttpClient httpClient, ILogger<PushoverNotificationProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not PushoverProviderSettings pushoverSettings)
        {
            return NotificationProviderTestResult.Failed("Invalid settings type for Pushover provider");
        }
        
        if (string.IsNullOrWhiteSpace(pushoverSettings.ApiToken))
        {
            return NotificationProviderTestResult.Failed("API Token is required");
        }
        
        if (string.IsNullOrWhiteSpace(pushoverSettings.UserKey))
        {
            return NotificationProviderTestResult.Failed("User Key is required");
        }
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // First validate the user key
            var validateResult = await ValidateUserKeyAsync(pushoverSettings, cancellationToken);
            if (!validateResult.Success)
            {
                return validateResult;
            }
            
            // Send a test notification
            var testNotification = new ExternalNotification
            {
                EventType = NotificationEventType.Test,
                Title = "Shortboxerr Test",
                Message = "This is a test notification from Shortboxerr."
            };
            
            var sendResult = await SendAsync(testNotification, pushoverSettings, cancellationToken);
            var latency = DateTime.UtcNow - startTime;
            
            if (sendResult.Success)
            {
                return NotificationProviderTestResult.Ok("Pushover test successful", latency);
            }
            
            return NotificationProviderTestResult.Failed(sendResult.ErrorMessage ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error testing Pushover connection");
            return NotificationProviderTestResult.Failed($"Connection failed: {ex.Message}");
        }
    }
    
    public async Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not PushoverProviderSettings pushoverSettings)
        {
            return NotificationSendResult.Failed("Invalid settings type for Pushover provider");
        }
        
        if (string.IsNullOrWhiteSpace(pushoverSettings.ApiToken))
        {
            return NotificationSendResult.Failed("API Token is required");
        }
        
        if (string.IsNullOrWhiteSpace(pushoverSettings.UserKey))
        {
            return NotificationSendResult.Failed("User Key is required");
        }
        
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["token"] = pushoverSettings.ApiToken,
                ["user"] = pushoverSettings.UserKey,
                ["title"] = notification.Title,
                ["message"] = notification.Message,
                ["priority"] = pushoverSettings.Priority.ToString()
            };
            
            // Add optional URL
            if (!string.IsNullOrEmpty(notification.Url))
            {
                formData["url"] = notification.Url;
                formData["url_title"] = "Open in Shortboxerr";
            }
            
            // Add optional devices
            if (!string.IsNullOrEmpty(pushoverSettings.Devices))
            {
                formData["device"] = pushoverSettings.Devices;
            }
            
            // Add optional sound
            if (!string.IsNullOrEmpty(pushoverSettings.Sound))
            {
                formData["sound"] = pushoverSettings.Sound;
            }
            
            // Add HTML formatting for rich content
            formData["html"] = "1";
            
            // For emergency priority, add retry and expire
            if (pushoverSettings.Priority == 2)
            {
                formData["retry"] = Math.Max(30, pushoverSettings.RetrySeconds).ToString();
                formData["expire"] = Math.Min(10800, pushoverSettings.ExpireSeconds).ToString();
            }
            
            // Add timestamp
            formData["timestamp"] = ((DateTimeOffset)notification.Timestamp).ToUnixTimeSeconds().ToString();
            
            _logger?.LogDebug("Sending Pushover notification: {Title}", notification.Title);
            
            using var content = new FormUrlEncodedContent(formData);
            using var response = await _httpClient.PostAsync(PushoverApiUrl, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PushoverResponse>(responseBody);
                if (result?.Status == 1)
                {
                    _logger?.LogInformation("Pushover notification sent successfully: {Request}", result.Request);
                    return NotificationSendResult.Ok(result.Request);
                }
                
                var errors = result?.Errors != null ? string.Join(", ", result.Errors) : "Unknown error";
                return NotificationSendResult.Failed($"Pushover API error: {errors}");
            }
            
            _logger?.LogWarning("Pushover notification failed: {Status} - {Body}", 
                response.StatusCode, responseBody);
            
            return NotificationSendResult.Failed($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to send Pushover notification");
            return NotificationSendResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return NotificationSendResult.Failed("Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error sending Pushover notification");
            return NotificationSendResult.Failed($"Unexpected error: {ex.Message}");
        }
    }
    
    private async Task<NotificationProviderTestResult> ValidateUserKeyAsync(
        PushoverProviderSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["token"] = settings.ApiToken,
                ["user"] = settings.UserKey
            };
            
            // Add device if specified
            if (!string.IsNullOrEmpty(settings.Devices))
            {
                formData["device"] = settings.Devices.Split(',')[0].Trim();
            }
            
            using var content = new FormUrlEncodedContent(formData);
            using var response = await _httpClient.PostAsync(PushoverValidateUrl, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PushoverValidateResponse>(responseBody);
            
            if (result?.Status == 1)
            {
                return NotificationProviderTestResult.Ok($"User validated. Devices: {string.Join(", ", result.Devices ?? Array.Empty<string>())}");
            }
            
            var errors = result?.Errors != null ? string.Join(", ", result.Errors) : "Invalid user key or API token";
            return NotificationProviderTestResult.Failed(errors);
        }
        catch (Exception ex)
        {
            return NotificationProviderTestResult.Failed($"Validation failed: {ex.Message}");
        }
    }
    
    private class PushoverResponse
    {
        public int Status { get; set; }
        public string? Request { get; set; }
        public string[]? Errors { get; set; }
    }
    
    private class PushoverValidateResponse
    {
        public int Status { get; set; }
        public string[]? Devices { get; set; }
        public string[]? Errors { get; set; }
    }
}
