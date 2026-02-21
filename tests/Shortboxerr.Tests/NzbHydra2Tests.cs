using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class NzbHydra2Tests
{
    #region NewznabIndexer Tests

    [Fact]
    public void NewznabIndexer_DefaultIsHydra_IsFalse()
    {
        var indexer = new NewznabIndexer
        {
            Name = "Test",
            BaseUrl = "https://test.com",
            ApiKey = "abc123"
        };

        Assert.False(indexer.IsHydra);
        Assert.Equal(NewznabIndexerType.Standard, indexer.IndexerType);
    }

    [Fact]
    public void NewznabIndexer_CanBeSetAsHydra()
    {
        var indexer = new NewznabIndexer
        {
            Name = "My Hydra",
            BaseUrl = "http://localhost:5076",
            ApiKey = "abc123",
            IsHydra = true,
            IndexerType = NewznabIndexerType.NzbHydra2
        };

        Assert.True(indexer.IsHydra);
        Assert.Equal(NewznabIndexerType.NzbHydra2, indexer.IndexerType);
    }

    #endregion

    #region NzbIndexerPresets Tests

    [Fact]
    public void NzbIndexerPresets_GetPreset_ReturnsStandardIndexers()
    {
        var indexer = NzbIndexerPresets.GetPreset("nzbgeek", "myapikey");

        Assert.NotNull(indexer);
        Assert.Equal("NZBgeek", indexer!.Name);
        Assert.Equal("https://api.nzbgeek.info", indexer.BaseUrl);
        Assert.Equal("myapikey", indexer.ApiKey);
        Assert.False(indexer.IsHydra);
        Assert.Equal(NewznabIndexerType.Standard, indexer.IndexerType);
    }

    [Fact]
    public void NzbIndexerPresets_CreateNzbHydra2_ReturnsHydraIndexer()
    {
        var indexer = NzbIndexerPresets.CreateNzbHydra2("http://localhost:5076", "hydrakey");

        Assert.NotNull(indexer);
        Assert.Equal("NZBHydra2", indexer.Name);
        Assert.Equal("http://localhost:5076", indexer.BaseUrl);
        Assert.Equal("hydrakey", indexer.ApiKey);
        Assert.True(indexer.IsHydra);
        Assert.Equal(NewznabIndexerType.NzbHydra2, indexer.IndexerType);
        Assert.Equal(10, indexer.Priority); // High priority
    }

    [Fact]
    public void NzbIndexerPresets_CreateNzbHydra2_AcceptsCustomName()
    {
        var indexer = NzbIndexerPresets.CreateNzbHydra2("http://192.168.1.100:5076", "key", "Home Hydra");

        Assert.Equal("Home Hydra", indexer.Name);
    }

    [Fact]
    public void NzbIndexerPresets_CreateNzbHydra2_TrimsTrailingSlash()
    {
        var indexer = NzbIndexerPresets.CreateNzbHydra2("http://localhost:5076/", "key");

        Assert.Equal("http://localhost:5076", indexer.BaseUrl);
    }

    [Fact]
    public void NzbIndexerPresets_GetPresetsByType_ContainsAllTypes()
    {
        var presetsByType = NzbIndexerPresets.GetPresetsByType();

        Assert.True(presetsByType.ContainsKey(NewznabIndexerType.Standard));
        Assert.True(presetsByType.ContainsKey(NewznabIndexerType.NzbHydra2));
        Assert.True(presetsByType[NewznabIndexerType.Standard].Count > 0);
        Assert.Empty(presetsByType[NewznabIndexerType.NzbHydra2]); // NZBHydra2 is self-hosted
    }

    #endregion

    #region NewznabRelease Hydra Properties Tests

    [Fact]
    public void NewznabRelease_HydraProperties_DefaultToNull()
    {
        var release = new NewznabRelease
        {
            Guid = "abc123",
            Title = "Test Release",
            NzbUrl = "http://test.com/nzb"
        };

        Assert.False(release.IsFromHydra);
        Assert.Null(release.HydraIndexerName);
        Assert.Null(release.HydraIndexerId);
        Assert.Null(release.HydraOriginalGuid);
        Assert.Null(release.HydraScore);
        Assert.Null(release.HydraIndexerHost);
    }

    [Fact]
    public void NewznabRelease_HydraProperties_CanBeSet()
    {
        var release = new NewznabRelease
        {
            Guid = "hydra-abc123",
            Title = "Test Release",
            NzbUrl = "http://hydra.local/nzb",
            IsFromHydra = true,
            HydraIndexerName = "NZBgeek",
            HydraIndexerId = "1",
            HydraOriginalGuid = "abc123",
            HydraScore = 100,
            HydraIndexerHost = "api.nzbgeek.info"
        };

        Assert.True(release.IsFromHydra);
        Assert.Equal("NZBgeek", release.HydraIndexerName);
        Assert.Equal("1", release.HydraIndexerId);
        Assert.Equal("abc123", release.HydraOriginalGuid);
        Assert.Equal(100, release.HydraScore);
        Assert.Equal("api.nzbgeek.info", release.HydraIndexerHost);
    }

    #endregion

    #region NewznabTestResult Tests

    [Fact]
    public void NewznabTestResult_IsHydra_DefaultsToFalse()
    {
        var result = NewznabTestResult.Ok("Success");

        Assert.False(result.IsHydra);
    }

    [Fact]
    public void NewznabTestResult_IsHydra_CanBeSetViaWith()
    {
        var result = NewznabTestResult.Ok("Success") with { IsHydra = true };

        Assert.True(result.IsHydra);
    }

    #endregion

    #region IsNzbHydra2 Detection Tests

    [Fact]
    public void IsNzbHydra2_DetectsHydraInTitle()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = new NewznabServerInfo
            {
                Title = "NZBHydra2",
                Version = "4.7.0"
            }
        };

        Assert.True(NewznabClient.IsNzbHydra2(caps));
    }

    [Fact]
    public void IsNzbHydra2_DetectsHydraInVersion()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = new NewznabServerInfo
            {
                Title = "My Server",
                Version = "nzbhydra2-4.7.0"
            }
        };

        Assert.True(NewznabClient.IsNzbHydra2(caps));
    }

    [Fact]
    public void IsNzbHydra2_DetectsHydraInStrapline()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = new NewznabServerInfo
            {
                Title = "My Server",
                Strapline = "Powered by NZBHydra2"
            }
        };

        Assert.True(NewznabClient.IsNzbHydra2(caps));
    }

    [Fact]
    public void IsNzbHydra2_ReturnsFalseForStandardIndexer()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = new NewznabServerInfo
            {
                Title = "NZBgeek",
                Version = "1.0"
            }
        };

        Assert.False(NewznabClient.IsNzbHydra2(caps));
    }

    [Fact]
    public void IsNzbHydra2_ReturnsFalseForNullServer()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = null
        };

        Assert.False(NewznabClient.IsNzbHydra2(caps));
    }

    [Fact]
    public void IsNzbHydra2_HandlesPartialServerInfo()
    {
        var caps = new NewznabCapabilities
        {
            Success = true,
            Server = new NewznabServerInfo
            {
                Title = null,
                Version = null,
                Strapline = "Hydra2 aggregator"
            }
        };

        Assert.True(NewznabClient.IsNzbHydra2(caps));
    }

    #endregion

    #region Indexer Type Tests

    [Theory]
    [InlineData("nzbgeek")]
    [InlineData("drunkenslug")]
    [InlineData("nzbfinder")]
    [InlineData("nzbplanet")]
    [InlineData("abnzb")]
    [InlineData("althub")]
    public void AllPresets_HaveStandardIndexerType(string presetName)
    {
        var indexer = NzbIndexerPresets.GetPreset(presetName, "testkey");

        Assert.NotNull(indexer);
        Assert.Equal(NewznabIndexerType.Standard, indexer!.IndexerType);
        Assert.False(indexer.IsHydra);
    }

    [Fact]
    public void NewznabIndexerType_HasExpectedValues()
    {
        Assert.Equal(0, (int)NewznabIndexerType.Standard);
        Assert.Equal(1, (int)NewznabIndexerType.NzbHydra2);
    }

    #endregion
}
