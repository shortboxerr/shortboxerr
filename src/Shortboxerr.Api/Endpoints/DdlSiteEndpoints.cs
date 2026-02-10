using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Api.Endpoints;

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
