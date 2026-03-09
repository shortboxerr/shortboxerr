using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for edition list filtering and search functionality.
/// </summary>
public class EditionFilterTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;

    public EditionFilterTests()
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
            new() { Id = 1, Title = "Batman", Publisher = "DC Comics", Status = SeriesStatus.Continuing },
            new() { Id = 2, Title = "Spider-Man", Publisher = "Marvel", Status = SeriesStatus.Continuing },
            new() { Id = 3, Title = "Saga", Publisher = "Image Comics", Status = SeriesStatus.Hiatus },
        };

        var editions = new List<EditionTitle>
        {
            new() { Id = 1, Title = "Batman: Year One", SeriesId = 1, EditionType = EditionType.TradesPaperback, VolumeNumber = 1 },
            new() { Id = 2, Title = "Batman: The Long Halloween", SeriesId = 1, EditionType = EditionType.TradesPaperback, VolumeNumber = 2 },
            new() { Id = 3, Title = "Batman: Dark Victory", SeriesId = 1, EditionType = EditionType.TradesPaperback, VolumeNumber = 3 },
            new() { Id = 4, Title = "Spider-Man: Blue", SeriesId = 2, EditionType = EditionType.TradesPaperback },
            new() { Id = 5, Title = "Spider-Man: Life Story", SeriesId = 2, EditionType = EditionType.Hardcover },
            new() { Id = 6, Title = "Saga Compendium One", SeriesId = 3, EditionType = EditionType.Compendium },
            new() { Id = 7, Title = "Saga Deluxe Edition", SortTitle = "Saga Deluxe", SeriesId = 3, EditionType = EditionType.Hardcover },
            new() { Id = 8, Title = "Watchmen", EditionType = EditionType.TradesPaperback }, // No series
        };

        _context.Series.AddRange(series);
        _context.EditionTitles.AddRange(editions);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Series Filter Tests

    [Fact]
    public async Task FilterBySeries_ReturnsOnlySeriesEditions()
    {
        // Act
        var result = await _context.EditionTitles
            .Where(e => e.SeriesId == 1)
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.Contains("Batman", e.Title));
    }

    [Fact]
    public async Task FilterBySeries_NoSeries_ReturnsEmpty()
    {
        // Act
        var result = await _context.EditionTitles
            .Where(e => e.SeriesId == 999)
            .ToListAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Text Search Tests

    [Fact]
    public async Task SearchByTitle_ExactMatch_ReturnsEdition()
    {
        // Act
        var searchTerm = "watchmen";
        var result = await _context.EditionTitles
            .Where(e => e.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Watchmen", result[0].Title);
    }

    [Fact]
    public async Task SearchByTitle_PartialMatch_ReturnsEditions()
    {
        // Act - search for "spider" should match Spider-Man editions
        var searchTerm = "spider";
        var result = await _context.EditionTitles
            .Where(e => e.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Contains("Spider-Man", e.Title));
    }

    [Fact]
    public async Task SearchByTitle_CaseInsensitive_ReturnsEdition()
    {
        // Act
        var searchTerm = "SAGA";
        var result = await _context.EditionTitles
            .Where(e => e.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchByTitle_NoMatch_ReturnsEmpty()
    {
        // Act
        var searchTerm = "nonexistent";
        var result = await _context.EditionTitles
            .Where(e => e.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchBySortTitle_ReturnsEdition()
    {
        // Act - search for "deluxe" should match via SortTitle
        var searchTerm = "deluxe";
        var result = await _context.EditionTitles
            .Where(e =>
                e.Title.ToLower().Contains(searchTerm.ToLower()) ||
                (e.SortTitle != null && e.SortTitle.ToLower().Contains(searchTerm.ToLower())))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Saga Deluxe Edition", result[0].Title);
    }

    [Fact]
    public async Task SearchBySeriesName_ReturnsEditions()
    {
        // Act - search for "batman" should match Batman editions via series relationship
        var searchTerm = "batman";
        var result = await _context.EditionTitles
            .Include(e => e.Series)
            .Where(e =>
                e.Title.ToLower().Contains(searchTerm.ToLower()) ||
                (e.Series != null && e.Series.Title.ToLower().Contains(searchTerm.ToLower())))
            .ToListAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.Equal(1, e.SeriesId));
    }

    [Fact]
    public async Task SearchByTitle_WithSeriesFilter_CombinesFilters()
    {
        // Act - search for "year" within Batman series (seriesId = 1)
        var searchTerm = "year";
        var seriesId = 1;
        var result = await _context.EditionTitles
            .Where(e => e.SeriesId == seriesId)
            .Where(e => e.Title.ToLower().Contains(searchTerm.ToLower()))
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Batman: Year One", result[0].Title);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task SortByTitle_Ascending_SortsCorrectly()
    {
        // Act
        var result = await _context.EditionTitles
            .OrderBy(e => e.SortTitle ?? e.Title)
            .ToListAsync();

        // Assert
        Assert.Equal("Batman: Dark Victory", result[0].Title);
        Assert.Equal("Watchmen", result[^1].Title);
    }

    [Fact]
    public async Task SortByVolumeNumber_Ascending_SortsCorrectly()
    {
        // Act - within Batman series
        var result = await _context.EditionTitles
            .Where(e => e.SeriesId == 1)
            .OrderBy(e => e.VolumeNumber)
            .ToListAsync();

        // Assert
        Assert.Equal("Batman: Year One", result[0].Title);
        Assert.Equal("Batman: The Long Halloween", result[1].Title);
        Assert.Equal("Batman: Dark Victory", result[2].Title);
    }

    #endregion
}
