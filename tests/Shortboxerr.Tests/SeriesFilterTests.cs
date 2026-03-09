using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for series list filtering and sorting functionality.
/// </summary>
public class SeriesFilterTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;

    public SeriesFilterTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ShortboxerrDbContext(options);
        SeedTestData();
    }

    private void SeedTestData()
    {
        var series = new List<Series>
        {
            new() { Id = 1, Title = "Batman", Publisher = "DC Comics", Status = SeriesStatus.Continuing, StartYear = 2016 },
            new() { Id = 2, Title = "Spider-Man", Publisher = "Marvel", Status = SeriesStatus.Ended, StartYear = 2018 },
            new() { Id = 3, Title = "Superman", Publisher = "DC Comics", Status = SeriesStatus.Continuing, StartYear = 2020 },
            new() { Id = 4, Title = "X-Men", Publisher = "Marvel", Status = SeriesStatus.Hiatus, StartYear = 2019 },
            new() { Id = 5, Title = "Saga", Publisher = "Image Comics", Status = SeriesStatus.Ended, StartYear = 2012 },
            new() { Id = 6, Title = "Wonder Woman", Publisher = "DC Comics", Status = SeriesStatus.Continuing, StartYear = 2016 },
            new() { Id = 7, Title = "Avengers", Publisher = "Marvel", Status = SeriesStatus.Continuing, StartYear = 2018 },
            new() { Id = 8, Title = "Walking Dead", Publisher = "Image Comics", Status = SeriesStatus.Ended, StartYear = 2003 },
        };

        _context.Series.AddRange(series);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Status Filter Tests

    [Fact]
    public async Task FilterByStatus_Continuing_ReturnsOnlyContinuing()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Continuing)
            .ToListAsync();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.All(result, s => Assert.Equal(SeriesStatus.Continuing, s.Status));
    }

    [Fact]
    public async Task FilterByStatus_Ended_ReturnsOnlyEnded()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Ended)
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal(SeriesStatus.Ended, s.Status));
    }

    [Fact]
    public async Task FilterByStatus_Hiatus_ReturnsOnlyHiatus()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Hiatus)
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("X-Men", result[0].Title);
    }

    #endregion

    #region Publisher Filter Tests

    [Fact]
    public async Task FilterByPublisher_DC_ReturnsOnlyDC()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains("dc"))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Contains("DC", s.Publisher!));
    }

    [Fact]
    public async Task FilterByPublisher_Marvel_ReturnsOnlyMarvel()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains("marvel"))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal("Marvel", s.Publisher));
    }

    [Fact]
    public async Task FilterByPublisher_Image_ReturnsOnlyImage()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains("image"))
            .ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Contains("Image", s.Publisher!));
    }

    #endregion

    #region Combined Filter Tests

    [Fact]
    public async Task FilterByStatusAndPublisher_DCContinuing_ReturnsCorrectSeries()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Continuing)
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains("dc"))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s.Title == "Batman");
        Assert.Contains(result, s => s.Title == "Superman");
        Assert.Contains(result, s => s.Title == "Wonder Woman");
    }

    [Fact]
    public async Task FilterByStatusAndPublisher_MarvelEnded_ReturnsCorrectSeries()
    {
        // Act
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Ended)
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains("marvel"))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Spider-Man", result[0].Title);
    }

    #endregion

    #region Sort Tests

    [Fact]
    public async Task SortByTitle_Ascending_ReturnsAlphabeticalOrder()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.Title)
            .ToListAsync();

        // Assert
        // Alphabetical order: Avengers, Batman, Saga, Spider-Man, Superman, Walking Dead, Wonder Woman, X-Men
        Assert.Equal("Avengers", result[0].Title);
        Assert.Equal("Batman", result[1].Title);
        Assert.Equal("Saga", result[2].Title);
        Assert.Equal("X-Men", result[7].Title);
    }

    [Fact]
    public async Task SortByTitle_Descending_ReturnsReverseAlphabeticalOrder()
    {
        // Act
        var result = await _context.Series
            .OrderByDescending(s => s.Title)
            .ToListAsync();

        // Assert
        Assert.Equal("X-Men", result[0].Title);
        Assert.Equal("Wonder Woman", result[1].Title);
    }

    [Fact]
    public async Task SortByStartYear_Ascending_ReturnsOldestFirst()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.StartYear)
            .ToListAsync();

        // Assert
        Assert.Equal("Walking Dead", result[0].Title);
        Assert.Equal(2003, result[0].StartYear);
    }

    [Fact]
    public async Task SortByStartYear_Descending_ReturnsNewestFirst()
    {
        // Act
        var result = await _context.Series
            .OrderByDescending(s => s.StartYear)
            .ToListAsync();

        // Assert
        Assert.Equal("Superman", result[0].Title);
        Assert.Equal(2020, result[0].StartYear);
    }

    [Fact]
    public async Task SortByStatus_Ascending_ReturnsCorrectOrder()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.Status)
            .ToListAsync();

        // Assert - Continuing = 0, Ended = 1, Hiatus = 2
        Assert.Equal(SeriesStatus.Continuing, result[0].Status);
        Assert.Equal(SeriesStatus.Hiatus, result[7].Status);
    }

    [Fact]
    public async Task SortByPublisher_Ascending_ReturnsCorrectOrder()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.Publisher)
            .ToListAsync();

        // Assert - Alphabetical: DC, Image, Marvel
        Assert.StartsWith("DC", result[0].Publisher);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Pagination_FirstPage_ReturnsCorrectCount()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.Title)
            .Skip(0)
            .Take(3)
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Avengers", result[0].Title);
    }

    [Fact]
    public async Task Pagination_SecondPage_ReturnsCorrectItems()
    {
        // Act
        var result = await _context.Series
            .OrderBy(s => s.Title)
            .Skip(3)
            .Take(3)
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, s => s.Title == "Avengers");
        Assert.DoesNotContain(result, s => s.Title == "Batman");
    }

    #endregion

    #region Filter Options Tests

    [Fact]
    public async Task GetDistinctPublishers_ReturnsAllPublishers()
    {
        // Act
        var publishers = await _context.Series
            .Where(s => s.Publisher != null)
            .Select(s => s.Publisher!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        // Assert
        Assert.Equal(3, publishers.Count);
        Assert.Contains("DC Comics", publishers);
        Assert.Contains("Image Comics", publishers);
        Assert.Contains("Marvel", publishers);
    }

    [Fact]
    public async Task GetStatusCounts_ReturnsCorrectCounts()
    {
        // Act
        var statusCounts = await _context.Series
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Assert
        Assert.Equal(3, statusCounts.Count);
        Assert.Equal(4, statusCounts.First(s => s.Status == SeriesStatus.Continuing).Count);
        Assert.Equal(3, statusCounts.First(s => s.Status == SeriesStatus.Ended).Count);
        Assert.Equal(1, statusCounts.First(s => s.Status == SeriesStatus.Hiatus).Count);
    }

    #endregion

    #region Text Search Tests

    [Fact]
    public async Task SearchByTitle_ExactMatch_ReturnsSeries()
    {
        // Act
        var searchTerm = "batman";
        var result = await _context.Series
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Batman", result[0].Title);
    }

    [Fact]
    public async Task SearchByTitle_PartialMatch_ReturnsSeries()
    {
        // Act - search for "man" should match Batman, Spider-Man, Superman, Wonder Woman
        // Note: X-Men has "Men" not "man" - case insensitive but "man" != "men"
        var searchTerm = "man";
        var result = await _context.Series
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains(result, s => s.Title == "Batman");
        Assert.Contains(result, s => s.Title == "Spider-Man");
        Assert.Contains(result, s => s.Title == "Superman");
        Assert.Contains(result, s => s.Title == "Wonder Woman");
    }

    [Fact]
    public async Task SearchByTitle_CaseInsensitive_ReturnsSeries()
    {
        // Act
        var searchTerm = "SPIDER";
        var result = await _context.Series
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Spider-Man", result[0].Title);
    }

    [Fact]
    public async Task SearchByTitle_NoMatch_ReturnsEmpty()
    {
        // Act
        var searchTerm = "nonexistent";
        var result = await _context.Series
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchByTitle_WithStatusFilter_CombinesFilters()
    {
        // Act - search for "man" in Continuing series
        var searchTerm = "man";
        var result = await _context.Series
            .Where(s => s.Status == SeriesStatus.Continuing)
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert - should match Batman, Superman, Wonder Woman (all Continuing)
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, s => s.Title == "Spider-Man"); // Ended
        Assert.DoesNotContain(result, s => s.Title == "X-Men"); // Hiatus
    }

    [Fact]
    public async Task SearchByTitle_WithPublisherFilter_CombinesFilters()
    {
        // Act - search for "man" in DC Comics
        var searchTerm = "man";
        var publisherSearch = "dc";
        var result = await _context.Series
            .Where(s => s.Publisher != null && s.Publisher.ToLower().Contains(publisherSearch))
            .Where(s => s.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert - should match Batman, Superman, Wonder Woman (all DC)
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, s => s.Title == "Spider-Man"); // Marvel
        Assert.DoesNotContain(result, s => s.Title == "X-Men"); // Marvel
    }

    #endregion

    #region Release Date Sorting Tests

    [Fact]
    public async Task SortByLatestRelease_Ascending_ReturnsCorrectOrder()
    {
        // Arrange - Add issues with different release dates
        var issues = new List<Issue>
        {
            new() { Id = 1, SeriesId = 1, IssueNumber = 1, StoreDate = new DateTime(2024, 1, 1), Status = IssueStatus.Owned },
            new() { Id = 2, SeriesId = 1, IssueNumber = 2, StoreDate = new DateTime(2024, 6, 1), Status = IssueStatus.Owned },
            new() { Id = 3, SeriesId = 2, IssueNumber = 1, StoreDate = new DateTime(2023, 3, 1), Status = IssueStatus.Owned },
            new() { Id = 4, SeriesId = 3, IssueNumber = 1, StoreDate = new DateTime(2024, 12, 1), Status = IssueStatus.Owned },
        };
        _context.Issues.AddRange(issues);
        await _context.SaveChangesAsync();

        // Act - Sort by latest release ascending (oldest latest first)
        var result = await _context.Series
            .Include(s => s.Issues)
            .Where(s => s.Issues.Any())
            .OrderBy(s => s.Issues.Max(i => i.StoreDate ?? i.ReleaseDate))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Spider-Man", result[0].Title); // 2023-03-01
        Assert.Equal("Batman", result[1].Title);     // 2024-06-01
        Assert.Equal("Superman", result[2].Title);   // 2024-12-01
    }

    [Fact]
    public async Task SortByLatestRelease_Descending_ReturnsCorrectOrder()
    {
        // Arrange - Add issues with different release dates
        var issues = new List<Issue>
        {
            new() { Id = 101, SeriesId = 1, IssueNumber = 1, StoreDate = new DateTime(2024, 1, 1), Status = IssueStatus.Owned },
            new() { Id = 102, SeriesId = 1, IssueNumber = 2, StoreDate = new DateTime(2024, 6, 1), Status = IssueStatus.Owned },
            new() { Id = 103, SeriesId = 2, IssueNumber = 1, StoreDate = new DateTime(2023, 3, 1), Status = IssueStatus.Owned },
            new() { Id = 104, SeriesId = 3, IssueNumber = 1, StoreDate = new DateTime(2024, 12, 1), Status = IssueStatus.Owned },
        };
        _context.Issues.AddRange(issues);
        await _context.SaveChangesAsync();

        // Act - Sort by latest release descending (most recent first)
        var result = await _context.Series
            .Include(s => s.Issues)
            .Where(s => s.Issues.Any())
            .OrderByDescending(s => s.Issues.Max(i => i.StoreDate ?? i.ReleaseDate))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Superman", result[0].Title);   // 2024-12-01
        Assert.Equal("Batman", result[1].Title);     // 2024-06-01
        Assert.Equal("Spider-Man", result[2].Title); // 2023-03-01
    }

    [Fact]
    public async Task SortByNextRelease_ReturnsUpcomingFirst()
    {
        // Arrange - Add issues with future dates
        var today = DateTime.Today;
        var issues = new List<Issue>
        {
            new() { Id = 201, SeriesId = 1, IssueNumber = 1, StoreDate = today.AddDays(-30), Status = IssueStatus.Owned },
            new() { Id = 202, SeriesId = 1, IssueNumber = 2, StoreDate = today.AddDays(7), Status = IssueStatus.Wanted },  // Next week
            new() { Id = 203, SeriesId = 2, IssueNumber = 1, StoreDate = today.AddDays(30), Status = IssueStatus.Wanted }, // Next month
            new() { Id = 204, SeriesId = 3, IssueNumber = 1, StoreDate = today.AddDays(1), Status = IssueStatus.Wanted },  // Tomorrow
        };
        _context.Issues.AddRange(issues);
        await _context.SaveChangesAsync();

        // Act - Sort by next release ascending (soonest first)
        var result = await _context.Series
            .Include(s => s.Issues)
            .Where(s => s.Issues.Any(i => (i.StoreDate ?? i.ReleaseDate) > today))
            .OrderBy(s => s.Issues
                .Where(i => (i.StoreDate ?? i.ReleaseDate) > today)
                .Min(i => i.StoreDate ?? i.ReleaseDate))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Superman", result[0].Title);   // Tomorrow
        Assert.Equal("Batman", result[1].Title);     // Next week
        Assert.Equal("Spider-Man", result[2].Title); // Next month
    }

    [Fact]
    public async Task SortByLatestRelease_WithReleaseDate_FallsBackCorrectly()
    {
        // Arrange - Mix of StoreDate and ReleaseDate
        var issues = new List<Issue>
        {
            new() { Id = 301, SeriesId = 1, IssueNumber = 1, ReleaseDate = new DateTime(2024, 5, 1), Status = IssueStatus.Owned },
            new() { Id = 302, SeriesId = 2, IssueNumber = 1, StoreDate = new DateTime(2024, 4, 1), Status = IssueStatus.Owned },
            new() { Id = 303, SeriesId = 3, IssueNumber = 1, StoreDate = new DateTime(2024, 6, 1), Status = IssueStatus.Owned },
        };
        _context.Issues.AddRange(issues);
        await _context.SaveChangesAsync();

        // Act - Sort by latest release (uses StoreDate ?? ReleaseDate)
        var result = await _context.Series
            .Include(s => s.Issues)
            .Where(s => s.Issues.Any())
            .OrderByDescending(s => s.Issues.Max(i => i.StoreDate ?? i.ReleaseDate))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Superman", result[0].Title);   // 2024-06-01 (StoreDate)
        Assert.Equal("Batman", result[1].Title);     // 2024-05-01 (ReleaseDate fallback)
        Assert.Equal("Spider-Man", result[2].Title); // 2024-04-01 (StoreDate)
    }

    #endregion
}
