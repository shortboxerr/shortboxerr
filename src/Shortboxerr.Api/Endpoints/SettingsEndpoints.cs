using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings")
            .WithTags("Settings");

        // Cover Cache Settings
        group.MapGet("/covers", GetCoverSettings)
            .WithName("GetCoverSettings")
            .WithOpenApi()
            .Produces<CoverCacheSettingsResponse>(200);

        group.MapPut("/covers", UpdateCoverSettings)
            .WithName("UpdateCoverSettings")
            .WithOpenApi()
            .Produces<CoverCacheSettingsResponse>(200);

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

        // Logging Settings
        group.MapGet("/logging", GetLoggingSettings)
            .WithName("GetLoggingSettings")
            .WithOpenApi()
            .Produces<LoggingSettings>(200);

        group.MapPut("/logging", UpdateLoggingSettings)
            .WithName("UpdateLoggingSettings")
            .WithOpenApi()
            .Produces<LoggingSettings>(200);

        group.MapPost("/logging/compress", TriggerLogCompression)
            .WithName("TriggerLogCompression")
            .WithDescription("Triggers immediate compression of old log files")
            .WithOpenApi()
            .Produces<LogCompressionResponse>(200);

        // Metron Settings (backup cover source)
        group.MapGet("/metron", GetMetronSettings)
            .WithName("GetMetronSettings")
            .WithDescription("Get Metron API configuration for backup cover lookups")
            .WithOpenApi()
            .Produces<MetronSettingsResponse>(200);

        group.MapPut("/metron", UpdateMetronSettings)
            .WithName("UpdateMetronSettings")
            .WithDescription("Update Metron API configuration")
            .WithOpenApi()
            .Produces<MetronSettingsResponse>(200);

        group.MapPost("/metron/test", TestMetronConnection)
            .WithName("TestMetronConnection")
            .WithDescription("Test Metron API connection with current credentials")
            .WithOpenApi()
            .Produces<MetronTestResponse>(200);

        // Auto-Match Settings
        group.MapGet("/automatch", GetAutoMatchSettings)
            .WithName("GetAutoMatchSettings")
            .WithDescription("Get auto-matching settings for import and series disambiguation")
            .WithOpenApi()
            .Produces<AutoMatchSettings>(200);

        group.MapPut("/automatch", UpdateAutoMatchSettings)
            .WithName("UpdateAutoMatchSettings")
            .WithDescription("Update auto-matching settings")
            .WithOpenApi()
            .Produces<AutoMatchSettings>(200);

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

        group.MapPut("/apikey/enabled", SetApiEnabled)
            .WithName("SetApiEnabled")
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

    private static async Task<IResult> GetCoverSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<CoverSettings>("covers", new CoverSettings(), cancellationToken)
            ?? new CoverSettings();

        return Results.Ok(new CoverCacheSettingsResponse
        {
            CacheDirectory = settings.CacheDirectory,
            RetentionDays = settings.RetentionDays,
            MaxCacheSizeMb = settings.MaxCacheSizeBytes / (1024 * 1024),
            CleanupTargetPercent = settings.CleanupTargetPercent,
            CleanupIntervalHours = settings.CleanupIntervalHours,
            AutoCleanupEnabled = settings.AutoCleanupEnabled,
            DefaultSize = settings.DefaultSize.ToString(),
            DownloadAllSizes = settings.DownloadAllSizes,
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads,
            DownloadTimeoutSeconds = settings.DownloadTimeoutSeconds,
            WarmCacheOnSeriesAdd = settings.WarmCacheOnSeriesAdd,
            WarmCacheSizes = settings.WarmCacheSizes,
            EnableRevalidation = settings.EnableRevalidation,
            RevalidationIntervalHours = settings.RevalidationIntervalHours
        });
    }

    private static async Task<IResult> UpdateCoverSettings(
        CoverCacheSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Validate max cache size (10MB - 10GB)
        if (request.MaxCacheSizeMb.HasValue && (request.MaxCacheSizeMb < 10 || request.MaxCacheSizeMb > 10240))
        {
            return Results.BadRequest(new { error = "MaxCacheSizeMb must be between 10 and 10240 (10GB)." });
        }

        // Validate cleanup target percent (50-95%)
        if (request.CleanupTargetPercent.HasValue && (request.CleanupTargetPercent < 50 || request.CleanupTargetPercent > 95))
        {
            return Results.BadRequest(new { error = "CleanupTargetPercent must be between 50 and 95." });
        }

        // Validate cleanup interval (0-168 hours = 1 week)
        if (request.CleanupIntervalHours.HasValue && (request.CleanupIntervalHours < 0 || request.CleanupIntervalHours > 168))
        {
            return Results.BadRequest(new { error = "CleanupIntervalHours must be between 0 and 168." });
        }

        // Validate retention days (0-365)
        if (request.RetentionDays.HasValue && (request.RetentionDays < 0 || request.RetentionDays > 365))
        {
            return Results.BadRequest(new { error = "RetentionDays must be between 0 and 365." });
        }

        // Get existing settings
        var settings = await settingsService.GetAsync<CoverSettings>("covers", new CoverSettings(), cancellationToken)
            ?? new CoverSettings();

        // Update only provided fields
        if (!string.IsNullOrEmpty(request.CacheDirectory))
            settings.CacheDirectory = request.CacheDirectory;
        if (request.RetentionDays.HasValue)
            settings.RetentionDays = request.RetentionDays.Value;
        if (request.MaxCacheSizeMb.HasValue)
            settings.MaxCacheSizeBytes = request.MaxCacheSizeMb.Value * 1024 * 1024;
        if (request.CleanupTargetPercent.HasValue)
            settings.CleanupTargetPercent = request.CleanupTargetPercent.Value;
        if (request.CleanupIntervalHours.HasValue)
            settings.CleanupIntervalHours = request.CleanupIntervalHours.Value;
        if (request.AutoCleanupEnabled.HasValue)
            settings.AutoCleanupEnabled = request.AutoCleanupEnabled.Value;
        if (!string.IsNullOrEmpty(request.DefaultSize))
        {
            if (Enum.TryParse<CoverSize>(request.DefaultSize, true, out var size))
                settings.DefaultSize = size;
        }
        if (request.DownloadAllSizes.HasValue)
            settings.DownloadAllSizes = request.DownloadAllSizes.Value;
        if (request.MaxConcurrentDownloads.HasValue)
            settings.MaxConcurrentDownloads = Math.Clamp(request.MaxConcurrentDownloads.Value, 1, 10);
        if (request.DownloadTimeoutSeconds.HasValue)
            settings.DownloadTimeoutSeconds = Math.Clamp(request.DownloadTimeoutSeconds.Value, 5, 120);
        if (request.WarmCacheOnSeriesAdd.HasValue)
            settings.WarmCacheOnSeriesAdd = request.WarmCacheOnSeriesAdd.Value;
        if (!string.IsNullOrEmpty(request.WarmCacheSizes))
            settings.WarmCacheSizes = request.WarmCacheSizes;
        if (request.EnableRevalidation.HasValue)
            settings.EnableRevalidation = request.EnableRevalidation.Value;
        if (request.RevalidationIntervalHours.HasValue)
            settings.RevalidationIntervalHours = Math.Clamp(request.RevalidationIntervalHours.Value, 0, 720); // Max 30 days

        // Save settings
        await settingsService.SetAsync("covers", settings, cancellationToken);

        return Results.Ok(new CoverCacheSettingsResponse
        {
            CacheDirectory = settings.CacheDirectory,
            RetentionDays = settings.RetentionDays,
            MaxCacheSizeMb = settings.MaxCacheSizeBytes / (1024 * 1024),
            CleanupTargetPercent = settings.CleanupTargetPercent,
            CleanupIntervalHours = settings.CleanupIntervalHours,
            AutoCleanupEnabled = settings.AutoCleanupEnabled,
            DefaultSize = settings.DefaultSize.ToString(),
            DownloadAllSizes = settings.DownloadAllSizes,
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads,
            DownloadTimeoutSeconds = settings.DownloadTimeoutSeconds,
            WarmCacheOnSeriesAdd = settings.WarmCacheOnSeriesAdd,
            WarmCacheSizes = settings.WarmCacheSizes,
            EnableRevalidation = settings.EnableRevalidation,
            RevalidationIntervalHours = settings.RevalidationIntervalHours
        });
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
        ILibraryOrganizationService organizationService,
        ILoggerFactory loggerFactory,
        ShortboxerrDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Get current settings to detect format changes
        var currentSettings = await settingsService.GetGeneralSettingsAsync(cancellationToken);
        
        var formatChanged = currentSettings.SeriesFolderFormat != request.SeriesFolderFormat ||
                           currentSettings.IssueFileFormat != request.IssueFileFormat ||
                           currentSettings.CollectionFileFormat != request.CollectionFileFormat;
        
        // Save the new settings
        await settingsService.SetGeneralSettingsAsync(request, cancellationToken);
        
        // Auto-organize if format changed and feature is enabled
        if (formatChanged && request.AutoOrganizeOnFormatChange)
        {
            var logger = loggerFactory.CreateLogger("Shortboxerr.Api.Endpoints.SettingsEndpoints");
            logger.LogInformation(
                "Library format settings changed and auto-organize is enabled. Triggering organization...");
            
            // Get all series IDs (excluding linked annuals)
            var seriesIds = await dbContext.Series
                .Where(s => !s.ParentSeriesId.HasValue)
                .Select(s => s.Id)
                .ToArrayAsync(cancellationToken);
            
            if (seriesIds.Length > 0)
            {
                // Execute organization in background to not block the settings save
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var results = await organizationService.ExecuteSeriesRenameAsync(seriesIds, dryRun: false, CancellationToken.None);
                        var renamed = results.Count(r => r.Success && r.FilesRenamed > 0);
                        var skipped = results.Count(r => r.Success && r.FilesRenamed == 0);
                        var failed = results.Count(r => !r.Success);
                        logger.LogInformation(
                            "Auto-organization complete: {Renamed} renamed, {Skipped} skipped, {Failed} failed",
                            renamed, skipped, failed);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Auto-organization failed");
                    }
                });
            }
        }
        
        return Results.Ok(request);
    }

    private static async Task<IResult> GetLoggingSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var logLevel = await settingsService.GetAsync("Logging:LogLevel", cancellationToken) ?? "Information";
        var logPath = await settingsService.GetAsync("Logging:LogPath", cancellationToken) ?? "";
        var maxFileSizeMb = int.TryParse(await settingsService.GetAsync("Logging:MaxFileSizeMb", cancellationToken), out var size) ? size : 10;
        var rotationFileCount = int.TryParse(await settingsService.GetAsync("Logging:RotationFileCount", cancellationToken), out var count) ? count : 5;
        var consoleLoggingEnabled = bool.TryParse(await settingsService.GetAsync("Logging:ConsoleLoggingEnabled", cancellationToken), out var console) && console;
        var sqlQueryLogging = bool.TryParse(await settingsService.GetAsync("Logging:SqlQueryLogging", cancellationToken), out var sql) && sql;
        var httpRequestBodyLogging = bool.TryParse(await settingsService.GetAsync("Logging:HttpRequestBodyLogging", cancellationToken), out var http) && http;
        var fullStackTraces = bool.TryParse(await settingsService.GetAsync("Logging:FullStackTraces", cancellationToken), out var stack) && stack;
        var retentionDays = int.TryParse(await settingsService.GetAsync("Logging:RetentionDays", cancellationToken), out var days) ? days : 30;
        var compressOldLogsStr = await settingsService.GetAsync("Logging:CompressOldLogs", cancellationToken);
        var compressOldLogs = string.IsNullOrEmpty(compressOldLogsStr) || bool.TryParse(compressOldLogsStr, out var compress) && compress;
        var compressLogsOlderThanDays = int.TryParse(await settingsService.GetAsync("Logging:CompressLogsOlderThanDays", cancellationToken), out var compressDays) ? compressDays : 1;

        // Get actual log path from environment or default
        var actualLogPath = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "shortboxerr", "logs");

        return Results.Ok(new LoggingSettings
        {
            LogLevel = logLevel,
            LogPath = string.IsNullOrEmpty(logPath) ? actualLogPath : logPath,
            MaxFileSizeMb = maxFileSizeMb,
            RotationFileCount = rotationFileCount,
            ConsoleLoggingEnabled = consoleLoggingEnabled,
            SqlQueryLogging = sqlQueryLogging,
            HttpRequestBodyLogging = httpRequestBodyLogging,
            FullStackTraces = fullStackTraces,
            RetentionDays = retentionDays,
            CompressOldLogs = compressOldLogs,
            CompressLogsOlderThanDays = compressLogsOlderThanDays
        });
    }

    private static async Task<IResult> UpdateLoggingSettings(
        LoggingSettings request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Validate log level
        var validLevels = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };
        if (!validLevels.Contains(request.LogLevel, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = $"Invalid log level. Must be one of: {string.Join(", ", validLevels)}" });
        }

        // Validate file size (1-100 MB)
        if (request.MaxFileSizeMb < 1 || request.MaxFileSizeMb > 100)
        {
            return Results.BadRequest(new { error = "MaxFileSizeMb must be between 1 and 100." });
        }

        // Validate rotation count (1-20)
        if (request.RotationFileCount < 1 || request.RotationFileCount > 20)
        {
            return Results.BadRequest(new { error = "RotationFileCount must be between 1 and 20." });
        }

        // Validate retention days (1-365)
        if (request.RetentionDays < 1 || request.RetentionDays > 365)
        {
            return Results.BadRequest(new { error = "RetentionDays must be between 1 and 365." });
        }

        // Validate compress logs older than days (1-30)
        if (request.CompressLogsOlderThanDays < 1 || request.CompressLogsOlderThanDays > 30)
        {
            return Results.BadRequest(new { error = "CompressLogsOlderThanDays must be between 1 and 30." });
        }

        await settingsService.SetAsync("Logging:LogLevel", request.LogLevel, cancellationToken);
        if (!string.IsNullOrEmpty(request.LogPath))
            await settingsService.SetAsync("Logging:LogPath", request.LogPath, cancellationToken);
        await settingsService.SetAsync("Logging:MaxFileSizeMb", request.MaxFileSizeMb.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:RotationFileCount", request.RotationFileCount.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:ConsoleLoggingEnabled", request.ConsoleLoggingEnabled.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:SqlQueryLogging", request.SqlQueryLogging.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:HttpRequestBodyLogging", request.HttpRequestBodyLogging.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:FullStackTraces", request.FullStackTraces.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:RetentionDays", request.RetentionDays.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:CompressOldLogs", request.CompressOldLogs.ToString(), cancellationToken);
        await settingsService.SetAsync("Logging:CompressLogsOlderThanDays", request.CompressLogsOlderThanDays.ToString(), cancellationToken);

        return Results.Ok(request);
    }

    private static async Task<IResult> TriggerLogCompression(
        Shortboxerr.Infrastructure.BackgroundServices.LogCompressionBackgroundService compressionService,
        CancellationToken cancellationToken)
    {
        var result = await compressionService.TriggerCompressionAsync(cancellationToken);
        return Results.Ok(new LogCompressionResponse
        {
            FilesCompressed = result.FilesCompressed,
            BytesSaved = result.BytesSaved
        });
    }

    private static async Task<IResult> GetMetronSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<MetronSettings>("metron", new MetronSettings(), cancellationToken)
            ?? new MetronSettings();

        return Results.Ok(new MetronSettingsResponse
        {
            Enabled = settings.Enabled,
            Username = settings.Username ?? "",
            HasPassword = !string.IsNullOrEmpty(settings.Password),
            CacheTtlHours = settings.CacheTtlHours,
            MinMatchConfidence = settings.MinMatchConfidence,
            TimeoutSeconds = MetronSettings.DefaultTimeoutSeconds,
            MaxRequestsPerMinute = MetronSettings.DefaultMaxRequestsPerMinute
        });
    }

    private static async Task<IResult> UpdateMetronSettings(
        MetronSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync<MetronSettings>("metron", new MetronSettings(), cancellationToken)
            ?? new MetronSettings();

        // Apply credential updates first (before checking if we can enable)
        if (!string.IsNullOrEmpty(request.Username))
            settings.Username = request.Username;
        if (!string.IsNullOrEmpty(request.Password))
            settings.Password = request.Password;
        if (request.CacheTtlHours.HasValue)
            settings.CacheTtlHours = Math.Clamp(request.CacheTtlHours.Value, 1, 168);
        if (request.MinMatchConfidence.HasValue)
            settings.MinMatchConfidence = Math.Clamp(request.MinMatchConfidence.Value, 50, 100);

        // Validate: cannot enable Metron without credentials
        if (request.Enabled == true)
        {
            var isConfigured = !string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password);
            if (!isConfigured)
            {
                return Results.BadRequest(new { error = "Cannot enable Metron without username and password configured" });
            }
        }

        if (request.Enabled.HasValue)
            settings.Enabled = request.Enabled.Value;

        // TimeoutSeconds and MaxRequestsPerMinute are hardcoded to Metron's limits (not user-configurable)

        await settingsService.SetAsync("metron", settings, cancellationToken);

        return Results.Ok(new MetronSettingsResponse
        {
            Enabled = settings.Enabled,
            Username = settings.Username ?? "",
            HasPassword = !string.IsNullOrEmpty(settings.Password),
            CacheTtlHours = settings.CacheTtlHours,
            MinMatchConfidence = settings.MinMatchConfidence,
            TimeoutSeconds = MetronSettings.DefaultTimeoutSeconds,
            MaxRequestsPerMinute = MetronSettings.DefaultMaxRequestsPerMinute
        });
    }

    private static async Task<IResult> TestMetronConnection(
        ISettingsService settingsService,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        // Load settings from database (not from DI-injected client which may have stale settings)
        var settings = await settingsService.GetAsync<MetronSettings>("metron", new MetronSettings(), cancellationToken)
            ?? new MetronSettings();

        if (!settings.IsConfigured)
        {
            return Results.Ok(new MetronTestResponse
            {
                Success = false,
                Message = "Metron credentials not configured. Please set username and password."
            });
        }

        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("https://metron.cloud/api/");
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Set Basic Auth header
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{settings.Username}:{settings.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shortboxerr/1.0");

            // Make a simple authenticated request to verify credentials
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            var response = await httpClient.GetAsync("publisher/?page_size=1", cts.Token);
            var isAvailable = response.IsSuccessStatusCode;

            return Results.Ok(new MetronTestResponse
            {
                Success = isAvailable,
                Message = isAvailable 
                    ? "Successfully connected to Metron API" 
                    : $"Failed to connect to Metron API. Status: {(int)response.StatusCode} {response.ReasonPhrase}"
            });
        }
        catch (TaskCanceledException)
        {
            return Results.Ok(new MetronTestResponse
            {
                Success = false,
                Message = "Connection timed out. Check network connectivity."
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Ok(new MetronTestResponse
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            });
        }
    }

    private static async Task<IResult> GetAutoMatchSettings(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAutoMatchSettingsAsync(cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateAutoMatchSettings(
        AutoMatchSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAutoMatchSettingsAsync(cancellationToken);

        // Validate and apply year tolerance (0-10)
        if (request.YearMatchTolerance.HasValue)
        {
            if (request.YearMatchTolerance < 0 || request.YearMatchTolerance > 10)
            {
                return Results.BadRequest(new { error = "YearMatchTolerance must be between 0 and 10." });
            }
            settings.YearMatchTolerance = request.YearMatchTolerance.Value;
        }

        // Validate and apply confidence threshold (50-100)
        if (request.ConfidenceThreshold.HasValue)
        {
            if (request.ConfidenceThreshold < 50 || request.ConfidenceThreshold > 100)
            {
                return Results.BadRequest(new { error = "ConfidenceThreshold must be between 50 and 100." });
            }
            settings.ConfidenceThreshold = request.ConfidenceThreshold.Value;
        }

        // Validate and apply year mismatch penalty (0-50)
        if (request.YearMismatchPenalty.HasValue)
        {
            if (request.YearMismatchPenalty < 0 || request.YearMismatchPenalty > 50)
            {
                return Results.BadRequest(new { error = "YearMismatchPenalty must be between 0 and 50." });
            }
            settings.YearMismatchPenalty = request.YearMismatchPenalty.Value;
        }

        // Validate and apply max candidates (1-20)
        if (request.MaxCandidatesForReview.HasValue)
        {
            if (request.MaxCandidatesForReview < 1 || request.MaxCandidatesForReview > 20)
            {
                return Results.BadRequest(new { error = "MaxCandidatesForReview must be between 1 and 20." });
            }
            settings.MaxCandidatesForReview = request.MaxCandidatesForReview.Value;
        }

        // Validate and apply publisher match bonus (0-50)
        if (request.PublisherMatchBonus.HasValue)
        {
            if (request.PublisherMatchBonus < 0 || request.PublisherMatchBonus > 50)
            {
                return Results.BadRequest(new { error = "PublisherMatchBonus must be between 0 and 50." });
            }
            settings.PublisherMatchBonus = request.PublisherMatchBonus.Value;
        }

        // Validate and apply publisher mismatch penalty (0-50)
        if (request.PublisherMismatchPenalty.HasValue)
        {
            if (request.PublisherMismatchPenalty < 0 || request.PublisherMismatchPenalty > 50)
            {
                return Results.BadRequest(new { error = "PublisherMismatchPenalty must be between 0 and 50." });
            }
            settings.PublisherMismatchPenalty = request.PublisherMismatchPenalty.Value;
        }

        // Apply boolean settings
        if (request.RejectMismatchedYears.HasValue)
            settings.RejectMismatchedYears = request.RejectMismatchedYears.Value;
        if (request.RequireYearForAmbiguousSeries.HasValue)
            settings.RequireYearForAmbiguousSeries = request.RequireYearForAmbiguousSeries.Value;
        if (request.EnableAmbiguousSeriesDetection.HasValue)
            settings.EnableAmbiguousSeriesDetection = request.EnableAmbiguousSeriesDetection.Value;
        if (request.PreferPublisherMatchForAmbiguous.HasValue)
            settings.PreferPublisherMatchForAmbiguous = request.PreferPublisherMatchForAmbiguous.Value;
        if (request.RejectMismatchedPublishers.HasValue)
            settings.RejectMismatchedPublishers = request.RejectMismatchedPublishers.Value;
        if (request.AutoMatchOnImport.HasValue)
            settings.AutoMatchOnImport = request.AutoMatchOnImport.Value;
        if (request.CreateMissingItems.HasValue)
            settings.CreateMissingItems = request.CreateMissingItems.Value;

        // Verification settings (EPIC 19.4)
        if (request.RequireConfirmationForFirstIssue.HasValue)
            settings.RequireConfirmationForFirstIssue = request.RequireConfirmationForFirstIssue.Value;
        if (request.LowConfidenceThreshold.HasValue)
        {
            if (request.LowConfidenceThreshold < 0 || request.LowConfidenceThreshold > 100)
            {
                return Results.BadRequest(new { error = "LowConfidenceThreshold must be between 0 and 100." });
            }
            settings.LowConfidenceThreshold = request.LowConfidenceThreshold.Value;
        }
        if (request.ShowMatchReasoning.HasValue)
            settings.ShowMatchReasoning = request.ShowMatchReasoning.Value;

        await settingsService.SetAutoMatchSettingsAsync(settings, cancellationToken);
        return Results.Ok(settings);
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
            IsEnabled = keyInfo.IsEnabled,
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
            IsEnabled = keyInfo.IsEnabled,
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
            IsEnabled = keyInfo.IsEnabled,
            MaskedKey = keyInfo.MaskedKey,
            FullKey = keyInfo.FullKey, // Return full key on regenerate
            CreatedAt = keyInfo.CreatedAt,
            LastUsedAt = keyInfo.LastUsedAt,
            IsNewKey = true
        });
    }

    private static async Task<IResult> SetApiEnabled(SetApiEnabledRequest request, ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var keyInfo = await settingsService.SetApiEnabledAsync(request.Enabled, cancellationToken);
        return Results.Ok(new ApiKeyResponse
        {
            IsEnabled = keyInfo.IsEnabled,
            MaskedKey = keyInfo.MaskedKey,
            FullKey = null,
            CreatedAt = keyInfo.CreatedAt,
            LastUsedAt = keyInfo.LastUsedAt
        });
    }

    // Denylist for the generic key-value settings endpoints.
    // Sensitive keys (security.*, metron, comicvine, providers, etc.) are blocked —
    // use their dedicated typed endpoints instead.
    private static readonly HashSet<string> DeniedGenericKeyPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Security:",
        "Metron:",
        "ComicVine:",
        "Providers:",
        "Api:",
    };

    private static bool IsAllowedGenericKey(string key)
    {
        // Block keys that start with sensitive prefixes
        foreach (var prefix in DeniedGenericKeyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static async Task<IResult> GetSetting(string key, ISettingsService settingsService, CancellationToken cancellationToken)
    {
        if (!IsAllowedGenericKey(key))
        {
            return Results.NotFound(new { error = $"Setting '{key}' not found." });
        }

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
        if (!IsAllowedGenericKey(key))
        {
            return Results.BadRequest(new { error = $"Setting '{key}' cannot be modified via this endpoint. Use the dedicated settings endpoint." });
        }

        await settingsService.SetAsync(key, request.Value, cancellationToken);
        return Results.Ok(new SettingResponse { Key = key, Value = request.Value });
    }

    private static async Task<IResult> DeleteSetting(string key, ISettingsService settingsService, CancellationToken cancellationToken)
    {
        if (!IsAllowedGenericKey(key))
        {
            return Results.NotFound(new { error = $"Setting '{key}' not found." });
        }

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

public class SetApiEnabledRequest
{
    public bool Enabled { get; set; }
}

public class ApiKeyResponse
{
    /// <summary>
    /// Whether API access is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

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

public class LoggingSettings
{
    /// <summary>
    /// Minimum log level: Verbose, Debug, Information, Warning, Error, Fatal
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Directory path where log files are stored
    /// </summary>
    public string LogPath { get; set; } = "";

    /// <summary>
    /// Maximum size of each log file in MB before rotation
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Number of rotated log files to keep
    /// </summary>
    public int RotationFileCount { get; set; } = 5;

    /// <summary>
    /// Whether to also log to console
    /// </summary>
    public bool ConsoleLoggingEnabled { get; set; } = true;

    /// <summary>
    /// Enable SQL query logging (debug feature)
    /// </summary>
    public bool SqlQueryLogging { get; set; }

    /// <summary>
    /// Enable HTTP request body logging (debug feature)
    /// </summary>
    public bool HttpRequestBodyLogging { get; set; }

    /// <summary>
    /// Enable full stack traces in error logs
    /// </summary>
    public bool FullStackTraces { get; set; }

    /// <summary>
    /// Number of days to retain log files before auto-cleanup
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether to compress old rotated log files
    /// </summary>
    public bool CompressOldLogs { get; set; } = true;

    /// <summary>
    /// Compress logs older than this many days
    /// </summary>
    public int CompressLogsOlderThanDays { get; set; } = 1;
}

public class CoverCacheSettingsRequest
{
    public string? CacheDirectory { get; set; }
    public int? RetentionDays { get; set; }
    public long? MaxCacheSizeMb { get; set; }
    public int? CleanupTargetPercent { get; set; }
    public int? CleanupIntervalHours { get; set; }
    public bool? AutoCleanupEnabled { get; set; }
    public string? DefaultSize { get; set; }
    public bool? DownloadAllSizes { get; set; }
    public int? MaxConcurrentDownloads { get; set; }
    public int? DownloadTimeoutSeconds { get; set; }
    public bool? WarmCacheOnSeriesAdd { get; set; }
    public string? WarmCacheSizes { get; set; }
    public bool? EnableRevalidation { get; set; }
    public int? RevalidationIntervalHours { get; set; }
}

public class CoverCacheSettingsResponse
{
    /// <summary>
    /// Directory where covers are cached.
    /// </summary>
    public string CacheDirectory { get; set; } = "covers";

    /// <summary>
    /// Number of days to keep cached covers (0 = indefinite).
    /// </summary>
    public int RetentionDays { get; set; } = 0;

    /// <summary>
    /// Maximum cache size in megabytes (0 = unlimited).
    /// </summary>
    public long MaxCacheSizeMb { get; set; } = 500;

    /// <summary>
    /// Target cache size after cleanup as percentage of max.
    /// </summary>
    public int CleanupTargetPercent { get; set; } = 80;

    /// <summary>
    /// Interval in hours for background cache cleanup (0 = disabled).
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether automatic cleanup is enabled.
    /// </summary>
    public bool AutoCleanupEnabled { get; set; } = true;

    /// <summary>
    /// Default size to download (Thumb, Small, Medium, Large).
    /// </summary>
    public string DefaultSize { get; set; } = "Medium";

    /// <summary>
    /// Whether to download all sizes when fetching a cover.
    /// </summary>
    public bool DownloadAllSizes { get; set; } = false;

    /// <summary>
    /// Maximum concurrent downloads.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Timeout for cover downloads in seconds.
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to automatically warm cache when a series is added.
    /// </summary>
    public bool WarmCacheOnSeriesAdd { get; set; } = false;

    /// <summary>
    /// Comma-separated list of sizes to warm (e.g., "Medium,Thumb").
    /// </summary>
    public string WarmCacheSizes { get; set; } = "Medium";

    /// <summary>
    /// Whether to use ETag/Last-Modified for efficient revalidation.
    /// </summary>
    public bool EnableRevalidation { get; set; } = true;

    /// <summary>
    /// Hours between revalidation checks.
    /// </summary>
    public int RevalidationIntervalHours { get; set; } = 168;
}

/// <summary>
/// Response from log compression operation.
/// </summary>
public class LogCompressionResponse
{
    /// <summary>
    /// Number of log files that were compressed.
    /// </summary>
    public int FilesCompressed { get; set; }

    /// <summary>
    /// Total bytes saved by compression.
    /// </summary>
    public long BytesSaved { get; set; }
}

/// <summary>
/// Request to update Metron settings.
/// </summary>
public class MetronSettingsRequest
{
    /// <summary>
    /// Whether Metron integration is enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Metron username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Metron password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Cache TTL in hours (1-168).
    /// </summary>
    public int? CacheTtlHours { get; set; }

    /// <summary>
    /// Minimum confidence (50-100) for ID-less Metron issue matching.
    /// Lower values increase recall but risk mismatched covers.
    /// </summary>
    public int? MinMatchConfidence { get; set; }
    
    // Note: TimeoutSeconds and MaxRequestsPerMinute are hardcoded to Metron's limits
    // and are not configurable via API to prevent exceeding rate limits.
}

/// <summary>
/// Response containing Metron settings.
/// </summary>
public class MetronSettingsResponse
{
    /// <summary>
    /// Whether Metron integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Metron username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Whether a password is configured (never returns actual password).
    /// </summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Cache TTL in hours.
    /// </summary>
    public int CacheTtlHours { get; set; }

    /// <summary>
    /// Minimum confidence (50-100) for ID-less Metron issue matching.
    /// </summary>
    public int MinMatchConfidence { get; set; }

    /// <summary>
    /// Request timeout in seconds (read-only, hardcoded to Metron's default).
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum requests per minute (read-only, hardcoded to Metron's API limit).
    /// </summary>
    public int MaxRequestsPerMinute { get; set; }
}

/// <summary>
/// Response from Metron connection test.
/// </summary>
public class MetronTestResponse
{
    /// <summary>
    /// Whether the connection test was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message describing the test result.
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
/// Request to update auto-match settings.
/// All fields are optional - only provided values are updated.
/// </summary>
public class AutoMatchSettingsRequest
{
    // === Year Matching ===
    
    /// <summary>
    /// Maximum year difference allowed between release and series (0-10).
    /// </summary>
    public int? YearMatchTolerance { get; set; }

    /// <summary>
    /// If true, reject matches where year mismatch exceeds tolerance.
    /// </summary>
    public bool? RejectMismatchedYears { get; set; }

    /// <summary>
    /// Penalty applied to confidence when year doesn't match (0-50).
    /// </summary>
    public int? YearMismatchPenalty { get; set; }

    // === Confidence ===

    /// <summary>
    /// Confidence threshold for auto-accepting matches (50-100).
    /// </summary>
    public int? ConfidenceThreshold { get; set; }

    // === Ambiguity Detection ===

    /// <summary>
    /// Require year when multiple series share the same name.
    /// </summary>
    public bool? RequireYearForAmbiguousSeries { get; set; }

    /// <summary>
    /// Enable detection of ambiguous series (multiple with same name).
    /// </summary>
    public bool? EnableAmbiguousSeriesDetection { get; set; }

    // === Publisher Matching (EPIC 19.2) ===

    /// <summary>
    /// Bonus applied when publisher matches (0-50).
    /// </summary>
    public int? PublisherMatchBonus { get; set; }

    /// <summary>
    /// Penalty applied when publisher doesn't match (0-50).
    /// </summary>
    public int? PublisherMismatchPenalty { get; set; }

    /// <summary>
    /// When ambiguous, prefer series with matching publisher.
    /// </summary>
    public bool? PreferPublisherMatchForAmbiguous { get; set; }

    /// <summary>
    /// If true, reject matches when publishers don't match.
    /// </summary>
    public bool? RejectMismatchedPublishers { get; set; }

    // === Verification Settings (EPIC 19.4) ===

    /// <summary>
    /// Require manual confirmation for first issue of any series.
    /// </summary>
    public bool? RequireConfirmationForFirstIssue { get; set; }

    /// <summary>
    /// Threshold below which matches are considered low confidence (0-100).
    /// </summary>
    public int? LowConfidenceThreshold { get; set; }

    /// <summary>
    /// Show detailed match reasoning in import queue UI.
    /// </summary>
    public bool? ShowMatchReasoning { get; set; }

    // === Import Behavior ===

    /// <summary>
    /// Whether to auto-match during import.
    /// </summary>
    public bool? AutoMatchOnImport { get; set; }

    /// <summary>
    /// Whether to create series/issues if not found locally.
    /// </summary>
    public bool? CreateMissingItems { get; set; }

    /// <summary>
    /// Maximum candidates to keep for manual review (1-20).
    /// </summary>
    public int? MaxCandidatesForReview { get; set; }
}
