using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Services;
using System.Net;

namespace Shortboxerr.Tests;

public class CoverServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<CoverService>> _mockLogger;
    private readonly CoverService _service;
    private readonly string _testCacheDir;

    public CoverServiceTests()
    {
        // Use in-memory SQLite for testing
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<CoverService>>();

        // Create a unique test cache directory
        _testCacheDir = Path.Combine(Path.GetTempPath(), "shortboxerr_cover_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testCacheDir);

        // Setup default settings
        _mockSettingsService.Setup(x => x.GetAsync<CoverSettings>(
                "covers",
                It.IsAny<CoverSettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CoverSettings { CacheDirectory = _testCacheDir });

        _service = new CoverService(
            _dbContext,
            _mockHttpClientFactory.Object,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();

        // Clean up test cache directory
        if (Directory.Exists(_testCacheDir))
        {
            try
            {
                Directory.Delete(_testCacheDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void SetupHttpClient(HttpStatusCode statusCode, byte[]? content = null, string contentType = "image/jpeg")
    {
        _mockHttpClientFactory.Setup(x => x.CreateClient("CoverDownload"))
            .Returns(() =>
            {
                var mockHandler = new Mock<HttpMessageHandler>();
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(() => new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = content != null 
                            ? new ByteArrayContent(content) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) } }
                            : null
                    });
                return new HttpClient(mockHandler.Object);
            });
    }

    [Fact]
    public async Task GetSeriesCoverAsync_WithNonExistentSeries_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetSeriesCoverAsync(9999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetSeriesCoverAsync_WithNoCoverUrl_ReturnsPlaceholder()
    {
        // Arrange
        var series = new Series { Title = "Test Series", CoverImageUrl = null };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSeriesCoverAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsPlaceholder);
    }

    [Fact]
    public async Task GetSeriesCoverAsync_WithCachedCover_ReturnsCachedFile()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Test Series", 
            CoverImageUrl = "https://example.com/cover.jpg" 
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Create a cached file
        var cachePath = Path.Combine(_testCacheDir, "series", series.Id.ToString());
        Directory.CreateDirectory(cachePath);
        var coverPath = Path.Combine(cachePath, "medium.jpg");
        await File.WriteAllBytesAsync(coverPath, new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG magic bytes

        // Act
        var result = await _service.GetSeriesCoverAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsPlaceholder);
        Assert.NotNull(result.FilePath);
        Assert.True(File.Exists(result.FilePath));
    }

    [Fact]
    public async Task GetSeriesCoverAsync_WithValidUrl_DownloadsAndCaches()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Test Series", 
            CoverImageUrl = "https://example.com/cover.jpg" 
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        SetupHttpClient(HttpStatusCode.OK, imageBytes);

        // Act
        var result = await _service.GetSeriesCoverAsync(series.Id);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsPlaceholder);
        Assert.NotNull(result.FilePath);
        Assert.True(File.Exists(result.FilePath));
        Assert.Equal(CoverType.Series, result.CoverType);
    }

    [Fact]
    public async Task GetIssueCoverAsync_WithNonExistentIssue_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetIssueCoverAsync(9999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetIssueCoverAsync_WithNoCoverUrl_FallsBackToSeriesCover()
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Test Series", 
            CoverImageUrl = "https://example.com/series-cover.jpg" 
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 1, 
            CoverImageUrl = null 
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        SetupHttpClient(HttpStatusCode.OK, imageBytes);

        // Act
        var result = await _service.GetIssueCoverAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task GetIssueCoverAsync_WithNoCoverAnywhere_ReturnsPlaceholder()
    {
        // Arrange
        var series = new Series { Title = "Test Series", CoverImageUrl = null };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        var issue = new Issue 
        { 
            SeriesId = series.Id, 
            IssueNumber = 1, 
            CoverImageUrl = null 
        };
        _dbContext.Issues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetIssueCoverAsync(issue.Id);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsPlaceholder);
    }

    [Fact]
    public async Task DownloadCoverAsync_WithEmptyUrl_ReturnsNotFound()
    {
        // Act
        var result = await _service.DownloadCoverAsync("", CoverType.Series, 1);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No URL", result.Error);
    }

    [Fact]
    public async Task DownloadCoverAsync_WithFailedDownload_ReturnsError()
    {
        // Arrange
        SetupHttpClient(HttpStatusCode.NotFound);

        // Act
        var result = await _service.DownloadCoverAsync(
            "https://example.com/cover.jpg", 
            CoverType.Series, 
            1);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to download", result.Error);
    }

    [Fact]
    public async Task DownloadCoverAsync_WithInvalidContentType_ReturnsError()
    {
        // Arrange
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<html>Error</html>");
        SetupHttpClient(HttpStatusCode.OK, htmlBytes, "text/html");

        // Act
        var result = await _service.DownloadCoverAsync(
            "https://example.com/cover.jpg", 
            CoverType.Series, 
            1);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid content type", result.Error);
    }

    [Fact]
    public async Task ClearSeriesCoverCacheAsync_DeletesCacheDirectory()
    {
        // Arrange
        var series = new Series { Title = "Test Series" };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        // Create a cached file
        var cachePath = Path.Combine(_testCacheDir, "series", series.Id.ToString());
        Directory.CreateDirectory(cachePath);
        var coverPath = Path.Combine(cachePath, "medium.jpg");
        await File.WriteAllBytesAsync(coverPath, new byte[] { 0xFF, 0xD8, 0xFF });

        Assert.True(Directory.Exists(cachePath));

        // Act
        await _service.ClearSeriesCoverCacheAsync(series.Id);

        // Assert
        Assert.False(Directory.Exists(cachePath));
    }

    [Fact]
    public async Task GetCacheStatsAsync_ReturnsCorrectStatistics()
    {
        // Arrange - Create some cached files
        var seriesDir = Path.Combine(_testCacheDir, "series", "1");
        Directory.CreateDirectory(seriesDir);
        await File.WriteAllBytesAsync(Path.Combine(seriesDir, "medium.jpg"), new byte[1024]);
        await File.WriteAllBytesAsync(Path.Combine(seriesDir, "thumb.jpg"), new byte[256]);

        var issueDir = Path.Combine(_testCacheDir, "issues", "1");
        Directory.CreateDirectory(issueDir);
        await File.WriteAllBytesAsync(Path.Combine(issueDir, "medium.jpg"), new byte[2048]);

        // Act
        var stats = await _service.GetCacheStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalCovers);
        Assert.Equal(2, stats.SeriesCovers);
        Assert.Equal(1, stats.IssueCovers);
        Assert.Equal(1024 + 256 + 2048, stats.TotalSizeBytes);
    }

    [Fact]
    public async Task ClearAllCacheAsync_RemovesAllCachedCovers()
    {
        // Arrange - Create some cached files
        var seriesDir = Path.Combine(_testCacheDir, "series", "1");
        Directory.CreateDirectory(seriesDir);
        await File.WriteAllBytesAsync(Path.Combine(seriesDir, "medium.jpg"), new byte[1024]);

        var issueDir = Path.Combine(_testCacheDir, "issues", "1");
        Directory.CreateDirectory(issueDir);
        await File.WriteAllBytesAsync(Path.Combine(issueDir, "medium.jpg"), new byte[1024]);

        // Act
        await _service.ClearAllCacheAsync();

        // Assert
        Assert.False(Directory.Exists(seriesDir));
        Assert.False(Directory.Exists(issueDir));
    }

    [Theory]
    [InlineData(CoverSize.Thumb, "scale_avatar")]
    [InlineData(CoverSize.Small, "scale_small")]
    [InlineData(CoverSize.Medium, "scale_medium")]
    [InlineData(CoverSize.Large, "original")]
    public async Task GetSeriesCoverAsync_RequestsCorrectSize(CoverSize size, string expectedUrlPart)
    {
        // Arrange
        var series = new Series 
        { 
            Title = "Test Series", 
            CoverImageUrl = "https://comicvine.gamespot.com/a/uploads/scale_medium/11/110017/cover.jpg"
        };
        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();

        string? requestedUrl = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => requestedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                }
            });

        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("CoverDownload")).Returns(client);

        // Act
        await _service.GetSeriesCoverAsync(series.Id, size);

        // Assert
        Assert.NotNull(requestedUrl);
        Assert.Contains(expectedUrlPart, requestedUrl);
    }

    #region DownloadExternalCoverAsync Tests

    [Fact]
    public async Task DownloadExternalCoverAsync_WithValidUrl_DownloadsCoverAndSetsSource()
    {
        // Arrange
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes
        SetupHttpClient(HttpStatusCode.OK, imageBytes);

        // Act
        var result = await _service.DownloadExternalCoverAsync(
            "https://metron.cloud/media/issue/cover.jpg",
            CoverType.Discovery,
            12345,
            CoverCacheSource.Metron,
            CoverSize.Medium);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.FilePath);
        Assert.True(File.Exists(result.FilePath));

        // Check metadata was saved with correct source
        var metadata = await _service.GetCachedCoverMetadataAsync(CoverType.Discovery, 12345, CoverSize.Medium);
        Assert.NotNull(metadata);
        Assert.Equal(CoverCacheSource.Metron, metadata.Source);
    }

    [Fact]
    public async Task DownloadExternalCoverAsync_SkipsIfHigherPrioritySourceExists()
    {
        // Arrange - First download a ComicVine cover (higher priority)
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        SetupHttpClient(HttpStatusCode.OK, imageBytes);

        await _service.DownloadExternalCoverAsync(
            "https://comicvine.com/cover.jpg",
            CoverType.Discovery,
            12345,
            CoverCacheSource.ComicVine,
            CoverSize.Medium);

        // Act - Try to download a Metron cover (lower priority)
        var result = await _service.DownloadExternalCoverAsync(
            "https://metron.cloud/cover.jpg",
            CoverType.Discovery,
            12345,
            CoverCacheSource.Metron,
            CoverSize.Medium);

        // Assert - Should still succeed but not overwrite
        Assert.True(result.Success);
        
        var metadata = await _service.GetCachedCoverMetadataAsync(CoverType.Discovery, 12345, CoverSize.Medium);
        Assert.NotNull(metadata);
        Assert.Equal(CoverCacheSource.ComicVine, metadata.Source); // Should still be ComicVine
    }

    [Fact]
    public async Task DownloadExternalCoverAsync_OverwritesLowerPrioritySource()
    {
        // Arrange - First download a Metron cover (lower priority)
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        SetupHttpClient(HttpStatusCode.OK, imageBytes);

        await _service.DownloadExternalCoverAsync(
            "https://metron.cloud/cover.jpg",
            CoverType.Discovery,
            12345,
            CoverCacheSource.Metron,
            CoverSize.Medium);

        // Act - Download a ComicVine cover (higher priority)
        var result = await _service.DownloadExternalCoverAsync(
            "https://comicvine.com/cover.jpg",
            CoverType.Discovery,
            12345,
            CoverCacheSource.ComicVine,
            CoverSize.Medium);

        // Assert - Should overwrite with higher priority source
        Assert.True(result.Success);
        
        var metadata = await _service.GetCachedCoverMetadataAsync(CoverType.Discovery, 12345, CoverSize.Medium);
        Assert.NotNull(metadata);
        Assert.Equal(CoverCacheSource.ComicVine, metadata.Source);
        Assert.Contains("comicvine.com", metadata.SourceUrl);
    }

    [Fact]
    public async Task DownloadExternalCoverAsync_WithEmptyUrl_ReturnsNotFound()
    {
        // Act
        var result = await _service.DownloadExternalCoverAsync(
            "",
            CoverType.Discovery,
            12345,
            CoverCacheSource.Metron);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No URL", result.Error);
    }

    [Fact]
    public async Task GetCachedCoverMetadataAsync_WithNoCachedCover_ReturnsNull()
    {
        // Act
        var metadata = await _service.GetCachedCoverMetadataAsync(CoverType.Discovery, 99999, CoverSize.Medium);

        // Assert
        Assert.Null(metadata);
    }

    #endregion
}

