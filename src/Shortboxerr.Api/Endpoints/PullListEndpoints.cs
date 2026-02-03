using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;

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
        .WithDescription("Gets this week's comic releases")
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
        .WithDescription("Gets releases for a specific week")
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
        .WithDescription("Gets all ComicVine releases this week (discovery mode)")
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
        .WithDescription("Gets all ComicVine releases for a specific week (discovery mode)")
        .Produces<WeeklyDiscoveryList>(200);

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
                cancellationToken);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("AddSeriesFromDiscovery")
        .WithDescription("Adds a series from discovery and optionally marks an issue as wanted")
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

        #endregion
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
    SeriesMonitoringMode MonitoringMode = SeriesMonitoringMode.FutureIssues);

#endregion
