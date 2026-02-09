using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Shortboxerr.Api.Endpoints;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;
using Xunit;

namespace Shortboxerr.Tests;

public class NzbEndpointsTests
{
    private readonly Mock<INzbIndexerProvider> _indexerProviderMock;
    private readonly Mock<INewznabClient> _newznabClientMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;

    public NzbEndpointsTests()
    {
        _indexerProviderMock = new Mock<INzbIndexerProvider>();
        _newznabClientMock = new Mock<INewznabClient>();
        _settingsServiceMock = new Mock<ISettingsService>();
    }

    private static NewznabIndexer CreateTestIndexer(string id = "test-1", string name = "Test Indexer") =>
        new()
        {
            Id = id,
            Name = name,
            BaseUrl = "https://api.test.com",
            ApiKey = "test-api-key",
            Enabled = true,
            Priority = 50,
            Categories = new List<int> { 7030, 7000 }
        };

    #region GetIndexers Tests

    [Fact]
    public async Task GetIndexers_ReturnsAllIndexers()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer 1"),
            CreateTestIndexer("2", "Indexer 2")
        };

        _indexerProviderMock
            .Setup(p => p.GetIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        // Act - we need to test via reflection since handlers are private static
        var result = await InvokeGetIndexers();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<Ok<IndexersResponse>>(result);
        Assert.Equal(2, okResult.Value!.TotalCount);
        Assert.Equal(2, okResult.Value.EnabledCount);
    }

    [Fact]
    public async Task GetIndexers_WithDisabledIndexers_ReturnsCorrectCounts()
    {
        // Arrange
        var enabled = CreateTestIndexer("1", "Enabled");
        enabled.Enabled = true;

        var disabled = CreateTestIndexer("2", "Disabled");
        disabled.Enabled = false;

        var indexers = new List<NewznabIndexer> { enabled, disabled };

        _indexerProviderMock
            .Setup(p => p.GetIndexersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        // Act
        var result = await InvokeGetIndexers();

        // Assert
        var okResult = Assert.IsType<Ok<IndexersResponse>>(result);
        Assert.Equal(2, okResult.Value!.TotalCount);
        Assert.Equal(1, okResult.Value.EnabledCount);
    }

    #endregion

    #region GetIndexer Tests

    [Fact]
    public async Task GetIndexer_WithValidId_ReturnsIndexer()
    {
        // Arrange
        var indexer = CreateTestIndexer("test-id", "Test Indexer");
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("test-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexer);

        // Act
        var result = await InvokeGetIndexer("test-id");

        // Assert
        var okResult = Assert.IsType<Ok<NewznabIndexer>>(result);
        Assert.Equal("Test Indexer", okResult.Value!.Name);
    }

    [Fact]
    public async Task GetIndexer_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer?)null);

        // Act
        var result = await InvokeGetIndexer("nonexistent");

        // Assert - check the result type name contains "NotFound"
        Assert.Contains("NotFound", result.GetType().Name);
    }

    #endregion

    #region AddIndexer Tests

    [Fact]
    public async Task AddIndexer_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new AddIndexerRequest
        {
            Name = "New Indexer",
            BaseUrl = "https://api.new.com",
            ApiKey = "new-key"
        };

        _indexerProviderMock
            .Setup(p => p.AddIndexerAsync(It.IsAny<NewznabIndexer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer i, CancellationToken _) =>
            {
                i.Id = "generated-id";
                return i;
            });

        // Act
        var result = await InvokeAddIndexer(request);

        // Assert
        var createdResult = Assert.IsType<Created<NewznabIndexer>>(result);
        Assert.Equal("New Indexer", createdResult.Value!.Name);
        Assert.Contains("generated-id", createdResult.Location);
    }

    [Fact]
    public async Task AddIndexer_WithMissingName_ReturnsValidationError()
    {
        // Arrange
        var request = new AddIndexerRequest
        {
            BaseUrl = "https://api.test.com",
            ApiKey = "key"
        };

        // Act
        var result = await InvokeAddIndexer(request);

        // Assert - ValidationProblem or ProblemHttpResult indicates validation failure
        Assert.Contains("Problem", result.GetType().Name);
    }

    [Fact]
    public async Task AddIndexer_WithMissingApiKey_ReturnsValidationError()
    {
        // Arrange
        var request = new AddIndexerRequest
        {
            Name = "Test",
            BaseUrl = "https://api.test.com"
        };

        // Act
        var result = await InvokeAddIndexer(request);

        // Assert
        Assert.Contains("Problem", result.GetType().Name);
    }

    #endregion

    #region UpdateIndexer Tests

    [Fact]
    public async Task UpdateIndexer_WithValidRequest_ReturnsUpdated()
    {
        // Arrange
        var existing = CreateTestIndexer("test-id", "Old Name");
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("test-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _indexerProviderMock
            .Setup(p => p.UpdateIndexerAsync(It.IsAny<NewznabIndexer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer i, CancellationToken _) => i);

        var request = new UpdateIndexerRequest { Name = "New Name" };

        // Act
        var result = await InvokeUpdateIndexer("test-id", request);

        // Assert
        var okResult = Assert.IsType<Ok<NewznabIndexer>>(result);
        Assert.Equal("New Name", okResult.Value!.Name);
    }

    [Fact]
    public async Task UpdateIndexer_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer?)null);

        // Act
        var result = await InvokeUpdateIndexer("nonexistent", new UpdateIndexerRequest());

        // Assert
        Assert.Contains("NotFound", result.GetType().Name);
    }

    #endregion

    #region DeleteIndexer Tests

    [Fact]
    public async Task DeleteIndexer_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _indexerProviderMock
            .Setup(p => p.DeleteIndexerAsync("test-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await InvokeDeleteIndexer("test-id");

        // Assert
        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteIndexer_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _indexerProviderMock
            .Setup(p => p.DeleteIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await InvokeDeleteIndexer("nonexistent");

        // Assert
        Assert.Contains("NotFound", result.GetType().Name);
    }

    #endregion

    #region TestIndexer Tests

    [Fact]
    public async Task TestIndexer_WithValidId_ReturnsTestResult()
    {
        // Arrange
        var indexer = CreateTestIndexer("test-id");
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("test-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexer);

        _indexerProviderMock
            .Setup(p => p.TestIndexerAsync(indexer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabTestResult.Ok("Connected successfully"));

        // Act
        var result = await InvokeTestIndexer("test-id");

        // Assert
        var okResult = Assert.IsType<Ok<NewznabTestResult>>(result);
        Assert.True(okResult.Value!.Success);
    }

    [Fact]
    public async Task TestIndexer_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _indexerProviderMock
            .Setup(p => p.GetIndexerAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewznabIndexer?)null);

        // Act
        var result = await InvokeTestIndexer("nonexistent");

        // Assert
        Assert.Contains("NotFound", result.GetType().Name);
    }

    #endregion

    #region GetIndexerPresets Tests

    [Fact]
    public void GetIndexerPresets_ReturnsPresetList()
    {
        // Act
        var result = InvokeGetIndexerPresets();

        // Assert
        var okResult = Assert.IsType<Ok<IndexerPresetsResponse>>(result);
        Assert.NotEmpty(okResult.Value!.Presets);
        Assert.Contains(okResult.Value.Presets, p => p.Id == "nzbgeek");
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchNzb_ReturnsResults()
    {
        // Arrange
        var release = new NewznabRelease
        {
            Guid = "release-1",
            Title = "Batman 001",
            NzbUrl = "http://test/1",
            Size = 50000000,
            PublishedDate = DateTime.UtcNow
        };

        var searchResult = new NzbAggregatedSearchResult
        {
            Releases = new[] { release },
            TotalResults = 1,
            IndexersSearched = 1,
            IndexersSuccessful = 1,
            Duration = TimeSpan.FromMilliseconds(500),
            IndexerResults = new[] { new IndexerSearchResult { IndexerId = "1", IndexerName = "Test", Success = true, ReleaseCount = 1, Duration = TimeSpan.FromMilliseconds(500) } }
        };

        _indexerProviderMock
            .Setup(p => p.SearchAllAsync(It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        // Act
        var result = await InvokeSearchNzb("Batman");

        // Assert
        var okResult = Assert.IsType<Ok<NzbSearchResponse>>(result);
        Assert.Single(okResult.Value!.Releases);
        Assert.Equal(1, okResult.Value.IndexersSearched);
    }

    #endregion

    #region Download Client Tests

    [Fact]
    public async Task GetDownloadClientSettings_ReturnsSettings()
    {
        // Arrange
        var settings = new DownloadClientSettings
        {
            ClientType = NzbDownloadClientType.SABnzbd,
            Sabnzbd = new SabnzbdSettings
            {
                Host = "http://localhost:8080",
                ApiKey = "test-key",
                Category = "comics"
            }
        };

        _settingsServiceMock
            .Setup(s => s.GetAsync<DownloadClientSettings>(It.IsAny<string>(), It.IsAny<DownloadClientSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await InvokeGetDownloadClientSettings();

        // Assert
        var okResult = Assert.IsType<Ok<DownloadClientSettingsResponse>>(result);
        Assert.True(okResult.Value!.IsConfigured);
        Assert.Equal(NzbDownloadClientType.SABnzbd, okResult.Value.ClientType);
    }

    [Fact]
    public async Task GetDownloadClientSettings_WithNoConfig_ReturnsNotConfigured()
    {
        // Arrange
        _settingsServiceMock
            .Setup(s => s.GetAsync<DownloadClientSettings>(It.IsAny<string>(), It.IsAny<DownloadClientSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadClientSettings());

        // Act
        var result = await InvokeGetDownloadClientSettings();

        // Assert
        var okResult = Assert.IsType<Ok<DownloadClientSettingsResponse>>(result);
        Assert.False(okResult.Value!.IsConfigured);
    }

    #endregion

    // === Helper Methods to Invoke Endpoint Handlers ===

    private async Task<IResult> InvokeGetIndexers()
    {
        // Use reflection to call the private static handler
        var method = typeof(NzbEndpoints).GetMethod("GetIndexers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeGetIndexer(string id)
    {
        var method = typeof(NzbEndpoints).GetMethod("GetIndexer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { id, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeAddIndexer(AddIndexerRequest request)
    {
        var method = typeof(NzbEndpoints).GetMethod("AddIndexer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { request, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeUpdateIndexer(string id, UpdateIndexerRequest request)
    {
        var method = typeof(NzbEndpoints).GetMethod("UpdateIndexer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { id, request, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeDeleteIndexer(string id)
    {
        var method = typeof(NzbEndpoints).GetMethod("DeleteIndexer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { id, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeTestIndexer(string id)
    {
        var method = typeof(NzbEndpoints).GetMethod("TestIndexer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { id, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private IResult InvokeGetIndexerPresets()
    {
        var method = typeof(NzbEndpoints).GetMethod("GetIndexerPresets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)method!.Invoke(null, null)!;
    }

    private async Task<IResult> InvokeSearchNzb(string? query)
    {
        var method = typeof(NzbEndpoints).GetMethod("SearchNzb",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object?[] { query, null, null, _indexerProviderMock.Object, CancellationToken.None })!;
    }

    private async Task<IResult> InvokeGetDownloadClientSettings()
    {
        var method = typeof(NzbEndpoints).GetMethod("GetDownloadClientSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (IResult)await (Task<IResult>)method!.Invoke(null, new object[] { _settingsServiceMock.Object, CancellationToken.None })!;
    }
}
