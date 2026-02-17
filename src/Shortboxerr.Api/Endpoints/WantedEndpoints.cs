using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for viewing and managing wanted items.
/// </summary>
public static class WantedEndpoints
{
    public static void MapWantedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/wanted")
            .WithTags("Wanted")
            .WithOpenApi();

        // GET wanted issues
        group.MapGet("/issues", async (
            ShortboxerrDbContext db,
            string? search = null,
            string? sortKey = "series",
            string? sortDir = "asc",
            int page = 1,
            int pageSize = 50) =>
        {
            var query = db.Issues
                .Include(i => i.Series)
                .Where(i => i.Status == IssueStatus.Wanted)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(i => 
                    (i.Title != null && i.Title.ToLower().Contains(searchLower)) ||
                    (i.Series != null && i.Series.Title.ToLower().Contains(searchLower)));
            }

            // Apply sorting (Note: SQLite doesn't support ORDER BY decimal, so IssueNumber sort is done in memory)
            var primarySortKey = sortKey?.ToLowerInvariant();
            var sortDescending = sortDir?.ToLowerInvariant() == "desc";
            
            query = (primarySortKey, sortDescending) switch
            {
                ("series", true) => query.OrderByDescending(i => i.Series!.SortTitle ?? i.Series.Title),
                ("series", false) => query.OrderBy(i => i.Series!.SortTitle ?? i.Series.Title),
                ("releasedate", true) => query.OrderByDescending(i => i.StoreDate ?? i.ReleaseDate),
                ("releasedate", false) => query.OrderBy(i => i.StoreDate ?? i.ReleaseDate),
                ("added", true) => query.OrderByDescending(i => i.CreatedAt),
                ("added", false) => query.OrderBy(i => i.CreatedAt),
                _ => query.OrderBy(i => i.Series!.SortTitle ?? i.Series.Title)
            };

            var totalCount = await query.CountAsync();
            
            // Fetch items and sort by IssueNumber in memory for SQLite compatibility
            var issuesRaw = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Secondary sort by IssueNumber in memory (SQLite decimal limitation)
            if (primarySortKey == "issue")
            {
                issuesRaw = sortDescending 
                    ? issuesRaw.OrderByDescending(i => i.IssueNumber).ToList()
                    : issuesRaw.OrderBy(i => i.IssueNumber).ToList();
            }
            else if (primarySortKey == "series" || primarySortKey == null)
            {
                // For series sort, add secondary sort by issue number
                issuesRaw = issuesRaw.OrderBy(i => i.Series?.SortTitle ?? i.Series?.Title ?? "")
                    .ThenBy(i => i.IssueNumber).ToList();
            }
            
            var items = issuesRaw.Select(i => new WantedIssueDto
                {
                    Id = i.Id,
                    Title = i.Title ?? $"Issue #{i.IssueNumber}",
                    Series = i.Series!.Title,
                    SeriesId = i.SeriesId,
                    IssueNumber = i.IssueNumber,
                    IssueNumberText = i.IssueNumberText,
                    ReleaseDate = i.StoreDate ?? i.ReleaseDate,
                    CoverImageUrl = i.CoverImageUrl,
                    ComicVineId = i.ComicVineId,
                    ComicVineUrl = i.ComicVineUrl,
                    DateAdded = i.CreatedAt
                })
                .ToList();

            return Results.Ok(new WantedPagedResult<WantedIssueDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        })
        .WithName("GetWantedIssues")
        .WithDescription("Gets all issues with Wanted status");

        // GET wanted collections/editions
        group.MapGet("/collections", async (
            ShortboxerrDbContext db,
            string? search = null,
            string? sortKey = "title",
            string? sortDir = "asc",
            int page = 1,
            int pageSize = 50) =>
        {
            // Editions that don't have an associated file
            var query = db.EditionTitles
                .Include(e => e.Series)
                .Where(e => !e.HasFile && e.Monitored)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(e => 
                    e.Title.ToLower().Contains(searchLower) ||
                    (e.Series != null && e.Series.Title.ToLower().Contains(searchLower)));
            }

            // Apply sorting
            query = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
            {
                ("title", "desc") => query.OrderByDescending(e => e.Title),
                ("title", _) => query.OrderBy(e => e.Title),
                ("series", "desc") => query.OrderByDescending(e => e.Series!.SortTitle ?? e.Series.Title),
                ("series", _) => query.OrderBy(e => e.Series!.SortTitle ?? e.Series.Title),
                ("added", "desc") => query.OrderByDescending(e => e.CreatedAt),
                ("added", _) => query.OrderBy(e => e.CreatedAt),
                _ => query.OrderBy(e => e.Title)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new WantedCollectionDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Series = e.Series != null ? e.Series.Title : "Unknown",
                    SeriesId = e.SeriesId,
                    EditionType = e.EditionType.ToString(),
                    VolumeNumber = e.VolumeNumber,
                    CoverImageUrl = e.CoverImageUrl,
                    DateAdded = e.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new WantedPagedResult<WantedCollectionDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        })
        .WithName("GetWantedCollections")
        .WithDescription("Gets all monitored collections without files");

        // GET combined wanted count (for dashboard)
        group.MapGet("/count", async (ShortboxerrDbContext db) =>
        {
            var issueCount = await db.Issues
                .CountAsync(i => i.Status == IssueStatus.Wanted);
            
            var collectionCount = await db.EditionTitles
                .CountAsync(e => !e.HasFile && e.Monitored);

            return Results.Ok(new WantedCountDto
            {
                Issues = issueCount,
                Collections = collectionCount,
                Total = issueCount + collectionCount
            });
        })
        .WithName("GetWantedCount")
        .WithDescription("Gets count of wanted issues and collections");
    }
}

#region DTOs

public record WantedIssueDto
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Series { get; init; }
    public int SeriesId { get; init; }
    public decimal IssueNumber { get; init; }
    public string? IssueNumberText { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public string? CoverImageUrl { get; init; }
    public int? ComicVineId { get; init; }
    public string? ComicVineUrl { get; init; }
    public DateTime DateAdded { get; init; }
}

public record WantedCollectionDto
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Series { get; init; }
    public int? SeriesId { get; init; }
    public required string EditionType { get; init; }
    public int? VolumeNumber { get; init; }
    public string? CoverImageUrl { get; init; }
    public DateTime DateAdded { get; init; }
}

public record WantedCountDto
{
    public int Issues { get; init; }
    public int Collections { get; init; }
    public int Total { get; init; }
}

public record WantedPagedResult<T>
{
    public required List<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

#endregion
