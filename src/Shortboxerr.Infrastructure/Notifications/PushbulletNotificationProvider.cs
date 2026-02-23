using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Pushbullet notification provider for sending push notifications via pushbullet.com.
/// </summary>
public class PushbulletNotificationProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PushbulletNotificationProvider>? _logger;
    
    private const string PushbulletApiUrl = "https://api.pushbullet.com/v2/pushes";
    private const string PushbulletMeUrl = "https://api.pushbullet.com/v2/users/me";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public string ProviderType => "Pushbullet";
    public string DisplayName => "Pushbullet";
    
    public PushbulletNotificationProvider(HttpClient httpClient, ILogger<PushbulletNotificationProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not PushbulletProviderSettings pushbulletSettings)
        {
            return NotificationProviderTestResult.Failed("Invalid settings type for Pushbullet provider");
        }
        
        if (string.IsNullOrWhiteSpace(pushbulletSettings.AccessToken))
        {
            return NotificationProviderTestResult.Failed("Access Token is required");
        }
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // First validate the access token by getting user info
            var validateResult = await ValidateAccessTokenAsync(pushbulletSettings, cancellationToken);
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
            
            var sendResult = await SendAsync(testNotification, pushbulletSettings, cancellationToken);
            var latency = DateTime.UtcNow - startTime;
            
            if (sendResult.Success)
            {
                return NotificationProviderTestResult.Ok("Pushbullet test successful", latency);
            }
            
            return NotificationProviderTestResult.Failed(sendResult.ErrorMessage ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error testing Pushbullet connection");
            return NotificationProviderTestResult.Failed($"Connection failed: {ex.Message}");
        }
    }
    
    public async Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not PushbulletProviderSettings pushbulletSettings)
        {
            return NotificationSendResult.Failed("Invalid settings type for Pushbullet provider");
        }
        
        if (string.IsNullOrWhiteSpace(pushbulletSettings.AccessToken))
        {
            return NotificationSendResult.Failed("Access Token is required");
        }
        
        try
        {
            var push = new PushbulletPush
            {
                Type = !string.IsNullOrEmpty(notification.Url) ? "link" : "note",
                Title = notification.Title,
                Body = notification.Message,
                Url = notification.Url
            };
            
            // Add optional target
            if (!string.IsNullOrEmpty(pushbulletSettings.DeviceId))
            {
                push.DeviceIden = pushbulletSettings.DeviceId;
            }
            else if (!string.IsNullOrEmpty(pushbulletSettings.ChannelTag))
            {
                push.ChannelTag = pushbulletSettings.ChannelTag;
            }
            else if (!string.IsNullOrEmpty(pushbulletSettings.SendToEmail))
            {
                push.Email = pushbulletSettings.SendToEmail;
            }
            
            var json = JsonSerializer.Serialize(push, JsonOptions);
            
            _logger?.LogDebug("Sending Pushbullet notification: {Title}", notification.Title);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, PushbulletApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Access-Token", pushbulletSettings.AccessToken);
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PushbulletPushResponse>(responseBody, JsonOptions);
                _logger?.LogInformation("Pushbullet notification sent successfully: {Iden}", result?.Iden);
                return NotificationSendResult.Ok(result?.Iden);
            }
            
            _logger?.LogWarning("Pushbullet notification failed: {Status} - {Body}", 
                response.StatusCode, responseBody);
            
            // Try to parse error response
            try
            {
                var error = JsonSerializer.Deserialize<PushbulletErrorResponse>(responseBody, JsonOptions);
                if (error?.Error != null)
                {
                    return NotificationSendResult.Failed($"Pushbullet error: {error.Error.Message}");
                }
            }
            catch
            {
                // Ignore parse errors
            }
            
            return NotificationSendResult.Failed($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to send Pushbullet notification");
            return NotificationSendResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return NotificationSendResult.Failed("Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error sending Pushbullet notification");
            return NotificationSendResult.Failed($"Unexpected error: {ex.Message}");
        }
    }
    
    private async Task<NotificationProviderTestResult> ValidateAccessTokenAsync(
        PushbulletProviderSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, PushbulletMeUrl);
            request.Headers.Add("Access-Token", settings.AccessToken);
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var user = JsonSerializer.Deserialize<PushbulletUserResponse>(responseBody, JsonOptions);
                return NotificationProviderTestResult.Ok($"Authenticated as: {user?.Email ?? user?.Name ?? "Unknown"}");
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return NotificationProviderTestResult.Failed("Invalid access token");
            }
            
            return NotificationProviderTestResult.Failed($"Validation failed: HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return NotificationProviderTestResult.Failed($"Validation failed: {ex.Message}");
        }
    }
    
    private class PushbulletPush
    {
        public string Type { get; set; } = "note";
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? Url { get; set; }
        public string? DeviceIden { get; set; }
        public string? ChannelTag { get; set; }
        public string? Email { get; set; }
    }
    
    private class PushbulletPushResponse
    {
        public string? Iden { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
    }
    
    private class PushbulletUserResponse
    {
        public string? Iden { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
    }
    
    private class PushbulletErrorResponse
    {
        public PushbulletError? Error { get; set; }
    }
    
    private class PushbulletError
    {
        public string? Code { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
    }
}
