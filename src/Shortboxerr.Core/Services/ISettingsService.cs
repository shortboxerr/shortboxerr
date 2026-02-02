namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for managing application settings stored in key-value format.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a setting value by key, returning default if not found.
    /// </summary>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a setting value with JSON serialization.
    /// </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all settings with a given prefix.
    /// </summary>
    Task<IDictionary<string, string>> GetAllAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a setting by key.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets UI settings.
    /// </summary>
    Task<UiSettings> GetUiSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets UI settings.
    /// </summary>
    Task SetUiSettingsAsync(UiSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets general settings.
    /// </summary>
    Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets general settings.
    /// </summary>
    Task SetGeneralSettingsAsync(GeneralSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// UI-specific settings (theme, display preferences).
/// </summary>
public class UiSettings
{
    /// <summary>
    /// Theme preference: "dark", "light", or "system"
    /// </summary>
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// Number of items to show per page in tables.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Whether to show file sizes in tables.
    /// </summary>
    public bool ShowFileSizes { get; set; } = true;

    /// <summary>
    /// Whether to show relative timestamps (e.g., "2 hours ago").
    /// </summary>
    public bool RelativeTimestamps { get; set; } = true;
}

/// <summary>
/// General application settings.
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Pattern for organizing series folders.
    /// </summary>
    public string SeriesFolderFormat { get; set; } = "{Series Title} ({Year})";

    /// <summary>
    /// Pattern for naming issue files.
    /// </summary>
    public string IssueFileFormat { get; set; } = "{Series Title} #{Issue} ({Year})";

    /// <summary>
    /// Pattern for naming collection files.
    /// </summary>
    public string CollectionFileFormat { get; set; } = "{Series Title} - {Edition Type} Vol. {Volume} ({Year})";

    /// <summary>
    /// Root path for the comic library.
    /// </summary>
    public string ComicLibraryPath { get; set; } = "/comics";

    /// <summary>
    /// Path where downloaded files are placed.
    /// </summary>
    public string DownloadFolder { get; set; } = "/downloads";

    /// <summary>
    /// Path where files are staged for import review.
    /// </summary>
    public string StagingFolder { get; set; } = "/staging";

    /// <summary>
    /// Whether to auto-move completed downloads to staging.
    /// </summary>
    public bool AutoMoveToStaging { get; set; } = true;
}

