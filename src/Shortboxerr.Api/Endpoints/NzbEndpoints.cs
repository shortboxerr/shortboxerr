using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class NzbEndpoints
{
    private const string DownloadClientSettingsKey = "nzb_download_client";

    public static void MapNzbEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/nzb")
            .WithTags("NZB");

        // === Indexer Endpoints ===
        group.MapGet("/indexers", GetIndexers)
            .WithName("GetNzbIndexers")
            .WithOpenApi()
            .Produces<IndexersResponse>(200);

        group.MapGet("/indexers/{id}", GetIndexer)
            .WithName("GetNzbIndexer")
            .WithOpenApi()
            .Produces<NewznabIndexer>(200)
            .Produces(404);

        group.MapPost("/indexers", AddIndexer)
            .WithName("AddNzbIndexer")
            .WithOpenApi()
            .Produces<NewznabIndexer>(201)
            .Produces<ValidationProblemDetails>(400);

        group.MapPut("/indexers/{id}", UpdateIndexer)
            .WithName("UpdateNzbIndexer")
            .WithOpenApi()
            .Produces<NewznabIndexer>(200)
            .Produces(404)
            .Produces<ValidationProblemDetails>(400);

        group.MapDelete("/indexers/{id}", DeleteIndexer)
            .WithName("DeleteNzbIndexer")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        group.MapPost("/indexers/{id}/test", TestIndexer)
            .WithName("TestNzbIndexer")
            .WithOpenApi()
            .Produces<NewznabTestResult>(200)
            .Produces(404);

        group.MapPost("/indexers/test", TestIndexerConfig)
            .WithName("TestNzbIndexerConfig")
            .WithOpenApi()
            .Produces<NewznabTestResult>(200);

        group.MapGet("/indexers/presets", GetIndexerPresets)
            .WithName("GetNzbIndexerPresets")
            .WithOpenApi()
            .Produces<IndexerPresetsResponse>(200);

        // === Download Client Endpoints ===
        group.MapGet("/download-client", GetDownloadClientSettings)
            .WithName("GetNzbDownloadClient")
            .WithOpenApi()
            .Produces<DownloadClientSettingsResponse>(200);

        group.MapPut("/download-client", UpdateDownloadClientSettings)
            .WithName("UpdateNzbDownloadClient")
            .WithOpenApi()
            .Produces<DownloadClientSettingsResponse>(200)
            .Produces<ValidationProblemDetails>(400);

        group.MapPost("/download-client/test", TestDownloadClient)
            .WithName("TestNzbDownloadClient")
            .WithOpenApi()
            .Produces<NzbClientTestResult>(200);

        // === Search Endpoints ===
        group.MapGet("/search", SearchNzb)
            .WithName("SearchNzb")
            .WithOpenApi()
            .Produces<NzbSearchResponse>(200);
    }

    // === Indexer Handlers ===

    private static async Task<IResult> GetIndexers(
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var indexers = await indexerProvider.GetIndexersAsync(cancellationToken);
        var enabledCount = indexers.Count(i => i.Enabled);

        return Results.Ok(new IndexersResponse
        {
            Indexers = indexers.ToList(),
            TotalCount = indexers.Count,
            EnabledCount = enabledCount
        });
    }

    private static async Task<IResult> GetIndexer(
        string id,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var indexer = await indexerProvider.GetIndexerAsync(id, cancellationToken);

        if (indexer == null)
        {
            return Results.NotFound(new { error = "Indexer not found" });
        }

        return Results.Ok(indexer);
    }

    private static async Task<IResult> AddIndexer(
        [FromBody] AddIndexerRequest request,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Name is required" }
            });
        }

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["baseUrl"] = new[] { "Base URL is required" }
            });
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["apiKey"] = new[] { "API key is required" }
            });
        }

        var indexer = new NewznabIndexer
        {
            Name = request.Name,
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            ApiKey = request.ApiKey,
            Enabled = request.Enabled ?? true,
            Priority = request.Priority ?? 50,
            Categories = request.Categories ?? new List<int> { 7030, 7000 }
        };

        var created = await indexerProvider.AddIndexerAsync(indexer, cancellationToken);
        return Results.Created($"/api/v1/nzb/indexers/{created.Id}", created);
    }

    private static async Task<IResult> UpdateIndexer(
        string id,
        [FromBody] UpdateIndexerRequest request,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var existing = await indexerProvider.GetIndexerAsync(id, cancellationToken);

        if (existing == null)
        {
            return Results.NotFound(new { error = "Indexer not found" });
        }

        var updated = new NewznabIndexer
        {
            Id = id,
            Name = request.Name ?? existing.Name,
            BaseUrl = (request.BaseUrl ?? existing.BaseUrl).TrimEnd('/'),
            ApiKey = request.ApiKey ?? existing.ApiKey,
            Enabled = request.Enabled ?? existing.Enabled,
            Priority = request.Priority ?? existing.Priority,
            Categories = request.Categories ?? existing.Categories
        };

        var result = await indexerProvider.UpdateIndexerAsync(updated, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteIndexer(
        string id,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var deleted = await indexerProvider.DeleteIndexerAsync(id, cancellationToken);

        if (!deleted)
        {
            return Results.NotFound(new { error = "Indexer not found" });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> TestIndexer(
        string id,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var indexer = await indexerProvider.GetIndexerAsync(id, cancellationToken);

        if (indexer == null)
        {
            return Results.NotFound(new { error = "Indexer not found" });
        }

        var result = await indexerProvider.TestIndexerAsync(indexer, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> TestIndexerConfig(
        [FromBody] TestIndexerRequest request,
        INewznabClient newznabClient,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BaseUrl) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return Results.Ok(NewznabTestResult.Failed("Base URL and API key are required"));
        }

        var indexer = new NewznabIndexer
        {
            Name = "Test",
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            ApiKey = request.ApiKey
        };

        var result = await newznabClient.TestConnectionAsync(indexer, cancellationToken);
        return Results.Ok(result);
    }

    private static IResult GetIndexerPresets()
    {
        var presets = NzbIndexerPresets.GetAvailablePresets();

        var presetInfos = presets.Select(name =>
        {
            var sample = NzbIndexerPresets.GetPreset(name, "SAMPLE_KEY");
            return new IndexerPresetInfo
            {
                Id = name,
                Name = sample?.Name ?? name,
                BaseUrl = sample?.BaseUrl ?? "",
                DefaultCategories = sample?.Categories ?? new List<int> { 7030, 7000 }
            };
        }).ToList();

        return Results.Ok(new IndexerPresetsResponse
        {
            Presets = presetInfos
        });
    }

    // === Download Client Handlers ===

    private static async Task<IResult> GetDownloadClientSettings(
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<DownloadClientSettings>(
            DownloadClientSettingsKey,
            new DownloadClientSettings(),
            cancellationToken) ?? new DownloadClientSettings();

        return Results.Ok(new DownloadClientSettingsResponse
        {
            ClientType = settings.ClientType,
            Sabnzbd = settings.Sabnzbd,
            IsConfigured = !string.IsNullOrEmpty(settings.Sabnzbd?.Host) && !string.IsNullOrEmpty(settings.Sabnzbd?.ApiKey)
        });
    }

    private static async Task<IResult> UpdateDownloadClientSettings(
        [FromBody] UpdateDownloadClientRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = new DownloadClientSettings
        {
            ClientType = request.ClientType ?? NzbDownloadClientType.SABnzbd,
            Sabnzbd = request.Sabnzbd
        };

        await settingsService.SetAsync(DownloadClientSettingsKey, settings, cancellationToken);

        return Results.Ok(new DownloadClientSettingsResponse
        {
            ClientType = settings.ClientType,
            Sabnzbd = settings.Sabnzbd,
            IsConfigured = !string.IsNullOrEmpty(settings.Sabnzbd?.Host) && !string.IsNullOrEmpty(settings.Sabnzbd?.ApiKey)
        });
    }

    private static async Task<IResult> TestDownloadClient(
        [FromBody] TestDownloadClientRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ClientType == NzbDownloadClientType.SABnzbd)
        {
            if (request.Sabnzbd == null ||
                string.IsNullOrWhiteSpace(request.Sabnzbd.Host) ||
                string.IsNullOrWhiteSpace(request.Sabnzbd.ApiKey))
            {
                return Results.Ok(NzbClientTestResult.Failed("SABnzbd host and API key are required"));
            }

            // Create a temporary client to test
            using var httpClient = new HttpClient();
            var client = new Shortboxerr.Infrastructure.Nzb.SabnzbdClient(httpClient, request.Sabnzbd);
            var result = await client.TestConnectionAsync(cancellationToken);
            return Results.Ok(result);
        }

        if (request.ClientType == NzbDownloadClientType.NZBGet)
        {
            if (request.Nzbget == null ||
                string.IsNullOrWhiteSpace(request.Nzbget.Host) ||
                string.IsNullOrWhiteSpace(request.Nzbget.Username) ||
                string.IsNullOrWhiteSpace(request.Nzbget.Password))
            {
                return Results.Ok(NzbClientTestResult.Failed("NZBGet host, username, and password are required"));
            }

            // Create a temporary client to test
            using var httpClient = new HttpClient();
            var client = new Shortboxerr.Infrastructure.Nzb.NzbgetClient(httpClient, request.Nzbget);
            var result = await client.TestConnectionAsync(cancellationToken);
            return Results.Ok(result);
        }

        return Results.Ok(NzbClientTestResult.Failed($"Client type {request.ClientType} is not yet supported"));
    }

    // === Search Handler ===

    private static async Task<IResult> SearchNzb(
        [FromQuery] string? query,
        [FromQuery] string? title,
        [FromQuery] int? limit,
        INzbIndexerProvider indexerProvider,
        CancellationToken cancellationToken)
    {
        var searchQuery = new NewznabSearchQuery
        {
            Query = query,
            Title = title,
            Limit = limit ?? 100
        };

        var result = await indexerProvider.SearchAllAsync(searchQuery, cancellationToken);

        return Results.Ok(new NzbSearchResponse
        {
            Releases = result.Releases.ToList(),
            TotalResults = result.TotalResults,
            IndexersSearched = result.IndexersSearched,
            IndexersSuccessful = result.IndexersSuccessful,
            DurationMs = (long)result.Duration.TotalMilliseconds,
            IndexerResults = result.IndexerResults.ToList()
        });
    }
}

// === Request/Response Models ===

public class IndexersResponse
{
    public required List<NewznabIndexer> Indexers { get; init; }
    public int TotalCount { get; init; }
    public int EnabledCount { get; init; }
}

public class AddIndexerRequest
{
    public string? Name { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool? Enabled { get; set; }
    public int? Priority { get; set; }
    public List<int>? Categories { get; set; }
}

public class UpdateIndexerRequest
{
    public string? Name { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool? Enabled { get; set; }
    public int? Priority { get; set; }
    public List<int>? Categories { get; set; }
}

public class TestIndexerRequest
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public class IndexerPresetsResponse
{
    public required List<IndexerPresetInfo> Presets { get; init; }
}

public class IndexerPresetInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required List<int> DefaultCategories { get; init; }
}

public class DownloadClientSettings
{
    public NzbDownloadClientType ClientType { get; set; } = NzbDownloadClientType.SABnzbd;
    public SabnzbdSettings? Sabnzbd { get; set; }
}

public class DownloadClientSettingsResponse
{
    public NzbDownloadClientType ClientType { get; init; }
    public SabnzbdSettings? Sabnzbd { get; init; }
    public bool IsConfigured { get; init; }
}

public class UpdateDownloadClientRequest
{
    public NzbDownloadClientType? ClientType { get; set; }
    public SabnzbdSettings? Sabnzbd { get; set; }
}

public class TestDownloadClientRequest
{
    public NzbDownloadClientType ClientType { get; set; }
    public SabnzbdSettings? Sabnzbd { get; set; }
    public NzbgetSettings? Nzbget { get; set; }
}

public class NzbSearchResponse
{
    public required List<NewznabRelease> Releases { get; init; }
    public int TotalResults { get; init; }
    public int IndexersSearched { get; init; }
    public int IndexersSuccessful { get; init; }
    public long DurationMs { get; init; }
    public required List<IndexerSearchResult> IndexerResults { get; init; }
}
