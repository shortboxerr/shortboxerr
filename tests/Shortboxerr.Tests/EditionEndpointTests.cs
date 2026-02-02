using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shortboxerr.Tests;

public class EditionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EditionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllEditions_ReturnsPagedResult()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/editions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("records", out _));
        Assert.True(doc.RootElement.TryGetProperty("page", out _));
        Assert.True(doc.RootElement.TryGetProperty("totalRecords", out _));
    }

    [Fact]
    public async Task CreateEdition_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            title = "Test TPB " + Guid.NewGuid().ToString("N")[..8],
            editionType = 0, // TradesPaperback
            isbn = "978-1234567890",
            monitored = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/editions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal(request.title, doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CreateEdition_WithInvalidSeriesId_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            title = "Test Edition",
            seriesId = 99999 // Non-existent series
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/editions", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEditionById_NotFound_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/editions/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEdition_ReturnsUpdated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            title = "Edition To Update " + Guid.NewGuid().ToString("N")[..8],
            isbn = "978-0000000000"
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/editions", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var editionId = createdDoc.RootElement.GetProperty("id").GetInt32();

        var updateRequest = new
        {
            isbn = "978-1111111111"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/editions/{editionId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("978-1111111111", doc.RootElement.GetProperty("isbn").GetString());
    }

    [Fact]
    public async Task DeleteEdition_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            title = "Edition To Delete " + Guid.NewGuid().ToString("N")[..8]
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/editions", createRequest);
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdContent);
        var editionId = createdDoc.RootElement.GetProperty("id").GetInt32();

        // Act
        var response = await client.DeleteAsync($"/api/v1/editions/{editionId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/v1/editions/{editionId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateEdition_WithSeries_LinksCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create a series first
        var seriesRequest = new { title = "Series for Edition " + Guid.NewGuid().ToString("N")[..8] };
        var seriesResponse = await client.PostAsJsonAsync("/api/v1/series", seriesRequest);
        var seriesContent = await seriesResponse.Content.ReadAsStringAsync();
        using var seriesDoc = JsonDocument.Parse(seriesContent);
        var seriesId = seriesDoc.RootElement.GetProperty("id").GetInt32();

        // Create edition linked to series
        var editionRequest = new
        {
            title = "Linked Edition " + Guid.NewGuid().ToString("N")[..8],
            seriesId = seriesId
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/editions", editionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(seriesId, doc.RootElement.GetProperty("seriesId").GetInt32());
    }
}

