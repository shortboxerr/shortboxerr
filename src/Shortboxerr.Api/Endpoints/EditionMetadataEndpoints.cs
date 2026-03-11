using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class EditionMetadataEndpoints
{
    public static void MapEditionMetadataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/editions")
            .WithTags("Edition Metadata")
            .WithOpenApi();

        // Search ComicVine for editions
        group.MapGet("/comicvine/search", async (
            IEditionMetadataService metadataService,
            [FromQuery] string query,
            [FromQuery] string? publisher = null,
            [FromQuery] int? year = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new { message = "Query is required" });
            }

            var q = query.Trim();

            // Check if query is a ComicVine volume ID (e.g. 4050-12345 or URL) - direct edition/collection lookup (14.11)
            var parsedVolumeId = ComicVineIdParser.TryParseAs(q, ComicVineResourceType.Volume);
            if (parsedVolumeId != null)
            {
                var edition = await metadataService.GetEditionByComicVineIdAsync(
                    parsedVolumeId.NumericId, cancellationToken);

                if (edition != null)
                {
                    return Results.Ok(new EditionSearchResult
                    {
                        Success = true,
                        Results = new List<EditionMatchCandidate> { edition },
                        TotalResults = 1,
                        Page = 1,
                        Limit = 1,
                        Query = q,
                        IsDirectLookup = true
                    });
                }

                return Results.Ok(new EditionSearchResult
                {
                    Success = true,
                    Results = new List<EditionMatchCandidate>(),
                    TotalResults = 0,
                    Page = 1,
                    Limit = limit,
                    Query = q,
                    IsDirectLookup = true,
                    Error = $"ComicVine volume {parsedVolumeId.FullId} not found"
                });
            }

            var result = await metadataService.SearchEditionsAsync(
                query, publisher, year, page, limit, cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("SearchComicVineEditions");

        // Get ComicVine edition preview by volume ID
        group.MapGet("/comicvine/{volumeId:int}", async (
            IEditionMetadataService metadataService,
            int volumeId,
            CancellationToken cancellationToken = default) =>
        {
            var result = await metadataService.GetEditionByComicVineIdAsync(volumeId, cancellationToken);

            return result != null
                ? Results.Ok(result)
                : Results.NotFound(new { message = $"ComicVine volume {volumeId} not found" });
        })
        .WithName("GetComicVineEditionPreview");

        // Match edition to ComicVine
        group.MapPost("/{editionId:int}/match/{comicVineId:int}", async (
            IEditionMetadataService metadataService,
            int editionId,
            int comicVineId,
            [FromQuery] bool syncMetadata = true,
            [FromQuery] bool mapContents = true,
            CancellationToken cancellationToken = default) =>
        {
            var result = await metadataService.MatchEditionAsync(
                editionId, comicVineId, syncMetadata, mapContents, cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("MatchEditionToComicVine");

        // Auto-match edition to ComicVine
        group.MapPost("/{editionId:int}/auto-match", async (
            IEditionMetadataService metadataService,
            int editionId,
            CancellationToken cancellationToken = default) =>
        {
            var result = await metadataService.AutoMatchEditionAsync(editionId, cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("AutoMatchEdition");

        // Unmatch edition from ComicVine
        group.MapDelete("/{editionId:int}/match", async (
            IEditionMetadataService metadataService,
            int editionId,
            CancellationToken cancellationToken = default) =>
        {
            var success = await metadataService.UnmatchEditionAsync(editionId, cancellationToken);

            return success
                ? Results.NoContent()
                : Results.NotFound(new { message = $"Edition {editionId} not found" });
        })
        .WithName("UnmatchEdition");

        // Refresh edition metadata from ComicVine
        group.MapPost("/{editionId:int}/refresh", async (
            IEditionMetadataService metadataService,
            int editionId,
            [FromQuery] bool force = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await metadataService.RefreshEditionMetadataAsync(editionId, force, cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("RefreshEditionMetadata");

        // Sync edition contents from ComicVine
        group.MapPost("/{editionId:int}/sync-contents", async (
            IEditionMetadataService metadataService,
            int editionId,
            CancellationToken cancellationToken = default) =>
        {
            var result = await metadataService.SyncEditionContentsAsync(editionId, cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("SyncEditionContents");
    }
}

