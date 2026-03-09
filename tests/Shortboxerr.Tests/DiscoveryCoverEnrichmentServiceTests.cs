using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

public class DiscoveryCoverEnrichmentServiceTests
{
    private readonly Mock<IComicVineClient> _comicVineClientMock;
    private readonly Mock<ICoverFallbackService> _coverFallbackServiceMock;
    private readonly ILogger<DiscoveryCoverEnrichmentService> _logger;

    public DiscoveryCoverEnrichmentServiceTests()
    {
        _comicVineClientMock = new Mock<IComicVineClient>();
        _coverFallbackServiceMock = new Mock<ICoverFallbackService>();
        _logger = NullLogger<DiscoveryCoverEnrichmentService>.Instance;
    }

    private (ShortboxerrDbContext, IServiceProvider) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ShortboxerrDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton(_comicVineClientMock.Object);
        services.AddSingleton(_coverFallbackServiceMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        return (dbContext, serviceProvider);
    }

    [Fact]
    public async Task RefreshFallbackCovers_UpdatesIssue_WhenComicVineHasCover()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);
        var issueId = 12345;

        // Create cached week with issue using fallback cover
        var issues = new List<ComicVineIssue>
        {
            new() { Id = issueId, IssueNumber = "1", Volume = new ComicVineVolumeRef { Id = 100, Name = "Test Series" } }
        };
        dbContext.CachedDiscoveryWeeks.Add(new CachedDiscoveryWeek
        {
            WeekStart = weekStart,
            IssuesJson = JsonSerializer.Serialize(issues),
            LastRefreshed = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IssueCount = 1
        });

        // Create fallback entry tracking the Metron cover
        dbContext.FallbackCoverEntries.Add(new FallbackCoverEntry
        {
            ComicVineIssueId = issueId,
            ComicVineVolumeId = 100,
            SeriesName = "Test Series",
            IssueNumber = "1",
            FallbackCoverUrl = "https://metron.cloud/media/issue/cover.jpg",
            FallbackSource = "Metron",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            LastChecked = null,
            WeekStart = weekStart
        });

        await dbContext.SaveChangesAsync();

        // Setup ComicVine to return a cover now
        _comicVineClientMock.Setup(c => c.GetIssuesByIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>
                {
                    new()
                    {
                        Id = issueId,
                        IssueNumber = "1",
                        Image = new ComicVineImage { MediumUrl = "https://comicvine.example.com/cover.jpg" }
                    }
                }
            });

        _coverFallbackServiceMock.Setup(c => c.ClearCacheAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act
        await service.TriggerCoverRefreshAsync();

        // Assert
        var updatedWeek = await dbContext.CachedDiscoveryWeeks.FirstAsync();
        var updatedIssues = JsonSerializer.Deserialize<List<ComicVineIssue>>(updatedWeek.IssuesJson);
        Assert.NotNull(updatedIssues);
        Assert.Equal("https://comicvine.example.com/cover.jpg", updatedIssues[0].Image?.MediumUrl);

        // Fallback entry should be removed
        Assert.Empty(await dbContext.FallbackCoverEntries.ToListAsync());

        // Cache should have been cleared
        _coverFallbackServiceMock.Verify(c => c.ClearCacheAsync(
            "Test Series", "1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshFallbackCovers_SkipsRecentlyChecked()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);
        var issueId = 12345;

        // Create fallback entry that was checked recently (within 7 days)
        dbContext.FallbackCoverEntries.Add(new FallbackCoverEntry
        {
            ComicVineIssueId = issueId,
            ComicVineVolumeId = 100,
            SeriesName = "Test Series",
            IssueNumber = "1",
            FallbackCoverUrl = "https://metron.cloud/media/issue/cover.jpg",
            FallbackSource = "Metron",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            LastChecked = DateTime.UtcNow.AddDays(-1), // Checked yesterday
            WeekStart = weekStart
        });

        await dbContext.SaveChangesAsync();

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act
        await service.TriggerCoverRefreshAsync();

        // Assert - ComicVine should NOT have been called
        _comicVineClientMock.Verify(c => c.GetIssuesByIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshFallbackCovers_UpdatesLastChecked_WhenNoNewCover()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);
        var issueId = 12345;

        // Create cached week
        var issues = new List<ComicVineIssue>
        {
            new() { Id = issueId, IssueNumber = "1", Volume = new ComicVineVolumeRef { Id = 100, Name = "Test Series" } }
        };
        dbContext.CachedDiscoveryWeeks.Add(new CachedDiscoveryWeek
        {
            WeekStart = weekStart,
            IssuesJson = JsonSerializer.Serialize(issues),
            LastRefreshed = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IssueCount = 1
        });

        // Create fallback entry
        dbContext.FallbackCoverEntries.Add(new FallbackCoverEntry
        {
            ComicVineIssueId = issueId,
            ComicVineVolumeId = 100,
            SeriesName = "Test Series",
            IssueNumber = "1",
            FallbackCoverUrl = "https://metron.cloud/media/issue/cover.jpg",
            FallbackSource = "Metron",
            CreatedAt = DateTime.UtcNow.AddDays(-14),
            LastChecked = null,
            WeekStart = weekStart
        });

        await dbContext.SaveChangesAsync();

        // ComicVine still doesn't have a cover
        _comicVineClientMock.Setup(c => c.GetIssuesByIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = true,
                Results = new List<ComicVineIssue>
                {
                    new()
                    {
                        Id = issueId,
                        IssueNumber = "1",
                        Image = null // Still no cover
                    }
                }
            });

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act
        await service.TriggerCoverRefreshAsync();

        // Assert - Entry should still exist but with updated LastChecked
        var entry = await dbContext.FallbackCoverEntries.FirstAsync();
        Assert.NotNull(entry.LastChecked);
        Assert.True(DateTime.UtcNow - entry.LastChecked.Value < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TrackFallbackCover_CreatesEntry_ForMetronCover()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act - directly test the internal tracking method via reflection (it's internal)
        var trackMethod = typeof(DiscoveryCoverEnrichmentService)
            .GetMethod("TrackFallbackCoverAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(trackMethod);

        await (Task)trackMethod.Invoke(service, new object[]
        {
            dbContext,
            12345,
            100,
            "Test Series",
            "1",
            "https://metron.cloud/media/issue/cover.jpg",
            CoverSource.Metron,
            weekStart,
            CancellationToken.None
        })!;

        await dbContext.SaveChangesAsync();

        // Assert
        var entry = await dbContext.FallbackCoverEntries.FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(12345, entry.ComicVineIssueId);
        Assert.Equal("Test Series", entry.SeriesName);
        Assert.Equal("1", entry.IssueNumber);
        Assert.Equal("Metron", entry.FallbackSource);
    }

    [Fact]
    public async Task TrackFallbackCover_DoesNotTrack_VolumeCover()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act - track a volume cover (should be ignored)
        var trackMethod = typeof(DiscoveryCoverEnrichmentService)
            .GetMethod("TrackFallbackCoverAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(trackMethod);

        await (Task)trackMethod.Invoke(service, new object[]
        {
            dbContext,
            12345,
            100,
            "Test Series",
            "1",
            "https://comicvine.example.com/volume-cover.jpg",
            CoverSource.ComicVineVolume, // Volume cover, not Metron
            weekStart,
            CancellationToken.None
        })!;

        await dbContext.SaveChangesAsync();

        // Assert - No entry should be created
        Assert.Empty(await dbContext.FallbackCoverEntries.ToListAsync());
    }

    [Fact]
    public async Task RefreshFallbackCovers_HandlesApiError_Gracefully()
    {
        // Arrange
        var (dbContext, serviceProvider) = CreateTestContext();
        var weekStart = new DateTime(2026, 2, 15);

        dbContext.FallbackCoverEntries.Add(new FallbackCoverEntry
        {
            ComicVineIssueId = 12345,
            ComicVineVolumeId = 100,
            SeriesName = "Test Series",
            IssueNumber = "1",
            FallbackCoverUrl = "https://metron.cloud/media/issue/cover.jpg",
            FallbackSource = "Metron",
            CreatedAt = DateTime.UtcNow.AddDays(-14),
            LastChecked = null,
            WeekStart = weekStart
        });

        await dbContext.SaveChangesAsync();

        // ComicVine returns error
        _comicVineClientMock.Setup(c => c.GetIssuesByIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSearchResult<ComicVineIssue>
            {
                Success = false,
                Error = "API rate limit exceeded"
            });

        var service = new DiscoveryCoverEnrichmentService(serviceProvider, _logger);

        // Act - should not throw
        await service.TriggerCoverRefreshAsync();

        // Assert - Entry should still exist unchanged
        var entry = await dbContext.FallbackCoverEntries.FirstAsync();
        Assert.Null(entry.LastChecked); // Not updated due to error
    }

    [Theory]
    [InlineData("2026-03-09", "2026-03-09")] // Monday returns same day
    [InlineData("2026-03-10", "2026-03-09")] // Tuesday returns Monday
    [InlineData("2026-03-11", "2026-03-09")] // Wednesday returns Monday  
    [InlineData("2026-03-14", "2026-03-09")] // Saturday returns Monday
    [InlineData("2026-03-15", "2026-03-09")] // Sunday returns Monday
    [InlineData("2026-03-16", "2026-03-16")] // Next Monday returns same day
    public void GetWeekStart_ReturnsMonday(string inputDateStr, string expectedMondayStr)
    {
        // This tests the static GetWeekStart method via reflection since it's private
        var inputDate = DateTime.Parse(inputDateStr);
        var expectedMonday = DateTime.Parse(expectedMondayStr);

        var getWeekStartMethod = typeof(DiscoveryCoverEnrichmentService)
            .GetMethod("GetWeekStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getWeekStartMethod);

        var result = (DateTime)getWeekStartMethod.Invoke(null, new object[] { inputDate })!;
        Assert.Equal(expectedMonday.Date, result.Date);
    }

    [Fact]
    public void GetWeekStart_WeekTransitionDetection_WorksCorrectly()
    {
        // This tests the week transition detection concept
        // When we cross from Sunday to Monday, we're in a new week
        var lastSunday = new DateTime(2026, 3, 8); // Sunday
        var monday = new DateTime(2026, 3, 9);     // Monday (new week)

        var getWeekStartMethod = typeof(DiscoveryCoverEnrichmentService)
            .GetMethod("GetWeekStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(getWeekStartMethod);

        var sundayWeek = (DateTime)getWeekStartMethod.Invoke(null, new object[] { lastSunday })!;
        var mondayWeek = (DateTime)getWeekStartMethod.Invoke(null, new object[] { monday })!;

        // Sunday Mar 8 belongs to week starting Mar 2
        Assert.Equal(new DateTime(2026, 3, 2).Date, sundayWeek.Date);
        // Monday Mar 9 starts a new week
        Assert.Equal(new DateTime(2026, 3, 9).Date, mondayWeek.Date);
        // These are different weeks
        Assert.True(mondayWeek > sundayWeek);
    }
}
