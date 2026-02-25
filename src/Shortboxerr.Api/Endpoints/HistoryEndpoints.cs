using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Activity;
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

        // GET /api/v1/history - Get unified history events (HistoryEvents + DownloadHistory)
        group.MapGet("/", GetHistory)
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

        // DELETE /api/v1/history - Clear all history (both HistoryEvents and DownloadHistory)
        group.MapDelete("/", async (
            ShortboxerrDbContext db,
            CancellationToken ct) =>
        {
            var historyCount = await db.HistoryEvents.ExecuteDeleteAsync(ct);
            var downloadCount = await db.DownloadHistories.ExecuteDeleteAsync(ct);
            return Results.Ok(new { success = true, deletedCount = historyCount + downloadCount });
        })
        .WithName("ClearHistory")
        .WithSummary("Clears all history events")
        .Produces<object>();
    }

    private static async Task<IResult> GetHistory(
        ShortboxerrDbContext db,
        string? type,
        string? search,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var allItems = new List<HistoryEventDto>();

        // Check if we need download history
        var includeDownloads = string.IsNullOrEmpty(type) || type == "all" || type == "downloaded" || type == "failed";
        var includeHistoryEvents = string.IsNullOrEmpty(type) || type == "all" || 
            type == "grabbed" || type == "imported" || type == "deleted" || type == "renamed" || type == "failed" || type == "added";

        // Get HistoryEvents
        if (includeHistoryEvents)
        {
            var historyQuery = db.HistoryEvents.AsQueryable();

            // Filter by event type
            if (!string.IsNullOrEmpty(type) && type != "all" && type != "downloaded")
            {
                if (type == "added")
                {
                    // "added" includes series, issue, and edition additions
                    historyQuery = historyQuery.Where(e => 
                        e.EventType == HistoryEventType.SeriesAdded ||
                        e.EventType == HistoryEventType.IssueAdded ||
                        e.EventType == HistoryEventType.EditionAdded);
                }
                else if (type == "deleted")
                {
                    // "deleted" includes file, series, and edition deletions
                    historyQuery = historyQuery.Where(e => 
                        e.EventType == HistoryEventType.FileDeleted ||
                        e.EventType == HistoryEventType.SeriesDeleted ||
                        e.EventType == HistoryEventType.EditionDeleted);
                }
                else
                {
                    var eventType = type.ToLowerInvariant() switch
                    {
                        "grabbed" => HistoryEventType.DownloadGrabbed,
                        "imported" => HistoryEventType.DdlImportCompleted,
                        "failed" => HistoryEventType.DownloadFailed,
                        "renamed" => HistoryEventType.FileRenamed,
                        _ => (HistoryEventType?)null
                    };

                    if (eventType.HasValue)
                    {
                        historyQuery = historyQuery.Where(e => e.EventType == eventType.Value);
                    }
                }
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                historyQuery = historyQuery.Where(e => 
                    e.Message.Contains(search) ||
                    (e.SourcePath != null && e.SourcePath.Contains(search)) ||
                    (e.DestinationPath != null && e.DestinationPath.Contains(search)));
            }

            var historyItems = await historyQuery
                .OrderByDescending(e => e.Timestamp)
                .Take(pageSize * 2)
                .ToListAsync(ct);

            allItems.AddRange(historyItems.Select(e => new HistoryEventDto
            {
                Id = e.Id,
                EventType = MapEventType(e.EventType),
                Message = e.Message,
                SeriesId = e.SeriesId,
                IssueId = e.IssueId,
                SourcePath = e.SourcePath,
                DestinationPath = e.DestinationPath,
                Success = e.Success,
                ErrorMessage = e.ErrorMessage,
                Date = e.Timestamp,
                Data = e.Data
            }));
        }

        // Get DownloadHistory (for "downloaded" and "failed" filters)
        if (includeDownloads)
        {
            var downloadQuery = db.DownloadHistories.AsQueryable();

            // Filter by download success/failure
            if (type == "downloaded")
            {
                downloadQuery = downloadQuery.Where(d => d.Success);
            }
            else if (type == "failed")
            {
                downloadQuery = downloadQuery.Where(d => !d.Success);
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                downloadQuery = downloadQuery.Where(d =>
                    d.Title.Contains(search) ||
                    (d.SourceSite != null && d.SourceSite.Contains(search)) ||
                    (d.DestinationPath != null && d.DestinationPath.Contains(search)));
            }

            var downloadItems = await downloadQuery
                .OrderByDescending(d => d.CompletedAt)
                .Take(pageSize * 2)
                .ToListAsync(ct);

            allItems.AddRange(downloadItems.Select(d => new HistoryEventDto
            {
                Id = d.Id + 1000000,
                EventType = d.Success ? "downloaded" : "failed",
                Message = d.Title,
                SeriesId = d.SeriesId,
                IssueId = d.IssueId,
                SourcePath = d.SourceUrl,
                DestinationPath = d.DestinationPath,
                Success = d.Success,
                ErrorMessage = d.ErrorMessage,
                Date = d.CompletedAt,
                Data = d.SourceSite != null ? $"Source: {d.SourceSite}" : null
            }));
        }

        // Sort combined results and paginate
        var sortedItems = allItems
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalCount = allItems.Count;

        return Results.Ok(new PagedHistoryResult
        {
            Items = sortedItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    private static string MapEventType(HistoryEventType eventType)
    {
        return eventType switch
        {
            HistoryEventType.DownloadGrabbed => "grabbed",
            HistoryEventType.DownloadCompleted => "downloaded",
            HistoryEventType.DownloadFailed => "failed",
            HistoryEventType.FileImported => "imported",
            HistoryEventType.DdlImportCompleted => "imported",
            HistoryEventType.DdlImportFailed => "failed",
            HistoryEventType.FileDeleted => "deleted",
            HistoryEventType.FileRenamed => "renamed",
            HistoryEventType.FileMoved => "renamed",
            HistoryEventType.SeriesAdded => "added",
            HistoryEventType.SeriesDeleted => "deleted",
            HistoryEventType.IssueAdded => "added",
            HistoryEventType.EditionAdded => "added",
            HistoryEventType.EditionDeleted => "deleted",
            _ => eventType.ToString().ToLowerInvariant()
        };
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
