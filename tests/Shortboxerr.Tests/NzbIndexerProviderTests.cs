using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

public class NzbIndexerProviderTests
{
    private readonly Mock<INewznabClient> _clientMock;
    private readonly Mock<ISettingsService> _settingsMock;
    private readonly Mock<ILogger<NzbIndexerProvider>> _loggerMock;

    public NzbIndexerProviderTests()
    {
        _clientMock = new Mock<INewznabClient>();
        _settingsMock = new Mock<ISettingsService>();
        _loggerMock = new Mock<ILogger<NzbIndexerProvider>>();
    }

    private NzbIndexerProvider CreateProvider() =>
        new(_clientMock.Object, _settingsMock.Object, _loggerMock.Object);

    private static NewznabIndexer CreateTestIndexer(string id, string name, bool enabled = true, int priority = 50) =>
        new()
        {
            Id = id,
            Name = name,
            BaseUrl = $"https://api.{name.ToLowerInvariant()}.com",
            ApiKey = "test-api-key",
            Enabled = enabled,
            Priority = priority
        };

    #region GetIndexers Tests

    [Fact]
    public async Task GetIndexersAsync_ReturnsAllConfiguredIndexers()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer1"),
            CreateTestIndexer("2", "Indexer2", enabled: false)
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetIndexersAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEnabledIndexersAsync_ReturnsOnlyEnabled()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer1", enabled: true),
            CreateTestIndexer("2", "Indexer2", enabled: false),
            CreateTestIndexer("3", "Indexer3", enabled: true)
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetEnabledIndexersAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.True(i.Enabled));
    }

    [Fact]
    public async Task GetEnabledIndexersAsync_OrdersByPriority()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "LowPriority", priority: 100),
            CreateTestIndexer("2", "HighPriority", priority: 10),
            CreateTestIndexer("3", "MediumPriority", priority: 50)
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetEnabledIndexersAsync();

        // Assert
        Assert.Equal("HighPriority", result[0].Name);
        Assert.Equal("MediumPriority", result[1].Name);
        Assert.Equal("LowPriority", result[2].Name);
    }

    [Fact]
    public async Task GetIndexerAsync_ReturnsMatchingIndexer()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer1"),
            CreateTestIndexer("2", "Indexer2")
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetIndexerAsync("2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Indexer2", result.Name);
    }

    [Fact]
    public async Task GetIndexerAsync_ReturnsNullForUnknownId()
    {
        // Arrange
        var indexers = new List<NewznabIndexer> { CreateTestIndexer("1", "Indexer1") };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetIndexerAsync("unknown");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Add/Update/Delete Tests

    [Fact]
    public async Task AddIndexerAsync_AddsToSettings()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer>();
        List<NewznabIndexer>? savedIndexers = null;

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        _settingsMock
            .Setup(s => s.SetAsync("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<NewznabIndexer>, CancellationToken>((_, list, _) => savedIndexers = list)
            .Returns(Task.CompletedTask);

        var provider = CreateProvider();
        var newIndexer = CreateTestIndexer("", "NewIndexer"); // ID will be generated

        // Act
        var result = await provider.AddIndexerAsync(newIndexer);

        // Assert
        Assert.NotNull(savedIndexers);
        Assert.Single(savedIndexers);
        Assert.False(string.IsNullOrEmpty(result.Id));
        Assert.Equal("NewIndexer", result.Name);
    }

    [Fact]
    public async Task AddIndexerAsync_ThrowsOnDuplicate()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer> { CreateTestIndexer("existing-id", "Existing") };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        var provider = CreateProvider();
        var duplicate = CreateTestIndexer("existing-id", "Duplicate");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.AddIndexerAsync(duplicate));
    }

    [Fact]
    public async Task UpdateIndexerAsync_UpdatesInSettings()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer> { CreateTestIndexer("1", "Original") };
        List<NewznabIndexer>? savedIndexers = null;

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        _settingsMock
            .Setup(s => s.SetAsync("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<NewznabIndexer>, CancellationToken>((_, list, _) => savedIndexers = list)
            .Returns(Task.CompletedTask);

        var provider = CreateProvider();
        var updated = CreateTestIndexer("1", "Updated");

        // Act
        var result = await provider.UpdateIndexerAsync(updated);

        // Assert
        Assert.NotNull(savedIndexers);
        Assert.Single(savedIndexers);
        Assert.Equal("Updated", savedIndexers[0].Name);
    }

    [Fact]
    public async Task UpdateIndexerAsync_ThrowsOnNotFound()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer> { CreateTestIndexer("1", "Existing") };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        var provider = CreateProvider();
        var notFound = CreateTestIndexer("unknown", "NotFound");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.UpdateIndexerAsync(notFound));
    }

    [Fact]
    public async Task DeleteIndexerAsync_RemovesFromSettings()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "ToDelete"),
            CreateTestIndexer("2", "ToKeep")
        };
        List<NewznabIndexer>? savedIndexers = null;

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        _settingsMock
            .Setup(s => s.SetAsync("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<NewznabIndexer>, CancellationToken>((_, list, _) => savedIndexers = list)
            .Returns(Task.CompletedTask);

        var provider = CreateProvider();

        // Act
        var result = await provider.DeleteIndexerAsync("1");

        // Assert
        Assert.True(result);
        Assert.NotNull(savedIndexers);
        Assert.Single(savedIndexers);
        Assert.Equal("ToKeep", savedIndexers[0].Name);
    }

    [Fact]
    public async Task DeleteIndexerAsync_ReturnsFalseForUnknown()
    {
        // Arrange
        var existingIndexers = new List<NewznabIndexer> { CreateTestIndexer("1", "Existing") };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndexers);

        var provider = CreateProvider();

        // Act
        var result = await provider.DeleteIndexerAsync("unknown");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Test Connection Tests

    [Fact]
    public async Task TestIndexerAsync_DelegatesToClient()
    {
        // Arrange
        var indexer = CreateTestIndexer("1", "TestIndexer");
        var expectedResult = NewznabTestResult.Ok("Success");

        _clientMock
            .Setup(c => c.TestConnectionAsync(indexer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var provider = CreateProvider();

        // Act
        var result = await provider.TestIndexerAsync(indexer);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Message);
        _clientMock.Verify(c => c.TestConnectionAsync(indexer, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchAll Tests

    [Fact]
    public async Task SearchAllAsync_SearchesAllEnabledIndexers()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer1", priority: 10),
            CreateTestIndexer("2", "Indexer2", priority: 20)
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var release1 = new NewznabRelease
        {
            Guid = "guid1",
            Title = "Batman 001",
            NzbUrl = "http://test/1",
            Size = 50000000,
            PublishedDate = DateTime.UtcNow.AddDays(-1),
            IndexerName = "Indexer1",
            IndexerId = "1"
        };

        var release2 = new NewznabRelease
        {
            Guid = "guid2",
            Title = "Batman 002",
            NzbUrl = "http://test/2",
            Size = 48000000,
            PublishedDate = DateTime.UtcNow.AddDays(-2),
            IndexerName = "Indexer2",
            IndexerId = "2"
        };

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "1"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Ok(new[] { release1 }, 1, 0, TimeSpan.FromMilliseconds(100)));

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "2"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Ok(new[] { release2 }, 1, 0, TimeSpan.FromMilliseconds(150)));

        var provider = CreateProvider();

        // Act
        var result = await provider.SearchAllAsync(new NewznabSearchQuery { Query = "Batman" });

        // Assert
        Assert.Equal(2, result.Releases.Count);
        Assert.Equal(2, result.IndexersSearched);
        Assert.Equal(2, result.IndexersSuccessful);
        _clientMock.Verify(c => c.SearchAsync(It.IsAny<NewznabIndexer>(), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SearchAllAsync_DeduplicatesResults()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "Indexer1"),
            CreateTestIndexer("2", "Indexer2")
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        // Same release from two indexers (same title and size)
        var release1 = new NewznabRelease
        {
            Guid = "guid1",
            Title = "Batman 001 (2024)",
            NzbUrl = "http://test/1",
            Size = 50000000,
            PublishedDate = DateTime.UtcNow.AddDays(-1),
            IndexerName = "Indexer1"
        };

        var release2 = new NewznabRelease
        {
            Guid = "guid2",
            Title = "Batman.001.(2024)", // Same release, different formatting
            NzbUrl = "http://test/2",
            Size = 50000000, // Same size
            PublishedDate = DateTime.UtcNow.AddDays(-1),
            IndexerName = "Indexer2"
        };

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "1"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Ok(new[] { release1 }, 1, 0, TimeSpan.Zero));

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "2"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Ok(new[] { release2 }, 1, 0, TimeSpan.Zero));

        var provider = CreateProvider();

        // Act
        var result = await provider.SearchAllAsync(new NewznabSearchQuery { Query = "Batman" });

        // Assert
        Assert.Single(result.Releases); // Deduplicated to 1
    }

    [Fact]
    public async Task SearchAllAsync_HandlesIndexerErrors()
    {
        // Arrange
        var indexers = new List<NewznabIndexer>
        {
            CreateTestIndexer("1", "GoodIndexer"),
            CreateTestIndexer("2", "BadIndexer")
        };

        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexers);

        var release = new NewznabRelease
        {
            Guid = "guid1",
            Title = "Batman 001",
            NzbUrl = "http://test/1",
            Size = 50000000,
            PublishedDate = DateTime.UtcNow,
            IndexerName = "GoodIndexer"
        };

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "1"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Ok(new[] { release }, 1, 0, TimeSpan.Zero));

        _clientMock
            .Setup(c => c.SearchAsync(It.Is<NewznabIndexer>(i => i.Id == "2"), It.IsAny<NewznabSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewznabSearchResult.Error("Connection timeout"));

        var provider = CreateProvider();

        // Act
        var result = await provider.SearchAllAsync(new NewznabSearchQuery { Query = "Batman" });

        // Assert
        Assert.Single(result.Releases);
        Assert.Equal(2, result.IndexersSearched);
        Assert.Equal(1, result.IndexersSuccessful);

        var failedResult = result.IndexerResults.First(r => r.IndexerName == "BadIndexer");
        Assert.False(failedResult.Success);
        Assert.Equal("Connection timeout", failedResult.ErrorMessage);
    }

    [Fact]
    public async Task SearchAllAsync_WithNoEnabledIndexers_ReturnsEmpty()
    {
        // Arrange
        _settingsMock
            .Setup(s => s.GetAsync<List<NewznabIndexer>>("nzb_indexers", It.IsAny<List<NewznabIndexer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NewznabIndexer>());

        var provider = CreateProvider();

        // Act
        var result = await provider.SearchAllAsync(new NewznabSearchQuery { Query = "Batman" });

        // Assert
        Assert.Empty(result.Releases);
        Assert.Equal(0, result.IndexersSearched);
        Assert.Equal(0, result.IndexersSuccessful);
    }

    #endregion
}

public class NzbIndexerPresetsTests
{
    [Theory]
    [InlineData("nzbgeek", "NZBgeek", "https://api.nzbgeek.info")]
    [InlineData("drunkenslug", "DrunkenSlug", "https://api.drunkenslug.com")]
    [InlineData("nzbfinder", "NZBFinder", "https://nzbfinder.ws")]
    public void GetPreset_ReturnsCorrectConfiguration(string presetName, string expectedName, string expectedBaseUrl)
    {
        // Act
        var preset = NzbIndexerPresets.GetPreset(presetName, "test-api-key");

        // Assert
        Assert.NotNull(preset);
        Assert.Equal(expectedName, preset.Name);
        Assert.Equal(expectedBaseUrl, preset.BaseUrl);
        Assert.Equal("test-api-key", preset.ApiKey);
        Assert.Contains(7030, preset.Categories);
    }

    [Fact]
    public void GetPreset_WithUnknownPreset_ReturnsNull()
    {
        // Act
        var preset = NzbIndexerPresets.GetPreset("unknown", "test-key");

        // Assert
        Assert.Null(preset);
    }

    [Fact]
    public void GetAvailablePresets_ReturnsKnownPresets()
    {
        // Act
        var presets = NzbIndexerPresets.GetAvailablePresets();

        // Assert
        Assert.Contains("nzbgeek", presets);
        Assert.Contains("drunkenslug", presets);
        Assert.Contains("nzbfinder", presets);
    }
}
