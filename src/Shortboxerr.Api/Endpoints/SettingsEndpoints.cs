using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings")
            .WithTags("Settings");

        // UI Settings
        group.MapGet("/ui", GetUiSettings)
            .WithName("GetUiSettings")
            .WithOpenApi()
            .Produces<UiSettings>(200);

        group.MapPut("/ui", UpdateUiSettings)
            .WithName("UpdateUiSettings")
            .WithOpenApi()
            .Produces<UiSettings>(200);

        // General Settings
        group.MapGet("/general", GetGeneralSettings)
            .WithName("GetGeneralSettings")
            .WithOpenApi()
            .Produces<GeneralSettings>(200);

        group.MapPut("/general", UpdateGeneralSettings)
            .WithName("UpdateGeneralSettings")
            .WithOpenApi()
            .Produces<GeneralSettings>(200);

        // Folder Settings (convenience endpoints)
        group.MapGet("/folders", GetFolderSettings)
            .WithName("GetFolderSettings")
            .WithOpenApi()
            .Produces<FolderSettingsResponse>(200);

        group.MapPut("/folders", UpdateFolderSettings)
            .WithName("UpdateFolderSettings")
            .WithOpenApi()
            .Produces<FolderSettingsResponse>(200);

        // Naming Format Tokens
        group.MapGet("/naming/tokens", GetNamingTokens)
            .WithName("GetNamingTokens")
            .WithOpenApi()
            .Produces<NamingTokensResponse>(200);

        // API Key Management
        group.MapGet("/apikey", GetApiKey)
            .WithName("GetApiKey")
            .WithOpenApi()
            .Produces<ApiKeyResponse>(200);

        group.MapGet("/apikey/full", GetApiKeyFull)
            .WithName("GetApiKeyFull")
            .WithOpenApi()
            .Produces<ApiKeyResponse>(200);

        group.MapPost("/apikey/regenerate", RegenerateApiKey)
            .WithName("RegenerateApiKey")
            .WithOpenApi()
            .Produces<ApiKeyResponse>(200);

        // Generic key-value access
        group.MapGet("/{key}", GetSetting)
            .WithName("GetSetting")
            .WithOpenApi()
            .Produces<SettingResponse>(200)
            .Produces(404);

        group.MapPut("/{key}", SetSetting)
            .WithName("SetSetting")
            .WithOpenApi()
            .Produces<SettingResponse>(200);

        group.MapDelete("/{key}", DeleteSetting)
            .WithName("DeleteSetting")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);
    }

    private static async Task<IResult> GetUiSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetUiSettingsAsync(cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateUiSettings(
        UiSettings request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Validate theme value
        var validThemes = new[] { "dark", "light", "system" };
        if (!validThemes.Contains(request.Theme.ToLowerInvariant()))
        {
            return Results.BadRequest(new { error = "Invalid theme. Must be 'dark', 'light', or 'system'." });
        }

        request.Theme = request.Theme.ToLowerInvariant();

        // Validate page size
        if (request.PageSize < 10 || request.PageSize > 500)
        {
            return Results.BadRequest(new { error = "PageSize must be between 10 and 500." });
        }

        await settingsService.SetUiSettingsAsync(request, cancellationToken);
        return Results.Ok(request);
    }

    private static async Task<IResult> GetGeneralSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetGeneralSettingsAsync(cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateGeneralSettings(
        GeneralSettings request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        await settingsService.SetGeneralSettingsAsync(request, cancellationToken);
        return Results.Ok(request);
    }

    private static async Task<IResult> GetFolderSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var general = await settingsService.GetGeneralSettingsAsync(cancellationToken);
        return Results.Ok(new FolderSettingsResponse
        {
            ComicLibraryPath = general.ComicLibraryPath,
            DownloadFolder = general.DownloadFolder,
            StagingFolder = general.StagingFolder,
            AutoMoveToStaging = general.AutoMoveToStaging
        });
    }

    private static async Task<IResult> UpdateFolderSettings(
        FolderSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var general = await settingsService.GetGeneralSettingsAsync(cancellationToken);
        
        if (!string.IsNullOrEmpty(request.ComicLibraryPath))
            general.ComicLibraryPath = request.ComicLibraryPath;
        if (!string.IsNullOrEmpty(request.DownloadFolder))
            general.DownloadFolder = request.DownloadFolder;
        if (!string.IsNullOrEmpty(request.StagingFolder))
            general.StagingFolder = request.StagingFolder;
        if (request.AutoMoveToStaging.HasValue)
            general.AutoMoveToStaging = request.AutoMoveToStaging.Value;

        await settingsService.SetGeneralSettingsAsync(general, cancellationToken);

        return Results.Ok(new FolderSettingsResponse
        {
            ComicLibraryPath = general.ComicLibraryPath,
            DownloadFolder = general.DownloadFolder,
            StagingFolder = general.StagingFolder,
            AutoMoveToStaging = general.AutoMoveToStaging
        });
    }

    private static IResult GetNamingTokens()
    {
        return Results.Ok(new NamingTokensResponse
        {
            SeriesFolderTokens = new[]
            {
                new NamingToken("{Series Title}", "The title of the series", "Batman"),
                new NamingToken("{Series Year}", "The year the series started", "2020"),
                new NamingToken("{Publisher}", "The publisher name", "DC"),
                new NamingToken("{Status}", "Series status (Continuing, Ended, Hiatus)", "Continuing")
            },
            IssueFileTokens = new[]
            {
                new NamingToken("{Series Title}", "The title of the series", "Batman"),
                new NamingToken("{Issue}", "Issue number (padded)", "001"),
                new NamingToken("{Issue Title}", "Title of the specific issue", "The Court of Owls"),
                new NamingToken("{Year}", "Release year of the issue", "2020"),
                new NamingToken("{Publisher}", "The publisher name", "DC"),
                new NamingToken("{Quality}", "Quality tag (Digital, Webrip, etc.)", "Digital")
            },
            CollectionFileTokens = new[]
            {
                new NamingToken("{Series Title}", "The title of the series", "Batman"),
                new NamingToken("{Edition Type}", "Type of collection (TPB, HC, Omnibus)", "TPB"),
                new NamingToken("{Volume}", "Volume number", "01"),
                new NamingToken("{Collection Title}", "Title of the collection", "Court of Owls"),
                new NamingToken("{Year}", "Release year of the collection", "2020"),
                new NamingToken("{Publisher}", "The publisher name", "DC")
            }
        });
    }

    private static async Task<IResult> GetApiKey(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var keyInfo = await settingsService.GetApiKeyAsync(includeFull: false, cancellationToken);
        return Results.Ok(new ApiKeyResponse
        {
            MaskedKey = keyInfo.MaskedKey,
            FullKey = null, // Never return full key on regular get
            CreatedAt = keyInfo.CreatedAt,
            LastUsedAt = keyInfo.LastUsedAt
        });
    }

    private static async Task<IResult> GetApiKeyFull(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var keyInfo = await settingsService.GetApiKeyAsync(includeFull: true, cancellationToken);
        return Results.Ok(new ApiKeyResponse
        {
            MaskedKey = keyInfo.MaskedKey,
            FullKey = keyInfo.FullKey,
            CreatedAt = keyInfo.CreatedAt,
            LastUsedAt = keyInfo.LastUsedAt
        });
    }

    private static async Task<IResult> RegenerateApiKey(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var keyInfo = await settingsService.RegenerateApiKeyAsync(cancellationToken);
        return Results.Ok(new ApiKeyResponse
        {
            MaskedKey = keyInfo.MaskedKey,
            FullKey = keyInfo.FullKey, // Return full key on regenerate
            CreatedAt = keyInfo.CreatedAt,
            LastUsedAt = keyInfo.LastUsedAt,
            IsNewKey = true
        });
    }

    private static async Task<IResult> GetSetting(string key, ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var value = await settingsService.GetAsync(key, cancellationToken);
        if (value == null)
        {
            return Results.NotFound(new { error = $"Setting '{key}' not found." });
        }
        return Results.Ok(new SettingResponse { Key = key, Value = value });
    }

    private static async Task<IResult> SetSetting(
        string key,
        SetSettingRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        await settingsService.SetAsync(key, request.Value, cancellationToken);
        return Results.Ok(new SettingResponse { Key = key, Value = request.Value });
    }

    private static async Task<IResult> DeleteSetting(string key, ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var value = await settingsService.GetAsync(key, cancellationToken);
        if (value == null)
        {
            return Results.NotFound(new { error = $"Setting '{key}' not found." });
        }
        await settingsService.DeleteAsync(key, cancellationToken);
        return Results.NoContent();
    }
}

// DTOs

public class FolderSettingsRequest
{
    public string? ComicLibraryPath { get; set; }
    public string? DownloadFolder { get; set; }
    public string? StagingFolder { get; set; }
    public bool? AutoMoveToStaging { get; set; }
}

public class FolderSettingsResponse
{
    public string ComicLibraryPath { get; set; } = "";
    public string DownloadFolder { get; set; } = "";
    public string StagingFolder { get; set; } = "";
    public bool AutoMoveToStaging { get; set; }
}

public class NamingTokensResponse
{
    public NamingToken[] SeriesFolderTokens { get; set; } = Array.Empty<NamingToken>();
    public NamingToken[] IssueFileTokens { get; set; } = Array.Empty<NamingToken>();
    public NamingToken[] CollectionFileTokens { get; set; } = Array.Empty<NamingToken>();
}

public record NamingToken(string Token, string Description, string Example);

public class SettingResponse
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class SetSettingRequest
{
    public string Value { get; set; } = "";
}

public class ApiKeyResponse
{
    /// <summary>
    /// The masked API key (shows prefix and last 4 characters).
    /// </summary>
    public string MaskedKey { get; set; } = "";

    /// <summary>
    /// The full API key (only returned when explicitly requested or on regenerate).
    /// </summary>
    public string? FullKey { get; set; }

    /// <summary>
    /// When the API key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the API key was last used (null if never used).
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// True if this is a newly generated key (on regenerate).
    /// </summary>
    public bool IsNewKey { get; set; }
}

