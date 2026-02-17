using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for site health monitoring.
/// </summary>
public static class SiteHealthEndpoints
{
    public static void MapSiteHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddl/health")
            .WithTags("Site Health")
            .WithOpenApi();

        // GET health status for all sites
        group.MapGet("/", async (ISiteHealthService healthService, CancellationToken ct) =>
        {
            var statuses = await healthService.GetAllHealthStatusesAsync(ct);
            return Results.Ok(statuses.Select(s => new SiteHealthStatusDto
            {
                SiteType = s.SiteType,
                DisplayName = s.DisplayName,
                State = s.State.ToString(),
                IsEnabled = s.IsEnabled,
                IsAutoDisabled = s.IsAutoDisabled,
                ConsecutiveFailures = s.ConsecutiveFailures,
                LastErrorMessage = s.LastErrorMessage,
                LastCheckTime = s.LastCheckTime,
                LastSuccessTime = s.LastSuccessTime,
                LastFailureTime = s.LastFailureTime,
                AverageLatencyMs = s.AverageLatencyMs,
                SuccessRate = s.SuccessRate,
                DetectedIssues = s.DetectedIssues.ToList(),
                AutoDisabledAt = s.AutoDisabledAt
            }));
        })
        .WithName("GetAllSiteHealthStatuses")
        .WithDescription("Gets the health status for all registered DDL sites.");

        // GET health status for a specific site
        group.MapGet("/{siteType}", async (string siteType, ISiteHealthService healthService, CancellationToken ct) =>
        {
            var status = await healthService.GetHealthStatusAsync(siteType, ct);
            if (status == null)
            {
                return Results.NotFound(new { message = $"Site type '{siteType}' not found." });
            }

            return Results.Ok(new SiteHealthStatusDto
            {
                SiteType = status.SiteType,
                DisplayName = status.DisplayName,
                State = status.State.ToString(),
                IsEnabled = status.IsEnabled,
                IsAutoDisabled = status.IsAutoDisabled,
                ConsecutiveFailures = status.ConsecutiveFailures,
                LastErrorMessage = status.LastErrorMessage,
                LastCheckTime = status.LastCheckTime,
                LastSuccessTime = status.LastSuccessTime,
                LastFailureTime = status.LastFailureTime,
                AverageLatencyMs = status.AverageLatencyMs,
                SuccessRate = status.SuccessRate,
                DetectedIssues = status.DetectedIssues.ToList(),
                AutoDisabledAt = status.AutoDisabledAt
            });
        })
        .WithName("GetSiteHealthStatus")
        .WithDescription("Gets the health status for a specific DDL site.");

        // POST check health of a specific site
        group.MapPost("/{siteType}/check", async (string siteType, ISiteHealthService healthService, CancellationToken ct) =>
        {
            var result = await healthService.CheckSiteHealthAsync(siteType, ct);
            return Results.Ok(new SiteHealthCheckResultDto
            {
                SiteType = result.SiteType,
                Success = result.Success,
                CheckedAt = result.CheckedAt,
                LatencyMs = result.LatencyMs,
                ResultCount = result.ResultCount,
                ErrorMessage = result.ErrorMessage,
                FailureType = result.FailureType?.ToString(),
                Warnings = result.Warnings.ToList()
            });
        })
        .WithName("CheckSiteHealth")
        .WithDescription("Performs a health check on a specific DDL site.");

        // POST check health of all enabled sites
        group.MapPost("/check-all", async (ISiteHealthService healthService, CancellationToken ct) =>
        {
            var results = await healthService.CheckAllSitesAsync(ct);
            return Results.Ok(results.Select(r => new SiteHealthCheckResultDto
            {
                SiteType = r.SiteType,
                Success = r.Success,
                CheckedAt = r.CheckedAt,
                LatencyMs = r.LatencyMs,
                ResultCount = r.ResultCount,
                ErrorMessage = r.ErrorMessage,
                FailureType = r.FailureType?.ToString(),
                Warnings = r.Warnings.ToList()
            }));
        })
        .WithName("CheckAllSitesHealth")
        .WithDescription("Performs health checks on all enabled DDL sites.");

        // GET health history for a site
        group.MapGet("/{siteType}/history", async (string siteType, int? limit, ISiteHealthService healthService, CancellationToken ct) =>
        {
            var history = await healthService.GetHealthHistoryAsync(siteType, limit ?? 50, ct);
            return Results.Ok(history.Select(r => new SiteHealthCheckResultDto
            {
                SiteType = r.SiteType,
                Success = r.Success,
                CheckedAt = r.CheckedAt,
                LatencyMs = r.LatencyMs,
                ResultCount = r.ResultCount,
                ErrorMessage = r.ErrorMessage,
                FailureType = r.FailureType?.ToString(),
                Warnings = r.Warnings.ToList()
            }));
        })
        .WithName("GetSiteHealthHistory")
        .WithDescription("Gets the health check history for a DDL site.");

        // DELETE clear health history for a site
        group.MapDelete("/{siteType}/history", async (string siteType, ISiteHealthService healthService, CancellationToken ct) =>
        {
            await healthService.ClearHealthHistoryAsync(siteType, ct);
            return Results.Ok(new { message = $"Health history cleared for site '{siteType}'." });
        })
        .WithName("ClearSiteHealthHistory")
        .WithDescription("Clears the health check history for a DDL site.");

        // POST re-enable an auto-disabled site
        group.MapPost("/{siteType}/re-enable", async (string siteType, ISiteHealthService healthService, CancellationToken ct) =>
        {
            var success = await healthService.ReEnableSiteAsync(siteType, ct);
            if (!success)
            {
                return Results.BadRequest(new { message = $"Site '{siteType}' was not auto-disabled or does not exist." });
            }

            return Results.Ok(new { message = $"Site '{siteType}' has been re-enabled.", siteType });
        })
        .WithName("ReEnableAutoDisabledSite")
        .WithDescription("Re-enables a site that was auto-disabled due to health failures.");

        // GET health settings
        group.MapGet("/settings", (ISiteHealthService healthService) =>
        {
            var settings = healthService.GetSettings();
            return Results.Ok(new SiteHealthSettingsDto
            {
                Enabled = settings.Enabled,
                CheckIntervalMinutes = settings.CheckIntervalMinutes,
                UnhealthyThreshold = settings.UnhealthyThreshold,
                AutoDisableThreshold = settings.AutoDisableThreshold,
                AutoDisableEnabled = settings.AutoDisableEnabled,
                CheckTimeoutSeconds = settings.CheckTimeoutSeconds,
                MaxHistoryEntries = settings.MaxHistoryEntries,
                DegradedLatencyThresholdMs = settings.DegradedLatencyThresholdMs,
                DetectLayoutChanges = settings.DetectLayoutChanges
            });
        })
        .WithName("GetSiteHealthSettings")
        .WithDescription("Gets the current site health monitoring settings.");

        // PUT update health settings
        group.MapPut("/settings", (SiteHealthSettingsDto request, ISiteHealthService healthService) =>
        {
            var settings = new SiteHealthSettings
            {
                Enabled = request.Enabled,
                CheckIntervalMinutes = request.CheckIntervalMinutes,
                UnhealthyThreshold = request.UnhealthyThreshold,
                AutoDisableThreshold = request.AutoDisableThreshold,
                AutoDisableEnabled = request.AutoDisableEnabled,
                CheckTimeoutSeconds = request.CheckTimeoutSeconds,
                MaxHistoryEntries = request.MaxHistoryEntries,
                DegradedLatencyThresholdMs = request.DegradedLatencyThresholdMs,
                DetectLayoutChanges = request.DetectLayoutChanges
            };

            healthService.UpdateSettings(settings);
            return Results.Ok(new { message = "Health monitoring settings updated.", settings = request });
        })
        .WithName("UpdateSiteHealthSettings")
        .WithDescription("Updates the site health monitoring settings.");
    }
}

public record SiteHealthStatusDto
{
    public required string SiteType { get; init; }
    public required string DisplayName { get; init; }
    public required string State { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsAutoDisabled { get; init; }
    public int ConsecutiveFailures { get; init; }
    public string? LastErrorMessage { get; init; }
    public DateTime? LastCheckTime { get; init; }
    public DateTime? LastSuccessTime { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public int AverageLatencyMs { get; init; }
    public double SuccessRate { get; init; }
    public List<string> DetectedIssues { get; init; } = new();
    public DateTime? AutoDisabledAt { get; init; }
}

public record SiteHealthCheckResultDto
{
    public required string SiteType { get; init; }
    public bool Success { get; init; }
    public DateTime CheckedAt { get; init; }
    public int LatencyMs { get; init; }
    public int? ResultCount { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FailureType { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public record SiteHealthSettingsDto
{
    public bool Enabled { get; init; } = true;
    public int CheckIntervalMinutes { get; init; } = 30;
    public int UnhealthyThreshold { get; init; } = 3;
    public int AutoDisableThreshold { get; init; } = 5;
    public bool AutoDisableEnabled { get; init; } = true;
    public int CheckTimeoutSeconds { get; init; } = 30;
    public int MaxHistoryEntries { get; init; } = 100;
    public int DegradedLatencyThresholdMs { get; init; } = 5000;
    public bool DetectLayoutChanges { get; init; } = true;
}

/// <summary>
/// API endpoints for DDL site management.
/// </summary>
public static class DdlSiteEndpoints
{
    public static void MapDdlSiteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddl/sites")
            .WithTags("DDL Sites")
            .WithOpenApi();

        // GET all sites with status
        group.MapGet("/", (IDdlSiteAdapterFactory factory) =>
        {
            var ddlFactory = factory as DdlSiteAdapterFactory;
            if (ddlFactory == null)
            {
                return Results.Ok(factory.GetAvailableSiteInfos().Select(s => new DdlSiteStatusDto
                {
                    SiteType = s.SiteType,
                    DisplayName = s.DisplayName,
                    DefaultBaseUrl = s.DefaultBaseUrl,
                    RequiresAuthentication = s.RequiresAuthentication,
                    DefaultRateLimitPerMinute = s.DefaultRateLimitPerMinute,
                    IsEnabled = true,
                    Priority = 0,
                    Health = "Unknown"
                }));
            }

            return Results.Ok(ddlFactory.GetSiteStatuses().Select(s => new DdlSiteStatusDto
            {
                SiteType = s.SiteType,
                DisplayName = s.DisplayName,
                DefaultBaseUrl = s.DefaultBaseUrl,
                RequiresAuthentication = s.RequiresAuthentication,
                DefaultRateLimitPerMinute = s.DefaultRateLimitPerMinute,
                IsEnabled = s.IsEnabled,
                Priority = s.Priority,
                Health = s.Health.ToString(),
                LastError = s.LastError,
                LastSuccessfulSearch = s.LastSuccessfulSearch
            }));
        })
        .WithName("GetDdlSites")
        .WithDescription("Gets all registered DDL sites with their current status.");

        // GET enabled sites only
        group.MapGet("/enabled", (IDdlSiteAdapterFactory factory) =>
        {
            return Results.Ok(factory.GetEnabledSites());
        })
        .WithName("GetEnabledDdlSites")
        .WithDescription("Gets the list of currently enabled DDL sites.");

        // POST enable a site
        group.MapPost("/{siteType}/enable", (string siteType, IDdlSiteAdapterFactory factory) =>
        {
            if (!factory.IsRegistered(siteType))
            {
                return Results.NotFound(new { message = $"Site type '{siteType}' is not registered." });
            }

            var ddlFactory = factory as DdlSiteAdapterFactory;
            if (ddlFactory == null)
            {
                return Results.BadRequest(new { message = "Factory does not support site management." });
            }

            ddlFactory.EnableSite(siteType);
            return Results.Ok(new { message = $"Site '{siteType}' enabled.", siteType, isEnabled = true });
        })
        .WithName("EnableDdlSite")
        .WithDescription("Enables a DDL site for searching.");

        // POST disable a site
        group.MapPost("/{siteType}/disable", (string siteType, IDdlSiteAdapterFactory factory) =>
        {
            if (!factory.IsRegistered(siteType))
            {
                return Results.NotFound(new { message = $"Site type '{siteType}' is not registered." });
            }

            var ddlFactory = factory as DdlSiteAdapterFactory;
            if (ddlFactory == null)
            {
                return Results.BadRequest(new { message = "Factory does not support site management." });
            }

            ddlFactory.DisableSite(siteType);
            return Results.Ok(new { message = $"Site '{siteType}' disabled.", siteType, isEnabled = false });
        })
        .WithName("DisableDdlSite")
        .WithDescription("Disables a DDL site from searching.");

        // POST test a site connection
        group.MapPost("/{siteType}/test", async (string siteType, IDdlSiteAdapterFactory factory, CancellationToken cancellationToken) =>
        {
            if (!factory.IsRegistered(siteType))
            {
                return Results.NotFound(new { message = $"Site type '{siteType}' is not registered." });
            }

            var adapter = factory.GetAdapter(siteType);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Try a simple search to test connectivity
                var result = await adapter.GetLatestAsync(limit: 5, cancellationToken);
                stopwatch.Stop();

                return Results.Ok(new DdlSiteTestResultDto
                {
                    SiteType = siteType,
                    Success = result.Success,
                    Message = result.Success 
                        ? $"Connection successful. Found {result.TotalResults} releases."
                        : result.ErrorMessage ?? "Unknown error",
                    SampleResultCount = result.Success ? result.Candidates.Count : 0,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Results.Ok(new DdlSiteTestResultDto
                {
                    SiteType = siteType,
                    Success = false,
                    Message = $"Connection failed: {ex.Message}",
                    SampleResultCount = 0,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                });
            }
        })
        .WithName("TestDdlSite")
        .WithDescription("Tests connectivity to a DDL site.");

        // PUT update enabled sites (bulk)
        group.MapPut("/enabled", (EnabledSitesRequest request, IDdlSiteAdapterFactory factory) =>
        {
            var ddlFactory = factory as DdlSiteAdapterFactory;
            if (ddlFactory == null)
            {
                return Results.BadRequest(new { message = "Factory does not support site management." });
            }

            ddlFactory.SetEnabledSites(request.SiteTypes);
            return Results.Ok(new { message = "Enabled sites updated.", enabledSites = factory.GetEnabledSites() });
        })
        .WithName("SetEnabledDdlSites")
        .WithDescription("Sets the list of enabled DDL sites.");
    }
}

public record DdlSiteStatusDto
{
    public required string SiteType { get; init; }
    public required string DisplayName { get; init; }
    public required string DefaultBaseUrl { get; init; }
    public bool RequiresAuthentication { get; init; }
    public int DefaultRateLimitPerMinute { get; init; }
    public bool IsEnabled { get; init; }
    public int Priority { get; init; }
    public string Health { get; init; } = "Unknown";
    public string? LastError { get; init; }
    public DateTime? LastSuccessfulSearch { get; init; }
}

public record DdlSiteTestResultDto
{
    public required string SiteType { get; init; }
    public bool Success { get; init; }
    public required string Message { get; init; }
    public int SampleResultCount { get; init; }
    public long LatencyMs { get; init; }
}

public record EnabledSitesRequest(List<string> SiteTypes);
