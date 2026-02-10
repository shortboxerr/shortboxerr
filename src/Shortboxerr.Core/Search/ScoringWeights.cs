namespace Shortboxerr.Core.Search;

/// <summary>
/// Configurable weights for search result scoring factors.
/// Higher weights mean the factor has more influence on the final score.
/// Based on Mylar3's search result scoring logic.
/// </summary>
public class ScoringWeights
{
    /// <summary>
    /// Weight for quality tier matching (Digital > Webrip > Scan).
    /// Base score: 100 for exact match, scaled by weight.
    /// </summary>
    public int QualityWeight { get; set; } = 100;

    /// <summary>
    /// Weight for file size scoring.
    /// Files in the expected range get full points, scaled by weight.
    /// </summary>
    public int SizeWeight { get; set; } = 50;

    /// <summary>
    /// Weight for trusted release group bonus.
    /// Releases from trusted groups get this bonus.
    /// </summary>
    public int ReleaseGroupWeight { get; set; } = 75;

    /// <summary>
    /// Weight for exact year match.
    /// Exact year match gets full points.
    /// </summary>
    public int YearMatchWeight { get; set; } = 50;

    /// <summary>
    /// Weight for exact issue number match.
    /// Exact issue match gets full points.
    /// </summary>
    public int IssueMatchWeight { get; set; } = 100;

    /// <summary>
    /// Weight for series title match accuracy.
    /// Exact match gets full points, partial matches get scaled points.
    /// </summary>
    public int SeriesMatchWeight { get; set; } = 100;

    /// <summary>
    /// Weight for format preference matching.
    /// Preferred format (CBZ) gets full points, CBR gets 75%, etc.
    /// </summary>
    public int FormatWeight { get; set; } = 25;

    /// <summary>
    /// Bonus points for each preferred word found in the release title.
    /// </summary>
    public int PreferredWordBonus { get; set; } = 10;

    /// <summary>
    /// Penalty points for each blacklisted word found.
    /// Applied as negative score.
    /// </summary>
    public int BlacklistWordPenalty { get; set; } = 50;

    /// <summary>
    /// Bonus for source priority (lower source priority = higher bonus).
    /// First-priority source gets full bonus.
    /// </summary>
    public int SourcePriorityWeight { get; set; } = 30;

    /// <summary>
    /// Bonus for newer/more recent releases (age in days).
    /// Releases posted recently get a freshness bonus.
    /// </summary>
    public int FreshnessWeight { get; set; } = 20;

    /// <summary>
    /// Maximum total score possible (for normalization).
    /// </summary>
    public int MaxScore => QualityWeight + SizeWeight + ReleaseGroupWeight + 
                           YearMatchWeight + IssueMatchWeight + SeriesMatchWeight + 
                           FormatWeight + SourcePriorityWeight + FreshnessWeight;

    /// <summary>
    /// Creates default scoring weights matching Mylar3 behavior.
    /// </summary>
    public static ScoringWeights Default => new();
}

/// <summary>
/// Configuration for trusted release groups.
/// Releases from these groups receive a score bonus.
/// </summary>
public class TrustedReleaseGroups
{
    /// <summary>
    /// List of trusted release group names (case-insensitive).
    /// </summary>
    public List<string> Groups { get; set; } = new()
    {
        // Common high-quality digital release groups
        "Minutemen",
        "DCP",
        "Digital-Empire",
        "Empire",
        "GreenGiant",
        "Glorith",
        "Bchry",
        "ComicMaster",
        "ComicKing",
        "GetComics",
        "Nemesis",
        "Nem",
        "Mephisto",
        "Zone",
        "GN",
        "Savitar",
        "Sav"
    };

    /// <summary>
    /// Whether to use strict matching (exact match required).
    /// If false, partial matches are allowed.
    /// </summary>
    public bool StrictMatching { get; set; } = false;

    /// <summary>
    /// Creates default trusted groups list.
    /// </summary>
    public static TrustedReleaseGroups Default => new();
}

/// <summary>
/// Expected file size ranges for scoring.
/// Files within these ranges get full size score.
/// </summary>
public class ExpectedSizeRanges
{
    /// <summary>
    /// Minimum expected size for a single issue (MB).
    /// </summary>
    public int SingleIssueMinMb { get; set; } = 20;

    /// <summary>
    /// Maximum expected size for a single issue (MB).
    /// </summary>
    public int SingleIssueMaxMb { get; set; } = 200;

    /// <summary>
    /// Ideal size for a single issue (MB).
    /// Files at this size get maximum points.
    /// </summary>
    public int SingleIssueIdealMb { get; set; } = 75;

    /// <summary>
    /// Minimum expected size for a pack/collection (MB).
    /// </summary>
    public int PackMinMb { get; set; } = 100;

    /// <summary>
    /// Maximum expected size for a pack/collection (MB).
    /// </summary>
    public int PackMaxMb { get; set; } = 3000;

    /// <summary>
    /// Creates default size ranges.
    /// </summary>
    public static ExpectedSizeRanges Default => new();
}
