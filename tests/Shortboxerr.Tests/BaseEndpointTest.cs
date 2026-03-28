namespace Shortboxerr.Tests;

/// <summary>
/// Base class for endpoint tests that provides an authenticated HTTP client.
/// All tests that make API calls should inherit from this and use _client.
/// </summary>
public abstract class BaseEndpointTest : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient _client;

    protected BaseEndpointTest(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }
}
