using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Notifications;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

public class NotificationServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<NotificationService>>();

        // Default settings - in-app enabled
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true });

        _service = new NotificationService(_dbContext, _mockSettingsService.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Notification Management Tests

    [Fact]
    public async Task CreateAsync_CreatesNotification()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Type = NotificationType.Info,
            Title = "Test Notification",
            Message = "This is a test"
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Notification", result.Title);
        Assert.Equal("This is a test", result.Message);
        Assert.Equal(NotificationType.Info, result.Type);
        Assert.False(result.IsRead);

        var dbNotification = await _dbContext.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(dbNotification);
        Assert.Equal(result.Id, dbNotification.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenDisabled_ReturnsPlaceholderWithoutPersisting()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = false });

        var request = new CreateNotificationRequest
        {
            Type = NotificationType.Info,
            Title = "Test",
            Message = "Should not be persisted"
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Id); // Not persisted, so ID is 0
        Assert.Empty(await _dbContext.Notifications.ToListAsync());
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsAllNotifications()
    {
        // Arrange
        await CreateTestNotifications(5);

        // Act
        var result = await _service.GetNotificationsAsync();

        // Assert
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_WithUnreadOnlyFilter_ReturnsOnlyUnread()
    {
        // Arrange
        await CreateTestNotifications(3, isRead: false);
        await CreateTestNotifications(2, isRead: true);

        // Act
        var result = await _service.GetNotificationsAsync(new NotificationFilter { UnreadOnly = true });

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task GetNotificationsAsync_WithTypeFilter_ReturnsMatchingTypes()
    {
        // Arrange
        _dbContext.Notifications.Add(new Notification { Title = "Info", Message = "...", Type = NotificationType.Info });
        _dbContext.Notifications.Add(new Notification { Title = "Warning", Message = "...", Type = NotificationType.Warning });
        _dbContext.Notifications.Add(new Notification { Title = "Error", Message = "...", Type = NotificationType.Error });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetNotificationsAsync(new NotificationFilter 
        { 
            Types = new List<NotificationType> { NotificationType.Warning, NotificationType.Error } 
        });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, n => n.Type == NotificationType.Info);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await CreateTestNotifications(3, isRead: false);
        await CreateTestNotifications(2, isRead: true);

        // Act
        var count = await _service.GetUnreadCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task MarkAsReadAsync_MarksNotificationAsRead()
    {
        // Arrange
        var notification = new Notification { Title = "Test", Message = "...", IsRead = false };
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.MarkAsReadAsync(notification.Id);

        // Assert
        Assert.True(result);
        var updated = await _dbContext.Notifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsfalseForNonexistent()
    {
        // Act
        var result = await _service.MarkAsReadAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        // Arrange
        await CreateTestNotifications(5, isRead: false);

        // Act
        var count = await _service.MarkAllAsReadAsync();

        // Assert
        Assert.Equal(5, count);
        var allNotifications = await _dbContext.Notifications.ToListAsync();
        Assert.All(allNotifications, n => Assert.True(n.IsRead));
    }

    [Fact]
    public async Task DeleteAsync_DeletesNotification()
    {
        // Arrange
        var notification = new Notification { Title = "Test", Message = "..." };
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(notification.Id);

        // Assert
        Assert.True(result);
        Assert.Null(await _dbContext.Notifications.FindAsync(notification.Id));
    }

    [Fact]
    public async Task DeleteReadAsync_DeletesOnlyReadNotifications()
    {
        // Arrange
        await CreateTestNotifications(3, isRead: true);
        await CreateTestNotifications(2, isRead: false);

        // Act
        var count = await _service.DeleteReadAsync();

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(2, await _dbContext.Notifications.CountAsync());
        Assert.All(await _dbContext.Notifications.ToListAsync(), n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_DeletesOldNotifications()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-1);
        
        _dbContext.Notifications.Add(new Notification { Title = "Old1", Message = "...", CreatedAt = oldDate });
        _dbContext.Notifications.Add(new Notification { Title = "Old2", Message = "...", CreatedAt = oldDate });
        _dbContext.Notifications.Add(new Notification { Title = "Recent", Message = "...", CreatedAt = recentDate });
        await _dbContext.SaveChangesAsync();

        // Act
        var count = await _service.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-5));

        // Assert
        Assert.Equal(2, count);
        Assert.Single(await _dbContext.Notifications.ToListAsync());
    }

    #endregion

    #region Notification Creation Tests

    [Fact]
    public async Task SendNewReleaseNotificationAsync_CreatesAggregatedNotification()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings 
            { 
                EnableInApp = true, 
                NewReleaseNotifications = true,
                AggregateReleaseNotifications = true
            });

        var request = new NewReleaseNotificationRequest
        {
            ReleaseDate = DateTime.Today.AddDays(3),
            IssueCount = 5,
            SeriesTitles = new List<string> { "Batman", "Spider-Man", "X-Men" }
        };

        // Act
        var result = await _service.SendNewReleaseNotificationAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(NotificationType.NewRelease, result.Type);
        Assert.Contains("5 Issues", result.Title);
        Assert.Contains("Batman", result.Message);
        Assert.Equal("/pulllist", result.Link);
    }

    [Fact]
    public async Task SendNewReleaseNotificationAsync_WhenDisabled_ReturnsNull()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true, NewReleaseNotifications = false });

        var request = new NewReleaseNotificationRequest { ReleaseDate = DateTime.Today, IssueCount = 5 };

        // Act
        var result = await _service.SendNewReleaseNotificationAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SendNewReleaseNotificationAsync_WithZeroIssues_ReturnsNull()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true, NewReleaseNotifications = true });

        var request = new NewReleaseNotificationRequest { ReleaseDate = DateTime.Today, IssueCount = 0 };

        // Act
        var result = await _service.SendNewReleaseNotificationAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SendGrabbedNotificationAsync_CreatesNotification()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true, GrabbedNotifications = true });

        var request = new GrabbedNotificationRequest
        {
            SeriesId = 1,
            IssueId = 10,
            SeriesTitle = "Batman",
            IssueNumber = 150,
            DownloadSource = "GetComics"
        };

        // Act
        var result = await _service.SendGrabbedNotificationAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(NotificationType.Grabbed, result.Type);
        Assert.Contains("Batman #150", result.Title);
        Assert.Contains("GetComics", result.Message);
        Assert.Equal(1, result.SeriesId);
        Assert.Equal(10, result.IssueId);
    }

    [Fact]
    public async Task SendWeeklySummaryAsync_CreatesNotification()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true, WeeklySummaryNotifications = true });

        var request = new WeeklySummaryRequest
        {
            WeekOf = DateTime.Today,
            TotalReleases = 10,
            WantedCount = 5,
            OwnedCount = 3,
            SkippedCount = 2
        };

        // Act
        var result = await _service.SendWeeklySummaryAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(NotificationType.WeeklySummary, result.Type);
        Assert.Contains("Weekly Pull List Summary", result.Title);
        Assert.Contains("10 releases", result.Message);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettings()
    {
        // Arrange
        var expectedSettings = new NotificationSettings
        {
            EnableInApp = true,
            NewReleaseNotifications = true,
            GrabbedNotifications = false
        };
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSettings);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        Assert.True(result.EnableInApp);
        Assert.True(result.NewReleaseNotifications);
        Assert.False(result.GrabbedNotifications);
    }

    [Fact]
    public async Task UpdateSettingsAsync_SavesSettings()
    {
        // Arrange
        var settings = new NotificationSettings { EnableInApp = false };

        // Act
        await _service.UpdateSettingsAsync(settings);

        // Assert
        _mockSettingsService.Verify(
            s => s.SetAsync("notifications", settings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Max Notifications Tests

    [Fact]
    public async Task CreateAsync_EnforcesMaxNotificationsLimit()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<NotificationSettings>("notifications", It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings { EnableInApp = true, MaxNotifications = 5 });

        // Create 5 notifications
        for (int i = 0; i < 5; i++)
        {
            _dbContext.Notifications.Add(new Notification 
            { 
                Title = $"Notification {i}", 
                Message = "...", 
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10) 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - add one more
        await _service.CreateAsync(new CreateNotificationRequest
        {
            Type = NotificationType.Info,
            Title = "New Notification",
            Message = "This should trigger cleanup"
        });

        // Assert - should still have 5 (oldest deleted)
        var count = await _dbContext.Notifications.CountAsync();
        Assert.Equal(5, count);
        Assert.Null(await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Title == "Notification 4")); // Oldest deleted
        Assert.NotNull(await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Title == "New Notification")); // Newest exists
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestNotifications(int count, bool isRead = false)
    {
        for (int i = 0; i < count; i++)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Title = $"Test {i}",
                Message = $"Message {i}",
                Type = NotificationType.Info,
                IsRead = isRead,
                ReadAt = isRead ? DateTime.UtcNow : null
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
