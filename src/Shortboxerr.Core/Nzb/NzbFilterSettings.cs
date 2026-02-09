namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Settings for filtering NZB search results.
/// </summary>
public class NzbFilterSettings
{
    /// <summary>
    /// Minimum age in days (0 = no minimum).
    /// </summary>
    public int MinAgeDays { get; set; } = 0;
    
    /// <summary>
    /// Maximum age in days (0 = no maximum).
    /// </summary>
    public int MaxAgeDays { get; set; } = 0;
    
    /// <summary>
    /// Minimum file size in bytes (0 = no minimum).
    /// </summary>
    public long MinSizeBytes { get; set; } = 0;
    
    /// <summary>
    /// Maximum file size in bytes (0 = no maximum).
    /// </summary>
    public long MaxSizeBytes { get; set; } = 0;
    
    /// <summary>
    /// Minimum file size in MB (convenience property).
    /// </summary>
    public double MinSizeMB
    {
        get => MinSizeBytes / 1024.0 / 1024.0;
        set => MinSizeBytes = (long)(value * 1024 * 1024);
    }
    
    /// <summary>
    /// Maximum file size in MB (convenience property).
    /// </summary>
    public double MaxSizeMB
    {
        get => MaxSizeBytes / 1024.0 / 1024.0;
        set => MaxSizeBytes = (long)(value * 1024 * 1024);
    }
    
    /// <summary>
    /// Words that must be present in the release title.
    /// </summary>
    public List<string> RequiredWords { get; set; } = new();
    
    /// <summary>
    /// Words that cause the release to be rejected.
    /// </summary>
    public List<string> BannedWords { get; set; } = new();
    
    /// <summary>
    /// Preferred words that boost the quality score.
    /// </summary>
    public List<string> PreferredWords { get; set; } = new();
    
    /// <summary>
    /// Whether to reject password-protected releases.
    /// </summary>
    public bool RejectPasswordProtected { get; set; } = true;
    
    /// <summary>
    /// Whether to prefer PROPER releases.
    /// </summary>
    public bool PreferProper { get; set; } = true;
    
    /// <summary>
    /// Whether to prefer REPACK releases.
    /// </summary>
    public bool PreferRepack { get; set; } = true;
    
    /// <summary>
    /// Preferred formats in order (first = most preferred).
    /// </summary>
    public List<string> PreferredFormats { get; set; } = new() { "cbz", "cbr", "pdf" };
    
    /// <summary>
    /// Minimum quality to accept (Digital, Scan, etc).
    /// </summary>
    public string? MinQuality { get; set; }
    
    /// <summary>
    /// Minimum confidence score from parser to accept (0-100).
    /// </summary>
    public int MinParseConfidence { get; set; } = 0;
    
    /// <summary>
    /// Category IDs to include (empty = all categories).
    /// </summary>
    public List<int> IncludeCategories { get; set; } = new();
    
    /// <summary>
    /// Category IDs to exclude.
    /// </summary>
    public List<int> ExcludeCategories { get; set; } = new();
    
    /// <summary>
    /// Indexer IDs to prefer (will boost priority).
    /// </summary>
    public List<string> PreferredIndexers { get; set; } = new();
    
    /// <summary>
    /// Creates default filter settings.
    /// </summary>
    public static NzbFilterSettings Default => new()
    {
        MinAgeDays = 0,
        MaxAgeDays = 0,
        MinSizeBytes = 0,
        MaxSizeBytes = 0,
        RequiredWords = new List<string>(),
        BannedWords = new List<string> { "sample", "trailer", "password", "passworded" },
        PreferredWords = new List<string> { "digital", "webrip" },
        RejectPasswordProtected = true,
        PreferProper = true,
        PreferRepack = true,
        PreferredFormats = new List<string> { "cbz", "cbr", "pdf" },
        MinParseConfidence = 0
    };
}

/// <summary>
/// Result of filtering an NZB candidate.
/// </summary>
public class NzbFilterResult
{
    /// <summary>
    /// Whether the candidate passed all filters.
    /// </summary>
    public bool Accepted { get; init; }
    
    /// <summary>
    /// Rejection reason if not accepted.
    /// </summary>
    public NzbRejectionReason RejectionReason { get; init; }
    
    /// <summary>
    /// Detailed rejection message.
    /// </summary>
    public string? RejectionMessage { get; init; }
    
    /// <summary>
    /// Quality score adjustment from filtering.
    /// </summary>
    public int ScoreAdjustment { get; init; }
    
    /// <summary>
    /// Checks that were applied.
    /// </summary>
    public List<NzbFilterCheck> Checks { get; init; } = new();
    
    public static NzbFilterResult Accept(int scoreAdjustment = 0, List<NzbFilterCheck>? checks = null)
    {
        return new NzbFilterResult
        {
            Accepted = true,
            RejectionReason = NzbRejectionReason.None,
            ScoreAdjustment = scoreAdjustment,
            Checks = checks ?? new List<NzbFilterCheck>()
        };
    }
    
    public static NzbFilterResult Reject(NzbRejectionReason reason, string message, List<NzbFilterCheck>? checks = null)
    {
        return new NzbFilterResult
        {
            Accepted = false,
            RejectionReason = reason,
            RejectionMessage = message,
            Checks = checks ?? new List<NzbFilterCheck>()
        };
    }
}

/// <summary>
/// Reasons why an NZB candidate may be rejected.
/// </summary>
public enum NzbRejectionReason
{
    None = 0,
    
    // Age rejections
    TooOld = 10,
    TooNew = 11,
    
    // Size rejections
    TooSmall = 20,
    TooLarge = 21,
    
    // Content rejections
    BannedWordFound = 30,
    MissingRequiredWord = 31,
    
    // Quality rejections
    QualityTooLow = 40,
    FormatNotAccepted = 41,
    
    // Security rejections
    PasswordProtected = 50,
    
    // Category rejections
    CategoryExcluded = 60,
    CategoryNotIncluded = 61,
    
    // Parse rejections
    LowConfidence = 70,
    
    // Other
    Unknown = 99
}

/// <summary>
/// Result of a single filter check.
/// </summary>
public record NzbFilterCheck(string Name, bool Passed, string Details);
