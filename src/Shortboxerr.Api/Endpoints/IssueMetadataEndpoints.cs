using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class IssueMetadataEndpoints
{
    public static void MapIssueMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/issues")
            .WithTags("Issue Metadata");

        // Get issue by ID
        group.MapGet("/{issueId:int}", GetIssue)
            .WithName("GetIssue")
            .WithDescription("Gets an issue by its database ID.")
            .WithOpenApi()
            .Produces<IssueDetailResponse>(200)
            .Produces(404);

        // Edit issue
        group.MapPut("/{issueId:int}", UpdateIssue)
            .WithName("UpdateIssue")
            .WithDescription("Updates issue metadata.")
            .WithOpenApi()
            .Produces<IssueDetailResponse>(200)
            .Produces(400)
            .Produces(404);

        // Get issue details from ComicVine
        group.MapGet("/comicvine/{issueId:int}", GetComicVineIssue)
            .WithName("GetComicVineIssuePreview")
            .WithOpenApi()
            .Produces<IssueDetailResult>(200)
            .Produces(404);

        // Refresh issue metadata
        group.MapPost("/{issueId:int}/refresh", RefreshIssueMetadata)
            .WithName("RefreshIssueMetadata")
            .WithOpenApi()
            .Produces<IssueRefreshResult>(200)
            .Produces(400)
            .Produces(404);

        // Sync issue story arcs
        group.MapPost("/{issueId:int}/story-arcs/sync", SyncIssueStoryArcs)
            .WithName("SyncIssueStoryArcs")
            .WithOpenApi()
            .Produces<IssueStoryArcSyncResult>(200)
            .Produces(400)
            .Produces(404);

        // Series-level endpoints
        var seriesGroup = app.MapGroup("/api/v1/series")
            .WithTags("Issue Metadata");

        // Refresh all issues in a series
        seriesGroup.MapPost("/{seriesId:int}/issues/refresh", RefreshSeriesIssuesMetadata)
            .WithName("RefreshSeriesIssuesMetadata")
            .WithOpenApi()
            .Produces<IssuesBulkRefreshResult>(200)
            .Produces(400)
            .Produces(404);

        // Detect special issues in a series
        seriesGroup.MapPost("/{seriesId:int}/issues/detect-specials", DetectSpecialIssues)
            .WithName("DetectSpecialIssues")
            .WithOpenApi()
            .Produces<SpecialIssueDetectionResult>(200)
            .Produces(404);
    }

    private static async Task<IResult> GetIssue(
        int issueId,
        ShortboxerrDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Issues
            .Include(i => i.Series)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return Results.NotFound(new { error = $"Issue {issueId} not found" });
        }

        return Results.Ok(MapToResponse(issue));
    }

    private static async Task<IResult> UpdateIssue(
        int issueId,
        UpdateIssueRequest request,
        ShortboxerrDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var issue = await dbContext.Issues
            .Include(i => i.Series)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return Results.NotFound(new { error = $"Issue {issueId} not found" });
        }

        // Update only provided fields
        if (request.IssueNumber.HasValue)
            issue.IssueNumber = request.IssueNumber.Value;
        if (request.IssueNumberText != null)
            issue.IssueNumberText = request.IssueNumberText;
        if (request.Title != null)
            issue.Title = request.Title;
        if (request.ReleaseDate.HasValue)
            issue.ReleaseDate = request.ReleaseDate.Value;
        if (request.StoreDate.HasValue)
            issue.StoreDate = request.StoreDate.Value;
        if (request.Overview != null)
            issue.Overview = request.Overview;
        if (request.Monitored.HasValue)
            issue.Monitored = request.Monitored.Value;
        if (request.Status.HasValue)
            issue.Status = request.Status.Value;
        if (request.IsAnnual.HasValue)
            issue.IsAnnual = request.IsAnnual.Value;
        if (request.IsSpecial.HasValue)
            issue.IsSpecial = request.IsSpecial.Value;
        if (request.SpecialType != null)
            issue.SpecialType = request.SpecialType;
        if (request.CoverImageUrl != null)
            issue.CoverImageUrl = request.CoverImageUrl;

        issue.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(MapToResponse(issue));
    }

    private static IssueDetailResponse MapToResponse(Issue issue)
    {
        return new IssueDetailResponse
        {
            Id = issue.Id,
            SeriesId = issue.SeriesId,
            SeriesTitle = issue.Series?.Title,
            IssueNumber = issue.IssueNumber,
            IssueNumberText = issue.IssueNumberText,
            Title = issue.Title,
            ReleaseDate = issue.ReleaseDate,
            StoreDate = issue.StoreDate,
            CoverDate = issue.CoverDate,
            Overview = issue.Overview,
            Monitored = issue.Monitored,
            Status = issue.Status,
            HasFile = issue.HasFile,
            IsAnnual = issue.IsAnnual,
            IsSpecial = issue.IsSpecial,
            SpecialType = issue.SpecialType,
            ComicVineId = issue.ComicVineId,
            ComicVineUrl = issue.ComicVineUrl,
            CoverImageUrl = issue.CoverImageUrl,
            SatisfiedByEdition = issue.SatisfiedByEdition,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            MetadataLastRefreshed = issue.MetadataLastRefreshed
        };
    }

    private static async Task<IResult> GetComicVineIssue(
        int issueId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.GetIssueByComicVineIdAsync(issueId, cancellationToken);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshIssueMetadata(
        int issueId,
        [FromQuery] bool force = false,
        IIssueMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.RefreshIssueMetadataAsync(issueId, force, cancellationToken);

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

    private static async Task<IResult> SyncIssueStoryArcs(
        int issueId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.SyncIssueStoryArcsAsync(issueId, cancellationToken);

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

    private static async Task<IResult> RefreshSeriesIssuesMetadata(
        int seriesId,
        [FromQuery] bool force = false,
        IIssueMetadataService metadataService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await metadataService.RefreshSeriesIssuesMetadataAsync(seriesId, force, cancellationToken);

        if (!result.Success && result.Error?.Contains("not found") == true)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> DetectSpecialIssues(
        int seriesId,
        IIssueMetadataService metadataService,
        CancellationToken cancellationToken)
    {
        var result = await metadataService.DetectSpecialIssuesAsync(seriesId, cancellationToken);

        if (!result.Success)
        {
            return Results.NotFound(new { error = result.Error });
        }

        return Results.Ok(result);
    }
}

/// <summary>
/// Request to update issue metadata.
/// </summary>
public class UpdateIssueRequest
{
    public decimal? IssueNumber { get; set; }
    public string? IssueNumberText { get; set; }
    public string? Title { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? StoreDate { get; set; }
    public string? Overview { get; set; }
    public bool? Monitored { get; set; }
    public IssueStatus? Status { get; set; }
    public bool? IsAnnual { get; set; }
    public bool? IsSpecial { get; set; }
    public string? SpecialType { get; set; }
    public string? CoverImageUrl { get; set; }
}

/// <summary>
/// Response containing issue details.
/// </summary>
public class IssueDetailResponse
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public decimal IssueNumber { get; set; }
    public string? IssueNumberText { get; set; }
    public string? Title { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? StoreDate { get; set; }
    public DateTime? CoverDate { get; set; }
    public string? Overview { get; set; }
    public bool Monitored { get; set; }
    public IssueStatus Status { get; set; }
    public bool HasFile { get; set; }
    public bool IsAnnual { get; set; }
    public bool IsSpecial { get; set; }
    public string? SpecialType { get; set; }
    public int? ComicVineId { get; set; }
    public string? ComicVineUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool SatisfiedByEdition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? MetadataLastRefreshed { get; set; }
}

