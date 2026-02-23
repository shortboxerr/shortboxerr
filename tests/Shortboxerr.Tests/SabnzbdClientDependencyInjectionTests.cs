using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for SabnzbdClient dependency injection configuration.
/// </summary>
public class SabnzbdClientDependencyInjectionTests
{
    [Fact]
    public void SabnzbdClient_CanBeResolvedFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure("Data Source=:memory:");
        
        // Configure SabnzbdSettings (required for DI resolution)
        services.Configure<SabnzbdSettings>(options =>
        {
            options.Host = "http://localhost:8080";
            options.ApiKey = "test-api-key";
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - This should not throw due to constructor ambiguity
        var client = serviceProvider.GetService<ISabnzbdClient>();
        
        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void SabnzbdClient_ViaHttpClientFactory_CanBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddInfrastructure("Data Source=:memory:");
        
        // Configure SabnzbdSettings
        services.Configure<SabnzbdSettings>(options =>
        {
            options.Host = "http://localhost:8080";
            options.ApiKey = "test-api-key";
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        
        // Act - Create a named HttpClient (the underlying mechanism)
        var httpClient = httpClientFactory.CreateClient(nameof(ISabnzbdClient));
        
        // Assert - HttpClient should be created without error
        Assert.NotNull(httpClient);
    }

    [Fact]
    public void SabnzbdClient_DirectInstantiation_WorksWithExplicitSettings()
    {
        // Arrange
        var httpClient = new HttpClient();
        var settings = new SabnzbdSettings
        {
            Host = "http://localhost:8080",
            ApiKey = "test-api-key"
        };
        
        // Act - Direct instantiation for testing should work
        var client = new Shortboxerr.Infrastructure.Nzb.SabnzbdClient(httpClient, settings);
        
        // Assert
        Assert.NotNull(client);
        Assert.Equal(NzbDownloadClientType.SABnzbd, client.ClientType);
    }
}
