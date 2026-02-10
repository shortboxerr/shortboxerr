namespace Shortboxerr.Core.Search;

/// <summary>
/// Service for managing search configuration settings.
/// </summary>
public interface ISearchSettingsService
{
    /// <summary>
    /// Gets the current search settings.
    /// </summary>
    Task<SearchSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates search settings.
    /// </summary>
    Task SaveSettingsAsync(SearchSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets settings to defaults.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates settings and returns any errors.
    /// </summary>
    IReadOnlyList<string> ValidateSettings(SearchSettings settings);
}
