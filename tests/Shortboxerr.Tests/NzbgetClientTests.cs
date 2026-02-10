using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class NzbgetClientTests
{
    private readonly Mock<ILogger<NzbgetClient>> _loggerMock;
    private readonly NzbgetSettings _settings;

    public NzbgetClientTests()
    {
        _loggerMock = new Mock<ILogger<NzbgetClient>>();
        _settings = new NzbgetSettings
        {
            Host = "localhost",
            Port = 6789,
            Username = "nzbget",
            Password = "tegbzn6789",
            Category = "comics",
            UseSsl = false
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

    private NzbgetClient CreateClient(HttpStatusCode statusCode, string jsonResponse)
    {
        var httpClient = CreateMockHttpClient(statusCode, jsonResponse);
        return new NzbgetClient(httpClient, _settings, _loggerMock.Object);
    }

    #region TestConnection Tests

    [Fact]
    public async Task TestConnectionAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """{"result": "21.1", "id": 1, "jsonrpc": "2.0"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("21.1", result.Version);
        Assert.Contains("NZBGet 21.1", result.Message);
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
    public async Task TestConnectionAsync_WithRpcError_ReturnsFailure()
    {
        // Arrange
        var json = """{"error": {"code": 1, "message": "Authentication failed"}, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.TestConnectionAsync();

        // Assert
        Assert.False(result.Success);
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

    #region Version Tests

    [Fact]
    public async Task GetVersionAsync_ReturnsVersion()
    {
        // Arrange
        var json = """{"result": "21.1", "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetVersionAsync();

        // Assert
        Assert.Equal("21.1", result);
    }

    #endregion

    #region AddNzb Tests

    [Fact]
    public async Task AddNzbAsync_WithValidResponse_ReturnsDownloadId()
    {
        // Arrange
        var json = """{"result": 12345, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);
        var nzbContent = Encoding.UTF8.GetBytes("<nzb></nzb>");

        // Act
        var result = await client.AddNzbAsync(nzbContent, "test.nzb");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("12345", result.DownloadId);
    }

    [Fact]
    public async Task AddNzbAsync_WithOptions_AppliesCorrectly()
    {
        // Arrange
        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) => 
                capturedBody = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"result": 123, "id": 1}""", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NzbgetClient(httpClient, _settings, _loggerMock.Object);

        var options = new NzbDownloadOptions
        {
            Category = "test-category",
            Priority = NzbPriority.High,
            Name = "My Download"
        };

        var nzbContent = Encoding.UTF8.GetBytes("<nzb></nzb>");

        // Act
        await client.AddNzbAsync(nzbContent, "test.nzb", options);

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("append", capturedBody);
        Assert.Contains("test-category", capturedBody);
    }

    [Fact]
    public async Task AddNzbAsync_WithZeroResult_ReturnsFailure()
    {
        // Arrange
        var json = """{"result": 0, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);
        var nzbContent = Encoding.UTF8.GetBytes("<nzb></nzb>");

        // Act
        var result = await client.AddNzbAsync(nzbContent, "test.nzb");

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddNzbUrlAsync_WithValidUrl_ReturnsDownloadId()
    {
        // Arrange
        // First call for downloading NZB, second for adding
        var responseQueue = new Queue<string>();
        responseQueue.Enqueue("<nzb></nzb>"); // NZB content
        responseQueue.Enqueue("""{"result": 456, "id": 1}"""); // Append result

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var content = callCount == 1 ? "<nzb></nzb>" : """{"result": 456, "id": 1}""";
                var mediaType = callCount == 1 ? "application/x-nzb" : "application/json";
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(content, Encoding.UTF8, mediaType)
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NzbgetClient(httpClient, _settings, _loggerMock.Object);

        // Act
        var result = await client.AddNzbUrlAsync("http://example.com/test.nzb");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("456", result.DownloadId);
    }

    #endregion

    #region Queue Tests

    [Fact]
    public async Task GetQueueAsync_WithItems_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "result": [
                    {
                        "NZBID": 1001,
                        "NZBName": "Batman 001",
                        "Status": "DOWNLOADING",
                        "Category": "comics",
                        "FileSizeLo": 104857600,
                        "FileSizeHi": 0,
                        "RemainingSizeLo": 52428800,
                        "RemainingSizeHi": 0,
                        "PausedSizeLo": 0,
                        "PausedSizeHi": 0,
                        "FileCount": 50,
                        "RemainingFileCount": 25,
                        "RemainingParCount": 5,
                        "Priority": 0,
                        "DestDir": "/downloads/inter/batman",
                        "FinalDir": "",
                        "Health": 1000
                    },
                    {
                        "NZBID": 1002,
                        "NZBName": "Batman 002",
                        "Status": "QUEUED",
                        "Category": "comics",
                        "FileSizeLo": 125829120,
                        "FileSizeHi": 0,
                        "RemainingSizeLo": 125829120,
                        "RemainingSizeHi": 0,
                        "PausedSizeLo": 0,
                        "PausedSizeHi": 0,
                        "FileCount": 60,
                        "RemainingFileCount": 60,
                        "RemainingParCount": 6,
                        "Priority": 50,
                        "DestDir": "/downloads/inter/batman2",
                        "FinalDir": "",
                        "Health": 1000
                    }
                ],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("1001", result[0].Id);
        Assert.Equal("Batman 001", result[0].Name);
        Assert.Equal(NzbDownloadState.Downloading, result[0].State);
        Assert.Equal("comics", result[0].Category);

        Assert.Equal("1002", result[1].Id);
        Assert.Equal(NzbDownloadState.Queued, result[1].State);
        Assert.Equal(NzbPriority.High, result[1].Priority);
    }

    [Fact]
    public async Task GetQueueAsync_WithEmptyQueue_ReturnsEmptyList()
    {
        // Arrange
        var json = """{"result": [], "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetQueueAsync_WithPausedDownload_ReturnsPausedState()
    {
        // Arrange
        var json = """
            {
                "result": [{
                    "NZBID": 1003,
                    "NZBName": "Paused Download",
                    "Status": "PAUSED",
                    "Category": "comics",
                    "FileSizeLo": 1000000,
                    "FileSizeHi": 0,
                    "RemainingSizeLo": 500000,
                    "RemainingSizeHi": 0,
                    "PausedSizeLo": 0,
                    "PausedSizeHi": 0,
                    "FileCount": 10,
                    "RemainingFileCount": 5,
                    "RemainingParCount": 1,
                    "Priority": 0,
                    "DestDir": "/downloads",
                    "FinalDir": "",
                    "Health": 1000
                }],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(NzbDownloadState.Paused, result[0].State);
    }

    #endregion

    #region History Tests

    [Fact]
    public async Task GetHistoryAsync_WithItems_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "result": [
                    {
                        "NZBID": 2001,
                        "Name": "Batman 001",
                        "Status": "SUCCESS",
                        "Category": "comics",
                        "FileSizeLo": 104857600,
                        "FileSizeHi": 0,
                        "DestDir": "/downloads/inter/batman",
                        "FinalDir": "/downloads/completed/batman",
                        "HistoryTime": 1706789000,
                        "DownloadTimeSec": 120,
                        "PostTotalTimeSec": 30,
                        "ParStatus": "SUCCESS",
                        "UnpackStatus": "SUCCESS",
                        "ScriptStatus": "NONE"
                    },
                    {
                        "NZBID": 2002,
                        "Name": "Batman 002",
                        "Status": "FAILURE",
                        "Category": "comics",
                        "FileSizeLo": 0,
                        "FileSizeHi": 0,
                        "DestDir": "/downloads/inter/batman2",
                        "FinalDir": "",
                        "HistoryTime": 1706789100,
                        "DownloadTimeSec": 60,
                        "PostTotalTimeSec": 0,
                        "ParStatus": "FAILURE",
                        "UnpackStatus": "NONE",
                        "ScriptStatus": "NONE",
                        "StatusText": "Par check failed"
                    }
                ],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetHistoryAsync();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("2001", result[0].Id);
        Assert.Equal("Batman 001", result[0].Name);
        Assert.Equal(NzbDownloadState.Completed, result[0].State);
        Assert.Equal("/downloads/completed/batman", result[0].DownloadPath);

        Assert.Equal("2002", result[1].Id);
        Assert.Equal(NzbDownloadState.Failed, result[1].State);
        Assert.Equal("Par check failed", result[1].ErrorMessage);
    }

    [Fact]
    public async Task GetHistoryAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $$"""
            {
                "NZBID": {{i}},
                "Name": "Item {{i}}",
                "Status": "SUCCESS",
                "Category": "comics",
                "FileSizeLo": 1000000,
                "FileSizeHi": 0,
                "DestDir": "/downloads",
                "FinalDir": "/completed",
                "HistoryTime": 1706789000,
                "DownloadTimeSec": 60,
                "PostTotalTimeSec": 10,
                "ParStatus": "SUCCESS",
                "UnpackStatus": "SUCCESS",
                "ScriptStatus": "NONE"
            }
            """);
        var json = $$"""{"result": [{{string.Join(",", items)}}], "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetHistoryAsync(10);

        // Assert
        Assert.Equal(10, result.Count);
    }

    #endregion

    #region Download Control Tests

    [Fact]
    public async Task PauseDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.PauseDownloadAsync("1001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task PauseDownloadAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, """{"result": true, "id": 1}""");

        // Act
        var result = await client.PauseDownloadAsync("not-a-number");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResumeDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ResumeDownloadAsync("1001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveDownloadAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.RemoveDownloadAsync("1001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveDownloadAsync_WithDeleteFiles_UsesCorrectCommand()
    {
        // Arrange
        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedBody = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"result": true, "id": 1}""", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new NzbgetClient(httpClient, _settings, _loggerMock.Object);

        // Act
        await client.RemoveDownloadAsync("1001", deleteFiles: true);

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("GroupFinalDelete", capturedBody);
    }

    #endregion

    #region NZBGet-Specific Tests

    [Fact]
    public async Task GetCategoriesAsync_ReturnsList()
    {
        // Arrange
        var json = """
            {
                "result": [
                    {"Name": "Category1.Name", "Value": "comics"},
                    {"Name": "Category1.DestDir", "Value": "/downloads/comics"},
                    {"Name": "Category2.Name", "Value": "movies"},
                    {"Name": "Category2.DestDir", "Value": "/downloads/movies"},
                    {"Name": "Category3.Name", "Value": ""},
                    {"Name": "OtherSetting", "Value": "value"}
                ],
                "id": 1
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
    public async Task GetStatusAsync_ReturnsStatus()
    {
        // Arrange
        var json = """
            {
                "result": {
                    "RemainingSizeMB": 5000,
                    "ForcedSizeMB": 0,
                    "DownloadRate": 5500000,
                    "AverageDownloadRate": 4500000,
                    "DownloadLimit": 0,
                    "DownloadPaused": false,
                    "ThreadCount": 8,
                    "PostJobCount": 1,
                    "UpTimeSec": 86400,
                    "DownloadTimeSec": 3600,
                    "ServerStandBy": false,
                    "FreeDiskSpaceMB": 100000,
                    "NewsServers": 3
                },
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5000, result.RemainingSizeMB);
        Assert.False(result.DownloadPaused);
        Assert.Equal(8, result.ThreadCount);
        Assert.Equal(100000, result.FreeDiskSpaceMB);
    }

    [Fact]
    public async Task PauseQueueAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
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
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ResumeQueueAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SetSpeedLimitAsync_WithSpeed_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.SetSpeedLimitAsync(5000);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ReloadConfigAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ReloadConfigAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ScanAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.ScanAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task WriteLogAsync_WithSuccess_ReturnsTrue()
    {
        // Arrange
        var json = """{"result": true, "id": 1}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.WriteLogAsync("info", "Test message");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetDiskSpaceAsync_ReturnsDiskInfo()
    {
        // Arrange
        var json = """
            {
                "result": {
                    "RemainingSizeMB": 1000,
                    "ForcedSizeMB": 0,
                    "DownloadRate": 0,
                    "AverageDownloadRate": 0,
                    "DownloadLimit": 0,
                    "DownloadPaused": false,
                    "ThreadCount": 0,
                    "PostJobCount": 0,
                    "UpTimeSec": 86400,
                    "DownloadTimeSec": 0,
                    "ServerStandBy": true,
                    "FreeDiskSpaceMB": 50000,
                    "NewsServers": 2
                },
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetDiskSpaceAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50000L * 1024 * 1024, result.FreeBytes);
        Assert.False(result.IsLow);
    }

    [Fact]
    public async Task GetDiskSpaceAsync_WithLowSpace_SetsIsLow()
    {
        // Arrange
        var json = """
            {
                "result": {
                    "RemainingSizeMB": 0,
                    "FreeDiskSpaceMB": 500
                },
                "id": 1
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
    public void ClientType_ReturnsNzbget()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, "{}");

        // Act & Assert
        Assert.Equal(NzbDownloadClientType.NZBGet, client.ClientType);
    }

    #endregion

    #region Download Status Mapping Tests

    [Theory]
    [InlineData("QUEUED", NzbDownloadState.Queued)]
    [InlineData("PAUSED", NzbDownloadState.Paused)]
    [InlineData("DOWNLOADING", NzbDownloadState.Downloading)]
    [InlineData("FETCHING", NzbDownloadState.Downloading)]
    [InlineData("PP_QUEUED", NzbDownloadState.PostProcessing)]
    [InlineData("LOADING_PARS", NzbDownloadState.Verifying)]
    [InlineData("VERIFYING_SOURCES", NzbDownloadState.Verifying)]
    [InlineData("REPAIRING", NzbDownloadState.Repairing)]
    [InlineData("VERIFYING_REPAIRED", NzbDownloadState.Verifying)]
    [InlineData("RENAMING", NzbDownloadState.PostProcessing)]
    [InlineData("UNPACKING", NzbDownloadState.Extracting)]
    [InlineData("MOVING", NzbDownloadState.PostProcessing)]
    [InlineData("EXECUTING_SCRIPT", NzbDownloadState.PostProcessing)]
    public async Task GetQueueAsync_MapsStatusCorrectly(string nzbgetStatus, NzbDownloadState expectedState)
    {
        // Arrange
        var json = $$"""
            {
                "result": [{
                    "NZBID": 1,
                    "NZBName": "Test",
                    "Status": "{{nzbgetStatus}}",
                    "Category": "",
                    "FileSizeLo": 1000,
                    "FileSizeHi": 0,
                    "RemainingSizeLo": 500,
                    "RemainingSizeHi": 0,
                    "PausedSizeLo": 0,
                    "PausedSizeHi": 0,
                    "FileCount": 1,
                    "RemainingFileCount": 1,
                    "RemainingParCount": 0,
                    "Priority": 0,
                    "DestDir": "",
                    "FinalDir": "",
                    "Health": 1000
                }],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetQueueAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedState, result[0].State);
    }

    [Theory]
    [InlineData("SUCCESS", NzbDownloadState.Completed)]
    [InlineData("FAILURE", NzbDownloadState.Failed)]
    [InlineData("DELETED", NzbDownloadState.Deleted)]
    [InlineData("DUPE", NzbDownloadState.Deleted)]
    [InlineData("BAD", NzbDownloadState.Failed)]
    [InlineData("GOOD", NzbDownloadState.Completed)]
    [InlineData("COPY", NzbDownloadState.Completed)]
    [InlineData("SCAN", NzbDownloadState.Completed)]
    [InlineData("MARK/GOOD", NzbDownloadState.Completed)]
    [InlineData("MARK/BAD", NzbDownloadState.Failed)]
    public async Task GetHistoryAsync_MapsStatusCorrectly(string nzbgetStatus, NzbDownloadState expectedState)
    {
        // Arrange
        var json = $$"""
            {
                "result": [{
                    "NZBID": 1,
                    "Name": "Test",
                    "Status": "{{nzbgetStatus}}",
                    "Category": "",
                    "FileSizeLo": 1000,
                    "FileSizeHi": 0,
                    "DestDir": "",
                    "FinalDir": "",
                    "HistoryTime": 1706789000,
                    "DownloadTimeSec": 60,
                    "PostTotalTimeSec": 10,
                    "ParStatus": "NONE",
                    "UnpackStatus": "NONE",
                    "ScriptStatus": "NONE"
                }],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetHistoryAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedState, result[0].State);
    }

    #endregion

    #region GetDownloadStatusAsync Tests

    [Fact]
    public async Task GetDownloadStatusAsync_InQueue_ReturnsStatus()
    {
        // Arrange
        var json = """
            {
                "result": [{
                    "NZBID": 5001,
                    "NZBName": "Queue Item",
                    "Status": "DOWNLOADING",
                    "Category": "comics",
                    "FileSizeLo": 100000000,
                    "FileSizeHi": 0,
                    "RemainingSizeLo": 50000000,
                    "RemainingSizeHi": 0,
                    "PausedSizeLo": 0,
                    "PausedSizeHi": 0,
                    "FileCount": 50,
                    "RemainingFileCount": 25,
                    "RemainingParCount": 5,
                    "Priority": 0,
                    "DestDir": "/downloads/queue",
                    "FinalDir": "",
                    "Health": 1000
                }],
                "id": 1
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        // Act
        var result = await client.GetDownloadStatusAsync("5001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("5001", result.Id);
        Assert.Equal("Queue Item", result.Name);
        Assert.Equal(NzbDownloadState.Downloading, result.State);
    }

    [Fact]
    public async Task GetDownloadStatusAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var client = CreateClient(HttpStatusCode.OK, """{"result": [], "id": 1}""");

        // Act
        var result = await client.GetDownloadStatusAsync("invalid-id");

        // Assert
        Assert.Null(result);
    }

    #endregion
}

/// <summary>
/// Tests for NzbgetSettings computation and port handling.
/// </summary>
public class NzbgetSettingsTests
{
    #region EffectivePort Tests

    [Fact]
    public void EffectivePort_WithNoPort_ReturnsDefault6789()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.Equal(6789, settings.EffectivePort);
    }

    [Fact]
    public void EffectivePort_WithCustomPort_ReturnsCustomPort()
    {
        var settings = new NzbgetSettings { Host = "localhost", Port = 8080, Username = "user", Password = "pass" };
        Assert.Equal(8080, settings.EffectivePort);
    }

    #endregion

    #region BaseUrl Tests

    [Fact]
    public void BaseUrl_WithHostAndDefaultPort_ReturnsCorrectUrl()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.Equal("http://localhost:6789", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithCustomPort_ReturnsCorrectUrl()
    {
        var settings = new NzbgetSettings { Host = "localhost", Port = 8080, Username = "user", Password = "pass" };
        Assert.Equal("http://localhost:8080", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithSsl_ReturnsHttps()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass", UseSsl = true };
        Assert.Equal("https://localhost:6789", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithSslAndCustomPort_ReturnsHttpsWithPort()
    {
        var settings = new NzbgetSettings { Host = "localhost", Port = 8443, Username = "user", Password = "pass", UseSsl = true };
        Assert.Equal("https://localhost:8443", settings.BaseUrl);
    }

    [Fact]
    public void BaseUrl_WithIpAddress_WorksCorrectly()
    {
        var settings = new NzbgetSettings { Host = "192.168.1.100", Port = 6789, Username = "user", Password = "pass" };
        Assert.Equal("http://192.168.1.100:6789", settings.BaseUrl);
    }

    #endregion

    #region JsonRpcUrl Tests

    [Fact]
    public void JsonRpcUrl_IncludesCredentials()
    {
        var settings = new NzbgetSettings 
        { 
            Host = "localhost", 
            Port = 6789, 
            Username = "nzbget", 
            Password = "tegbzn6789" 
        };
        Assert.Equal("http://localhost:6789/nzbget:tegbzn6789/jsonrpc", settings.JsonRpcUrl);
    }

    [Fact]
    public void JsonRpcUrl_WithSsl_UsesHttps()
    {
        var settings = new NzbgetSettings 
        { 
            Host = "localhost", 
            Port = 6790, 
            Username = "admin", 
            Password = "secret",
            UseSsl = true
        };
        Assert.Equal("https://localhost:6790/admin:secret/jsonrpc", settings.JsonRpcUrl);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Category_DefaultsToComics()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.Equal("comics", settings.Category);
    }

    [Fact]
    public void DefaultPriority_DefaultsToNormal()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.Equal(NzbgetPriority.Normal, settings.DefaultPriority);
    }

    [Fact]
    public void UseSsl_DefaultsToFalse()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.False(settings.UseSsl);
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.Equal(30, settings.TimeoutSeconds);
    }

    [Fact]
    public void AddPaused_DefaultsToFalse()
    {
        var settings = new NzbgetSettings { Host = "localhost", Username = "user", Password = "pass" };
        Assert.False(settings.AddPaused);
    }

    #endregion
}

/// <summary>
/// Tests for NzbgetPriority enum values.
/// </summary>
public class NzbgetPriorityTests
{
    [Fact]
    public void VeryLow_HasCorrectValue()
    {
        Assert.Equal(-100, (int)NzbgetPriority.VeryLow);
    }

    [Fact]
    public void Low_HasCorrectValue()
    {
        Assert.Equal(-50, (int)NzbgetPriority.Low);
    }

    [Fact]
    public void Normal_HasCorrectValue()
    {
        Assert.Equal(0, (int)NzbgetPriority.Normal);
    }

    [Fact]
    public void High_HasCorrectValue()
    {
        Assert.Equal(50, (int)NzbgetPriority.High);
    }

    [Fact]
    public void VeryHigh_HasCorrectValue()
    {
        Assert.Equal(100, (int)NzbgetPriority.VeryHigh);
    }

    [Fact]
    public void Force_HasCorrectValue()
    {
        Assert.Equal(900, (int)NzbgetPriority.Force);
    }
}
