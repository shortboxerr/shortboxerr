using Shortboxerr.Core.Search;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for search settings configuration.
/// </summary>
public static class SearchSettingsEndpoints
{
    public static void MapSearchSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/settings/search")
            .WithTags("Search Settings")
            .WithOpenApi();

        // GET search settings
        group.MapGet("/", async (ISearchSettingsService service, CancellationToken cancellationToken) =>
        {
            var settings = await service.GetSettingsAsync(cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetSearchSettings")
        .WithDescription("Gets the current search configuration settings.");

        // PUT update search settings
        group.MapPut("/", async (SearchSettings settings, ISearchSettingsService service, CancellationToken cancellationToken) =>
        {
            var errors = service.ValidateSettings(settings);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new { errors });
            }

            await service.SaveSettingsAsync(settings, cancellationToken);
            return Results.Ok(new { message = "Search settings saved successfully" });
        })
        .WithName("UpdateSearchSettings")
        .WithDescription("Updates search configuration settings.");

        // POST reset to defaults
        group.MapPost("/reset", async (ISearchSettingsService service, CancellationToken cancellationToken) =>
        {
            await service.ResetToDefaultsAsync(cancellationToken);
            var settings = await service.GetSettingsAsync(cancellationToken);
            return Results.Ok(new { message = "Search settings reset to defaults", settings });
        })
        .WithName("ResetSearchSettings")
        .WithDescription("Resets search settings to default values.");

        // POST validate settings
        group.MapPost("/validate", (SearchSettings settings, ISearchSettingsService service) =>
        {
            var errors = service.ValidateSettings(settings);
            return Results.Ok(new { 
                valid = errors.Count == 0, 
                errors 
            });
        })
        .WithName("ValidateSearchSettings")
        .WithDescription("Validates search settings without saving.");

        // GET defaults
        group.MapGet("/defaults", () =>
        {
            return Results.Ok(SearchSettings.Default);
        })
        .WithName("GetSearchSettingsDefaults")
        .WithDescription("Gets the default search settings.");
    }
}
