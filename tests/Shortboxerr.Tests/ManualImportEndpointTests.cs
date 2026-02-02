using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shortboxerr.Tests;

public class ManualImportEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ManualImportEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ScanStagingFolder_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/manualimport");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // Should return an array (even if empty)
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task GetImportPreview_WithNonExistentFile_ReturnsCannotImport()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/nonexistent/file.cbz",
            seriesId = (int?)null,
            issueId = (int?)null,
            editionId = (int?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/preview", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("canImport").GetBoolean());
        Assert.Equal("Source file does not exist", doc.RootElement.GetProperty("blockReason").GetString());
    }

    [Fact]
    public async Task GetImportPreview_WithInvalidSeriesId_ReturnsCannotImport()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a temp file to test with
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");
        
        try
        {
            var request = new
            {
                sourcePath = tempFile,
                seriesId = 99999, // Non-existent series
                issueId = (int?)null,
                editionId = (int?)null
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/manualimport/preview", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            Assert.False(doc.RootElement.GetProperty("canImport").GetBoolean());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteImport_WithNonExistentFile_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/nonexistent/file.cbz"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Swagger_IncludesManualImportEndpoints()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/manualimport", content);
        Assert.Contains("Manual Import", content);
    }
}

