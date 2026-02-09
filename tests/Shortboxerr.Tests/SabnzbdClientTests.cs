using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class SabnzbdClientTests
{
    private readonly Mock<ILogger<SabnzbdClient>> _loggerMock;
    private readonly SabnzbdSettings _settings;

    public SabnzbdClientTests()
    {
        _loggerMock = new Mock<ILogger<SabnzbdClient>>();
        _settings = new SabnzbdSettings
        {
            Host = "http://localhost:8080",
            ApiKey = "test-api-key",
            Category = "comics"
        };
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    private SabnzbdClient CreateClient(HttpStatusCode statusCode, string jsonResponse)
    {
        var httpClient = CreateMockHttpClient(statusCode, jsonResponse);
        return new SabnzbdClient(httpClient, _settings, _loggerMock.Object);
    }

    #region TestConnection Tests

    [Fact]
    public async Task TestConnectionAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """{"version": "4.2.1"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("4.2.1", result.Version);
        Assert.Contains("SABnzbd 4.2.1", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_WithHttpError_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("failed", result.Message.ToLower());
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "not json");

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region AddNzb Tests

    [Fact]
    public async Task AddNzbUrlAsync_WithValidResponse_ReturnsDownloadId()
    {
        // Arrange
        var json = """{"status": true, "nzo_ids": ["SABnzbd_nzo_abc123"]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.AddNzbUrlAsync("http://example.com/test.nzb");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("SABnzbd_nzo_abc123", result.DownloadId);
    }

    [Fact]
    public async Task AddNzbUrlAsync_WithOptions_AppliesCorrectly()
    {
        // Arrange
        string? capturedUrl = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"status": true, "nzo_ids": ["nzo123"]}""", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new SabnzbdClient(httpClient, _settings, _loggerMock.Object);

        var options = new NzbDownloadOptions
        {
            Category = "test-category",
            Priority = NzbPriority.High,
            Name = "My Download"
        };

        // Act
        await client.AddNzbUrlAsync("http://example.com/test.nzb", options);

        // Assert
        Assert.NotNull(capturedUrl);
        Assert.Contains("cat=test-category", capturedUrl);
        Assert.Contains("priority=1", capturedUrl);
        Assert.Contains("nzbname=My", capturedUrl); // URL encoded
    }

    [Fact]
    public async Task AddNzbUrlAsync_WithError_ReturnsFailure()
    {
        // Arrange
        var json = """{"status": false, "error": "Invalid URL"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.AddNzbUrlAsync("http://invalid.com/bad.nzb");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid URL", result.ErrorMessage);
    }

    [Fact]
    public async Task AddNzbAsync_WithValidNzbContent_ReturnsDownloadId()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"status": true, "nzo_ids": ["nzo_xyz789"]}""", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new SabnzbdClient(httpClient, _settings, _loggerMock.Object);

        var nzbContent = Encoding.UTF8.GetBytes("<nzb></nzb>");

        // Act
        var result = await client.AddNzbAsync(nzbContent, "test.nzb");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("nzo_xyz789", result.DownloadId);
    }

    #endregion

    #region Queue Tests

    [Fact]
    public async Task GetQueueAsync_WithItems_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "queue": {
                    "paused": false,
                    "speed": "5.5 MB/s",
                    "noofslots": 2,
                    "slots": [
                        {
                            "nzo_id": "nzo_001",
                            "filename": "Batman 001.nzb",
                            "status": "Downloading",
                            "cat": "comics",
                            "size": "100 MB",
                            "sizeleft": "50 MB",
                            "timeleft": "0:05:30",
                            "priority": "Normal"
                        },
                        {
                            "nzo_id": "nzo_002",
                            "filename": "Batman 002.nzb",
                            "status": "Queued",
                            "cat": "comics",
                            "size": "120 MB",
                            "sizeleft": "120 MB",
                            "priority": "High"
                        }
                    ]
                }
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("nzo_001", result[0].Id);
        Assert.Equal("Batman 001.nzb", result[0].Name);
        Assert.Equal(NzbDownloadState.Downloading, result[0].State);
        Assert.Equal("comics", result[0].Category);
    }

    [Fact]
    public async Task GetQueueAsync_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var json = """{"queue": {"noofslots": 0, "slots": []}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task GetHistoryAsync_WithItems_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "history": {
                    "slots": [
                        {
                            "nzo_id": "nzo_h001",
                            "name": "Batman 001",
                            "status": "Completed",
                            "category": "comics",
                            "bytes": 104857600,
                            "completed": 1706789000,
                            "storage": "/downloads/comics/Batman 001"
                        },
                        {
                            "nzo_id": "nzo_h002",
                            "name": "Batman 002",
                            "status": "Failed",
                            "category": "comics",
                            "bytes": 0,
                            "fail_message": "Download verification failed"
                        }
                    ]
                }
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetHistoryAsync();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("nzo_h001", result[0].Id);
        Assert.Equal("Batman 001", result[0].Name);
        Assert.Equal(NzbDownloadState.Completed, result[0].State);
        Assert.Equal("/downloads/comics/Batman 001", result[0].DownloadPath);

        Assert.Equal("nzo_h002", result[1].Id);
        Assert.Equal(NzbDownloadState.Failed, result[1].State);
        Assert.Equal("Download verification failed", result[1].ErrorMessage);
    }

    #endregion

    #region Download Control Tests

    [Fact]
    public async Task PauseDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.PauseDownloadAsync("nzo_001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResumeDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ResumeDownloadAsync("nzo_001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.RemoveDownloadAsync("nzo_001");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SABnzbd-Specific Tests

    [Fact]
    public async Task GetCategoriesAsync_ReturnsList()
    {
        // Arrange
        var json = """{"categories": ["Default", "comics", "movies", "tv"]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetCategoriesAsync();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains("comics", result);
    }

    [Fact]
    public async Task GetScriptsAsync_ReturnsList()
    {
        // Arrange
        var json = """{"scripts": ["Default", "unrar.py", "notify.sh"]}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetScriptsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("unrar.py", result);
    }

    [Fact]
    public async Task PauseQueueAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.PauseQueueAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResumeQueueAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ResumeQueueAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetServerStatsAsync_ReturnsStats()
    {
        // Arrange
        var json = """
            {
                "queue": {
                    "paused": false,
                    "speed": "5500",
                    "noofslots": 3,
                    "sizeleft": "500 MB",
                    "timeleft": "0:15:00",
                    "speedlimit": "0"
                }
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetServerStatsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.QueueCount);
        Assert.False(result.IsPaused);
        Assert.Equal(0, result.SpeedLimitKbps);
    }

    [Fact]
    public async Task GetDiskSpaceAsync_ReturnsDiskInfo()
    {
        // Arrange
        var json = """
            {
                "queue": {
                    "diskspace1": "100 GB",
                    "diskspacetotal1": "500 GB",
                    "have_warnings": "false",
                    "download_dir": "/downloads"
                }
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetDiskSpaceAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/downloads", result.Path);
        Assert.False(result.IsLow);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsVersion()
    {
        // Arrange
        var json = """{"version": "4.2.1"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetVersionAsync();

        // Assert
        Assert.Equal("4.2.1", result);
    }

    #endregion

    #region ClientType Tests

    [Fact]
    public void ClientType_ReturnsSabnzbd()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "{}");

        // Act & Assert
        Assert.Equal(NzbDownloadClientType.SABnzbd, client.ClientType);
    }

    #endregion
}
