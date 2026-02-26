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

        // GET scan staging folder (root path)
        group.MapGet("/", async (IStagingService stagingService, CancellationToken cancellationToken) =>
        {
            var items = await stagingService.ScanStagingFolderAsync(cancellationToken);
            return Results.Ok(items.Select(StagedItemDto.FromModel));
        })
        .WithName("ScanStagingFolder")
        .WithDescription("Scans the staging folder and returns all importable files with parsed metadata.");

        // GET /staged - alias for UI compatibility
        group.MapGet("/staged", async (IStagingService stagingService, CancellationToken cancellationToken) =>
        {
            var items = await stagingService.ScanStagingFolderAsync(cancellationToken);
            return Results.Ok(items.Select(StagedItemDto.FromModel));
        })
        .WithName("ScanStagingFolderAlias")
        .WithDescription("Alias for ScanStagingFolder - scans the staging folder and returns all importable files.");

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

        // POST execute import (root path)
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

        // POST /import - bulk import for UI compatibility
        group.MapPost("/import", async (
            IStagingService stagingService,
            BulkImportRequest request,
            CancellationToken cancellationToken) =>
        {
            var results = new List<ImportResultDto>();
            var errors = new List<string>();

            // Get staging scan to retrieve matched series/edition IDs
            var stagedFiles = await stagingService.ScanStagingFolderAsync(cancellationToken);
            var stagedLookup = stagedFiles.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in request.Files)
            {
                // Look up the matched series/edition from the staging scan
                int? seriesId = null;
                int? editionId = null;
                
                if (stagedLookup.TryGetValue(filePath, out var stagedItem))
                {
                    seriesId = stagedItem.SuggestedSeriesId;
                    editionId = stagedItem.SuggestedEditionId;
                }

                var result = await stagingService.ImportAsync(
                    filePath,
                    seriesId: seriesId,
                    issueId: null,
                    editionId: editionId,
                    cancellationToken);

                results.Add(ImportResultDto.FromModel(result));
                if (!result.Success)
                {
                    errors.Add($"{filePath}: {result.ErrorMessage}");
                }
            }

            return errors.Count == 0
                ? Results.Ok(new { imported = results.Count, results })
                : Results.Ok(new { imported = results.Count(r => r.Success), failed = errors.Count, results, errors });
        })
        .WithName("BulkImport")
        .WithDescription("Imports multiple files at once from the staging folder.");

        // POST move to failed (original)
        group.MapPost("/failed", async (
            IStagingService stagingService,
            RejectRequest request,
            CancellationToken cancellationToken) =>
        {
            var success = await stagingService.MoveToFailedAsync(request.SourcePath, request.Reason ?? "Rejected by user", cancellationToken);
            return success
                ? Results.Ok(new { message = "File moved to failed folder" })
                : Results.BadRequest(new { message = "Failed to move file" });
        })
        .WithName("MoveToFailed")
        .WithDescription("Moves a file to the failed folder with a reason.");

        // POST /reject - alias for UI compatibility
        group.MapPost("/reject", async (
            IStagingService stagingService,
            RejectRequest request,
            CancellationToken cancellationToken) =>
        {
            var success = await stagingService.MoveToFailedAsync(request.SourcePath, request.Reason ?? "Rejected by user", cancellationToken);
            return success
                ? Results.Ok(new { message = "File rejected and moved to failed folder", path = request.SourcePath })
                : Results.BadRequest(new { message = "Failed to reject file", path = request.SourcePath });
        })
        .WithName("RejectFile")
        .WithDescription("Rejects a staged file and moves it to the failed folder.");

        // POST /update-match - update the series match for a staged file
        group.MapPost("/update-match", async (
            IStagingService stagingService,
            UpdateMatchRequest request,
            CancellationToken cancellationToken) =>
        {
            var success = await stagingService.UpdateMatchAsync(
                request.SourcePath,
                request.SeriesId,
                request.IssueId,
                request.EditionId,
                cancellationToken);

            var isClearing = !request.SeriesId.HasValue && !request.IssueId.HasValue && !request.EditionId.HasValue;
            var message = isClearing ? "Match cleared" : "Match updated";

            return success
                ? Results.Ok(new { message, path = request.SourcePath })
                : Results.BadRequest(new { message = "Failed to update match", path = request.SourcePath });
        })
        .WithName("UpdateMatch")
        .WithDescription("Updates the series/issue/edition match for a staged file.");
    }
}

/// <summary>
/// Request for bulk import operations.
/// </summary>
public record BulkImportRequest
{
    public required string[] Files { get; init; }
}

/// <summary>
/// Request for rejecting a staged file.
/// </summary>
public record RejectRequest
{
    public required string SourcePath { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Request for updating a staged file's match.
/// </summary>
public record UpdateMatchRequest
{
    public required string SourcePath { get; init; }
    public int? SeriesId { get; init; }
    public int? IssueId { get; init; }
    public int? EditionId { get; init; }
}



