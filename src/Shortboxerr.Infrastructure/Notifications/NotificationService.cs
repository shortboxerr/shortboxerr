using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Notifications;

/// <summary>
/// Service for managing in-app notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<NotificationService> _logger;
    
    private const string NotificationSettingsKey = "notifications";

    public NotificationService(
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    #region Notification Management

    public async Task<List<Notification>> GetNotificationsAsync(
        NotificationFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications.AsQueryable();

        if (filter?.UnreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (filter?.Types != null && filter.Types.Count > 0)
        {
            query = query.Where(n => filter.Types.Contains(n.Type));
        }

        if (filter?.SeriesId != null)
        {
            query = query.Where(n => n.SeriesId == filter.SeriesId);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(filter?.Offset ?? 0)
            .Take(filter?.Limit ?? 50)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .CountAsync(n => !n.IsRead, cancellationToken);
    }

    public async Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var unreadNotifications = await _dbContext.Notifications
            .Where(n => !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Marked {Count} notifications as read", unreadNotifications.Count);
        return unreadNotifications.Count;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        _dbContext.Notifications.Remove(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteReadAsync(CancellationToken cancellationToken = default)
    {
        var readNotifications = await _dbContext.Notifications
            .Where(n => n.IsRead)
            .ToListAsync(cancellationToken);

        _dbContext.Notifications.RemoveRange(readNotifications);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} read notifications", readNotifications.Count);
        return readNotifications.Count;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var oldNotifications = await _dbContext.Notifications
            .Where(n => n.CreatedAt < date)
            .ToListAsync(cancellationToken);

        _dbContext.Notifications.RemoveRange(oldNotifications);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} notifications older than {Date}", oldNotifications.Count, date);
        return oldNotifications.Count;
    }

    #endregion

    #region Notification Creation

    public async Task<Notification> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.EnableInApp)
        {
            _logger.LogDebug("In-app notifications disabled, skipping notification creation");
            // Return a placeholder notification without persisting
            return new Notification
            {
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                Link = request.Link,
                SeriesId = request.SeriesId,
                IssueId = request.IssueId,
                Data = request.Data
            };
        }

        var notification = new Notification
        {
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            Link = request.Link,
            SeriesId = request.SeriesId,
            IssueId = request.IssueId,
            Data = request.Data,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enforce max notifications limit
        await EnforceMaxNotificationsAsync(settings, cancellationToken);

        _logger.LogDebug("Created notification: {Title}", notification.Title);
        return notification;
    }

    public async Task<Notification?> SendNewReleaseNotificationAsync(
        NewReleaseNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.NewReleaseNotifications)
        {
            _logger.LogDebug("New release notifications disabled");
            return null;
        }

        if (request.IssueCount == 0)
        {
            _logger.LogDebug("No releases to notify about");
            return null;
        }

        string title;
        string message;

        if (settings.AggregateReleaseNotifications)
        {
            title = $"{request.IssueCount} Issue{(request.IssueCount > 1 ? "s" : "")} Releasing This Week";
            var seriesList = request.SeriesTitles.Count > 3
                ? string.Join(", ", request.SeriesTitles.Take(3)) + $" and {request.SeriesTitles.Count - 3} more"
                : string.Join(", ", request.SeriesTitles);
            message = $"New releases on {request.ReleaseDate:MMM dd}: {seriesList}";
        }
        else
        {
            title = "New Releases This Week";
            message = $"{request.IssueCount} issues releasing on {request.ReleaseDate:MMM dd, yyyy}";
        }

        return await CreateAsync(new CreateNotificationRequest
        {
            Type = NotificationType.NewRelease,
            Title = title,
            Message = message,
            Link = "/pulllist"
        }, cancellationToken);
    }

    public async Task<Notification?> SendGrabbedNotificationAsync(
        GrabbedNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.GrabbedNotifications)
        {
            _logger.LogDebug("Grabbed notifications disabled");
            return null;
        }

        var title = $"Grabbed: {request.SeriesTitle} #{request.IssueNumber}";
        var message = $"Successfully grabbed from {request.DownloadSource ?? "unknown source"}";

        return await CreateAsync(new CreateNotificationRequest
        {
            Type = NotificationType.Grabbed,
            Title = title,
            Message = message,
            Link = $"/series/{request.SeriesId}",
            SeriesId = request.SeriesId,
            IssueId = request.IssueId
        }, cancellationToken);
    }

    public async Task<Notification?> SendWeeklySummaryAsync(
        WeeklySummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.WeeklySummaryNotifications)
        {
            _logger.LogDebug("Weekly summary notifications disabled");
            return null;
        }

        var title = $"Weekly Pull List Summary";
        var message = $"Week of {request.WeekOf:MMM dd}: {request.TotalReleases} releases, " +
                      $"{request.WantedCount} wanted, {request.OwnedCount} owned, {request.SkippedCount} skipped";

        return await CreateAsync(new CreateNotificationRequest
        {
            Type = NotificationType.WeeklySummary,
            Title = title,
            Message = message,
            Link = "/pulllist"
        }, cancellationToken);
    }

    #endregion

    #region Settings

    public async Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsService.GetAsync(
            NotificationSettingsKey, 
            new NotificationSettings(), 
            cancellationToken) ?? new NotificationSettings();
    }

    public async Task UpdateSettingsAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsService.SetAsync(NotificationSettingsKey, settings, cancellationToken);
        _logger.LogInformation("Notification settings updated");
    }

    #endregion

    #region Private Helpers

    private async Task EnforceMaxNotificationsAsync(NotificationSettings settings, CancellationToken cancellationToken)
    {
        if (settings.MaxNotifications <= 0)
        {
            return;
        }

        var count = await _dbContext.Notifications.CountAsync(cancellationToken);
        if (count <= settings.MaxNotifications)
        {
            return;
        }

        // Delete oldest notifications beyond limit
        var toDelete = count - settings.MaxNotifications;
        var oldestNotifications = await _dbContext.Notifications
            .OrderBy(n => n.CreatedAt)
            .Take(toDelete)
            .ToListAsync(cancellationToken);

        if (oldestNotifications.Count > 0)
        {
            _dbContext.Notifications.RemoveRange(oldestNotifications);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Deleted {Count} oldest notifications to enforce limit of {Max}", 
                oldestNotifications.Count, settings.MaxNotifications);
        }
    }

    #endregion
}
