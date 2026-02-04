using Microsoft.EntityFrameworkCore;
using Shortboxerr.Api.Caching;
using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Caching;
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

        // GET all series (with paging and server-side caching)
        group.MapGet("/", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int page = 1,
            int pageSize = 20,
            string? sortKey = "title",
            string? sortDir = "asc") =>
        {
            // Generate cache key including query parameters
            var cacheKey = cacheService.GenerateKey(
                CacheKeys.SeriesList,
                page,
                pageSize,
                sortKey ?? "title",
                sortDir ?? "asc");

            // Cache for 2 minutes
            var result = await cacheService.GetOrCreateAsync(cacheKey, async () =>
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

                return PagedResult<SeriesDto>.Create(
                    records.Select(SeriesDto.FromEntity).ToList(),
                    page,
                    pageSize,
                    totalRecords);
            }, TimeSpan.FromMinutes(2));

            return Results.Ok(result);
        })
        .WithName("GetAllSeries")
        .WithHttpCache(120); // 2 minutes HTTP cache for list view

        // GET single series by ID (with ETag support and server-side caching)
        group.MapGet("/{id:int}", async (
            HttpContext httpContext,
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id) =>
        {
            // Generate cache key
            var cacheKey = cacheService.GenerateKey(CacheKeys.SeriesDetail, id);

            // Get from cache or database (5-minute TTL)
            var series = await cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                return await db.Series
                    .Include(s => s.Issues)
                    .Include(s => s.Editions)
                    .FirstOrDefaultAsync(s => s.Id == id);
            }, TimeSpan.FromMinutes(5));

            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            // Generate ETag from series ID and UpdatedAt
            var lastModified = series.UpdatedAt ?? series.CreatedAt;
            var etag = ETagHelper.GenerateETag(series.Id, lastModified);
            
            // Check If-None-Match header
            if (ETagHelper.IsNotModified(httpContext.Request, etag))
            {
                httpContext.Response.Headers.ETag = etag;
                return Results.StatusCode(304); // Not Modified
            }
            
            // Set ETag and Last-Modified headers
            httpContext.Response.Headers.ETag = etag;
            httpContext.Response.Headers.LastModified = lastModified.ToString("R"); // RFC1123 format
            
            return Results.Ok(SeriesDto.FromEntity(series));
        })
        .WithName("GetSeriesById")
        .WithHttpCache(300); // 5 minutes HTTP cache for detail view

        // GET issues for a series (with server-side caching)
        group.MapGet("/{id:int}/issues", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id,
            int page = 1,
            int pageSize = 100,
            string? sortKey = "issueNumber",
            string? sortDir = "asc") =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            // Generate cache key including query parameters
            var cacheKey = cacheService.GenerateKey(
                CacheKeys.Series,
                id,
                "issues",
                page,
                pageSize,
                sortKey ?? "issueNumber",
                sortDir ?? "asc");

            // Cache for 2 minutes
            var result = await cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var query = db.Issues
                    .Include(i => i.StoryArcs)
                    .Where(i => i.SeriesId == id);

                // Note: SQLite doesn't support ORDER BY decimal, so we sort in memory for IssueNumber
                var allRecords = await query.ToListAsync();
                var totalRecords = allRecords.Count;

                // Apply sorting in memory
                var sortedRecords = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
                {
                    ("issuenumber", "desc") => allRecords.OrderByDescending(i => i.IssueNumber),
                    ("issuenumber", _) => allRecords.OrderBy(i => i.IssueNumber),
                    ("releasedate", "desc") => allRecords.OrderByDescending(i => i.ReleaseDate ?? i.StoreDate),
                    ("releasedate", _) => allRecords.OrderBy(i => i.ReleaseDate ?? i.StoreDate),
                    ("title", "desc") => allRecords.OrderByDescending(i => i.Title),
                    ("title", _) => allRecords.OrderBy(i => i.Title),
                    ("status", "desc") => allRecords.OrderByDescending(i => i.HasFile).ThenByDescending(i => i.Monitored),
                    ("status", _) => allRecords.OrderBy(i => i.HasFile).ThenBy(i => i.Monitored),
                    _ => allRecords.OrderBy(i => i.IssueNumber)
                };

                var pagedRecords = sortedRecords
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return PagedResult<IssueDto>.Create(
                    pagedRecords.Select(IssueDto.FromEntity).ToList(),
                    page,
                    pageSize,
                    totalRecords);
            }, TimeSpan.FromMinutes(2));

            return Results.Ok(result);
        })
        .WithName("GetSeriesIssues")
        .WithHttpCache(120); // 2 minutes HTTP cache for issue list

        // POST create series
        group.MapPost("/", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            CreateSeriesRequest request) =>
        {
            var entity = request.ToEntity();
            db.Series.Add(entity);
            await db.SaveChangesAsync();

            // Invalidate series list cache (new series added)
            cacheService.RemoveByPrefix(CacheKeys.SeriesList);

            return Results.Created($"/api/v1/series/{entity.Id}", SeriesDto.FromEntity(entity));
        })
        .WithName("CreateSeries");

        // PUT update series
        group.MapPut("/{id:int}", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id,
            UpdateSeriesRequest request) =>
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

            // Invalidate caches for this series and list
            cacheService.RemoveByPrefix(CacheKeys.SeriesList);
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.SeriesDetail, id));
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.Series, id, "issues"));

            return Results.Ok(SeriesDto.FromEntity(series));
        })
        .WithName("UpdateSeries");

        // DELETE series
        group.MapDelete("/{id:int}", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id) =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            db.Series.Remove(series);
            await db.SaveChangesAsync();

            // Invalidate caches for this series and list
            cacheService.RemoveByPrefix(CacheKeys.SeriesList);
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.SeriesDetail, id));
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.Series, id, "issues"));

            return Results.NoContent();
        })
        .WithName("DeleteSeries");
    }
}

