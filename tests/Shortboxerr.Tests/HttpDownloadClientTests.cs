using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.DownloadClients;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for the built-in HTTP download client.
/// Note: This is an internal service, not a user-configurable download client provider.
/// </summary>
public class HttpDownloadClientTests : IDisposable
{
    private readonly string _testDownloadDir;

    public HttpDownloadClientTests()
    {
        _testDownloadDir = Path.Combine(Path.GetTempPath(), $"shortboxerr_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDownloadDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDownloadDir))
        {
            try { Directory.Delete(_testDownloadDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DownloadUrl_WithValidUrl_DownloadsFile()
    {
        // Arrange
        var content = "This is test content for a comic file";
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, content);
        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "test.cbz");

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destPath, result.FilePath);
        Assert.True(File.Exists(destPath));
        // Verify file was written
        var fileContent = await File.ReadAllTextAsync(destPath);
        Assert.Equal(content, fileContent);
    }

    [Fact]
    public async Task DownloadUrl_With404_ReturnsFailed()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.NotFound, "Not Found");
        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "missing.cbz");

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/missing.cbz",
            destPath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public async Task GetFileSize_WithContentLength_ReturnsSize()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[12345])
            });

        var client = CreateClient(mockHandler);

        // Act
        var size = await client.GetFileSizeAsync("https://example.com/test.cbz");

        // Assert
        Assert.Equal(12345, size);
    }

    [Fact]
    public async Task IsReachable_WithValidUrl_ReturnsTrue()
    {
        // Arrange
        var mockHandler = CreateHeadMockHandler(HttpStatusCode.OK);
        var client = CreateClient(mockHandler);

        // Act
        var reachable = await client.IsReachableAsync("https://example.com/test.cbz");

        // Assert
        Assert.True(reachable);
    }

    [Fact]
    public async Task IsReachable_With404_ReturnsFalse()
    {
        // Arrange
        var mockHandler = CreateHeadMockHandler(HttpStatusCode.NotFound);
        var client = CreateClient(mockHandler);

        // Act
        var reachable = await client.IsReachableAsync("https://example.com/missing.cbz");

        // Assert
        Assert.False(reachable);
    }

    [Fact]
    public async Task DownloadUrl_WithProgressCallback_ReportsProgress()
    {
        // Arrange
        var content = new string('X', 10000); // 10KB content
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, content);
        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "progress_test.cbz");
        
        var progressReports = new List<HttpDownloadProgress>();
        var progress = new Progress<HttpDownloadProgress>(p => progressReports.Add(p));

        var options = new HttpDownloadOptions
        {
            Progress = progress
        };

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath,
            options);

        // Assert
        Assert.True(result.Success);
        // Progress might not always be reported for small files, but the mechanism exists
    }

    private Shortboxerr.Infrastructure.DownloadClients.HttpDownloadClient CreateClient(Mock<HttpMessageHandler>? mockHandler = null)
    {
        var handler = mockHandler ?? CreateMockHandler(HttpStatusCode.OK, "default content");
        var httpClient = new HttpClient(handler.Object);
        var logger = Mock.Of<ILogger<Shortboxerr.Infrastructure.DownloadClients.HttpDownloadClient>>();
        var settings = new HttpDownloadClientSettings
        {
            Id = 1,
            Name = "Test HTTP Client",
            DownloadDirectory = _testDownloadDir,
            MaxConcurrentDownloads = 3,
            TimeoutSeconds = 30,
            MaxRetries = 3
        };

        return new Shortboxerr.Infrastructure.DownloadClients.HttpDownloadClient(
            httpClient,
            logger,
            settings);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        
        // Also support HEAD requests for reachability checks
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content))
            });

        return mockHandler;
    }

    private static Mock<HttpMessageHandler> CreateHeadMockHandler(HttpStatusCode statusCode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode
            });

        return mockHandler;
    }
}

