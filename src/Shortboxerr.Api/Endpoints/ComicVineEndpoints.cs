using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class ComicVineEndpoints
{
    public static void MapComicVineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/comicvine")
            .WithTags("ComicVine");

        // Settings
        group.MapGet("/settings", GetSettings)
            .WithName("GetComicVineSettings")
            .WithOpenApi()
            .Produces<ComicVineSettingsResponse>(200);

        group.MapGet("/settings/apikey", GetFullApiKey)
            .WithName("GetComicVineFullApiKey")
            .WithOpenApi()
            .Produces<ComicVineApiKeyResponse>(200);

        group.MapPut("/settings", UpdateSettings)
            .WithName("UpdateComicVineSettings")
            .WithOpenApi()
            .Produces<ComicVineSettingsResponse>(200)
            .Produces(400);

        // Connection test
        group.MapPost("/test", TestConnection)
            .WithName("TestComicVineConnection")
            .WithOpenApi()
            .Produces<ComicVineTestResult>(200);

        // Rate limit status
        group.MapGet("/ratelimit", GetRateLimitStatus)
            .WithName("GetComicVineRateLimitStatus")
            .WithOpenApi()
            .Produces<ComicVineRateLimitStatus>(200);

        // Search
        group.MapGet("/search/volumes", SearchVolumes)
            .WithName("SearchComicVineVolumes")
            .WithOpenApi()
            .Produces<ComicVineSearchResult<ComicVineVolume>>(200);

        group.MapGet("/search/issues", SearchIssues)
            .WithName("SearchComicVineIssues")
            .WithOpenApi()
            .Produces<ComicVineSearchResult<ComicVineIssue>>(200);

        // Get by ID
        group.MapGet("/volumes/{volumeId:int}", GetVolume)
            .WithName("GetComicVineVolume")
            .WithOpenApi()
            .Produces<ComicVineResult<ComicVineVolume>>(200)
            .Produces(404);

        group.MapGet("/volumes/{volumeId:int}/issues", GetVolumeIssues)
            .WithName("GetComicVineVolumeIssues")
            .WithOpenApi()
            .Produces<ComicVineSearchResult<ComicVineIssue>>(200);

        group.MapGet("/issues/{issueId:int}", GetIssue)
            .WithName("GetComicVineIssue")
            .WithOpenApi()
            .Produces<ComicVineResult<ComicVineIssue>>(200)
            .Produces(404);

        group.MapGet("/publishers/{publisherId:int}", GetPublisher)
            .WithName("GetComicVinePublisher")
            .WithOpenApi()
            .Produces<ComicVineResult<ComicVinePublisher>>(200)
            .Produces(404);
    }

    private static async Task<IResult> GetSettings(
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        
        return Results.Ok(new ComicVineSettingsResponse
        {
            Enabled = settings?.Enabled ?? false,
            HasApiKey = !string.IsNullOrEmpty(settings?.ApiKey),
            MaskedApiKey = MaskApiKey(settings?.ApiKey),
            CacheTtlHours = settings?.CacheTtlHours ?? 24,
            CoverCacheDirectory = settings?.CoverCacheDirectory ?? "/config/covers",
            AutoMatchThreshold = settings?.AutoMatchThreshold ?? 85,
            AutoRefreshEnabled = settings?.AutoRefreshEnabled ?? true,
            RefreshIntervalDays = settings?.RefreshIntervalDays ?? 7
        });
    }

    private static async Task<IResult> GetFullApiKey(
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        
        return Results.Ok(new ComicVineApiKeyResponse
        {
            ApiKey = settings?.ApiKey ?? ""
        });
    }

    private static async Task<IResult> UpdateSettings(
        [FromBody] ComicVineSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Get existing settings
        var existing = await settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        
        // Update fields
        var settings = new ComicVineSettings
        {
            // Only update API key if provided (not null or empty replacement)
            ApiKey = string.IsNullOrEmpty(request.ApiKey) ? (existing?.ApiKey ?? "") : request.ApiKey,
            Enabled = request.Enabled ?? existing?.Enabled ?? false,
            CacheTtlHours = request.CacheTtlHours ?? existing?.CacheTtlHours ?? 24,
            CoverCacheDirectory = request.CoverCacheDirectory ?? existing?.CoverCacheDirectory ?? "/config/covers",
            AutoMatchThreshold = request.AutoMatchThreshold ?? existing?.AutoMatchThreshold ?? 85,
            AutoRefreshEnabled = request.AutoRefreshEnabled ?? existing?.AutoRefreshEnabled ?? true,
            RefreshIntervalDays = request.RefreshIntervalDays ?? existing?.RefreshIntervalDays ?? 7
        };

        await settingsService.SetAsync("comicvine", settings, cancellationToken);

        return Results.Ok(new ComicVineSettingsResponse
        {
            Enabled = settings.Enabled,
            HasApiKey = !string.IsNullOrEmpty(settings.ApiKey),
            MaskedApiKey = MaskApiKey(settings.ApiKey),
            CacheTtlHours = settings.CacheTtlHours,
            CoverCacheDirectory = settings.CoverCacheDirectory,
            AutoMatchThreshold = settings.AutoMatchThreshold,
            AutoRefreshEnabled = settings.AutoRefreshEnabled,
            RefreshIntervalDays = settings.RefreshIntervalDays
        });
    }

    private static async Task<IResult> TestConnection(
        IComicVineClient client,
        CancellationToken cancellationToken)
    {
        var result = await client.TestConnectionAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static IResult GetRateLimitStatus(IComicVineClient client)
    {
        var status = client.GetRateLimitStatus();
        return Results.Ok(status);
    }

    private static async Task<IResult> SearchVolumes(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        IComicVineClient client = null!,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest("Search query is required");
        }

        var result = await client.SearchVolumesAsync(q, page, limit, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchIssues(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        IComicVineClient client = null!,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest("Search query is required");
        }

        var result = await client.SearchIssuesAsync(q, page, limit, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetVolume(
        int volumeId,
        IComicVineClient client,
        CancellationToken cancellationToken)
    {
        var result = await client.GetVolumeAsync(volumeId, cancellationToken);
        
        if (!result.Success || result.Data == null)
        {
            return Results.NotFound(new { error = result.Error ?? "Volume not found" });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetVolumeIssues(
        int volumeId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 100,
        IComicVineClient client = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetVolumeIssuesAsync(volumeId, page, limit, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetIssue(
        int issueId,
        IComicVineClient client,
        CancellationToken cancellationToken)
    {
        var result = await client.GetIssueAsync(issueId, cancellationToken);
        
        if (!result.Success || result.Data == null)
        {
            return Results.NotFound(new { error = result.Error ?? "Issue not found" });
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPublisher(
        int publisherId,
        IComicVineClient client,
        CancellationToken cancellationToken)
    {
        var result = await client.GetPublisherAsync(publisherId, cancellationToken);
        
        if (!result.Success || result.Data == null)
        {
            return Results.NotFound(new { error = result.Error ?? "Publisher not found" });
        }

        return Results.Ok(result);
    }

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            return null;

        return $"{apiKey[..4]}...{apiKey[^4..]}";
    }
}

#region DTOs

public class ComicVineSettingsResponse
{
    public bool Enabled { get; set; }
    public bool HasApiKey { get; set; }
    public string? MaskedApiKey { get; set; }
    public int CacheTtlHours { get; set; }
    public string CoverCacheDirectory { get; set; } = "";
    public int AutoMatchThreshold { get; set; }
    public bool AutoRefreshEnabled { get; set; }
    public int RefreshIntervalDays { get; set; }
}

public class ComicVineApiKeyResponse
{
    public string ApiKey { get; set; } = "";
}

public class ComicVineSettingsRequest
{
    /// <summary>
    /// The ComicVine API key. Leave null to keep existing.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Whether ComicVine integration is enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Cache TTL in hours.
    /// </summary>
    public int? CacheTtlHours { get; set; }

    /// <summary>
    /// Directory for cover cache.
    /// </summary>
    public string? CoverCacheDirectory { get; set; }

    /// <summary>
    /// Auto-match confidence threshold (0-100).
    /// </summary>
    public int? AutoMatchThreshold { get; set; }

    /// <summary>
    /// Whether to auto-refresh metadata.
    /// </summary>
    public bool? AutoRefreshEnabled { get; set; }

    /// <summary>
    /// Refresh interval in days.
    /// </summary>
    public int? RefreshIntervalDays { get; set; }
}

#endregion

