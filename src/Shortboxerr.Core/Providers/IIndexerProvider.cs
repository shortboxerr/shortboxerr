using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Providers;

/// <summary>
/// Provider interface for search/discovery operations.
/// Indexers find release candidates from various sources.
/// </summary>
public interface IIndexerProvider : IProvider
{
    /// <summary>
    /// Whether this indexer supports RSS feeds for automatic updates.
    /// </summary>
    bool SupportsRss { get; }
    
    /// <summary>
    /// Whether this indexer supports search queries.
    /// </summary>
    bool SupportsSearch { get; }
    
    /// <summary>
    /// Search for releases matching the query.
    /// </summary>
    Task<IndexerSearchResult> SearchAsync(IndexerSearchQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest releases (RSS-style polling).
    /// </summary>
    Task<IndexerSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search query for indexers.
/// </summary>
public class IndexerSearchQuery
{
    /// <summary>
    /// Series title to search for.
    /// </summary>
    public string? SeriesTitle { get; init; }
    
    /// <summary>
    /// Specific issue number to find.
    /// </summary>
    public decimal? IssueNumber { get; init; }
    
    /// <summary>
    /// Volume number.
    /// </summary>
    public int? VolumeNumber { get; init; }
    
    /// <summary>
    /// Year filter.
    /// </summary>
    public int? Year { get; init; }
    
    /// <summary>
    /// Free-text search query.
    /// </summary>
    public string? Query { get; init; }
    
    /// <summary>
    /// Maximum results to return.
    /// </summary>
    public int Limit { get; init; } = 100;
    
    /// <summary>
    /// Whether to search for collections (TPB, HC, etc.).
    /// </summary>
    public bool SearchCollections { get; init; }
    
    /// <summary>
    /// Categories to search (provider-specific).
    /// </summary>
    public List<string> Categories { get; init; } = new();
}

/// <summary>
/// Result of an indexer search operation.
/// </summary>
public class IndexerSearchResult
{
    /// <summary>
    /// Whether the search was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if search failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Candidates found by the search.
    /// </summary>
    public List<Candidate> Candidates { get; init; } = new();
    
    /// <summary>
    /// Total results available (may be more than returned).
    /// </summary>
    public int TotalResults { get; init; }
    
    /// <summary>
    /// Search query that was executed.
    /// </summary>
    public IndexerSearchQuery? Query { get; init; }
    
    /// <summary>
    /// Time taken to execute the search.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Create a successful result.
    /// </summary>
    public static IndexerSearchResult Ok(List<Candidate> candidates, int? total = null, TimeSpan? duration = null) => new()
    {
        Success = true,
        Candidates = candidates,
        TotalResults = total ?? candidates.Count,
        Duration = duration ?? TimeSpan.Zero
    };
    
    /// <summary>
    /// Create a failed result.
    /// </summary>
    public static IndexerSearchResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

