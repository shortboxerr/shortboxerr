using System.Net;
using System.Net.Http.Json;
using Xunit;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Tests;

public class SettingsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SettingsEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ========== UI Settings Tests ==========

    [Fact]
    public async Task GetUiSettings_ReturnsValidSettings()
    {
        var response = await _client.GetAsync("/api/v1/settings/ui");
        response.EnsureSuccessStatusCode();

        var settings = await response.Content.ReadFromJsonAsync<UiSettings>();
        Assert.NotNull(settings);
        // Theme must be one of the valid values
        Assert.Contains(settings.Theme, new[] { "dark", "light", "system" });
        // PageSize must be reasonable
        Assert.InRange(settings.PageSize, 10, 500);
    }

    [Fact]
    public async Task UpdateUiSettings_Theme_SavesAndReturns()
    {
        var request = new UiSettings
        {
            Theme = "light",
            PageSize = 100,
            ShowFileSizes = false,
            RelativeTimestamps = false
        };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/ui", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<UiSettings>();
        Assert.NotNull(updated);
        Assert.Equal("light", updated.Theme);
        Assert.Equal(100, updated.PageSize);
        Assert.False(updated.ShowFileSizes);
        Assert.False(updated.RelativeTimestamps);

        // Verify persistence
        var getResponse = await _client.GetAsync("/api/v1/settings/ui");
        var persisted = await getResponse.Content.ReadFromJsonAsync<UiSettings>();
        Assert.NotNull(persisted);
        Assert.Equal("light", persisted.Theme);
    }

    [Fact]
    public async Task UpdateUiSettings_InvalidTheme_ReturnsBadRequest()
    {
        var request = new UiSettings
        {
            Theme = "invalid",
            PageSize = 50,
            ShowFileSizes = true,
            RelativeTimestamps = true
        };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/ui", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUiSettings_InvalidPageSize_ReturnsBadRequest()
    {
        var request = new UiSettings
        {
            Theme = "dark",
            PageSize = 5, // Too small
            ShowFileSizes = true,
            RelativeTimestamps = true
        };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/ui", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUiSettings_SystemTheme_IsValid()
    {
        var request = new UiSettings
        {
            Theme = "system",
            PageSize = 50,
            ShowFileSizes = true,
            RelativeTimestamps = true
        };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/ui", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<UiSettings>();
        Assert.NotNull(updated);
        Assert.Equal("system", updated.Theme);
    }

    // ========== General Settings Tests ==========

    [Fact]
    public async Task GetGeneralSettings_ReturnsValidSettings()
    {
        var response = await _client.GetAsync("/api/v1/settings/general");
        response.EnsureSuccessStatusCode();

        var settings = await response.Content.ReadFromJsonAsync<GeneralSettings>();
        Assert.NotNull(settings);
        // Formats should not be empty
        Assert.NotEmpty(settings.SeriesFolderFormat);
        Assert.NotEmpty(settings.IssueFileFormat);
        Assert.NotEmpty(settings.CollectionFileFormat);
        // Paths should not be empty
        Assert.NotEmpty(settings.ComicLibraryPath);
        Assert.NotEmpty(settings.DownloadFolder);
        Assert.NotEmpty(settings.StagingFolder);
    }

    [Fact]
    public async Task UpdateGeneralSettings_SavesAndReturns()
    {
        var request = new GeneralSettings
        {
            SeriesFolderFormat = "{Publisher}/{Series Title}",
            IssueFileFormat = "{Series Title} - #{Issue}",
            CollectionFileFormat = "{Series Title} - {Edition Type}",
            ComicLibraryPath = "/my/comics",
            DownloadFolder = "/my/downloads",
            StagingFolder = "/my/staging",
            AutoMoveToStaging = false
        };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/general", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<GeneralSettings>();
        Assert.NotNull(updated);
        Assert.Equal("{Publisher}/{Series Title}", updated.SeriesFolderFormat);
        Assert.Equal("/my/comics", updated.ComicLibraryPath);
        Assert.False(updated.AutoMoveToStaging);
    }

    // ========== Folder Settings Tests ==========

    [Fact]
    public async Task GetFolderSettings_ReturnsValues()
    {
        var response = await _client.GetAsync("/api/v1/settings/folders");
        response.EnsureSuccessStatusCode();

        var settings = await response.Content.ReadFromJsonAsync<FolderSettingsResponse>();
        Assert.NotNull(settings);
        Assert.NotNull(settings.DownloadFolder);
        Assert.NotNull(settings.StagingFolder);
    }

    [Fact]
    public async Task UpdateFolderSettings_PartialUpdate_Works()
    {
        var request = new { downloadFolder = "/new/downloads" };

        var response = await _client.PutAsJsonAsync("/api/v1/settings/folders", request);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<FolderSettingsResponse>();
        Assert.NotNull(updated);
        Assert.Equal("/new/downloads", updated.DownloadFolder);
    }

    // ========== Naming Tokens Tests ==========

    [Fact]
    public async Task GetNamingTokens_ReturnsAllTokens()
    {
        var response = await _client.GetAsync("/api/v1/settings/naming/tokens");
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync<NamingTokensResponse>();
        Assert.NotNull(tokens);
        
        // Series folder tokens
        Assert.NotEmpty(tokens.SeriesFolderTokens);
        Assert.Contains(tokens.SeriesFolderTokens, t => t.Token == "{Series Title}");
        Assert.Contains(tokens.SeriesFolderTokens, t => t.Token == "{Publisher}");

        // Issue file tokens
        Assert.NotEmpty(tokens.IssueFileTokens);
        Assert.Contains(tokens.IssueFileTokens, t => t.Token == "{Issue}");
        Assert.Contains(tokens.IssueFileTokens, t => t.Token == "{Quality}");

        // Collection file tokens
        Assert.NotEmpty(tokens.CollectionFileTokens);
        Assert.Contains(tokens.CollectionFileTokens, t => t.Token == "{Edition Type}");
        Assert.Contains(tokens.CollectionFileTokens, t => t.Token == "{Volume}");
    }

    // ========== Generic Settings Tests ==========

    [Fact]
    public async Task GetSetting_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/settings/nonexistent.key");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetSetting_CreatesNewSetting()
    {
        var request = new { value = "test-value" };
        var response = await _client.PutAsJsonAsync("/api/v1/settings/custom.test.key", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SettingResponse>();
        Assert.NotNull(result);
        Assert.Equal("custom.test.key", result.Key);
        Assert.Equal("test-value", result.Value);
    }

    [Fact]
    public async Task DeleteSetting_RemovesSetting()
    {
        // First create
        var request = new { value = "to-delete" };
        await _client.PutAsJsonAsync("/api/v1/settings/delete.test.key", request);

        // Then delete
        var deleteResponse = await _client.DeleteAsync("/api/v1/settings/delete.test.key");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify gone
        var getResponse = await _client.GetAsync("/api/v1/settings/delete.test.key");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteSetting_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync("/api/v1/settings/nonexistent.to.delete");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

// Response DTOs for deserialization
public class FolderSettingsResponse
{
    public string ComicLibraryPath { get; set; } = "";
    public string DownloadFolder { get; set; } = "";
    public string StagingFolder { get; set; } = "";
    public bool AutoMoveToStaging { get; set; }
}

public class NamingTokensResponse
{
    public NamingToken[] SeriesFolderTokens { get; set; } = Array.Empty<NamingToken>();
    public NamingToken[] IssueFileTokens { get; set; } = Array.Empty<NamingToken>();
    public NamingToken[] CollectionFileTokens { get; set; } = Array.Empty<NamingToken>();
}

public record NamingToken(string Token, string Description, string Example);

public class SettingResponse
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

