using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for managing user notifications.
/// </summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        #region Notification Queries

        // GET /api/v1/notifications - get all notifications
        group.MapGet("/", async (
            [FromQuery] bool? unreadOnly,
            [FromQuery] string? types,
            [FromQuery] int? seriesId,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var filter = new NotificationFilter
            {
                UnreadOnly = unreadOnly,
                SeriesId = seriesId,
                Limit = limit ?? 50,
                Offset = offset ?? 0
            };

            if (!string.IsNullOrEmpty(types))
            {
                filter.Types = types
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => Enum.TryParse<NotificationType>(t, true, out var nt) ? nt : (NotificationType?)null)
                    .Where(t => t.HasValue)
                    .Select(t => t!.Value)
                    .ToList();
            }

            var notifications = await notificationService.GetNotificationsAsync(filter, cancellationToken);
            var unreadCount = await notificationService.GetUnreadCountAsync(cancellationToken);
            
            return Results.Ok(new NotificationListResponse
            {
                Notifications = notifications,
                UnreadCount = unreadCount,
                TotalReturned = notifications.Count
            });
        })
        .WithName("GetNotifications")
        .WithDescription("Gets all notifications with optional filtering")
        .Produces<NotificationListResponse>(200);

        // GET /api/v1/notifications/unread/count - get unread count
        group.MapGet("/unread/count", async (
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var count = await notificationService.GetUnreadCountAsync(cancellationToken);
            return Results.Ok(new { UnreadCount = count });
        })
        .WithName("GetUnreadNotificationCount")
        .WithDescription("Gets the count of unread notifications")
        .Produces<object>(200);

        // GET /api/v1/notifications/{id} - get a specific notification
        group.MapGet("/{id}", async (
            int id,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var notification = await notificationService.GetByIdAsync(id, cancellationToken);
            return notification != null 
                ? Results.Ok(notification) 
                : Results.NotFound(new { Error = "Notification not found" });
        })
        .WithName("GetNotification")
        .WithDescription("Gets a specific notification by ID")
        .Produces<Notification>(200)
        .Produces(404);

        #endregion

        #region Notification Actions

        // POST /api/v1/notifications/{id}/read - mark as read
        group.MapPost("/{id}/read", async (
            int id,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var success = await notificationService.MarkAsReadAsync(id, cancellationToken);
            return success 
                ? Results.Ok(new { Success = true }) 
                : Results.NotFound(new { Error = "Notification not found" });
        })
        .WithName("MarkNotificationAsRead")
        .WithDescription("Marks a notification as read")
        .Produces<object>(200)
        .Produces(404);

        // POST /api/v1/notifications/read-all - mark all as read
        group.MapPost("/read-all", async (
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var count = await notificationService.MarkAllAsReadAsync(cancellationToken);
            return Results.Ok(new { MarkedRead = count });
        })
        .WithName("MarkAllNotificationsAsRead")
        .WithDescription("Marks all notifications as read")
        .Produces<object>(200);

        // DELETE /api/v1/notifications/{id} - delete a notification
        group.MapDelete("/{id}", async (
            int id,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var success = await notificationService.DeleteAsync(id, cancellationToken);
            return success 
                ? Results.Ok(new { Success = true }) 
                : Results.NotFound(new { Error = "Notification not found" });
        })
        .WithName("DeleteNotification")
        .WithDescription("Deletes a notification")
        .Produces<object>(200)
        .Produces(404);

        // DELETE /api/v1/notifications/read - delete all read notifications
        group.MapDelete("/read", async (
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var count = await notificationService.DeleteReadAsync(cancellationToken);
            return Results.Ok(new { Deleted = count });
        })
        .WithName("DeleteReadNotifications")
        .WithDescription("Deletes all read notifications")
        .Produces<object>(200);

        #endregion

        #region Settings

        // GET /api/v1/notifications/settings - get notification settings
        group.MapGet("/settings", async (
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var settings = await notificationService.GetSettingsAsync(cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetNotificationSettings")
        .WithDescription("Gets notification settings")
        .Produces<NotificationSettings>(200);

        // PUT /api/v1/notifications/settings - update notification settings
        group.MapPut("/settings", async (
            [FromBody] NotificationSettings settings,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            await notificationService.UpdateSettingsAsync(settings, cancellationToken);
            return Results.Ok(new { Success = true });
        })
        .WithName("UpdateNotificationSettings")
        .WithDescription("Updates notification settings")
        .Produces<object>(200);

        #endregion

        #region Webhook Providers

        // GET /api/v1/notifications/providers - get all webhook providers
        group.MapGet("/providers", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers", 
                new List<WebhookProviderSettings>(), 
                cancellationToken) ?? [];
            return Results.Ok(providers);
        })
        .WithName("GetWebhookProviders")
        .WithDescription("Gets all configured webhook notification providers")
        .Produces<List<WebhookProviderSettings>>(200);

        // GET /api/v1/notifications/providers/{id} - get a specific webhook provider
        group.MapGet("/providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers",
                new List<WebhookProviderSettings>(),
                cancellationToken) ?? [];
            var provider = providers.FirstOrDefault(p => p.Id == id);
            return provider != null 
                ? Results.Ok(provider) 
                : Results.NotFound(new { Error = "Webhook provider not found" });
        })
        .WithName("GetWebhookProvider")
        .WithDescription("Gets a specific webhook provider by ID")
        .Produces<WebhookProviderSettings>(200)
        .Produces(404);

        // POST /api/v1/notifications/providers - add a new webhook provider
        group.MapPost("/providers", async (
            [FromBody] WebhookProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.WebhookUrl))
            {
                return Results.BadRequest(new { Error = "Webhook URL is required" });
            }

            // Ensure ID is set
            if (string.IsNullOrEmpty(provider.Id))
            {
                provider.Id = Guid.NewGuid().ToString();
            }

            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers",
                new List<WebhookProviderSettings>(),
                cancellationToken) ?? [];

            // Check for duplicate name
            if (providers.Any(p => p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A provider with name '{provider.Name}' already exists" });
            }

            providers.Add(provider);
            await settingsService.SetAsync("notification_providers", providers, cancellationToken);

            return Results.Created($"/api/v1/notifications/providers/{provider.Id}", provider);
        })
        .WithName("AddWebhookProvider")
        .WithDescription("Adds a new webhook notification provider")
        .Produces<WebhookProviderSettings>(201)
        .Produces(400)
        .Produces(409);

        // PUT /api/v1/notifications/providers/{id} - update a webhook provider
        group.MapPut("/providers/{id}", async (
            string id,
            [FromBody] WebhookProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.WebhookUrl))
            {
                return Results.BadRequest(new { Error = "Webhook URL is required" });
            }

            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers",
                new List<WebhookProviderSettings>(),
                cancellationToken) ?? [];

            var existingIndex = providers.FindIndex(p => p.Id == id);
            if (existingIndex == -1)
            {
                return Results.NotFound(new { Error = "Webhook provider not found" });
            }

            // Check for duplicate name (excluding self)
            if (providers.Any(p => p.Id != id && p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A provider with name '{provider.Name}' already exists" });
            }

            provider.Id = id; // Ensure ID matches URL
            providers[existingIndex] = provider;
            await settingsService.SetAsync("notification_providers", providers, cancellationToken);

            return Results.Ok(provider);
        })
        .WithName("UpdateWebhookProvider")
        .WithDescription("Updates an existing webhook notification provider")
        .Produces<WebhookProviderSettings>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        // DELETE /api/v1/notifications/providers/{id} - delete a webhook provider
        group.MapDelete("/providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers",
                new List<WebhookProviderSettings>(),
                cancellationToken) ?? [];

            var removed = providers.RemoveAll(p => p.Id == id);
            if (removed == 0)
            {
                return Results.NotFound(new { Error = "Webhook provider not found" });
            }

            await settingsService.SetAsync("notification_providers", providers, cancellationToken);
            return Results.Ok(new { Success = true });
        })
        .WithName("DeleteWebhookProvider")
        .WithDescription("Deletes a webhook notification provider")
        .Produces<object>(200)
        .Produces(404);

        // POST /api/v1/notifications/providers/{id}/test - test a webhook provider
        group.MapPost("/providers/{id}/test", async (
            string id,
            [FromServices] ISettingsService settingsService,
            [FromServices] INotificationProvider webhookProvider,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<WebhookProviderSettings>>(
                "notification_providers",
                new List<WebhookProviderSettings>(),
                cancellationToken) ?? [];

            var provider = providers.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Webhook provider not found" });
            }

            var result = await webhookProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new WebhookTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestWebhookProvider")
        .WithDescription("Tests a webhook notification provider")
        .Produces<WebhookTestResponse>(200)
        .Produces(404);

        // POST /api/v1/notifications/providers/test - test a webhook provider with settings
        group.MapPost("/providers/test", async (
            [FromBody] WebhookProviderSettings provider,
            [FromServices] INotificationProvider webhookProvider,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.WebhookUrl))
            {
                return Results.BadRequest(new { Error = "Webhook URL is required" });
            }

            var result = await webhookProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new WebhookTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestWebhookProviderSettings")
        .WithDescription("Tests webhook notification settings without saving")
        .Produces<WebhookTestResponse>(200)
        .Produces(400);

        #endregion

        #region Email Providers

        // GET /api/v1/notifications/email-providers - get all email providers
        group.MapGet("/email-providers", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers", 
                new List<EmailProviderSettings>(), 
                cancellationToken) ?? [];
            return Results.Ok(providers);
        })
        .WithName("GetEmailProviders")
        .WithDescription("Gets all configured email notification providers")
        .Produces<List<EmailProviderSettings>>(200);

        // GET /api/v1/notifications/email-providers/{id} - get a specific email provider
        group.MapGet("/email-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers",
                new List<EmailProviderSettings>(),
                cancellationToken) ?? [];
            var provider = providers.FirstOrDefault(p => p.Id == id);
            return provider != null 
                ? Results.Ok(provider) 
                : Results.NotFound(new { Error = "Email provider not found" });
        })
        .WithName("GetEmailProvider")
        .WithDescription("Gets a specific email provider by ID")
        .Produces<EmailProviderSettings>(200)
        .Produces(404);

        // POST /api/v1/notifications/email-providers - add a new email provider
        group.MapPost("/email-providers", async (
            [FromBody] EmailProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.SmtpServer))
            {
                return Results.BadRequest(new { Error = "SMTP server is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.SenderEmail))
            {
                return Results.BadRequest(new { Error = "Sender email is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.RecipientEmails))
            {
                return Results.BadRequest(new { Error = "At least one recipient email is required" });
            }

            if (string.IsNullOrEmpty(provider.Id))
            {
                provider.Id = Guid.NewGuid().ToString();
            }

            var providers = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers",
                new List<EmailProviderSettings>(),
                cancellationToken) ?? [];

            if (providers.Any(p => p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"An email provider with name '{provider.Name}' already exists" });
            }

            providers.Add(provider);
            await settingsService.SetAsync("email_notification_providers", providers, cancellationToken);

            return Results.Created($"/api/v1/notifications/email-providers/{provider.Id}", provider);
        })
        .WithName("AddEmailProvider")
        .WithDescription("Adds a new email notification provider")
        .Produces<EmailProviderSettings>(201)
        .Produces(400)
        .Produces(409);

        // PUT /api/v1/notifications/email-providers/{id} - update an email provider
        group.MapPut("/email-providers/{id}", async (
            string id,
            [FromBody] EmailProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.SmtpServer))
            {
                return Results.BadRequest(new { Error = "SMTP server is required" });
            }

            var providers = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers",
                new List<EmailProviderSettings>(),
                cancellationToken) ?? [];

            var existingIndex = providers.FindIndex(p => p.Id == id);
            if (existingIndex == -1)
            {
                return Results.NotFound(new { Error = "Email provider not found" });
            }

            if (providers.Any(p => p.Id != id && p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"An email provider with name '{provider.Name}' already exists" });
            }

            provider.Id = id;
            providers[existingIndex] = provider;
            await settingsService.SetAsync("email_notification_providers", providers, cancellationToken);

            return Results.Ok(provider);
        })
        .WithName("UpdateEmailProvider")
        .WithDescription("Updates an existing email notification provider")
        .Produces<EmailProviderSettings>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        // DELETE /api/v1/notifications/email-providers/{id} - delete an email provider
        group.MapDelete("/email-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers",
                new List<EmailProviderSettings>(),
                cancellationToken) ?? [];

            var provider = providers.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Email provider not found" });
            }

            providers.Remove(provider);
            await settingsService.SetAsync("email_notification_providers", providers, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteEmailProvider")
        .WithDescription("Deletes an email notification provider")
        .Produces(204)
        .Produces(404);

        // POST /api/v1/notifications/email-providers/{id}/test - test an existing email provider
        group.MapPost("/email-providers/{id}/test", async (
            string id,
            [FromServices] ISettingsService settingsService,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            var allProviders = await settingsService.GetAsync<List<EmailProviderSettings>>(
                "email_notification_providers",
                new List<EmailProviderSettings>(),
                cancellationToken) ?? [];

            var provider = allProviders.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Email provider not found" });
            }

            var emailProvider = providers.FirstOrDefault(p => p.ProviderType == "Email");
            if (emailProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await emailProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new EmailTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestEmailProvider")
        .WithDescription("Tests an email notification provider")
        .Produces<EmailTestResponse>(200)
        .Produces(404);

        // POST /api/v1/notifications/email-providers/test - test email provider with settings
        group.MapPost("/email-providers/test", async (
            [FromBody] EmailProviderSettings provider,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.SmtpServer))
            {
                return Results.BadRequest(new { Error = "SMTP server is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.SenderEmail))
            {
                return Results.BadRequest(new { Error = "Sender email is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.RecipientEmails))
            {
                return Results.BadRequest(new { Error = "At least one recipient email is required" });
            }

            var emailProvider = providers.FirstOrDefault(p => p.ProviderType == "Email");
            if (emailProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await emailProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new EmailTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestEmailProviderSettings")
        .WithDescription("Tests email notification settings without saving")
        .Produces<EmailTestResponse>(200)
        .Produces(400);

        #endregion

        #region Pushover Providers

        // GET /api/v1/notifications/pushover-providers - get all pushover providers
        group.MapGet("/pushover-providers", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers", 
                new List<PushoverProviderSettings>(), 
                cancellationToken) ?? [];
            return Results.Ok(providers);
        })
        .WithName("GetPushoverProviders")
        .WithDescription("Gets all configured Pushover notification providers")
        .Produces<List<PushoverProviderSettings>>(200);

        // GET /api/v1/notifications/pushover-providers/{id}
        group.MapGet("/pushover-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers",
                new List<PushoverProviderSettings>(),
                cancellationToken) ?? [];
            var provider = providers.FirstOrDefault(p => p.Id == id);
            return provider != null 
                ? Results.Ok(provider) 
                : Results.NotFound(new { Error = "Pushover provider not found" });
        })
        .WithName("GetPushoverProvider")
        .WithDescription("Gets a specific Pushover provider by ID")
        .Produces<PushoverProviderSettings>(200)
        .Produces(404);

        // POST /api/v1/notifications/pushover-providers
        group.MapPost("/pushover-providers", async (
            [FromBody] PushoverProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.ApiToken))
            {
                return Results.BadRequest(new { Error = "API Token is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.UserKey))
            {
                return Results.BadRequest(new { Error = "User Key is required" });
            }

            if (string.IsNullOrEmpty(provider.Id))
            {
                provider.Id = Guid.NewGuid().ToString();
            }

            var providers = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers",
                new List<PushoverProviderSettings>(),
                cancellationToken) ?? [];

            if (providers.Any(p => p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A Pushover provider with name '{provider.Name}' already exists" });
            }

            providers.Add(provider);
            await settingsService.SetAsync("pushover_notification_providers", providers, cancellationToken);

            return Results.Created($"/api/v1/notifications/pushover-providers/{provider.Id}", provider);
        })
        .WithName("AddPushoverProvider")
        .WithDescription("Adds a new Pushover notification provider")
        .Produces<PushoverProviderSettings>(201)
        .Produces(400)
        .Produces(409);

        // PUT /api/v1/notifications/pushover-providers/{id}
        group.MapPut("/pushover-providers/{id}", async (
            string id,
            [FromBody] PushoverProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.ApiToken))
            {
                return Results.BadRequest(new { Error = "API Token is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.UserKey))
            {
                return Results.BadRequest(new { Error = "User Key is required" });
            }

            var providers = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers",
                new List<PushoverProviderSettings>(),
                cancellationToken) ?? [];

            var existingIndex = providers.FindIndex(p => p.Id == id);
            if (existingIndex == -1)
            {
                return Results.NotFound(new { Error = "Pushover provider not found" });
            }

            if (providers.Any(p => p.Id != id && p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A Pushover provider with name '{provider.Name}' already exists" });
            }

            provider.Id = id;
            providers[existingIndex] = provider;
            await settingsService.SetAsync("pushover_notification_providers", providers, cancellationToken);

            return Results.Ok(provider);
        })
        .WithName("UpdatePushoverProvider")
        .WithDescription("Updates an existing Pushover notification provider")
        .Produces<PushoverProviderSettings>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        // DELETE /api/v1/notifications/pushover-providers/{id}
        group.MapDelete("/pushover-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers",
                new List<PushoverProviderSettings>(),
                cancellationToken) ?? [];

            var provider = providers.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Pushover provider not found" });
            }

            providers.Remove(provider);
            await settingsService.SetAsync("pushover_notification_providers", providers, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeletePushoverProvider")
        .WithDescription("Deletes a Pushover notification provider")
        .Produces(204)
        .Produces(404);

        // POST /api/v1/notifications/pushover-providers/{id}/test
        group.MapPost("/pushover-providers/{id}/test", async (
            string id,
            [FromServices] ISettingsService settingsService,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            var allProviders = await settingsService.GetAsync<List<PushoverProviderSettings>>(
                "pushover_notification_providers",
                new List<PushoverProviderSettings>(),
                cancellationToken) ?? [];

            var provider = allProviders.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Pushover provider not found" });
            }

            var pushoverProvider = providers.FirstOrDefault(p => p.ProviderType == "Pushover");
            if (pushoverProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await pushoverProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestPushoverProvider")
        .WithDescription("Tests a Pushover notification provider")
        .Produces<PushTestResponse>(200)
        .Produces(404);

        // POST /api/v1/notifications/pushover-providers/test
        group.MapPost("/pushover-providers/test", async (
            [FromBody] PushoverProviderSettings provider,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.ApiToken))
            {
                return Results.BadRequest(new { Error = "API Token is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.UserKey))
            {
                return Results.BadRequest(new { Error = "User Key is required" });
            }

            var pushoverProvider = providers.FirstOrDefault(p => p.ProviderType == "Pushover");
            if (pushoverProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await pushoverProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestPushoverProviderSettings")
        .WithDescription("Tests Pushover notification settings without saving")
        .Produces<PushTestResponse>(200)
        .Produces(400);

        #endregion

        #region Pushbullet Providers

        // GET /api/v1/notifications/pushbullet-providers
        group.MapGet("/pushbullet-providers", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers", 
                new List<PushbulletProviderSettings>(), 
                cancellationToken) ?? [];
            return Results.Ok(providers);
        })
        .WithName("GetPushbulletProviders")
        .WithDescription("Gets all configured Pushbullet notification providers")
        .Produces<List<PushbulletProviderSettings>>(200);

        // GET /api/v1/notifications/pushbullet-providers/{id}
        group.MapGet("/pushbullet-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers",
                new List<PushbulletProviderSettings>(),
                cancellationToken) ?? [];
            var provider = providers.FirstOrDefault(p => p.Id == id);
            return provider != null 
                ? Results.Ok(provider) 
                : Results.NotFound(new { Error = "Pushbullet provider not found" });
        })
        .WithName("GetPushbulletProvider")
        .WithDescription("Gets a specific Pushbullet provider by ID")
        .Produces<PushbulletProviderSettings>(200)
        .Produces(404);

        // POST /api/v1/notifications/pushbullet-providers
        group.MapPost("/pushbullet-providers", async (
            [FromBody] PushbulletProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.AccessToken))
            {
                return Results.BadRequest(new { Error = "Access Token is required" });
            }

            if (string.IsNullOrEmpty(provider.Id))
            {
                provider.Id = Guid.NewGuid().ToString();
            }

            var providers = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers",
                new List<PushbulletProviderSettings>(),
                cancellationToken) ?? [];

            if (providers.Any(p => p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A Pushbullet provider with name '{provider.Name}' already exists" });
            }

            providers.Add(provider);
            await settingsService.SetAsync("pushbullet_notification_providers", providers, cancellationToken);

            return Results.Created($"/api/v1/notifications/pushbullet-providers/{provider.Id}", provider);
        })
        .WithName("AddPushbulletProvider")
        .WithDescription("Adds a new Pushbullet notification provider")
        .Produces<PushbulletProviderSettings>(201)
        .Produces(400)
        .Produces(409);

        // PUT /api/v1/notifications/pushbullet-providers/{id}
        group.MapPut("/pushbullet-providers/{id}", async (
            string id,
            [FromBody] PushbulletProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }
            if (string.IsNullOrWhiteSpace(provider.AccessToken))
            {
                return Results.BadRequest(new { Error = "Access Token is required" });
            }

            var providers = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers",
                new List<PushbulletProviderSettings>(),
                cancellationToken) ?? [];

            var existingIndex = providers.FindIndex(p => p.Id == id);
            if (existingIndex == -1)
            {
                return Results.NotFound(new { Error = "Pushbullet provider not found" });
            }

            if (providers.Any(p => p.Id != id && p.Name.Equals(provider.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new { Error = $"A Pushbullet provider with name '{provider.Name}' already exists" });
            }

            provider.Id = id;
            providers[existingIndex] = provider;
            await settingsService.SetAsync("pushbullet_notification_providers", providers, cancellationToken);

            return Results.Ok(provider);
        })
        .WithName("UpdatePushbulletProvider")
        .WithDescription("Updates an existing Pushbullet notification provider")
        .Produces<PushbulletProviderSettings>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        // DELETE /api/v1/notifications/pushbullet-providers/{id}
        group.MapDelete("/pushbullet-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers",
                new List<PushbulletProviderSettings>(),
                cancellationToken) ?? [];

            var provider = providers.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Pushbullet provider not found" });
            }

            providers.Remove(provider);
            await settingsService.SetAsync("pushbullet_notification_providers", providers, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeletePushbulletProvider")
        .WithDescription("Deletes a Pushbullet notification provider")
        .Produces(204)
        .Produces(404);

        // POST /api/v1/notifications/pushbullet-providers/{id}/test
        group.MapPost("/pushbullet-providers/{id}/test", async (
            string id,
            [FromServices] ISettingsService settingsService,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            var allProviders = await settingsService.GetAsync<List<PushbulletProviderSettings>>(
                "pushbullet_notification_providers",
                new List<PushbulletProviderSettings>(),
                cancellationToken) ?? [];

            var provider = allProviders.FirstOrDefault(p => p.Id == id);
            if (provider == null)
            {
                return Results.NotFound(new { Error = "Pushbullet provider not found" });
            }

            var pushbulletProvider = providers.FirstOrDefault(p => p.ProviderType == "Pushbullet");
            if (pushbulletProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await pushbulletProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestPushbulletProvider")
        .WithDescription("Tests a Pushbullet notification provider")
        .Produces<PushTestResponse>(200)
        .Produces(404);

        // POST /api/v1/notifications/pushbullet-providers/test
        group.MapPost("/pushbullet-providers/test", async (
            [FromBody] PushbulletProviderSettings provider,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.AccessToken))
            {
                return Results.BadRequest(new { Error = "Access Token is required" });
            }

            var pushbulletProvider = providers.FirstOrDefault(p => p.ProviderType == "Pushbullet");
            if (pushbulletProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await pushbulletProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestPushbulletProviderSettings")
        .WithDescription("Tests Pushbullet notification settings without saving")
        .Produces<PushTestResponse>(200)
        .Produces(400);

        #endregion

        #region Telegram Providers

        // GET /api/v1/notifications/telegram-providers
        group.MapGet("/telegram-providers", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken);
            return Results.Ok(providers);
        })
        .WithName("GetTelegramProviders")
        .WithDescription("Gets all configured Telegram notification providers")
        .Produces<List<TelegramProviderSettings>>(200);

        // GET /api/v1/notifications/telegram-providers/{id}
        group.MapGet("/telegram-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken);
            var provider = providers?.FirstOrDefault(p => p.Id == id);
            return provider != null ? Results.Ok(provider) : Results.NotFound();
        })
        .WithName("GetTelegramProvider")
        .WithDescription("Gets a specific Telegram notification provider")
        .Produces<TelegramProviderSettings>(200)
        .Produces(404);

        // POST /api/v1/notifications/telegram-providers
        group.MapPost("/telegram-providers", async (
            [FromBody] TelegramProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.BotToken))
            {
                return Results.BadRequest(new { Error = "Bot Token is required" });
            }
            
            if (string.IsNullOrWhiteSpace(provider.ChatId))
            {
                return Results.BadRequest(new { Error = "Chat ID is required" });
            }

            var providers = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken) ?? new List<TelegramProviderSettings>();

            if (providers.Any(p => p.Id == provider.Id))
            {
                return Results.Conflict(new { Error = "Provider with this ID already exists" });
            }

            provider.ProviderType = "Telegram";
            providers.Add(provider);
            await settingsService.SetAsync("telegram_notification_providers", providers, cancellationToken);

            return Results.Created($"/api/v1/notifications/telegram-providers/{provider.Id}", provider);
        })
        .WithName("AddTelegramProvider")
        .WithDescription("Adds a new Telegram notification provider")
        .Produces<TelegramProviderSettings>(201)
        .Produces(400)
        .Produces(409);

        // PUT /api/v1/notifications/telegram-providers/{id}
        group.MapPut("/telegram-providers/{id}", async (
            string id,
            [FromBody] TelegramProviderSettings provider,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.BotToken))
            {
                return Results.BadRequest(new { Error = "Bot Token is required" });
            }
            
            if (string.IsNullOrWhiteSpace(provider.ChatId))
            {
                return Results.BadRequest(new { Error = "Chat ID is required" });
            }

            var providers = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken) ?? new List<TelegramProviderSettings>();

            var index = providers.FindIndex(p => p.Id == id);
            if (index < 0)
            {
                return Results.NotFound();
            }

            if (id != provider.Id && providers.Any(p => p.Id == provider.Id))
            {
                return Results.Conflict(new { Error = "Provider with this ID already exists" });
            }

            provider.ProviderType = "Telegram";
            providers[index] = provider;
            await settingsService.SetAsync("telegram_notification_providers", providers, cancellationToken);

            return Results.Ok(provider);
        })
        .WithName("UpdateTelegramProvider")
        .WithDescription("Updates an existing Telegram notification provider")
        .Produces<TelegramProviderSettings>(200)
        .Produces(400)
        .Produces(404)
        .Produces(409);

        // DELETE /api/v1/notifications/telegram-providers/{id}
        group.MapDelete("/telegram-providers/{id}", async (
            string id,
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var providers = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken) ?? new List<TelegramProviderSettings>();

            var removed = providers.RemoveAll(p => p.Id == id);
            if (removed == 0)
            {
                return Results.NotFound();
            }

            await settingsService.SetAsync("telegram_notification_providers", providers, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteTelegramProvider")
        .WithDescription("Deletes a Telegram notification provider")
        .Produces(204)
        .Produces(404);

        // POST /api/v1/notifications/telegram-providers/{id}/test
        group.MapPost("/telegram-providers/{id}/test", async (
            string id,
            [FromServices] ISettingsService settingsService,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            var telegramProviders = await settingsService.GetAsync<List<TelegramProviderSettings>>(
                "telegram_notification_providers", new List<TelegramProviderSettings>(), cancellationToken);
            var settings = telegramProviders?.FirstOrDefault(p => p.Id == id);
            
            if (settings == null)
            {
                return Results.NotFound();
            }

            var telegramProvider = providers.FirstOrDefault(p => p.ProviderType == "Telegram");
            if (telegramProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await telegramProvider.TestAsync(settings, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestTelegramProvider")
        .WithDescription("Tests a saved Telegram notification provider")
        .Produces<PushTestResponse>(200)
        .Produces(404);

        // POST /api/v1/notifications/telegram-providers/test
        group.MapPost("/telegram-providers/test", async (
            [FromBody] TelegramProviderSettings provider,
            [FromServices] IEnumerable<INotificationProvider> providers,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(provider.BotToken))
            {
                return Results.BadRequest(new { Error = "Bot Token is required" });
            }
            
            if (string.IsNullOrWhiteSpace(provider.ChatId))
            {
                return Results.BadRequest(new { Error = "Chat ID is required" });
            }

            var telegramProvider = providers.FirstOrDefault(p => p.ProviderType == "Telegram");
            if (telegramProvider == null)
            {
                return Results.StatusCode(500);
            }

            var result = await telegramProvider.TestAsync(provider, cancellationToken);
            return Results.Ok(new PushTestResponse
            {
                Success = result.Success,
                Message = result.Message,
                Latency = result.Latency
            });
        })
        .WithName("TestTelegramProviderSettings")
        .WithDescription("Tests Telegram notification settings without saving")
        .Produces<PushTestResponse>(200)
        .Produces(400);

        #endregion

        #region Test Endpoints (for development/testing)

        // POST /api/v1/notifications/test - create a test notification
        group.MapPost("/test", async (
            [FromBody] CreateNotificationRequest request,
            [FromServices] INotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var notification = await notificationService.CreateAsync(request, cancellationToken);
            return Results.Ok(notification);
        })
        .WithName("CreateTestNotification")
        .WithDescription("Creates a test notification (for development)")
        .Produces<Notification>(200);

        #endregion
    }
}

#region Response DTOs

/// <summary>
/// Response containing a list of notifications.
/// </summary>
public class NotificationListResponse
{
    public List<Notification> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalReturned { get; set; }
}

/// <summary>
/// Response from testing a webhook provider.
/// </summary>
public class WebhookTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan? Latency { get; set; }
}

/// <summary>
/// Response from testing an email provider.
/// </summary>
public class EmailTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan? Latency { get; set; }
}

/// <summary>
/// Response from testing a push notification provider (Pushover/Pushbullet).
/// </summary>
public class PushTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan? Latency { get; set; }
}

#endregion
