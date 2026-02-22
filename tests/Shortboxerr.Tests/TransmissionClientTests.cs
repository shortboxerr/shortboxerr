using Shortboxerr.Core.Torrent;
using Xunit;

namespace Shortboxerr.Tests;

public class TransmissionClientTests
{
    #region TransmissionSettings Tests

    [Fact]
    public void TransmissionSettings_DefaultPort_Is9091()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost"
        };

        Assert.Equal(9091, settings.EffectivePort);
    }

    [Fact]
    public void TransmissionSettings_CustomPort_IsUsed()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost",
            Port = 9092
        };

        Assert.Equal(9092, settings.EffectivePort);
    }

    [Fact]
    public void TransmissionSettings_RpcUrl_CorrectFormat()
    {
        var settings = new TransmissionSettings
        {
            Host = "192.168.1.100",
            Port = 9091
        };

        Assert.Equal("http://192.168.1.100:9091/transmission/rpc", settings.RpcUrl);
    }

    [Fact]
    public void TransmissionSettings_RpcUrl_WithSsl()
    {
        var settings = new TransmissionSettings
        {
            Host = "myserver.com",
            Port = 443,
            UseSsl = true
        };

        Assert.Equal("https://myserver.com:443/transmission/rpc", settings.RpcUrl);
    }

    [Fact]
    public void TransmissionSettings_RpcUrl_CustomPath()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost",
            RpcPath = "/custom/rpc"
        };

        Assert.Equal("http://localhost:9091/custom/rpc", settings.RpcUrl);
    }

    [Fact]
    public void TransmissionSettings_RpcUrl_PathWithoutLeadingSlash()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost",
            RpcPath = "api/transmission"
        };

        Assert.Equal("http://localhost:9091/api/transmission", settings.RpcUrl);
    }

    [Fact]
    public void TransmissionSettings_DefaultTimeout_Is30Seconds()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost"
        };

        Assert.Equal(30, settings.TimeoutSeconds);
    }

    [Fact]
    public void TransmissionSettings_DefaultAddPaused_IsFalse()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost"
        };

        Assert.False(settings.AddPaused);
    }

    [Fact]
    public void TransmissionSettings_DefaultRpcPath_IsCorrect()
    {
        var settings = new TransmissionSettings
        {
            Host = "localhost"
        };

        Assert.Equal("/transmission/rpc", settings.RpcPath);
    }

    #endregion

    #region TransmissionSessionInfo Tests

    [Fact]
    public void TransmissionSessionInfo_CanBeCreated()
    {
        var info = new TransmissionSessionInfo
        {
            Version = "3.00",
            RpcVersion = 17,
            RpcVersionMinimum = 14,
            DownloadDir = "/downloads",
            SpeedLimitDownKBps = 1000,
            SpeedLimitDownEnabled = true,
            SpeedLimitUpKBps = 500,
            SpeedLimitUpEnabled = true,
            SeedRatioLimit = 2.0,
            SeedRatioLimited = true
        };

        Assert.Equal("3.00", info.Version);
        Assert.Equal(17, info.RpcVersion);
        Assert.Equal("/downloads", info.DownloadDir);
        Assert.Equal(1000, info.SpeedLimitDownKBps);
        Assert.True(info.SpeedLimitDownEnabled);
        Assert.Equal(2.0, info.SeedRatioLimit);
    }

    #endregion

    #region TransmissionSessionStats Tests

    [Fact]
    public void TransmissionSessionStats_CanBeCreated()
    {
        var stats = new TransmissionSessionStats
        {
            ActiveTorrentCount = 5,
            PausedTorrentCount = 2,
            TorrentCount = 7,
            DownloadSpeedBps = 1024 * 1024, // 1 MB/s
            UploadSpeedBps = 512 * 1024, // 512 KB/s
            CurrentStats = new TransmissionCumulativeStats
            {
                DownloadedBytes = 10L * 1024 * 1024 * 1024, // 10 GB
                UploadedBytes = 5L * 1024 * 1024 * 1024, // 5 GB
                FilesAdded = 100,
                SessionCount = 1,
                SecondsActive = 3600
            }
        };

        Assert.Equal(5, stats.ActiveTorrentCount);
        Assert.Equal(7, stats.TorrentCount);
        Assert.Equal(1024 * 1024, stats.DownloadSpeedBps);
        Assert.NotNull(stats.CurrentStats);
        Assert.Equal(100, stats.CurrentStats!.FilesAdded);
    }

    #endregion

    #region TransmissionCumulativeStats Tests

    [Fact]
    public void TransmissionCumulativeStats_CanBeCreated()
    {
        var stats = new TransmissionCumulativeStats
        {
            DownloadedBytes = 100L * 1024 * 1024 * 1024,
            UploadedBytes = 50L * 1024 * 1024 * 1024,
            FilesAdded = 1000,
            SessionCount = 100,
            SecondsActive = 86400 * 30 // 30 days
        };

        Assert.Equal(100L * 1024 * 1024 * 1024, stats.DownloadedBytes);
        Assert.Equal(1000, stats.FilesAdded);
        Assert.Equal(100, stats.SessionCount);
    }

    #endregion

    #region TorrentClientType Tests

    [Fact]
    public void TorrentClientType_Transmission_HasCorrectValue()
    {
        Assert.Equal(2, (int)TorrentClientType.Transmission);
    }

    #endregion

    #region Integration Pattern Tests

    [Fact]
    public void TransmissionSettings_FollowsQBittorrentPattern()
    {
        // Verify Transmission settings follow same pattern as qBittorrent
        var qbSettings = new QBittorrentSettings
        {
            Host = "localhost",
            Port = 8080,
            Username = "admin",
            Password = "password",
            UseSsl = false
        };

        var trSettings = new TransmissionSettings
        {
            Host = "localhost",
            Port = 9091,
            Username = "admin",
            Password = "password",
            UseSsl = false
        };

        // Both should have similar structure
        Assert.Equal(qbSettings.Host, trSettings.Host);
        Assert.Equal(qbSettings.Username, trSettings.Username);
        Assert.Equal(qbSettings.Password, trSettings.Password);
        Assert.Equal(qbSettings.UseSsl, trSettings.UseSsl);
    }

    [Fact]
    public void TransmissionSettings_HasDownloadDir_LikeQBittorrentHasSavePath()
    {
        var trSettings = new TransmissionSettings
        {
            Host = "localhost",
            DownloadDir = "/downloads/comics"
        };

        var qbSettings = new QBittorrentSettings
        {
            Host = "localhost",
            SavePath = "/downloads/comics"
        };

        // Similar concepts, different names
        Assert.Equal(qbSettings.SavePath, trSettings.DownloadDir);
    }

    #endregion

    #region URL Construction Tests

    [Theory]
    [InlineData("localhost", null, false, "http://localhost:9091/transmission/rpc")]
    [InlineData("192.168.1.100", 9091, false, "http://192.168.1.100:9091/transmission/rpc")]
    [InlineData("myserver.local", 443, true, "https://myserver.local:443/transmission/rpc")]
    [InlineData("torrent.example.com", 8080, false, "http://torrent.example.com:8080/transmission/rpc")]
    public void TransmissionSettings_RpcUrl_VariousCombinations(string host, int? port, bool useSsl, string expectedUrl)
    {
        var settings = new TransmissionSettings
        {
            Host = host,
            Port = port,
            UseSsl = useSsl
        };

        Assert.Equal(expectedUrl, settings.RpcUrl);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void TransmissionSettings_AllDefaults()
    {
        var settings = new TransmissionSettings { Host = "test" };

        Assert.Equal("test", settings.Host);
        Assert.Null(settings.Port);
        Assert.Equal(9091, settings.EffectivePort);
        Assert.Null(settings.Username);
        Assert.Null(settings.Password);
        Assert.Null(settings.DownloadDir);
        Assert.False(settings.UseSsl);
        Assert.Equal(30, settings.TimeoutSeconds);
        Assert.False(settings.AddPaused);
        Assert.Equal("/transmission/rpc", settings.RpcPath);
    }

    [Fact]
    public void TransmissionSessionInfo_AllDefaults()
    {
        var info = new TransmissionSessionInfo();

        Assert.Null(info.Version);
        Assert.Equal(0, info.RpcVersion);
        Assert.Equal(0, info.RpcVersionMinimum);
        Assert.Null(info.DownloadDir);
        Assert.Null(info.ConfigDir);
        Assert.Equal(0, info.SpeedLimitDownKBps);
        Assert.False(info.SpeedLimitDownEnabled);
        Assert.Equal(0, info.SpeedLimitUpKBps);
        Assert.False(info.SpeedLimitUpEnabled);
        Assert.Equal(0, info.SeedRatioLimit);
        Assert.False(info.SeedRatioLimited);
    }

    #endregion
}
