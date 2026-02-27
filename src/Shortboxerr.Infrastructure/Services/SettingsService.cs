using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Implementation of ISettingsService that persists settings to the database.
/// Automatically encrypts/decrypts properties marked with [SensitiveCredential].
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ShortboxerrDbContext _context;
    private readonly ICredentialEncryptionService _encryptionService;
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

    // Auto-match settings
    private const string AutoMatchYearToleranceKey = "automatch.yearTolerance";
    private const string AutoMatchRejectMismatchedYearsKey = "automatch.rejectMismatchedYears";
    private const string AutoMatchMinConfidenceKey = "automatch.minConfidenceForAutoImport";
    private const string AutoMatchRequireYearForAmbiguousKey = "automatch.requireYearForAmbiguousSeries";
    private const string AutoMatchEnableAmbiguousDetectionKey = "automatch.enableAmbiguousSeriesDetection";
    private const string AutoMatchYearMismatchPenaltyKey = "automatch.yearMismatchPenalty";
    
    // Publisher matching settings (EPIC 19.2)
    private const string AutoMatchPublisherBonusKey = "automatch.publisherMatchBonus";
    private const string AutoMatchPublisherPenaltyKey = "automatch.publisherMismatchPenalty";
    private const string AutoMatchPreferPublisherKey = "automatch.preferPublisherMatchForAmbiguous";
    private const string AutoMatchRejectMismatchedPublishersKey = "automatch.rejectMismatchedPublishers";
    
    // Verification settings (EPIC 19.4)
    private const string AutoMatchRequireFirstIssueKey = "automatch.requireConfirmationForFirstIssue";
    private const string AutoMatchLowConfidenceThresholdKey = "automatch.lowConfidenceThreshold";
    private const string AutoMatchShowReasoningKey = "automatch.showMatchReasoning";

    public SettingsService(ShortboxerrDbContext context, ICredentialEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
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
            var result = JsonSerializer.Deserialize<T>(value, JsonOptions);
            
            // Decrypt sensitive credential fields
            if (result != null)
                DecryptSensitiveFields(result);
            
            return result;
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
        {
            // Create a copy to avoid modifying the original object
            var valueCopy = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions);
            
            // Encrypt sensitive credential fields before storing
            if (valueCopy != null)
                EncryptSensitiveFields(valueCopy);
            
            stringValue = JsonSerializer.Serialize(valueCopy, JsonOptions);
        }

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

    public async Task<AutoMatchSettings> GetAutoMatchSettingsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new AutoMatchSettings();

        return new AutoMatchSettings
        {
            // Year matching
            YearMatchTolerance = await GetAsync<int>(AutoMatchYearToleranceKey, defaults.YearMatchTolerance, cancellationToken),
            RejectMismatchedYears = await GetAsync<bool>(AutoMatchRejectMismatchedYearsKey, defaults.RejectMismatchedYears, cancellationToken),
            YearMismatchPenalty = await GetAsync<int>(AutoMatchYearMismatchPenaltyKey, defaults.YearMismatchPenalty, cancellationToken),
            
            // Confidence
            MinConfidenceForAutoImport = await GetAsync<int>(AutoMatchMinConfidenceKey, defaults.MinConfidenceForAutoImport, cancellationToken),
            
            // Ambiguity detection
            RequireYearForAmbiguousSeries = await GetAsync<bool>(AutoMatchRequireYearForAmbiguousKey, defaults.RequireYearForAmbiguousSeries, cancellationToken),
            EnableAmbiguousSeriesDetection = await GetAsync<bool>(AutoMatchEnableAmbiguousDetectionKey, defaults.EnableAmbiguousSeriesDetection, cancellationToken),
            
            // Publisher matching (EPIC 19.2)
            PublisherMatchBonus = await GetAsync<int>(AutoMatchPublisherBonusKey, defaults.PublisherMatchBonus, cancellationToken),
            PublisherMismatchPenalty = await GetAsync<int>(AutoMatchPublisherPenaltyKey, defaults.PublisherMismatchPenalty, cancellationToken),
            PreferPublisherMatchForAmbiguous = await GetAsync<bool>(AutoMatchPreferPublisherKey, defaults.PreferPublisherMatchForAmbiguous, cancellationToken),
            RejectMismatchedPublishers = await GetAsync<bool>(AutoMatchRejectMismatchedPublishersKey, defaults.RejectMismatchedPublishers, cancellationToken),
            
            // Verification settings (EPIC 19.4)
            RequireConfirmationForFirstIssue = await GetAsync<bool>(AutoMatchRequireFirstIssueKey, defaults.RequireConfirmationForFirstIssue, cancellationToken),
            LowConfidenceThreshold = await GetAsync<int>(AutoMatchLowConfidenceThresholdKey, defaults.LowConfidenceThreshold, cancellationToken),
            ShowMatchReasoning = await GetAsync<bool>(AutoMatchShowReasoningKey, defaults.ShowMatchReasoning, cancellationToken)
        };
    }

    public async Task SetAutoMatchSettingsAsync(AutoMatchSettings settings, CancellationToken cancellationToken = default)
    {
        // Year matching
        await SetAsync<int>(AutoMatchYearToleranceKey, settings.YearMatchTolerance, cancellationToken);
        await SetAsync<bool>(AutoMatchRejectMismatchedYearsKey, settings.RejectMismatchedYears, cancellationToken);
        await SetAsync<int>(AutoMatchYearMismatchPenaltyKey, settings.YearMismatchPenalty, cancellationToken);
        
        // Confidence
        await SetAsync<int>(AutoMatchMinConfidenceKey, settings.MinConfidenceForAutoImport, cancellationToken);
        
        // Ambiguity detection
        await SetAsync<bool>(AutoMatchRequireYearForAmbiguousKey, settings.RequireYearForAmbiguousSeries, cancellationToken);
        await SetAsync<bool>(AutoMatchEnableAmbiguousDetectionKey, settings.EnableAmbiguousSeriesDetection, cancellationToken);
        
        // Publisher matching (EPIC 19.2)
        await SetAsync<int>(AutoMatchPublisherBonusKey, settings.PublisherMatchBonus, cancellationToken);
        await SetAsync<int>(AutoMatchPublisherPenaltyKey, settings.PublisherMismatchPenalty, cancellationToken);
        await SetAsync<bool>(AutoMatchPreferPublisherKey, settings.PreferPublisherMatchForAmbiguous, cancellationToken);
        await SetAsync<bool>(AutoMatchRejectMismatchedPublishersKey, settings.RejectMismatchedPublishers, cancellationToken);
        
        // Verification settings (EPIC 19.4)
        await SetAsync<bool>(AutoMatchRequireFirstIssueKey, settings.RequireConfirmationForFirstIssue, cancellationToken);
        await SetAsync<int>(AutoMatchLowConfidenceThresholdKey, settings.LowConfidenceThreshold, cancellationToken);
        await SetAsync<bool>(AutoMatchShowReasoningKey, settings.ShowMatchReasoning, cancellationToken);
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

    /// <summary>
    /// Encrypts all string properties marked with [SensitiveCredential] attribute.
    /// Recursively processes nested objects.
    /// </summary>
    private void EncryptSensitiveFields<T>(T obj)
    {
        if (obj == null) return;
        EncryptSensitiveFieldsRecursive(obj, obj.GetType(), new HashSet<object>());
    }

    private void EncryptSensitiveFieldsRecursive(object obj, Type type, HashSet<object> visited)
    {
        if (obj == null || !visited.Add(obj)) return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            // Handle string properties with [SensitiveCredential]
            if (property.PropertyType == typeof(string) && 
                property.GetCustomAttribute<SensitiveCredentialAttribute>() != null)
            {
                var value = property.GetValue(obj) as string;
                if (!string.IsNullOrEmpty(value) && !_encryptionService.IsEncrypted(value))
                {
                    var encrypted = _encryptionService.Encrypt(value);
                    property.SetValue(obj, encrypted);
                }
            }
            // Recursively process nested objects (non-primitive, non-string, non-collection class types)
            else if (property.PropertyType.IsClass && 
                     property.PropertyType != typeof(string) &&
                     !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                var nestedObj = property.GetValue(obj);
                if (nestedObj != null)
                {
                    EncryptSensitiveFieldsRecursive(nestedObj, property.PropertyType, visited);
                }
            }
        }
    }

    /// <summary>
    /// Decrypts all string properties marked with [SensitiveCredential] attribute.
    /// Recursively processes nested objects.
    /// </summary>
    private void DecryptSensitiveFields<T>(T obj)
    {
        if (obj == null) return;
        DecryptSensitiveFieldsRecursive(obj, obj.GetType(), new HashSet<object>());
    }

    private void DecryptSensitiveFieldsRecursive(object obj, Type type, HashSet<object> visited)
    {
        if (obj == null || !visited.Add(obj)) return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            // Handle string properties with [SensitiveCredential]
            if (property.PropertyType == typeof(string) && 
                property.GetCustomAttribute<SensitiveCredentialAttribute>() != null)
            {
                var value = property.GetValue(obj) as string;
                if (!string.IsNullOrEmpty(value) && _encryptionService.IsEncrypted(value))
                {
                    try
                    {
                        var decrypted = _encryptionService.Decrypt(value);
                        property.SetValue(obj, decrypted);
                    }
                    catch
                    {
                        // If decryption fails (e.g., migrated from different machine),
                        // leave the value as-is - it will need to be re-entered
                    }
                }
            }
            // Recursively process nested objects (non-primitive, non-string, non-collection class types)
            else if (property.PropertyType.IsClass && 
                     property.PropertyType != typeof(string) &&
                     !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                var nestedObj = property.GetValue(obj);
                if (nestedObj != null)
                {
                    DecryptSensitiveFieldsRecursive(nestedObj, property.PropertyType, visited);
                }
            }
        }
    }
}

