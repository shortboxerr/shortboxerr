using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Mylar3Migration;

namespace Shortboxerr.Api.Endpoints;

public static class Mylar3ImportEndpoints
{
    public static void MapMylar3ImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mylar3")
            .WithTags("Mylar3 Import")
            .WithOpenApi();

        #region Full Database Migration

        // POST /api/v1/mylar3/migration/analyze - Analyze Mylar3 database
        group.MapPost("/migration/analyze", async (
            IMylar3MigrationService migrationService,
            [FromBody] AnalyzeDatabaseRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.DatabasePath))
            {
                return Results.BadRequest(new { message = "Database path is required" });
            }

            var result = await migrationService.AnalyzeDatabaseAsync(request.DatabasePath, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("AnalyzeMylar3Database");

        // POST /api/v1/mylar3/migration/export - Export snapshot to JSON
        group.MapPost("/migration/export", async (
            IMylar3MigrationService migrationService,
            [FromBody] ExportSnapshotRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.Snapshot == null)
            {
                return Results.BadRequest(new { message = "Snapshot is required" });
            }
            if (string.IsNullOrEmpty(request.OutputPath))
            {
                return Results.BadRequest(new { message = "Output path is required" });
            }

            try
            {
                var path = await migrationService.ExportSnapshotAsync(
                    request.Snapshot, request.OutputPath, cancellationToken);
                return Results.Ok(new { path, message = "Snapshot exported successfully" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = $"Export failed: {ex.Message}" });
            }
        })
        .WithName("ExportMylar3Snapshot");

        // POST /api/v1/mylar3/migration/import - Import from snapshot
        group.MapPost("/migration/import", async (
            IMylar3MigrationService migrationService,
            [FromBody] ImportFromSnapshotRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.Snapshot == null)
            {
                return Results.BadRequest(new { message = "Snapshot is required" });
            }

            var result = await migrationService.ImportAsync(
                request.Snapshot,
                request.Options ?? new Mylar3MigrationOptions(),
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("ImportMylar3Snapshot");

        // POST /api/v1/mylar3/migration/migrate - Full migration (analyze + import)
        group.MapPost("/migration/migrate", async (
            IMylar3MigrationService migrationService,
            [FromBody] FullMigrationRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.DatabasePath))
            {
                return Results.BadRequest(new { message = "Database path is required" });
            }

            var result = await migrationService.MigrateAsync(
                request.DatabasePath,
                request.Options ?? new Mylar3MigrationOptions(),
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error, result });
        })
        .WithName("MigrateMylar3Database");

        #endregion

        #region ComicVine Settings Import

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

        #endregion
    }
}

#region Request DTOs

// Full Migration DTOs
public record AnalyzeDatabaseRequest
{
    public string? DatabasePath { get; init; }
}

public record ExportSnapshotRequest
{
    public Mylar3Snapshot? Snapshot { get; init; }
    public string? OutputPath { get; init; }
}

public record ImportFromSnapshotRequest
{
    public Mylar3Snapshot? Snapshot { get; init; }
    public Mylar3MigrationOptions? Options { get; init; }
}

public record FullMigrationRequest
{
    public string? DatabasePath { get; init; }
    public Mylar3MigrationOptions? Options { get; init; }
}

// ComicVine Import DTOs
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
