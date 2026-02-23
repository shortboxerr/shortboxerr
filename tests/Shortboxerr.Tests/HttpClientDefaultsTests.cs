using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Infrastructure;
using Shortboxerr.Infrastructure.Http;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for HttpClient default configuration including User-Agent headers.
/// </summary>
public class HttpClientDefaultsTests
{
    [Fact]
    public void UserAgent_ContainsApplicationName()
    {
        var userAgent = HttpClientDefaults.UserAgent;
        
        Assert.Contains("Shortboxerr", userAgent);
    }

    [Fact]
    public void UserAgent_ContainsVersion()
    {
        var userAgent = HttpClientDefaults.UserAgent;
        
        // Should contain a version pattern like "0.1.0" or "1.0.0"
        Assert.Matches(@"\d+\.\d+\.\d+", userAgent);
    }

    [Fact]
    public void UserAgent_ContainsProjectUrl()
    {
        var userAgent = HttpClientDefaults.UserAgent;
        
        Assert.Contains("github.com/shortboxerr", userAgent);
    }

    [Fact]
    public void UserAgent_MatchesExpectedFormat()
    {
        var userAgent = HttpClientDefaults.UserAgent;
        
        // Expected format: "Shortboxerr/x.y.z (+https://github.com/shortboxerr/shortboxerr)"
        Assert.Matches(@"^Shortboxerr/\d+\.\d+\.\d+ \(\+https://github\.com/shortboxerr/shortboxerr\)$", userAgent);
    }

    [Fact]
    public void HttpClient_FromFactory_HasUserAgentHeader()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure("Data Source=:memory:");
        var serviceProvider = services.BuildServiceProvider();
        
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        
        // Act
        var client = httpClientFactory.CreateClient();
        
        // Assert
        Assert.True(client.DefaultRequestHeaders.Contains("User-Agent"));
        
        var userAgentValues = client.DefaultRequestHeaders.GetValues("User-Agent");
        var userAgentString = string.Join(" ", userAgentValues);
        Assert.Contains("Shortboxerr", userAgentString);
    }

    [Fact]
    public void NamedHttpClient_HasUserAgentHeader()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure("Data Source=:memory:");
        var serviceProvider = services.BuildServiceProvider();
        
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        
        // Act - test a named client
        var client = httpClientFactory.CreateClient("CoverDownload");
        
        // Assert
        Assert.True(client.DefaultRequestHeaders.Contains("User-Agent"));
        
        var userAgentValues = client.DefaultRequestHeaders.GetValues("User-Agent");
        var userAgentString = string.Join(" ", userAgentValues);
        Assert.Contains("Shortboxerr", userAgentString);
    }

    [Fact]
    public void ApplicationName_IsCorrect()
    {
        Assert.Equal("Shortboxerr", HttpClientDefaults.ApplicationName);
    }

    [Fact]
    public void DefaultTimeoutSeconds_IsReasonable()
    {
        Assert.Equal(30, HttpClientDefaults.DefaultTimeoutSeconds);
    }

    [Fact]
    public void LongTimeoutSeconds_IsReasonable()
    {
        Assert.Equal(300, HttpClientDefaults.LongTimeoutSeconds);
    }
}
