using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for application-wide history/events.
/// </summary>
public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/history")
            .WithTags("History")
            .WithOpenApi();

        // GET /api/v1/history - Get history events
        group.MapGet("/", async (
            ShortboxerrDbContext db,
            string? type,
            string? search,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            var query = db.HistoryEvents.AsQueryable();

            // Filter by event type
            if (!string.IsNullOrEmpty(type) && type != "all")
            {
                var eventType = type.ToLowerInvariant() switch
                {
                    "grabbed" => HistoryEventType.DownloadGrabbed,
                    "imported" => HistoryEventType.DdlImportCompleted,
                    "deleted" => HistoryEventType.FileDeleted,
                    "failed" => HistoryEventType.DownloadFailed,
                    "renamed" => HistoryEventType.FileRenamed,
                    _ => (HistoryEventType?)null
                };

                if (eventType.HasValue)
                {
                    query = query.Where(e => e.EventType == eventType.Value);
                }
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(e => 
                    e.Message.Contains(search) ||
                    (e.SourcePath != null && e.SourcePath.Contains(search)) ||
                    (e.DestinationPath != null && e.DestinationPath.Contains(search)));
            }

            var totalCount = await query.CountAsync(ct);
            
            var items = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new HistoryEventDto
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString().ToLowerInvariant(),
                    Message = e.Message,
                    SeriesId = e.SeriesId,
                    IssueId = e.IssueId,
                    SourcePath = e.SourcePath,
                    DestinationPath = e.DestinationPath,
                    Success = e.Success,
                    ErrorMessage = e.ErrorMessage,
                    Date = e.Timestamp,
                    Data = e.Data
                })
                .ToListAsync(ct);

            return Results.Ok(new PagedHistoryResult
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        })
        .WithName("GetHistory")
        .WithSummary("Gets application history events")
        .Produces<PagedHistoryResult>();

        // GET /api/v1/history/{id} - Get specific event
        group.MapGet("/{id:int}", async (
            int id,
            ShortboxerrDbContext db,
            CancellationToken ct) =>
        {
            var evt = await db.HistoryEvents.FindAsync(new object[] { id }, ct);
            return evt != null 
                ? Results.Ok(new HistoryEventDto
                {
                    Id = evt.Id,
                    EventType = evt.EventType.ToString().ToLowerInvariant(),
                    Message = evt.Message,
                    SeriesId = evt.SeriesId,
                    IssueId = evt.IssueId,
                    SourcePath = evt.SourcePath,
                    DestinationPath = evt.DestinationPath,
                    Success = evt.Success,
                    ErrorMessage = evt.ErrorMessage,
                    Date = evt.Timestamp,
                    Data = evt.Data
                })
                : Results.NotFound();
        })
        .WithName("GetHistoryEvent")
        .WithSummary("Gets a specific history event")
        .Produces<HistoryEventDto>()
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/v1/history/{id} - Delete a history event
        group.MapDelete("/{id:int}", async (
            int id,
            ShortboxerrDbContext db,
            CancellationToken ct) =>
        {
            var evt = await db.HistoryEvents.FindAsync(new object[] { id }, ct);
            if (evt == null)
                return Results.NotFound();

            db.HistoryEvents.Remove(evt);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { success = true });
        })
        .WithName("DeleteHistoryEvent")
        .WithSummary("Deletes a history event")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/v1/history - Clear all history
        group.MapDelete("/", async (
            ShortboxerrDbContext db,
            CancellationToken ct) =>
        {
            var count = await db.HistoryEvents.ExecuteDeleteAsync(ct);
            return Results.Ok(new { success = true, deletedCount = count });
        })
        .WithName("ClearHistory")
        .WithSummary("Clears all history events")
        .Produces<object>();
    }
}

public class HistoryEventDto
{
    public int Id { get; init; }
    public required string EventType { get; init; }
    public required string Message { get; init; }
    public int? SeriesId { get; init; }
    public int? IssueId { get; init; }
    public string? SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Date { get; init; }
    public string? Data { get; init; }
}

public class PagedHistoryResult
{
    public required IList<HistoryEventDto> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
