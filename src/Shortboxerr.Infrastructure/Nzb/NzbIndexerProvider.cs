using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Provides access to configured NZB indexers and aggregated search functionality.
/// </summary>
public class NzbIndexerProvider : INzbIndexerProvider
{
    private readonly INewznabClient _newznabClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<NzbIndexerProvider>? _logger;

    private const string IndexersSettingsKey = "nzb_indexers";

    public NzbIndexerProvider(
        INewznabClient newznabClient,
        ISettingsService settingsService,
        ILogger<NzbIndexerProvider>? logger = null)
    {
        _newznabClient = newznabClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewznabIndexer>> GetIndexersAsync(CancellationToken cancellationToken = default)
    {
        var indexers = await _settingsService.GetAsync<List<NewznabIndexer>>(IndexersSettingsKey, new(), cancellationToken);
        return indexers ?? new List<NewznabIndexer>();
    }

    public async Task<IReadOnlyList<NewznabIndexer>> GetEnabledIndexersAsync(CancellationToken cancellationToken = default)
    {
        var indexers = await GetIndexersAsync(cancellationToken);
        return indexers
            .Where(i => i.Enabled)
            .OrderBy(i => i.Priority)
            .ToList();
    }

    public async Task<NewznabIndexer?> GetIndexerAsync(string id, CancellationToken cancellationToken = default)
    {
        var indexers = await GetIndexersAsync(cancellationToken);
        return indexers.FirstOrDefault(i => i.Id == id);
    }

    public async Task<NewznabIndexer> AddIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default)
    {
        // Ensure ID is set
        if (string.IsNullOrEmpty(indexer.Id))
        {
            indexer.Id = Guid.NewGuid().ToString();
        }

        var indexers = (await GetIndexersAsync(cancellationToken)).ToList();

        // Check for duplicate
        if (indexers.Any(i => i.Id == indexer.Id))
        {
            throw new InvalidOperationException($"Indexer with ID {indexer.Id} already exists");
        }

        indexers.Add(indexer);
        await _settingsService.SetAsync(IndexersSettingsKey, indexers, cancellationToken);

        _logger?.LogInformation("Added NZB indexer: {IndexerName} ({IndexerId})", indexer.Name, indexer.Id);
        return indexer;
    }

    public async Task<NewznabIndexer> UpdateIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default)
    {
        var indexers = (await GetIndexersAsync(cancellationToken)).ToList();
        var existingIndex = indexers.FindIndex(i => i.Id == indexer.Id);

        if (existingIndex < 0)
        {
            throw new InvalidOperationException($"Indexer with ID {indexer.Id} not found");
        }

        indexers[existingIndex] = indexer;
        await _settingsService.SetAsync(IndexersSettingsKey, indexers, cancellationToken);

        _logger?.LogInformation("Updated NZB indexer: {IndexerName} ({IndexerId})", indexer.Name, indexer.Id);
        return indexer;
    }

    public async Task<bool> DeleteIndexerAsync(string id, CancellationToken cancellationToken = default)
    {
        var indexers = (await GetIndexersAsync(cancellationToken)).ToList();
        var removed = indexers.RemoveAll(i => i.Id == id);

        if (removed > 0)
        {
            await _settingsService.SetAsync(IndexersSettingsKey, indexers, cancellationToken);
            _logger?.LogInformation("Deleted NZB indexer: {IndexerId}", id);
            return true;
        }

        return false;
    }

    public async Task<NewznabTestResult> TestIndexerAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default)
    {
        return await _newznabClient.TestConnectionAsync(indexer, cancellationToken);
    }

    public async Task<NzbAggregatedSearchResult> SearchAllAsync(NewznabSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var enabledIndexers = await GetEnabledIndexersAsync(cancellationToken);

        if (enabledIndexers.Count == 0)
        {
            _logger?.LogWarning("No enabled NZB indexers configured");
            return new NzbAggregatedSearchResult
            {
                Releases = Array.Empty<NewznabRelease>(),
                IndexerResults = Array.Empty<IndexerSearchResult>(),
                TotalResults = 0,
                IndexersSearched = 0,
                IndexersSuccessful = 0,
                Duration = stopwatch.Elapsed
            };
        }

        _logger?.LogInformation("Searching {Count} NZB indexers for: {Query}",
            enabledIndexers.Count, query.Query ?? query.Title ?? "(all)");

        // Search all indexers in parallel
        var searchTasks = enabledIndexers.Select(indexer => SearchIndexerAsync(indexer, query, cancellationToken));
        var results = await Task.WhenAll(searchTasks);

        stopwatch.Stop();

        // Aggregate results
        var allReleases = new List<NewznabRelease>();
        var indexerResults = new List<IndexerSearchResult>();
        var successCount = 0;

        foreach (var (indexer, result, duration) in results)
        {
            var indexerResult = new IndexerSearchResult
            {
                IndexerId = indexer.Id,
                IndexerName = indexer.Name,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                ReleaseCount = result.Releases.Count,
                Duration = duration
            };
            indexerResults.Add(indexerResult);

            if (result.Success)
            {
                successCount++;
                allReleases.AddRange(result.Releases);
            }
        }

        // Deduplicate by title (same release from multiple indexers)
        var uniqueReleases = DeduplicateReleases(allReleases);

        // Sort by quality score (for now, by age - newer is better)
        var sortedReleases = uniqueReleases
            .OrderBy(r => r.Age)
            .ThenByDescending(r => r.Size)
            .ToList();

        _logger?.LogInformation(
            "NZB search completed: {Total} releases from {Success}/{Total} indexers in {Duration}ms",
            sortedReleases.Count, successCount, enabledIndexers.Count, stopwatch.ElapsedMilliseconds);

        return new NzbAggregatedSearchResult
        {
            Releases = sortedReleases,
            IndexerResults = indexerResults,
            TotalResults = sortedReleases.Count,
            IndexersSearched = enabledIndexers.Count,
            IndexersSuccessful = successCount,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<(NewznabIndexer indexer, NewznabSearchResult result, TimeSpan duration)> SearchIndexerAsync(
        NewznabIndexer indexer,
        NewznabSearchQuery query,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _newznabClient.SearchAsync(indexer, query, cancellationToken);
            stopwatch.Stop();
            return (indexer, result, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "Error searching indexer {IndexerName}", indexer.Name);
            return (indexer, NewznabSearchResult.Error(ex.Message), stopwatch.Elapsed);
        }
    }

    private static List<NewznabRelease> DeduplicateReleases(List<NewznabRelease> releases)
    {
        // Group by normalized title and size (same file from different indexers)
        var grouped = releases
            .GroupBy(r => (NormalizeTitle(r.Title), r.Size))
            .Select(g => g.First()) // Take first occurrence (typically from highest priority indexer)
            .ToList();

        return grouped;
    }

    private static string NormalizeTitle(string title)
    {
        // Basic normalization for deduplication
        return title
            .ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Trim();
    }
}
