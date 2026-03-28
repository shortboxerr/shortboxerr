using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Metron;

/// <summary>
/// Implementation of IFallbackEnrichmentService using Metron as fallback source.
/// </summary>
public class FallbackEnrichmentService : IFallbackEnrichmentService
{
    private readonly IMetronClient _metronClient;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ILogger<FallbackEnrichmentService> _logger;

    public FallbackEnrichmentService(
        IMetronClient metronClient,
        ShortboxerrDbContext dbContext,
        ILogger<FallbackEnrichmentService> logger)
    {
        _metronClient = metronClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<FallbackEnrichmentResult> EnrichSeriesFromMetronAsync(
        string seriesTitle,
        string? publisher = null,
        bool createIfFound = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seriesTitle))
        {
            return new FallbackEnrichmentResult
            {
                Found = false,
                Reason = "Series title is required"
            };
        }

        try
        {
            // Note: Metron API doesn't have direct series search via IMetronClient.
            // This would require implementing SearchSeriesAsync in the MetronClient.
            // For now, we return a not-yet-implemented result.
            // 
            // TODO: Implement SearchSeriesAsync in IMetronClient using Metron's /series/ endpoint
            // and update this service to use it.

            return new FallbackEnrichmentResult
            {
                Found = false,
                Reason = "Metron series search not yet implemented. TODO: Add SearchSeriesAsync to IMetronClient"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching series from Metron: {Title}", seriesTitle);
            return new FallbackEnrichmentResult
            {
                Found = false,
                Reason = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<FallbackIssueEnrichmentResult> EnrichIssueFromMetronAsync(
        int seriesId,
        string issueNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
            if (series == null)
            {
                return new FallbackIssueEnrichmentResult
                {
                    Found = false,
                    Reason = "Series not found"
                };
            }

            MetronIssueResult? metronIssue = null;

            // Try Metron series ID lookup first (most reliable)
            // Note: This requires the series to have MetronSeriesId populated
            // which would come from EnrichSeriesFromMetronAsync (once implemented)
            
            // Fall back to issue search by series name
            var searchResult = await _metronClient.SearchIssueAsync(
                series.Title,
                issueNumber,
                cancellationToken: cancellationToken);

            if (searchResult.Success && searchResult.Issues.Any())
            {
                metronIssue = await _metronClient.GetIssueByIdAsync(
                    searchResult.Issues.First().Id,
                    cancellationToken: cancellationToken);
            }

            if (metronIssue == null || !metronIssue.Success)
            {
                return new FallbackIssueEnrichmentResult
                {
                    Found = false,
                    Reason = $"Issue {issueNumber} not found in Metron"
                };
            }

            var issueData = new FallbackIssueEnrichmentResult.IssueData
            {
                Number = metronIssue.Issue?.Number,
                Title = metronIssue.Issue?.Title,
                ReleaseDate = metronIssue.Issue?.CoverDate,
                CoverUrl = metronIssue.Issue?.ImageUrl,
                Description = metronIssue.Issue?.Description
            };

            return new FallbackIssueEnrichmentResult
            {
                Found = true,
                Issue = issueData,
                Source = "Metron"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching issue from Metron: Series {SeriesId}, Issue {IssueNumber}",
                seriesId, issueNumber);
            return new FallbackIssueEnrichmentResult
            {
                Found = false,
                Reason = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<BulkFallbackEnrichmentResult> BulkEnrichUnmatchedSeriesAsync(
        IProgress<FallbackEnrichmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Find all unmatched series
        var unmatchedSeries = await _dbContext.Series
            .Where(s => s.ComicVineId == null)
            .ToListAsync(cancellationToken);

        var result = new BulkFallbackEnrichmentResult
        {
            TotalProcessed = unmatchedSeries.Count
        };

        var currentProgress = new FallbackEnrichmentProgress
        {
            Total = unmatchedSeries.Count
        };

        foreach (var series in unmatchedSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentProgress.Current++;
            currentProgress.CurrentSeries = series.Title;
            progress?.Report(currentProgress);

            try
            {
                var enrichResult = await EnrichSeriesFromMetronAsync(
                    series.Title,
                    series.Publisher,
                    createIfFound: false,
                    cancellationToken);

                if (enrichResult.Found && enrichResult.ExternalId.HasValue)
                {
                    result.MatchesFound++;
                    currentProgress.MatchesFound++;

                    _logger.LogInformation(
                        "Matched unmatched series to Metron: {Title} (ID: {MetronId})",
                        series.Title, enrichResult.ExternalId);
                }

                result.Results.Add(new BulkEnrichmentItemResult
                {
                    SeriesId = series.Id,
                    SeriesTitle = series.Title,
                    Found = enrichResult.Found,
                    ExternalId = enrichResult.ExternalId,
                    Source = enrichResult.Source,
                    Reason = enrichResult.Reason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching series {SeriesId}: {Title}", series.Id, series.Title);
                result.Errors++;

                result.Results.Add(new BulkEnrichmentItemResult
                {
                    SeriesId = series.Id,
                    SeriesTitle = series.Title,
                    Found = false,
                    Reason = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Bulk Metron enrichment complete: {Total} processed, {Matches} matched, {Errors} errors",
            result.TotalProcessed, result.MatchesFound, result.Errors);

        return result;
    }
}
