namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Filters DDL candidates based on configurable rules.
/// Implements Mylar3-compatible filtering logic.
/// </summary>
public interface IDdlFilter
{
    /// <summary>
    /// Filter a list of candidates, marking those that should be excluded.
    /// </summary>
    IReadOnlyList<DdlCandidate> Filter(IEnumerable<DdlCandidate> candidates, DdlFilterSettings settings);
    
    /// <summary>
    /// Check if a single candidate passes all filters.
    /// </summary>
    (bool Passes, string? Reason) CheckCandidate(DdlCandidate candidate, DdlFilterSettings settings);
}

/// <summary>
/// Configuration settings for DDL filtering.
/// Matches Mylar3 defaults where documented.
/// </summary>
public class DdlFilterSettings
{
    /// <summary>
    /// Words that cause immediate rejection if found in title.
    /// Default: sample, preview (Mylar3 defaults)
    /// </summary>
    public List<string> BannedWords { get; set; } = new() { "sample", "preview" };
    
    /// <summary>
    /// Words that must be present (if any specified).
    /// </summary>
    public List<string> RequiredWords { get; set; } = new();
    
    /// <summary>
    /// Minimum file size in bytes for single issues. 0 = no limit.
    /// </summary>
    public long MinSizeBytesSingles { get; set; } = 1_000_000; // 1MB
    
    /// <summary>
    /// Maximum file size in bytes for single issues. 0 = no limit.
    /// </summary>
    public long MaxSizeBytesSingles { get; set; } = 200_000_000; // 200MB
    
    /// <summary>
    /// Minimum file size in bytes for collections. 0 = no limit.
    /// </summary>
    public long MinSizeBytesCollections { get; set; } = 5_000_000; // 5MB
    
    /// <summary>
    /// Maximum file size in bytes for collections. 0 = no limit.
    /// </summary>
    public long MaxSizeBytesCollections { get; set; } = 2_000_000_000; // 2GB
    
    /// <summary>
    /// Preferred formats in order (first = most preferred).
    /// </summary>
    public List<string> PreferredFormats { get; set; } = new() { "cbz", "cbr" };
    
    /// <summary>
    /// Formats that are never accepted.
    /// </summary>
    public List<string> BlockedFormats { get; set; } = new() { "pdf" };
    
    /// <summary>
    /// Whether to require format to be in preferred list.
    /// </summary>
    public bool RequirePreferredFormat { get; set; } = false;
    
    /// <summary>
    /// Minimum parse confidence score (0-100). 0 = accept all.
    /// </summary>
    public int MinParseConfidence { get; set; } = 20;
    
    /// <summary>
    /// Whether to filter out releases without year information.
    /// </summary>
    public bool RequireYear { get; set; } = false;
    
    /// <summary>
    /// Whether to filter out releases without series title.
    /// </summary>
    public bool RequireSeriesTitle { get; set; } = true;
    
    /// <summary>
    /// Blocked release groups.
    /// </summary>
    public List<string> BlockedGroups { get; set; } = new();
    
    /// <summary>
    /// Preferred release groups (used for scoring, not filtering).
    /// </summary>
    public List<string> PreferredGroups { get; set; } = new();
}



