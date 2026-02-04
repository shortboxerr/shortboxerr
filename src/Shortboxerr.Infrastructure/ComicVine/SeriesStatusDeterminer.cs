using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Determines series status (Continuing/Ended) based on available data.
/// </summary>
public class SeriesStatusDeterminer
{
    private readonly ILogger<SeriesStatusDeterminer>? _logger;
    
    /// <summary>
    /// Default number of years after which a series with no new issues is considered ended.
    /// </summary>
    public const int DefaultEndedYearsThreshold = 2;
    
    /// <summary>
    /// Default number of months after which to consider a series potentially ended.
    /// </summary>
    public const int DefaultStaleMonthsThreshold = 18;
    
    public SeriesStatusDeterminer(ILogger<SeriesStatusDeterminer>? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Determines the series status based on available metadata.
    /// </summary>
    /// <param name="series">The series to evaluate.</param>
    /// <param name="lastIssueDate">Date of the last issue (from ComicVine).</param>
    /// <param name="expectedIssueCount">Total issue count from ComicVine.</param>
    /// <param name="comicVineLastUpdated">When ComicVine last updated this volume.</param>
    /// <param name="yearsThreshold">Years of inactivity to consider ended.</param>
    /// <returns>Tuple of (status, source, reasons for determination).</returns>
    public (SeriesStatus Status, StatusSource Source, List<string> Reasons) DetermineStatus(
        Series series,
        DateTime? lastIssueDate,
        int? expectedIssueCount,
        DateTime? comicVineLastUpdated,
        int yearsThreshold = DefaultEndedYearsThreshold)
    {
        var reasons = new List<string>();
        
        // If manually set, don't override
        if (series.StatusSource == StatusSource.Manual)
        {
            reasons.Add("Status was manually set by user, not overriding");
            return (series.Status, StatusSource.Manual, reasons);
        }
        
        // Check if series has an end year set (definitive ended indicator)
        if (series.EndYear.HasValue && series.EndYear.Value <= DateTime.UtcNow.Year)
        {
            reasons.Add($"Series has end year set: {series.EndYear}");
            return (SeriesStatus.Ended, StatusSource.Auto, reasons);
        }
        
        // Calculate time since last issue
        var now = DateTime.UtcNow;
        
        if (lastIssueDate.HasValue)
        {
            var timeSinceLastIssue = now - lastIssueDate.Value;
            var yearsSinceLastIssue = timeSinceLastIssue.TotalDays / 365.25;
            
            if (yearsSinceLastIssue >= yearsThreshold)
            {
                reasons.Add($"Last issue was {yearsSinceLastIssue:F1} years ago (threshold: {yearsThreshold} years)");
                
                // Additional confidence: check if issue count matches expected
                if (expectedIssueCount.HasValue && series.Issues?.Count >= expectedIssueCount.Value)
                {
                    reasons.Add($"All {expectedIssueCount} expected issues are present");
                }
                
                return (SeriesStatus.Ended, StatusSource.Auto, reasons);
            }
            else
            {
                reasons.Add($"Last issue was {yearsSinceLastIssue:F1} years ago (< {yearsThreshold} year threshold)");
            }
        }
        else
        {
            reasons.Add("No last issue date available");
        }
        
        // Check ComicVine last updated date as secondary indicator
        if (comicVineLastUpdated.HasValue)
        {
            var timeSinceUpdate = now - comicVineLastUpdated.Value;
            var yearsSinceUpdate = timeSinceUpdate.TotalDays / 365.25;
            
            if (yearsSinceUpdate >= yearsThreshold + 1) // Add 1 year buffer for CV updates
            {
                reasons.Add($"ComicVine hasn't updated this series in {yearsSinceUpdate:F1} years");
                
                // Only consider ended if we also have high issue count confidence
                if (expectedIssueCount.HasValue && series.Issues?.Count >= expectedIssueCount.Value)
                {
                    reasons.Add($"Issue count ({series.Issues?.Count}) matches ComicVine ({expectedIssueCount})");
                    return (SeriesStatus.Ended, StatusSource.Auto, reasons);
                }
            }
        }
        
        // Check for mini-series pattern (small, fixed issue count)
        if (IsLikelyMiniSeries(series, expectedIssueCount, lastIssueDate))
        {
            reasons.Add("Appears to be a completed mini-series");
            return (SeriesStatus.Ended, StatusSource.Auto, reasons);
        }
        
        // Default to Continuing if no indicators suggest ended
        reasons.Add("No definitive ended indicators found, assuming Continuing");
        return (SeriesStatus.Continuing, StatusSource.Auto, reasons);
    }
    
    /// <summary>
    /// Determines status from ComicVine data during initial sync.
    /// </summary>
    public (SeriesStatus Status, StatusSource Source, List<string> Reasons) DetermineStatusFromComicVine(
        string volumeName,
        int? startYear,
        int? issueCount,
        DateTime? firstIssueDate,
        DateTime? lastIssueDate,
        DateTime? comicVineLastUpdated,
        int yearsThreshold = DefaultEndedYearsThreshold)
    {
        var reasons = new List<string>();
        var now = DateTime.UtcNow;
        
        // No issues = likely not started or placeholder
        if (!issueCount.HasValue || issueCount.Value == 0)
        {
            reasons.Add("No issues in volume");
            return (SeriesStatus.Continuing, StatusSource.ComicVine, reasons);
        }
        
        // Check last issue date
        if (lastIssueDate.HasValue)
        {
            var yearsSinceLastIssue = (now - lastIssueDate.Value).TotalDays / 365.25;
            
            if (yearsSinceLastIssue >= yearsThreshold)
            {
                reasons.Add($"Last issue was {yearsSinceLastIssue:F1} years ago");
                return (SeriesStatus.Ended, StatusSource.ComicVine, reasons);
            }
            else
            {
                reasons.Add($"Recent activity: last issue {yearsSinceLastIssue:F1} years ago");
            }
        }
        
        // Check for mini-series pattern
        if (issueCount.HasValue && issueCount.Value > 0 && issueCount.Value <= 12)
        {
            // Small issue count + old last issue = likely completed mini-series
            if (lastIssueDate.HasValue)
            {
                var monthsSinceLastIssue = (now - lastIssueDate.Value).TotalDays / 30.44;
                if (monthsSinceLastIssue > DefaultStaleMonthsThreshold)
                {
                    reasons.Add($"Mini-series ({issueCount} issues) with no recent releases");
                    return (SeriesStatus.Ended, StatusSource.ComicVine, reasons);
                }
            }
        }
        
        // Check ComicVine staleness
        if (comicVineLastUpdated.HasValue)
        {
            var yearsSinceUpdate = (now - comicVineLastUpdated.Value).TotalDays / 365.25;
            if (yearsSinceUpdate >= yearsThreshold + 1)
            {
                reasons.Add($"ComicVine data is {yearsSinceUpdate:F1} years stale");
                // This alone isn't definitive, but combined with other factors...
            }
        }
        
        reasons.Add("Appears to be actively publishing");
        return (SeriesStatus.Continuing, StatusSource.ComicVine, reasons);
    }
    
    /// <summary>
    /// Checks if a series appears to be a mini-series (limited series).
    /// </summary>
    private bool IsLikelyMiniSeries(Series series, int? expectedIssueCount, DateTime? lastIssueDate)
    {
        // Mini-series typically have 4-12 issues
        if (!expectedIssueCount.HasValue || expectedIssueCount.Value < 2 || expectedIssueCount.Value > 12)
        {
            return false;
        }
        
        // Check if all issues are present
        var actualCount = series.Issues?.Count ?? 0;
        if (actualCount < expectedIssueCount.Value)
        {
            return false;
        }
        
        // Check if the last issue was more than 18 months ago
        if (lastIssueDate.HasValue)
        {
            var monthsSinceLastIssue = (DateTime.UtcNow - lastIssueDate.Value).TotalDays / 30.44;
            return monthsSinceLastIssue > DefaultStaleMonthsThreshold;
        }
        
        return false;
    }
}
