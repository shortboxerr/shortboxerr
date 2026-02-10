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
}
