using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class MetadataRefreshEndpoints
{
    public static void MapMetadataRefreshEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/metadata")
            .WithTags("Metadata Refresh")
            .WithOpenApi();

        // Get refresh settings
        group.MapGet("/settings", async (
            IMetadataRefreshService refreshService,
            CancellationToken cancellationToken) =>
        {
            var settings = await refreshService.GetSettingsAsync(cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetMetadataRefreshSettings");

        // Get stale series count
        group.MapGet("/stale-count", async (
            IMetadataRefreshService refreshService,
            CancellationToken cancellationToken) =>
        {
            var count = await refreshService.GetStaleSeriesCountAsync(cancellationToken);
            return Results.Ok(new { staleSeriesCount = count });
        })
        .WithName("GetStaleSeriesCount");

        // Refresh a single series
        group.MapPost("/series/{seriesId:int}/refresh", async (
            IMetadataRefreshService refreshService,
            int seriesId,
            [FromQuery] bool force = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await refreshService.RefreshSeriesAsync(seriesId, force, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("MetadataRefreshSeries");

        // Refresh series issues only
        group.MapPost("/series/{seriesId:int}/issues/refresh", async (
            IMetadataRefreshService refreshService,
            int seriesId,
            [FromQuery] bool force = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await refreshService.RefreshSeriesIssuesAsync(seriesId, force, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("RefreshSeriesIssues");

        // Refresh all series
        group.MapPost("/series/refresh-all", async (
            IMetadataRefreshService refreshService,
            [FromQuery] bool force = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await refreshService.RefreshAllSeriesAsync(force, null, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("RefreshAllSeriesMetadata");

        // Refresh stale series only
        group.MapPost("/series/refresh-stale", async (
            IMetadataRefreshService refreshService,
            [FromQuery] int? maxAgeDays = null,
            CancellationToken cancellationToken = default) =>
        {
            var settings = await refreshService.GetSettingsAsync(cancellationToken);
            var maxAge = maxAgeDays.HasValue 
                ? TimeSpan.FromDays(maxAgeDays.Value) 
                : settings.RefreshInterval;

            var result = await refreshService.RefreshStaleSeriesAsync(maxAge, null, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("RefreshStaleSeriesMetadata");

        // Refresh a single edition
        group.MapPost("/editions/{editionId:int}/refresh", async (
            IMetadataRefreshService refreshService,
            int editionId,
            [FromQuery] bool force = false,
            CancellationToken cancellationToken = default) =>
        {
            var result = await refreshService.RefreshEditionAsync(editionId, force, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(new { message = result.Error });
        })
        .WithName("MetadataRefreshEdition");

        // Get refresh history for a series
        group.MapGet("/series/{seriesId:int}/history", async (
            IMetadataRefreshService refreshService,
            int seriesId,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            var history = await refreshService.GetSeriesRefreshHistoryAsync(seriesId, limit, cancellationToken);
            return Results.Ok(history);
        })
        .WithName("GetSeriesRefreshHistory");

        // Get recent refresh events
        group.MapGet("/history", async (
            IMetadataRefreshService refreshService,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default) =>
        {
            var events = await refreshService.GetRecentRefreshEventsAsync(limit, cancellationToken);
            return Results.Ok(events);
        })
        .WithName("GetRecentRefreshEvents");
    }
}

