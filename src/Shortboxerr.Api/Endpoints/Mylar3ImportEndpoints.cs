using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class Mylar3ImportEndpoints
{
    public static void MapMylar3ImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mylar3")
            .WithTags("Mylar3 Import")
            .WithOpenApi();

        // Parse ComicVine settings from config content
        group.MapPost("/comicvine/parse", (
            IMylar3ComicVineImporter importer,
            [FromBody] ParseConfigRequest request) =>
        {
            if (string.IsNullOrEmpty(request.ConfigContent))
            {
                return Results.BadRequest(new { message = "Config content is required" });
            }

            var result = importer.ParseComicVineSettings(request.ConfigContent);
            return Results.Ok(result);
        })
        .WithName("ParseMylar3ComicVineSettings");

        // Parse ComicVine settings from file path
        group.MapPost("/comicvine/parse-file", async (
            IMylar3ComicVineImporter importer,
            [FromBody] ParseFileRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return Results.BadRequest(new { message = "File path is required" });
            }

            var result = await importer.ParseComicVineSettingsFileAsync(request.FilePath, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("ParseMylar3ComicVineSettingsFile");

        // Import ComicVine settings
        group.MapPost("/comicvine/import", async (
            IMylar3ComicVineImporter importer,
            [FromBody] ImportComicVineSettingsRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.Settings == null)
            {
                return Results.BadRequest(new { message = "Settings are required" });
            }

            var result = await importer.ImportComicVineSettingsAsync(
                request.Settings,
                request.Options ?? new ComicVineImportOptions(),
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("ImportMylar3ComicVineSettings");

        // Validate ComicVine IDs from Mylar3 database
        group.MapPost("/comicvine/validate-ids", async (
            IMylar3ComicVineImporter importer,
            [FromBody] ValidateIdsRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.DatabasePath))
            {
                return Results.BadRequest(new { message = "Database path is required" });
            }

            var result = await importer.ValidateComicVineIdsAsync(request.DatabasePath, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("ValidateMylar3ComicVineIds");

        // Migrate ComicVine IDs from Mylar3 database
        group.MapPost("/comicvine/migrate-ids", async (
            IMylar3ComicVineImporter importer,
            [FromBody] MigrateIdsRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.DatabasePath))
            {
                return Results.BadRequest(new { message = "Database path is required" });
            }

            var result = await importer.MigrateComicVineIdsAsync(
                request.DatabasePath,
                request.Options ?? new ComicVineIdMigrationOptions(),
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("MigrateMylar3ComicVineIds");
    }
}

#region Request DTOs

public record ParseConfigRequest
{
    public string? ConfigContent { get; init; }
}

public record ParseFileRequest
{
    public string? FilePath { get; init; }
}

public record ImportComicVineSettingsRequest
{
    public Mylar3ComicVineSettings? Settings { get; init; }
    public ComicVineImportOptions? Options { get; init; }
}

public record ValidateIdsRequest
{
    public string? DatabasePath { get; init; }
}

public record MigrateIdsRequest
{
    public string? DatabasePath { get; init; }
    public ComicVineIdMigrationOptions? Options { get; init; }
}

#endregion
