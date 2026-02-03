using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class IssueMetadataEndpoints
{
    public static void MapIssueMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/issues")
            .WithTags("Issue Metadata");

        // Get issue details from ComicVine
        group.MapGet("/comicvine/{issueId:int}", GetComicVineIssue)
            .WithName("GetComicVineIssuePreview")
            .WithOpenApi()
            .Produces<IssueDetailResult>(200)
            .Produces(404);

        // Refresh issue metadata
        group.MapPost("/{issueId:int}/refresh", RefreshIssueMetadata)
            .WithName("RefreshIssueMetadata")
            .WithOpenApi()
            .Produces<IssueRefreshResult>(200)
            .Produces(400)
            .Produces(404);

        // Sync issue story arcs
        group.MapPost("/{issueId:int}/story-arcs/sync", SyncIssueStoryArcs)
            .WithName("SyncIssueStoryArcs")
            .WithOpenApi()
            .Produces<IssueStoryArcSyncResult>(200)
            .Produces(400)
            .Produces(404);

        // Series-level endpoints
        var seriesGroup = app.MapGroup("/api/v1/series")
            .WithTags("Issue Metadata");

        // Refresh all issues in a series
        seriesGroup.MapPost("/{seriesId:int}/issues/refresh", RefreshSeriesIssuesMetadata)
            .WithName("RefreshSeriesIssuesMetadata")
            .WithOpenApi()
            .Produces<IssuesBulkRefreshResult>(200)
            .Produces(400)
            .Produces(404);

        // Detect special issues in a series
        seriesGroup.MapPost("/{seriesId:int}/issues/detect-specials", DetectSpecialIssues)
            .WithName("DetectSpecialIssues")
            .WithOpenApi()
            .Produces<SpecialIssueDetectionResult>(200)
            .Produces(404);
    }

    private static async Task<IResult> GetComicVineIssue(
        int issueId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.GetIssueByComicVineIdAsync(issueId, cancellationToken);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshIssueMetadata(
        int issueId,
        [FromQuery] bool force = false,
        IIssueMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.RefreshIssueMetadataAsync(issueId, force, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> SyncIssueStoryArcs(
        int issueId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.SyncIssueStoryArcsAsync(issueId, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshSeriesIssuesMetadata(
        int seriesId,
        [FromQuery] bool force = false,
        IIssueMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.RefreshSeriesIssuesMetadataAsync(seriesId, force, cancellationToken);

        if (!result.Success && result.Error?.Contains("not found") == true)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> DetectSpecialIssues(
        int seriesId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.DetectSpecialIssuesAsync(seriesId, cancellationToken);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }
}

