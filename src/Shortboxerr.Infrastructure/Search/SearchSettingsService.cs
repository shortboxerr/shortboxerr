using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Search;

/// <summary>
/// Service for managing search configuration settings.
/// Persists settings via ISettingsService.
/// </summary>
public class SearchSettingsService : ISearchSettingsService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SearchSettingsService>? _logger;
    private SearchSettings? _cachedSettings;

    public SearchSettingsService(ISettingsService settingsService, ILogger<SearchSettingsService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<SearchSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        var settings = await _settingsService.GetAsync<SearchSettings>(
            SearchSettings.SettingsKey,
            SearchSettings.Default,
            cancellationToken);

        _cachedSettings = settings ?? SearchSettings.Default;
        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(SearchSettings settings, CancellationToken cancellationToken = default)
    {
        var errors = ValidateSettings(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid settings: {string.Join(", ", errors)}");
        }

        await _settingsService.SetAsync(SearchSettings.SettingsKey, settings, cancellationToken);
        _cachedSettings = settings;

        _logger?.LogInformation("Search settings saved successfully");
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaults = SearchSettings.Default;
        await SaveSettingsAsync(defaults, cancellationToken);
        _logger?.LogInformation("Search settings reset to defaults");
    }

    public IReadOnlyList<string> ValidateSettings(SearchSettings settings)
    {
        var errors = new List<string>();

        // Validate delay
        if (settings.SearchDelaySeconds < 0)
        {
            errors.Add("Search delay cannot be negative");
        }

        // Validate size limits
        if (settings.MinSizeMb < 0)
        {
            errors.Add("Minimum size cannot be negative");
        }

        if (settings.MaxSizeMb < 0)
        {
            errors.Add("Maximum size cannot be negative");
        }

        if (settings.MinSizeMb > 0 && settings.MaxSizeMb > 0 && settings.MinSizeMb > settings.MaxSizeMb)
        {
            errors.Add("Minimum size cannot be greater than maximum size");
        }

        if (settings.MinSizePackMb < 0)
        {
            errors.Add("Minimum pack size cannot be negative");
        }

        if (settings.MaxSizePackMb < 0)
        {
            errors.Add("Maximum pack size cannot be negative");
        }

        if (settings.MinSizePackMb > 0 && settings.MaxSizePackMb > 0 && settings.MinSizePackMb > settings.MaxSizePackMb)
        {
            errors.Add("Minimum pack size cannot be greater than maximum pack size");
        }

        // Validate automation settings
        if (settings.AutoSearchIntervalHours < 1)
        {
            errors.Add("Auto-search interval must be at least 1 hour");
        }

        if (settings.StaleSearchThresholdDays < 0)
        {
            errors.Add("Stale search threshold cannot be negative");
        }

        // Validate format preference
        if (settings.FormatPreference.Count == 0)
        {
            errors.Add("At least one format preference must be specified");
        }

        // Validate search tier cutoff
        if (settings.SearchTierCutoff < 0)
        {
            errors.Add("Search tier cutoff cannot be negative");
        }

        // Validate max results
        if (settings.MaxResultsPerProvider < 1)
        {
            errors.Add("Max results per provider must be at least 1");
        }

        return errors;
    }
}
