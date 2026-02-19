using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

public class HostBlacklistServiceTests
{
    private readonly Mock<IDownloadHostResolverFactory> _mockResolverFactory;
    private readonly HostBlacklistService _service;

    public HostBlacklistServiceTests()
    {
        _mockResolverFactory = new Mock<IDownloadHostResolverFactory>();
        _mockResolverFactory.Setup(f => f.GetAllResolvers()).Returns(new List<IDownloadHostResolver>());
        _service = new HostBlacklistService(_mockResolverFactory.Object, null);
    }

    #region IsBlacklisted Tests

    [Fact]
    public void IsBlacklisted_NewHost_ReturnsFalse()
    {
        // Act
        var result = _service.IsBlacklisted("mediafire");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsBlacklisted_BlacklistedHost_ReturnsTrue()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test reason", TimeSpan.FromHours(1));

        // Act
        var result = _service.IsBlacklisted("mediafire");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsBlacklisted_ExpiredEntry_ReturnsFalse()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test reason", TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10); // Wait for expiry

        // Act
        var result = _service.IsBlacklisted("mediafire");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsBlacklisted_NullOrEmpty_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(_service.IsBlacklisted(null!));
        Assert.False(_service.IsBlacklisted(""));
    }

    [Fact]
    public void IsBlacklisted_CaseInsensitive()
    {
        // Arrange
        _service.Blacklist("MediaFire", "Test reason", TimeSpan.FromHours(1));

        // Act & Assert
        Assert.True(_service.IsBlacklisted("mediafire"));
        Assert.True(_service.IsBlacklisted("MEDIAFIRE"));
        Assert.True(_service.IsBlacklisted("MediaFire"));
    }

    #endregion

    #region IsUrlBlacklisted Tests

    [Fact]
    public void IsUrlBlacklisted_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(_service.IsUrlBlacklisted(null!));
        Assert.False(_service.IsUrlBlacklisted(""));
    }

    [Fact]
    public void IsUrlBlacklisted_InvalidUrl_ReturnsFalse()
    {
        Assert.False(_service.IsUrlBlacklisted("not-a-url"));
    }

    #endregion

    #region Blacklist Tests

    [Fact]
    public void Blacklist_AddsEntry()
    {
        // Act
        _service.Blacklist("mega", "Rate limited", TimeSpan.FromHours(1));

        // Assert
        var entry = _service.GetBlacklistEntry("mega");
        Assert.NotNull(entry);
        Assert.Equal("mega", entry.HostId);
        Assert.Equal("Rate limited", entry.Reason);
        Assert.False(entry.IsAutomatic);
    }

    [Fact]
    public void Blacklist_WithNullDuration_UsesDefault()
    {
        // Arrange
        var settings = _service.GetSettings();
        var expectedDuration = settings.DefaultBlacklistDuration;

        // Act
        _service.Blacklist("mega", "Test reason");

        // Assert
        var entry = _service.GetBlacklistEntry("mega");
        Assert.NotNull(entry);
        Assert.NotNull(entry.ExpiresAt);
        var actualDuration = entry.ExpiresAt.Value - entry.BlacklistedAt;
        Assert.True(Math.Abs((actualDuration - expectedDuration).TotalSeconds) < 5);
    }

    [Fact]
    public void Blacklist_EscalatesDuration_ForRepeatOffenders()
    {
        // Arrange
        var settings = new HostBlacklistSettings
        {
            EscalateDuration = true,
            EscalationMultiplier = 2.0,
            DefaultBlacklistDuration = TimeSpan.FromMinutes(10),
            MaxBlacklistDuration = TimeSpan.FromHours(24)
        };
        _service.UpdateSettings(settings);

        // First blacklist
        _service.Blacklist("mega", "First offense", TimeSpan.FromMinutes(10));
        var firstEntry = _service.GetBlacklistEntry("mega");
        _service.RemoveFromBlacklist("mega");

        // Second blacklist (should be escalated)
        _service.Blacklist("mega", "Second offense", TimeSpan.FromMinutes(10));
        var secondEntry = _service.GetBlacklistEntry("mega");

        // Assert - second duration should be approximately double
        Assert.NotNull(firstEntry?.ExpiresAt);
        Assert.NotNull(secondEntry?.ExpiresAt);
        var firstDuration = firstEntry.ExpiresAt.Value - firstEntry.BlacklistedAt;
        var secondDuration = secondEntry.ExpiresAt.Value - secondEntry.BlacklistedAt;
        Assert.True(secondDuration > firstDuration);
    }

    #endregion

    #region RemoveFromBlacklist Tests

    [Fact]
    public void RemoveFromBlacklist_ExistingEntry_ReturnsTrue()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test reason");

        // Act
        var result = _service.RemoveFromBlacklist("mediafire");

        // Assert
        Assert.True(result);
        Assert.False(_service.IsBlacklisted("mediafire"));
    }

    [Fact]
    public void RemoveFromBlacklist_NonExistentEntry_ReturnsFalse()
    {
        // Act
        var result = _service.RemoveFromBlacklist("nonexistent");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RecordFailure Tests

    [Fact]
    public void RecordFailure_TracksFailureCount()
    {
        // Act
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound, "File deleted");
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound, "File deleted");

        // Assert
        var stats = _service.GetHostFailureStats("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(2, stats.FailureCount);
        Assert.Equal(2, stats.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_TracksLastError()
    {
        // Act
        _service.RecordFailure("mediafire", HostResolverFailureReason.RateLimited, "Too many requests");

        // Assert
        var stats = _service.GetHostFailureStats("mediafire");
        Assert.NotNull(stats);
        Assert.Equal("Too many requests", stats.LastErrorMessage);
        Assert.Equal(HostResolverFailureReason.RateLimited, stats.LastFailureReason);
    }

    [Fact]
    public void RecordFailure_AutoBlacklists_AfterThreshold()
    {
        // Arrange
        var settings = new HostBlacklistSettings
        {
            AutoBlacklistEnabled = true,
            ConsecutiveFailureThreshold = 3,
            DefaultBlacklistDuration = TimeSpan.FromHours(1)
        };
        _service.UpdateSettings(settings);

        // Act - trigger threshold
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);

        // Assert
        Assert.True(_service.IsBlacklisted("mediafire"));
        var entry = _service.GetBlacklistEntry("mediafire");
        Assert.NotNull(entry);
        Assert.True(entry.IsAutomatic);
    }

    [Fact]
    public void RecordFailure_ImmediateBlacklist_ForCriticalReasons()
    {
        // Arrange
        var settings = new HostBlacklistSettings
        {
            AutoBlacklistEnabled = true,
            ImmediateBlacklistReasons = new HashSet<HostResolverFailureReason>
            {
                HostResolverFailureReason.HostUnavailable
            }
        };
        _service.UpdateSettings(settings);

        // Act - single failure with immediate blacklist reason
        _service.RecordFailure("mediafire", HostResolverFailureReason.HostUnavailable, "Service down");

        // Assert
        Assert.True(_service.IsBlacklisted("mediafire"));
    }

    [Fact]
    public void RecordFailure_SkipsNonBlacklistableReasons()
    {
        // Arrange
        var settings = new HostBlacklistSettings
        {
            AutoBlacklistEnabled = true,
            ConsecutiveFailureThreshold = 2,
            NonBlacklistableReasons = new HashSet<HostResolverFailureReason>
            {
                HostResolverFailureReason.Timeout
            }
        };
        _service.UpdateSettings(settings);

        // Act - trigger threshold with non-blacklistable reason
        _service.RecordFailure("mediafire", HostResolverFailureReason.Timeout);
        _service.RecordFailure("mediafire", HostResolverFailureReason.Timeout);
        _service.RecordFailure("mediafire", HostResolverFailureReason.Timeout);

        // Assert - should NOT be blacklisted
        Assert.False(_service.IsBlacklisted("mediafire"));
    }

    [Fact]
    public void RecordFailure_TracksFailuresByReason()
    {
        // Act
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.RateLimited);

        // Assert
        var stats = _service.GetHostFailureStats("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(2, stats.FailuresByReason[HostResolverFailureReason.FileNotFound]);
        Assert.Equal(1, stats.FailuresByReason[HostResolverFailureReason.RateLimited]);
    }

    #endregion

    #region RecordSuccess Tests

    [Fact]
    public void RecordSuccess_TracksSuccessCount()
    {
        // Act
        _service.RecordSuccess("mediafire");
        _service.RecordSuccess("mediafire");

        // Assert
        var stats = _service.GetHostFailureStats("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(2, stats.SuccessCount);
    }

    [Fact]
    public void RecordSuccess_ResetsConsecutiveFailures()
    {
        // Arrange
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);

        // Act
        _service.RecordSuccess("mediafire");

        // Assert
        var stats = _service.GetHostFailureStats("mediafire");
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ConsecutiveFailures);
        Assert.Equal(2, stats.FailureCount); // Total failures still tracked
    }

    #endregion

    #region GetBlacklist Tests

    [Fact]
    public void GetBlacklist_ReturnsAllEntries()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test 1", TimeSpan.FromHours(1));
        _service.Blacklist("mega", "Test 2", TimeSpan.FromHours(1));

        // Act
        var entries = _service.GetBlacklist();

        // Assert
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void GetBlacklist_ExcludesExpiredEntries()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test 1", TimeSpan.FromMilliseconds(1));
        _service.Blacklist("mega", "Test 2", TimeSpan.FromHours(1));
        Thread.Sleep(10); // Wait for expiry

        // Act
        var entries = _service.GetBlacklist();

        // Assert
        Assert.Single(entries);
        Assert.Equal("mega", entries[0].HostId);
    }

    #endregion

    #region GetFailureStatistics Tests

    [Fact]
    public void GetFailureStatistics_ReturnsAllTrackedHosts()
    {
        // Arrange
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordSuccess("mega");

        // Act
        var stats = _service.GetFailureStatistics();

        // Assert
        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public void GetFailureStatistics_CalculatesSuccessRate()
    {
        // Arrange
        _service.RecordSuccess("mediafire");
        _service.RecordSuccess("mediafire");
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);

        // Act
        var stats = _service.GetHostFailureStats("mediafire");

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(50, stats.SuccessRate); // 2 success, 2 failure = 50%
    }

    #endregion

    #region ClearAll Tests

    [Fact]
    public void ClearAll_RemovesAllEntriesAndStats()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test");
        _service.RecordFailure("mega", HostResolverFailureReason.FileNotFound);

        // Act
        _service.ClearAll();

        // Assert
        Assert.Empty(_service.GetBlacklist());
        Assert.Empty(_service.GetFailureStatistics());
    }

    #endregion

    #region ClearHostStats Tests

    [Fact]
    public void ClearHostStats_RemovesSpecificHost()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test");
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.RecordFailure("mega", HostResolverFailureReason.FileNotFound);

        // Act
        _service.ClearHostStats("mediafire");

        // Assert
        Assert.False(_service.IsBlacklisted("mediafire"));
        Assert.Null(_service.GetHostFailureStats("mediafire"));
        Assert.NotNull(_service.GetHostFailureStats("mega"));
    }

    #endregion

    #region Settings Tests

    [Fact]
    public void GetSettings_ReturnsCurrentSettings()
    {
        // Act
        var settings = _service.GetSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.AutoBlacklistEnabled);
        Assert.Equal(3, settings.ConsecutiveFailureThreshold);
    }

    [Fact]
    public void UpdateSettings_AppliesNewSettings()
    {
        // Arrange
        var newSettings = new HostBlacklistSettings
        {
            AutoBlacklistEnabled = false,
            ConsecutiveFailureThreshold = 5,
            DefaultBlacklistDuration = TimeSpan.FromMinutes(30)
        };

        // Act
        _service.UpdateSettings(newSettings);

        // Assert
        var settings = _service.GetSettings();
        Assert.False(settings.AutoBlacklistEnabled);
        Assert.Equal(5, settings.ConsecutiveFailureThreshold);
        Assert.Equal(TimeSpan.FromMinutes(30), settings.DefaultBlacklistDuration);
    }

    #endregion

    #region PurgeExpiredEntries Tests

    [Fact]
    public void PurgeExpiredEntries_RemovesExpired()
    {
        // Arrange
        _service.Blacklist("mediafire", "Expired", TimeSpan.FromMilliseconds(1));
        _service.Blacklist("mega", "Not expired", TimeSpan.FromHours(1));
        Thread.Sleep(10); // Wait for expiry

        // Act
        var purged = _service.PurgeExpiredEntries();

        // Assert
        Assert.Equal(1, purged);
        Assert.Single(_service.GetBlacklist());
    }

    [Fact]
    public void PurgeExpiredEntries_ReturnsZero_WhenNoneExpired()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test", TimeSpan.FromHours(1));

        // Act
        var purged = _service.PurgeExpiredEntries();

        // Assert
        Assert.Equal(0, purged);
    }

    #endregion

    #region Blacklist Entry Properties Tests

    [Fact]
    public void BlacklistEntry_TimeRemaining_CalculatedCorrectly()
    {
        // Arrange
        _service.Blacklist("mediafire", "Test", TimeSpan.FromHours(1));

        // Act
        var entry = _service.GetBlacklistEntry("mediafire");

        // Assert
        Assert.NotNull(entry);
        Assert.NotNull(entry.TimeRemaining);
        Assert.True(entry.TimeRemaining.Value.TotalMinutes > 50); // Should be close to 60 minutes
        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void HostFailureStats_IncludesBlacklistStatus()
    {
        // Arrange
        _service.RecordFailure("mediafire", HostResolverFailureReason.FileNotFound);
        _service.Blacklist("mediafire", "Test");

        // Act
        var stats = _service.GetHostFailureStats("mediafire");

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.IsBlacklisted);
    }

    #endregion
}
