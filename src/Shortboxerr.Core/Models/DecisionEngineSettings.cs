namespace Shortboxerr.Core.Models;

/// <summary>
/// Configuration settings for the DecisionEngine.
/// Defaults match Mylar3 behavior where documented.
/// </summary>
public class DecisionEngineSettings
{
    /// <summary>
    /// Whether auto-grab is enabled.
    /// </summary>
    public bool AutoGrabEnabled { get; set; } = true;
    
    /// <summary>
    /// Minimum score required for auto-grab.
    /// Candidates below this threshold require manual approval.
    /// </summary>
    public int AutoGrabThreshold { get; set; } = 80;
    
    /// <summary>
    /// Score margin for manual choice.
    /// If top candidates are within this margin, prompt for manual selection.
    /// </summary>
    public int ManualChoiceMargin { get; set; } = 10;
    
    /// <summary>
    /// Preferred format order (first = best).
    /// </summary>
    public List<string> FormatPreferenceOrder { get; set; } = new() { "cbz", "cbr" };
    
    /// <summary>
    /// Words that cause immediate rejection.
    /// </summary>
    public List<string> BannedWords { get; set; } = new() { "sample", "preview" };
    
    /// <summary>
    /// Words that must be present (if non-empty).
    /// </summary>
    public List<string> RequiredWords { get; set; } = new();
    
    /// <summary>
    /// Minimum file size for single issues (bytes). 0 = no limit.
    /// Mylar3 default: ~1MB
    /// </summary>
    public long MinSizeBytesSingles { get; set; } = 1_000_000;
    
    /// <summary>
    /// Maximum file size for single issues (bytes). 0 = no limit.
    /// Mylar3 default: ~200MB
    /// </summary>
    public long MaxSizeBytesSingles { get; set; } = 200_000_000;
    
    /// <summary>
    /// Minimum file size for collections (bytes). 0 = no limit.
    /// </summary>
    public long MinSizeBytesCollections { get; set; } = 5_000_000;
    
    /// <summary>
    /// Maximum file size for collections (bytes). 0 = no limit.
    /// </summary>
    public long MaxSizeBytesCollections { get; set; } = 2_000_000_000;
    
    /// <summary>
    /// Source priority list (first = highest priority).
    /// Sources not in list get lowest priority.
    /// </summary>
    public List<string> SourcePriority { get; set; } = new();
    
    /// <summary>
    /// Points awarded for preferred format match.
    /// </summary>
    public int FormatMatchPoints { get; set; } = 20;
    
    /// <summary>
    /// Points awarded for exact series title match.
    /// </summary>
    public int ExactSeriesMatchPoints { get; set; } = 30;
    
    /// <summary>
    /// Points awarded for partial series title match.
    /// </summary>
    public int PartialSeriesMatchPoints { get; set; } = 15;
    
    /// <summary>
    /// Points awarded for exact issue number match.
    /// </summary>
    public int ExactIssueMatchPoints { get; set; } = 25;
    
    /// <summary>
    /// Points awarded for year match.
    /// </summary>
    public int YearMatchPoints { get; set; } = 10;
    
    /// <summary>
    /// Penalty per priority rank below top source.
    /// </summary>
    public int SourcePriorityPenalty { get; set; } = 5;
}



