using Shortboxerr.Core.Torrent;
using Shortboxerr.Infrastructure.Torrent;
using Xunit;

namespace Shortboxerr.Tests;

public class DelugeClientTests
{
    #region DelugeSettings Tests

    [Fact]
    public void DelugeSettings_DefaultPort_Is8112()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.Equal(8112, settings.EffectivePort);
    }

    [Fact]
    public void DelugeSettings_CustomPort_IsUsed()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost",
            Port = 8113
        };

        Assert.Equal(8113, settings.EffectivePort);
    }

    [Fact]
    public void DelugeSettings_BaseUrl_CorrectFormat()
    {
        var settings = new DelugeSettings
        {
            Host = "192.168.1.100",
            Port = 8112
        };

        Assert.Equal("http://192.168.1.100:8112", settings.BaseUrl);
    }

    [Fact]
    public void DelugeSettings_BaseUrl_WithSsl()
    {
        var settings = new DelugeSettings
        {
            Host = "myserver.com",
            Port = 443,
            UseSsl = true
        };

        Assert.Equal("https://myserver.com:443", settings.BaseUrl);
    }

    [Fact]
    public void DelugeSettings_JsonRpcUrl_CorrectFormat()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.Equal("http://localhost:8112/json", settings.JsonRpcUrl);
    }

    [Fact]
    public void DelugeSettings_DefaultPassword_IsDeluge()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.Equal("deluge", settings.Password);
    }

    [Fact]
    public void DelugeSettings_DefaultTimeout_Is30Seconds()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.Equal(30, settings.TimeoutSeconds);
    }

    [Fact]
    public void DelugeSettings_DefaultAddPaused_IsFalse()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.False(settings.AddPaused);
    }

    [Fact]
    public void DelugeSettings_DefaultMoveCompleted_IsFalse()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.False(settings.MoveCompleted);
    }

    [Fact]
    public void DelugeSettings_DefaultUseSsl_IsFalse()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost"
        };

        Assert.False(settings.UseSsl);
    }

    #endregion

    #region DelugeSessionStatus Tests

    [Fact]
    public void DelugeSessionStatus_CanBeCreated()
    {
        var status = new DelugeSessionStatus
        {
            DownloadRateBps = 1024 * 1024,
            UploadRateBps = 512 * 1024,
            TotalDownloadedBytes = 10L * 1024 * 1024 * 1024,
            TotalUploadedBytes = 5L * 1024 * 1024 * 1024,
            NumDownloading = 3,
            NumSeeding = 5,
            NumTorrents = 8,
            DhtRunning = true,
            DhtNodes = 200,
            FreeDiskSpace = 100L * 1024 * 1024 * 1024
        };

        Assert.Equal(1024 * 1024, status.DownloadRateBps);
        Assert.Equal(3, status.NumDownloading);
        Assert.Equal(8, status.NumTorrents);
        Assert.True(status.DhtRunning);
        Assert.Equal(200, status.DhtNodes);
    }

    #endregion

    #region DelugeTorrentOptions Tests

    [Fact]
    public void DelugeTorrentOptions_CanBeCreated()
    {
        var options = new DelugeTorrentOptions
        {
            MaxDownloadSpeed = 1000,
            MaxUploadSpeed = 500,
            MaxConnections = 50,
            SequentialDownload = true,
            StopAtRatio = 2.0,
            RemoveAtRatio = true,
            MoveCompleted = true,
            MoveCompletedPath = "/completed"
        };

        Assert.Equal(1000, options.MaxDownloadSpeed);
        Assert.Equal(500, options.MaxUploadSpeed);
        Assert.True(options.SequentialDownload);
        Assert.Equal(2.0, options.StopAtRatio);
        Assert.True(options.MoveCompleted);
        Assert.Equal("/completed", options.MoveCompletedPath);
    }

    [Fact]
    public void DelugeTorrentOptions_AllPropertiesNullable()
    {
        var options = new DelugeTorrentOptions();

        Assert.Null(options.MaxDownloadSpeed);
        Assert.Null(options.MaxUploadSpeed);
        Assert.Null(options.MaxConnections);
        Assert.Null(options.MaxUploadSlots);
        Assert.Null(options.PrioritizeFirstLastPieces);
        Assert.Null(options.SequentialDownload);
        Assert.Null(options.StopAtRatio);
        Assert.Null(options.RemoveAtRatio);
        Assert.Null(options.MoveCompleted);
        Assert.Null(options.MoveCompletedPath);
        Assert.Null(options.AutoManaged);
    }

    #endregion

    #region DelugeConfig Tests

    [Fact]
    public void DelugeConfig_CanBeCreated()
    {
        var config = new DelugeConfig
        {
            DownloadLocation = "/downloads",
            MoveCompleted = true,
            MoveCompletedPath = "/completed",
            MaxDownloadSpeed = -1,
            MaxUploadSpeed = -1,
            MaxConnections = 200,
            MaxActiveDownloading = 5,
            MaxActiveSeeding = 10,
            MaxActiveLimit = 15,
            DhtEnabled = true,
            ListenPortStart = 6881,
            ListenPortEnd = 6891
        };

        Assert.Equal("/downloads", config.DownloadLocation);
        Assert.True(config.MoveCompleted);
        Assert.Equal(-1, config.MaxDownloadSpeed);
        Assert.Equal(200, config.MaxConnections);
        Assert.True(config.DhtEnabled);
        Assert.Equal(6881, config.ListenPortStart);
    }

    #endregion

    #region TorrentClientType Tests

    [Fact]
    public void TorrentClientType_Deluge_HasCorrectValue()
    {
        Assert.Equal(3, (int)TorrentClientType.Deluge);
    }

    #endregion

    #region Integration Pattern Tests

    [Fact]
    public void DelugeSettings_FollowsQBittorrentPattern()
    {
        var qbSettings = new QBittorrentSettings
        {
            Host = "localhost",
            Port = 8080,
            Username = "admin",
            Password = "password",
            UseSsl = false
        };

        var delugeSettings = new DelugeSettings
        {
            Host = "localhost",
            Port = 8112,
            Password = "deluge",
            UseSsl = false
        };

        Assert.Equal(qbSettings.Host, delugeSettings.Host);
        Assert.Equal(qbSettings.UseSsl, delugeSettings.UseSsl);
    }

    [Fact]
    public void DelugeSettings_FollowsTransmissionPattern()
    {
        var trSettings = new TransmissionSettings
        {
            Host = "localhost",
            Port = 9091,
            Username = "admin",
            Password = "password",
            UseSsl = false,
            DownloadDir = "/downloads"
        };

        var delugeSettings = new DelugeSettings
        {
            Host = "localhost",
            Port = 8112,
            Password = "deluge",
            UseSsl = false,
            DownloadPath = "/downloads"
        };

        Assert.Equal(trSettings.Host, delugeSettings.Host);
        Assert.Equal(trSettings.UseSsl, delugeSettings.UseSsl);
        Assert.Equal(trSettings.DownloadDir, delugeSettings.DownloadPath);
    }

    [Fact]
    public void DelugeSettings_HasLabel_ForCategorySupport()
    {
        var delugeSettings = new DelugeSettings
        {
            Host = "localhost",
            Label = "comics"
        };

        var qbSettings = new QBittorrentSettings
        {
            Host = "localhost",
            Category = "comics"
        };

        Assert.Equal(qbSettings.Category, delugeSettings.Label);
    }

    #endregion

    #region URL Construction Tests

    [Theory]
    [InlineData("localhost", null, false, "http://localhost:8112/json")]
    [InlineData("192.168.1.100", 8112, false, "http://192.168.1.100:8112/json")]
    [InlineData("myserver.local", 443, true, "https://myserver.local:443/json")]
    [InlineData("torrent.example.com", 8080, false, "http://torrent.example.com:8080/json")]
    public void DelugeSettings_JsonRpcUrl_VariousCombinations(string host, int? port, bool useSsl, string expectedUrl)
    {
        var settings = new DelugeSettings
        {
            Host = host,
            Port = port,
            UseSsl = useSsl
        };

        Assert.Equal(expectedUrl, settings.JsonRpcUrl);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void DelugeSettings_AllDefaults()
    {
        var settings = new DelugeSettings { Host = "test" };

        Assert.Equal("test", settings.Host);
        Assert.Null(settings.Port);
        Assert.Equal(8112, settings.EffectivePort);
        Assert.Equal("deluge", settings.Password);
        Assert.Null(settings.Label);
        Assert.Null(settings.DownloadPath);
        Assert.False(settings.UseSsl);
        Assert.Equal(30, settings.TimeoutSeconds);
        Assert.False(settings.AddPaused);
        Assert.False(settings.MoveCompleted);
        Assert.Null(settings.MoveCompletedPath);
    }

    [Fact]
    public void DelugeSessionStatus_AllDefaults()
    {
        var status = new DelugeSessionStatus();

        Assert.Equal(0, status.DownloadRateBps);
        Assert.Equal(0, status.UploadRateBps);
        Assert.Equal(0, status.TotalDownloadedBytes);
        Assert.Equal(0, status.TotalUploadedBytes);
        Assert.Equal(0, status.NumDownloading);
        Assert.Equal(0, status.NumSeeding);
        Assert.Equal(0, status.NumTorrents);
        Assert.False(status.DhtRunning);
        Assert.Equal(0, status.DhtNodes);
        Assert.Equal(0, status.FreeDiskSpace);
    }

    [Fact]
    public void DelugeConfig_AllDefaults()
    {
        var config = new DelugeConfig();

        Assert.Null(config.DownloadLocation);
        Assert.False(config.MoveCompleted);
        Assert.Null(config.MoveCompletedPath);
        Assert.Equal(0, config.MaxDownloadSpeed);
        Assert.Equal(0, config.MaxUploadSpeed);
        Assert.Equal(0, config.MaxConnections);
        Assert.Equal(0, config.MaxActiveDownloading);
        Assert.Equal(0, config.MaxActiveSeeding);
        Assert.Equal(0, config.MaxActiveLimit);
        Assert.False(config.DhtEnabled);
        Assert.Equal(0, config.ListenPortStart);
        Assert.Equal(0, config.ListenPortEnd);
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void DelugeAuthenticationException_HasMessage()
    {
        var ex = new DelugeAuthenticationException("Invalid password");

        Assert.Equal("Invalid password", ex.Message);
    }

    [Fact]
    public void DelugeRpcException_HasCodeAndMessage()
    {
        var ex = new DelugeRpcException("Method not found", -32601);

        Assert.Equal("Method not found", ex.Message);
        Assert.Equal(-32601, ex.ErrorCode);
    }

    #endregion

    #region Move Completed Settings Tests

    [Fact]
    public void DelugeSettings_MoveCompleted_WithPath()
    {
        var settings = new DelugeSettings
        {
            Host = "localhost",
            MoveCompleted = true,
            MoveCompletedPath = "/completed/comics"
        };

        Assert.True(settings.MoveCompleted);
        Assert.Equal("/completed/comics", settings.MoveCompletedPath);
    }

    [Fact]
    public void DelugeTorrentOptions_MoveCompleted_Settings()
    {
        var options = new DelugeTorrentOptions
        {
            MoveCompleted = true,
            MoveCompletedPath = "/completed"
        };

        Assert.True(options.MoveCompleted);
        Assert.Equal("/completed", options.MoveCompletedPath);
    }

    #endregion
}
