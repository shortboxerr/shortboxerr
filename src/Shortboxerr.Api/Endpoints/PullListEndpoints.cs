using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for the weekly pull list and release calendar.
/// </summary>
public static class PullListEndpoints
{
    public static void MapPullListEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/pulllist")
            .WithTags("Pull List");

        #region Calendar & Release Tracking

        // GET /api/v1/pulllist/week - this week's releases
        group.MapGet("/week", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] string? publishers,
            [FromQuery] string? statuses,
            [FromQuery] bool? monitoredOnly,
            CancellationToken cancellationToken) =>
        {
            var filter = BuildFilter(publishers, statuses, monitoredOnly);
            var result = await pullListService.GetThisWeekAsync(filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetThisWeekPullList")
        .WithDescription("Gets this week's comic releases.")
        .Produces<WeeklyPullList>(200);

        // GET /api/v1/pulllist/week/{date} - releases for a specific week
        group.MapGet("/week/{date}", async (
            DateTime date,
            [FromServices] IPullListService pullListService,
            [FromQuery] string? publishers,
            [FromQuery] string? statuses,
            [FromQuery] bool? monitoredOnly,
            CancellationToken cancellationToken) =>
        {
            var filter = BuildFilter(publishers, statuses, monitoredOnly);
            var result = await pullListService.GetWeeklyReleasesAsync(date, filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetWeekPullList")
        .WithDescription("Gets releases for a specific week.")
        .Produces<WeeklyPullList>(200);

        // GET /api/v1/pulllist/upcoming - upcoming releases
        group.MapGet("/upcoming", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] int? weeks,
            [FromQuery] string? publishers,
            [FromQuery] string? statuses,
            [FromQuery] bool? monitoredOnly,
            CancellationToken cancellationToken) =>
        {
            var filter = BuildFilter(publishers, statuses, monitoredOnly);
            var result = await pullListService.GetUpcomingReleasesAsync(
                Math.Min(weeks ?? 4, 12), filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUpcomingReleases")
        .WithDescription("Gets upcoming comic releases")
        .Produces<List<WeeklyPullList>>(200);

        // GET /api/v1/pulllist/past - past releases
        group.MapGet("/past", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] int? weeks,
            [FromQuery] string? publishers,
            [FromQuery] string? statuses,
            [FromQuery] bool? monitoredOnly,
            CancellationToken cancellationToken) =>
        {
            var filter = BuildFilter(publishers, statuses, monitoredOnly);
            var result = await pullListService.GetPastReleasesAsync(
                Math.Min(weeks ?? 4, 12), filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetPastReleases")
        .WithDescription("Gets past comic releases")
        .Produces<List<WeeklyPullList>>(200);

        // GET /api/v1/pulllist/calendar - full calendar view
        group.MapGet("/calendar", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? publishers,
            [FromQuery] string? statuses,
            [FromQuery] bool? monitoredOnly,
            CancellationToken cancellationToken) =>
        {
            var start = startDate ?? DateTime.Today.AddDays(-7);
            var end = endDate ?? DateTime.Today.AddDays(28);
            var filter = BuildFilter(publishers, statuses, monitoredOnly);
            var result = await pullListService.GetCalendarAsync(start, end, filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetReleaseCalendar")
        .WithDescription("Gets the release calendar for a date range")
        .Produces<ReleaseCalendar>(200);

        #endregion

        #region Discovery (Mylar3 "This Week" Parity)

        // GET /api/v1/pulllist/discover/week - discover all releases this week
        group.MapGet("/discover/week", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] string? publishers,
            [FromQuery] bool? inLibraryOnly,
            [FromQuery] bool? newOnly,
            [FromQuery] bool? includeAnnuals,
            [FromQuery] bool? includeSpecials,
            CancellationToken cancellationToken) =>
        {
            var filter = new DiscoveryFilter
            {
                Publishers = string.IsNullOrEmpty(publishers) ? null : publishers.Split(',').ToList(),
                InLibraryOnly = inLibraryOnly,
                NewOnly = newOnly,
                IncludeAnnuals = includeAnnuals ?? true,
                IncludeSpecials = includeSpecials ?? true
            };
            var result = await pullListService.GetWeeklyDiscoveryAsync(DateTime.Today, filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetWeeklyDiscovery")
        .WithDescription("Gets all ComicVine releases this week (discovery mode).")
        .Produces<WeeklyDiscoveryList>(200);

        // GET /api/v1/pulllist/discover/week/{date} - discover releases for a specific week
        group.MapGet("/discover/week/{date}", async (
            DateTime date,
            [FromServices] IPullListService pullListService,
            [FromQuery] string? publishers,
            [FromQuery] bool? inLibraryOnly,
            [FromQuery] bool? newOnly,
            [FromQuery] bool? includeAnnuals,
            [FromQuery] bool? includeSpecials,
            CancellationToken cancellationToken) =>
        {
            var filter = new DiscoveryFilter
            {
                Publishers = string.IsNullOrEmpty(publishers) ? null : publishers.Split(',').ToList(),
                InLibraryOnly = inLibraryOnly,
                NewOnly = newOnly,
                IncludeAnnuals = includeAnnuals ?? true,
                IncludeSpecials = includeSpecials ?? true
            };
            var result = await pullListService.GetWeeklyDiscoveryAsync(date, filter, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetWeeklyDiscoveryByDate")
        .WithDescription("Gets all ComicVine releases for a specific week (discovery mode).")
        .Produces<WeeklyDiscoveryList>(200);

        // GET /api/v1/pulllist/discover/publishers - get available publishers for filter
        group.MapGet("/discover/publishers", async (
            [FromServices] IPullListService pullListService,
            [FromQuery] DateTime? weekOf,
            [FromQuery] bool? includeComicVineLookup,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.GetDiscoveryPublishersAsync(
                weekOf ?? DateTime.Today,
                includeComicVineLookup ?? false,
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetDiscoveryPublishers")
        .WithDescription("Gets available publishers for discovery filter dropdown.")
        .Produces<DiscoveryPublishersResult>(200);

        // POST /api/v1/pulllist/discover/add-issue - add one-off issue
        group.MapPost("/discover/add-issue", async (
            [FromBody] AddOneOffRequest request,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.AddIssueOneOffAsync(request.ComicVineIssueId, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("AddIssueOneOff")
        .WithDescription("Adds a single issue as wanted without fully adding the series")
        .Produces<AddOneOffResult>(200)
        .Produces<AddOneOffResult>(400);

        // POST /api/v1/pulllist/discover/add-series - add series from discovery
        group.MapPost("/discover/add-series", async (
            [FromBody] AddSeriesFromDiscoveryRequest request,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.AddSeriesFromDiscoveryAsync(
                request.ComicVineVolumeId,
                request.MarkIssueWantedComicVineId,
                request.MonitoringMode,
                request.ExpectedPublisher,
                request.SeriesTitle,
                request.ExpectedIssueNumber,
                cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("AddSeriesFromDiscovery")
        .WithDescription("Adds a series from discovery and optionally marks an issue as wanted. Validates volume using publisher and issue count.")
        .Produces<AddFromDiscoveryResult>(200)
        .Produces<AddFromDiscoveryResult>(400);

        #endregion

        #region Issue Management

        // POST /api/v1/pulllist/issues/{id}/wanted - mark as wanted
        group.MapPost("/issues/{id}/wanted", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.MarkAsWantedAsync(id, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("MarkIssueWanted")
        .WithDescription("Marks an issue as wanted")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        // POST /api/v1/pulllist/issues/{id}/owned - mark as owned
        group.MapPost("/issues/{id}/owned", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.MarkAsOwnedAsync(id, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("MarkIssueOwned")
        .WithDescription("Marks an issue as owned")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        // POST /api/v1/pulllist/issues/{id}/skipped - mark as skipped
        group.MapPost("/issues/{id}/skipped", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.MarkAsSkippedAsync(id, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("MarkIssueSkipped")
        .WithDescription("Marks an issue as skipped")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        // POST /api/v1/pulllist/issues/bulk - bulk update
        group.MapPost("/issues/bulk", async (
            [FromBody] BulkUpdateRequest request,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.BulkUpdateStatusAsync(
                request.IssueIds, request.Status, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("BulkUpdateIssues")
        .WithDescription("Bulk updates issue statuses")
        .Produces<PullListBulkResult>(200)
        .Produces<PullListBulkResult>(400);

        #endregion

        #region Series Monitoring

        // GET /api/v1/pulllist/series/{id}/monitoring - get monitoring mode
        group.MapGet("/series/{id}/monitoring", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var mode = await pullListService.GetSeriesMonitoringModeAsync(id, cancellationToken);
            return Results.Ok(new { seriesId = id, monitoringMode = mode.ToString() });
        })
        .WithName("GetSeriesMonitoring")
        .WithDescription("Gets the monitoring mode for a series")
        .Produces(200);

        // PUT /api/v1/pulllist/series/{id}/monitoring - set monitoring mode
        group.MapPut("/series/{id}/monitoring", async (
            int id,
            [FromBody] SetMonitoringRequest request,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.SetSeriesMonitoringModeAsync(
                id, request.Mode, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("SetSeriesMonitoring")
        .WithDescription("Sets the monitoring mode for a series")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        #endregion

        #region Auto-Processing

        // POST /api/v1/pulllist/process/series/{id} - process new issues for series
        group.MapPost("/process/series/{id}", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.ProcessNewIssuesAsync(id, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ProcessSeriesNewIssues")
        .WithDescription("Processes new issues for a series based on monitoring mode")
        .Produces<AutoAddResult>(200)
        .Produces<AutoAddResult>(400);

        // POST /api/v1/pulllist/process/releaseday - process release day
        group.MapPost("/process/releaseday", async (
            [FromQuery] DateTime? date,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var releaseDate = date ?? DateTime.Today;
            var result = await pullListService.ProcessReleaseDayAsync(releaseDate, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ProcessReleaseDay")
        .WithDescription("Processes all releases for a given date")
        .Produces<AutoAddResult>(200)
        .Produces<AutoAddResult>(400);

        #endregion

        #region Statistics

        // GET /api/v1/pulllist/stats - get statistics
        group.MapGet("/stats", async (
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var stats = await pullListService.GetStatsAsync(cancellationToken);
            return Results.Ok(stats);
        })
        .WithName("GetPullListStats")
        .WithDescription("Gets pull list statistics")
        .Produces<PullListStats>(200);

        // GET /api/v1/pulllist/config-status - get configuration status for UX
        group.MapGet("/config-status", async (
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var status = await pullListService.GetConfigStatusAsync(cancellationToken);
            return Results.Ok(status);
        })
        .WithName("GetPullListConfigStatus")
        .WithDescription("Gets pull list configuration status for UX improvements")
        .Produces<PullListConfigStatus>(200);

        #endregion

        #region Settings

        // GET /api/v1/pulllist/settings - get pull list settings
        group.MapGet("/settings", async (
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var settings = await pullListService.GetSettingsAsync(cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetPullListSettings")
        .WithDescription("Gets pull list configuration settings")
        .Produces<PullListSettings>(200);

        // PUT /api/v1/pulllist/settings - update pull list settings
        group.MapPut("/settings", async (
            [FromBody] PullListSettings settings,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.UpdateSettingsAsync(settings, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("UpdatePullListSettings")
        .WithDescription("Updates pull list configuration settings")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        // GET /api/v1/pulllist/series/{id}/settings - get per-series settings
        group.MapGet("/series/{id}/settings", async (
            int id,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var settings = await pullListService.GetSeriesSettingsAsync(id, cancellationToken);
            return settings != null 
                ? Results.Ok(settings) 
                : Results.Ok(new SeriesPullListSettings { SeriesId = id });
        })
        .WithName("GetSeriesPullListSettings")
        .WithDescription("Gets per-series pull list settings")
        .Produces<SeriesPullListSettings>(200);

        // PUT /api/v1/pulllist/series/{id}/settings - update per-series settings
        group.MapPut("/series/{id}/settings", async (
            int id,
            [FromBody] SeriesPullListSettings settings,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            settings.SeriesId = id; // Ensure ID matches route
            var result = await pullListService.UpdateSeriesSettingsAsync(settings, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("UpdateSeriesPullListSettings")
        .WithDescription("Updates per-series pull list settings")
        .Produces<PullListActionResult>(200)
        .Produces<PullListActionResult>(400);

        #endregion

        #region Weekly Export (Mylar3 Parity)

        // POST /api/v1/pulllist/export - export current week's pull list
        group.MapPost("/export", async (
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.ExportCurrentWeekAsync(cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ExportCurrentWeek")
        .WithDescription("Exports the current week's pull list to a file")
        .Produces<WeeklyExportResult>(200)
        .Produces<WeeklyExportResult>(400);

        // POST /api/v1/pulllist/export/{date} - export specific week's pull list
        group.MapPost("/export/{date}", async (
            DateTime date,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var result = await pullListService.ExportWeekAsync(date, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ExportWeek")
        .WithDescription("Exports a specific week's pull list to a file")
        .Produces<WeeklyExportResult>(200)
        .Produces<WeeklyExportResult>(400);

        // GET /api/v1/pulllist/export/history - get export history
        group.MapGet("/export/history", async (
            [FromQuery] int? limit,
            [FromServices] IPullListService pullListService,
            CancellationToken cancellationToken) =>
        {
            var history = await pullListService.GetExportHistoryAsync(limit ?? 10, cancellationToken);
            return Results.Ok(history);
        })
        .WithName("GetExportHistory")
        .WithDescription("Gets the history of weekly exports")
        .Produces<List<WeeklyExportInfo>>(200);

        // GET /api/v1/pulllist/export/compare/{date} - get pull list comparison data for debugging
        group.MapGet("/export/compare/{date}", async (
            DateTime date,
            [FromServices] IPullListService pullListService,
            [FromServices] IComicVineClient comicVineClient,
            [FromServices] ShortboxerrDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            // Get week boundaries
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
            var weekStart = date.Date.AddDays(-diff);
            var weekEnd = weekStart.AddDays(7);
            var releaseDay = weekStart.AddDays((int)DayOfWeek.Wednesday);
            
            // Get our pull list (from library)
            var pullList = await pullListService.GetWeeklyReleasesAsync(date, null, cancellationToken);
            
            // Get discovery data (from ComicVine)
            var discovery = await pullListService.GetWeeklyDiscoveryAsync(date, null, cancellationToken);
            
            // Get raw ComicVine count
            var dateFilter = $"{weekStart:yyyy-MM-dd}|{weekEnd.AddDays(-1):yyyy-MM-dd}";
            var cvResult = await comicVineClient.GetIssuesByStoreDateAsync(dateFilter, 0, 1, cancellationToken);
            
            // Get library stats
            var librarySeriesCount = await dbContext.Series.CountAsync(cancellationToken);
            var monitoredSeriesCount = await dbContext.Series.CountAsync(s => s.Monitored, cancellationToken);
            var matchedSeriesCount = await dbContext.Series.CountAsync(s => s.ComicVineId != null, cancellationToken);
            
            // Get publisher breakdown from discovery
            var publisherBreakdown = discovery.Issues
                .Where(i => !string.IsNullOrEmpty(i.Publisher))
                .GroupBy(i => i.Publisher!)
                .Select(g => new { Publisher = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToDictionary(x => x.Publisher, x => x.Count);
            
            return Results.Ok(new PullListComparisonResult
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                ReleaseDay = releaseDay,
                
                // Our library pull list
                LibraryIssueCount = pullList.TotalCount,
                LibraryWantedCount = pullList.WantedCount,
                LibraryOwnedCount = pullList.OwnedCount,
                LibrarySkippedCount = pullList.SkippedCount,
                
                // ComicVine discovery
                DiscoveryIssueCount = discovery.Issues.Count,
                DiscoveryInLibraryCount = discovery.Issues.Count(i => i.IsInLibrary),
                DiscoveryNewCount = discovery.Issues.Count(i => !i.IsInLibrary),
                
                // Raw ComicVine total
                ComicVineTotalIssues = cvResult.TotalResults,
                
                // Library stats
                TotalSeriesInLibrary = librarySeriesCount,
                MonitoredSeriesCount = monitoredSeriesCount,
                MatchedSeriesCount = matchedSeriesCount,
                
                // Publisher breakdown
                IssuesByPublisher = publisherBreakdown,
                
                // Data source info
                DataSources = new DataSourceInfo
                {
                    PullListSource = "Local Database (monitored series)",
                    DiscoverySource = "ComicVine API (store_date filter)",
                    DateField = "store_date",
                    Notes = new[]
                    {
                        "ComicVine may have delays (up to 4+ days) in updating new releases",
                        "External release schedules may have fresher data than ComicVine",
                        "Library pull list only shows issues from monitored series",
                        "Discovery shows ALL ComicVine issues for the week"
                    }
                },
                
                // Cache info
                CacheMetadata = discovery.CacheMetadata,
                
                // Sample issues for comparison
                SampleLibraryIssues = pullList.Issues.Take(10).Select(i => new SampleIssue
                {
                    SeriesTitle = i.SeriesTitle,
                    IssueNumber = i.IssueNumber,
                    Publisher = i.Publisher,
                    StoreDate = i.StoreDate,
                    Status = i.Status.ToString()
                }).ToList(),
                
                SampleDiscoveryIssues = discovery.Issues.Take(10).Select(i => new SampleIssue
                {
                    SeriesTitle = i.SeriesTitle,
                    IssueNumber = i.IssueNumber,
                    Publisher = i.Publisher,
                    StoreDate = i.StoreDate,
                    Status = i.Status?.ToString() ?? "N/A",
                    IsInLibrary = i.IsInLibrary,
                    ComicVineIssueId = i.ComicVineIssueId
                }).ToList()
            });
        })
        .WithName("GetPullListComparison")
        .WithDescription("Gets detailed pull list comparison data for debugging Mylar3 parity")
        .Produces<PullListComparisonResult>(200);

        #endregion

        #region Discovery Refresh

        // DELETE /api/v1/pulllist/discovery/cache - clear all discovery cache and trigger refresh
        group.MapDelete("/discovery/cache", async (
            [FromQuery] bool refresh,
            [FromServices] ShortboxerrDbContext dbContext,
            [FromServices] ICacheService cacheService,
            [FromServices] Infrastructure.BackgroundServices.DiscoveryRefreshBackgroundService refreshService,
            CancellationToken cancellationToken) =>
        {
            // Clear database cache
            var deletedCount = await dbContext.CachedDiscoveryWeeks.ExecuteDeleteAsync(cancellationToken);
            
            // Clear memory cache
            var memoryCacheCleared = cacheService.RemoveByPrefix(CacheKeys.PullListDiscovery);
            
            var result = new 
            { 
                Success = true, 
                DatabaseEntriesDeleted = deletedCount,
                MemoryCacheEntriesCleared = memoryCacheCleared,
                Message = $"Cleared {deletedCount} database entries and {memoryCacheCleared} memory cache entries"
            };
            
            // Optionally trigger refresh to repopulate
            if (refresh)
            {
                await refreshService.TriggerRefreshAsync(cancellationToken);
                return Results.Ok(new 
                { 
                    result.Success,
                    result.DatabaseEntriesDeleted,
                    result.MemoryCacheEntriesCleared,
                    RefreshTriggered = true,
                    Message = result.Message + ". Refresh triggered."
                });
            }
            
            return Results.Ok(result);
        })
        .WithName("ClearDiscoveryCache")
        .WithDescription("Clears all cached discovery data from database and memory. Set refresh=true to immediately repopulate.")
        .Produces<object>(200);

        // POST /api/v1/pulllist/discovery/refresh - trigger manual discovery refresh
        group.MapPost("/discovery/refresh", async (
            [FromServices] Infrastructure.BackgroundServices.DiscoveryRefreshBackgroundService refreshService,
            CancellationToken cancellationToken) =>
        {
            await refreshService.TriggerRefreshAsync(cancellationToken);
            return Results.Ok(new { Success = true, Message = "Discovery refresh triggered" });
        })
        .WithName("TriggerDiscoveryRefresh")
        .WithDescription("Triggers a manual refresh of ComicVine discovery data")
        .Produces<object>(200);

        // GET /api/v1/pulllist/discovery/status - get discovery refresh status
        group.MapGet("/discovery/status", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync<ComicVineSettings>("comicvine", new(), cancellationToken) 
                ?? new ComicVineSettings();
            var lastRefresh = await settingsService.GetAsync<DateTime?>("comicvine_discovery_last_refresh", null, cancellationToken);
            
            return Results.Ok(new DiscoveryRefreshStatus
            {
                Enabled = settings.DiscoveryRefreshEnabled,
                RefreshIntervalHours = settings.DiscoveryRefreshIntervalHours,
                WeeksAhead = settings.DiscoveryRefreshWeeksAhead,
                AllowedHours = settings.DiscoveryRefreshAllowedHours,
                LastRefresh = lastRefresh,
                NextRefreshEstimate = lastRefresh.HasValue 
                    ? lastRefresh.Value.AddHours(settings.DiscoveryRefreshIntervalHours)
                    : null
            });
        })
        .WithName("GetDiscoveryRefreshStatus")
        .WithDescription("Gets the status of ComicVine discovery refresh")
        .Produces<DiscoveryRefreshStatus>(200);

        // POST /api/v1/pulllist/discovery/enrich-covers - trigger manual cover enrichment
        group.MapPost("/discovery/enrich-covers", async (
            [FromQuery] bool? force,
            [FromServices] Infrastructure.BackgroundServices.DiscoveryCoverEnrichmentService enrichmentService,
            CancellationToken cancellationToken) =>
        {
            var forceEnrich = force ?? false;
            await enrichmentService.TriggerEnrichmentAsync(cancellationToken, forceEnrich);
            var message = forceEnrich 
                ? "Cover enrichment triggered (force mode - bypassing cooldown). Missing covers will be fetched from Metron."
                : "Cover enrichment triggered. Missing covers will be fetched from Metron.";
            return Results.Ok(new { Success = true, Message = message, Force = forceEnrich });
        })
        .WithName("TriggerCoverEnrichment")
        .WithDescription("Triggers manual cover enrichment for cached discovery issues (fetches missing covers from Metron). Use force=true to bypass the 7-day cooldown.")
        .Produces<object>(200);

        // POST /api/v1/pulllist/discovery/refresh-covers - trigger ComicVine cover refresh check
        group.MapPost("/discovery/refresh-covers", async (
            [FromServices] Infrastructure.BackgroundServices.DiscoveryCoverEnrichmentService enrichmentService,
            CancellationToken cancellationToken) =>
        {
            await enrichmentService.TriggerCoverRefreshAsync(cancellationToken);
            return Results.Ok(new { Success = true, Message = "Cover refresh check triggered. Issues with Metron fallback covers will be checked for ComicVine updates." });
        })
        .WithName("TriggerCoverRefresh")
        .WithDescription("Checks if ComicVine now has covers for issues previously using Metron fallback covers")
        .Produces<object>(200);

        #endregion

        #region Release Day Processing

        // POST /api/v1/pulllist/releaseday/process - trigger manual release day processing
        group.MapPost("/releaseday/process", async (
            [FromQuery] DateTime? date,
            [FromServices] Infrastructure.BackgroundServices.ReleaseDayBackgroundService releaseDayService,
            CancellationToken cancellationToken) =>
        {
            await releaseDayService.TriggerProcessingAsync(date, cancellationToken);
            return Results.Ok(new { Success = true, Message = $"Release day processing triggered for {(date ?? DateTime.Today).ToShortDateString()}" });
        })
        .WithName("TriggerReleaseDayProcessing")
        .WithDescription("Triggers manual release day processing to auto-add issues to wanted list")
        .Produces<object>(200);

        // GET /api/v1/pulllist/releaseday/status - get release day processing status
        group.MapGet("/releaseday/status", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync<PullListSettings>("pulllist", new(), cancellationToken) ?? new PullListSettings();
            var lastProcessed = await settingsService.GetAsync<DateTime?>("pulllist_release_day_last_processed", null, cancellationToken);
            
            return Results.Ok(new ReleaseDayStatus
            {
                Enabled = settings.AutoAddToWanted,
                ReleaseDay = settings.ReleaseDay,
                ProcessingHours = settings.ReleaseDayProcessingHours,
                LastProcessed = lastProcessed,
                NextProcessingDate = GetNextReleaseDay(settings.ReleaseDay)
            });
        })
        .WithName("GetReleaseDayStatus")
        .WithDescription("Gets the status of release day auto-processing")
        .Produces<ReleaseDayStatus>(200);

        #endregion
    }

    private static DateTime GetNextReleaseDay(DayOfWeek releaseDay)
    {
        var today = DateTime.Today;
        var daysUntilReleaseDay = ((int)releaseDay - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilReleaseDay == 0 && DateTime.Now.Hour >= 23)
            daysUntilReleaseDay = 7; // Already past release day today, get next week
        return today.AddDays(daysUntilReleaseDay);
    }

    private static PullListFilter? BuildFilter(
        string? publishers,
        string? statuses,
        bool? monitoredOnly)
    {
        if (string.IsNullOrEmpty(publishers) && string.IsNullOrEmpty(statuses) && monitoredOnly == null)
            return null;

        var filter = new PullListFilter { MonitoredOnly = monitoredOnly };

        if (!string.IsNullOrEmpty(publishers))
            filter.Publishers = publishers.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!string.IsNullOrEmpty(statuses))
        {
            filter.Statuses = statuses
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Enum.TryParse<IssueStatus>(s, true, out var status) ? status : (IssueStatus?)null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToList();
        }

        return filter;
    }
}

#region Request DTOs

public record BulkUpdateRequest(List<int> IssueIds, IssueStatus Status);
public record SetMonitoringRequest(SeriesMonitoringMode Mode);
public record AddOneOffRequest(int ComicVineIssueId);
public record AddSeriesFromDiscoveryRequest(
    int ComicVineVolumeId, 
    int? MarkIssueWantedComicVineId = null,
    SeriesMonitoringMode MonitoringMode = SeriesMonitoringMode.FutureIssues,
    string? ExpectedPublisher = null,
    string? SeriesTitle = null,
    decimal? ExpectedIssueNumber = null);

#endregion

#region Response DTOs

/// <summary>
/// Status of the ComicVine discovery refresh background service.
/// </summary>
public class DiscoveryRefreshStatus
{
    /// <summary>Whether automatic discovery refresh is enabled.</summary>
    public bool Enabled { get; set; }
    
    /// <summary>Refresh interval in hours.</summary>
    public int RefreshIntervalHours { get; set; }
    
    /// <summary>Number of weeks ahead to refresh.</summary>
    public int WeeksAhead { get; set; }
    
    /// <summary>Hours during which refresh is allowed (empty = all hours).</summary>
    public List<int> AllowedHours { get; set; } = new();
    
    /// <summary>When the last refresh occurred.</summary>
    public DateTime? LastRefresh { get; set; }
    
    /// <summary>Estimated next refresh time (based on interval).</summary>
    public DateTime? NextRefreshEstimate { get; set; }
}

/// <summary>
/// Status of the release day auto-processing background service.
/// </summary>
public class ReleaseDayStatus
{
    /// <summary>Whether automatic release day processing is enabled.</summary>
    public bool Enabled { get; set; }
    
    /// <summary>Day of week for comic releases (default: Wednesday).</summary>
    public DayOfWeek ReleaseDay { get; set; }
    
    /// <summary>Hours during which processing is allowed (empty = all hours).</summary>
    public List<int> ProcessingHours { get; set; } = new();
    
    /// <summary>When the last processing occurred.</summary>
    public DateTime? LastProcessed { get; set; }
    
    /// <summary>Next release day date.</summary>
    public DateTime NextProcessingDate { get; set; }
}

/// <summary>
/// Pull list comparison result for debugging Mylar3 parity.
/// </summary>
public class PullListComparisonResult
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public DateTime ReleaseDay { get; set; }
    
    // Library pull list stats
    public int LibraryIssueCount { get; set; }
    public int LibraryWantedCount { get; set; }
    public int LibraryOwnedCount { get; set; }
    public int LibrarySkippedCount { get; set; }
    
    // Discovery stats (all ComicVine issues for week)
    public int DiscoveryIssueCount { get; set; }
    public int DiscoveryInLibraryCount { get; set; }
    public int DiscoveryNewCount { get; set; }
    
    // Raw ComicVine total
    public int ComicVineTotalIssues { get; set; }
    
    // Library stats
    public int TotalSeriesInLibrary { get; set; }
    public int MonitoredSeriesCount { get; set; }
    public int MatchedSeriesCount { get; set; }
    
    // Publisher breakdown
    public Dictionary<string, int> IssuesByPublisher { get; set; } = new();
    
    // Data source info
    public DataSourceInfo DataSources { get; set; } = new();
    
    // Cache metadata
    public PullListCacheMetadata? CacheMetadata { get; set; }
    
    // Sample issues for comparison
    public List<SampleIssue> SampleLibraryIssues { get; set; } = new();
    public List<SampleIssue> SampleDiscoveryIssues { get; set; } = new();
}

/// <summary>
/// Information about data sources used.
/// </summary>
public class DataSourceInfo
{
    public string PullListSource { get; set; } = string.Empty;
    public string DiscoverySource { get; set; } = string.Empty;
    public string DateField { get; set; } = string.Empty;
    public string[] Notes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Sample issue for comparison display.
/// </summary>
public class SampleIssue
{
    public string? SeriesTitle { get; set; }
    public decimal IssueNumber { get; set; }
    public string? Publisher { get; set; }
    public DateTime? StoreDate { get; set; }
    public string? Status { get; set; }
    public bool? IsInLibrary { get; set; }
    public int? ComicVineIssueId { get; set; }
}

#endregion
