namespace Shortboxerr.Core.Search;

/// <summary>
/// Search configuration settings with Mylar3 parity.
/// These settings control how searches are performed across DDL, NZB, and torrent providers.
/// </summary>
public class SearchSettings
{
    #region Search Behavior

    /// <summary>
    /// Delay in seconds between consecutive searches to avoid rate limiting.
    /// Default: 1 second (Mylar3 default).
    /// </summary>
    public int SearchDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Whether to prefer pack/collection releases over individual issues.
    /// When enabled, searches prioritize releases that include multiple issues.
    /// </summary>
    public bool PreferPackReleases { get; set; } = false;

    /// <summary>
    /// Number of providers to search before stopping if a good match is found.
    /// 0 = search all providers regardless of matches found.
    /// </summary>
    public int SearchTierCutoff { get; set; } = 0;

    /// <summary>
    /// Maximum number of results to return per provider per search.
    /// </summary>
    public int MaxResultsPerProvider { get; set; } = 50;

    #endregion

    #region Quality Preferences

    /// <summary>
    /// Preferred quality tier for releases.
    /// </summary>
    public PreferredQuality PreferredQuality { get; set; } = PreferredQuality.Digital;

    /// <summary>
    /// Preferred format ordering. First format in list is most preferred.
    /// </summary>
    public List<string> FormatPreference { get; set; } = new() { "cbz", "cbr", "pdf", "epub" };

    /// <summary>
    /// When true, only accept CBZ format files. Reject CBR, PDF, etc.
    /// </summary>
    public bool CbzOnly { get; set; } = false;

    #endregion

    #region Size Limits

    /// <summary>
    /// Minimum file size in MB for single issue releases.
    /// 0 = no minimum.
    /// </summary>
    public int MinSizeMb { get; set; } = 0;

    /// <summary>
    /// Maximum file size in MB for single issue releases.
    /// 0 = no maximum.
    /// </summary>
    public int MaxSizeMb { get; set; } = 500;

    /// <summary>
    /// Minimum file size in MB for pack/collection releases.
    /// 0 = no minimum.
    /// </summary>
    public int MinSizePackMb { get; set; } = 0;

    /// <summary>
    /// Maximum file size in MB for pack/collection releases.
    /// 0 = no maximum.
    /// </summary>
    public int MaxSizePackMb { get; set; } = 5000;

    #endregion

    #region Filtering

    /// <summary>
    /// Words that disqualify a release (e.g., "sample", "preview", "watermark").
    /// Releases containing any of these words will be rejected.
    /// </summary>
    public List<string> BlacklistWords { get; set; } = new()
    {
        "sample", "preview", "watermark", "corrupt", "incomplete", "password"
    };

    /// <summary>
    /// Required words - release must contain at least one of these words.
    /// Empty list = no requirement.
    /// </summary>
    public List<string> WhitelistWords { get; set; } = new();

    /// <summary>
    /// Words to strip from release names during matching.
    /// Useful for removing common tags that interfere with matching.
    /// </summary>
    public List<string> IgnoreWords { get; set; } = new()
    {
        "repack", "proper", "fixed"
    };

    #endregion

    #region Provider Toggles

    /// <summary>
    /// Enable DDL (Direct Download Link) provider search.
    /// </summary>
    public bool EnableDdlSearch { get; set; } = true;

    /// <summary>
    /// Enable NZB/Usenet provider search.
    /// </summary>
    public bool EnableNzbSearch { get; set; } = true;

    /// <summary>
    /// Enable torrent provider search.
    /// </summary>
    public bool EnableTorrentSearch { get; set; } = false;

    #endregion

    #region Automation

    /// <summary>
    /// Automatically search for missing/wanted issues.
    /// </summary>
    public bool AutoSearchEnabled { get; set; } = false;

    /// <summary>
    /// Interval in hours between automatic searches.
    /// </summary>
    public int AutoSearchIntervalHours { get; set; } = 24;

    /// <summary>
    /// Automatically search for issues when adding a new series.
    /// </summary>
    public bool SearchNewSeriesOnAdd { get; set; } = true;

    /// <summary>
    /// Re-search for an issue if not found after this many days.
    /// 0 = don't re-search stale items.
    /// </summary>
    public int StaleSearchThresholdDays { get; set; } = 7;

    #endregion

    /// <summary>
    /// Settings key for persistence.
    /// </summary>
    public const string SettingsKey = "Search:Settings";

    /// <summary>
    /// Creates default search settings.
    /// </summary>
    public static SearchSettings Default => new();
}

/// <summary>
/// Quality preference tiers for releases.
/// </summary>
public enum PreferredQuality
{
    /// <summary>
    /// Any quality is acceptable.
    /// </summary>
    Any = 0,

    /// <summary>
    /// Prefer digital releases (highest quality).
    /// </summary>
    Digital = 1,

    /// <summary>
    /// Prefer web rips.
    /// </summary>
    Webrip = 2,

    /// <summary>
    /// Prefer scans (physical book scans).
    /// </summary>
    Scan = 3
}
