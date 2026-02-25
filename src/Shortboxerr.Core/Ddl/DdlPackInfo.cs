namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Information about a DDL pack (multiple issues in one download).
/// Based on Mylar3's pack detection and handling.
/// </summary>
public class DdlPackInfo
{
    /// <summary>
    /// Whether this is a pack download.
    /// </summary>
    public bool IsPack { get; set; }
    
    /// <summary>
    /// Series title extracted from pack.
    /// </summary>
    public string? Series { get; set; }
    
    /// <summary>
    /// Year or year range (e.g., "2024" or "2020-2024").
    /// </summary>
    public string? Year { get; set; }
    
    /// <summary>
    /// Issue range string (e.g., "1-12" or "1, 2, 5-10").
    /// </summary>
    public string? IssueRange { get; set; }
    
    /// <summary>
    /// List of individual issue numbers included.
    /// </summary>
    public List<int> Issues { get; set; } = new();
    
    /// <summary>
    /// Volume label if present (e.g., "Vol. 1", "Volume 2").
    /// </summary>
    public string? VolumeLabel { get; set; }
    
    /// <summary>
    /// Numeric volume extracted from label.
    /// </summary>
    public int? VolumeNumber { get; set; }
    
    /// <summary>
    /// Book type for collections.
    /// </summary>
    public DdlBookType BookType { get; set; } = DdlBookType.Issue;
    
    /// <summary>
    /// Whether this pack includes annuals.
    /// </summary>
    public bool IncludesAnnuals { get; set; }
    
    /// <summary>
    /// Original title for reference.
    /// </summary>
    public string? OriginalTitle { get; set; }
    
    /// <summary>
    /// Cleaned/normalized filename.
    /// </summary>
    public string? Filename { get; set; }
    
    /// <summary>
    /// Parse issue range string into list of integers.
    /// Handles formats: "1-12", "1, 2, 3", "1-6, 8, 10-12", etc.
    /// </summary>
    public static List<int> ParseIssueRange(string? range)
    {
        var issues = new List<int>();
        
        if (string.IsNullOrWhiteSpace(range))
        {
            return issues;
        }
        
        // Remove # prefix if present
        range = range.Replace("#", "").Trim();
        
        // Split by comma or plus
        var parts = range.Replace('+', ',').Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            
            // Check for range (contains dash)
            var dashIndex = trimmed.IndexOf('-');
            if (dashIndex > 0 && dashIndex < trimmed.Length - 1)
            {
                var startStr = trimmed.Substring(0, dashIndex).Trim();
                var endStr = trimmed.Substring(dashIndex + 1).Trim();
                
                if (int.TryParse(startStr, out var start) && int.TryParse(endStr, out var end))
                {
                    for (var i = start; i <= end; i++)
                    {
                        if (!issues.Contains(i))
                        {
                            issues.Add(i);
                        }
                    }
                }
            }
            else if (int.TryParse(trimmed, out var single))
            {
                if (!issues.Contains(single))
                {
                    issues.Add(single);
                }
            }
        }
        
        issues.Sort();
        return issues;
    }
    
    /// <summary>
    /// Check if a specific issue is included in this pack.
    /// </summary>
    public bool ContainsIssue(int issueNumber)
    {
        return Issues.Contains(issueNumber);
    }
    
    /// <summary>
    /// Check if this pack contains all issues in a given range.
    /// </summary>
    public bool ContainsRange(int start, int end)
    {
        for (var i = start; i <= end; i++)
        {
            if (!Issues.Contains(i))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Book type for DDL releases.
/// Based on Mylar3's gc_booktype values.
/// </summary>
public enum DdlBookType
{
    /// <summary>
    /// Single issue.
    /// </summary>
    Issue,
    
    /// <summary>
    /// Trade paperback collection.
    /// </summary>
    TPB,
    
    /// <summary>
    /// Hardcover collection.
    /// </summary>
    HC,
    
    /// <summary>
    /// Graphic novel.
    /// </summary>
    GN,
    
    /// <summary>
    /// One-shot.
    /// </summary>
    OneShot,
    
    /// <summary>
    /// Could be TPB, GN, HC, or One-Shot (needs manual determination).
    /// </summary>
    Collection
}
