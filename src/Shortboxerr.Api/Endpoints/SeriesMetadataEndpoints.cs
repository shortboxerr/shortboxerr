using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

public static class SeriesMetadataEndpoints
{
    public static void MapSeriesMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/series")
            .WithTags("Series Metadata");

        // Search ComicVine for series
        group.MapGet("/comicvine/search", SearchComicVine)
            .WithName("SearchComicVineForSeries")
            .WithOpenApi()
            .Produces<SeriesSearchResult>(200)
            .Produces(400);

        // Get series info by ComicVine ID (preview before adding)
        group.MapGet("/comicvine/{volumeId:int}", GetComicVineVolume)
            .WithName("PreviewComicVineVolume")
            .WithOpenApi()
            .Produces<SeriesMatchCandidate>(200)
            .Produces(404);

        // Add series from ComicVine by volume ID
        group.MapPost("/comicvine/{volumeId:int}", AddSeriesFromComicVine)
            .WithName("AddSeriesFromComicVine")
            .WithOpenApi()
            .Produces<SeriesAddResult>(201)
            .Produces<SeriesAddResult>(409) // Conflict if already exists
            .Produces(400);

        // Match existing series to ComicVine
        group.MapPost("/{seriesId:int}/match/{volumeId:int}", MatchSeriesToComicVine)
            .WithName("MatchSeriesToComicVine")
            .WithOpenApi()
            .Produces<SeriesMatchResult>(200)
            .Produces(404);

        // Auto-match series to ComicVine
        group.MapPost("/{seriesId:int}/automatch", AutoMatchSeries)
            .WithName("AutoMatchSeries")
            .WithOpenApi()
            .Produces<SeriesAutoMatchResult>(200)
            .Produces(404);

        // Unmatch series from ComicVine
        group.MapPost("/{seriesId:int}/unmatch", UnmatchSeries)
            .WithName("UnmatchSeries")
            .WithOpenApi()
            .Produces(200)
            .Produces(404);

        // Refresh series metadata from ComicVine
        group.MapPost("/{seriesId:int}/refresh", RefreshSeriesMetadata)
            .WithName("RefreshSeriesMetadata")
            .WithOpenApi()
            .Produces<SeriesRefreshResult>(200)
            .Produces(404);

        // Sync issues from ComicVine
        group.MapPost("/{seriesId:int}/sync-issues", SyncIssuesFromComicVine)
            .WithName("SyncIssuesFromComicVine")
            .WithOpenApi()
            .Produces<IssueSyncResult>(200)
            .Produces(404);

        // Bulk match all unmatched series
        group.MapPost("/match-all", MatchAllUnmatchedSeries)
            .WithName("MatchAllUnmatchedSeries")
            .WithOpenApi()
            .Produces<BulkMatchResult>(200);

        // Search for annual series related to a parent series (Mylar3 parity)
        group.MapGet("/{seriesId:int}/annuals/search", SearchAnnualSeries)
            .WithName("SearchAnnualSeries")
            .WithOpenApi()
            .Produces<List<SeriesMatchCandidate>>(200)
            .Produces(404);

        // Add annual series and link to parent (Mylar3 parity)
        group.MapPost("/{seriesId:int}/annuals/{volumeId:int}", AddAnnualSeries)
            .WithName("AddAnnualSeries")
            .WithOpenApi()
            .Produces<SeriesAddResult>(201)
            .Produces(404);
        
        // Link all existing annual series in library to their parents
        group.MapPost("/link-annuals", LinkExistingAnnualSeries)
            .WithName("LinkExistingAnnualSeries")
            .WithOpenApi()
            .WithDescription("Scans all existing series and links annual series to their parent series. Use this to update series added before the annual linking feature.")
            .Produces<AnnualLinkingResult>(200);
        
        // Link a single series to its parent (if it's an annual)
        group.MapPost("/{seriesId:int}/link-annual", LinkSingleSeries)
            .WithName("LinkSingleSeriesAsAnnual")
            .WithOpenApi()
            .WithDescription("Attempts to link a single series to its parent if it's detected as an annual series.")
            .Produces(200)
            .Produces(404);
    }

    private static async Task<IResult> SearchComicVine(
        [FromQuery] string q,
        [FromQuery] string? publisher = null,
        [FromQuery] int? yearStart = null,
        [FromQuery] int? yearEnd = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        ISeriesMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { error = "Search query is required" });
        }

        // Check if query is a ComicVine ID (e.g., "4050-12345" or URL)
        var parsedId = ComicVineIdParser.TryParseAs(q, ComicVineResourceType.Volume);
        if (parsedId != null)
        {
            // Direct lookup by ID instead of search
            var volumeResult = await metadataService.GetSeriesByComicVineIdAsync(
                parsedId.NumericId, cancellationToken);

            if (volumeResult != null)
            {
                // Return as a single-result search
                return Results.Ok(new SeriesSearchResult
                {
                    Success = true,
                    Results = new List<SeriesMatchCandidate> { volumeResult },
                    TotalResults = 1,
                    Page = 1,
                    PageSize = 1,
                    Query = q,
                    IsDirectLookup = true
                });
            }
            else
            {
                return Results.Ok(new SeriesSearchResult
                {
                    Success = true,
                    Results = new List<SeriesMatchCandidate>(),
                    TotalResults = 0,
                    Page = 1,
                    PageSize = limit,
                    Query = q,
                    IsDirectLookup = true,
                    Error = $"ComicVine volume {parsedId.FullId} not found"
                });
            }
        }

        // Regular text search
        var result = await metadataService.SearchSeriesAsync(
            q, publisher, yearStart, yearEnd, page, limit, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetComicVineVolume(
        int volumeId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.GetSeriesByComicVineIdAsync(volumeId, cancellationToken);

        if (result == null)
        {
            return Results.NotFound(new { error = $"ComicVine volume {volumeId} not found" });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> AddSeriesFromComicVine(
        int volumeId,
        [FromBody] AddSeriesFromComicVineRequest? request,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.AddSeriesByComicVineIdAsync(
            volumeId,
            request?.RootFolder,
            request?.Monitored ?? true,
            request?.MonitoringMode ?? SeriesMonitoringMode.AllIssues,
            cancellationToken);

        if (!result.Success)
        {
            if (result.AlreadyExists)
            {
                return Results.Conflict(result);
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Created($"/api/v1/series/{result.SeriesId}", result);
    }

    private static async Task<IResult> MatchSeriesToComicVine(
        int seriesId,
        int volumeId,
        [FromQuery] bool syncMetadata = true,
        [FromQuery] bool createMissingIssues = true,
        ISeriesMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.MatchSeriesAsync(
            seriesId, volumeId, syncMetadata, createMissingIssues, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> AutoMatchSeries(
        int seriesId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.AutoMatchSeriesAsync(seriesId, cancellationToken);

        if (!result.Success && result.Error?.Contains("not found") == true)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> UnmatchSeries(
        int seriesId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var success = await metadataService.UnmatchSeriesAsync(seriesId, cancellationToken);

        if (!success)
        {
            return Results.NotFound(new { error = $"Series with ID {seriesId} not found" });
        }

        return Results.Ok(new { message = "Series unmatched from ComicVine" });
    }

    private static async Task<IResult> RefreshSeriesMetadata(
        int seriesId,
        [FromQuery] bool force = false,
        ISeriesMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.RefreshSeriesMetadataAsync(seriesId, force, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> SyncIssuesFromComicVine(
        int seriesId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.SyncIssuesFromComicVineAsync(seriesId, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> MatchAllUnmatchedSeries(
        [FromQuery] int? confidenceThreshold = null,
        ISeriesMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.AutoMatchAllSeriesAsync(confidenceThreshold, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchAnnualSeries(
        int seriesId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.SearchForAnnualSeriesAsync(seriesId, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddAnnualSeries(
        int seriesId,
        int volumeId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.AddAnnualSeriesAsync(seriesId, volumeId, cancellationToken);

        if (!result.Success)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return Results.NotFound(new { error = result.Error });
            }
            if (result.AlreadyExists)
            {
                return Results.Conflict(result);
            }
            return Results.BadRequest(new { error = result.Error });
        }

        return Results.Created($"/api/v1/series/{result.SeriesId}", result);
    }
    
    private static async Task<IResult> LinkExistingAnnualSeries(
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.LinkExistingAnnualSeriesAsync(cancellationToken);
        return Results.Ok(result);
    }
    
    private static async Task<IResult> LinkSingleSeries(
        int seriesId,
        ISeriesMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var linked = await metadataService.TryLinkSingleSeriesAsync(seriesId, cancellationToken);
        
        if (linked)
        {
            return Results.Ok(new { message = "Series linked to parent", seriesId });
        }
        else
        {
            return Results.Ok(new { message = "Series not identified as an annual or no parent found", seriesId });
        }
    }
}

#region Request DTOs

public class AddSeriesFromComicVineRequest
{
    /// <summary>
    /// Root folder for the series.
    /// </summary>
    public string? RootFolder { get; set; }

    /// <summary>
    /// Whether to monitor the series for new issues.
    /// </summary>
    public bool Monitored { get; set; } = true;

    /// <summary>
    /// How to monitor the series.
    /// </summary>
    public SeriesMonitoringMode MonitoringMode { get; set; } = SeriesMonitoringMode.AllIssues;
}

#endregion

