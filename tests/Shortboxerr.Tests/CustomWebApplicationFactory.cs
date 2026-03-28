using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

/// <summary>
/// Custom web application factory that uses an in-memory SQLite database
/// to isolate tests from each other.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private SqliteConnection? _connection;

    /// <summary>
    /// Test API key used for all test requests.
    /// </summary>
    public const string TestApiKey = "test-api-key-for-integration-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ShortboxerrDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove background services to prevent delays during testing
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServiceDescriptors)
            {
                services.Remove(hostedService);
            }

            // Create and open an in-memory SQLite connection
            // This connection must stay open for the lifetime of the factory
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add DbContext using the shared connection
            services.AddDbContext<ShortboxerrDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create schema after host is built
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        db.Database.EnsureCreated();

        // Seed test API key into the database
        SeedTestApiKey(db);

        return host;
    }

    /// <summary>
    /// Seeds a test API key into the database so tests can authenticate.
    /// </summary>
    private static void SeedTestApiKey(ShortboxerrDbContext db)
    {
        const string apiKeyValueKey = "security.apiKey";
        const string apiKeyEnabledKey = "security.apiKeyEnabled";
        const string apiKeyCreatedAtKey = "security.apiKeyCreatedAt";

        var existingKey = db.SystemSettings.FirstOrDefault(s => s.Key == apiKeyValueKey);
        if (existingKey != null)
        {
            return; // Already seeded
        }

        db.SystemSettings.Add(new SystemSetting { Key = apiKeyValueKey, Value = TestApiKey });
        db.SystemSettings.Add(new SystemSetting { Key = apiKeyEnabledKey, Value = "true" });
        db.SystemSettings.Add(new SystemSetting { Key = apiKeyCreatedAtKey, Value = DateTime.UtcNow.ToString("O") });

        db.SaveChanges();
    }

    /// <summary>
    /// Creates an HTTP client with the test API key automatically added to all requests.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

