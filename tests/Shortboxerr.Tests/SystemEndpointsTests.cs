using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Shortboxerr.Tests;

public class SystemEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SystemEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/info");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSystemInfo_ContainsRequiredFields()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/info");
        var content = await response.Content.ReadFromJsonAsync<SystemInfoTestResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.Equal("Shortboxerr", content.AppName);
        Assert.NotNull(content.Version);
        Assert.NotNull(content.RuntimeVersion);
        Assert.NotNull(content.OsDescription);
        Assert.NotNull(content.OsArchitecture);
        Assert.NotNull(content.ProcessArchitecture);
        Assert.NotNull(content.DatabaseProvider);
        Assert.NotNull(content.DataDirectory);
        Assert.NotNull(content.LogDirectory);
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsValidMemoryInfo()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/info");
        var content = await response.Content.ReadFromJsonAsync<SystemInfoTestResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.True(content.WorkingSetBytes > 0, "WorkingSetBytes should be positive");
        Assert.True(content.GcTotalMemoryBytes > 0, "GcTotalMemoryBytes should be positive");
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsValidUptime()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/info");
        var content = await response.Content.ReadFromJsonAsync<SystemInfoTestResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.True(content.StartTime <= DateTime.UtcNow, "StartTime should be in the past");
    }

    [Fact]
    public async Task GetSystemStatus_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSystemStatus_ContainsRequiredFields()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/status");
        var content = await response.Content.ReadFromJsonAsync<SystemStatusTestResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.Equal("Shortboxerr", content.AppName);
        Assert.NotNull(content.Version);
        Assert.True(content.IsHealthy);
        Assert.True(content.WorkingSetMb > 0);
    }

    [Fact]
    public async Task GetSystemStatus_ContainsStatistics()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/status");
        var content = await response.Content.ReadFromJsonAsync<SystemStatusTestResponse>();

        // Assert - verify statistics fields are present (can be zero in test environment)
        Assert.NotNull(content);
        Assert.True(content.SeriesCount >= 0);
        Assert.True(content.IssuesCount >= 0);
        Assert.True(content.CollectionsCount >= 0);
        Assert.True(content.FilesCount >= 0);
        Assert.True(content.EnabledIndexers >= 0);
        Assert.NotNull(content.IndexerStatus);
        Assert.NotNull(content.DatabaseStatus);
        Assert.True(content.QueuedDownloads >= 0);
    }

    [Fact]
    public async Task GetLogFiles_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/logs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogFiles_ContainsLogDirectory()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/system/logs");
        var content = await response.Content.ReadFromJsonAsync<LogFilesTestResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.NotNull(content.LogDirectory);
        Assert.NotNull(content.Files);
    }

    // Test response DTOs (minimal for deserialization)
    private class SystemInfoTestResponse
    {
        public string? AppName { get; set; }
        public string? Version { get; set; }
        public string? RuntimeVersion { get; set; }
        public string? OsDescription { get; set; }
        public string? OsArchitecture { get; set; }
        public string? ProcessArchitecture { get; set; }
        public string? DatabaseProvider { get; set; }
        public string? DataDirectory { get; set; }
        public string? LogDirectory { get; set; }
        public long WorkingSetBytes { get; set; }
        public long GcTotalMemoryBytes { get; set; }
        public DateTime StartTime { get; set; }
    }

    private class SystemStatusTestResponse
    {
        public string? AppName { get; set; }
        public string? Version { get; set; }
        public bool IsHealthy { get; set; }
        public double WorkingSetMb { get; set; }
        public int SeriesCount { get; set; }
        public int IssuesCount { get; set; }
        public int CollectionsCount { get; set; }
        public int FilesCount { get; set; }
        public int EnabledIndexers { get; set; }
        public string? IndexerStatus { get; set; }
        public string? DatabaseStatus { get; set; }
        public int QueuedDownloads { get; set; }
    }

    private class LogFilesTestResponse
    {
        public string? LogDirectory { get; set; }
        public List<object>? Files { get; set; }
    }
}
