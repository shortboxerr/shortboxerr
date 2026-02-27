using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class AutoMatchEndpoints
{
    public static void MapAutoMatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auto-match")
            .WithTags("Auto-Match")
            .WithOpenApi();

        // Get auto-match settings
        group.MapGet("/settings", async (
            IAutoMatchService autoMatchService,
            CancellationToken cancellationToken) =>
        {
            var settings = await autoMatchService.GetSettingsAsync(cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetAutoMatchSettingsV1");

        // Bulk auto-match all unmatched series
        group.MapPost("/series/bulk", async (
            IAutoMatchService autoMatchService,
            [FromQuery] int? confidenceThreshold = null,
            [FromQuery] bool matchImmediately = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await autoMatchService.AutoMatchAllUnmatchedSeriesAsync(
                confidenceThreshold,
                matchImmediately,
                progress: null,
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("BulkAutoMatchSeries");

        // Bulk auto-match all unmatched editions
        group.MapPost("/editions/bulk", async (
            IAutoMatchService autoMatchService,
            [FromQuery] int? confidenceThreshold = null,
            [FromQuery] bool matchImmediately = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await autoMatchService.AutoMatchAllUnmatchedEditionsAsync(
                confidenceThreshold,
                matchImmediately,
                progress: null,
                cancellationToken);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("BulkAutoMatchEditions");

        // Get pending matches requiring review
        group.MapGet("/pending", async (
            IAutoMatchService autoMatchService,
            CancellationToken cancellationToken) =>
        {
            var pending = await autoMatchService.GetPendingMatchesAsync(cancellationToken);
            return Results.Ok(pending);
        })
        .WithName("GetPendingMatches");

        // Accept a pending match
        group.MapPost("/pending/{id:int}/accept", async (
            IAutoMatchService autoMatchService,
            int id,
            CancellationToken cancellationToken) =>
        {
            var success = await autoMatchService.AcceptPendingMatchAsync(id, cancellationToken);
            return success
                ? Results.NoContent()
                : Results.NotFound(new { message = $"Pending match {id} not found or already resolved" });
        })
        .WithName("AcceptPendingMatch");

        // Reject a pending match
        group.MapPost("/pending/{id:int}/reject", async (
            IAutoMatchService autoMatchService,
            int id,
            CancellationToken cancellationToken) =>
        {
            var success = await autoMatchService.RejectPendingMatchAsync(id, cancellationToken);
            return success
                ? Results.NoContent()
                : Results.NotFound(new { message = $"Pending match {id} not found or already resolved" });
        })
        .WithName("RejectPendingMatch");

        // Get summary stats
        group.MapGet("/stats", async (
            IAutoMatchService autoMatchService,
            CancellationToken cancellationToken) =>
        {
            var pending = await autoMatchService.GetPendingMatchesAsync(cancellationToken);
            
            return Results.Ok(new
            {
                pendingCount = pending.Count,
                pendingSeries = pending.Count(p => p.ItemType == "Series"),
                pendingEditions = pending.Count(p => p.ItemType == "Edition")
            });
        })
        .WithName("GetAutoMatchStats");
    }
}

