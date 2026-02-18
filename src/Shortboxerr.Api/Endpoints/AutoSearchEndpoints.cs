using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Search;
using Shortboxerr.Infrastructure.BackgroundServices;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for auto-search functionality.
/// </summary>
public static class AutoSearchEndpoints
{
    public static void MapAutoSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/search/auto")
            .WithTags("Auto Search")
            .WithOpenApi();

        group.MapGet("/status", GetStatus)
            .WithName("GetAutoSearchStatus")
            .WithSummary("Get auto-search status")
            .WithDescription("Returns the current status of the auto-search service including enabled state, running state, and statistics.")
            .Produces<AutoSearchStatusDto>();

        group.MapGet("/searchable", GetSearchableIssues)
            .WithName("GetSearchableIssues")
            .WithSummary("Get issues available for searching")
            .WithDescription("Returns a list of wanted issues that are due for searching based on current settings.")
            .Produces<List<SearchableIssueDto>>();

        group.MapGet("/history", GetHistory)
            .WithName("GetAutoSearchHistory")
            .WithSummary("Get auto-search history")
            .WithDescription("Returns recent auto-search history entries.")
            .Produces<List<AutoSearchHistoryDto>>();

        group.MapPost("/trigger", TriggerSearch)
            .WithName("TriggerAutoSearch")
            .WithSummary("Trigger auto-search")
            .WithDescription("Manually triggers an auto-search run for all wanted issues.")
            .Produces<AutoSearchBatchResultDto>();

        group.MapPost("/issue/{issueId:int}", SearchIssue)
            .WithName("SearchIssue")
            .WithSummary("Search for a specific issue")
            .WithDescription("Searches for a specific issue across all enabled providers.")
            .Produces<AutoSearchResultDto>();

        group.MapPost("/series/{seriesId:int}", SearchSeriesWanted)
            .WithName("SearchSeriesWanted")
            .WithSummary("Search for all wanted issues in a series")
            .WithDescription("Searches for all wanted issues in a specific series.")
            .Produces<AutoSearchBatchResultDto>();
    }

    private static async Task<IResult> GetStatus(
        [FromServices] IAutoSearchService autoSearchService,
        CancellationToken cancellationToken)
    {
        var status = await autoSearchService.GetStatusAsync(cancellationToken);
        return Results.Ok(new AutoSearchStatusDto
        {
            Enabled = status.Enabled,
            IsRunning = status.IsRunning,
            WantedIssuesCount = status.WantedIssuesCount,
            SearchableCount = status.SearchableCount,
            LastRunAt = status.LastRunAt,
            NextRunAt = status.NextRunAt,
            TodaySearchCount = status.TodaySearchCount,
            TodayFoundCount = status.TodayFoundCount
        });
    }

    private static async Task<IResult> GetSearchableIssues(
        [FromServices] IAutoSearchService autoSearchService,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var issues = await autoSearchService.GetSearchableIssuesAsync(limit ?? 100, cancellationToken);
        var dtos = issues.Select(i => new SearchableIssueDto
        {
            IssueId = i.IssueId,
            SeriesId = i.SeriesId,
            SeriesTitle = i.SeriesTitle,
            IssueNumber = i.IssueNumber,
            IssueTitle = i.IssueTitle,
            ReleaseDate = i.ReleaseDate,
            LastSearchedAt = i.LastSearchedAt,
            SearchAttempts = i.SearchAttempts
        }).ToList();
        
        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetHistory(
        [FromServices] IAutoSearchService autoSearchService,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var history = await autoSearchService.GetHistoryAsync(limit ?? 50, cancellationToken);
        var dtos = history.Select(h => new AutoSearchHistoryDto
        {
            IssueId = h.IssueId,
            SeriesTitle = h.SeriesTitle,
            IssueNumber = h.IssueNumber,
            SearchedAt = h.SearchedAt,
            Found = h.Found,
            CandidatesFound = h.CandidatesFound,
            SelectedCandidate = h.SelectedCandidate,
            Error = h.Error
        }).ToList();
        
        return Results.Ok(dtos);
    }

    private static async Task<IResult> TriggerSearch(
        [FromServices] IAutoSearchService autoSearchService,
        [FromQuery] int? maxIssues,
        CancellationToken cancellationToken)
    {
        var result = await autoSearchService.SearchAllWantedAsync(maxIssues ?? 50, cancellationToken);
        return Results.Ok(MapToBatchResultDto(result));
    }

    private static async Task<IResult> SearchIssue(
        int issueId,
        [FromServices] IAutoSearchService autoSearchService,
        CancellationToken cancellationToken)
    {
        var result = await autoSearchService.SearchIssueAsync(issueId, cancellationToken);
        return Results.Ok(MapToResultDto(result));
    }

    private static async Task<IResult> SearchSeriesWanted(
        int seriesId,
        [FromServices] IAutoSearchService autoSearchService,
        CancellationToken cancellationToken)
    {
        var result = await autoSearchService.SearchSeriesWantedAsync(seriesId, cancellationToken);
        return Results.Ok(MapToBatchResultDto(result));
    }

    private static AutoSearchResultDto MapToResultDto(AutoSearchResult result) => new()
    {
        IssueId = result.IssueId,
        SeriesTitle = result.SeriesTitle,
        IssueNumber = result.IssueNumber,
        Success = result.Success,
        CandidatesFound = result.CandidatesFound,
        SelectedCandidateTitle = result.SelectedCandidateTitle,
        DownloadId = result.DownloadId,
        Error = result.Error,
        DurationMs = (int)result.Duration.TotalMilliseconds
    };

    private static AutoSearchBatchResultDto MapToBatchResultDto(AutoSearchBatchResult result) => new()
    {
        TotalSearched = result.TotalSearched,
        SuccessCount = result.SuccessCount,
        FailedCount = result.FailedCount,
        NotFoundCount = result.NotFoundCount,
        Results = result.Results.Select(MapToResultDto).ToList(),
        TotalDurationMs = (int)result.TotalDuration.TotalMilliseconds,
        Error = result.Error
    };
}

// DTOs for API responses
public record AutoSearchStatusDto
{
    public bool Enabled { get; init; }
    public bool IsRunning { get; init; }
    public int WantedIssuesCount { get; init; }
    public int SearchableCount { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime? NextRunAt { get; init; }
    public int TodaySearchCount { get; init; }
    public int TodayFoundCount { get; init; }
}

public record SearchableIssueDto
{
    public int IssueId { get; init; }
    public int SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public string? IssueTitle { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? LastSearchedAt { get; init; }
    public int SearchAttempts { get; init; }
}

public record AutoSearchHistoryDto
{
    public int IssueId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public DateTime SearchedAt { get; init; }
    public bool Found { get; init; }
    public int CandidatesFound { get; init; }
    public string? SelectedCandidate { get; init; }
    public string? Error { get; init; }
}

public record AutoSearchResultDto
{
    public int IssueId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public bool Success { get; init; }
    public int CandidatesFound { get; init; }
    public string? SelectedCandidateTitle { get; init; }
    public string? DownloadId { get; init; }
    public string? Error { get; init; }
    public int DurationMs { get; init; }
}

public record AutoSearchBatchResultDto
{
    public int TotalSearched { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int NotFoundCount { get; init; }
    public required List<AutoSearchResultDto> Results { get; init; }
    public int TotalDurationMs { get; init; }
    public string? Error { get; init; }
}
