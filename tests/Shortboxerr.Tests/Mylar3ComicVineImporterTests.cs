using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Persistence;
using Series = Shortboxerr.Core.Entities.Series;
using SeriesStatus = Shortboxerr.Core.Entities.SeriesStatus;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for Mylar3ComicVineImporter.
/// </summary>
public class Mylar3ComicVineImporterTests : IDisposable
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IComicVineClient> _mockComicVineClient;
    private readonly Mock<ISeriesMetadataService> _mockSeriesMetadataService;
    private readonly Mock<ILogger<Mylar3ComicVineImporter>> _mockLogger;
    private readonly Mylar3ComicVineImporter _importer;

    public Mylar3ComicVineImporterTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ShortboxerrDbContext(options);
        _mockSettingsService = new Mock<ISettingsService>();
        _mockComicVineClient = new Mock<IComicVineClient>();
        _mockSeriesMetadataService = new Mock<ISeriesMetadataService>();
        _mockLogger = new Mock<ILogger<Mylar3ComicVineImporter>>();

        // Default settings mock
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings());

        _mockSettingsService
            .Setup(x => x.GetAsync<MetadataRefreshSettings>("metadata_refresh", It.IsAny<MetadataRefreshSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataRefreshSettings());

        _importer = new Mylar3ComicVineImporter(
            _mockSettingsService.Object,
            _mockComicVineClient.Object,
            _mockSeriesMetadataService.Object,
            _dbContext,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region ParseComicVineSettings Tests

    [Fact]
    public void ParseComicVineSettings_WithValidConfig_ExtractsApiKey()
    {
        // Arrange
        var config = @"
[General]
comic_location = /comics

[CV]
api_key = test-api-key-12345
enabled = true
automatch_threshold = 85
";

        // Act
        var result = _importer.ParseComicVineSettings(config);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("test-api-key-12345", result.ApiKey);
        Assert.True(result.Enabled);
        Assert.Equal(85, result.AutoMatchThreshold);
    }

    [Fact]
    public void ParseComicVineSettings_WithComicVineSection_ExtractsSettings()
    {
        // Arrange
        var config = @"
[ComicVine]
apikey = my-comicvine-key
enabled = 1
refresh_interval = 14
skip_variants = true
skip_annuals = false
cover_quality = original
";

        // Act
        var result = _importer.ParseComicVineSettings(config);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("my-comicvine-key", result.ApiKey);
        Assert.True(result.Enabled);
        Assert.Equal(14, result.RefreshIntervalDays);
        Assert.True(result.SkipVariants);
        Assert.False(result.SkipAnnuals);
        Assert.Equal("original", result.CoverQuality);
    }

    [Fact]
    public void ParseComicVineSettings_WithNoApiKey_AddsWarning()
    {
        // Arrange
        var config = @"
[CV]
enabled = true
";

        // Act
        var result = _importer.ParseComicVineSettings(config);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ApiKey);
        Assert.Contains(result.Warnings, w => w.Contains("No ComicVine API key"));
    }

    [Fact]
    public void ParseComicVineSettings_WithGeneralSectionApiKey_ExtractsKey()
    {
        // Arrange
        var config = @"
[General]
comicvine_api = general-section-key
cache_dir = /cache
";

        // Act
        var result = _importer.ParseComicVineSettings(config);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("general-section-key", result.ApiKey);
        Assert.Equal("/cache", result.CoverCachePath);
    }

    [Fact]
    public void ParseComicVineSettings_TracksUnmappedSettings()
    {
        // Arrange
        var config = @"
[CV]
api_key = test-key
unknown_setting = some_value
another_unknown = 123
";

        // Act
        var result = _importer.ParseComicVineSettings(config);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("unknown_setting", result.UnmappedSettings);
        Assert.Contains("another_unknown", result.UnmappedSettings);
    }

    [Fact]
    public void ParseComicVineSettings_HandlesBooleanFormats()
    {
        // Arrange - test various boolean formats
        var config1 = "[CV]\nenabled = 1";
        var config2 = "[CV]\nenabled = true";
        var config3 = "[CV]\nenabled = yes";
        var config4 = "[CV]\nenabled = 0";

        // Act
        var result1 = _importer.ParseComicVineSettings(config1);
        var result2 = _importer.ParseComicVineSettings(config2);
        var result3 = _importer.ParseComicVineSettings(config3);
        var result4 = _importer.ParseComicVineSettings(config4);

        // Assert
        Assert.True(result1.Enabled);
        Assert.True(result2.Enabled);
        Assert.True(result3.Enabled);
        Assert.False(result4.Enabled);
    }

    #endregion

    #region ImportComicVineSettingsAsync Tests

    [Fact]
    public async Task ImportComicVineSettingsAsync_WithApiKey_ImportsSuccessfully()
    {
        // Arrange
        var settings = new Mylar3ComicVineSettings
        {
            Success = true,
            ApiKey = "new-api-key",
            Enabled = true,
            AutoMatchThreshold = 90
        };

        var options = new ComicVineImportOptions
        {
            OverwriteApiKey = true,
            ImportAutoMatchSettings = true
        };

        // Act
        var result = await _importer.ImportComicVineSettingsAsync(settings, options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ApiKeyImported);
        Assert.True(result.AutoMatchSettingsImported);
        Assert.Contains("ApiKey", result.ImportedSettings);
        Assert.Contains("AutoMatchThreshold: 90", result.ImportedSettings);

        // Verify settings were saved
        _mockSettingsService.Verify(
            x => x.SetAsync("comicvine", It.IsAny<ComicVineSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportComicVineSettingsAsync_WithExistingApiKey_SkipsUnlessOverwrite()
    {
        // Arrange
        _mockSettingsService
            .Setup(x => x.GetAsync<ComicVineSettings>("comicvine", It.IsAny<ComicVineSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComicVineSettings { ApiKey = "existing-key" });

        var settings = new Mylar3ComicVineSettings
        {
            Success = true,
            ApiKey = "new-api-key"
        };

        var options = new ComicVineImportOptions
        {
            OverwriteApiKey = false
        };

        // Act
        var result = await _importer.ImportComicVineSettingsAsync(settings, options);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.ApiKeyImported);
        Assert.Contains(result.SkippedSettings, s => s.Contains("ApiKey") && s.Contains("overwrite=false"));
    }

    [Fact]
    public async Task ImportComicVineSettingsAsync_WithRefreshInterval_ImportsSetting()
    {
        // Arrange
        var settings = new Mylar3ComicVineSettings
        {
            Success = true,
            RefreshIntervalDays = 14
        };

        var options = new ComicVineImportOptions
        {
            ImportRefreshSettings = true
        };

        // Act
        var result = await _importer.ImportComicVineSettingsAsync(settings, options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.RefreshSettingsImported);
        Assert.Contains("RefreshInterval: 14 days", result.ImportedSettings);

        _mockSettingsService.Verify(
            x => x.SetAsync("metadata_refresh", It.IsAny<MetadataRefreshSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region File Not Found Tests

    [Fact]
    public async Task ParseComicVineSettingsFileAsync_WithNonExistentFile_ReturnsError()
    {
        // Act
        var result = await _importer.ParseComicVineSettingsFileAsync("/nonexistent/config.ini");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task ValidateComicVineIdsAsync_WithNonExistentDb_ReturnsError()
    {
        // Act
        var result = await _importer.ValidateComicVineIdsAsync("/nonexistent/mylar.db");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task MigrateComicVineIdsAsync_WithNonExistentDb_ReturnsError()
    {
        // Act
        var result = await _importer.MigrateComicVineIdsAsync(
            "/nonexistent/mylar.db",
            new ComicVineIdMigrationOptions());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    #endregion
}

