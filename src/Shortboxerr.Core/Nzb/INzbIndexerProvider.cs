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
                Priority = 25,
                IndexerType = NewznabIndexerType.Standard
            },
            "drunkenslug" => new NewznabIndexer
            {
                Name = "DrunkenSlug",
                BaseUrl = "https://api.drunkenslug.com",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 25,
                IndexerType = NewznabIndexerType.Standard
            },
            "nzbfinder" => new NewznabIndexer
            {
                Name = "NZBFinder",
                BaseUrl = "https://nzbfinder.ws",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 30,
                IndexerType = NewznabIndexerType.Standard
            },
            "nzbplanet" => new NewznabIndexer
            {
                Name = "NZBPlanet",
                BaseUrl = "https://api.nzbplanet.net",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 30,
                IndexerType = NewznabIndexerType.Standard
            },
            "abnzb" => new NewznabIndexer
            {
                Name = "ABnzb",
                BaseUrl = "https://abnzb.com",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 35,
                IndexerType = NewznabIndexerType.Standard
            },
            "althub" => new NewznabIndexer
            {
                Name = "altHUB",
                BaseUrl = "https://api.althub.co.za",
                ApiKey = apiKey,
                Categories = new List<int> { 7030, 7000 },
                Priority = 35,
                IndexerType = NewznabIndexerType.Standard
            },
            _ => null
        };
    }

    /// <summary>
    /// Creates an NZBHydra2 aggregator indexer configuration.
    /// NZBHydra2 is self-hosted, so the user provides their own URL.
    /// </summary>
    /// <param name="baseUrl">NZBHydra2 base URL (e.g., http://localhost:5076)</param>
    /// <param name="apiKey">NZBHydra2 API key</param>
    /// <param name="name">Optional custom name (defaults to "NZBHydra2")</param>
    /// <returns>Configured NZBHydra2 indexer</returns>
    public static NewznabIndexer CreateNzbHydra2(string baseUrl, string apiKey, string name = "NZBHydra2")
    {
        return new NewznabIndexer
        {
            Name = name,
            BaseUrl = baseUrl.TrimEnd('/'),
            ApiKey = apiKey,
            Categories = new List<int> { 7030, 7000 },
            Priority = 10, // High priority since it aggregates multiple indexers
            IsHydra = true,
            IndexerType = NewznabIndexerType.NzbHydra2
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
    /// Gets preset names grouped by type.
    /// </summary>
    public static IReadOnlyDictionary<NewznabIndexerType, IReadOnlyList<string>> GetPresetsByType() =>
        new Dictionary<NewznabIndexerType, IReadOnlyList<string>>
        {
            [NewznabIndexerType.Standard] = new[] { "nzbgeek", "drunkenslug", "nzbfinder", "nzbplanet", "abnzb", "althub" },
            [NewznabIndexerType.NzbHydra2] = Array.Empty<string>() // NZBHydra2 is self-hosted, no preset URL
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
