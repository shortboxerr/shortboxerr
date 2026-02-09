using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Nzb;
using Shortboxerr.Infrastructure.Persistence;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for NzbImportService.
/// </summary>
public class NzbImportServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _db;
    private readonly Mock<ISabnzbdClient> _mockSabnzbdClient;
    private readonly Mock<IFilenameParser> _mockParser;
    private readonly Mock<IStagingService> _mockStagingService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly NzbImportService _service;
    private readonly string _tempDir;

    public NzbImportServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ShortboxerrDbContext(options);

        _mockSabnzbdClient = new Mock<ISabnzbdClient>();
        _mockParser = new Mock<IFilenameParser>();
        _mockStagingService = new Mock<IStagingService>();
        _mockSettingsService = new Mock<ISettingsService>();

        // Create temp directory for tests
        _tempDir = Path.Combine(Path.GetTempPath(), "shortboxerr_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MediaManagement:StagingFolder"] = Path.Combine(_tempDir, "staging")
            })
            .Build();

        _service = new NzbImportService(
            _db,
            _mockSabnzbdClient.Object,
            _mockParser.Object,
            _mockStagingService.Object,
            _mockSettingsService.Object,
            configuration,
            NullLogger<NzbImportService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    #region GetCompletedDownloadsAsync Tests

    [Fact]
    public async Task GetCompletedDownloads_ReturnsEmpty_WhenNoHistory()
    {
        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NzbDownloadStatus>());

        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var result = await _service.GetCompletedDownloadsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedDownloads_FiltersAlreadyProcessed()
    {
        var downloadPath = Path.Combine(_tempDir, "download1");
        Directory.CreateDirectory(downloadPath);

        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus
                {
                    Id = "processed-id",
                    Name = "Already Processed",
                    State = NzbDownloadState.Completed,
                    DownloadPath = downloadPath
                }
            });

        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "processed-id" });

        var result = await _service.GetCompletedDownloadsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedDownloads_FiltersNonCompleted()
    {
        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus
                {
                    Id = "failed-id",
                    Name = "Failed Download",
                    State = NzbDownloadState.Failed,
                    DownloadPath = _tempDir
                }
            });

        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var result = await _service.GetCompletedDownloadsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedDownloads_ReturnsCompletedWithValidPath()
    {
        var downloadPath = Path.Combine(_tempDir, "download1");
        Directory.CreateDirectory(downloadPath);

        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus
                {
                    Id = "new-id",
                    Name = "New Download",
                    State = NzbDownloadState.Completed,
                    DownloadPath = downloadPath,
                    TotalBytes = 1024 * 1024,
                    CompletedAt = DateTime.UtcNow
                }
            });

        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var result = await _service.GetCompletedDownloadsAsync();

        Assert.Single(result);
        Assert.Equal("new-id", result[0].DownloadId);
        Assert.Equal("New Download", result[0].Name);
    }

    [Fact]
    public async Task GetCompletedDownloads_SkipsNonExistentPath()
    {
        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus
                {
                    Id = "missing-path-id",
                    Name = "Missing Path",
                    State = NzbDownloadState.Completed,
                    DownloadPath = "/nonexistent/path"
                }
            });

        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var result = await _service.GetCompletedDownloadsAsync();

        Assert.Empty(result);
    }

    #endregion

    #region ProcessCompletedDownloadAsync Tests

    [Fact]
    public async Task ProcessCompletedDownload_ReturnsNoFilesFound_WhenEmpty()
    {
        var downloadPath = Path.Combine(_tempDir, "empty_download");
        Directory.CreateDirectory(downloadPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());

        var result = await _service.ProcessCompletedDownloadAsync(download);

        Assert.False(result.Success);
        Assert.Equal(NzbImportState.NoFilesFound, result.State);
    }

    [Fact]
    public async Task ProcessCompletedDownload_FindsComicFiles()
    {
        var downloadPath = Path.Combine(_tempDir, "comic_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Batman #001.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Batman", 1, 80);

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.True(result.Success);
        Assert.Single(result.ImportedFiles);
    }

    [Fact]
    public async Task ProcessCompletedDownload_MovesToStaging_WhenNoMatch()
    {
        var downloadPath = Path.Combine(_tempDir, "unmatched_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Unknown Comic.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser(null, null, 20); // Low confidence, no match

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.True(result.Success);
        Assert.Single(result.ImportedFiles);
        Assert.NotNull(result.ImportedFiles[0].StagingPath);
        Assert.False(result.ImportedFiles[0].WasAutoImported);
    }

    [Fact]
    public async Task ProcessCompletedDownload_AutoImports_WhenHighConfidence()
    {
        var downloadPath = Path.Combine(_tempDir, "matched_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Batman #001.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Add a series to the database
        var series = new Series
        {
            Title = "Batman",
            Status = SeriesStatus.Continuing,
            Monitored = true
        };
        _db.Series.Add(series);
        await _db.SaveChangesAsync();

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Batman", 1, 90); // High confidence

        _mockStagingService
            .Setup(s => s.ImportAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, int? sid, int? iid, int? eid, CancellationToken ct) => new ImportResult
            {
                Success = true,
                SourcePath = path,
                DestinationPath = "/library/Batman/Batman #001.cbz",
                FileAssetId = 123
            });

        var options = new NzbImportOptions { AutoImport = true, MinAutoImportConfidence = 80 };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.True(result.Success);
        Assert.Single(result.ImportedFiles);
        Assert.True(result.ImportedFiles[0].WasAutoImported);
        Assert.Equal(123, result.ImportedFiles[0].FileAssetId);
    }

    [Fact]
    public async Task ProcessCompletedDownload_CreatesHistoryEvent()
    {
        var downloadPath = Path.Combine(_tempDir, "history_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Batman #001.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Batman", 1, 50);

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.NotNull(result.HistoryEventId);

        var historyEvent = await _db.HistoryEvents.FindAsync(result.HistoryEventId);
        Assert.NotNull(historyEvent);
        Assert.Equal(HistoryEventType.DownloadCompleted, historyEvent.EventType);
    }

    [Fact]
    public async Task ProcessCompletedDownload_MarksAsProcessed()
    {
        var downloadPath = Path.Combine(_tempDir, "processed_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Comic.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Comic", 1, 50);

        var options = new NzbImportOptions { AutoImport = false };
        await _service.ProcessCompletedDownloadAsync(download, options);

        _mockSettingsService.Verify(s =>
            s.SetAsync(It.IsAny<string>(), It.Is<HashSet<string>>(h => h.Contains(download.DownloadId)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ProcessAllCompletedAsync Tests

    [Fact]
    public async Task ProcessAllCompleted_ProcessesAllDownloads()
    {
        var path1 = Path.Combine(_tempDir, "download1");
        var path2 = Path.Combine(_tempDir, "download2");
        Directory.CreateDirectory(path1);
        Directory.CreateDirectory(path2);
        await File.WriteAllBytesAsync(Path.Combine(path1, "comic1.cbz"), new byte[1024]);
        await File.WriteAllBytesAsync(Path.Combine(path2, "comic2.cbz"), new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus { Id = "d1", Name = "Download 1", State = NzbDownloadState.Completed, DownloadPath = path1 },
                new NzbDownloadStatus { Id = "d2", Name = "Download 2", State = NzbDownloadState.Completed, DownloadPath = path2 }
            });

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Comic", 1, 50);

        var options = new NzbImportOptions { AutoImport = false };
        var results = await _service.ProcessAllCompletedAsync(options);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task ProcessAllCompleted_FiltersByCategory()
    {
        var path1 = Path.Combine(_tempDir, "comics_download");
        var path2 = Path.Combine(_tempDir, "movies_download");
        Directory.CreateDirectory(path1);
        Directory.CreateDirectory(path2);
        await File.WriteAllBytesAsync(Path.Combine(path1, "comic.cbz"), new byte[1024]);
        await File.WriteAllBytesAsync(Path.Combine(path2, "movie.cbz"), new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        _mockSabnzbdClient
            .Setup(c => c.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NzbDownloadStatus { Id = "d1", Name = "Comic", State = NzbDownloadState.Completed, DownloadPath = path1, Category = "comics" },
                new NzbDownloadStatus { Id = "d2", Name = "Movie", State = NzbDownloadState.Completed, DownloadPath = path2, Category = "movies" }
            });

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Comic", 1, 50);

        var options = new NzbImportOptions { Categories = new List<string> { "comics" }, AutoImport = false };
        var results = await _service.ProcessAllCompletedAsync(options);

        Assert.Single(results);
        Assert.Equal("d1", results[0].DownloadId);
    }

    #endregion

    #region MarkAsProcessed and IsProcessed Tests

    [Fact]
    public async Task MarkAsProcessed_AddsToProcessedList()
    {
        var currentIds = new HashSet<string>();
        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentIds);

        await _service.MarkAsProcessedAsync("test-id");

        _mockSettingsService.Verify(s =>
            s.SetAsync(It.IsAny<string>(), It.Is<HashSet<string>>(h => h.Contains("test-id")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsProcessed_ReturnsTrue_WhenProcessed()
    {
        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "processed-id" });

        var result = await _service.IsProcessedAsync("processed-id");

        Assert.True(result);
    }

    [Fact]
    public async Task IsProcessed_ReturnsFalse_WhenNotProcessed()
    {
        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var result = await _service.IsProcessedAsync("new-id");

        Assert.False(result);
    }

    #endregion

    #region File Finding Tests

    [Fact]
    public async Task ProcessCompletedDownload_ReturnsResultWithSkipped_WhenProcessingFails()
    {
        var downloadPath = Path.Combine(_tempDir, "debug_download");
        Directory.CreateDirectory(downloadPath);
        var comicFile = Path.Combine(downloadPath, "Debug.cbz");
        await File.WriteAllBytesAsync(comicFile, new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        // Don't set up filename parser to cause an exception
        _mockParser
            .Setup(p => p.Parse(It.IsAny<string>()))
            .Throws(new Exception("Parser error"));

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        // Should succeed but have the file in skipped
        Assert.True(result.Success);
        Assert.Empty(result.ImportedFiles);
        Assert.Single(result.SkippedFiles);
    }

    [Fact]
    public async Task ProcessCompletedDownload_FindsNestedComicFiles()
    {
        var downloadPath = Path.Combine(_tempDir, "nested_download");
        var subDir = Path.Combine(downloadPath, "subfolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllBytesAsync(Path.Combine(subDir, "Comic.cbz"), new byte[1024]);

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Comic", 1, 50);

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.True(result.Success);
        Assert.Single(result.ImportedFiles);
    }

    [Fact]
    public async Task ProcessCompletedDownload_SupportsMultipleFormats()
    {
        var downloadPath = Path.Combine(_tempDir, "multiformat_download");
        Directory.CreateDirectory(downloadPath);
        await File.WriteAllBytesAsync(Path.Combine(downloadPath, "Comic1.cbz"), new byte[1024]);
        await File.WriteAllBytesAsync(Path.Combine(downloadPath, "Comic2.cbr"), new byte[1024]);
        await File.WriteAllBytesAsync(Path.Combine(downloadPath, "Comic3.pdf"), new byte[1024]);
        await File.WriteAllTextAsync(Path.Combine(downloadPath, "Readme.txt"), "not a comic");

        // Ensure staging folder exists
        var stagingPath = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingPath);

        var download = CreateCompletedDownload(downloadPath);

        SetupProcessedIds(new HashSet<string>());
        SetupFilenameParser("Comic", 1, 50);

        var options = new NzbImportOptions { AutoImport = false };
        var result = await _service.ProcessCompletedDownloadAsync(download, options);

        Assert.True(result.Success);
        Assert.Equal(3, result.ImportedFiles.Count);
        Assert.Empty(result.SkippedFiles);
    }

    #endregion

    #region Helper Methods

    private NzbCompletedDownload CreateCompletedDownload(string downloadPath, string? name = null)
    {
        return new NzbCompletedDownload
        {
            DownloadId = Guid.NewGuid().ToString(),
            Name = name ?? "Test Download",
            DownloadPath = downloadPath,
            CompletedAt = DateTime.UtcNow,
            TotalBytes = 1024 * 1024,
            ClientName = "SABnzbd"
        };
    }

    private void SetupProcessedIds(HashSet<string> ids)
    {
        _mockSettingsService
            .Setup(s => s.GetAsync<HashSet<string>>(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        _mockSettingsService
            .Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupFilenameParser(string? series, decimal? issue, int confidence)
    {
        var result = (new ParsedComicInfo
        {
            SeriesTitle = series,
            IssueNumber = issue
        }, confidence, false);
        
        _mockParser
            .Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(result);
    }

    #endregion
}
