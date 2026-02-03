using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Caching;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for cache management and statistics.
/// </summary>
public static class CacheEndpoints
{
    public static void MapCacheEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/cache")
            .WithTags("Cache");

        // GET /api/v1/cache/stats - get cache statistics
        group.MapGet("/stats", (
            [FromServices] ICacheService cacheService) =>
        {
            var stats = cacheService.GetStatistics();
            return Results.Ok(stats);
        })
        .WithName("GetCacheStatistics")
        .WithDescription("Gets cache hit/miss statistics and item counts")
        .Produces<CacheStatistics>(200);

        // POST /api/v1/cache/stats/reset - reset cache statistics
        group.MapPost("/stats/reset", (
            [FromServices] ICacheService cacheService) =>
        {
            cacheService.ResetStatistics();
            return Results.Ok(new { Success = true, Message = "Cache statistics reset" });
        })
        .WithName("ResetCacheStatistics")
        .WithDescription("Resets cache statistics counters")
        .Produces<object>(200);

        // DELETE /api/v1/cache - clear all cache
        group.MapDelete("/", (
            [FromServices] ICacheService cacheService) =>
        {
            cacheService.Clear();
            return Results.Ok(new { Success = true, Message = "Cache cleared" });
        })
        .WithName("ClearCache")
        .WithDescription("Clears all cached data")
        .Produces<object>(200);

        // DELETE /api/v1/cache/{prefix} - clear cache by prefix
        group.MapDelete("/{prefix}", (
            string prefix,
            [FromServices] ICacheService cacheService) =>
        {
            var count = cacheService.RemoveByPrefix(prefix);
            return Results.Ok(new { Success = true, RemovedCount = count, Message = $"Removed {count} entries with prefix '{prefix}'" });
        })
        .WithName("ClearCacheByPrefix")
        .WithDescription("Clears cached data matching the specified key prefix")
        .Produces<object>(200);

        // GET /api/v1/cache/keys - list known cache key prefixes
        group.MapGet("/keys", () =>
        {
            // Return well-known cache key prefixes for documentation
            return Results.Ok(new
            {
                Prefixes = new Dictionary<string, string>
                {
                    [CacheKeys.PullList] = "Pull list queries",
                    [CacheKeys.PullListWeek] = "Weekly pull list data",
                    [CacheKeys.PullListUpcoming] = "Upcoming releases",
                    [CacheKeys.PullListPast] = "Past releases",
                    [CacheKeys.PullListDiscovery] = "ComicVine discovery data",
                    [CacheKeys.Series] = "All series data",
                    [CacheKeys.SeriesList] = "Series list queries",
                    [CacheKeys.SeriesDetail] = "Series detail pages",
                    [CacheKeys.Issue] = "All issue data",
                    [CacheKeys.IssueList] = "Issue list queries",
                    [CacheKeys.Dashboard] = "All dashboard data",
                    [CacheKeys.DashboardStats] = "Dashboard statistics",
                    [CacheKeys.DashboardThisWeek] = "Dashboard this week widget",
                    [CacheKeys.ComicVine] = "All ComicVine data",
                    [CacheKeys.ComicVineSearch] = "ComicVine search results",
                    [CacheKeys.ComicVineVolume] = "ComicVine volume data",
                    [CacheKeys.ComicVineIssue] = "ComicVine issue data"
                }
            });
        })
        .WithName("GetCacheKeyPrefixes")
        .WithDescription("Lists known cache key prefixes for targeted invalidation")
        .Produces<object>(200);
    }
}
