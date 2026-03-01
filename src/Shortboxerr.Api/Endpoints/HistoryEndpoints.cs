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
        // Check if we need download history
        var includeDownloads = string.IsNullOrEmpty(type) || type == "all" || type == "downloaded" || type == "failed";
        var includeHistoryEvents = string.IsNullOrEmpty(type) || type == "all" || 
            type == "grabbed" || type == "imported" || type == "deleted" || type == "renamed" || type == "failed" || type == "added";

        // Build filtered queries (not yet executed)
        IQueryable<HistoryEvent>? historyQuery = null;
        IQueryable<DownloadHistory>? downloadQuery = null;
        var historyCount = 0;
        var downloadCount = 0;

        if (includeHistoryEvents)
        {
            historyQuery = db.HistoryEvents.AsQueryable();

            // Filter by event type
            if (!string.IsNullOrEmpty(type) && type != "all" && type != "downloaded")
            {
                if (type == "added")
                {
                    historyQuery = historyQuery.Where(e => 
                        e.EventType == HistoryEventType.SeriesAdded ||
                        e.EventType == HistoryEventType.IssueAdded ||
                        e.EventType == HistoryEventType.EditionAdded);
                }
                else if (type == "deleted")
                {
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

            historyCount = await historyQuery.CountAsync(ct);
        }

        if (includeDownloads)
        {
            downloadQuery = db.DownloadHistories.AsQueryable();

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

            downloadCount = await downloadQuery.CountAsync(ct);
        }

        var totalCount = historyCount + downloadCount;

        // Optimization: Instead of fetching pageSize*2 from each source, calculate exactly what we need
        // For page N with size P, we need at most (N * P) items from each source to ensure we get
        // the correct merged result. This is still over-fetching but better than the old approach.
        var fetchLimit = page * pageSize;
        
        // Fetch paginated data from each source (ordered by date at database level)
        var historyItems = historyQuery != null 
            ? await historyQuery.OrderByDescending(e => e.Timestamp).Take(fetchLimit).ToListAsync(ct) 
            : new List<HistoryEvent>();
        var downloadItems = downloadQuery != null 
            ? await downloadQuery.OrderByDescending(d => d.CompletedAt).Take(fetchLimit).ToListAsync(ct) 
            : new List<DownloadHistory>();

        // Map to DTOs (client-side) and merge
        var historyDtos = historyItems.Select(e => new HistoryEventDto
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
        });

        var downloadDtos = downloadItems.Select(d => new HistoryEventDto
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
        });

        // Merge and sort combined results, then paginate
        var sortedItems = historyDtos.Concat(downloadDtos)
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

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
