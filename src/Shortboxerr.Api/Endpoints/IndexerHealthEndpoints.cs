using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.BackgroundServices;

namespace Shortboxerr.Api.Endpoints;

public static class IndexerHealthEndpoints
{
    public static void MapIndexerHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/indexers/health")
            .WithTags("Indexer Health");

        group.MapGet("/", GetAllHealthAsync)
            .WithName("GetAllIndexerHealth")
            .WithSummary("Gets health status for all indexers")
            .Produces<IReadOnlyList<IndexerHealthStatusDto>>();

        group.MapGet("/summary", GetHealthSummaryAsync)
            .WithName("GetIndexerHealthSummary")
            .WithSummary("Gets aggregated health summary")
            .Produces<IndexerHealthSummaryDto>();

        group.MapGet("/{indexerId}", GetIndexerHealthAsync)
            .WithName("GetIndexerHealth")
            .WithSummary("Gets health status for a specific indexer")
            .Produces<IndexerHealthStatusDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/check", TriggerHealthCheckAsync)
            .WithName("TriggerHealthCheck")
            .WithSummary("Triggers a health check on all enabled indexers")
            .Produces<IReadOnlyList<IndexerHealthCheckResultDto>>();

        group.MapPost("/check/{indexerId}", CheckIndexerHealthAsync)
            .WithName("CheckIndexerHealth")
            .WithSummary("Performs a health check on a specific indexer")
            .Produces<IndexerHealthCheckResultDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/reset/{indexerId}", ResetIndexerHealthAsync)
            .WithName("ResetIndexerHealth")
            .WithSummary("Resets health data for an indexer")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/healthy", GetHealthyIndexersAsync)
            .WithName("GetHealthyIndexers")
            .WithSummary("Gets list of healthy indexers available for searching")
            .Produces<IReadOnlyList<HealthyIndexerDto>>();
    }

    private static async Task<IResult> GetAllHealthAsync(
        IIndexerHealthService healthService,
        CancellationToken cancellationToken)
    {
        var statuses = await healthService.GetAllHealthAsync(cancellationToken);
        var dtos = statuses.Select(MapToDto).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetHealthSummaryAsync(
        IIndexerHealthService healthService,
        CancellationToken cancellationToken)
    {
        var summary = await healthService.GetHealthSummaryAsync(cancellationToken);
        return Results.Ok(MapToDto(summary));
    }

    private static async Task<IResult> GetIndexerHealthAsync(
        string indexerId,
        IIndexerHealthService healthService,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await healthService.GetHealthAsync(indexerId, cancellationToken);
            return Results.Ok(MapToDto(status));
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { message = $"Indexer with ID {indexerId} not found" });
        }
    }

    private static async Task<IResult> TriggerHealthCheckAsync(
        IIndexerHealthService healthService,
        IndexerHealthBackgroundService backgroundService,
        CancellationToken cancellationToken)
    {
        var results = await backgroundService.TriggerHealthCheckAsync(cancellationToken);
        var dtos = results.Select(MapToDto).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> CheckIndexerHealthAsync(
        string indexerId,
        IIndexerHealthService healthService,
        CancellationToken cancellationToken)
    {
        var result = await healthService.CheckHealthAsync(indexerId, cancellationToken);

        if (result.ErrorMessage == "Indexer not found")
        {
            return Results.NotFound(new { message = $"Indexer with ID {indexerId} not found" });
        }

        return Results.Ok(MapToDto(result));
    }

    private static async Task<IResult> ResetIndexerHealthAsync(
        string indexerId,
        IIndexerHealthService healthService,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var indexer = await indexerProvider.GetIndexerAsync(indexerId, cancellationToken);
        if (indexer == null)
        {
            return Results.NotFound(new { message = $"Indexer with ID {indexerId} not found" });
        }

        await healthService.ResetHealthAsync(indexerId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetHealthyIndexersAsync(
        IIndexerHealthService healthService,
        CancellationToken cancellationToken)
    {
        var indexers = await healthService.GetHealthyIndexersAsync(cancellationToken);
        var dtos = indexers.Select(i => new HealthyIndexerDto
        {
            Id = i.Id,
            Name = i.Name,
            Priority = i.Priority
        }).ToList();
        return Results.Ok(dtos);
    }

    private static IndexerHealthStatusDto MapToDto(IndexerHealthStatus status) => new()
    {
        IndexerId = status.IndexerId,
        IndexerName = status.IndexerName,
        State = status.State.ToString(),
        IsHealthy = status.IsHealthy,
        IsRateLimited = status.IsRateLimited,
        RateLimitExpiresAt = status.RateLimitExpiresAt,
        AverageResponseTimeMs = status.AverageResponseTimeMs,
        LastResponseTimeMs = status.LastResponseTimeMs,
        SuccessCount = status.SuccessCount,
        FailureCount = status.FailureCount,
        SuccessRate = status.SuccessRate,
        LastSuccessAt = status.LastSuccessAt,
        LastFailureAt = status.LastFailureAt,
        LastErrorMessage = status.LastErrorMessage,
        ConsecutiveFailures = status.ConsecutiveFailures,
        LastUpdatedAt = status.LastUpdatedAt
    };

    private static IndexerHealthCheckResultDto MapToDto(IndexerHealthCheckResult result) => new()
    {
        IndexerId = result.IndexerId,
        IndexerName = result.IndexerName,
        Success = result.Success,
        ResponseTimeMs = result.ResponseTimeMs,
        ErrorMessage = result.ErrorMessage,
        StatusCode = result.StatusCode,
        IsRateLimited = result.IsRateLimited,
        CheckedAt = result.CheckedAt
    };

    private static IndexerHealthSummaryDto MapToDto(IndexerHealthSummary summary) => new()
    {
        TotalIndexers = summary.TotalIndexers,
        EnabledIndexers = summary.EnabledIndexers,
        HealthyIndexers = summary.HealthyIndexers,
        DegradedIndexers = summary.DegradedIndexers,
        UnavailableIndexers = summary.UnavailableIndexers,
        OfflineIndexers = summary.OfflineIndexers,
        RateLimitedIndexers = summary.RateLimitedIndexers,
        AverageResponseTimeMs = summary.AverageResponseTimeMs,
        OverallHealthPercent = summary.OverallHealthPercent,
        GeneratedAt = summary.GeneratedAt
    };
}

public class IndexerHealthStatusDto
{
    public required string IndexerId { get; init; }
    public required string IndexerName { get; init; }
    public required string State { get; init; }
    public bool IsHealthy { get; init; }
    public bool IsRateLimited { get; init; }
    public DateTime? RateLimitExpiresAt { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public double? LastResponseTimeMs { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double SuccessRate { get; init; }
    public DateTime? LastSuccessAt { get; init; }
    public DateTime? LastFailureAt { get; init; }
    public string? LastErrorMessage { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime LastUpdatedAt { get; init; }
}

public class IndexerHealthCheckResultDto
{
    public required string IndexerId { get; init; }
    public required string IndexerName { get; init; }
    public bool Success { get; init; }
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }
    public bool IsRateLimited { get; init; }
    public DateTime CheckedAt { get; init; }
}

public class IndexerHealthSummaryDto
{
    public int TotalIndexers { get; init; }
    public int EnabledIndexers { get; init; }
    public int HealthyIndexers { get; init; }
    public int DegradedIndexers { get; init; }
    public int UnavailableIndexers { get; init; }
    public int OfflineIndexers { get; init; }
    public int RateLimitedIndexers { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public double OverallHealthPercent { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public class HealthyIndexerDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; }
}
