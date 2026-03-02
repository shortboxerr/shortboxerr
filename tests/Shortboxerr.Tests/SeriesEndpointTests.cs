using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shortboxerr.Tests;

public class SeriesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SeriesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllSeries_ReturnsPagedResult()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/series");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("records", out _));
        Assert.True(doc.RootElement.TryGetProperty("page", out _));
        Assert.True(doc.RootElement.TryGetProperty("pageSize", out _));
        Assert.True(doc.RootElement.TryGetProperty("totalRecords", out _));
    }

    [Fact]
    public async Task CreateSeries_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            title = "Test Series " + Guid.NewGuid().ToString("N")[..8],
            publisher = "Test Publisher",
            startYear = 2024,
            monitored = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/series", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal(request.title, doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetSeriesById_NotFound_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/series/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSeries_ReturnsUpdated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            title = "Series To Update " + Guid.NewGuid().ToString("N")[..8],
            publisher = "Original Publisher"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/series", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var seriesId = createdDoc.RootElement.GetProperty("id").GetInt32();

        var updateRequest = new
        {
            publisher = "Updated Publisher"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/series/{seriesId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Updated Publisher", doc.RootElement.GetProperty("publisher").GetString());
    }

    [Fact]
    public async Task DeleteSeries_ReturnsOkWithDeletionInfo()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            title = "Series To Delete " + Guid.NewGuid().ToString("N")[..8]
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/series", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var seriesId = createdDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.DeleteAsync($"/api/v1/series/{seriesId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("totalDeleted").GetInt32() >= 1);

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/v1/series/{seriesId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetAllSeries_ReturnsCacheControlHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/series");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is not null);
        Assert.Contains("max-age", response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetSeriesById_ReturnsCacheControlAndETagHeaders()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a series first
        var createRequest = new
        {
            title = "Series For Cache Test " + Guid.NewGuid().ToString("N")[..8]
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/series", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var seriesId = createdDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.GetAsync($"/api/v1/series/{seriesId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is not null);
        Assert.Contains("max-age", response.Headers.CacheControl.ToString());
        Assert.True(response.Headers.ETag is not null);
        Assert.True(response.Content.Headers.LastModified is not null);
    }

    [Fact]
    public async Task GetSeriesById_WithIfNoneMatch_Returns304()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a series first
        var createRequest = new
        {
            title = "Series For ETag Test " + Guid.NewGuid().ToString("N")[..8]
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/series", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var seriesId = createdDoc.RootElement.GetProperty("id").GetInt32();

        // First request to get ETag
        var firstResponse = await client.GetAsync($"/api/v1/series/{seriesId}");
        var etag = firstResponse.Headers.ETag?.Tag;

        // Act - Second request with If-None-Match header
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/series/{seriesId}");
        request.Headers.Add("If-None-Match", etag);
        var secondResponse = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetSeriesIssues_ReturnsCacheControlHeader()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a series first
        var createRequest = new
        {
            title = "Series For Issues Cache Test " + Guid.NewGuid().ToString("N")[..8]
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/series", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var seriesId = createdDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.GetAsync($"/api/v1/series/{seriesId}/issues");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is not null);
        Assert.Contains("max-age", response.Headers.CacheControl.ToString());
    }
}

