using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class ManualImportEndpoints
{
    public static void MapManualImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/manualimport")
            .WithTags("Manual Import")
            .WithOpenApi();

        // GET scan staging folder
        group.MapGet("/", async (IStagingService stagingService, CancellationToken cancellationToken) =>
        {
            var items = await stagingService.ScanStagingFolderAsync(cancellationToken);
            return Results.Ok(items.Select(StagedItemDto.FromModel));
        })
        .WithName("ScanStagingFolder")
        .WithDescription("Scans the staging folder and returns all importable files with parsed metadata.");

        // POST get import preview
        group.MapPost("/preview", async (
            IStagingService stagingService,
            ImportRequest request,
            CancellationToken cancellationToken) =>
        {
            var preview = await stagingService.GetImportPreviewAsync(
                request.SourcePath,
                request.SeriesId,
                request.IssueId,
                request.EditionId,
                cancellationToken);

            return Results.Ok(ImportPreviewDto.FromModel(preview));
        })
        .WithName("GetImportPreview")
        .WithDescription("Gets a preview of the import operation showing source, destination, and any warnings.");

        // POST execute import
        group.MapPost("/", async (
            IStagingService stagingService,
            ImportRequest request,
            CancellationToken cancellationToken) =>
        {
            var result = await stagingService.ImportAsync(
                request.SourcePath,
                request.SeriesId,
                request.IssueId,
                request.EditionId,
                cancellationToken);

            return result.Success
                ? Results.Ok(ImportResultDto.FromModel(result))
                : Results.BadRequest(ImportResultDto.FromModel(result));
        })
        .WithName("ExecuteImport")
        .WithDescription("Executes the import operation, moving the file to the library and creating database records.");

        // POST move to failed
        group.MapPost("/failed", async (
            IStagingService stagingService,
            string sourcePath,
            string reason,
            CancellationToken cancellationToken) =>
        {
            var success = await stagingService.MoveToFailedAsync(sourcePath, reason, cancellationToken);
            return success
                ? Results.Ok(new { message = "File moved to failed folder" })
                : Results.BadRequest(new { message = "Failed to move file" });
        })
        .WithName("MoveToFailed")
        .WithDescription("Moves a file to the failed folder with a reason.");
    }
}

