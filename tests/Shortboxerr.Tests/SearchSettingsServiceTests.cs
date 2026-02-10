using Moq;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Search;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for SearchSettingsService.
/// </summary>
public class SearchSettingsServiceTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly SearchSettingsService _service;

    public SearchSettingsServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _service = new SearchSettingsService(_mockSettingsService.Object);
    }

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultsWhenNoSettingsStored()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<SearchSettings>(SearchSettings.SettingsKey, It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchSettings.Default);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.SearchDelaySeconds);
        Assert.False(result.PreferPackReleases);
        Assert.True(result.EnableDdlSearch);
        Assert.True(result.EnableNzbSearch);
        Assert.False(result.EnableTorrentSearch);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsStoredSettings()
    {
        // Arrange
        var storedSettings = new SearchSettings
        {
            SearchDelaySeconds = 5,
            PreferPackReleases = true,
            CbzOnly = true,
            MaxSizeMb = 1000
        };

        _mockSettingsService
            .Setup(s => s.GetAsync<SearchSettings>(SearchSettings.SettingsKey, It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedSettings);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        Assert.Equal(5, result.SearchDelaySeconds);
        Assert.True(result.PreferPackReleases);
        Assert.True(result.CbzOnly);
        Assert.Equal(1000, result.MaxSizeMb);
    }

    [Fact]
    public async Task GetSettingsAsync_CachesResult()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync<SearchSettings>(SearchSettings.SettingsKey, It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchSettings.Default);

        // Act - call twice
        await _service.GetSettingsAsync();
        await _service.GetSettingsAsync();

        // Assert - should only call backend once due to caching
        _mockSettingsService.Verify(
            s => s.GetAsync<SearchSettings>(SearchSettings.SettingsKey, It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region SaveSettingsAsync Tests

    [Fact]
    public async Task SaveSettingsAsync_SavesValidSettings()
    {
        // Arrange
        var settings = SearchSettings.Default;

        // Act
        await _service.SaveSettingsAsync(settings);

        // Assert
        _mockSettingsService.Verify(
            s => s.SetAsync(SearchSettings.SettingsKey, settings, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task SaveSettingsAsync_ThrowsOnInvalidSettings()
    {
        // Arrange
        var invalidSettings = new SearchSettings
        {
            SearchDelaySeconds = -1 // Invalid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveSettingsAsync(invalidSettings));
    }

    [Fact]
    public async Task SaveSettingsAsync_UpdatesCache()
    {
        // Arrange
        var initialSettings = SearchSettings.Default;
        _mockSettingsService
            .Setup(s => s.GetAsync<SearchSettings>(SearchSettings.SettingsKey, It.IsAny<SearchSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialSettings);

        // Get initial settings to populate cache
        await _service.GetSettingsAsync();

        var newSettings = new SearchSettings
        {
            SearchDelaySeconds = 10
        };

        // Act
        await _service.SaveSettingsAsync(newSettings);
        var cachedResult = await _service.GetSettingsAsync();

        // Assert - should return the new settings from cache without calling backend again
        Assert.Equal(10, cachedResult.SearchDelaySeconds);
    }

    #endregion

    #region ResetToDefaultsAsync Tests

    [Fact]
    public async Task ResetToDefaultsAsync_SavesDefaultSettings()
    {
        // Act
        await _service.ResetToDefaultsAsync();

        // Assert
        _mockSettingsService.Verify(
            s => s.SetAsync(SearchSettings.SettingsKey, It.Is<SearchSettings>(settings =>
                settings.SearchDelaySeconds == 1 &&
                settings.PreferPackReleases == false &&
                settings.EnableDdlSearch == true), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region ValidateSettings Tests

    [Fact]
    public void ValidateSettings_AcceptsValidSettings()
    {
        // Arrange
        var settings = SearchSettings.Default;

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_RejectsNegativeSearchDelay()
    {
        // Arrange
        var settings = new SearchSettings { SearchDelaySeconds = -1 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("delay"));
    }

    [Fact]
    public void ValidateSettings_RejectsNegativeMinSize()
    {
        // Arrange
        var settings = new SearchSettings { MinSizeMb = -10 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("Minimum size"));
    }

    [Fact]
    public void ValidateSettings_RejectsMinSizeGreaterThanMaxSize()
    {
        // Arrange
        var settings = new SearchSettings { MinSizeMb = 100, MaxSizeMb = 50 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("greater than maximum"));
    }

    [Fact]
    public void ValidateSettings_RejectsMinPackSizeGreaterThanMaxPackSize()
    {
        // Arrange
        var settings = new SearchSettings { MinSizePackMb = 1000, MaxSizePackMb = 500 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("pack size"));
    }

    [Fact]
    public void ValidateSettings_RejectsInvalidAutoSearchInterval()
    {
        // Arrange
        var settings = new SearchSettings { AutoSearchIntervalHours = 0 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("interval"));
    }

    [Fact]
    public void ValidateSettings_RejectsNegativeStaleThreshold()
    {
        // Arrange
        var settings = new SearchSettings { StaleSearchThresholdDays = -1 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("threshold"));
    }

    [Fact]
    public void ValidateSettings_RejectsEmptyFormatPreference()
    {
        // Arrange
        var settings = new SearchSettings { FormatPreference = new List<string>() };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("format preference"));
    }

    [Fact]
    public void ValidateSettings_RejectsNegativeSearchTierCutoff()
    {
        // Arrange
        var settings = new SearchSettings { SearchTierCutoff = -1 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("tier cutoff"));
    }

    [Fact]
    public void ValidateSettings_RejectsInvalidMaxResults()
    {
        // Arrange
        var settings = new SearchSettings { MaxResultsPerProvider = 0 };

        // Act
        var errors = _service.ValidateSettings(settings);

        // Assert
        Assert.Contains(errors, e => e.Contains("results"));
    }

    #endregion

    #region SearchSettings Default Values Tests

    [Fact]
    public void SearchSettings_Default_HasCorrectValues()
    {
        // Act
        var defaults = SearchSettings.Default;

        // Assert - Search Behavior
        Assert.Equal(1, defaults.SearchDelaySeconds);
        Assert.False(defaults.PreferPackReleases);
        Assert.Equal(0, defaults.SearchTierCutoff);
        Assert.Equal(50, defaults.MaxResultsPerProvider);

        // Assert - Quality Preferences
        Assert.Equal(PreferredQuality.Digital, defaults.PreferredQuality);
        Assert.Contains("cbz", defaults.FormatPreference);
        Assert.False(defaults.CbzOnly);

        // Assert - Size Limits
        Assert.Equal(0, defaults.MinSizeMb);
        Assert.Equal(500, defaults.MaxSizeMb);
        Assert.Equal(0, defaults.MinSizePackMb);
        Assert.Equal(5000, defaults.MaxSizePackMb);

        // Assert - Provider Toggles
        Assert.True(defaults.EnableDdlSearch);
        Assert.True(defaults.EnableNzbSearch);
        Assert.False(defaults.EnableTorrentSearch);

        // Assert - Automation
        Assert.False(defaults.AutoSearchEnabled);
        Assert.Equal(24, defaults.AutoSearchIntervalHours);
        Assert.True(defaults.SearchNewSeriesOnAdd);
        Assert.Equal(7, defaults.StaleSearchThresholdDays);
    }

    [Fact]
    public void SearchSettings_Default_HasCorrectBlacklistWords()
    {
        // Act
        var defaults = SearchSettings.Default;

        // Assert
        Assert.Contains("sample", defaults.BlacklistWords);
        Assert.Contains("preview", defaults.BlacklistWords);
        Assert.Contains("watermark", defaults.BlacklistWords);
    }

    [Fact]
    public void SearchSettings_Default_HasCorrectIgnoreWords()
    {
        // Act
        var defaults = SearchSettings.Default;

        // Assert
        Assert.Contains("repack", defaults.IgnoreWords);
        Assert.Contains("proper", defaults.IgnoreWords);
    }

    #endregion
}
