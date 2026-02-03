using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class CoverEndpoints
{
    public static void MapCoverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/covers")
            .WithTags("Covers");

        // Get series cover
        group.MapGet("/series/{seriesId:int}", GetSeriesCover)
            .WithName("GetSeriesCover")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);

        // Get issue cover
        group.MapGet("/issues/{issueId:int}", GetIssueCover)
            .WithName("GetIssueCover")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);

        // Clear series cover cache
        group.MapDelete("/series/{seriesId:int}", ClearSeriesCoverCache)
            .WithName("ClearSeriesCoverCache")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        // Clear issue cover cache
        group.MapDelete("/issues/{issueId:int}", ClearIssueCoverCache)
            .WithName("ClearIssueCoverCache")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        // Get cache statistics
        group.MapGet("/cache/stats", GetCacheStats)
            .WithName("GetCoverCacheStats")
            .WithOpenApi()
            .Produces<CoverCacheStats>(200);

        // Clear all cache
        group.MapDelete("/cache", ClearAllCache)
            .WithName("ClearAllCoverCache")
            .WithOpenApi()
            .Produces(204);

        // Download/refresh a cover
        group.MapPost("/series/{seriesId:int}/refresh", RefreshSeriesCover)
            .WithName("RefreshSeriesCover")
            .WithOpenApi()
            .Produces<CoverResult>(200)
            .Produces(400)
            .Produces(404);

        group.MapPost("/issues/{issueId:int}/refresh", RefreshIssueCover)
            .WithName("RefreshIssueCover")
            .WithOpenApi()
            .Produces<CoverResult>(200)
            .Produces(400)
            .Produces(404);
    }

    private static async Task<IResult> GetSeriesCover(
        int seriesId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.GetSeriesCoverAsync(seriesId, size, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.FilePath))
        {
            return Results.NotFound(new { error = result.Error ?? "Cover not found" });
        }

        if (!File.Exists(result.FilePath))
        {
            return Results.NotFound(new { error = "Cover file not found" });
        }

        return Results.File(result.FilePath, result.ContentType ?? "image/jpeg");
    }

    private static async Task<IResult> GetIssueCover(
        int issueId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.GetIssueCoverAsync(issueId, size, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.FilePath))
        {
            return Results.NotFound(new { error = result.Error ?? "Cover not found" });
        }

        if (!File.Exists(result.FilePath))
        {
            return Results.NotFound(new { error = "Cover file not found" });
        }

        return Results.File(result.FilePath, result.ContentType ?? "image/jpeg");
    }

    private static async Task<IResult> ClearSeriesCoverCache(
        int seriesId,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearSeriesCoverCacheAsync(seriesId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearIssueCoverCache(
        int issueId,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearIssueCoverCacheAsync(issueId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCacheStats(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var stats = await coverService.GetCacheStatsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> ClearAllCache(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearAllCacheAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshSeriesCover(
        int seriesId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        // Clear existing cache first
        await coverService.ClearSeriesCoverCacheAsync(seriesId, cancellationToken);
        
        // Re-fetch
        var result = await coverService.GetSeriesCoverAsync(seriesId, size, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshIssueCover(
        int issueId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        // Clear existing cache first
        await coverService.ClearIssueCoverCacheAsync(issueId, cancellationToken);
        
        // Re-fetch
        var result = await coverService.GetIssueCoverAsync(issueId, size, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }
}

