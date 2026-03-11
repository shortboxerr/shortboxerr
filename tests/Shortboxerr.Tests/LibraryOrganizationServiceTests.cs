using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Services;

namespace Shortboxerr.Tests;

public class LibraryOrganizationServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _db;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<ILogger<LibraryOrganizationService>> _loggerMock;
    private readonly LibraryOrganizationService _service;
    private readonly string _tempDir;

    public LibraryOrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ShortboxerrDbContext(options);

        _settingsServiceMock = new Mock<ISettingsService>();
        _settingsServiceMock.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings
            {
                SeriesFolderFormat = "{Publisher}/{Series Title} ({Year})",
                IssueFileFormat = "{Series Title} #{Issue} ({Year})",
                CollectionFileFormat = "{Series Title} - {Edition Type} Vol. {Volume} ({Year})"
            });

        _loggerMock = new Mock<ILogger<LibraryOrganizationService>>();
        
        _tempDir = Path.Combine(Path.GetTempPath(), $"shortboxerr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MediaManagement:RootFolders:0"] = _tempDir
            })
            .Build();

        _service = new LibraryOrganizationService(_db, _settingsServiceMock.Object, config, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetSeriesRenamePreview_WithNoFiles_ReturnsEmptyFileList()
    {
        // Arrange
        var series = new Series
        {
            Title = "Test Series",
            Publisher = "Test Publisher",
            StartYear = 2024,
            Status = SeriesStatus.Continuing
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        Assert.Equal(series.Id, preview.SeriesId);
        Assert.Equal("Test Series", preview.SeriesTitle);
        Assert.Equal(0, preview.FileCount);
        Assert.Empty(preview.Files);
        Assert.True(preview.CanRename);
    }

    [Fact]
    public async Task GetSeriesRenamePreview_ComputesCorrectPath()
    {
        // Arrange
        var series = new Series
        {
            Title = "Batman",
            Publisher = "DC Comics",
            StartYear = 2016,
            Status = SeriesStatus.Continuing
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        var expectedPath = Path.Combine(_tempDir, "DC Comics", "Batman (2016)");
        Assert.Equal(expectedPath, preview.NewPath);
    }

    [Fact]
    public async Task GetSeriesRenamePreview_WithMissingYear_CleansUpFormat()
    {
        // Arrange
        var series = new Series
        {
            Title = "Untitled Series",
            Publisher = "Unknown Publisher"
            // No StartYear
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        // Should not have empty parentheses
        Assert.DoesNotContain("()", preview.NewPath);
    }

    [Fact]
    public async Task GetSeriesRenamePreview_SeriesNotFound_ReturnsNull()
    {
        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(99999);

        // Assert
        Assert.Null(preview);
    }

    [Fact]
    public async Task GetSeriesRenamePreviewsAsync_WithEmptySeriesIds_ReturnsAllSeries()
    {
        // Arrange
        _db.Series.AddRange(
            new Series { Title = "Series 1", Publisher = "Pub 1" },
            new Series { Title = "Series 2", Publisher = "Pub 2" },
            new Series { Title = "Series 3", Publisher = "Pub 3" }
        );
        await _db.SaveChangesAsync();

        // Act
        var previews = await _service.GetSeriesRenamePreviewsAsync(Array.Empty<int>());

        // Assert
        Assert.Equal(3, previews.Count);
    }

    [Fact]
    public async Task GetSeriesRenamePreviewsAsync_FiltersBySeriesIds()
    {
        // Arrange
        var series1 = new Series { Title = "Series 1", Publisher = "Pub 1" };
        var series2 = new Series { Title = "Series 2", Publisher = "Pub 2" };
        var series3 = new Series { Title = "Series 3", Publisher = "Pub 3" };
        _db.Series.AddRange(series1, series2, series3);
        await _db.SaveChangesAsync();

        // Act
        var previews = await _service.GetSeriesRenamePreviewsAsync(new[] { series1.Id, series3.Id });

        // Assert
        Assert.Equal(2, previews.Count);
        Assert.Contains(previews, p => p.SeriesTitle == "Series 1");
        Assert.Contains(previews, p => p.SeriesTitle == "Series 3");
    }

    [Fact]
    public async Task GetSeriesRenamePreview_ExcludesLinkedAnnualSeries()
    {
        // Arrange
        var parentSeries = new Series { Title = "Parent Series", Publisher = "Pub" };
        var annualSeries = new Series 
        { 
            Title = "Parent Series Annual",
            Publisher = "Pub",
            SeriesType = SeriesType.Annual
        };
        _db.Series.AddRange(parentSeries, annualSeries);
        await _db.SaveChangesAsync();
        
        annualSeries.ParentSeriesId = parentSeries.Id;
        await _db.SaveChangesAsync();

        // Act
        var previews = await _service.GetSeriesRenamePreviewsAsync(Array.Empty<int>());

        // Assert - should only return parent series
        Assert.Single(previews);
        Assert.Equal("Parent Series", previews[0].SeriesTitle);
    }

    [Fact]
    public async Task ExecuteSeriesRename_SeriesNotFound_ReturnsError()
    {
        // Act
        var result = await _service.ExecuteSeriesRenameAsync(99999);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Series not found", result.Error);
    }

    [Fact]
    public async Task ExecuteSeriesRename_WithNoFiles_SucceedsAndUpdatesPath()
    {
        // Arrange
        var series = new Series
        {
            Title = "Test Series",
            Publisher = "Test Publisher",
            StartYear = 2024
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ExecuteSeriesRenameAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.FilesRenamed);
        Assert.Equal(0, result.FilesFailed);
        
        // Verify series path was updated
        var updatedSeries = await _db.Series.FindAsync(series.Id);
        Assert.NotNull(updatedSeries);
        Assert.NotNull(updatedSeries.Path);
        Assert.Contains("Test Publisher", updatedSeries.Path);
        Assert.Contains("Test Series (2024)", updatedSeries.Path);
    }

    [Fact]
    public async Task ExecuteSeriesRenameAsync_EmptySeriesIds_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ExecuteSeriesRenameAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task GetSeriesRenamePreview_WillMove_TrueWhenPathsDiffer()
    {
        // Arrange
        var series = new Series
        {
            Title = "Batman",
            Publisher = "DC Comics",
            StartYear = 2016,
            Path = "/old/path/batman" // Different from computed path
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        Assert.True(preview.WillMove);
    }

    [Fact]
    public async Task GetSeriesRenamePreview_WillCreate_TrueWhenNoCurrentPath()
    {
        // Arrange
        var series = new Series
        {
            Title = "New Series",
            Publisher = "New Publisher"
            // No Path set
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        Assert.True(preview.WillCreate);
    }

    [Fact]
    public async Task GetSeriesRenamePreview_IncludesIssueFiles()
    {
        // Arrange
        var series = new Series
        {
            Title = "Batman",
            Publisher = "DC Comics",
            StartYear = 2016
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "Issue 1"
        };
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync();

        var testFile = Path.Combine(_tempDir, "test-file.cbz");
        await File.WriteAllTextAsync(testFile, "test content");

        var fileAsset = new FileAsset
        {
            Path = testFile,
            Format = "cbz",
            Size = 100,
            IssueId = issue.Id
        };
        _db.FileAssets.Add(fileAsset);
        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);

        // Assert
        Assert.NotNull(preview);
        Assert.Equal(1, preview.FileCount);
        Assert.Single(preview.Files);
        Assert.Equal(1m, preview.Files[0].IssueNumber);
        Assert.Contains("Batman", preview.Files[0].NewFileName);
        Assert.Contains("#001", preview.Files[0].NewFileName);
    }

    [Fact]
    public async Task ExecuteSeriesRename_WhenOneFileFails_RollsBackSuccessfulMovesAndDoesNotUpdateDb()
    {
        // Arrange: series with two files; second file's destination already exists so second move fails
        var currentDir = Path.Combine(_tempDir, "current");
        Directory.CreateDirectory(currentDir);
        var file1Path = Path.Combine(currentDir, "file1.cbz");
        var file2Path = Path.Combine(currentDir, "file2.cbz");
        await File.WriteAllTextAsync(file1Path, "content1");
        await File.WriteAllTextAsync(file2Path, "content2");

        var series = new Series
        {
            Title = "Batman",
            Publisher = "DC Comics",
            StartYear = 2016,
            Path = currentDir
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        var issue1 = new Issue { SeriesId = series.Id, IssueNumber = 1, Title = "One" };
        var issue2 = new Issue { SeriesId = series.Id, IssueNumber = 2, Title = "Two" };
        _db.Issues.AddRange(issue1, issue2);
        await _db.SaveChangesAsync();

        var asset1 = new FileAsset { Path = file1Path, Format = "cbz", Size = 100, IssueId = issue1.Id };
        var asset2 = new FileAsset { Path = file2Path, Format = "cbz", Size = 100, IssueId = issue2.Id };
        _db.FileAssets.AddRange(asset1, asset2);
        await _db.SaveChangesAsync();

        var preview = await _service.GetSeriesRenamePreviewAsync(series.Id);
        Assert.NotNull(preview);
        Assert.True(preview.CanRename);
        var secondFileNewPath = preview.Files[1].NewPath;
        var secondFileNewDir = Path.GetDirectoryName(secondFileNewPath);
        if (!string.IsNullOrEmpty(secondFileNewDir))
            Directory.CreateDirectory(secondFileNewDir);
        await File.WriteAllTextAsync(secondFileNewPath, "blocker"); // cause "destination already exists" for second move

        // Act
        var result = await _service.ExecuteSeriesRenameAsync(series.Id);

        // Assert: operation failed
        Assert.False(result.Success);
        Assert.Equal(1, result.FilesRenamed);
        Assert.Equal(1, result.FilesFailed);
        Assert.Contains("rolled back", result.Error ?? "");

        // First file was moved then rolled back: should be back at original path
        Assert.True(File.Exists(file1Path), "First file should be back at original path after rollback");
        Assert.False(File.Exists(preview.Files[0].NewPath), "First file should no longer be at new path after rollback");

        // DB unchanged: series path still old
        var updatedSeries = await _db.Series.FindAsync(series.Id);
        Assert.NotNull(updatedSeries);
        Assert.Equal(currentDir, updatedSeries.Path);
    }
}
