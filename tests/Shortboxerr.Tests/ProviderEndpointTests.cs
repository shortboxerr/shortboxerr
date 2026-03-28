using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shortboxerr.Tests;

public class ProviderEndpointTests : BaseEndpointTest
{
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProviderEndpointTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAllProviders_ReturnsEmptyList()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/v1/providers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task GetIndexers_ReturnsEmptyList()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/v1/providers/indexers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetDownloadClients_ReturnsEmptyList()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/v1/providers/downloadclients");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetImplementations_ReturnsAvailableTypes()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/v1/providers/implementations");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
        
        // Should contain RSS indexer (only user-configurable provider)
        // Note: DDL and HTTP download clients are built-in services, not user-configurable providers
        var hasRss = doc.RootElement.EnumerateArray()
            .Any(e => e.GetProperty("name").GetString() == "RssIndexer");
        Assert.True(hasRss);
    }

    [Fact]
    public async Task CreateIndexer_ReturnsCreated()
    {
        // Arrange
        var client = _client;
        var request = new
        {
            name = "Test RSS Indexer",
            implementation = "RssIndexer",
            isEnabled = true,
            baseUrl = "https://example.com/feed"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/providers/indexers", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        Assert.Equal("Test RSS Indexer", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("RssIndexer", doc.RootElement.GetProperty("implementation").GetString());
        Assert.True(doc.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task CreateIndexer_WithInvalidImplementation_ReturnsBadRequest()
    {
        // Arrange
        var client = _client;
        var request = new
        {
            name = "Invalid Provider",
            implementation = "NonExistentProvider",
            isEnabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/providers/indexers", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDownloadClient_WithNoImplementations_ReturnsBadRequest()
    {
        // Note: HTTP Download Client is now a built-in service, not a user-configurable provider.
        // External download clients (torrent, usenet) are planned for EPIC 10+.
        // Arrange
        var client = _client;
        var request = new
        {
            name = "Test Download Client",
            implementation = "HttpDownloadClient", // Not registered as a provider
            isEnabled = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/providers/downloadclients", request);

        // Assert - No download client implementations exist currently
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProviderById_NotFound_Returns404()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/v1/providers/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProvider_ReturnsUpdated()
    {
        // Arrange
        var client = _client;
        
        // First create a provider
        var createRequest = new
        {
            name = "Original Name",
            implementation = "RssIndexer",
            isEnabled = true,
            baseUrl = "https://example.com/feed"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/providers/indexers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        // Update
        var updateRequest = new
        {
            name = "Updated Name",
            isEnabled = false
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/providers/{id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        Assert.Equal("Updated Name", doc.RootElement.GetProperty("name").GetString());
        Assert.False(doc.RootElement.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task DeleteProvider_ReturnsNoContent()
    {
        // Arrange
        var client = _client;
        
        // First create a provider
        var createRequest = new
        {
            name = "To Delete",
            implementation = "RssIndexer",
            isEnabled = true,
            baseUrl = "https://example.com/feed"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/providers/indexers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.DeleteAsync($"/api/v1/providers/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/v1/providers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task TestProvider_ReturnsTestResult()
    {
        // Arrange
        var client = _client;
        
        // First create a provider
        var createRequest = new
        {
            name = "Test Provider",
            implementation = "RssIndexer",
            isEnabled = true,
            baseUrl = "https://example.com/feed"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/providers/indexers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.PostAsync($"/api/v1/providers/{id}/test", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        // Null provider returns not implemented (RssIndexer uses NullIndexerProvider)
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("not yet implemented", doc.RootElement.GetProperty("message").GetString()?.ToLower());
    }

    [Fact]
    public async Task TestNewProvider_WithoutSaving_ReturnsTestResult()
    {
        // Arrange
        var client = _client;
        var request = new
        {
            name = "Unsaved Provider",
            implementation = "RssIndexer",
            baseUrl = "https://test.example.com/feed"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/providers/test", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        Assert.NotNull(doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SetProviderEnabled_TogglesStatus()
    {
        // Arrange
        var client = _client;
        
        // Create provider
        var createRequest = new
        {
            name = "Toggle Test",
            implementation = "RssIndexer",
            isEnabled = true,
            baseUrl = "https://example.com/feed"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/providers/indexers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        // Act - disable
        var response = await client.PostAsync($"/api/v1/providers/{id}/enable?enabled=false", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify disabled
        var getResponse = await client.GetAsync($"/api/v1/providers/{id}");
        var content = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("Disabled", doc.RootElement.GetProperty("lastHealthStatus").GetString());
    }

    [Fact]
    public async Task Swagger_IncludesProviderEndpoints()
    {
        // Arrange
        var client = _client;

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("/api/v1/providers", content);
        Assert.Contains("GetAllProviders", content);
        Assert.Contains("GetIndexers", content);
        Assert.Contains("GetDownloadClients", content);
    }
}

