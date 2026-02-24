using Microsoft.EntityFrameworkCore;
using Shortboxerr.Api.Caching;
using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class SeriesEndpoints
{
    public static void MapSeriesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/series")
            .WithTags("Series")
            .WithOpenApi();

        // GET all series (with paging, filtering, and server-side caching)
        group.MapGet("/", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            IPullListService pullListService,
            int page = 1,
            int pageSize = 20,
            string? sortKey = "title",
            string? sortDir = "asc",
            string? status = null,
            string? publisher = null,
            bool? monitored = null) =>
        {
            // Check if series-annual integration is enabled (defaults to true)
            var settings = await pullListService.GetSettingsAsync();
            var hideLinkedAnnuals = settings.EnableSeriesAnnualIntegration ?? true;
            
            // Generate cache key including query parameters
            var cacheKey = cacheService.GenerateKey(
                CacheKeys.SeriesList,
                page,
                pageSize,
                sortKey ?? "title",
                sortDir ?? "asc",
                status ?? "all",
                publisher ?? "all",
                monitored?.ToString() ?? "all",
                hideLinkedAnnuals.ToString());

            // Cache for 2 minutes
            var result = await cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var query = db.Series
                    .Include(s => s.Issues)
                    .Include(s => s.Editions)
                    .AsQueryable();

                // If series-annual integration is enabled, exclude linked annual series
                // They appear through their parent series' Annuals section instead
                if (hideLinkedAnnuals)
                {
                    query = query.Where(s => !s.ParentSeriesId.HasValue);
                }

                // Apply status filter
                if (!string.IsNullOrEmpty(status) && status.ToLowerInvariant() != "all")
                {
                    if (Enum.TryParse<SeriesStatus>(status, ignoreCase: true, out var parsedStatus))
                    {
                        query = query.Where(s => s.Status == parsedStatus);
                    }
                }

                // Apply publisher filter
                if (!string.IsNullOrEmpty(publisher) && publisher.ToLowerInvariant() != "all")
                {
                    query = query.Where(s => s.Publisher != null && 
                        s.Publisher.ToLower().Contains(publisher.ToLower()));
                }

                // Apply monitored filter
                if (monitored.HasValue)
                {
                    query = query.Where(s => s.Monitored == monitored.Value);
                }

                // Apply sorting
                query = (sortKey?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
                {
                    ("title", "desc") => query.OrderByDescending(s => s.SortTitle ?? s.Title),
                    ("title", _) => query.OrderBy(s => s.SortTitle ?? s.Title),
                    ("startyear", "desc") => query.OrderByDescending(s => s.StartYear),
                    ("startyear", _) => query.OrderBy(s => s.StartYear),
                    ("createdat", "desc") => query.OrderByDescending(s => s.CreatedAt),
                    ("createdat", _) => query.OrderBy(s => s.CreatedAt),
                    ("status", "desc") => query.OrderByDescending(s => s.Status),
                    ("status", _) => query.OrderBy(s => s.Status),
                    ("publisher", "desc") => query.OrderByDescending(s => s.Publisher),
                    ("publisher", _) => query.OrderBy(s => s.Publisher),
                    ("issuecount", "desc") => query.OrderByDescending(s => s.Issues.Count),
                    ("issuecount", _) => query.OrderBy(s => s.Issues.Count),
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
        .WithSummary("Get all series with optional filtering and sorting")
        .WithDescription("Supports filtering by status (Continuing/Ended/Hiatus), publisher, and monitored state. Supports sorting by title, startyear, createdat, status, publisher, issuecount.")
        .WithHttpCache(120); // 2 minutes HTTP cache for list view

        // GET filter options for series list
        group.MapGet("/filter-options", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            IPullListService pullListService) =>
        {
            // Check if series-annual integration is enabled (defaults to true)
            var settings = await pullListService.GetSettingsAsync();
            var hideLinkedAnnuals = settings.EnableSeriesAnnualIntegration ?? true;
            
            var cacheKey = cacheService.GenerateKey(CacheKeys.SeriesList, "filter-options", hideLinkedAnnuals.ToString());

            var options = await cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                // If series-annual integration is enabled, exclude linked annual series
                var baseQuery = hideLinkedAnnuals 
                    ? db.Series.Where(s => !s.ParentSeriesId.HasValue)
                    : db.Series.AsQueryable();
                
                // Get distinct publishers
                var publishers = await baseQuery
                    .Where(s => s.Publisher != null)
                    .Select(s => s.Publisher!)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToListAsync();

                // Get status counts
                var statusCounts = await baseQuery
                    .GroupBy(s => s.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                return new SeriesFilterOptions
                {
                    Statuses = Enum.GetValues<SeriesStatus>()
                        .Select(s => new FilterOption<SeriesStatus>
                        {
                            Value = s,
                            Label = s.ToString(),
                            Count = statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0
                        })
                        .ToList(),
                    Publishers = publishers.Select(p => new FilterOption<string>
                    {
                        Value = p,
                        Label = p,
                        Count = 0 // Can add count if needed
                    }).ToList(),
                    SortOptions = new List<SortOption>
                    {
                        new() { Value = "title", Label = "Title" },
                        new() { Value = "startyear", Label = "Start Year" },
                        new() { Value = "createdat", Label = "Date Added" },
                        new() { Value = "status", Label = "Status" },
                        new() { Value = "publisher", Label = "Publisher" },
                        new() { Value = "issuecount", Label = "Issue Count" }
                    },
                    TotalSeries = await baseQuery.CountAsync()
                };
            }, TimeSpan.FromMinutes(5));

            return Results.Ok(options);
        })
        .WithName("GetSeriesFilterOptions")
        .WithSummary("Get available filter options for series list")
        .WithHttpCache(300);

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
                    .Include(s => s.LinkedAnnualSeries) // Include linked annual series
                        .ThenInclude(a => a.Issues)
                    .AsSplitQuery() // Use split queries for multiple collection navigations
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

        // GET all annuals for a series (includes issues from linked annual series - Mylar3 parity)
        group.MapGet("/{id:int}/annuals", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id) =>
        {
            var series = await db.Series
                .Include(s => s.Issues)
                .Include(s => s.LinkedAnnualSeries)
                    .ThenInclude(a => a.Issues)
                .AsSplitQuery() // Use split queries for multiple collection navigations
                .FirstOrDefaultAsync(s => s.Id == id);
                
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            // Collect all annual issues:
            // 1. Issues from the main series marked as annuals
            var annualIssues = series.Issues?
                .Where(i => i.IsAnnual)
                .Select(IssueDto.FromEntity)
                .ToList() ?? new List<IssueDto>();
            
            // 2. All issues from linked annual series
            if (series.LinkedAnnualSeries != null)
            {
                foreach (var annualSeries in series.LinkedAnnualSeries)
                {
                    var linkedIssues = annualSeries.Issues?
                        .Select(i => 
                        {
                            var dto = IssueDto.FromEntity(i);
                            // Mark these issues as coming from linked series for UI clarity
                            return dto with { LinkedAnnualSeriesTitle = annualSeries.Title };
                        })
                        .ToList() ?? new List<IssueDto>();
                    
                    annualIssues.AddRange(linkedIssues);
                }
            }
            
            // Sort by release date, then issue number
            annualIssues = annualIssues
                .OrderBy(i => i.ReleaseDate ?? i.CoverDate)
                .ThenBy(i => i.IssueNumber)
                .ToList();
            
            return Results.Ok(new SeriesAnnualsResponse
            {
                SeriesId = id,
                SeriesTitle = series.Title,
                TotalAnnuals = annualIssues.Count,
                LinkedAnnualSeriesCount = series.LinkedAnnualSeries?.Count ?? 0,
                Annuals = annualIssues
            });
        })
        .WithName("GetSeriesAnnuals")
        .WithHttpCache(120);

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

        // PUT set series status manually
        group.MapPut("/{id:int}/status", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id,
            SetSeriesStatusRequest request) =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            var previousStatus = series.Status;
            var previousSource = series.StatusSource;

            series.Status = request.Status;
            series.StatusSource = request.IsManualOverride ? StatusSource.Manual : StatusSource.Auto;
            series.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // Invalidate caches
            cacheService.RemoveByPrefix(CacheKeys.SeriesList);
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.SeriesDetail, id));

            return Results.Ok(new SetSeriesStatusResponse
            {
                SeriesId = id,
                Title = series.Title,
                PreviousStatus = previousStatus,
                NewStatus = series.Status,
                PreviousSource = previousSource,
                NewSource = series.StatusSource
            });
        })
        .WithName("SetSeriesStatus");

        // DELETE reset series status to auto
        group.MapDelete("/{id:int}/status/override", async (
            ShortboxerrDbContext db,
            ICacheService cacheService,
            int id) =>
        {
            var series = await db.Series.FindAsync(id);
            if (series is null)
                return Results.NotFound(new { message = $"Series {id} not found" });

            if (series.StatusSource != StatusSource.Manual)
                return Results.Ok(new { message = "Status is not manually overridden", seriesId = id });

            // Reset to auto - the next metadata refresh will recalculate
            series.StatusSource = StatusSource.Auto;
            series.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // Invalidate caches
            cacheService.RemoveByPrefix(CacheKeys.SeriesList);
            cacheService.Remove(cacheService.GenerateKey(CacheKeys.SeriesDetail, id));

            return Results.Ok(new { 
                message = "Manual override removed. Status will be recalculated on next metadata refresh.", 
                seriesId = id,
                currentStatus = series.Status.ToString()
            });
        })
        .WithName("ResetSeriesStatusOverride");

        // GET upcoming releases for a series (from WalkSoftly cache)
        group.MapGet("/{id:int}/upcoming", async (
            int id,
            IPullListService pullListService,
            int weeksAhead = 4,
            CancellationToken cancellationToken = default) =>
        {
            var result = await pullListService.GetSeriesUpcomingReleasesAsync(id, weeksAhead, cancellationToken);
            
            if (result.SeriesTitle == "Unknown")
                return Results.NotFound(new { message = $"Series {id} not found" });
            
            return Results.Ok(result);
        })
        .WithName("GetSeriesUpcomingReleases")
        .WithDescription("Gets upcoming releases from WalkSoftly cache that haven't been indexed by ComicVine yet")
        .Produces<SeriesUpcomingReleasesResult>(200)
        .Produces(404);
    }
}

public record SetSeriesStatusRequest
{
    public SeriesStatus Status { get; init; }
    public bool IsManualOverride { get; init; } = true;
}

public record SetSeriesStatusResponse
{
    public int SeriesId { get; init; }
    public string Title { get; init; } = "";
    public SeriesStatus PreviousStatus { get; init; }
    public SeriesStatus NewStatus { get; init; }
    public StatusSource PreviousSource { get; init; }
    public StatusSource NewSource { get; init; }
}

/// <summary>
/// Available filter and sort options for the series list.
/// </summary>
public record SeriesFilterOptions
{
    /// <summary>Available status values with counts.</summary>
    public List<FilterOption<SeriesStatus>> Statuses { get; init; } = new();
    
    /// <summary>Available publishers.</summary>
    public List<FilterOption<string>> Publishers { get; init; } = new();
    
    /// <summary>Available sort options.</summary>
    public List<SortOption> SortOptions { get; init; } = new();
    
    /// <summary>Total number of series.</summary>
    public int TotalSeries { get; init; }
}

/// <summary>
/// A filter option with its value, label, and count.
/// </summary>
public record FilterOption<T>
{
    public T Value { get; init; } = default!;
    public string Label { get; init; } = "";
    public int Count { get; init; }
}

/// <summary>
/// A sort option with its value and label.
/// </summary>
public record SortOption
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}

/// <summary>
/// Response containing all annual issues for a series (including linked annual series).
/// </summary>
public record SeriesAnnualsResponse
{
    public int SeriesId { get; init; }
    public string SeriesTitle { get; init; } = "";
    public int TotalAnnuals { get; init; }
    public int LinkedAnnualSeriesCount { get; init; }
    public List<IssueDto> Annuals { get; init; } = new();
}