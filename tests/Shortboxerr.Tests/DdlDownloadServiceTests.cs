using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DDL download service functionality.
/// </summary>
public class DdlDownloadServiceTests : IDisposable
{
    private readonly IDdlDownloadService _downloadService;
    private readonly string _tempFolder;

    public DdlDownloadServiceTests()
    {
        _downloadService = new DdlDownloadService();
        _tempFolder = Path.Combine(Path.GetTempPath(), "shortboxerr_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        // Clean up temp folder
        try
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        catch { }
    }

    #region Download Options Tests

    [Fact]
    public void DownloadOptions_DefaultValues_AreCorrect()
    {
        var options = new DdlDownloadOptions();
        
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(1000, options.RetryDelayMs);
        Assert.Equal(30000, options.MaxRetryDelayMs);
        Assert.Equal(300, options.TimeoutSeconds);
        Assert.True(options.EnableResume);
        Assert.True(options.VerifyDownload);
    }

    #endregion

    #region Download Result Tests

    [Fact]
    public void DownloadResult_Succeeded_HasCorrectProperties()
    {
        var result = DdlDownloadResult.Succeeded(
            downloadId: "test-id",
            filePath: "/path/to/file.cbz",
            fileName: "file.cbz",
            fileSize: 1024 * 1024,
            duration: TimeSpan.FromSeconds(10),
            retryAttempts: 0,
            wasResumed: false,
            sourceUrl: "https://example.com/file.cbz"
        );
        
        Assert.True(result.Success);
        Assert.Equal("test-id", result.DownloadId);
        Assert.Equal("/path/to/file.cbz", result.FilePath);
        Assert.Equal("file.cbz", result.FileName);
        Assert.Equal(1024 * 1024, result.FileSize);
        Assert.Equal(DdlDownloadFailureReason.None, result.FailureReason);
        Assert.Null(result.ErrorMessage);
        Assert.True(result.BytesPerSecond > 0);
    }

    [Fact]
    public void DownloadResult_Failed_HasCorrectProperties()
    {
        var result = DdlDownloadResult.Failed(
            downloadId: "test-id",
            reason: DdlDownloadFailureReason.NotFound,
            errorMessage: "File not found",
            retryAttempts: 3,
            httpStatusCode: 404,
            sourceUrl: "https://example.com/missing.cbz"
        );
        
        Assert.False(result.Success);
        Assert.Equal("test-id", result.DownloadId);
        Assert.Equal(DdlDownloadFailureReason.NotFound, result.FailureReason);
        Assert.Equal("File not found", result.ErrorMessage);
        Assert.Equal(3, result.RetryAttempts);
        Assert.Equal(404, result.HttpStatusCode);
        Assert.Null(result.FilePath);
    }

    #endregion

    #region Download Status Tests

    [Fact]
    public void DownloadStatus_ProgressPercent_CalculatedCorrectly()
    {
        var status = new DdlDownloadStatus
        {
            DownloadId = "test",
            SourceUrl = "https://example.com",
            DestinationPath = "/test/path",
            StartedAt = DateTime.UtcNow,
            TotalBytes = 1000,
            BytesDownloaded = 500
        };
        
        Assert.Equal(50.0, status.ProgressPercent);
    }

    [Fact]
    public void DownloadStatus_ProgressPercent_ZeroWhenNoTotal()
    {
        var status = new DdlDownloadStatus
        {
            DownloadId = "test",
            SourceUrl = "https://example.com",
            DestinationPath = "/test/path",
            StartedAt = DateTime.UtcNow,
            TotalBytes = null,
            BytesDownloaded = 500
        };
        
        Assert.Equal(0, status.ProgressPercent);
    }

    #endregion

    #region Failure Reason Tests

    [Fact]
    public void FailureReason_AllValuesAreDefined()
    {
        var reasons = Enum.GetValues<DdlDownloadFailureReason>();
        
        // Ensure we have the expected failure categories
        Assert.Contains(DdlDownloadFailureReason.None, reasons);
        Assert.Contains(DdlDownloadFailureReason.Timeout, reasons);
        Assert.Contains(DdlDownloadFailureReason.NotFound, reasons);
        Assert.Contains(DdlDownloadFailureReason.Unauthorized, reasons);
        Assert.Contains(DdlDownloadFailureReason.RateLimited, reasons);
        Assert.Contains(DdlDownloadFailureReason.ServerError, reasons);
        Assert.Contains(DdlDownloadFailureReason.EmptyFile, reasons);
        Assert.Contains(DdlDownloadFailureReason.FileTooSmall, reasons);
        Assert.Contains(DdlDownloadFailureReason.HtmlErrorPage, reasons);
        Assert.Contains(DdlDownloadFailureReason.VerificationFailed, reasons);
        Assert.Contains(DdlDownloadFailureReason.Cancelled, reasons);
        Assert.Contains(DdlDownloadFailureReason.MaxRetriesExceeded, reasons);
        Assert.Contains(DdlDownloadFailureReason.NoValidLinks, reasons);
    }

    #endregion

    #region Download State Tests

    [Fact]
    public void DownloadState_AllStatesAreDefined()
    {
        var states = Enum.GetValues<DdlDownloadState>();
        
        Assert.Contains(DdlDownloadState.Queued, states);
        Assert.Contains(DdlDownloadState.Connecting, states);
        Assert.Contains(DdlDownloadState.Downloading, states);
        Assert.Contains(DdlDownloadState.Paused, states);
        Assert.Contains(DdlDownloadState.Retrying, states);
        Assert.Contains(DdlDownloadState.Verifying, states);
        Assert.Contains(DdlDownloadState.Completed, states);
        Assert.Contains(DdlDownloadState.Failed, states);
        Assert.Contains(DdlDownloadState.Cancelled, states);
    }

    #endregion

    #region Active Downloads Tests

    [Fact]
    public void GetActiveDownloads_InitiallyEmpty()
    {
        var active = _downloadService.GetActiveDownloads();
        
        Assert.Empty(active);
    }

    [Fact]
    public void GetDownloadHistory_InitiallyEmpty()
    {
        var history = _downloadService.GetDownloadHistory();
        
        Assert.Empty(history);
    }

    [Fact]
    public void GetDownloadStatus_ReturnsNullForUnknown()
    {
        var status = _downloadService.GetDownloadStatus("non-existent-id");
        
        Assert.Null(status);
    }

    [Fact]
    public void CancelDownload_ReturnsFalseForUnknown()
    {
        var result = _downloadService.CancelDownload("non-existent-id");
        
        Assert.False(result);
    }

    #endregion

    #region Download Candidate Tests

    [Fact]
    public async Task DownloadAsync_WithNoLinks_ReturnsNoValidLinks()
    {
        var candidate = new DdlCandidate
        {
            Id = "test",
            ReleaseTitle = "Test Comic #1.cbz",
            SourceSite = "TestSite",
            ParsedInfo = new DdlParsedInfo(),
            DownloadLinks = new List<DdlDownloadLink>() // Empty links
        };
        
        var result = await _downloadService.DownloadAsync(candidate);
        
        Assert.False(result.Success);
        Assert.Equal(DdlDownloadFailureReason.NoValidLinks, result.FailureReason);
    }

    [Fact]
    public async Task DownloadAsync_WithInvalidUrl_ReturnsConnectionFailed()
    {
        var candidate = new DdlCandidate
        {
            Id = "test",
            ReleaseTitle = "Test Comic #1.cbz",
            SourceSite = "TestSite",
            ParsedInfo = new DdlParsedInfo(),
            DownloadLinks = new List<DdlDownloadLink>
            {
                new()
                {
                    Url = "https://invalid.nonexistent.domain/file.cbz",
                    LinkType = DdlLinkType.Direct,
                    Priority = 0
                }
            }
        };
        
        var options = new DdlDownloadOptions
        {
            MaxRetries = 0, // No retries for faster test
            TimeoutSeconds = 5,
            DestinationFolder = _tempFolder
        };
        
        var result = await _downloadService.DownloadAsync(candidate, options);
        
        Assert.False(result.Success);
        // Accept any network-related failure
        Assert.True(
            result.FailureReason is DdlDownloadFailureReason.ConnectionFailed or 
                                    DdlDownloadFailureReason.DnsFailure or 
                                    DdlDownloadFailureReason.Timeout or
                                    DdlDownloadFailureReason.MaxRetriesExceeded or
                                    DdlDownloadFailureReason.Unknown,
            $"Expected network failure, got: {result.FailureReason}"
        );
    }

    #endregion

    #region History Entry Tests

    [Fact]
    public void DownloadHistoryEntry_HasAllRequiredFields()
    {
        var entry = new DdlDownloadHistoryEntry
        {
            Id = "entry-1",
            DownloadId = "download-1",
            SourceUrl = "https://example.com/file.cbz",
            SourceSite = "TestSite",
            ReleaseTitle = "Test Comic #1",
            DestinationPath = "/downloads/file.cbz",
            FileSize = 1024 * 1024,
            Success = true,
            RetryAttempts = 0,
            Duration = TimeSpan.FromSeconds(10),
            StartedAt = DateTime.UtcNow.AddSeconds(-10),
            CompletedAt = DateTime.UtcNow
        };
        
        Assert.NotNull(entry.Id);
        Assert.NotNull(entry.DownloadId);
        Assert.NotNull(entry.SourceUrl);
        Assert.True(entry.Success);
        Assert.Null(entry.FailureReason);
    }

    #endregion

    #region Progress Tests

    [Fact]
    public void DownloadProgress_HasCorrectFields()
    {
        var progress = new DdlDownloadProgress
        {
            DownloadId = "test",
            BytesDownloaded = 512 * 1024,
            TotalBytes = 1024 * 1024,
            ProgressPercent = 50.0,
            BytesPerSecond = 102400,
            EstimatedTimeRemaining = TimeSpan.FromSeconds(5)
        };
        
        Assert.Equal("test", progress.DownloadId);
        Assert.Equal(512 * 1024, progress.BytesDownloaded);
        Assert.Equal(1024 * 1024, progress.TotalBytes);
        Assert.Equal(50.0, progress.ProgressPercent);
        Assert.Equal(102400, progress.BytesPerSecond);
        Assert.NotNull(progress.EstimatedTimeRemaining);
    }

    #endregion
}

