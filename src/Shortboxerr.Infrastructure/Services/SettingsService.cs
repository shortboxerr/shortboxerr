using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Implementation of ISettingsService that persists settings to the database.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ShortboxerrDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        // Include null values in serialization (important for nullable settings like EnableSeriesAnnualIntegration)
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    // Setting key prefixes/names
    private const string UiThemeKey = "ui.theme";
    private const string UiPageSizeKey = "ui.pageSize";
    private const string UiShowFileSizesKey = "ui.showFileSizes";
    private const string UiRelativeTimestampsKey = "ui.relativeTimestamps";
    private const string UiIssueViewModeKey = "ui.issueViewMode";
    private const string UiPullListDisplayModeKey = "ui.pullListDisplayMode";
    private const string GeneralSeriesFolderFormatKey = "general.seriesFolderFormat";
    private const string GeneralIssueFileFormatKey = "general.issueFileFormat";
    private const string GeneralCollectionFileFormatKey = "general.collectionFileFormat";
    private const string GeneralComicLibraryPathKey = "general.comicLibraryPath";
    private const string GeneralDownloadFolderKey = "general.downloadFolder";
    private const string GeneralStagingFolderKey = "general.stagingFolder";
    private const string GeneralAutoMoveToStagingKey = "general.autoMoveToStaging";
    
    // API key settings
    private const string ApiKeyValueKey = "security.apiKey";
    private const string ApiKeyCreatedAtKey = "security.apiKeyCreatedAt";
    private const string ApiKeyLastUsedAtKey = "security.apiKeyLastUsedAt";
    private const string ApiKeyEnabledKey = "security.apiKeyEnabled";

    public SettingsService(ShortboxerrDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return setting?.Value;
    }

    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        try
        {
            // Handle simple types
            if (typeof(T) == typeof(string))
                return (T)(object)value;
            if (typeof(T) == typeof(int))
                return int.TryParse(value, out var intVal) ? (T)(object)intVal : defaultValue;
            if (typeof(T) == typeof(bool))
                return bool.TryParse(value, out var boolVal) ? (T)(object)boolVal : defaultValue;

            // Handle complex types with JSON deserialization
            return JsonSerializer.Deserialize<T>(value, JsonOptions);
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        string stringValue;

        // Handle simple types
        if (typeof(T) == typeof(string))
            stringValue = (string)(object)value!;
        else if (typeof(T) == typeof(int) || typeof(T) == typeof(bool))
            stringValue = value?.ToString() ?? "";
        else
            stringValue = JsonSerializer.Serialize(value, JsonOptions);

        await SetAsync(key, stringValue, cancellationToken);
    }

    public async Task<IDictionary<string, string>> GetAllAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemSettings.AsNoTracking();

        if (!string.IsNullOrEmpty(prefix))
            query = query.Where(s => s.Key.StartsWith(prefix));

        var settings = await query.ToListAsync(cancellationToken);
        return settings.ToDictionary(s => s.Key, s => s.Value ?? "");
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting != null)
        {
            _context.SystemSettings.Remove(setting);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UiSettings> GetUiSettingsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new UiSettings();

        return new UiSettings
        {
            Theme = await GetAsync(UiThemeKey, cancellationToken) ?? defaults.Theme,
            PageSize = await GetAsync<int>(UiPageSizeKey, defaults.PageSize, cancellationToken),
            ShowFileSizes = await GetAsync<bool>(UiShowFileSizesKey, defaults.ShowFileSizes, cancellationToken),
            RelativeTimestamps = await GetAsync<bool>(UiRelativeTimestampsKey, defaults.RelativeTimestamps, cancellationToken),
            IssueViewMode = await GetAsync(UiIssueViewModeKey, cancellationToken) ?? defaults.IssueViewMode,
            PullListDisplayMode = await GetAsync(UiPullListDisplayModeKey, cancellationToken) ?? defaults.PullListDisplayMode
        };
    }

    public async Task SetUiSettingsAsync(UiSettings settings, CancellationToken cancellationToken = default)
    {
        await SetAsync(UiThemeKey, settings.Theme, cancellationToken);
        await SetAsync<int>(UiPageSizeKey, settings.PageSize, cancellationToken);
        await SetAsync<bool>(UiShowFileSizesKey, settings.ShowFileSizes, cancellationToken);
        await SetAsync<bool>(UiRelativeTimestampsKey, settings.RelativeTimestamps, cancellationToken);
        await SetAsync(UiIssueViewModeKey, settings.IssueViewMode, cancellationToken);
        await SetAsync(UiPullListDisplayModeKey, settings.PullListDisplayMode, cancellationToken);
    }

    public async Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new GeneralSettings();

        return new GeneralSettings
        {
            SeriesFolderFormat = await GetAsync(GeneralSeriesFolderFormatKey, cancellationToken) ?? defaults.SeriesFolderFormat,
            IssueFileFormat = await GetAsync(GeneralIssueFileFormatKey, cancellationToken) ?? defaults.IssueFileFormat,
            CollectionFileFormat = await GetAsync(GeneralCollectionFileFormatKey, cancellationToken) ?? defaults.CollectionFileFormat,
            ComicLibraryPath = await GetAsync(GeneralComicLibraryPathKey, cancellationToken) ?? defaults.ComicLibraryPath,
            DownloadFolder = await GetAsync(GeneralDownloadFolderKey, cancellationToken) ?? defaults.DownloadFolder,
            StagingFolder = await GetAsync(GeneralStagingFolderKey, cancellationToken) ?? defaults.StagingFolder,
            AutoMoveToStaging = await GetAsync<bool>(GeneralAutoMoveToStagingKey, defaults.AutoMoveToStaging, cancellationToken)
        };
    }

    public async Task SetGeneralSettingsAsync(GeneralSettings settings, CancellationToken cancellationToken = default)
    {
        await SetAsync(GeneralSeriesFolderFormatKey, settings.SeriesFolderFormat, cancellationToken);
        await SetAsync(GeneralIssueFileFormatKey, settings.IssueFileFormat, cancellationToken);
        await SetAsync(GeneralCollectionFileFormatKey, settings.CollectionFileFormat, cancellationToken);
        await SetAsync(GeneralComicLibraryPathKey, settings.ComicLibraryPath, cancellationToken);
        await SetAsync(GeneralDownloadFolderKey, settings.DownloadFolder, cancellationToken);
        await SetAsync(GeneralStagingFolderKey, settings.StagingFolder, cancellationToken);
        await SetAsync<bool>(GeneralAutoMoveToStagingKey, settings.AutoMoveToStaging, cancellationToken);
    }

    public async Task<ApiKeyInfo> GetApiKeyAsync(bool includeFull = false, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetAsync(ApiKeyValueKey, cancellationToken);
        var isEnabled = await GetAsync<bool>(ApiKeyEnabledKey, true, cancellationToken); // Default to enabled
        
        // Generate a new key if none exists (auto-generate on first access)
        if (string.IsNullOrEmpty(apiKey))
        {
            var newKeyInfo = await RegenerateApiKeyAsync(cancellationToken);
            newKeyInfo.IsEnabled = isEnabled;
            return newKeyInfo;
        }

        var createdAtStr = await GetAsync(ApiKeyCreatedAtKey, cancellationToken);
        var lastUsedAtStr = await GetAsync(ApiKeyLastUsedAtKey, cancellationToken);

        return new ApiKeyInfo
        {
            IsEnabled = isEnabled,
            MaskedKey = MaskApiKey(apiKey),
            FullKey = includeFull ? apiKey : null,
            CreatedAt = DateTime.TryParse(createdAtStr, out var createdAt) ? createdAt : DateTime.UtcNow,
            LastUsedAt = DateTime.TryParse(lastUsedAtStr, out var lastUsed) ? lastUsed : null
        };
    }

    public async Task<ApiKeyInfo> RegenerateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var newKey = GenerateApiKey();
        var createdAt = DateTime.UtcNow;
        var isEnabled = await GetAsync<bool>(ApiKeyEnabledKey, true, cancellationToken);

        await SetAsync(ApiKeyValueKey, newKey, cancellationToken);
        await SetAsync(ApiKeyCreatedAtKey, createdAt.ToString("O"), cancellationToken);
        await DeleteAsync(ApiKeyLastUsedAtKey, cancellationToken); // Reset last used

        return new ApiKeyInfo
        {
            IsEnabled = isEnabled,
            MaskedKey = MaskApiKey(newKey),
            FullKey = newKey, // Return full key on regenerate so user can copy it
            CreatedAt = createdAt,
            LastUsedAt = null
        };
    }

    public async Task<ApiKeyInfo> SetApiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await SetAsync<bool>(ApiKeyEnabledKey, enabled, cancellationToken);
        return await GetApiKeyAsync(includeFull: false, cancellationToken);
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apiKey))
            return false;

        var storedKey = await GetAsync(ApiKeyValueKey, cancellationToken);
        if (string.IsNullOrEmpty(storedKey))
            return false;

        var isValid = string.Equals(apiKey, storedKey, StringComparison.Ordinal);

        if (isValid)
        {
            // Update last used timestamp
            await SetAsync(ApiKeyLastUsedAtKey, DateTime.UtcNow.ToString("O"), cancellationToken);
        }

        return isValid;
    }

    /// <summary>
    /// Generates a cryptographically secure API key.
    /// Format: sk_live_{32 random hex chars}
    /// </summary>
    private static string GenerateApiKey()
    {
        var bytes = new byte[16]; // 16 bytes = 32 hex characters
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"sk_live_{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    /// <summary>
    /// Masks an API key, showing only first 7 and last 4 characters.
    /// Example: sk_live_abc...wxyz
    /// </summary>
    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 12)
            return "****";

        // Show "sk_live_" prefix (8 chars) and last 4 chars
        var prefix = apiKey[..8];
        var suffix = apiKey[^4..];
        return $"{prefix}...{suffix}";
    }
}

