using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Metron;

/// <summary>
/// Service for enriching comics using fallback data sources when ComicVine is unavailable or incomplete.
/// 
/// Strategy:
/// 1. ComicVine (primary) - comprehensive US/mainstream coverage
/// 2. Metron (fallback) - better indie/international/UK coverage
/// 3. Manual creation - user creates series manually
/// 
/// This improves coverage for niche publishers and international comics.
/// </summary>
public interface IFallbackEnrichmentService
{
    /// <summary>
    /// Attempts to enrich an unmatched series using Metron.
    /// 
    /// Uses series title + publisher to search Metron,
    /// then creates the series if found, linking to Metron as data source.
    /// </summary>
    /// <param name="seriesTitle">The series title to search for</param>
    /// <param name="publisher">Optional publisher name for better matching</param>
    /// <param name="createIfFound">If true, creates the series in DB after finding in Metron</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with series if found, reason if not</returns>
    Task<FallbackEnrichmentResult> EnrichSeriesFromMetronAsync(
        string seriesTitle,
        string? publisher = null,
        bool createIfFound = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to fetch missing issue data from Metron for a series.
    /// 
    /// If series has ComicVine ID, tries Metron's ComicVine mapping first.
    /// Otherwise falls back to series name + issue number search.
    /// </summary>
    /// <param name="seriesId">The series ID in our DB</param>
    /// <param name="issueNumber">The issue number to find</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue metadata if found</returns>
    Task<FallbackIssueEnrichmentResult> EnrichIssueFromMetronAsync(
        int seriesId,
        string issueNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk enrichment: attempts to match all unmatched series against Metron.
    /// Reports progress and returns summary of matches found.
    /// </summary>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of enrichment attempt</returns>
    Task<BulkFallbackEnrichmentResult> BulkEnrichUnmatchedSeriesAsync(
        IProgress<FallbackEnrichmentProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of attempting to enrich a series from Metron.
/// </summary>
public class FallbackEnrichmentResult
{
    /// <summary>
    /// Whether the series was found in the fallback source.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// The created/matched series if found.
    /// </summary>
    public Series? Series { get; set; }

    /// <summary>
    /// The source data source used (e.g., "Metron").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// External ID in the fallback source (e.g., Metron series ID).
    /// </summary>
    public int? ExternalId { get; set; }

    /// <summary>
    /// Reason if not found or error.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Result of attempting to enrich an issue from fallback source.
/// </summary>
public class FallbackIssueEnrichmentResult
{
    /// <summary>
    /// Whether issue data was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Retrieved issue details if found.
    /// </summary>
    public class IssueData
    {
        public string? Number { get; set; }
        public string? Title { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? CoverUrl { get; set; }
        public string? Description { get; set; }
    }

    public IssueData? Issue { get; set; }

    /// <summary>
    /// The source used (e.g., "Metron").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Reason if not found.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Progress report for bulk enrichment.
/// </summary>
public class FallbackEnrichmentProgress
{
    /// <summary>
    /// Total series being processed.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Number processed so far.
    /// </summary>
    public int Current { get; set; }

    /// <summary>
    /// Number of matches found so far.
    /// </summary>
    public int MatchesFound { get; set; }

    /// <summary>
    /// Current series being processed.
    /// </summary>
    public string? CurrentSeries { get; set; }
}

/// <summary>
/// Summary of bulk enrichment attempt.
/// </summary>
public class BulkFallbackEnrichmentResult
{
    /// <summary>
    /// Total unmatched series processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of series matched from fallback source.
    /// </summary>
    public int MatchesFound { get; set; }

    /// <summary>
    /// Number of errors during processing.
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Detailed results per series.
    /// </summary>
    public List<BulkEnrichmentItemResult> Results { get; set; } = new();

    /// <summary>
    /// Overall success (at least some matches found).
    /// </summary>
    public bool Success => MatchesFound > 0 && Errors < (TotalProcessed / 2);
}

/// <summary>
/// Result for a single series in bulk enrichment.
/// </summary>
public class BulkEnrichmentItemResult
{
    /// <summary>
    /// The series ID that was processed.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// Series title.
    /// </summary>
    public string? SeriesTitle { get; set; }

    /// <summary>
    /// Whether a match was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// External ID if matched.
    /// </summary>
    public int? ExternalId { get; set; }

    /// <summary>
    /// Data source (e.g., "Metron").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Reason if not found or error message.
    /// </summary>
    public string? Reason { get; set; }
}
