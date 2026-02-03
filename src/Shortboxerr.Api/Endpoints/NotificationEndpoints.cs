using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Notifications;

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

#endregion
