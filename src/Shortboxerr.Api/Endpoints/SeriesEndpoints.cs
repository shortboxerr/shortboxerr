using Microsoft.EntityFrameworkCore;
using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class SeriesEndpoints
{
    public static void MapSeriesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/series")
            .WithTags("Series")
            .WithOpenApi();

        // GET all series (with paging)
        group.MapGet("/", async (
            ShortboxerrDbContext db,
            int page = 1,
            int pageSize = 20,
            string? sortKey = "title",
            string? sortDir = "asc") =>
        {
            var query = db.Series
                .Include(s => s.Issues)
                .Include(s => s.Editions)
                .AsQueryable();

            // Apply sorting
            query = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
            {
                ("title", "desc") => query.OrderByDescending(s => s.SortTitle ?? s.Title),
                ("title", _) => query.OrderBy(s => s.SortTitle ?? s.Title),
                ("startyear", "desc") => query.OrderByDescending(s => s.StartYear),
                ("startyear", _) => query.OrderBy(s => s.StartYear),
                ("createdat", "desc") => query.OrderByDescending(s => s.CreatedAt),
                ("createdat", _) => query.OrderBy(s => s.CreatedAt),
                _ => query.OrderBy(s => s.SortTitle ?? s.Title)
            };

            var totalRecords = await query.CountAsync();
            var records = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(PagedResult<SeriesDto>.Create(
                records.Select(SeriesDto.FromEntity).ToList(),
                page,
                pageSize,
                totalRecords));
        })
        .WithName("GetAllSeries");

        // GET single series by ID
        group.MapGet("/{id:int}", async (ShortboxerrDbContext db, int id) =>
        {
            var series = await db.Series
                .Include(s => s.Issues)
                .Include(s => s.Editions)
                .FirstOrDefaultAsync(s => s.Id == id);

            return series is null
                ? Results.NotFound(new { message = $"Series {id} not found" })
                : Results.Ok(SeriesDto.FromEntity(series));
        })
        .WithName("GetSeriesById");

        // GET issues for a series
        group.MapGet("/{id:int}/issues", async (
            ShortboxerrDbContext db,
            int id,
            int page = 1,
            int pageSize = 100,
            string? sortKey = "issueNumber",
            string? sortDir = "asc") =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            var query = db.Issues.Where(i => i.SeriesId == id);

            // Apply sorting
            query = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
            {
                ("issuenumber", "desc") => query.OrderByDescending(i => i.IssueNumber),
                ("issuenumber", _) => query.OrderBy(i => i.IssueNumber),
                ("releasedate", "desc") => query.OrderByDescending(i => i.ReleaseDate ?? i.StoreDate),
                ("releasedate", _) => query.OrderBy(i => i.ReleaseDate ?? i.StoreDate),
                ("title", "desc") => query.OrderByDescending(i => i.Title),
                ("title", _) => query.OrderBy(i => i.Title),
                _ => query.OrderBy(i => i.IssueNumber)
            };

            var totalRecords = await query.CountAsync();
            var records = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(PagedResult<IssueDto>.Create(
                records.Select(IssueDto.FromEntity).ToList(),
                page,
                pageSize,
                totalRecords));
        })
        .WithName("GetSeriesIssues");

        // POST create series
        group.MapPost("/", async (ShortboxerrDbContext db, CreateSeriesRequest request) =>
        {
            var entity = request.ToEntity();
            db.Series.Add(entity);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/series/{entity.Id}", SeriesDto.FromEntity(entity));
        })
        .WithName("CreateSeries");

        // PUT update series
        group.MapPut("/{id:int}", async (ShortboxerrDbContext db, int id, UpdateSeriesRequest request) =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            // Apply updates (only non-null values)
            if (request.Title is not null) series.Title = request.Title;
            if (request.SortTitle is not null) series.SortTitle = request.SortTitle;
            if (request.Publisher is not null) series.Publisher = request.Publisher;
            if (request.StartYear.HasValue) series.StartYear = request.StartYear;
            if (request.EndYear.HasValue) series.EndYear = request.EndYear;
            if (request.Status.HasValue) series.Status = request.Status.Value;
            if (request.Path is not null) series.Path = request.Path;
            if (request.ExternalId is not null) series.ExternalId = request.ExternalId;
            if (request.ExternalSource is not null) series.ExternalSource = request.ExternalSource;
            if (request.Overview is not null) series.Overview = request.Overview;
            if (request.Monitored.HasValue) series.Monitored = request.Monitored.Value;

            series.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(SeriesDto.FromEntity(series));
        })
        .WithName("UpdateSeries");

        // DELETE series
        group.MapDelete("/{id:int}", async (ShortboxerrDbContext db, int id) =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            db.Series.Remove(series);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteSeries");
    }
}

