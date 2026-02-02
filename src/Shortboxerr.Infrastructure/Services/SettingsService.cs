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
        WriteIndented = false
    };

    // Setting key prefixes/names
    private const string UiThemeKey = "ui.theme";
    private const string UiPageSizeKey = "ui.pageSize";
    private const string UiShowFileSizesKey = "ui.showFileSizes";
    private const string UiRelativeTimestampsKey = "ui.relativeTimestamps";
    private const string GeneralSeriesFolderFormatKey = "general.seriesFolderFormat";
    private const string GeneralIssueFileFormatKey = "general.issueFileFormat";
    private const string GeneralCollectionFileFormatKey = "general.collectionFileFormat";
    private const string GeneralComicLibraryPathKey = "general.comicLibraryPath";
    private const string GeneralDownloadFolderKey = "general.downloadFolder";
    private const string GeneralStagingFolderKey = "general.stagingFolder";
    private const string GeneralAutoMoveToStagingKey = "general.autoMoveToStaging";

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
            RelativeTimestamps = await GetAsync<bool>(UiRelativeTimestampsKey, defaults.RelativeTimestamps, cancellationToken)
        };
    }

    public async Task SetUiSettingsAsync(UiSettings settings, CancellationToken cancellationToken = default)
    {
        await SetAsync(UiThemeKey, settings.Theme, cancellationToken);
        await SetAsync<int>(UiPageSizeKey, settings.PageSize, cancellationToken);
        await SetAsync<bool>(UiShowFileSizesKey, settings.ShowFileSizes, cancellationToken);
        await SetAsync<bool>(UiRelativeTimestampsKey, settings.RelativeTimestamps, cancellationToken);
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
}

