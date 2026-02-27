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

    /// <summary>
    /// Gets the API key info (masked by default).
    /// </summary>
    Task<ApiKeyInfo> GetApiKeyAsync(bool includeFull = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerates the API key.
    /// </summary>
    Task<ApiKeyInfo> RegenerateApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an API key.
    /// </summary>
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets whether API access is enabled.
    /// </summary>
    Task<ApiKeyInfo> SetApiEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets auto-match settings.
    /// </summary>
    Task<AutoMatchSettings> GetAutoMatchSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets auto-match settings.
    /// </summary>
    Task SetAutoMatchSettingsAsync(AutoMatchSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// API key information.
/// </summary>
public class ApiKeyInfo
{
    /// <summary>
    /// Whether API access is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The masked API key (shows first 4 and last 4 characters).
    /// </summary>
    public string MaskedKey { get; set; } = "";

    /// <summary>
    /// The full API key (only included when explicitly requested).
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

    /// <summary>
    /// Preferred view mode for issues in series detail view: "cover" or "list"
    /// </summary>
    public string IssueViewMode { get; set; } = "cover";

    /// <summary>
    /// Preferred display mode for pull list: "list" or "grid"
    /// </summary>
    public string PullListDisplayMode { get; set; } = "list";
}

/// <summary>
/// General application settings.
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Pattern for organizing series folders.
    /// Supports tokens: {Publisher}, {Series Title}, {Year}, {Status}
    /// Use "/" to create subdirectories (e.g., "{Publisher}/{Series Title}")
    /// </summary>
    public string SeriesFolderFormat { get; set; } = "{Publisher}/{Series Title} ({Year})";

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

/// <summary>
/// Settings for auto-matching downloaded files to series/issues.
/// Used by both DDL import matching and ComicVine auto-matching.
/// </summary>
public class AutoMatchSettings
{
    // === Year Matching Settings ===
    
    /// <summary>
    /// Maximum year difference allowed between release and series.
    /// If parsed year differs from series StartYear by more than this, the match is rejected.
    /// Default is 2 years to handle re-releases and reprints.
    /// </summary>
    public int YearMatchTolerance { get; set; } = 2;

    /// <summary>
    /// If true, reject matches where parsed year differs from series year by more than YearMatchTolerance.
    /// If false, year mismatch only reduces confidence score.
    /// </summary>
    public bool RejectMismatchedYears { get; set; } = true;

    /// <summary>
    /// Year penalty applied when parsed year doesn't match but is within tolerance.
    /// This reduces confidence score for near-misses.
    /// </summary>
    public int YearMismatchPenalty { get; set; } = 25;

    // === Confidence Settings ===

    /// <summary>
    /// Confidence threshold for auto-accepting matches (0-100).
    /// Matches at or above this threshold are auto-imported.
    /// </summary>
    public int ConfidenceThreshold { get; set; } = 85;

    /// <summary>
    /// Minimum confidence score (0-100) required for automatic import.
    /// Matches below this threshold are queued for manual review.
    /// Alias for ConfidenceThreshold for DDL import compatibility.
    /// </summary>
    public int MinConfidenceForAutoImport 
    { 
        get => ConfidenceThreshold; 
        set => ConfidenceThreshold = value; 
    }

    // === Ambiguity Detection Settings ===

    /// <summary>
    /// If true, matches without a parsed year when multiple series with the same name exist
    /// will be flagged as low-confidence and require manual review.
    /// </summary>
    public bool RequireYearForAmbiguousSeries { get; set; } = true;

    /// <summary>
    /// If true, apply stricter matching when multiple series share the same base name.
    /// This enables year-based disambiguation and higher confidence thresholds.
    /// </summary>
    public bool EnableAmbiguousSeriesDetection { get; set; } = true;

    // === Publisher Matching Settings ===

    /// <summary>
    /// Bonus applied when parsed publisher exactly matches series publisher.
    /// Default +15 confidence boost.
    /// </summary>
    public int PublisherMatchBonus { get; set; } = 15;

    /// <summary>
    /// Penalty applied when parsed release has a publisher that doesn't match series publisher.
    /// Only applied when both release and series have publisher information.
    /// Default -20 confidence reduction.
    /// </summary>
    public int PublisherMismatchPenalty { get; set; } = 20;

    /// <summary>
    /// If true, when multiple series share the same name and the release has publisher info,
    /// only series with matching publishers will be considered (non-matching eliminated).
    /// </summary>
    public bool PreferPublisherMatchForAmbiguous { get; set; } = true;

    /// <summary>
    /// If true, reject matches when both have publishers and they don't match.
    /// Similar to RejectMismatchedYears but for publisher.
    /// </summary>
    public bool RejectMismatchedPublishers { get; set; } = false;

    // === Verification & Confirmation Settings (EPIC 19.4) ===

    /// <summary>
    /// If true, require manual confirmation for the first issue imported to any series.
    /// This helps catch mismatches early before importing many issues to wrong series.
    /// </summary>
    public bool RequireConfirmationForFirstIssue { get; set; } = true;

    /// <summary>
    /// Threshold below which matches are considered "low confidence" and flagged for review.
    /// This is separate from MinConfidenceForAutoImport - matches between LowConfidenceThreshold
    /// and MinConfidenceForAutoImport are auto-imported but flagged for review.
    /// Default: 70 (meaning 70-84% confidence is "borderline" auto-import with warning)
    /// </summary>
    public int LowConfidenceThreshold { get; set; } = 70;

    /// <summary>
    /// If true, show detailed match reasoning in the import queue UI.
    /// Includes score breakdown, confidence reductions, and alternatives.
    /// </summary>
    public bool ShowMatchReasoning { get; set; } = true;

    // === Import Behavior Settings ===

    /// <summary>
    /// Whether to auto-match during import.
    /// </summary>
    public bool AutoMatchOnImport { get; set; } = true;
    
    /// <summary>
    /// Whether to create series/issues if not found locally.
    /// </summary>
    public bool CreateMissingItems { get; set; } = true;
    
    /// <summary>
    /// Maximum candidates to keep for manual review.
    /// </summary>
    public int MaxCandidatesForReview { get; set; } = 5;
}

