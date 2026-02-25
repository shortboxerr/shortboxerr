using Shortboxerr.Core.Activity;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for download activity monitoring.
/// Provides real-time visibility into active downloads across all clients.
/// </summary>
public static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/activity")
            .WithTags("Activity")
            .WithOpenApi();

        // GET /api/v1/activity - Get all active downloads
        group.MapGet("/", async (IActivityService activityService, CancellationToken ct) =>
        {
            var activities = await activityService.GetActiveDownloadsAsync(ct);
            return Results.Ok(activities);
        })
        .WithName("GetActiveDownloads")
        .WithSummary("Gets all active downloads across all clients")
        .Produces<IReadOnlyList<DownloadActivity>>();

        // GET /api/v1/activity/history - Get recent history
        group.MapGet("/history", async (IActivityService activityService, int? limit, CancellationToken ct) =>
        {
            var history = await activityService.GetRecentHistoryAsync(limit ?? 50, ct);
            return Results.Ok(history);
        })
        .WithName("GetActivityHistory")
        .WithSummary("Gets recent download history (completed, failed, cancelled)")
        .Produces<IReadOnlyList<DownloadActivity>>();

        // GET /api/v1/activity/summary - Get activity summary
        group.MapGet("/summary", async (IActivityService activityService, CancellationToken ct) =>
        {
            var summary = await activityService.GetSummaryAsync(ct);
            return Results.Ok(summary);
        })
        .WithName("GetActivitySummary")
        .WithSummary("Gets activity summary statistics")
        .Produces<ActivitySummary>();

        // GET /api/v1/activity/{id} - Get specific download
        group.MapGet("/{id}", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var activity = await activityService.GetByIdAsync(id, ct);
            return activity != null ? Results.Ok(activity) : Results.NotFound();
        })
        .WithName("GetActivityById")
        .WithSummary("Gets a specific download activity by ID")
        .Produces<DownloadActivity>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/activity/{id}/pause - Pause a download
        group.MapPost("/{id}/pause", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var result = await activityService.PauseAsync(id, ct);
            return result ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Failed to pause download" });
        })
        .WithName("PauseDownload")
        .WithSummary("Pauses a download (if supported)")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/v1/activity/{id}/resume - Resume a download
        group.MapPost("/{id}/resume", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var result = await activityService.ResumeAsync(id, ct);
            return result ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Failed to resume download" });
        })
        .WithName("ResumeDownload")
        .WithSummary("Resumes a paused download")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // DELETE /api/v1/activity/{id} - Cancel a download
        group.MapDelete("/{id}", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var result = await activityService.CancelAsync(id, ct);
            return result ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Failed to cancel download" });
        })
        .WithName("CancelDownload")
        .WithSummary("Cancels an active download")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/v1/activity/{id}/retry - Retry a failed download
        group.MapPost("/{id}/retry", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var result = await activityService.RetryAsync(id, ct);
            return result ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Failed to retry download" });
        })
        .WithName("RetryDownload")
        .WithSummary("Retries a failed download")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // DELETE /api/v1/activity/history/{id} - Remove from history
        group.MapDelete("/history/{id}", async (string id, IActivityService activityService, CancellationToken ct) =>
        {
            var result = await activityService.RemoveFromHistoryAsync(id, ct);
            return result ? Results.Ok(new { success = true }) : Results.NotFound();
        })
        .WithName("RemoveFromHistory")
        .WithSummary("Removes a download from history")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/v1/activity/history/completed - Clear completed items
        group.MapDelete("/history/completed", async (IActivityService activityService, CancellationToken ct) =>
        {
            var count = await activityService.ClearCompletedAsync(ct);
            return Results.Ok(new { success = true, removedCount = count });
        })
        .WithName("ClearCompletedHistory")
        .WithSummary("Clears all completed downloads from history")
        .Produces<object>();

        // DELETE /api/v1/activity/history - Clear all history
        group.MapDelete("/history", async (IActivityService activityService, CancellationToken ct) =>
        {
            var count = await activityService.ClearAllHistoryAsync(ct);
            return Results.Ok(new { success = true, removedCount = count });
        })
        .WithName("ClearAllHistory")
        .WithSummary("Clears all download history (completed, failed, and cancelled)")
        .Produces<object>();
    }
}
