namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Provides access to configured NZB indexers and aggregated search functionality.
/// </summary>
public interface INzbIndexerProvider
{
    /// <summary>
    /// Gets all configured indexers.
    /// </summary>
    Task<IReadOnlyList<NewznabIndexer>> GetIndexersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets only enabled indexers, ordered by priority.
    /// </summary>
    Task<IReadOnlyList<NewznabIndexer>> GetEnabledIndexersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific indexer by ID.
    /// </summary>
    Task<NewznabIndexer?> GetIndexerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new indexer.
    /// </summary>
    Task<NewznabIndexer> AddIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing indexer.
    /// </summary>
    Task<NewznabIndexer> UpdateIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an indexer.
    /// </summary>
    Task<bool> DeleteIndexerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests an indexer connection.
    /// </summary>
    Task<NewznabTestResult> TestIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches across all enabled indexers.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated search results from all indexers</returns>
    Task<NzbAggregatedSearchResult> SearchAllAsync(NewznabSearchQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated search results from multiple NZB indexers.
/// </summary>
public class NzbAggregatedSearchResult
{
    /// <summary>
    /// Combined releases from all indexers, sorted by relevance/quality.
    /// </summary>
    public IReadOnlyList<NewznabRelease> Releases { get; init; } = Array.Empty<NewznabRelease>();

    /// <summary>
    /// Individual results from each indexer.
    /// </summary>
    public IReadOnlyList<IndexerSearchResult> IndexerResults { get; init; } = Array.Empty<IndexerSearchResult>();

    /// <summary>
    /// Total releases found across all indexers.
    /// </summary>
    public int TotalResults { get; init; }

    /// <summary>
    /// Number of indexers that were searched.
    /// </summary>
    public int IndexersSearched { get; init; }

    /// <summary>
    /// Number of indexers that returned results successfully.
    /// </summary>
    public int IndexersSuccessful { get; init; }

    /// <summary>
    /// Total time taken for the aggregated search.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Search result from a single indexer.
/// </summary>
public class IndexerSearchResult
{
    /// <summary>
    /// Indexer ID.
    /// </summary>
    public required string IndexerId { get; init; }

    /// <summary>
    /// Indexer name.
    /// </summary>
    public required string IndexerName { get; init; }

    /// <summary>
    /// Whether the search was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if not successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of releases found.
    /// </summary>
    public int ReleaseCount { get; init; }

    /// <summary>
    /// Time taken for this indexer's search.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Built-in indexer presets for easy setup.
/// </summary>
public static class NzbIndexerPresets
{
    /// <summary>
    /// Gets a preset configuration for a known indexer (requires API key).
    /// </summary>
    public static NewznabIndexer? GetPreset(string presetName, string apiKey)
    {
        return presetName.ToLowerInvariant() switch
        {
            "nzbgeek" => new NewznabIndexer
            {
                Name = "NZBgeek",
                BaseUrl = "https://api.nzbgeek.info",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 25
            },
            "drunkenslug" => new NewznabIndexer
            {
                Name = "DrunkenSlug",
                BaseUrl = "https://api.drunkenslug.com",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 25
            },
            "nzbfinder" => new NewznabIndexer
            {
                Name = "NZBFinder",
                BaseUrl = "https://nzbfinder.ws",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 30
            },
            "nzbplanet" => new NewznabIndexer
            {
                Name = "NZBPlanet",
                BaseUrl = "https://api.nzbplanet.net",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 30
            },
            "abnzb" => new NewznabIndexer
            {
                Name = "ABnzb",
                BaseUrl = "https://abnzb.com",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 35
            },
            "althub" => new NewznabIndexer
            {
                Name = "altHUB",
                BaseUrl = "https://api.althub.co.za",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 35
            },
            _ => null
        };
    }

    /// <summary>
    /// Gets all available preset names.
    /// </summary>
    public static IReadOnlyList<string> GetAvailablePresets() => new[]
    {
        "nzbgeek",
        "drunkenslug",
        "nzbfinder",
        "nzbplanet",
        "abnzb",
        "althub"
    };

    /// <summary>
    /// Common NZB categories for comics.
    /// </summary>
    public static class Categories
    {
        /// <summary>Comics category (7030)</summary>
        public const int Comics = 7030;

        /// <summary>Books/EBooks category (7000)</summary>
        public const int Books = 7000;

        /// <summary>Magazines category (7010)</summary>
        public const int Magazines = 7010;

        /// <summary>All comic-related categories</summary>
        public static readonly int[] ComicCategories = { Comics, Books };
    }
}
