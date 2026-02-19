using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for variant cover detection and management.
/// </summary>
public static class VariantCoverEndpoints
{
    public static void MapVariantCoverEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/variants")
            .WithTags("Variant Covers")
            .WithOpenApi();

        // GET variant covers for an issue
        group.MapGet("/issues/{issueId:int}", async (int issueId, IVariantCoverService variantService) =>
        {
            var covers = await variantService.GetVariantCoversAsync(issueId);
            return Results.Ok(covers.Select(ToDto));
        })
        .WithName("GetIssueVariantCovers")
        .WithDescription("Gets all variant covers for a specific issue.");

        // POST fetch variant covers from ComicVine for an issue
        group.MapPost("/issues/{issueId:int}/fetch", async (int issueId, IVariantCoverService variantService, CancellationToken ct) =>
        {
            var result = await variantService.FetchVariantCoversAsync(issueId, ct);
            return result.Success 
                ? Results.Ok(ToResultDto(result))
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("FetchIssueVariantCovers")
        .WithDescription("Fetches variant covers from ComicVine for a specific issue.");

        // POST fetch variant covers for all issues in a series
        group.MapPost("/series/{seriesId:int}/fetch", async (int seriesId, IVariantCoverService variantService, CancellationToken ct) =>
        {
            var result = await variantService.FetchSeriesVariantCoversAsync(seriesId, ct);
            return result.Success 
                ? Results.Ok(ToSeriesResultDto(result))
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("FetchSeriesVariantCovers")
        .WithDescription("Fetches variant covers from ComicVine for all issues in a series.");

        // GET issues with variants in a series
        group.MapGet("/series/{seriesId:int}/issues", async (int seriesId, IVariantCoverService variantService, CancellationToken ct) =>
        {
            var issues = await variantService.GetIssuesWithVariantsAsync(seriesId, ct);
            return Results.Ok(issues.Select(ToIssueWithVariantsDto));
        })
        .WithName("GetSeriesIssuesWithVariants")
        .WithDescription("Gets all issues with variant covers in a series.");

        // GET variant cover statistics for a series
        group.MapGet("/series/{seriesId:int}/stats", async (int seriesId, IVariantCoverService variantService, CancellationToken ct) =>
        {
            var stats = await variantService.GetSeriesStatsAsync(seriesId, ct);
            return Results.Ok(ToStatsDto(stats));
        })
        .WithName("GetSeriesVariantStats")
        .WithDescription("Gets variant cover statistics for a series.");

        // PUT set preferred cover for an issue
        group.MapPut("/issues/{issueId:int}/preferred", async (int issueId, SetPreferredCoverRequest request, IVariantCoverService variantService, CancellationToken ct) =>
        {
            await variantService.SetPreferredCoverAsync(issueId, request.VariantCoverId, ct);
            return Results.Ok(new { message = "Preferred cover updated." });
        })
        .WithName("SetPreferredCover")
        .WithDescription("Sets the preferred cover for an issue (null for main cover).");

        // POST detect variant from text (utility endpoint)
        group.MapPost("/detect", (DetectVariantRequest request, IVariantCoverService variantService) =>
        {
            var result = variantService.DetectVariant(request.Caption, request.ImageTags, request.Filename);
            return Results.Ok(ToDetectionResultDto(result));
        })
        .WithName("DetectVariant")
        .WithDescription("Detects if the provided text indicates a variant cover.");
    }

    private static VariantCoverDto ToDto(VariantCover cover) => new()
    {
        Id = cover.Id,
        IssueId = cover.IssueId,
        ComicVineImageId = cover.ComicVineImageId,
        ImageUrl = cover.ImageUrl,
        Caption = cover.Caption,
        VariantType = cover.VariantType,
        IsPrimaryCover = cover.IsPrimaryCover,
        IsPreferred = cover.IsPreferred,
        DetectedAt = cover.DetectedAt
    };

    private static VariantCoverResultDto ToResultDto(VariantCoverResult result) => new()
    {
        Success = result.Success,
        Error = result.Error,
        IssueId = result.IssueId,
        TotalImagesFound = result.TotalImagesFound,
        VariantsDetected = result.VariantsDetected,
        Variants = result.Variants.Select(ToDto).ToList()
    };

    private static SeriesVariantCoverResultDto ToSeriesResultDto(SeriesVariantCoverResult result) => new()
    {
        Success = result.Success,
        Error = result.Error,
        SeriesId = result.SeriesId,
        IssuesProcessed = result.IssuesProcessed,
        IssuesWithVariants = result.IssuesWithVariants,
        TotalVariantsDetected = result.TotalVariantsDetected
    };

    private static IssueWithVariantsDto ToIssueWithVariantsDto(IssueWithVariants issue) => new()
    {
        IssueId = issue.IssueId,
        IssueNumber = issue.IssueNumber,
        Title = issue.Title,
        MainCoverUrl = issue.MainCoverUrl,
        VariantCount = issue.VariantCount,
        Variants = issue.Variants.Select(ToDto).ToList(),
        PreferredVariant = issue.PreferredVariant != null ? ToDto(issue.PreferredVariant) : null
    };

    private static VariantCoverStatsDto ToStatsDto(VariantCoverStats stats) => new()
    {
        SeriesId = stats.SeriesId,
        TotalIssues = stats.TotalIssues,
        IssuesWithVariants = stats.IssuesWithVariants,
        TotalVariants = stats.TotalVariants,
        AverageVariantsPerIssue = stats.AverageVariantsPerIssue,
        VariantsByType = stats.VariantsByType.ToDictionary(x => x.Key, x => x.Value),
        LastFetchedAt = stats.LastFetchedAt
    };

    private static VariantDetectionResultDto ToDetectionResultDto(VariantDetectionResult result) => new()
    {
        IsVariant = result.IsVariant,
        VariantType = result.VariantType,
        Confidence = result.Confidence,
        MatchedPatterns = result.MatchedPatterns.ToList()
    };
}

// DTOs
public class VariantCoverDto
{
    public int Id { get; init; }
    public int IssueId { get; init; }
    public int ComicVineImageId { get; init; }
    public required string ImageUrl { get; init; }
    public string? Caption { get; init; }
    public string? VariantType { get; init; }
    public bool IsPrimaryCover { get; init; }
    public bool IsPreferred { get; init; }
    public DateTime DetectedAt { get; init; }
}

public class VariantCoverResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int IssueId { get; init; }
    public int TotalImagesFound { get; init; }
    public int VariantsDetected { get; init; }
    public List<VariantCoverDto> Variants { get; init; } = new();
}

public class SeriesVariantCoverResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int SeriesId { get; init; }
    public int IssuesProcessed { get; init; }
    public int IssuesWithVariants { get; init; }
    public int TotalVariantsDetected { get; init; }
}

public class IssueWithVariantsDto
{
    public int IssueId { get; init; }
    public decimal IssueNumber { get; init; }
    public string? Title { get; init; }
    public string? MainCoverUrl { get; init; }
    public int VariantCount { get; init; }
    public List<VariantCoverDto> Variants { get; init; } = new();
    public VariantCoverDto? PreferredVariant { get; init; }
}

public class VariantCoverStatsDto
{
    public int SeriesId { get; init; }
    public int TotalIssues { get; init; }
    public int IssuesWithVariants { get; init; }
    public int TotalVariants { get; init; }
    public double AverageVariantsPerIssue { get; init; }
    public Dictionary<string, int> VariantsByType { get; init; } = new();
    public DateTime? LastFetchedAt { get; init; }
}

public class VariantDetectionResultDto
{
    public bool IsVariant { get; init; }
    public string? VariantType { get; init; }
    public int Confidence { get; init; }
    public List<string> MatchedPatterns { get; init; } = new();
}

public class SetPreferredCoverRequest
{
    public int? VariantCoverId { get; init; }
}

public class DetectVariantRequest
{
    public string? Caption { get; init; }
    public string? ImageTags { get; init; }
    public string? Filename { get; init; }
}
