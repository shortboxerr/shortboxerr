using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for DDL site management functionality.
/// </summary>
public class DdlSiteManagementTests
{
    [Fact]
    public void Factory_RegistersBuiltInAdapters()
    {
        // Arrange & Act
        var factory = new DdlSiteAdapterFactory();

        // Assert
        Assert.True(factory.IsRegistered("GetComics"));
        Assert.True(factory.IsRegistered("ReadComicOnline"));
        Assert.True(factory.IsRegistered("MockDdl"));
    }

    [Fact]
    public void Factory_EnablesGetComicsAndReadComicOnlineByDefault()
    {
        // Arrange & Act
        var factory = new DdlSiteAdapterFactory();
        var enabledSites = factory.GetEnabledSites();

        // Assert
        Assert.Contains("GetComics", enabledSites);
        Assert.Contains("ReadComicOnline", enabledSites);
    }

    [Fact]
    public void Factory_MockDdlNotEnabledByDefault()
    {
        // Arrange & Act
        var factory = new DdlSiteAdapterFactory();
        var enabledSites = factory.GetEnabledSites();

        // Assert - MockDdl should only be enabled if environment variable is set
        // In normal test conditions, it should not be enabled
        if (Environment.GetEnvironmentVariable("SHORTBOXERR_ENABLE_MOCK_DDL") != "true")
        {
            Assert.DoesNotContain("MockDdl", enabledSites);
        }
    }

    [Fact]
    public void Factory_CanEnableSite()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();
        factory.DisableSite("GetComics"); // First disable it

        // Act
        factory.EnableSite("GetComics");
        var enabledSites = factory.GetEnabledSites();

        // Assert
        Assert.Contains("GetComics", enabledSites);
    }

    [Fact]
    public void Factory_CanDisableSite()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        factory.DisableSite("ReadComicOnline");
        var enabledSites = factory.GetEnabledSites();

        // Assert
        Assert.DoesNotContain("ReadComicOnline", enabledSites);
    }

    [Fact]
    public void Factory_IsSiteEnabled_ReturnsCorrectStatus()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Assert
        Assert.True(factory.IsSiteEnabled("GetComics"));
        Assert.True(factory.IsSiteEnabled("ReadComicOnline"));
        
        factory.DisableSite("GetComics");
        Assert.False(factory.IsSiteEnabled("GetComics"));
    }

    [Fact]
    public void Factory_SetEnabledSites_ReplacesCurrentSet()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        factory.SetEnabledSites(new[] { "MockDdl" });
        var enabledSites = factory.GetEnabledSites();

        // Assert
        Assert.Single(enabledSites);
        Assert.Contains("MockDdl", enabledSites);
        Assert.DoesNotContain("GetComics", enabledSites);
        Assert.DoesNotContain("ReadComicOnline", enabledSites);
    }

    [Fact]
    public void Factory_GetSiteStatuses_ReturnsAllSites()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        var statuses = factory.GetSiteStatuses();

        // Assert
        Assert.True(statuses.Count >= 2);
        Assert.Contains(statuses, s => s.SiteType == "GetComics");
        Assert.Contains(statuses, s => s.SiteType == "ReadComicOnline");
    }

    [Fact]
    public void Factory_GetSiteStatuses_IncludesEnabledFlag()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();
        factory.DisableSite("ReadComicOnline");

        // Act
        var statuses = factory.GetSiteStatuses();

        // Assert
        var getComicsStatus = statuses.First(s => s.SiteType == "GetComics");
        var readComicOnlineStatus = statuses.First(s => s.SiteType == "ReadComicOnline");

        Assert.True(getComicsStatus.IsEnabled);
        Assert.False(readComicOnlineStatus.IsEnabled);
    }

    [Fact]
    public void Factory_GetSiteStatuses_SortedByPriority()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        var statuses = factory.GetSiteStatuses();

        // Assert - GetComics (priority 1) should come before ReadComicOnline (priority 2)
        var getComicsIndex = statuses.ToList().FindIndex(s => s.SiteType == "GetComics");
        var readComicOnlineIndex = statuses.ToList().FindIndex(s => s.SiteType == "ReadComicOnline");

        Assert.True(getComicsIndex < readComicOnlineIndex);
    }

    [Fact]
    public void Factory_GetAvailableSiteInfos_ReturnsCorrectInfo()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        var siteInfos = factory.GetAvailableSiteInfos();

        // Assert
        var getComics = siteInfos.First(s => s.SiteType == "GetComics");
        Assert.Equal("GetComics.org", getComics.DisplayName);
        Assert.Equal("https://getcomics.org", getComics.DefaultBaseUrl);
        Assert.False(getComics.RequiresAuthentication);
        Assert.Equal(10, getComics.DefaultRateLimitPerMinute);

        var readComicOnline = siteInfos.First(s => s.SiteType == "ReadComicOnline");
        Assert.Equal("ReadComicOnline", readComicOnline.DisplayName);
        Assert.Equal("https://readcomiconline.li", readComicOnline.DefaultBaseUrl);
        Assert.Equal(5, readComicOnline.DefaultRateLimitPerMinute); // More restrictive
    }

    [Fact]
    public void Adapter_GetComics_HasCorrectRateLimit()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        var adapter = factory.GetAdapter("GetComics");

        // Assert
        Assert.Equal(10, adapter.DefaultRateLimitPerMinute);
    }

    [Fact]
    public void Adapter_ReadComicOnline_HasRestrictiveRateLimit()
    {
        // Arrange
        var factory = new DdlSiteAdapterFactory();

        // Act
        var adapter = factory.GetAdapter("ReadComicOnline");

        // Assert
        Assert.Equal(5, adapter.DefaultRateLimitPerMinute);
    }
}
