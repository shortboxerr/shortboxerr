using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Metron;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that pre-enriches upcoming releases for monitored series.
/// 
/// This service runs slowly/ploddingly to respect Metron API rate limits:
/// 1. Gets all monitored series with ComicVine volume IDs
/// 2. For each series, pre-fetches Metron data (series lookup, issue list, issue details)
/// 3. Results are cached by MetronClient for 24 hours
/// 4. When the API endpoint is called, it returns cached data (fast)
/// 
/// This prevents the API from hammering Metron when multiple series need enrichment.
/// </summary>
public class UpcomingReleasesEnrichmentService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UpcomingReleasesEnrichmentService> _logger;
    
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _delayBetweenSeries = TimeSpan.FromSeconds(10);

    public UpcomingReleasesEnrichmentService(
        IServiceProvider serviceProvider,
        ILogger<UpcomingReleasesEnrichmentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Upcoming releases enrichment service starting. Check interval: {Interval}",
            _checkInterval);

        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnrichUpcomingReleasesForAllSeriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in upcoming releases enrichment service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Upcoming releases enrichment service stopping");
    }

    private async Task EnrichUpcomingReleasesForAllSeriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        var metronClient = scope.ServiceProvider.GetRequiredService<IMetronClient>();

        // Get all monitored series with ComicVine volume IDs
        var monitoredSeries = await dbContext.Series
            .Where(s => s.Monitored && s.ComicVineId.HasValue)
            .Select(s => new { s.Id, s.Title, s.ComicVineId })
            .ToListAsync(cancellationToken);

        if (monitoredSeries.Count == 0)
        {
            _logger.LogDebug("No monitored series with ComicVine IDs to enrich");
            return;
        }

        _logger.LogInformation(
            "Starting upcoming releases pre-enrichment for {Count} monitored series",
            monitoredSeries.Count);

        var enrichedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var series in monitoredSeries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var enriched = await PreEnrichSeriesAsync(
                    metronClient, 
                    series.ComicVineId!.Value, 
                    series.Title,
                    cancellationToken);

                if (enriched)
                    enrichedCount++;
                else
                    skippedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to pre-enrich upcoming releases for series '{Title}' (CV: {CvId})",
                    series.Title, series.ComicVineId);
                failedCount++;
            }

            // Delay between series to avoid hammering the API
            await Task.Delay(_delayBetweenSeries, cancellationToken);
        }

        _logger.LogInformation(
            "Upcoming releases pre-enrichment complete: {Enriched} enriched, {Skipped} skipped (cached), {Failed} failed",
            enrichedCount, skippedCount, failedCount);
    }

    /// <summary>
    /// Pre-enriches Metron data for a series by warming the cache.
    /// </summary>
    /// <returns>True if new data was fetched, false if already cached</returns>
    private async Task<bool> PreEnrichSeriesAsync(
        IMetronClient metronClient,
        int comicVineVolumeId,
        string seriesTitle,
        CancellationToken cancellationToken)
    {
        // Step 1: Get Metron series ID from CV volume ID
        var seriesResult = await metronClient.GetSeriesByCvIdAsync(
            comicVineVolumeId, 
            cancellationToken: cancellationToken);

        if (!seriesResult.Success || seriesResult.Series == null)
        {
            _logger.LogDebug(
                "No Metron series found for '{Title}' (CV: {CvId}): {Error}",
                seriesTitle, comicVineVolumeId, seriesResult.Error);
            return false;
        }

        // If this was from cache, the rest is likely cached too
        if (seriesResult.FromCache)
        {
            _logger.LogDebug(
                "Metron data already cached for '{Title}' (Metron: {MetronId})",
                seriesTitle, seriesResult.Series.Id);
            return false;
        }

        var metronSeriesId = seriesResult.Series.Id;

        // Step 2: Get issue list for the series
        var issueListResult = await metronClient.GetSeriesIssueListAsync(
            metronSeriesId, 
            cancellationToken: cancellationToken);

        if (!issueListResult.Success || issueListResult.Issues.Count == 0)
        {
            _logger.LogDebug(
                "No Metron issues found for '{Title}' (Metron: {MetronId})",
                seriesTitle, metronSeriesId);
            return true; // We did fetch new data (even if empty)
        }

        // Step 3: For upcoming issues (future store dates), fetch full details
        var today = DateTime.UtcNow.Date;
        var upcomingIssues = issueListResult.Issues
            .Where(i => i.StoreDate.HasValue && i.StoreDate.Value.Date >= today)
            .ToList();

        if (upcomingIssues.Count == 0)
        {
            _logger.LogDebug(
                "No upcoming issues for '{Title}' (Metron: {MetronId})",
                seriesTitle, metronSeriesId);
            return true;
        }

        _logger.LogDebug(
            "Pre-enriching {Count} upcoming issues for '{Title}'",
            upcomingIssues.Count, seriesTitle);

        foreach (var issue in upcomingIssues)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Fetch full issue details (this warms the cache)
            var issueResult = await metronClient.GetIssueByIdAsync(
                issue.Id, 
                cancellationToken: cancellationToken);

            if (issueResult.Success && issueResult.Issue != null)
            {
                _logger.LogDebug(
                    "Pre-cached Metron issue {MetronId}: {Series} #{Number}",
                    issue.Id, seriesTitle, issue.Number);
            }
        }

        _logger.LogInformation(
            "Pre-enriched upcoming releases for '{Title}': {Count} issues (Metron: {MetronId})",
            seriesTitle, upcomingIssues.Count, metronSeriesId);

        return true;
    }
}
