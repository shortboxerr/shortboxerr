using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Notifications;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Email notification provider using SMTP.
/// </summary>
public class EmailNotificationProvider : INotificationProvider
{
    private readonly ILogger<EmailNotificationProvider>? _logger;
    
    public string ProviderType => "Email";
    public string DisplayName => "Email (SMTP)";
    
    public EmailNotificationProvider(ILogger<EmailNotificationProvider>? logger = null)
    {
        _logger = logger;
    }
    
    public async Task<NotificationProviderTestResult> TestAsync(
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not EmailProviderSettings emailSettings)
        {
            return NotificationProviderTestResult.Failed("Invalid settings type for email provider");
        }
        
        var validationError = ValidateSettings(emailSettings);
        if (validationError != null)
        {
            return NotificationProviderTestResult.Failed(validationError);
        }
        
        var testNotification = new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Shortboxerr Test Email",
            Message = "This is a test email from Shortboxerr. If you received this, your email notifications are configured correctly."
        };
        
        var startTime = DateTime.UtcNow;
        var result = await SendAsync(testNotification, emailSettings, cancellationToken);
        var latency = DateTime.UtcNow - startTime;
        
        if (result.Success)
        {
            return NotificationProviderTestResult.Ok("Test email sent successfully", latency);
        }
        
        return NotificationProviderTestResult.Failed(result.ErrorMessage ?? "Unknown error");
    }
    
    public async Task<NotificationSendResult> SendAsync(
        ExternalNotification notification,
        NotificationProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings is not EmailProviderSettings emailSettings)
        {
            return NotificationSendResult.Failed("Invalid settings type for email provider");
        }
        
        var validationError = ValidateSettings(emailSettings);
        if (validationError != null)
        {
            return NotificationSendResult.Failed(validationError);
        }
        
        try
        {
            using var client = CreateSmtpClient(emailSettings);
            using var message = CreateMailMessage(notification, emailSettings);
            
            _logger?.LogDebug("Sending email notification: {Subject}", message.Subject);
            
            await client.SendMailAsync(message, cancellationToken);
            
            _logger?.LogInformation("Email notification sent successfully to {Recipients}", 
                emailSettings.RecipientEmails);
            
            return NotificationSendResult.Ok();
        }
        catch (SmtpException ex)
        {
            _logger?.LogError(ex, "SMTP error sending email notification");
            return NotificationSendResult.Failed($"SMTP error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return NotificationSendResult.Failed("Email send was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error sending email notification");
            return NotificationSendResult.Failed($"Unexpected error: {ex.Message}");
        }
    }
    
    private static string? ValidateSettings(EmailProviderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpServer))
            return "SMTP server is required";
            
        if (settings.Port <= 0 || settings.Port > 65535)
            return "Invalid SMTP port";
            
        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
            return "Sender email address is required";
            
        if (string.IsNullOrWhiteSpace(settings.RecipientEmails))
            return "At least one recipient email is required";
            
        try
        {
            _ = new MailAddress(settings.SenderEmail);
        }
        catch
        {
            return "Invalid sender email address format";
        }
        
        return null;
    }
    
    private static SmtpClient CreateSmtpClient(EmailProviderSettings settings)
    {
        var client = new SmtpClient(settings.SmtpServer, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };
        
        if (!string.IsNullOrEmpty(settings.Username))
        {
            client.Credentials = new NetworkCredential(
                settings.Username, 
                settings.Password ?? string.Empty);
        }
        
        return client;
    }
    
    private MailMessage CreateMailMessage(ExternalNotification notification, EmailProviderSettings settings)
    {
        var fromAddress = new MailAddress(
            settings.SenderEmail, 
            settings.SenderName ?? "Shortboxerr");
            
        var message = new MailMessage
        {
            From = fromAddress,
            Subject = BuildSubject(notification, settings),
            IsBodyHtml = settings.UseHtml
        };
        
        // Add recipients
        foreach (var email in ParseEmailList(settings.RecipientEmails))
        {
            message.To.Add(email);
        }
        
        // Add CC recipients
        if (!string.IsNullOrWhiteSpace(settings.CcEmails))
        {
            foreach (var email in ParseEmailList(settings.CcEmails))
            {
                message.CC.Add(email);
            }
        }
        
        // Add BCC recipients
        if (!string.IsNullOrWhiteSpace(settings.BccEmails))
        {
            foreach (var email in ParseEmailList(settings.BccEmails))
            {
                message.Bcc.Add(email);
            }
        }
        
        // Build body
        message.Body = settings.UseHtml 
            ? BuildHtmlBody(notification, settings) 
            : BuildPlainTextBody(notification);
            
        return message;
    }
    
    private static string BuildSubject(ExternalNotification notification, EmailProviderSettings settings)
    {
        var prefix = settings.SubjectPrefix;
        if (!string.IsNullOrWhiteSpace(prefix) && !prefix.EndsWith(" "))
        {
            prefix += " ";
        }
        
        return $"{prefix}{notification.Title}";
    }
    
    private static IEnumerable<string> ParseEmailList(string emailList)
    {
        return emailList
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e));
    }
    
    private static string BuildPlainTextBody(ExternalNotification notification)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine(notification.Message);
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(notification.SeriesTitle))
        {
            sb.AppendLine($"Series: {notification.SeriesTitle}");
        }
        
        if (notification.IssueNumber.HasValue)
        {
            sb.AppendLine($"Issue: #{notification.IssueNumber}");
        }
        
        if (!string.IsNullOrEmpty(notification.Url))
        {
            sb.AppendLine();
            sb.AppendLine($"Link: {notification.Url}");
        }
        
        sb.AppendLine();
        sb.AppendLine("--");
        sb.AppendLine("Sent by Shortboxerr");
        
        return sb.ToString();
    }
    
    private static string BuildHtmlBody(ExternalNotification notification, EmailProviderSettings settings)
    {
        var eventColor = GetEventColor(notification.EventType);
        
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'></head>");
        sb.AppendLine("<body style='font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 0; background-color: #f5f5f5;'>");
        
        // Container
        sb.AppendLine("<div style='max-width: 600px; margin: 20px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>");
        
        // Header with color bar
        sb.AppendLine($"<div style='background: {eventColor}; height: 4px;'></div>");
        sb.AppendLine("<div style='padding: 24px;'>");
        
        // Title
        sb.AppendLine($"<h1 style='margin: 0 0 16px 0; font-size: 20px; color: #1a1a1a;'>{HtmlEncode(notification.Title)}</h1>");
        
        // Message
        sb.AppendLine($"<p style='margin: 0 0 20px 0; color: #444; line-height: 1.6;'>{HtmlEncode(notification.Message)}</p>");
        
        // Cover image if available
        if (settings.IncludeImages && !string.IsNullOrEmpty(notification.ImageUrl))
        {
            sb.AppendLine("<div style='margin-bottom: 20px;'>");
            sb.AppendLine($"<img src='{HtmlEncode(notification.ImageUrl)}' alt='Cover' style='max-width: 200px; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);' />");
            sb.AppendLine("</div>");
        }
        
        // Details table
        if (!string.IsNullOrEmpty(notification.SeriesTitle) || notification.IssueNumber.HasValue)
        {
            sb.AppendLine("<table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>");
            
            if (!string.IsNullOrEmpty(notification.SeriesTitle))
            {
                sb.AppendLine("<tr>");
                sb.AppendLine("<td style='padding: 8px 0; color: #666; width: 80px;'>Series</td>");
                sb.AppendLine($"<td style='padding: 8px 0; color: #1a1a1a; font-weight: 500;'>{HtmlEncode(notification.SeriesTitle)}</td>");
                sb.AppendLine("</tr>");
            }
            
            if (notification.IssueNumber.HasValue)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine("<td style='padding: 8px 0; color: #666;'>Issue</td>");
                sb.AppendLine($"<td style='padding: 8px 0; color: #1a1a1a; font-weight: 500;'>#{notification.IssueNumber}</td>");
                sb.AppendLine("</tr>");
            }
            
            sb.AppendLine("</table>");
        }
        
        // Link button
        if (!string.IsNullOrEmpty(notification.Url))
        {
            sb.AppendLine("<div style='margin-bottom: 20px;'>");
            sb.AppendLine($"<a href='{HtmlEncode(notification.Url)}' style='display: inline-block; background: {eventColor}; color: #fff; padding: 10px 20px; text-decoration: none; border-radius: 4px; font-weight: 500;'>View Details</a>");
            sb.AppendLine("</div>");
        }
        
        sb.AppendLine("</div>"); // End padding div
        
        // Footer
        sb.AppendLine("<div style='background: #f9f9f9; padding: 16px 24px; border-top: 1px solid #eee;'>");
        sb.AppendLine($"<p style='margin: 0; font-size: 12px; color: #888;'>Sent by Shortboxerr at {notification.Timestamp:g} UTC</p>");
        sb.AppendLine("</div>");
        
        sb.AppendLine("</div>"); // End container
        sb.AppendLine("</body></html>");
        
        return sb.ToString();
    }
    
    private static string GetEventColor(NotificationEventType eventType)
    {
        return eventType switch
        {
            NotificationEventType.Test => "#3498db",
            NotificationEventType.NewRelease => "#2ecc71",
            NotificationEventType.Grabbed => "#9b59b6",
            NotificationEventType.Imported => "#27ae60",
            NotificationEventType.DownloadFailed => "#e74c3c",
            NotificationEventType.Health => "#f39c12",
            NotificationEventType.WeeklySummary => "#3498db",
            NotificationEventType.SeriesAdded => "#1abc9c",
            NotificationEventType.Update => "#34495e",
            _ => "#95a5a6"
        };
    }
    
    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
