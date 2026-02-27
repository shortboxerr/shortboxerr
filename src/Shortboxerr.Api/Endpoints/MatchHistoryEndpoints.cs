using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for match history auditing and statistics (EPIC 19.5).
/// </summary>
public static class MatchHistoryEndpoints
{
    public static void MapMatchHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/match-history")
            .WithTags("Match History")
            .WithOpenApi();

        // GET /api/v1/match-history - Get match history with filtering
        group.MapGet("/", GetMatchHistory)
            .WithName("GetMatchHistory")
            .WithSummary("Gets match history records with filtering and pagination")
            .Produces<MatchHistoryResponse>();

        // GET /api/v1/match-history/{id} - Get specific match record
        group.MapGet("/{id:int}", async (
            int id,
            IMatchHistoryService service,
            CancellationToken ct) =>
        {
            var result = await service.GetHistoryAsync(new MatchHistoryQuery { PageSize = 1 }, ct);
            var record = result.Records.FirstOrDefault(r => r.Id == id);
            return record != null 
                ? Results.Ok(MapToDto(record))
                : Results.NotFound();
        })
        .WithName("GetMatchHistoryRecord")
        .WithSummary("Gets a specific match history record")
        .Produces<MatchHistoryDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/match-history/{id}/verify - Verify a match as correct or incorrect
        group.MapPost("/{id:int}/verify", async (
            int id,
            VerifyMatchRequest request,
            IMatchHistoryService service,
            CancellationToken ct) =>
        {
            var result = await service.VerifyMatchAsync(
                id,
                request.IsCorrect,
                request.CorrectedSeriesId,
                request.CorrectedIssueId,
                ct);

            return result != null 
                ? Results.Ok(MapToDto(result))
                : Results.NotFound();
        })
        .WithName("VerifyMatch")
        .WithSummary("Verifies a match as correct or incorrect")
        .Produces<MatchHistoryDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/match-history/stats - Get accuracy statistics
        group.MapGet("/stats", async (
            int? seriesId,
            int? days,
            IMatchHistoryService service,
            CancellationToken ct) =>
        {
            var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;
            var stats = await service.GetAccuracyStatsAsync(seriesId, since, ct);
            return Results.Ok(stats);
        })
        .WithName("GetMatchStats")
        .WithSummary("Gets match accuracy statistics")
        .Produces<MatchAccuracyStats>();

        // GET /api/v1/match-history/problematic-series - Get series with frequent mismatches
        group.MapGet("/problematic-series", async (
            int minMismatches,
            int? days,
            IMatchHistoryService service,
            CancellationToken ct) =>
        {
            var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;
            var series = await service.GetProblematicSeriesAsync(minMismatches, since, ct);
            return Results.Ok(series);
        })
        .WithName("GetProblematicSeries")
        .WithSummary("Gets series with frequent match mismatches")
        .Produces<IReadOnlyList<SeriesMismatchSummary>>();
    }

    private static async Task<IResult> GetMatchHistory(
        IMatchHistoryService service,
        int? seriesId,
        string? outcome,
        bool? requiredReview,
        bool? verified,
        string? search,
        int? days,
        int page = 1,
        int pageSize = 50,
        string sortBy = "timestamp",
        bool descending = true,
        CancellationToken ct = default)
    {
        var query = new MatchHistoryQuery
        {
            SeriesId = seriesId,
            RequiredReview = requiredReview,
            UserVerified = verified,
            SearchTerm = search,
            Page = page,
            PageSize = Math.Min(pageSize, 100),
            SortDescending = descending
        };

        // Parse outcome filter
        if (!string.IsNullOrEmpty(outcome))
        {
            query.Outcome = Enum.TryParse<MatchOutcome>(outcome, true, out var o) ? o : null;
        }

        // Parse date range
        if (days.HasValue)
        {
            query.Since = DateTime.UtcNow.AddDays(-days.Value);
        }

        // Parse sort
        query.SortBy = sortBy.ToLowerInvariant() switch
        {
            "confidence" => MatchHistorySortBy.ConfidenceScore,
            "series" => MatchHistorySortBy.SeriesTitle,
            "outcome" => MatchHistorySortBy.Outcome,
            _ => MatchHistorySortBy.Timestamp
        };

        var result = await service.GetHistoryAsync(query, ct);

        return Results.Ok(new MatchHistoryResponse
        {
            Records = result.Records.Select(MapToDto).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages
        });
    }

    private static MatchHistoryDto MapToDto(MatchHistory record) => new()
    {
        Id = record.Id,
        MatchId = record.MatchId,
        ReleaseTitle = record.ReleaseTitle,
        SourceSite = record.SourceSite,
        ParsedSeriesTitle = record.ParsedSeriesTitle,
        ParsedIssueNumber = record.ParsedIssueNumber,
        ParsedYear = record.ParsedYear,
        ParsedPublisher = record.ParsedPublisher,
        Outcome = record.Outcome.ToString(),
        MatchFound = record.MatchFound,
        ConfidenceScore = record.ConfidenceScore,
        MatchedSeriesId = record.MatchedSeriesId,
        MatchedSeriesTitle = record.MatchedSeriesTitle,
        MatchedIssueId = record.MatchedIssueId,
        MatchedIssueNumber = record.MatchedIssueNumber,
        WasFirstIssue = record.WasFirstIssue,
        RequiredManualReview = record.RequiredManualReview,
        ReviewReason = record.ReviewReason,
        Explanation = record.Explanation,
        ScoreBreakdown = record.ScoreBreakdownJson,
        ConfidenceReductions = record.ConfidenceReductionsJson,
        UserVerified = record.UserVerified,
        CorrectedSeriesId = record.CorrectedSeriesId,
        CorrectedIssueId = record.CorrectedIssueId,
        Timestamp = record.Timestamp,
        VerifiedAt = record.VerifiedAt
    };
}

public class MatchHistoryDto
{
    public int Id { get; init; }
    public required string MatchId { get; init; }
    public required string ReleaseTitle { get; init; }
    public string? SourceSite { get; init; }
    public string? ParsedSeriesTitle { get; init; }
    public string? ParsedIssueNumber { get; init; }
    public int? ParsedYear { get; init; }
    public string? ParsedPublisher { get; init; }
    public required string Outcome { get; init; }
    public bool MatchFound { get; init; }
    public int ConfidenceScore { get; init; }
    public int? MatchedSeriesId { get; init; }
    public string? MatchedSeriesTitle { get; init; }
    public int? MatchedIssueId { get; init; }
    public string? MatchedIssueNumber { get; init; }
    public bool WasFirstIssue { get; init; }
    public bool RequiredManualReview { get; init; }
    public string? ReviewReason { get; init; }
    public string? Explanation { get; init; }
    public string? ScoreBreakdown { get; init; }
    public string? ConfidenceReductions { get; init; }
    public bool? UserVerified { get; init; }
    public int? CorrectedSeriesId { get; init; }
    public int? CorrectedIssueId { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime? VerifiedAt { get; init; }
}

public class MatchHistoryResponse
{
    public required IList<MatchHistoryDto> Records { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public class VerifyMatchRequest
{
    public bool IsCorrect { get; set; }
    public int? CorrectedSeriesId { get; set; }
    public int? CorrectedIssueId { get; set; }
}
