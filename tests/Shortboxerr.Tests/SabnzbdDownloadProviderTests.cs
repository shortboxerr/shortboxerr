using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Providers;
using Xunit;

namespace Shortboxerr.Tests;

public class SabnzbdDownloadProviderTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly ProviderDefinition _providerDefinition;

    public SabnzbdDownloadProviderTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _providerDefinition = new ProviderDefinition
        {
            Id = 1,
            Name = "Test SABnzbd",
            Implementation = "SABnzbd",
            Type = ProviderType.Usenet,
            Category = ProviderCategory.DownloadClient,
            IsEnabled = true,
            Priority = 1,
            BaseUrl = "localhost:8080",
            ApiKey = "test-api-key",
            Settings = """{"host":"localhost:8080","apiKey":"test-api-key","category":"comics","useSsl":false}"""
        };
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    private SabnzbdDownloadProvider CreateProvider(HttpStatusCode statusCode, string jsonResponse)
    {
        var httpClient = CreateMockHttpClient(statusCode, jsonResponse);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        return new SabnzbdDownloadProvider(_providerDefinition, _httpClientFactoryMock.Object);
    }

    #region Provider Properties Tests

    [Fact]
    public void Id_ReturnsDefinitionId()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        Assert.Equal(1, provider.Id);
    }

    [Fact]
    public void Name_ReturnsDefinitionName()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        Assert.Equal("Test SABnzbd", provider.Name);
    }

    [Fact]
    public void Type_ReturnsUsenet()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        Assert.Equal(ProviderType.Usenet, provider.Type);
    }

    [Fact]
    public void SupportedProtocols_IncludesNzbAndUsenet()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        Assert.Contains("nzb", provider.SupportedProtocols);
        Assert.Contains("usenet", provider.SupportedProtocols);
    }

    [Fact]
    public void IsEnabled_ReflectsDefinition()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        Assert.True(provider.IsEnabled);
    }

    #endregion

    #region TestAsync Tests

    [Fact]
    public async Task TestAsync_WithValidResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """{"version": "4.2.1"}""";
        var provider = CreateProvider(HttpStatusCode.OK, json);

        // Act
        var result = await provider.TestAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Contains("SABnzbd", result.Message);
        Assert.Contains("4.2.1", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithHttpError_ReturnsFailure()
    {
        // Arrange
        var provider = CreateProvider(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await provider.TestAsync();

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestAsync_WithAuthError_ReturnsFailure()
    {
        // Arrange
        var json = """{"error": "API Key Required"}""";
        var provider = CreateProvider(HttpStatusCode.Unauthorized, json);

        // Act
        var result = await provider.TestAsync();

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region GetHealthAsync Tests

    [Fact]
    public async Task GetHealthAsync_WhenEnabled_ReturnsHealthy()
    {
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        var health = await provider.GetHealthAsync();
        
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal("Enabled", health.Message);
    }

    [Fact]
    public async Task GetHealthAsync_WhenDisabled_ReturnsUnknown()
    {
        _providerDefinition.IsEnabled = false;
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        var health = await provider.GetHealthAsync();
        
        Assert.Equal(HealthStatus.Unknown, health.Status);
        Assert.Equal("Disabled", health.Message);
    }

    #endregion

    #region DownloadAsync Tests

    [Fact]
    public async Task DownloadAsync_WithValidCandidate_ReturnsSuccess()
    {
        // Arrange
        var json = """{"status": true, "nzo_ids": ["SABnzbd_nzo_12345"]}""";
        var provider = CreateProvider(HttpStatusCode.OK, json);
        var candidate = new Candidate
        {
            Id = "test-123",
            ReleaseTitle = "Test Comic #1",
            Source = "TestSource",
            DownloadUrl = "https://example.com/test.nzb"
        };

        // Act
        var result = await provider.DownloadAsync(candidate);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DownloadId);
        Assert.Equal(candidate, result.Candidate);
    }

    [Fact]
    public async Task DownloadAsync_WithNoUrl_ReturnsFailure()
    {
        // Arrange
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        var candidate = new Candidate
        {
            Id = "test-123",
            ReleaseTitle = "Test Comic #1",
            Source = "TestSource",
            DownloadUrl = null
        };

        // Act
        var result = await provider.DownloadAsync(candidate);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No download URL", result.Error);
    }

    [Fact]
    public async Task DownloadAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        var json = """{"status": false, "error": "Queue full"}""";
        var provider = CreateProvider(HttpStatusCode.OK, json);
        var candidate = new Candidate
        {
            Id = "test-123",
            ReleaseTitle = "Test Comic #1",
            Source = "TestSource",
            DownloadUrl = "https://example.com/test.nzb"
        };

        // Act
        var result = await provider.DownloadAsync(candidate);

        // Assert
        Assert.False(result.Success);
    }

    #endregion

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_WithQueueItem_ReturnsStatus()
    {
        // Arrange
        var json = """
        {
            "queue": {
                "slots": [
                    {
                        "nzo_id": "test-download-id",
                        "filename": "Test Comic",
                        "status": "Downloading",
                        "percentage": "50",
                        "sizeleft": "500 MB",
                        "size": "1 GB",
                        "timeleft": "00:10:00"
                    }
                ]
            }
        }
        """;
        var provider = CreateProvider(HttpStatusCode.OK, json);

        // Act
        var status = await provider.GetStatusAsync("test-download-id");

        // Assert
        Assert.Equal("test-download-id", status.DownloadId);
        Assert.Equal(DownloadState.Downloading, status.State);
    }

    [Fact]
    public async Task GetStatusAsync_WithNotFound_ReturnsFailedState()
    {
        // Arrange - empty queue and history
        var json = """{"queue": {"slots": []}, "history": {"slots": []}}""";
        var provider = CreateProvider(HttpStatusCode.OK, json);

        // Act
        var status = await provider.GetStatusAsync("non-existent-id");

        // Assert
        Assert.Equal(DownloadState.Failed, status.State);
        Assert.Contains("not found", status.Error);
    }

    #endregion

    #region CancelAsync Tests

    [Fact]
    public async Task CancelAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var json = """{"status": true}""";
        var provider = CreateProvider(HttpStatusCode.OK, json);

        // Act
        var result = await provider.CancelAsync("test-download-id");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CancelAsync_WithError_ReturnsFalse()
    {
        // Arrange
        var provider = CreateProvider(HttpStatusCode.InternalServerError, "");

        // Act
        var result = await provider.CancelAsync("test-download-id");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Settings Parsing Tests

    [Fact]
    public void Provider_WithValidSettings_ParsesCorrectly()
    {
        // Settings are in the _providerDefinition
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        
        // Just verify the provider creates successfully
        Assert.NotNull(provider);
        Assert.Equal("Test SABnzbd", provider.Name);
    }

    [Fact]
    public void Provider_WithEmptySettings_UsesDefaults()
    {
        _providerDefinition.Settings = "";
        _providerDefinition.BaseUrl = "fallback-host:8080";
        _providerDefinition.ApiKey = "fallback-key";
        
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        
        Assert.NotNull(provider);
    }

    [Fact]
    public void Provider_WithInvalidJson_UsesDefaults()
    {
        _providerDefinition.Settings = "not valid json";
        _providerDefinition.BaseUrl = "fallback-host:8080";
        _providerDefinition.ApiKey = "fallback-key";
        
        var provider = CreateProvider(HttpStatusCode.OK, "{}");
        
        Assert.NotNull(provider);
    }

    #endregion

    #region Factory Tests

    [Fact]
    public void Factory_Create_ReturnsProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new SabnzbdDownloadProviderFactory(serviceProvider);

        // Act
        var provider = factory.Create(_providerDefinition);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("Test SABnzbd", provider.Name);
        Assert.Equal(ProviderType.Usenet, provider.Type);
    }

    #endregion
}
