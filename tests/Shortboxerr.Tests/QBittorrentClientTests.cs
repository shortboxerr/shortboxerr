using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Torrent;
using Shortboxerr.Infrastructure.Torrent;
using Xunit;

namespace Shortboxerr.Tests;

public class QBittorrentClientTests
{
    private readonly Mock<ILogger<QBittorrentClient>> _loggerMock;
    private readonly QBittorrentSettings _settings;

    public QBittorrentClientTests()
    {
        _loggerMock = new Mock<ILogger<QBittorrentClient>>();
        _settings = new QBittorrentSettings
        {
            Host = "localhost",
            Port = 8080,
            Username = "admin",
            Password = "admin",
            Category = "comics",
            UseSsl = false
        };
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content, Action<HttpRequestMessage>? requestCallback = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => requestCallback?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    private HttpClient CreateSequentialMockHttpClient(Queue<(HttpStatusCode, string)> responses)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var (status, content) = responses.Dequeue();
                return new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
            });

        return new HttpClient(handlerMock.Object);
    }

    private QBittorrentClient CreateClient(HttpStatusCode statusCode, string jsonResponse)
    {
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.OK, "Ok.")); // Auth response
        responses.Enqueue((statusCode, jsonResponse));
        var httpClient = CreateSequentialMockHttpClient(responses);
        return new QBittorrentClient(httpClient, _settings, _loggerMock.Object);
    }

    #region TestConnection Tests

    [Fact]
    public async Task TestConnectionAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.OK, "Ok.")); // Auth
        responses.Enqueue((HttpStatusCode.OK, "v4.6.1")); // Version
        var httpClient = CreateSequentialMockHttpClient(responses);
        var client = new QBittorrentClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("v4.6.1", result.Version);
        Assert.Contains("qBittorrent", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_WithHttpError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "");
        var client = new QBittorrentClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("failed", result.Message.ToLower());
    }

    [Fact]
    public async Task TestConnectionAsync_WithAuthFailure_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "Fails.");
        var client = new QBittorrentClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region Version Tests

    [Fact]
    public async Task GetVersionAsync_ReturnsVersion()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "v4.6.1");

        // Act
        var result = await client.GetVersionAsync();

        // Assert
        Assert.Equal("v4.6.1", result);
    }

    [Fact]
    public async Task GetApiVersionAsync_ReturnsApiVersion()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "2.9.3");

        // Act
        var result = await client.GetApiVersionAsync();

        // Assert
        Assert.Equal("2.9.3", result);
    }

    #endregion

    #region AddTorrent Tests

    [Fact]
    public async Task AddTorrentMagnetAsync_WithValidMagnet_ReturnsSuccess()
    {
        // Arrange
        var magnet = "magnet:?xt=urn:btih:abc123def456&dn=Test+Torrent";
        var client = CreateClient(HttpStatusCode.OK, "Ok.");

        // Act
        var result = await client.AddTorrentMagnetAsync(magnet);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("abc123def456", result.Hash);
    }

    [Fact]
    public async Task AddTorrentMagnetAsync_WithOptions_AppliesCorrectly()
    {
        // Arrange
        string? capturedContent = null;
        var responses = new Queue<(HttpStatusCode, string)>();
        responses.Enqueue((HttpStatusCode.OK, "Ok.")); // Auth
        
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                if (req.Content is MultipartFormDataContent multipart)
                {
                    capturedContent = await multipart.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Ok.", Encoding.UTF8, "text/plain")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new QBittorrentClient(httpClient, _settings, _loggerMock.Object);

        var options = new TorrentAddOptions
        {
            Category = "test-category",
            AddPaused = true,
            SequentialDownload = true
        };

        // Act
        await client.AddTorrentMagnetAsync("magnet:?xt=urn:btih:abc123", options);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("test-category", capturedContent);
    }

    [Fact]
    public async Task AddTorrentUrlAsync_WithValidUrl_ReturnsSuccess()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "Ok.");

        // Act
        var result = await client.AddTorrentUrlAsync("http://example.com/test.torrent");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddTorrentFileAsync_WithValidContent_ReturnsSuccess()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "Ok.");
        var torrentContent = Encoding.UTF8.GetBytes("d8:announce...e");

        // Act
        var result = await client.AddTorrentFileAsync(torrentContent, "test.torrent");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddTorrentMagnetAsync_WithInvalidResponse_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "Fails.");

        // Act
        var result = await client.AddTorrentMagnetAsync("magnet:?xt=urn:btih:abc123");

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region GetTorrents Tests

    [Fact]
    public async Task GetAllTorrentsAsync_WithItems_ReturnsList()
    {
        // Arrange
        var json = """
            [
                {
                    "hash": "abc123",
                    "name": "Batman 001",
                    "state": "downloading",
                    "category": "comics",
                    "total_size": 104857600,
                    "downloaded": 52428800,
                    "uploaded": 10485760,
                    "dlspeed": 5500000,
                    "upspeed": 550000,
                    "num_seeds": 10,
                    "num_leechs": 5,
                    "ratio": 0.2,
                    "eta": 300,
                    "save_path": "/downloads/comics",
                    "added_on": 1706789000,
                    "progress": 0.5
                },
                {
                    "hash": "def456",
                    "name": "Batman 002",
                    "state": "uploading",
                    "category": "comics",
                    "total_size": 125829120,
                    "downloaded": 125829120,
                    "uploaded": 251658240,
                    "dlspeed": 0,
                    "upspeed": 1100000,
                    "num_seeds": 0,
                    "num_leechs": 15,
                    "ratio": 2.0,
                    "save_path": "/downloads/comics",
                    "added_on": 1706789100,
                    "completion_on": 1706789500,
                    "progress": 1.0
                }
            ]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetAllTorrentsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("abc123", result[0].Hash);
        Assert.Equal("Batman 001", result[0].Name);
        Assert.Equal(TorrentState.Downloading, result[0].State);
        Assert.Equal("comics", result[0].Category);
        Assert.Equal(50, Math.Round(result[0].Progress));

        Assert.Equal("def456", result[1].Hash);
        Assert.Equal(TorrentState.Seeding, result[1].State);
        Assert.Equal(2.0, result[1].Ratio);
    }

    [Fact]
    public async Task GetAllTorrentsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "[]");

        // Act
        var result = await client.GetAllTorrentsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStatusAsync_WithValidHash_ReturnsTorrent()
    {
        // Arrange
        var json = """
            [{
                "hash": "abc123",
                "name": "Test Torrent",
                "state": "downloading",
                "category": "comics",
                "total_size": 100000000,
                "downloaded": 50000000,
                "dlspeed": 1000000,
                "save_path": "/downloads"
            }]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetStatusAsync("abc123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("abc123", result.Hash);
        Assert.Equal("Test Torrent", result.Name);
        Assert.Equal(TorrentState.Downloading, result.State);
    }

    [Fact]
    public async Task GetStatusAsync_WithUnknownHash_ReturnsNull()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "[]");

        // Act
        var result = await client.GetStatusAsync("unknown");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Download Control Tests

    [Fact]
    public async Task PauseTorrentAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.PauseTorrentAsync("abc123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResumeTorrentAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.ResumeTorrentAsync("abc123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveTorrentAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.RemoveTorrentAsync("abc123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveTorrentAsync_WithDeleteFiles_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.RemoveTorrentAsync("abc123", deleteFiles: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task PauseAllAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.PauseAllAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResumeAllAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.ResumeAllAsync();

        // Assert
        Assert.True(result);
    }

    #endregion

    #region qBittorrent-Specific Tests

    [Fact]
    public async Task GetCategoriesAsync_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "comics": {"name": "comics", "savePath": "/downloads/comics"},
                "movies": {"name": "movies", "savePath": "/downloads/movies"}
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetCategoriesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("comics", result);
        Assert.Contains("movies", result);
    }

    [Fact]
    public async Task CreateCategoryAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.CreateCategoryAsync("test-category", "/downloads/test");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetTransferInfoAsync_ReturnsInfo()
    {
        // Arrange
        var json = """
            {
                "dl_info_speed": 5500000,
                "up_info_speed": 1100000,
                "dl_rate_limit": 0,
                "up_rate_limit": 0,
                "dl_info_data": 10737418240,
                "up_info_data": 2147483648,
                "alltime_dl": 107374182400,
                "alltime_ul": 53687091200,
                "connection_status": "connected",
                "dht_nodes": 450,
                "free_space_on_disk": 536870912000
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetTransferInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5500000, result.DownloadSpeedBps);
        Assert.Equal(1100000, result.UploadSpeedBps);
        Assert.Equal("connected", result.ConnectionStatus);
        Assert.Equal(450, result.DhtNodes);
    }

    [Fact]
    public async Task SetDownloadLimitAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.SetDownloadLimitAsync(10000000);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SetUploadLimitAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.SetUploadLimitAsync(5000000);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RecheckTorrentAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.RecheckTorrentAsync("abc123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ForceStartAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.ForceStartAsync("abc123");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SetCategoryAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.SetCategoryAsync("abc123", "new-category");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SetPriorityAsync_TopPriority_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "");

        // Act
        var result = await client.SetPriorityAsync("abc123", QBittorrentPriority.TopPriority);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetDiskSpaceAsync_ReturnsDiskInfo()
    {
        // Arrange
        var json = """
            {
                "free_space_on_disk": 536870912000
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetDiskSpaceAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(536870912000, result.FreeBytes);
        Assert.False(result.IsLow);
    }

    [Fact]
    public async Task GetDiskSpaceAsync_WithLowSpace_SetsIsLow()
    {
        // Arrange
        var json = """
            {
                "free_space_on_disk": 500000000
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetDiskSpaceAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsLow);
    }

    #endregion

    #region ClientType Tests

    [Fact]
    public void ClientType_ReturnsQBittorrent()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var client = new QBittorrentClient(httpClient, _settings, _loggerMock.Object);

        // Act & Assert
        Assert.Equal(TorrentClientType.QBittorrent, client.ClientType);
    }

    #endregion

    #region State Mapping Tests

    [Theory]
    [InlineData("error", TorrentState.Error)]
    [InlineData("missingFiles", TorrentState.Error)]
    [InlineData("uploading", TorrentState.Seeding)]
    [InlineData("pausedUP", TorrentState.Paused)]
    [InlineData("queuedUP", TorrentState.Queued)]
    [InlineData("stalledUP", TorrentState.Seeding)]
    [InlineData("checkingUP", TorrentState.Checking)]
    [InlineData("forcedUP", TorrentState.Seeding)]
    [InlineData("allocating", TorrentState.Queued)]
    [InlineData("downloading", TorrentState.Downloading)]
    [InlineData("metaDL", TorrentState.FetchingMetadata)]
    [InlineData("pausedDL", TorrentState.Paused)]
    [InlineData("queuedDL", TorrentState.Queued)]
    [InlineData("stalledDL", TorrentState.Stalled)]
    [InlineData("checkingDL", TorrentState.Checking)]
    [InlineData("forcedDL", TorrentState.Downloading)]
    [InlineData("checkingResumeData", TorrentState.Checking)]
    [InlineData("moving", TorrentState.Moving)]
    [InlineData("unknown_state", TorrentState.Unknown)]
    public async Task GetAllTorrentsAsync_MapsStateCorrectly(string qbtState, TorrentState expectedState)
    {
        // Arrange
        var json = $$"""
            [{
                "hash": "abc123",
                "name": "Test",
                "state": "{{qbtState}}",
                "category": "",
                "total_size": 1000,
                "downloaded": 500,
                "save_path": ""
            }]
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetAllTorrentsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedState, result[0].State);
    }

    #endregion

    #region Magnet Hash Extraction Tests

    [Theory]
    [InlineData("magnet:?xt=urn:btih:ABC123DEF456&dn=Test", "abc123def456")]
    [InlineData("magnet:?xt=urn:btih:abc123def456", "abc123def456")]
    [InlineData("magnet:?xt=urn:btih:ABC123&tr=http://tracker.com", "abc123")]
    public async Task AddTorrentMagnetAsync_ExtractsHashCorrectly(string magnet, string expectedHash)
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "Ok.");

        // Act
        var result = await client.AddTorrentMagnetAsync(magnet);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedHash, result.Hash);
    }

    #endregion
}

/// <summary>
/// Tests for QBittorrentSettings computation and port handling.
/// </summary>
public class QBittorrentSettingsTests
{
    #region EffectivePort Tests

    [Fact]
    public void EffectivePort_WithNoPort_ReturnsDefault8080()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.Equal(8080, settings.EffectivePort);
    }

    [Fact]
    public void EffectivePort_WithCustomPort_ReturnsCustomPort()
    {
        var settings = new QBittorrentSettings { Host = "localhost", Port = 9090 };
        Assert.Equal(9090, settings.EffectivePort);
    }

    #endregion

    #region BaseUrl Tests

    [Fact]
    public void BaseUrl_WithHostAndDefaultPort_ReturnsCorrectUrl()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.Equal("http://localhost:8080", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithCustomPort_ReturnsCorrectUrl()
    {
        var settings = new QBittorrentSettings { Host = "localhost", Port = 9090 };
        Assert.Equal("http://localhost:9090", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithSsl_ReturnsHttps()
    {
        var settings = new QBittorrentSettings { Host = "localhost", UseSsl = true };
        Assert.Equal("https://localhost:8080", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithSslAndCustomPort_ReturnsHttpsWithPort()
    {
        var settings = new QBittorrentSettings { Host = "localhost", Port = 8443, UseSsl = true };
        Assert.Equal("https://localhost:8443", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithIpAddress_WorksCorrectly()
    {
        var settings = new QBittorrentSettings { Host = "192.168.1.100", Port = 8080 };
        Assert.Equal("http://192.168.1.100:8080", settings.BaseUrl);
    }

    #endregion

    #region ApiUrl Tests

    [Fact]
    public void ApiUrl_ReturnsCorrectPath()
    {
        var settings = new QBittorrentSettings { Host = "localhost", Port = 8080 };
        Assert.Equal("http://localhost:8080/api/v2", settings.ApiUrl);
    }

    [Fact]
    public void ApiUrl_WithSsl_ReturnsHttpsPath()
    {
        var settings = new QBittorrentSettings { Host = "localhost", UseSsl = true };
        Assert.Equal("https://localhost:8080/api/v2", settings.ApiUrl);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Category_DefaultsToComics()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.Equal("comics", settings.Category);
    }

    [Fact]
    public void UseSsl_DefaultsToFalse()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.False(settings.UseSsl);
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.Equal(30, settings.TimeoutSeconds);
    }

    [Fact]
    public void AddPaused_DefaultsToFalse()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.False(settings.AddPaused);
    }

    [Fact]
    public void SequentialDownload_DefaultsToFalse()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.False(settings.SequentialDownload);
    }

    [Fact]
    public void FirstLastPiecePriority_DefaultsToFalse()
    {
        var settings = new QBittorrentSettings { Host = "localhost" };
        Assert.False(settings.FirstLastPiecePriority);
    }

    #endregion
}

/// <summary>
/// Tests for TorrentPriority enum values.
/// </summary>
public class TorrentPriorityTests
{
    [Fact]
    public void Low_HasCorrectValue()
    {
        Assert.Equal(0, (int)TorrentPriority.Low);
    }

    [Fact]
    public void Normal_HasCorrectValue()
    {
        Assert.Equal(1, (int)TorrentPriority.Normal);
    }

    [Fact]
    public void High_HasCorrectValue()
    {
        Assert.Equal(2, (int)TorrentPriority.High);
    }

    [Fact]
    public void Force_HasCorrectValue()
    {
        Assert.Equal(3, (int)TorrentPriority.Force);
    }
}
