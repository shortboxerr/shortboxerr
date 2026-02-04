using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Mylar3Migration;
using Shortboxerr.Infrastructure.Mylar3Migration;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

public class Mylar3MigrationServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly Mock<ILogger<Mylar3MigrationService>> _mockLogger;
    private readonly Mylar3MigrationService _service;
    private readonly string _testDbPath;

    public Mylar3MigrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _mockLogger = new Mock<ILogger<Mylar3MigrationService>>();
        
        _service = new Mylar3MigrationService(
            _dbContext,
            _mockSeriesMetadataService.Object,
            _mockLogger.Object);

        // Create a test SQLite database for Mylar3
        _testDbPath = Path.Combine(Path.GetTempPath(), $"mylar3_test_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    #region AnalyzeDatabaseAsync Tests

    [Fact]
    public async Task AnalyzeDatabaseAsync_ReturnsError_WhenFileNotFound()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/database.db";

        // Act
        var result = await _service.AnalyzeDatabaseAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error ?? "");
    }

    [Fact]
    public async Task AnalyzeDatabaseAsync_ReadsComicsTable()
    {
        // Arrange
        CreateTestDatabase(comics: new (string?, string, int?, string?)[]
        {
            ("12345", "Spider-Man", 2022, "Marvel"),
            ("67890", "Batman", 2021, "DC Comics")
        });

        // Act
        var result = await _service.AnalyzeDatabaseAsync(_testDbPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Stats.TotalSeries);
        Assert.Contains("comics", result.TablesFound);
        
        var spiderman = result.Series.FirstOrDefault(s => s.ComicName == "Spider-Man");
        Assert.NotNull(spiderman);
        Assert.Equal(12345, spiderman.ComicVineId);
        Assert.Equal(2022, spiderman.ComicYear);
        Assert.Equal("Marvel", spiderman.ComicPublisher);
    }

    [Fact]
    public async Task AnalyzeDatabaseAsync_ReadsIssuesTable()
    {
        // Arrange
        CreateTestDatabase(
            comics: new (string?, string, int?, string?)[] { ("12345", "Spider-Man", 2022, "Marvel") },
            issues: new[]
            {
                ("100001", "12345", "1", "First Issue", "Wanted"),
                ("100002", "12345", "2", "Second Issue", "Downloaded")
            });

        // Act
        var result = await _service.AnalyzeDatabaseAsync(_testDbPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Stats.TotalIssues);
        Assert.Equal(1, result.Stats.WantedIssues);
        Assert.Equal(1, result.Stats.DownloadedIssues);
    }

    [Fact]
    public async Task AnalyzeDatabaseAsync_CountsComicVineIds()
    {
        // Arrange
        CreateTestDatabase(comics: new (string?, string, int?, string?)[]
        {
            ("12345", "Spider-Man", 2022, "Marvel"), // Has ComicVine ID
            (null, "Unknown Series", null, null)     // No ComicVine ID
        });

        // Act
        var result = await _service.AnalyzeDatabaseAsync(_testDbPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Stats.TotalSeries);
        Assert.Equal(1, result.Stats.SeriesWithComicVineId);
    }

    #endregion

    #region ImportAsync Tests

    [Fact]
    public async Task ImportAsync_ImportsNewSeries()
    {
        // Arrange
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            Series = new List<Mylar3Series>
            {
                new() { ComicId = "12345", ComicName = "Spider-Man", ComicYear = 2022, ComicVineId = 12345 }
            }
        };
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            SyncMetadataAfterImport = false
        };

        // Act
        var result = await _service.ImportAsync(snapshot, options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.SeriesImported);
        Assert.Equal(0, result.SeriesSkipped);
        
        var series = await _dbContext.Series.FirstOrDefaultAsync(s => s.Title == "Spider-Man");
        Assert.NotNull(series);
        Assert.Equal(12345, series.ComicVineId);
    }

    [Fact]
    public async Task ImportAsync_SkipsExistingSeries_WhenConfigured()
    {
        // Arrange
        _dbContext.Series.Add(new Core.Entities.Series { Title = "Spider-Man" });
        await _dbContext.SaveChangesAsync();
        
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            Series = new List<Mylar3Series>
            {
                new() { ComicId = "12345", ComicName = "Spider-Man", ComicYear = 2022 }
            }
        };
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            SkipExistingSeries = true,
            UpdateExistingSeries = false
        };

        // Act
        var result = await _service.ImportAsync(snapshot, options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.SeriesImported);
        Assert.Equal(1, result.SeriesSkipped);
    }

    [Fact]
    public async Task ImportAsync_UpdatesExistingSeries_WhenConfigured()
    {
        // Arrange
        _dbContext.Series.Add(new Core.Entities.Series { Title = "Spider-Man" });
        await _dbContext.SaveChangesAsync();
        
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            Series = new List<Mylar3Series>
            {
                new() { ComicId = "12345", ComicName = "Spider-Man", ComicYear = 2022, ComicVineId = 12345 }
            }
        };
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            SkipExistingSeries = false,
            UpdateExistingSeries = true
        };

        // Act
        var result = await _service.ImportAsync(snapshot, options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.SeriesUpdated);
        
        var series = await _dbContext.Series.FirstOrDefaultAsync(s => s.Title == "Spider-Man");
        Assert.NotNull(series);
        Assert.Equal(12345, series.ComicVineId);
    }

    [Fact]
    public async Task ImportAsync_DryRun_DoesNotModifyDatabase()
    {
        // Arrange
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            Series = new List<Mylar3Series>
            {
                new() { ComicId = "12345", ComicName = "Spider-Man" }
            }
        };
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            DryRun = true
        };

        // Act
        var result = await _service.ImportAsync(snapshot, options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.WasDryRun);
        Assert.Equal(1, result.SeriesImported);
        
        // Verify no series was actually created
        var series = await _dbContext.Series.FirstOrDefaultAsync(s => s.Title == "Spider-Man");
        Assert.Null(series);
    }

    [Fact]
    public async Task ImportAsync_ImportsIssues()
    {
        // Arrange
        _dbContext.Series.Add(new Core.Entities.Series { Id = 1, Title = "Spider-Man" });
        await _dbContext.SaveChangesAsync();
        
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            Series = new List<Mylar3Series>
            {
                new() { ComicId = "12345", ComicName = "Spider-Man" }
            },
            Issues = new List<Mylar3Issue>
            {
                new() { IssueId = "100001", ComicId = "12345", IssueNumber = "1", IssueName = "First Issue" },
                new() { IssueId = "100002", ComicId = "12345", IssueNumber = "2", IssueName = "Second Issue" }
            }
        };
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            ImportIssues = true,
            SkipExistingSeries = false,
            UpdateExistingSeries = true,
            SyncMetadataAfterImport = false
        };

        // Act
        var result = await _service.ImportAsync(snapshot, options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.IssuesImported);
        
        var issues = await _dbContext.Issues.Where(i => i.SeriesId == 1).ToListAsync();
        Assert.Equal(2, issues.Count);
    }

    #endregion

    #region MigrateAsync Tests

    [Fact]
    public async Task MigrateAsync_PerformsFullMigration()
    {
        // Arrange
        CreateTestDatabase(
            comics: new (string?, string, int?, string?)[] { ("12345", "Spider-Man", 2022, "Marvel") },
            issues: new[] { ("100001", "12345", "1", "First Issue", "Wanted") });
        
        var options = new Mylar3MigrationOptions
        {
            ImportSeries = true,
            ImportIssues = true,
            SyncMetadataAfterImport = false
        };

        // Act
        var result = await _service.MigrateAsync(_testDbPath, options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.SeriesImported);
        Assert.Equal(1, result.IssuesImported);
    }

    #endregion

    #region Helper Methods

    private void CreateTestDatabase(
        (string? ComicId, string ComicName, int? ComicYear, string? ComicPublisher)[]? comics = null,
        (string IssueId, string ComicId, string IssueNumber, string IssueName, string Status)[]? issues = null)
    {
        var connectionString = $"Data Source={_testDbPath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create comics table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE comics (
                    ComicID TEXT,
                    ComicName TEXT,
                    ComicYear INTEGER,
                    ComicPublisher TEXT,
                    ComicImage TEXT,
                    Status TEXT,
                    Total INTEGER,
                    Have INTEGER,
                    ComicLocation TEXT,
                    Ignored INTEGER DEFAULT 0,
                    DateAdded TEXT,
                    LastUpdated TEXT
                )";
            cmd.ExecuteNonQuery();
        }

        // Create issues table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE issues (
                    IssueID TEXT,
                    ComicID TEXT,
                    Issue_Number TEXT,
                    IssueName TEXT,
                    ReleaseDate TEXT,
                    DigitalDate TEXT,
                    Status TEXT,
                    Location TEXT,
                    ImageURL TEXT
                )";
            cmd.ExecuteNonQuery();
        }

        // Insert comics
        if (comics != null)
        {
            foreach (var (comicId, comicName, comicYear, comicPublisher) in comics)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO comics (ComicID, ComicName, ComicYear, ComicPublisher) VALUES (@id, @name, @year, @publisher)";
                cmd.Parameters.AddWithValue("@id", comicId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@name", comicName);
                cmd.Parameters.AddWithValue("@year", comicYear ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@publisher", comicPublisher ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        // Insert issues
        if (issues != null)
        {
            foreach (var (issueId, comicId, issueNumber, issueName, status) in issues)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO issues (IssueID, ComicID, Issue_Number, IssueName, Status) VALUES (@id, @comicId, @number, @name, @status)";
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@comicId", comicId);
                cmd.Parameters.AddWithValue("@number", issueNumber);
                cmd.Parameters.AddWithValue("@name", issueName);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.ExecuteNonQuery();
            }
        }
    }

    #endregion
}
