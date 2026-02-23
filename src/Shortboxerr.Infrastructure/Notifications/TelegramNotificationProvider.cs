using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Telegram notification provider for sending messages via Telegram Bot API.
/// </summary>
public class TelegramNotificationProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationProvider>? _logger;
    
    private const string TelegramApiBaseUrl = "https://api.telegram.org/bot";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public string ProviderType => "Telegram";
    public string DisplayName => "Telegram";
    
    public TelegramNotificationProvider(HttpClient httpClient, ILogger<TelegramNotificationProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not TelegramProviderSettings telegramSettings)
        {
            return NotificationProviderTestResult.Failed("Invalid settings type for Telegram provider");
        }
        
        var validationResult = ValidateSettings(telegramSettings);
        if (!validationResult.Success)
        {
            return validationResult;
        }
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // First verify the bot token by calling getMe
            var getMeResult = await VerifyBotTokenAsync(telegramSettings.BotToken, cancellationToken);
            if (!getMeResult.Success)
            {
                return getMeResult;
            }
            
            // Send a test message
            var testNotification = new ExternalNotification
            {
                EventType = NotificationEventType.Test,
                Title = "Shortboxerr Test",
                Message = "This is a test notification from Shortboxerr."
            };
            
            var sendResult = await SendAsync(testNotification, telegramSettings, cancellationToken);
            var latency = DateTime.UtcNow - startTime;
            
            if (sendResult.Success)
            {
                return NotificationProviderTestResult.Ok($"Telegram test successful (Bot: {getMeResult.Message})", latency);
            }
            
            return NotificationProviderTestResult.Failed(sendResult.ErrorMessage ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error testing Telegram connection");
            return NotificationProviderTestResult.Failed($"Connection failed: {ex.Message}");
        }
    }
    
    public async Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not TelegramProviderSettings telegramSettings)
        {
            return NotificationSendResult.Failed("Invalid settings type for Telegram provider");
        }
        
        var validationResult = ValidateSettings(telegramSettings);
        if (!validationResult.Success)
        {
            return NotificationSendResult.Failed(validationResult.Message);
        }
        
        try
        {
            var message = FormatMessage(notification, telegramSettings.ParseMode);
            
            var payload = new TelegramSendMessageRequest
            {
                ChatId = telegramSettings.ChatId,
                Text = message,
                ParseMode = telegramSettings.ParseMode,
                DisableNotification = telegramSettings.SilentNotification,
                DisableWebPagePreview = !telegramSettings.EnableLinkPreview,
                MessageThreadId = telegramSettings.TopicId
            };
            
            var url = $"{TelegramApiBaseUrl}{telegramSettings.BotToken}/sendMessage";
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            
            _logger?.LogDebug("Sending Telegram notification to chat {ChatId}: {Title}", 
                telegramSettings.ChatId, notification.Title);
            
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TelegramResponse>(responseBody, JsonOptions);
            
            if (result?.Ok == true)
            {
                var messageId = result.Result?.MessageId?.ToString();
                _logger?.LogInformation("Telegram notification sent successfully. Message ID: {MessageId}", messageId);
                return NotificationSendResult.Ok(messageId);
            }
            
            var errorDescription = result?.Description ?? "Unknown error";
            var errorCode = result?.ErrorCode;
            
            _logger?.LogWarning("Telegram notification failed: [{Code}] {Description}", 
                errorCode, errorDescription);
            
            return NotificationSendResult.Failed($"Telegram API error [{errorCode}]: {errorDescription}");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to send Telegram notification");
            return NotificationSendResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return NotificationSendResult.Failed("Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error sending Telegram notification");
            return NotificationSendResult.Failed($"Unexpected error: {ex.Message}");
        }
    }
    
    private static NotificationProviderTestResult ValidateSettings(TelegramProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            return NotificationProviderTestResult.Failed("Bot Token is required");
        }
        
        if (!settings.BotToken.Contains(':'))
        {
            return NotificationProviderTestResult.Failed("Bot Token format is invalid. Expected format: 123456789:ABCdefGHIjklMNOpqrsTUVwxyz");
        }
        
        if (string.IsNullOrWhiteSpace(settings.ChatId))
        {
            return NotificationProviderTestResult.Failed("Chat ID is required");
        }
        
        var validParseModes = new[] { "HTML", "Markdown", "MarkdownV2" };
        if (!string.IsNullOrEmpty(settings.ParseMode) && !validParseModes.Contains(settings.ParseMode, StringComparer.OrdinalIgnoreCase))
        {
            return NotificationProviderTestResult.Failed($"Invalid parse mode. Valid options: {string.Join(", ", validParseModes)}");
        }
        
        return NotificationProviderTestResult.Ok();
    }
    
    private async Task<NotificationProviderTestResult> VerifyBotTokenAsync(
        string botToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{TelegramApiBaseUrl}{botToken}/getMe";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TelegramGetMeResponse>(responseBody, JsonOptions);
            
            if (result?.Ok == true && result.Result != null)
            {
                var botName = result.Result.Username ?? result.Result.FirstName ?? "Unknown";
                return NotificationProviderTestResult.Ok($"@{botName}");
            }
            
            var errorDescription = result?.Description ?? "Invalid bot token";
            return NotificationProviderTestResult.Failed(errorDescription);
        }
        catch (Exception ex)
        {
            return NotificationProviderTestResult.Failed($"Failed to verify bot token: {ex.Message}");
        }
    }
    
    private static string FormatMessage(ExternalNotification notification, string parseMode)
    {
        var sb = new StringBuilder();
        
        if (parseMode.Equals("HTML", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"<b>{EscapeHtml(notification.Title)}</b>");
            sb.AppendLine();
            sb.AppendLine(EscapeHtml(notification.Message));
            
            if (!string.IsNullOrEmpty(notification.SeriesTitle))
            {
                sb.AppendLine();
                sb.Append($"<i>Series: {EscapeHtml(notification.SeriesTitle)}");
                if (notification.IssueNumber.HasValue)
                {
                    sb.Append($" #{notification.IssueNumber}");
                }
                sb.AppendLine("</i>");
            }
            
            if (!string.IsNullOrEmpty(notification.Url))
            {
                sb.AppendLine();
                sb.AppendLine($"<a href=\"{notification.Url}\">Open in Shortboxerr</a>");
            }
        }
        else
        {
            sb.AppendLine($"*{EscapeMarkdown(notification.Title)}*");
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(notification.Message));
            
            if (!string.IsNullOrEmpty(notification.SeriesTitle))
            {
                sb.AppendLine();
                sb.Append($"_Series: {EscapeMarkdown(notification.SeriesTitle)}");
                if (notification.IssueNumber.HasValue)
                {
                    sb.Append($" \\#{notification.IssueNumber}");
                }
                sb.AppendLine("_");
            }
            
            if (!string.IsNullOrEmpty(notification.Url))
            {
                sb.AppendLine();
                sb.AppendLine($"[Open in Shortboxerr]({notification.Url})");
            }
        }
        
        return sb.ToString().TrimEnd();
    }
    
    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
    
    private static string EscapeMarkdown(string text)
    {
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        var result = text;
        foreach (var c in specialChars)
        {
            result = result.Replace(c.ToString(), $"\\{c}");
        }
        return result;
    }
    
    private class TelegramSendMessageRequest
    {
        public string ChatId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? ParseMode { get; set; }
        public bool? DisableNotification { get; set; }
        public bool? DisableWebPagePreview { get; set; }
        public int? MessageThreadId { get; set; }
    }
    
    private class TelegramResponse
    {
        public bool Ok { get; set; }
        public string? Description { get; set; }
        public int? ErrorCode { get; set; }
        public TelegramMessage? Result { get; set; }
    }
    
    private class TelegramMessage
    {
        public long? MessageId { get; set; }
    }
    
    private class TelegramGetMeResponse
    {
        public bool Ok { get; set; }
        public string? Description { get; set; }
        public TelegramUser? Result { get; set; }
    }
    
    private class TelegramUser
    {
        public long? Id { get; set; }
        public string? FirstName { get; set; }
        public string? Username { get; set; }
        public bool? IsBot { get; set; }
    }
}
