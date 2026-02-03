using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Providers;

namespace Shortboxerr.Api.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/providers")
            .WithTags("Providers")
            .WithOpenApi();

        // GET all providers
        group.MapGet("/", async (IProviderManager manager) =>
        {
            var providers = await manager.GetAllAsync();
            return Results.Ok(providers.Select(ProviderDto.FromEntity));
        })
        .WithName("GetAllProviders")
        .WithDescription("Get all configured providers");

        // GET providers by category
        group.MapGet("/indexers", async (IProviderManager manager) =>
        {
            var providers = await manager.GetByCategoryAsync(ProviderCategory.Indexer);
            return Results.Ok(providers.Select(ProviderDto.FromEntity));
        })
        .WithName("GetIndexers")
        .WithDescription("Get all indexer providers");

        group.MapGet("/downloadclients", async (IProviderManager manager) =>
        {
            var providers = await manager.GetByCategoryAsync(ProviderCategory.DownloadClient);
            return Results.Ok(providers.Select(ProviderDto.FromEntity));
        })
        .WithName("GetDownloadClients")
        .WithDescription("Get all download client providers");

        // GET provider by ID
        group.MapGet("/{id:int}", async (int id, IProviderManager manager) =>
        {
            var provider = await manager.GetByIdAsync(id);
            return provider != null 
                ? Results.Ok(ProviderDto.FromEntity(provider))
                : Results.NotFound();
        })
        .WithName("GetProviderById")
        .WithDescription("Get a provider by ID");

        // GET available implementations
        group.MapGet("/implementations", (IProviderFactory factory) =>
        {
            var implementations = factory.GetImplementations();
            return Results.Ok(implementations.Select(i => new ProviderImplementationDto
            {
                Name = i.Name,
                DisplayName = i.DisplayName,
                Description = i.Description,
                Category = i.Category.ToString(),
                Type = i.Type.ToString(),
                RequiresBaseUrl = i.RequiresBaseUrl,
                RequiresApiKey = i.RequiresApiKey,
                RequiresCredentials = i.RequiresCredentials,
                SettingsSchema = i.SettingsSchema
            }));
        })
        .WithName("GetProviderImplementations")
        .WithDescription("Get all available provider implementations");

        // POST create indexer
        group.MapPost("/indexers", async (CreateProviderRequest request, IProviderManager manager, IProviderFactory factory) =>
        {
            var impl = factory.GetImplementation(request.Implementation);
            if (impl == null || impl.Category != ProviderCategory.Indexer)
            {
                return Results.BadRequest($"Invalid indexer implementation: {request.Implementation}");
            }

            var entity = request.ToEntity(ProviderCategory.Indexer, impl.Type);
            var created = await manager.CreateAsync(entity);
            
            return Results.Created($"/api/v1/providers/{created.Id}", ProviderDto.FromEntity(created));
        })
        .WithName("CreateIndexer")
        .WithDescription("Create a new indexer provider");

        // POST create download client
        group.MapPost("/downloadclients", async (CreateProviderRequest request, IProviderManager manager, IProviderFactory factory) =>
        {
            var impl = factory.GetImplementation(request.Implementation);
            if (impl == null || impl.Category != ProviderCategory.DownloadClient)
            {
                return Results.BadRequest($"Invalid download client implementation: {request.Implementation}");
            }

            var entity = request.ToEntity(ProviderCategory.DownloadClient, impl.Type);
            var created = await manager.CreateAsync(entity);
            
            return Results.Created($"/api/v1/providers/{created.Id}", ProviderDto.FromEntity(created));
        })
        .WithName("CreateDownloadClient")
        .WithDescription("Create a new download client provider");

        // PUT update provider
        group.MapPut("/{id:int}", async (int id, UpdateProviderRequest request, IProviderManager manager) =>
        {
            var existing = await manager.GetByIdAsync(id);
            if (existing == null)
            {
                return Results.NotFound();
            }

            // Apply updates
            if (request.Name != null) existing.Name = request.Name;
            if (request.IsEnabled.HasValue) existing.IsEnabled = request.IsEnabled.Value;
            if (request.Priority.HasValue) existing.Priority = request.Priority.Value;
            if (request.BaseUrl != null) existing.BaseUrl = request.BaseUrl;
            if (request.ApiKey != null) existing.ApiKey = request.ApiKey;
            if (request.Username != null) existing.Username = request.Username;
            if (request.Password != null) existing.Password = request.Password;
            if (request.Settings != null) existing.Settings = request.Settings;
            if (request.Tags != null) existing.Tags = request.Tags;

            var updated = await manager.UpdateAsync(existing);
            return Results.Ok(ProviderDto.FromEntity(updated));
        })
        .WithName("UpdateProvider")
        .WithDescription("Update a provider");

        // DELETE provider
        group.MapDelete("/{id:int}", async (int id, IProviderManager manager) =>
        {
            var deleted = await manager.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteProvider")
        .WithDescription("Delete a provider");

        // POST enable/disable provider
        group.MapPost("/{id:int}/enable", async (int id, bool enabled, IProviderManager manager) =>
        {
            var success = await manager.SetEnabledAsync(id, enabled);
            return success ? Results.Ok() : Results.NotFound();
        })
        .WithName("SetProviderEnabled")
        .WithDescription("Enable or disable a provider");

        // POST reorder indexers
        group.MapPost("/indexers/reorder", async (ReorderProvidersRequest request, IProviderManager manager) =>
        {
            await manager.ReorderAsync(ProviderCategory.Indexer, request.OrderedIds);
            return Results.Ok();
        })
        .WithName("ReorderIndexers")
        .WithDescription("Reorder indexer providers");

        // POST reorder download clients
        group.MapPost("/downloadclients/reorder", async (ReorderProvidersRequest request, IProviderManager manager) =>
        {
            await manager.ReorderAsync(ProviderCategory.DownloadClient, request.OrderedIds);
            return Results.Ok();
        })
        .WithName("ReorderDownloadClients")
        .WithDescription("Reorder download client providers");

        // POST test provider
        group.MapPost("/{id:int}/test", async (int id, IProviderManager manager) =>
        {
            var result = await manager.TestAsync(id);
            return Results.Ok(ProviderTestResultDto.FromResult(result));
        })
        .WithName("TestProvider")
        .WithDescription("Test a provider's connection and configuration");

        // POST test provider before saving (without ID)
        group.MapPost("/test", async (CreateProviderRequest request, IProviderManager manager, IProviderFactory factory) =>
        {
            var impl = factory.GetImplementation(request.Implementation);
            if (impl == null)
            {
                return Results.BadRequest($"Unknown implementation: {request.Implementation}");
            }

            var entity = request.ToEntity(impl.Category, impl.Type);
            var result = await manager.TestAsync(entity);
            return Results.Ok(ProviderTestResultDto.FromResult(result));
        })
        .WithName("TestNewProvider")
        .WithDescription("Test a provider configuration before saving");
    }
}



