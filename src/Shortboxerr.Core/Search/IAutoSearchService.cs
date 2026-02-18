namespace Shortboxerr.Core.Search;

/// <summary>
/// Service for automatic searching of wanted issues.
/// Coordinates with DDL/NZB providers to find and download comics.
/// </summary>
public interface IAutoSearchService
{
    /// <summary>
    /// Search for a specific issue across all enabled providers.
    /// </summary>
    Task<AutoSearchResult> SearchIssueAsync(int issueId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for all wanted issues in a series.
    /// </summary>
    Task<AutoSearchBatchResult> SearchSeriesWantedAsync(int seriesId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for all wanted issues across the library.
    /// Respects rate limits and search intervals.
    /// </summary>
    Task<AutoSearchBatchResult> SearchAllWantedAsync(int? maxIssues = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get issues that are due for searching based on settings.
    /// </summary>
    Task<IReadOnlyList<WantedIssueInfo>> GetSearchableIssuesAsync(int? limit = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current auto-search status.
    /// </summary>
    Task<AutoSearchStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent auto-search history.
    /// </summary>
    Task<IReadOnlyList<AutoSearchHistoryEntry>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of searching for a single issue.
/// </summary>
public record AutoSearchResult
{
    public required int IssueId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public required bool Success { get; init; }
    public required int CandidatesFound { get; init; }
    public string? SelectedCandidateTitle { get; init; }
    public string? DownloadId { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    
    public static AutoSearchResult NotFound(int issueId, string seriesTitle, string issueNumber, TimeSpan duration) => new()
    {
        IssueId = issueId,
        SeriesTitle = seriesTitle,
        IssueNumber = issueNumber,
        Success = false,
        CandidatesFound = 0,
        Duration = duration
    };
    
    public static AutoSearchResult Found(int issueId, string seriesTitle, string issueNumber, int candidates, string selectedTitle, string? downloadId, TimeSpan duration) => new()
    {
        IssueId = issueId,
        SeriesTitle = seriesTitle,
        IssueNumber = issueNumber,
        Success = true,
        CandidatesFound = candidates,
        SelectedCandidateTitle = selectedTitle,
        DownloadId = downloadId,
        Duration = duration
    };
    
    public static AutoSearchResult Failed(int issueId, string seriesTitle, string issueNumber, string error, TimeSpan duration) => new()
    {
        IssueId = issueId,
        SeriesTitle = seriesTitle,
        IssueNumber = issueNumber,
        Success = false,
        CandidatesFound = 0,
        Error = error,
        Duration = duration
    };
}

/// <summary>
/// Result of searching for multiple issues.
/// </summary>
public record AutoSearchBatchResult
{
    public required int TotalSearched { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailedCount { get; init; }
    public required int NotFoundCount { get; init; }
    public required IReadOnlyList<AutoSearchResult> Results { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public string? Error { get; init; }
    
    public static AutoSearchBatchResult Empty => new()
    {
        TotalSearched = 0,
        SuccessCount = 0,
        FailedCount = 0,
        NotFoundCount = 0,
        Results = Array.Empty<AutoSearchResult>(),
        TotalDuration = TimeSpan.Zero
    };
}

/// <summary>
/// Information about a wanted issue that can be searched.
/// </summary>
public record WantedIssueInfo
{
    public required int IssueId { get; init; }
    public required int SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public required string? IssueTitle { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? LastSearchedAt { get; init; }
    public int SearchAttempts { get; init; }
}

/// <summary>
/// Current status of the auto-search service.
/// </summary>
public record AutoSearchStatus
{
    public required bool Enabled { get; init; }
    public required bool IsRunning { get; init; }
    public required int WantedIssuesCount { get; init; }
    public required int SearchableCount { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime? NextRunAt { get; init; }
    public int TodaySearchCount { get; init; }
    public int TodayFoundCount { get; init; }
}

/// <summary>
/// Entry in the auto-search history.
/// </summary>
public record AutoSearchHistoryEntry
{
    public required int IssueId { get; init; }
    public required string SeriesTitle { get; init; }
    public required string IssueNumber { get; init; }
    public required DateTime SearchedAt { get; init; }
    public required bool Found { get; init; }
    public required int CandidatesFound { get; init; }
    public string? SelectedCandidate { get; init; }
    public string? Error { get; init; }
}
