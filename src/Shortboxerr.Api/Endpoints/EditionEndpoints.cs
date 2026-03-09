using Microsoft.EntityFrameworkCore;
using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class EditionEndpoints
{
    public static void MapEditionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/editions")
            .WithTags("Editions")
            .WithOpenApi();

        // GET all editions (with paging)
        group.MapGet("/", async (
            ShortboxerrDbContext db,
            int page = 1,
            int pageSize = 20,
            int? seriesId = null,
            string? search = null,
            bool? monitored = null,
            bool? hasFile = null,
            string? editionType = null,
            string? sortKey = "title",
            string? sortDir = "asc") =>
        {
            var query = db.EditionTitles
                .Include(e => e.Series)
                .Include(e => e.Contents)
                .AsQueryable();

            // Filter by series if specified
            if (seriesId.HasValue)
                query = query.Where(e => e.SeriesId == seriesId.Value);

            // Apply text search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(e =>
                    e.Title.ToLower().Contains(searchLower) ||
                    (e.SortTitle != null && e.SortTitle.ToLower().Contains(searchLower)) ||
                    (e.Series != null && e.Series.Title.ToLower().Contains(searchLower)));
            }

            // Apply monitored filter
            if (monitored.HasValue)
                query = query.Where(e => e.Monitored == monitored.Value);

            // Apply hasFile filter
            if (hasFile.HasValue)
                query = query.Where(e => e.HasFile == hasFile.Value);

            // Apply edition type filter
            if (!string.IsNullOrWhiteSpace(editionType) && 
                Enum.TryParse<EditionType>(editionType, ignoreCase: true, out var parsedType))
            {
                query = query.Where(e => e.EditionType == parsedType);
            }

            // Apply sorting
            query = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
            {
                ("title", "desc") => query.OrderByDescending(e => e.SortTitle ?? e.Title),
                ("title", _) => query.OrderBy(e => e.SortTitle ?? e.Title),
                ("releasedate", "desc") => query.OrderByDescending(e => e.ReleaseDate),
                ("releasedate", _) => query.OrderBy(e => e.ReleaseDate),
                ("createdat", "desc") => query.OrderByDescending(e => e.CreatedAt),
                ("createdat", _) => query.OrderBy(e => e.CreatedAt),
                ("volumenumber", "desc") => query.OrderByDescending(e => e.VolumeNumber),
                ("volumenumber", _) => query.OrderBy(e => e.VolumeNumber),
                _ => query.OrderBy(e => e.SortTitle ?? e.Title)
            };

            var totalRecords = await query.CountAsync();
            var records = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(PagedResult<EditionDto>.Create(
                records.Select(EditionDto.FromEntity).ToList(),
                page,
                pageSize,
                totalRecords));
        })
        .WithName("GetAllEditions")
        .WithSummary("Get all editions with optional filtering and sorting")
        .WithDescription("Supports filtering by series ID, text search (title, series name), monitored status, hasFile status, and edition type. Supports sorting by title, releasedate, createdat, volumenumber.");

        // GET single edition by ID (basic info)
        group.MapGet("/{id:int}", async (ShortboxerrDbContext db, int id) =>
        {
            var edition = await db.EditionTitles
                .Include(e => e.Series)
                .Include(e => e.Contents)
                .FirstOrDefaultAsync(e => e.Id == id);

            return edition is null
                ? Results.NotFound(new { message = $"Edition {id} not found" })
                : Results.Ok(EditionDto.FromEntity(edition));
        })
        .WithName("GetEditionById");

        // GET edition detail with full contents
        group.MapGet("/{id:int}/detail", async (ShortboxerrDbContext db, int id) =>
        {
            var edition = await db.EditionTitles
                .Include(e => e.Series)
                .Include(e => e.Contents)
                    .ThenInclude(c => c.Issue)
                        .ThenInclude(i => i!.Series)
                .Include(e => e.Contents)
                    .ThenInclude(c => c.Series)
                .AsSplitQuery() // Use split queries for multiple collection navigations
                .FirstOrDefaultAsync(e => e.Id == id);

            return edition is null
                ? Results.NotFound(new { message = $"Edition {id} not found" })
                : Results.Ok(EditionDetailDto.FromEntity(edition));
        })
        .WithName("GetEditionDetail");

        // GET edition contents (issues contained in the edition)
        group.MapGet("/{id:int}/contents", async (ShortboxerrDbContext db, int id) =>
        {
            var edition = await db.EditionTitles.FindAsync(id);
            if (edition is null)
                return Results.NotFound(new { message = $"Edition {id} not found" });

            var contents = await db.EditionContents
                .Include(c => c.Issue)
                    .ThenInclude(i => i!.Series)
                .Include(c => c.Series)
                .AsSplitQuery() // Use split queries for multiple collection navigations
                .Where(c => c.EditionTitleId == id)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            return Results.Ok(contents.Select(EditionContentDto.FromEntity).ToList());
        })
        .WithName("GetEditionContents");

        // POST create edition
        group.MapPost("/", async (
            ShortboxerrDbContext db,
            IHistoryService historyService,
            CreateEditionRequest request,
            CancellationToken ct) =>
        {
            // Validate SeriesId if provided
            string? seriesTitle = null;
            if (request.SeriesId.HasValue)
            {
                var series = await db.Series.FindAsync(new object[] { request.SeriesId.Value }, ct);
                if (series == null)
                    return Results.BadRequest(new { message = $"Series {request.SeriesId} not found" });
                seriesTitle = series.Title;
            }

            var entity = request.ToEntity();
            db.EditionTitles.Add(entity);
            await db.SaveChangesAsync(ct);

            // Record history event
            if (request.SeriesId.HasValue)
            {
                await historyService.RecordEditionAddedAsync(
                    entity.Id, request.SeriesId.Value, entity.Title, seriesTitle ?? "Unknown", ct);
            }

            return Results.Created($"/api/v1/editions/{entity.Id}", EditionDto.FromEntity(entity));
        })
        .WithName("CreateEdition");

        // PUT update edition
        group.MapPut("/{id:int}", async (ShortboxerrDbContext db, int id, UpdateEditionRequest request) =>
        {
            var edition = await db.EditionTitles
                .Include(e => e.Series)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (edition is null)
                return Results.NotFound(new { message = $"Edition {id} not found" });

            // Validate SeriesId if changing
            if (request.SeriesId.HasValue && request.SeriesId != edition.SeriesId)
            {
                var seriesExists = await db.Series.AnyAsync(s => s.Id == request.SeriesId.Value);
                if (!seriesExists)
                    return Results.BadRequest(new { message = $"Series {request.SeriesId} not found" });
                edition.SeriesId = request.SeriesId;
            }

            // Apply updates (only non-null values)
            if (request.Title is not null) edition.Title = request.Title;
            if (request.SortTitle is not null) edition.SortTitle = request.SortTitle;
            if (request.EditionType.HasValue) edition.EditionType = request.EditionType.Value;
            if (request.VolumeNumber.HasValue) edition.VolumeNumber = request.VolumeNumber;
            if (request.Isbn is not null) edition.Isbn = request.Isbn;
            if (request.Publisher is not null) edition.Publisher = request.Publisher;
            if (request.ReleaseDate.HasValue) edition.ReleaseDate = request.ReleaseDate;
            if (request.PageCount.HasValue) edition.PageCount = request.PageCount;
            if (request.ExternalId is not null) edition.ExternalId = request.ExternalId;
            if (request.ExternalSource is not null) edition.ExternalSource = request.ExternalSource;
            if (request.Overview is not null) edition.Overview = request.Overview;
            if (request.Monitored.HasValue) edition.Monitored = request.Monitored.Value;

            edition.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(EditionDto.FromEntity(edition));
        })
        .WithName("UpdateEdition");

        // DELETE edition
        group.MapDelete("/{id:int}", async (
            ShortboxerrDbContext db,
            IHistoryService historyService,
            int id,
            bool deleteFiles = false,
            CancellationToken ct = default) =>
        {
            var edition = await db.EditionTitles
                .Include(e => e.Series)
                .FirstOrDefaultAsync(e => e.Id == id, ct);
            if (edition is null)
                return Results.NotFound(new { message = $"Edition {id} not found" });

            var editionTitle = edition.Title;
            var seriesId = edition.SeriesId;
            var seriesTitle = edition.Series?.Title ?? "Unknown";

            // TODO: If deleteFiles is true, delete associated file assets from disk

            db.EditionTitles.Remove(edition);
            await db.SaveChangesAsync(ct);

            // Record history event (pass null for editionId since the edition is now deleted)
            await historyService.RecordEditionDeletedAsync(
                null, seriesId, editionTitle, seriesTitle, deleteFiles, ct);

            return Results.NoContent();
        })
        .WithName("DeleteEdition");
    }
}

