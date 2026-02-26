using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Api.Caching;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;

namespace Shortboxerr.Api.Endpoints;

public static class CoverEndpoints
{
    // Cover images are cached for 1 day (can be manually refreshed)
    private const int CoverCacheSeconds = 86400; // 1 day
    
    public static void MapCoverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/covers")
            .WithTags("Covers");

        // Get series cover (with long-lived cache)
        group.MapGet("/series/{seriesId:int}", GetSeriesCover)
            .WithName("GetSeriesCover")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);

        // Get issue cover (with long-lived cache)
        group.MapGet("/issues/{issueId:int}", GetIssueCover)
            .WithName("GetIssueCover")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);

        // Get discovery cover (covers for Pull List items not yet in library)
        // The coverId is the cache key used when downloading (Metron ID, DB issue ID, etc.)
        group.MapGet("/discovery/{coverId:int}/{size}", GetDiscoveryCoverWithSize)
            .WithName("GetDiscoveryCoverWithSize")
            .WithDescription("Gets a cached discovery cover by cache ID with size in route. The ID corresponds to the key used when caching (Metron ID for external enrichment, DB issue ID for known issues).")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);
        
        group.MapGet("/discovery/{coverId:int}", GetDiscoveryCover)
            .WithName("GetDiscoveryCover")
            .WithDescription("Gets a cached discovery cover by cache ID. The ID corresponds to the key used when caching (Metron ID for external enrichment, DB issue ID for known issues).")
            .WithOpenApi()
            .Produces(200, contentType: "image/jpeg")
            .Produces(404);

        // Clear series cover cache
        group.MapDelete("/series/{seriesId:int}", ClearSeriesCoverCache)
            .WithName("ClearSeriesCoverCache")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        // Clear issue cover cache
        group.MapDelete("/issues/{issueId:int}", ClearIssueCoverCache)
            .WithName("ClearIssueCoverCache")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        // Get cache statistics
        group.MapGet("/cache/stats", GetCacheStats)
            .WithName("GetCoverCacheStats")
            .WithOpenApi()
            .Produces<CoverCacheStats>(200);

        // Get detailed cache statistics with size breakdown
        group.MapGet("/cache/stats/detailed", GetDetailedCacheStats)
            .WithName("GetDetailedCoverCacheStats")
            .WithDescription("Gets detailed cache statistics including breakdown by size and limit info.")
            .WithOpenApi()
            .Produces<DetailedCoverCacheStats>(200);

        // Reset access statistics
        group.MapPost("/cache/stats/reset", ResetAccessStats)
            .WithName("ResetCoverAccessStats")
            .WithDescription("Resets the cover cache access statistics (hits/misses/etc).")
            .WithOpenApi()
            .Produces(204);

        // Trigger cache cleanup (LRU eviction + retention policy)
        group.MapPost("/cleanup", TriggerCleanup)
            .WithName("TriggerCoverCacheCleanup")
            .WithDescription("Triggers cache cleanup: removes expired covers and enforces size limit via LRU eviction.")
            .WithOpenApi()
            .Produces<CoverCleanupResult>(200);

        // Clear all cache
        group.MapDelete("/cache", ClearAllCache)
            .WithName("ClearAllCoverCache")
            .WithOpenApi()
            .Produces(204);

        // Download/refresh a cover
        group.MapPost("/series/{seriesId:int}/refresh", RefreshSeriesCover)
            .WithName("RefreshSeriesCover")
            .WithOpenApi()
            .Produces<CoverResult>(200)
            .Produces(400)
            .Produces(404);

        group.MapPost("/issues/{issueId:int}/refresh", RefreshIssueCover)
            .WithName("RefreshIssueCover")
            .WithOpenApi()
            .Produces<CoverResult>(200)
            .Produces(400)
            .Produces(404);

        // Cache warming endpoints
        group.MapPost("/warm/series/{seriesId:int}", WarmSeriesCache)
            .WithName("WarmSeriesCoverCache")
            .WithDescription("Pre-fetches covers for a specific series.")
            .WithOpenApi()
            .Produces<CacheWarmingResult>(200);

        group.MapPost("/warm", WarmCache)
            .WithName("WarmCoverCache")
            .WithDescription("Pre-fetches covers for multiple series.")
            .WithOpenApi()
            .Produces<CacheWarmingResult>(200);

        group.MapGet("/warm/status", GetWarmingStatus)
            .WithName("GetCoverWarmingStatus")
            .WithDescription("Gets the status of the current cache warming operation.")
            .WithOpenApi()
            .Produces<CacheWarmingStatus>(200);
    }

    private static async Task<IResult> GetSeriesCover(
        HttpContext httpContext,
        int seriesId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.GetSeriesCoverAsync(seriesId, size, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.FilePath))
        {
            return Results.NotFound(new { error = result.Error ?? "Cover not found" });
        }

        if (!File.Exists(result.FilePath))
        {
            return Results.NotFound(new { error = "Cover file not found" });
        }

        // Add Cache-Control header for cover images (1 day)
        httpContext.Response.Headers.CacheControl = $"public, max-age={CoverCacheSeconds}";
        
        // Add ETag based on file modification time
        var fileInfo = new FileInfo(result.FilePath);
        var etag = ETagHelper.GenerateETag(seriesId, fileInfo.LastWriteTimeUtc);
        
        if (ETagHelper.IsNotModified(httpContext.Request, etag))
        {
            httpContext.Response.Headers.ETag = etag;
            return Results.StatusCode(304);
        }
        
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.LastModified = fileInfo.LastWriteTimeUtc.ToString("R");

        return Results.File(result.FilePath, result.ContentType ?? "image/jpeg");
    }

    private static async Task<IResult> GetIssueCover(
        HttpContext httpContext,
        int issueId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.GetIssueCoverAsync(issueId, size, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.FilePath))
        {
            return Results.NotFound(new { error = result.Error ?? "Cover not found" });
        }

        if (!File.Exists(result.FilePath))
        {
            return Results.NotFound(new { error = "Cover file not found" });
        }

        // Add Cache-Control header for cover images (1 day)
        httpContext.Response.Headers.CacheControl = $"public, max-age={CoverCacheSeconds}";
        
        // Add ETag based on file modification time
        var fileInfo = new FileInfo(result.FilePath);
        var etag = ETagHelper.GenerateETag(issueId, fileInfo.LastWriteTimeUtc);
        
        if (ETagHelper.IsNotModified(httpContext.Request, etag))
        {
            httpContext.Response.Headers.ETag = etag;
            return Results.StatusCode(304);
        }
        
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.LastModified = fileInfo.LastWriteTimeUtc.ToString("R");

        return Results.File(result.FilePath, result.ContentType ?? "image/jpeg");
    }

    private static async Task<IResult> GetDiscoveryCoverWithSize(
        HttpContext httpContext,
        int coverId,
        string size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CoverSize>(size, ignoreCase: true, out var coverSize))
        {
            coverSize = CoverSize.Medium;
        }
        return await GetDiscoveryCoverInternal(httpContext, coverId, coverSize, coverService, cancellationToken);
    }

    private static async Task<IResult> GetDiscoveryCover(
        HttpContext httpContext,
        int coverId,
        [FromQuery] CoverSize? size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        return await GetDiscoveryCoverInternal(httpContext, coverId, size ?? CoverSize.Medium, coverService, cancellationToken);
    }

    private static async Task<IResult> GetDiscoveryCoverInternal(
        HttpContext httpContext,
        int coverId,
        CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.GetDiscoveryCoverAsync(coverId, size, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.FilePath))
        {
            return Results.NotFound(new { error = result.Error ?? "Cover not found" });
        }

        if (!File.Exists(result.FilePath))
        {
            return Results.NotFound(new { error = "Cover file not found" });
        }

        // Add Cache-Control header for cover images (1 day)
        httpContext.Response.Headers.CacheControl = $"public, max-age={CoverCacheSeconds}";
        
        // Add ETag based on file modification time
        var fileInfo = new FileInfo(result.FilePath);
        var etag = ETagHelper.GenerateETag(coverId, fileInfo.LastWriteTimeUtc);
        
        if (ETagHelper.IsNotModified(httpContext.Request, etag))
        {
            httpContext.Response.Headers.ETag = etag;
            return Results.StatusCode(304);
        }
        
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.LastModified = fileInfo.LastWriteTimeUtc.ToString("R");

        return Results.File(result.FilePath, result.ContentType ?? "image/jpeg");
    }

    private static async Task<IResult> ClearSeriesCoverCache(
        int seriesId,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearSeriesCoverCacheAsync(seriesId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearIssueCoverCache(
        int issueId,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearIssueCoverCacheAsync(issueId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCacheStats(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var stats = await coverService.GetCacheStatsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetDetailedCacheStats(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var stats = await coverService.GetDetailedCacheStatsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static IResult ResetAccessStats(ICoverService coverService)
    {
        coverService.ResetAccessStats();
        return Results.NoContent();
    }

    private static async Task<IResult> TriggerCleanup(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.CleanupCacheAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ClearAllCache(
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        await coverService.ClearAllCacheAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshSeriesCover(
        int seriesId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        // Clear existing cache first
        await coverService.ClearSeriesCoverCacheAsync(seriesId, cancellationToken);
        
        // Re-fetch
        var result = await coverService.GetSeriesCoverAsync(seriesId, size, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshIssueCover(
        int issueId,
        [FromQuery] CoverSize size,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        // Clear existing cache first
        await coverService.ClearIssueCoverCacheAsync(issueId, cancellationToken);
        
        // Re-fetch
        var result = await coverService.GetIssueCoverAsync(issueId, size, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> WarmSeriesCache(
        int seriesId,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.WarmSeriesCacheAsync(seriesId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> WarmCache(
        [FromBody] WarmCacheRequest request,
        ICoverService coverService,
        CancellationToken cancellationToken)
    {
        var result = await coverService.WarmCacheAsync(request.SeriesIds, cancellationToken);
        return Results.Ok(result);
    }

    private static IResult GetWarmingStatus(ICoverService coverService)
    {
        var status = coverService.GetWarmingStatus();
        return Results.Ok(status);
    }
}

public class WarmCacheRequest
{
    public List<int> SeriesIds { get; set; } = new();
}

