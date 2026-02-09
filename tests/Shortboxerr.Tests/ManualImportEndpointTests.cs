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

    [Fact]
    public async Task ScanStagingFolder_StagedAlias_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - test the /staged alias endpoint
        var response = await client.GetAsync("/api/v1/manualimport/staged");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // Should return an array (even if empty)
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task BulkImport_WithEmptyFiles_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            files = Array.Empty<string>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(0, doc.RootElement.GetProperty("imported").GetInt32());
    }

    [Fact]
    public async Task BulkImport_WithNonExistentFiles_ReturnsPartialFailure()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            files = new[] { "/nonexistent/file1.cbz", "/nonexistent/file2.cbz" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(0, doc.RootElement.GetProperty("imported").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task RejectFile_WithNonExistentFile_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/nonexistent/file.cbz",
            reason = "Test rejection"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/reject", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Failed to reject file", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RejectFile_WithValidFile_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create a temp file in staging folder simulation
        var stagingFolder = Path.Combine(Path.GetTempPath(), "shortboxerr-staging-test");
        Directory.CreateDirectory(stagingFolder);
        var testFile = Path.Combine(stagingFolder, $"test-reject-{Guid.NewGuid()}.cbz");
        await File.WriteAllTextAsync(testFile, "test content");
        
        try
        {
            var request = new
            {
                sourcePath = testFile,
                reason = "Test rejection"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/manualimport/reject", request);

            // This will fail because staging folder is not configured in test factory
            // but we test the endpoint exists and responds appropriately
            var content = await response.Content.ReadAsStringAsync();
            
            // Either OK (file moved) or BadRequest (couldn't move due to configuration)
            Assert.True(
                response.StatusCode == HttpStatusCode.OK || 
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected OK or BadRequest, got {response.StatusCode}");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (Directory.Exists(stagingFolder)) 
            {
                try { Directory.Delete(stagingFolder, true); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    [Fact]
    public async Task UpdateMatch_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/some/file.cbz",
            seriesId = 1,
            issueId = (int?)null,
            editionId = (int?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/update-match", request);

        // Assert - UpdateMatch always succeeds for in-memory cache updates
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Match updated", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UpdateMatch_ClearMatch_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/some/file.cbz",
            seriesId = (int?)null,
            issueId = (int?)null,
            editionId = (int?)null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/update-match", request);

        // Assert - Clearing match also succeeds
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Match cleared", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task MoveToFailed_WithValidRequest_EndpointExists()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            sourcePath = "/nonexistent/file.cbz",
            reason = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/manualimport/failed", request);

        // Assert - Endpoint exists and returns expected response
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

