using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

public class DdlImportServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;
    private readonly DdlImportService _service;
    private readonly DdlReleaseParser _parser;
    private readonly IConfiguration _configuration;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly string _testFolder;

    public DdlImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ShortboxerrDbContext(options);
        _parser = new DdlReleaseParser();
        
        _testFolder = Path.Combine(Path.GetTempPath(), "ddl_import_tests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testFolder);
        
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Shortboxerr:StagingFolder", Path.Combine(_testFolder, "staging") },
            { "Shortboxerr:LibraryFolder", Path.Combine(_testFolder, "library") }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        
        // Setup settings service mock with default folder format and auto-match settings
        _settingsServiceMock = new Mock<ISettingsService>();
        _settingsServiceMock.Setup(s => s.GetGeneralSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings
            {
                SeriesFolderFormat = "{Publisher}/{Series Title} ({Year})",
                IssueFileFormat = "{Series Title} #{Issue} ({Year})",
                CollectionFileFormat = "{Series Title} - {Edition Type} Vol. {Volume}"
            });
        _settingsServiceMock.Setup(s => s.GetAutoMatchSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoMatchSettings
            {
                YearMatchTolerance = 2,
                RejectMismatchedYears = true,
                YearMismatchPenalty = 25,
                ConfidenceThreshold = 85,
                RequireYearForAmbiguousSeries = true,
                EnableAmbiguousSeriesDetection = true,
                AutoMatchOnImport = true,
                CreateMissingItems = true,
                MaxCandidatesForReview = 5
            });
        
        _service = new DdlImportService(_context, _parser, _configuration, _settingsServiceMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (Directory.Exists(_testFolder))
        {
            try { Directory.Delete(_testFolder, true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    private string CreateTestFile(string filename, byte[] content)
    {
        var downloadFolder = Path.Combine(_testFolder, "downloads");
        Directory.CreateDirectory(downloadFolder);
        var filePath = Path.Combine(downloadFolder, filename);
        File.WriteAllBytes(filePath, content);
        return filePath;
    }

    private static byte[] CreateMinimalZip()
    {
        // Minimal ZIP file (empty archive with just end-of-central-directory)
        return new byte[]
        {
            0x50, 0x4B, 0x05, 0x06, // End of central directory signature
            0x00, 0x00, // Number of this disk
            0x00, 0x00, // Disk where central directory starts
            0x00, 0x00, // Number of central directory records on this disk
            0x00, 0x00, // Total number of central directory records
            0x00, 0x00, 0x00, 0x00, // Size of central directory
            0x00, 0x00, 0x00, 0x00, // Offset of start of central directory
            0x00, 0x00  // Comment length
        };
    }

    private static byte[] CreateLargerZip()
    {
        // Create a larger "ZIP" file (just header + padding)
        var content = new byte[2_000_000]; // 2MB
        content[0] = 0x50; // P
        content[1] = 0x4B; // K
        return content;
    }

    private DdlCandidate CreateCandidate(string releaseTitle, string? seriesTitle = null, decimal? issueNumber = null, bool isCollection = false)
    {
        return new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = releaseTitle,
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = seriesTitle ?? "Batman",
                IssueNumber = issueNumber ?? 1,
                IsCollection = isCollection,
                Format = "cbz"
            }
        };
    }

    [Fact]
    public async Task VerifyFile_WithValidZip_ReturnsValid()
    {
        var filePath = CreateTestFile("test.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.VerifyFileAsync(filePath, candidate);

        Assert.True(result.IsValid);
        Assert.Equal("cbz", result.DetectedFormat);
        Assert.True(result.FormatSupported);
    }

    [Fact]
    public async Task VerifyFile_WithEmptyFile_ReturnsInvalid()
    {
        var filePath = CreateTestFile("empty.cbz", Array.Empty<byte>());
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.VerifyFileAsync(filePath, candidate);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task VerifyFile_WithHtmlErrorPage_ReturnsInvalid()
    {
        var htmlContent = "<!DOCTYPE html><html><body>404 Not Found</body></html>"u8.ToArray();
        var filePath = CreateTestFile("error.cbz", htmlContent);
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.VerifyFileAsync(filePath, candidate);

        Assert.False(result.IsValid);
        Assert.Contains("html", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task VerifyFile_WithNonExistentFile_ReturnsInvalid()
    {
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.VerifyFileAsync("/nonexistent/path/file.cbz", candidate);

        Assert.False(result.IsValid);
        Assert.Contains("not exist", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task MoveToStaging_WithValidFile_MovesSuccessfully()
    {
        var filePath = CreateTestFile("Batman.001.cbz", CreateMinimalZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.MoveToStagingAsync(filePath, candidate);

        Assert.True(result.Success);
        Assert.NotNull(result.StagingPath);
        Assert.False(File.Exists(filePath)); // Source should be moved
        Assert.True(File.Exists(result.StagingPath)); // Destination should exist
    }

    [Fact]
    public async Task MoveToStaging_WithDuplicateFilename_CreatesUniqueFilename()
    {
        var stagingFolder = Path.Combine(_testFolder, "staging");
        Directory.CreateDirectory(stagingFolder);
        
        // Create existing file in staging
        var existingFile = Path.Combine(stagingFolder, "Batman 001 (2016).cbz");
        File.WriteAllBytes(existingFile, CreateMinimalZip());
        
        var filePath = CreateTestFile("batman.cbz", CreateMinimalZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz");

        var result = await _service.MoveToStagingAsync(filePath, candidate);

        Assert.True(result.Success);
        Assert.NotEqual(existingFile, result.StagingPath);
        Assert.Contains("_1", result.StagingFilename);
    }

    [Fact]
    public async Task AutoMatch_WithMatchingSeries_ReturnsMatch()
    {
        // Add series to database
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.Series);
        Assert.Equal("Batman", result.Series.Title);
    }

    [Fact]
    public async Task AutoMatch_WithMatchingSeriesAndIssue_ReturnsHighConfidence()
    {
        // Add series and issue to database
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "I Am Gotham Part 1"
        };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.Issue);
        Assert.Equal(1, result.Issue.IssueNumber);
        Assert.True(result.Confidence >= 80);
    }

    [Fact]
    public async Task AutoMatch_WithNoMatchingSeries_ReturnsNoMatch()
    {
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);

        var result = await _service.AutoMatchAsync(candidate);

        Assert.False(result.MatchFound);
        Assert.Contains("No series found", result.Explanation);
    }

    [Fact]
    public async Task ProcessDownload_WithHighConfidenceMatch_AutoImports()
    {
        // Setup series and issue
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "I Am Gotham Part 1"
        };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions
        {
            AutoImportEnabled = true,
            AutoImportMinConfidence = 80,
            RequireSeriesMatch = true,
            RequireIssueMatch = true
        };

        var result = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.True(result.Success);
        Assert.Equal(DdlImportState.Completed, result.State);
        Assert.NotNull(result.LibraryPath);
    }

    [Fact]
    public async Task ProcessDownload_WithLowConfidenceMatch_QueuesPendingReview()
    {
        // Setup series but no issue
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var filePath = CreateTestFile("Batman.099.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 099 (2016).cbz", "Batman", 99);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions
        {
            AutoImportEnabled = true,
            AutoImportMinConfidence = 80,
            RequireSeriesMatch = true,
            RequireIssueMatch = true // Requires issue match, but issue doesn't exist
        };

        var result = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.False(result.Success);
        Assert.Equal(DdlImportState.PendingReview, result.State);
        Assert.True(result.PendingManualReview);
        Assert.NotNull(result.PendingImportId);
    }

    [Fact]
    public async Task ProcessDownload_WithAutoImportDisabled_QueuesPendingReview()
    {
        // Setup series and issue
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "I Am Gotham Part 1"
        };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions
        {
            AutoImportEnabled = false // Disabled
        };

        var result = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.False(result.Success);
        Assert.True(result.PendingManualReview);
    }

    [Fact]
    public async Task GetPendingImports_ReturnsAllPending()
    {
        // Create some pending imports
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // First pending import
        var filePath1 = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate1 = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult1 = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath1,
            fileName: Path.GetFileName(filePath1),
            fileSize: new FileInfo(filePath1).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions { AutoImportEnabled = false };
        await _service.ProcessDownloadAsync(downloadResult1, candidate1, options);

        // Second pending import
        var filePath2 = CreateTestFile("Batman.002.cbz", CreateLargerZip());
        var candidate2 = CreateCandidate("Batman 002 (2016).cbz", "Batman", 2);
        var downloadResult2 = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath2,
            fileName: Path.GetFileName(filePath2),
            fileSize: new FileInfo(filePath2).Length,
            duration: TimeSpan.FromSeconds(30)
        );
        await _service.ProcessDownloadAsync(downloadResult2, candidate2, options);

        var pending = await _service.GetPendingImportsAsync();

        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task ApprovePendingImport_WithValidId_ImportsSuccessfully()
    {
        // Setup series
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Create pending import
        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions { AutoImportEnabled = false };
        var processResult = await _service.ProcessDownloadAsync(downloadResult, candidate, options);
        
        Assert.True(processResult.PendingManualReview);
        Assert.NotNull(processResult.PendingImportId);

        // Approve the pending import
        var approveResult = await _service.ApprovePendingImportAsync(processResult.PendingImportId, series.Id);

        Assert.True(approveResult.Success);
        Assert.Equal(DdlImportState.Completed, approveResult.State);
    }

    [Fact]
    public async Task RejectPendingImport_WithValidId_RemovesPending()
    {
        // Setup series
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Create pending import
        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions { AutoImportEnabled = false };
        var processResult = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.NotNull(processResult.PendingImportId);

        // Reject the pending import
        var rejectResult = await _service.RejectPendingImportAsync(processResult.PendingImportId, "Not wanted", deleteFile: false);

        Assert.True(rejectResult);
        
        // Verify it's removed from pending
        var pending = await _service.GetPendingImportsAsync();
        Assert.DoesNotContain(pending, p => p.Id == processResult.PendingImportId);
    }

    [Fact]
    public async Task RejectPendingImport_WithDeleteFlag_DeletesFile()
    {
        // Setup series
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Create pending import
        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions { AutoImportEnabled = false };
        var processResult = await _service.ProcessDownloadAsync(downloadResult, candidate, options);
        var stagingPath = processResult.StagingPath;

        Assert.NotNull(processResult.PendingImportId);

        // Reject with delete flag
        await _service.RejectPendingImportAsync(processResult.PendingImportId, "Not wanted", deleteFile: true);

        // Verify file is deleted
        Assert.False(File.Exists(stagingPath));
    }

    [Fact]
    public async Task ProcessDownload_CreatesHistoryEvent()
    {
        // Setup series and issue
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "I Am Gotham Part 1"
        };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions
        {
            AutoImportEnabled = true,
            AutoImportMinConfidence = 80,
            RequireSeriesMatch = true,
            RequireIssueMatch = true
        };

        var result = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.True(result.Success);
        Assert.NotNull(result.HistoryEventId);

        // Verify history event was created
        var historyEvent = await _context.HistoryEvents.FindAsync(result.HistoryEventId.Value);
        Assert.NotNull(historyEvent);
        Assert.Equal(HistoryEventType.DdlImportCompleted, historyEvent.EventType);
        Assert.Equal(series.Id, historyEvent.SeriesId);
    }

    [Fact]
    public async Task ProcessDownload_CreatesFileAsset()
    {
        // Setup series and issue
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var issue = new Issue
        {
            SeriesId = series.Id,
            IssueNumber = 1,
            Title = "I Am Gotham Part 1"
        };
        _context.Issues.Add(issue);
        await _context.SaveChangesAsync();

        var filePath = CreateTestFile("Batman.001.cbz", CreateLargerZip());
        var candidate = CreateCandidate("Batman 001 (2016).cbz", "Batman", 1);
        var downloadResult = DdlDownloadResult.Succeeded(
            downloadId: Guid.NewGuid().ToString(),
            filePath: filePath,
            fileName: Path.GetFileName(filePath),
            fileSize: new FileInfo(filePath).Length,
            duration: TimeSpan.FromSeconds(30)
        );

        var options = new DdlImportOptions
        {
            AutoImportEnabled = true,
            AutoImportMinConfidence = 80,
            RequireSeriesMatch = true,
            RequireIssueMatch = true
        };

        var result = await _service.ProcessDownloadAsync(downloadResult, candidate, options);

        Assert.True(result.Success);
        Assert.NotNull(result.FileAssetId);

        // Verify file asset was created
        var fileAsset = await _context.FileAssets.FindAsync(result.FileAssetId.Value);
        Assert.NotNull(fileAsset);
        Assert.Equal(issue.Id, fileAsset.IssueId);
        Assert.Equal("cbz", fileAsset.Format);
    }

    // ========== Year-Aware Matching Tests (EPIC 19.1) ==========

    [Fact]
    public async Task AutoMatch_WithExactYearMatch_ReturnsHighConfidence()
    {
        // Setup: Series with year 2017
        var series = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2017,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Candidate with matching year
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Deadman 001 (2017).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Deadman",
                IssueNumber = 1,
                Year = 2017,
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.Equal("Deadman", result.Series?.Title);
        Assert.True(result.Confidence >= 80, $"Expected confidence >= 80, got {result.Confidence}");
    }

    [Fact]
    public async Task AutoMatch_WithYearMismatchBeyondTolerance_RejectsMatch()
    {
        // Setup: Series with year 2006 (Deadman original run)
        var series = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2006,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Candidate from 2017 - 11 year difference, well beyond tolerance of 2
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Deadman 001 (2017).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Deadman",
                IssueNumber = 1,
                Year = 2017,
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.False(result.MatchFound, "Should reject match when year mismatch exceeds tolerance");
        Assert.Contains("year mismatch", result.Explanation?.ToLower() ?? "");
    }

    [Fact]
    public async Task AutoMatch_WithYearWithinTolerance_MatchesWithReducedConfidence()
    {
        // Setup: Series with year 2016
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Candidate from 2017 - 1 year difference, within tolerance of 2
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Batman 001 (2017).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2017,
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.Equal("Batman", result.Series?.Title);
    }

    [Fact]
    public async Task AutoMatch_WithMultipleSameNameSeries_SelectsCorrectByYear()
    {
        // Setup: Two series with same name but different years
        var series2006 = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2006,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        var series2017 = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2017,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        _context.Series.AddRange(series2006, series2017);
        await _context.SaveChangesAsync();

        // Candidate from 2017
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Deadman 002 (2017).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Deadman",
                IssueNumber = 2,
                Year = 2017,
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.Series);
        // Should match the 2017 series, not the 2006 series
        Assert.Equal(2017, result.Series.StartYear);
    }

    [Fact]
    public async Task AutoMatch_AmbiguousSeriesWithoutYear_FlagsLowConfidence()
    {
        // Setup: Two series with same name
        var series2006 = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2006,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        var series2017 = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2017,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        _context.Series.AddRange(series2006, series2017);
        await _context.SaveChangesAsync();

        // Candidate WITHOUT year - ambiguous!
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Deadman 002.cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Deadman",
                IssueNumber = 2,
                Year = null, // No year in release name
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        // Should have reduced confidence due to ambiguous series without year
        Assert.True(result.Confidence < 70, 
            $"Expected reduced confidence for ambiguous series without year, got {result.Confidence}");
        Assert.Contains(result.ConfidenceReductions, r => r.ToLower().Contains("ambiguous"));
    }

    [Fact]
    public async Task AutoMatch_WithRejectMismatchedYearsDisabled_MatchesWithPenalty()
    {
        // Setup with RejectMismatchedYears = false
        _settingsServiceMock.Setup(s => s.GetAutoMatchSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoMatchSettings
            {
                YearMatchTolerance = 2,
                RejectMismatchedYears = false, // Don't reject, just penalize
                YearMismatchPenalty = 25,
                ConfidenceThreshold = 85,
                RequireYearForAmbiguousSeries = true,
                EnableAmbiguousSeriesDetection = true,
                AutoMatchOnImport = true,
                CreateMissingItems = true,
                MaxCandidatesForReview = 5
            });

        // Setup: Series with year 2006
        var series = new Series
        {
            Title = "Deadman",
            SortTitle = "deadman",
            StartYear = 2006,
            Publisher = "DC Comics",
            Status = SeriesStatus.Ended
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        // Candidate from 2017 - big year difference but should still match (with penalty)
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Deadman 001 (2017).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Deadman",
                IssueNumber = 1,
                Year = 2017,
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        // When RejectMismatchedYears is false, it should still match but with reduced confidence
        Assert.True(result.MatchFound);
        Assert.Equal("Deadman", result.Series?.Title);
    }

    // === EPIC 19.2: Publisher Disambiguation Tests ===

    [Fact]
    public async Task AutoMatch_WithMatchingPublisher_ReturnsHigherConfidence()
    {
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Batman 001 (2016) (DC).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016,
                Publisher = "DC Comics", // Matches series publisher
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.Series);
        // Score breakdown should show publisher match bonus
        Assert.NotNull(result.ScoreBreakdown);
        Assert.Equal("Exact", result.ScoreBreakdown.PublisherMatchStatus);
        Assert.True(result.ScoreBreakdown.PublisherAdjustment > 0);
    }

    [Fact]
    public async Task AutoMatch_WithMismatchedPublisher_ReducesConfidence()
    {
        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Batman 001 (2016) (Marvel).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016,
                Publisher = "Marvel", // Different publisher!
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.ScoreBreakdown);
        Assert.Equal("Mismatch", result.ScoreBreakdown.PublisherMatchStatus);
        Assert.True(result.ScoreBreakdown.PublisherAdjustment < 0);
    }

    [Fact]
    public async Task AutoMatch_AmbiguousSeriesWithPublisher_PrefersMatchingPublisher()
    {
        // Two series with same name but different publishers
        var dcSeries = new Series
        {
            Title = "Spawn",
            SortTitle = "spawn",
            StartYear = 2019,
            Publisher = "DC Comics", // Wrong publisher
            Status = SeriesStatus.Continuing
        };
        var imageSeries = new Series
        {
            Title = "Spawn",
            SortTitle = "spawn",
            StartYear = 2019,
            Publisher = "Image Comics", // Correct publisher
            Status = SeriesStatus.Continuing
        };
        _context.Series.AddRange(dcSeries, imageSeries);
        await _context.SaveChangesAsync();

        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Spawn 001 (2019) (Image).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Spawn",
                IssueNumber = 1,
                Year = 2019,
                Publisher = "Image Comics",
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.Series);
        Assert.Equal("Image Comics", result.Series.Publisher);
    }

    [Fact]
    public async Task AutoMatch_WithRejectMismatchedPublishers_RejectsOnMismatch()
    {
        // Setup with RejectMismatchedPublishers = true
        _settingsServiceMock.Setup(s => s.GetAutoMatchSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutoMatchSettings
            {
                YearMatchTolerance = 2,
                RejectMismatchedYears = true,
                YearMismatchPenalty = 25,
                ConfidenceThreshold = 85,
                RequireYearForAmbiguousSeries = true,
                EnableAmbiguousSeriesDetection = true,
                PublisherMatchBonus = 15,
                PublisherMismatchPenalty = 20,
                PreferPublisherMatchForAmbiguous = true,
                RejectMismatchedPublishers = true, // Strict mode
                AutoMatchOnImport = true,
                CreateMissingItems = true,
                MaxCandidatesForReview = 5
            });

        var series = new Series
        {
            Title = "Batman",
            SortTitle = "batman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Batman 001 (2016) (Marvel).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2016,
                Publisher = "Marvel", // Mismatched publisher
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        // With RejectMismatchedPublishers=true, should not match
        Assert.False(result.MatchFound);
        Assert.Contains("publisher mismatch", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoMatch_HasDetailedConfidenceBreakdown()
    {
        var series = new Series
        {
            Title = "Wonder Woman",
            SortTitle = "wonder woman",
            StartYear = 2016,
            Publisher = "DC Comics",
            Status = SeriesStatus.Continuing
        };
        _context.Series.Add(series);
        await _context.SaveChangesAsync();

        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = "Wonder Woman 001 (2016) (DC).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Wonder Woman",
                IssueNumber = 1,
                Year = 2016,
                Publisher = "DC Comics",
                Format = "cbz"
            }
        };

        var result = await _service.AutoMatchAsync(candidate);

        Assert.True(result.MatchFound);
        Assert.NotNull(result.ScoreBreakdown);
        
        // Verify breakdown has all expected components
        Assert.True(result.ScoreBreakdown.TitleScore > 0);
        Assert.NotEmpty(result.ScoreBreakdown.YearMatchStatus);
        Assert.NotEmpty(result.ScoreBreakdown.PublisherMatchStatus);
        Assert.True(result.ScoreBreakdown.FinalScore > 0);
        Assert.NotEmpty(result.ScoreBreakdown.ScoreExplanations);
    }
}

