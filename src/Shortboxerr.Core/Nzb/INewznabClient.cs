namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Client for interacting with Newznab-compatible NZB indexer APIs.
/// Newznab is the standard API used by most NZB indexers (NZBgeek, DrunkenSlug, etc.).
/// </summary>
public interface INewznabClient
{
    /// <summary>
    /// Searches for NZB releases matching the query.
    /// </summary>
    /// <param name="indexer">The indexer configuration to search</param>
    /// <param name="query">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results containing NZB releases</returns>
    Task<NewznabSearchResult> SearchAsync(NewznabIndexer indexer, NewznabSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities of an indexer (supported categories, search types, etc.).
    /// </summary>
    /// <param name="indexer">The indexer configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Indexer capabilities</returns>
    Task<NewznabCapabilities> GetCapabilitiesAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to an indexer.
    /// </summary>
    /// <param name="indexer">The indexer configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test result with success/failure and details</returns>
    Task<NewznabTestResult> TestConnectionAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an NZB file from an indexer.
    /// </summary>
    /// <param name="indexer">The indexer configuration</param>
    /// <param name="nzbUrl">The NZB download URL (may be GUID or full URL)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>NZB file content as bytes</returns>
    Task<byte[]> DownloadNzbAsync(NewznabIndexer indexer, string nzbUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for a Newznab-compatible indexer.
/// </summary>
public class NewznabIndexer
{
    /// <summary>
    /// Unique identifier for this indexer.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the indexer.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Base URL of the indexer API (e.g., https://api.nzbgeek.info).
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Whether this indexer is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority for this indexer (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 50;

    /// <summary>
    /// Categories to search (Newznab category IDs).
    /// Common comic categories: 7030 (Comics/EBook), 7000 (Books)
    /// </summary>
    public List<int> Categories { get; set; } = new() { 7030, 7000 };

    /// <summary>
    /// Whether to enable early download (before full retention).
    /// </summary>
    public bool EarlyDownload { get; set; } = false;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Additional query parameters to append to requests.
    /// </summary>
    public Dictionary<string, string> AdditionalParameters { get; set; } = new();

    /// <summary>
    /// Whether this indexer is an NZBHydra2 aggregator.
    /// When true, results include backend indexer metadata.
    /// </summary>
    public bool IsHydra { get; set; } = false;

    /// <summary>
    /// Indexer type for display/categorization purposes.
    /// </summary>
    public NewznabIndexerType IndexerType { get; set; } = NewznabIndexerType.Standard;
}

/// <summary>
/// Type of Newznab indexer.
/// </summary>
public enum NewznabIndexerType
{
    /// <summary>
    /// Standard Newznab indexer (NZBgeek, DrunkenSlug, etc.)
    /// </summary>
    Standard = 0,

    /// <summary>
    /// NZBHydra2 aggregator (aggregates multiple backend indexers)
    /// </summary>
    NzbHydra2 = 1
}

/// <summary>
/// Search query parameters for Newznab API.
/// </summary>
public class NewznabSearchQuery
{
    /// <summary>
    /// Free-text search query.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Series/book name for targeted search.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Issue/episode number.
    /// </summary>
    public string? Episode { get; set; }

    /// <summary>
    /// Season/volume number.
    /// </summary>
    public string? Season { get; set; }

    /// <summary>
    /// Year of publication.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Category IDs to search (overrides indexer default if set).
    /// </summary>
    public List<int>? Categories { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Offset for pagination.
    /// </summary>
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Minimum age in days.
    /// </summary>
    public int? MinAge { get; set; }

    /// <summary>
    /// Maximum age in days.
    /// </summary>
    public int? MaxAge { get; set; }

    /// <summary>
    /// Minimum size in bytes.
    /// </summary>
    public long? MinSize { get; set; }

    /// <summary>
    /// Maximum size in bytes.
    /// </summary>
    public long? MaxSize { get; set; }
}

/// <summary>
/// Result of a Newznab search operation.
/// </summary>
public class NewznabSearchResult
{
    /// <summary>
    /// Whether the search was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if not successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// HTTP status code from the request.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Found releases.
    /// </summary>
    public IReadOnlyList<NewznabRelease> Releases { get; init; } = Array.Empty<NewznabRelease>();

    /// <summary>
    /// Total results available (may be more than returned).
    /// </summary>
    public int TotalResults { get; init; }

    /// <summary>
    /// Offset used in the request.
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// Time taken for the search.
    /// </summary>
    public TimeSpan Duration { get; init; }

    public static NewznabSearchResult Ok(IReadOnlyList<NewznabRelease> releases, int totalResults, int offset, TimeSpan duration)
    {
        return new NewznabSearchResult
        {
            Success = true,
            Releases = releases,
            TotalResults = totalResults,
            Offset = offset,
            Duration = duration
        };
    }

    public static NewznabSearchResult Error(string message, int? statusCode = null)
    {
        return new NewznabSearchResult
        {
            Success = false,
            ErrorMessage = message,
            StatusCode = statusCode
        };
    }
}

/// <summary>
/// An NZB release from a Newznab indexer.
/// </summary>
public record NewznabRelease
{
    /// <summary>
    /// Unique identifier (GUID) for this release.
    /// </summary>
    public required string Guid { get; init; }

    /// <summary>
    /// Release title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// URL to download the NZB file.
    /// </summary>
    public required string NzbUrl { get; init; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Publication date on the indexer.
    /// </summary>
    public DateTime PublishedDate { get; init; }

    /// <summary>
    /// Category IDs assigned by the indexer.
    /// </summary>
    public List<int> Categories { get; init; } = new();

    /// <summary>
    /// Category names assigned by the indexer.
    /// </summary>
    public List<string> CategoryNames { get; init; } = new();

    /// <summary>
    /// Indexer name/identifier (the configured indexer that returned this result).
    /// </summary>
    public string? IndexerName { get; init; }

    /// <summary>
    /// Indexer ID (the configured indexer that returned this result).
    /// </summary>
    public string? IndexerId { get; init; }

    /// <summary>
    /// Age in days since posting.
    /// </summary>
    public int Age => (int)(DateTime.UtcNow - PublishedDate).TotalDays;

    /// <summary>
    /// URL to the release info page on the indexer.
    /// </summary>
    public string? InfoUrl { get; init; }

    /// <summary>
    /// Poster/uploader name.
    /// </summary>
    public string? Poster { get; init; }

    /// <summary>
    /// Group the release belongs to.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Number of grabs/downloads.
    /// </summary>
    public int? Grabs { get; init; }

    /// <summary>
    /// Number of files in the release.
    /// </summary>
    public int? Files { get; init; }

    /// <summary>
    /// Password status (0=none, 1=password protected).
    /// </summary>
    public int? PasswordStatus { get; init; }

    /// <summary>
    /// Additional attributes from the indexer.
    /// </summary>
    public Dictionary<string, string> Attributes { get; init; } = new();

    #region NZBHydra2-specific Properties

    /// <summary>
    /// Whether this result came from an NZBHydra2 aggregator.
    /// </summary>
    public bool IsFromHydra { get; init; }

    /// <summary>
    /// The backend indexer name (from NZBHydra2's hydraIndexerName attribute).
    /// Only populated when IsFromHydra is true.
    /// </summary>
    public string? HydraIndexerName { get; init; }

    /// <summary>
    /// The backend indexer's internal ID in NZBHydra2.
    /// Only populated when IsFromHydra is true.
    /// </summary>
    public string? HydraIndexerId { get; init; }

    /// <summary>
    /// The original GUID from the backend indexer (before NZBHydra2 wrapping).
    /// Only populated when IsFromHydra is true.
    /// </summary>
    public string? HydraOriginalGuid { get; init; }

    /// <summary>
    /// Score/priority assigned by NZBHydra2 for this result.
    /// Higher scores indicate better priority. Only populated when IsFromHydra is true.
    /// </summary>
    public int? HydraScore { get; init; }

    /// <summary>
    /// The backend indexer host (from NZBHydra2's hydraIndexerHost attribute).
    /// Only populated when IsFromHydra is true.
    /// </summary>
    public string? HydraIndexerHost { get; init; }

    #endregion
}

/// <summary>
/// Capabilities of a Newznab indexer.
/// </summary>
public class NewznabCapabilities
{
    /// <summary>
    /// Whether caps were successfully retrieved.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if not successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Server information.
    /// </summary>
    public NewznabServerInfo? Server { get; init; }

    /// <summary>
    /// Supported search types.
    /// </summary>
    public NewznabSearchCapabilities Searching { get; init; } = new();

    /// <summary>
    /// Available categories.
    /// </summary>
    public IReadOnlyList<NewznabCategory> Categories { get; init; } = Array.Empty<NewznabCategory>();

    /// <summary>
    /// API limits.
    /// </summary>
    public NewznabLimits Limits { get; init; } = new();
}

/// <summary>
/// Server information from capabilities response.
/// </summary>
public class NewznabServerInfo
{
    public string? Version { get; init; }
    public string? Title { get; init; }
    public string? Strapline { get; init; }
    public string? Email { get; init; }
    public string? Url { get; init; }
}

/// <summary>
/// Search capabilities.
/// </summary>
public class NewznabSearchCapabilities
{
    public bool SearchAvailable { get; init; }
    public bool TvSearchAvailable { get; init; }
    public bool MovieSearchAvailable { get; init; }
    public bool MusicSearchAvailable { get; init; }
    public bool BookSearchAvailable { get; init; }
    public bool AudioSearchAvailable { get; init; }
}

/// <summary>
/// API limits from capabilities.
/// </summary>
public class NewznabLimits
{
    public int Max { get; init; } = 100;
    public int Default { get; init; } = 100;
}

/// <summary>
/// A category from the indexer.
/// </summary>
public class NewznabCategory
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<NewznabCategory> SubCategories { get; init; } = Array.Empty<NewznabCategory>();
}

/// <summary>
/// Result of testing an indexer connection.
/// </summary>
public record NewznabTestResult
{
    /// <summary>
    /// Whether the test was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Status message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Indexer capabilities (if retrieved).
    /// </summary>
    public NewznabCapabilities? Capabilities { get; init; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; init; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Whether the indexer was detected as an NZBHydra2 aggregator.
    /// </summary>
    public bool IsHydra { get; init; }

    public static NewznabTestResult Ok(string message, NewznabCapabilities? capabilities = null, long responseTimeMs = 0)
    {
        return new NewznabTestResult
        {
            Success = true,
            Message = message,
            Capabilities = capabilities,
            ResponseTimeMs = responseTimeMs
        };
    }

    public static NewznabTestResult Failed(string message, int? statusCode = null)
    {
        return new NewznabTestResult
        {
            Success = false,
            Message = message,
            StatusCode = statusCode
        };
    }
}
