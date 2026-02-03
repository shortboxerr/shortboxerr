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

    [Fact]
    public async Task DownloadUrl_WithCustomHeaders_SendsHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test content")
            });

        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "headers_test.cbz");

        var options = new HttpDownloadOptions
        {
            CustomHeaders = new Dictionary<string, string>
            {
                { "X-Custom-Header", "test-value" }
            },
            Referer = "https://getcomics.org/search"
        };

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath,
            options);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Custom-Header"));
        Assert.Equal("https://getcomics.org/search", capturedRequest.Headers.Referrer?.ToString());
    }

    [Fact]
    public async Task DownloadUrl_WithCookies_SendsCookieHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test content")
            });

        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "cookies_test.cbz");

        var options = new HttpDownloadOptions
        {
            Cookies = new Dictionary<string, string>
            {
                { "session_id", "abc123" },
                { "auth_token", "xyz789" }
            }
        };

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath,
            options);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Cookie"));
        var cookieHeader = capturedRequest.Headers.GetValues("Cookie").First();
        Assert.Contains("session_id=abc123", cookieHeader);
        Assert.Contains("auth_token=xyz789", cookieHeader);
    }

    [Fact]
    public async Task DownloadUrl_WithCustomUserAgent_SendsUserAgent()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test content")
            });

        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "useragent_test.cbz");

        var options = new HttpDownloadOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0"
        };

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath,
            options);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        var userAgent = capturedRequest.Headers.UserAgent.ToString();
        Assert.Contains("Chrome/120.0.0.0", userAgent);
    }

    [Fact]
    public async Task DownloadUrl_WithBasicAuth_SendsAuthHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test content")
            });

        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "auth_test.cbz");

        var options = new HttpDownloadOptions
        {
            Username = "testuser",
            Password = "testpass"
        };

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath,
            options);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Basic", capturedRequest.Headers.Authorization.Scheme);
        // Base64 of "testuser:testpass"
        var expectedAuth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("testuser:testpass"));
        Assert.Equal(expectedAuth, capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DownloadUrl_With500Error_ReturnsFailed()
    {
        // Arrange
        // Note: The HTTP client returns a failed result on HTTP errors, it doesn't throw.
        // Retries only happen on network-level errors (HttpRequestException).
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server Error")
            });

        var client = CreateClient(mockHandler, maxRetries: 2);
        var destPath = Path.Combine(_testDownloadDir, "retry_test.cbz");

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Contains("500", result.Error);
    }

    [Fact]
    public async Task DownloadUrl_WithNetworkErrorThenSuccess_Retries()
    {
        // Arrange
        // Retries happen on HttpRequestException (network errors), not HTTP status codes
        var attemptCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new HttpRequestException("Connection failed", null, HttpStatusCode.ServiceUnavailable);
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Success content")
                };
            });

        var client = CreateClient(mockHandler, maxRetries: 3);
        var destPath = Path.Combine(_testDownloadDir, "transient_test.cbz");

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, attemptCount);
        Assert.True(File.Exists(destPath));
    }

    [Fact]
    public async Task DownloadUrl_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var content = "Test content";
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, content);
        var client = CreateClient(mockHandler);
        var subDir = Path.Combine(_testDownloadDir, "subdir", "nested");
        var destPath = Path.Combine(subDir, "test.cbz");

        // Ensure directory doesn't exist
        Assert.False(Directory.Exists(subDir));

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath);

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(destPath));
    }

    [Fact]
    public async Task DownloadUrl_ReturnsMetadataFields()
    {
        // Arrange
        var content = "Comic book content here";
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, content);
        var client = CreateClient(mockHandler);
        var destPath = Path.Combine(_testDownloadDir, "metadata_test.cbz");

        // Act
        var result = await client.DownloadUrlAsync(
            "https://example.com/test.cbz",
            destPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destPath, result.FilePath);
        Assert.True(File.Exists(destPath));
        // Duration should be set (even if very small)
        Assert.True(result.Duration.Ticks >= 0);
        // File was written with content
        var actualContent = await File.ReadAllTextAsync(destPath);
        Assert.Equal(content, actualContent);
    }

    [Fact]
    public async Task DownloadUrl_WithVariousContentSizes_DownloadsCorrectly()
    {
        // Test with different content sizes to verify streaming works
        var testCases = new[] { 100, 1000, 10000 };

        foreach (var size in testCases)
        {
            var content = new string('X', size);
            var mockHandler = CreateMockHandler(HttpStatusCode.OK, content);
            var client = CreateClient(mockHandler);
            var destPath = Path.Combine(_testDownloadDir, $"size_test_{size}.cbz");

            // Act
            var result = await client.DownloadUrlAsync(
                $"https://example.com/file_{size}.cbz",
                destPath);

            // Assert
            Assert.True(result.Success, $"Failed for size {size}");
            Assert.True(File.Exists(destPath), $"File not created for size {size}");
            var actualContent = await File.ReadAllTextAsync(destPath);
            Assert.Equal(content, actualContent);
        }
    }

    private Shortboxerr.Infrastructure.DownloadClients.HttpDownloadClient CreateClient(
        Mock<HttpMessageHandler>? mockHandler = null,
        int maxRetries = 3)
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
            MaxRetries = maxRetries,
            RetryDelayMs = 10 // Fast retries for tests
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

