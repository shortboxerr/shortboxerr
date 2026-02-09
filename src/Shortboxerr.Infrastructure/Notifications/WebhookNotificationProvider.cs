using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Webhook notification provider that supports generic webhooks, Discord, and Slack.
/// </summary>
public class WebhookNotificationProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookNotificationProvider>? _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    public string ProviderType => "Webhook";
    public string DisplayName => "Webhook";
    
    public WebhookNotificationProvider(HttpClient httpClient, ILogger<WebhookNotificationProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not WebhookProviderSettings webhookSettings)
        {
            return NotificationProviderTestResult.Failed("Invalid settings type for webhook provider");
        }
        
        if (string.IsNullOrWhiteSpace(webhookSettings.WebhookUrl))
        {
            return NotificationProviderTestResult.Failed("Webhook URL is required");
        }
        
        var testNotification = new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Shortboxerr Test Notification",
            Message = "This is a test notification from Shortboxerr."
        };
        
        var startTime = DateTime.UtcNow;
        var result = await SendAsync(testNotification, webhookSettings, cancellationToken);
        var latency = DateTime.UtcNow - startTime;
        
        if (result.Success)
        {
            return NotificationProviderTestResult.Ok("Webhook test successful", latency);
        }
        
        return NotificationProviderTestResult.Failed(result.ErrorMessage ?? "Unknown error");
    }
    
    public async Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not WebhookProviderSettings webhookSettings)
        {
            return NotificationSendResult.Failed("Invalid settings type for webhook provider");
        }
        
        if (string.IsNullOrWhiteSpace(webhookSettings.WebhookUrl))
        {
            return NotificationSendResult.Failed("Webhook URL is required");
        }
        
        try
        {
            var payload = CreatePayload(notification, webhookSettings);
            var content = new StringContent(payload, Encoding.UTF8, webhookSettings.ContentType);
            
            using var request = new HttpRequestMessage(
                new HttpMethod(webhookSettings.Method),
                webhookSettings.WebhookUrl)
            {
                Content = content
            };
            
            // Add basic auth if configured
            if (!string.IsNullOrEmpty(webhookSettings.Username))
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{webhookSettings.Username}:{webhookSettings.Password ?? ""}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            
            // Add custom headers
            if (webhookSettings.Headers != null)
            {
                foreach (var header in webhookSettings.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            _logger?.LogDebug("Sending webhook notification to {Url}: {Title}", 
                webhookSettings.WebhookUrl, notification.Title);
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("Webhook notification sent successfully to {Url}", 
                    webhookSettings.WebhookUrl);
                return NotificationSendResult.Ok();
            }
            
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogWarning("Webhook notification failed: {Status} - {Body}", 
                response.StatusCode, errorBody);
            
            return NotificationSendResult.Failed($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to send webhook notification to {Url}", 
                webhookSettings.WebhookUrl);
            return NotificationSendResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return NotificationSendResult.Failed("Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error sending webhook notification");
            return NotificationSendResult.Failed($"Unexpected error: {ex.Message}");
        }
    }
    
    private string CreatePayload(ExternalNotification notification, WebhookProviderSettings settings)
    {
        // Detect webhook type and create appropriate payload
        var webhookType = DetectWebhookType(settings.WebhookUrl);
        
        return webhookType switch
        {
            WebhookType.Discord => CreateDiscordPayload(notification, settings),
            WebhookType.Slack => CreateSlackPayload(notification, settings),
            _ => CreateGenericPayload(notification)
        };
    }
    
    private static WebhookType DetectWebhookType(string url)
    {
        if (url.Contains("discord.com/api/webhooks") || url.Contains("discordapp.com/api/webhooks"))
            return WebhookType.Discord;
        
        if (url.Contains("hooks.slack.com"))
            return WebhookType.Slack;
        
        return WebhookType.Generic;
    }
    
    private string CreateDiscordPayload(ExternalNotification notification, WebhookProviderSettings settings)
    {
        // Discord webhook format with embed
        var color = GetDiscordColor(notification.EventType);
        
        var embed = new
        {
            title = notification.Title,
            description = notification.Message,
            color = color,
            timestamp = notification.Timestamp.ToString("o"),
            thumbnail = settings.IncludeImages && !string.IsNullOrEmpty(notification.ImageUrl)
                ? new { url = notification.ImageUrl }
                : null,
            fields = BuildDiscordFields(notification, settings),
            footer = new { text = "Shortboxerr" }
        };
        
        var payload = new
        {
            username = "Shortboxerr",
            embeds = new[] { embed }
        };
        
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
    
    private static object[]? BuildDiscordFields(ExternalNotification notification, WebhookProviderSettings settings)
    {
        var fields = new List<object>();
        
        if (settings.IncludeSeries && !string.IsNullOrEmpty(notification.SeriesTitle))
        {
            fields.Add(new { name = "Series", value = notification.SeriesTitle, inline = true });
        }
        
        if (notification.IssueNumber.HasValue)
        {
            fields.Add(new { name = "Issue", value = $"#{notification.IssueNumber}", inline = true });
        }
        
        if (notification.Data != null)
        {
            if (notification.Data.TryGetValue("downloadSource", out var source))
            {
                fields.Add(new { name = "Source", value = source?.ToString() ?? "Unknown", inline = true });
            }
        }
        
        return fields.Count > 0 ? fields.ToArray() : null;
    }
    
    private static int GetDiscordColor(NotificationEventType eventType)
    {
        return eventType switch
        {
            NotificationEventType.Test => 0x3498db, // Blue
            NotificationEventType.NewRelease => 0x2ecc71, // Green
            NotificationEventType.Grabbed => 0x9b59b6, // Purple
            NotificationEventType.Imported => 0x27ae60, // Dark Green
            NotificationEventType.DownloadFailed => 0xe74c3c, // Red
            NotificationEventType.Health => 0xf39c12, // Orange
            _ => 0x95a5a6 // Gray
        };
    }
    
    private string CreateSlackPayload(ExternalNotification notification, WebhookProviderSettings settings)
    {
        // Slack webhook format with blocks
        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = notification.Title }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = notification.Message }
            }
        };
        
        // Add series info if available
        if (settings.IncludeSeries && !string.IsNullOrEmpty(notification.SeriesTitle))
        {
            var fieldsBlock = new
            {
                type = "section",
                fields = new List<object>
                {
                    new { type = "mrkdwn", text = $"*Series*\n{notification.SeriesTitle}" }
                }
            };
            
            if (notification.IssueNumber.HasValue)
            {
                ((List<object>)fieldsBlock.fields).Add(
                    new { type = "mrkdwn", text = $"*Issue*\n#{notification.IssueNumber}" });
            }
            
            blocks.Add(fieldsBlock);
        }
        
        // Add image if available
        if (settings.IncludeImages && !string.IsNullOrEmpty(notification.ImageUrl))
        {
            blocks.Add(new
            {
                type = "image",
                image_url = notification.ImageUrl,
                alt_text = notification.Title
            });
        }
        
        // Add footer
        blocks.Add(new
        {
            type = "context",
            elements = new[]
            {
                new { type = "plain_text", text = $"Shortboxerr • {notification.Timestamp:g}" }
            }
        });
        
        var payload = new { blocks };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
    
    private string CreateGenericPayload(ExternalNotification notification)
    {
        // Generic JSON payload
        var payload = new
        {
            eventType = notification.EventType.ToString(),
            title = notification.Title,
            message = notification.Message,
            url = notification.Url,
            imageUrl = notification.ImageUrl,
            seriesTitle = notification.SeriesTitle,
            issueNumber = notification.IssueNumber,
            timestamp = notification.Timestamp,
            data = notification.Data,
            source = "Shortboxerr"
        };
        
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
    
    private enum WebhookType
    {
        Generic,
        Discord,
        Slack
    }
}
