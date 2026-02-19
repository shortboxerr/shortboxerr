using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Api.Endpoints;

public static class DownloadClientHealthEndpoints
{
    public static void MapDownloadClientHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/downloadclients/health")
            .WithTags("Download Client Health");

        group.MapGet("/", GetAllHealthAsync)
            .WithName("GetAllDownloadClientHealth")
            .WithSummary("Gets health status for all download clients")
            .Produces<IReadOnlyList<DownloadClientHealthStatusDto>>();

        group.MapGet("/summary", GetHealthSummaryAsync)
            .WithName("GetDownloadClientHealthSummary")
            .WithSummary("Gets aggregated health summary")
            .Produces<DownloadClientHealthSummaryDto>();

        group.MapGet("/{providerId:int}", GetClientHealthAsync)
            .WithName("GetDownloadClientHealth")
            .WithSummary("Gets health status for a specific download client")
            .Produces<DownloadClientHealthStatusDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/check", TriggerHealthCheckAsync)
            .WithName("TriggerDownloadClientHealthCheck")
            .WithSummary("Triggers a health check on all enabled download clients")
            .Produces<IReadOnlyList<DownloadClientCheckResultDto>>();

        group.MapPost("/check/{providerId:int}", CheckClientHealthAsync)
            .WithName("CheckDownloadClientHealth")
            .WithSummary("Performs a health check on a specific download client")
            .Produces<DownloadClientCheckResultDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/reset/{providerId:int}", ResetClientHealthAsync)
            .WithName("ResetDownloadClientHealth")
            .WithSummary("Resets health data for a download client")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/healthy", GetHealthyClientsAsync)
            .WithName("GetHealthyDownloadClients")
            .WithSummary("Gets list of healthy download clients available for downloads")
            .Produces<IReadOnlyList<HealthyDownloadClientDto>>();
    }

    private static async Task<IResult> GetAllHealthAsync(
        IDownloadClientHealthService healthService,
        CancellationToken cancellationToken)
    {
        var statuses = await healthService.GetAllHealthAsync(cancellationToken);
        var dtos = statuses.Select(MapToDto).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetHealthSummaryAsync(
        IDownloadClientHealthService healthService,
        CancellationToken cancellationToken)
    {
        var summary = await healthService.GetHealthSummaryAsync(cancellationToken);
        return Results.Ok(MapToDto(summary));
    }

    private static async Task<IResult> GetClientHealthAsync(
        int providerId,
        IDownloadClientHealthService healthService,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await healthService.GetHealthAsync(providerId, cancellationToken);
            return Results.Ok(MapToDto(status));
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { message = $"Download client with ID {providerId} not found" });
        }
    }

    private static async Task<IResult> TriggerHealthCheckAsync(
        IDownloadClientHealthService healthService,
        CancellationToken cancellationToken)
    {
        var results = await healthService.CheckAllHealthAsync(cancellationToken);
        var dtos = results.Select(MapToDto).ToList();
        return Results.Ok(dtos);
    }

    private static async Task<IResult> CheckClientHealthAsync(
        int providerId,
        IDownloadClientHealthService healthService,
        CancellationToken cancellationToken)
    {
        var result = await healthService.CheckHealthAsync(providerId, cancellationToken);

        if (result.ErrorMessage == "Download client not found")
        {
            return Results.NotFound(new { message = $"Download client with ID {providerId} not found" });
        }

        return Results.Ok(MapToDto(result));
    }

    private static async Task<IResult> ResetClientHealthAsync(
        int providerId,
        IDownloadClientHealthService healthService,
        IProviderManager providerManager,
        CancellationToken cancellationToken)
    {
        var provider = await providerManager.GetByIdAsync(providerId, cancellationToken);
        if (provider == null || provider.Category != ProviderCategory.DownloadClient)
        {
            return Results.NotFound(new { message = $"Download client with ID {providerId} not found" });
        }

        await healthService.ResetHealthAsync(providerId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetHealthyClientsAsync(
        IDownloadClientHealthService healthService,
        ProviderType? type,
        CancellationToken cancellationToken)
    {
        var clients = await healthService.GetHealthyClientsAsync(type, cancellationToken);
        var dtos = clients.Select(c => new HealthyDownloadClientDto
        {
            Id = c.Id,
            Name = c.Name,
            Type = c.Type.ToString(),
            Priority = c.Priority
        }).ToList();
        return Results.Ok(dtos);
    }

    private static DownloadClientHealthStatusDto MapToDto(DownloadClientHealthStatus status) => new()
    {
        ProviderId = status.ProviderId,
        ProviderName = status.ProviderName,
        Type = status.Type.ToString(),
        State = status.State.ToString(),
        IsHealthy = status.IsHealthy,
        AverageDownloadTimeSeconds = status.AverageDownloadTimeSeconds,
        LastDownloadTimeSeconds = status.LastDownloadTimeSeconds,
        SuccessCount = status.SuccessCount,
        FailureCount = status.FailureCount,
        SuccessRate = status.SuccessRate,
        LastSuccessAt = status.LastSuccessAt,
        LastFailureAt = status.LastFailureAt,
        LastErrorMessage = status.LastErrorMessage,
        ConsecutiveFailures = status.ConsecutiveFailures,
        LastUpdatedAt = status.LastUpdatedAt
    };

    private static DownloadClientCheckResultDto MapToDto(DownloadClientCheckResult result) => new()
    {
        ProviderId = result.ProviderId,
        ProviderName = result.ProviderName,
        Type = result.Type.ToString(),
        Success = result.Success,
        ResponseTimeMs = result.ResponseTimeMs,
        ErrorMessage = result.ErrorMessage,
        CheckedAt = result.CheckedAt
    };

    private static DownloadClientHealthSummaryDto MapToDto(DownloadClientHealthSummary summary) => new()
    {
        TotalClients = summary.TotalClients,
        EnabledClients = summary.EnabledClients,
        HealthyClients = summary.HealthyClients,
        DegradedClients = summary.DegradedClients,
        UnavailableClients = summary.UnavailableClients,
        OfflineClients = summary.OfflineClients,
        AverageDownloadTimeSeconds = summary.AverageDownloadTimeSeconds,
        OverallHealthPercent = summary.OverallHealthPercent,
        GeneratedAt = summary.GeneratedAt
    };
}

public class DownloadClientHealthStatusDto
{
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string Type { get; init; }
    public required string State { get; init; }
    public bool IsHealthy { get; init; }
    public double AverageDownloadTimeSeconds { get; init; }
    public double? LastDownloadTimeSeconds { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double SuccessRate { get; init; }
    public DateTime? LastSuccessAt { get; init; }
    public DateTime? LastFailureAt { get; init; }
    public string? LastErrorMessage { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime LastUpdatedAt { get; init; }
}

public class DownloadClientCheckResultDto
{
    public int ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string Type { get; init; }
    public bool Success { get; init; }
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CheckedAt { get; init; }
}

public class DownloadClientHealthSummaryDto
{
    public int TotalClients { get; init; }
    public int EnabledClients { get; init; }
    public int HealthyClients { get; init; }
    public int DegradedClients { get; init; }
    public int UnavailableClients { get; init; }
    public int OfflineClients { get; init; }
    public double AverageDownloadTimeSeconds { get; init; }
    public double OverallHealthPercent { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public class HealthyDownloadClientDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public int Priority { get; init; }
}
