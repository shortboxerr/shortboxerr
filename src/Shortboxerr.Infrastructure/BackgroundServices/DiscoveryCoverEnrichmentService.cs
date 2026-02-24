using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that enriches cached discovery data with cover images from ComicVine.
/// 
/// WalkSoftly data doesn't include cover images, so this service periodically:
/// 1. Scans cached discovery weeks for issues missing cover images
/// 2. Batch fetches volume covers from ComicVine
/// 3. Updates the cached data with the enriched cover URLs
/// 
/// This runs independently of the main discovery refresh to avoid rate limiting
/// and to allow gradual enrichment over time.
/// </summary>
public class DiscoveryCoverEnrichmentService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DiscoveryCoverEnrichmentService> _logger;
    
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(2);
    private readonly int _maxVolumesPerBatch = 50;

    public DiscoveryCoverEnrichmentService(
        IServiceProvider serviceProvider,
        ILogger<DiscoveryCoverEnrichmentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Cover enrichment service starting. Check interval: {Interval}",
            _checkInterval);

        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnrichMissingCoversAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cover enrichment service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Cover enrichment service stopping");
    }

    private async Task EnrichMissingCoversAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();

        var cachedWeeks = await dbContext.CachedDiscoveryWeeks
            .Where(c => c.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(c => c.WeekStart)
            .ToListAsync(cancellationToken);

        if (cachedWeeks.Count == 0)
        {
            _logger.LogDebug("No cached discovery weeks to enrich");
            return;
        }

        var totalEnriched = 0;

        foreach (var cachedWeek in cachedWeeks)
        {
            try
            {
                var enrichedCount = await EnrichCachedWeekAsync(
                    cachedWeek, comicVineClient, dbContext, cancellationToken);
                totalEnriched += enrichedCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to enrich cached week {WeekStart}",
                    cachedWeek.WeekStart);
            }
        }

        if (totalEnriched > 0)
        {
            _logger.LogInformation(
                "Cover enrichment complete: enriched {Count} issues across {Weeks} weeks",
                totalEnriched, cachedWeeks.Count);
        }
    }

    private async Task<int> EnrichCachedWeekAsync(
        Core.Entities.CachedDiscoveryWeek cachedWeek,
        IComicVineClient comicVineClient,
        ShortboxerrDbContext dbContext,
        CancellationToken cancellationToken)
    {
        List<ComicVineIssue>? issues;
        try
        {
            issues = JsonSerializer.Deserialize<List<ComicVineIssue>>(cachedWeek.IssuesJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize cached issues for week {WeekStart}",
                cachedWeek.WeekStart);
            return 0;
        }

        if (issues == null || issues.Count == 0)
        {
            return 0;
        }

        var issuesMissingCovers = issues
            .Where(i => i.Image == null && i.Volume?.Id > 0)
            .ToList();

        if (issuesMissingCovers.Count == 0)
        {
            _logger.LogDebug(
                "Week {WeekStart}: all {Count} issues already have covers",
                cachedWeek.WeekStart, issues.Count);
            return 0;
        }

        _logger.LogInformation(
            "Week {WeekStart}: {Missing} of {Total} issues missing covers",
            cachedWeek.WeekStart, issuesMissingCovers.Count, issues.Count);

        var volumeIds = issuesMissingCovers
            .Select(i => i.Volume!.Id)
            .Distinct()
            .Take(_maxVolumesPerBatch)
            .ToList();

        var volumeResult = await comicVineClient.GetVolumesByIdsAsync(volumeIds, cancellationToken);

        if (!volumeResult.Success || volumeResult.Results == null)
        {
            _logger.LogWarning(
                "Failed to fetch volumes for cover enrichment: {Error}",
                volumeResult.Error ?? "Unknown error");
            return 0;
        }

        var volumeCoverLookup = volumeResult.Results
            .Where(v => v.Image != null)
            .ToDictionary(v => v.Id, v => v.Image!);

        var enrichedCount = 0;

        foreach (var issue in issues.Where(i => i.Image == null && i.Volume?.Id > 0))
        {
            if (volumeCoverLookup.TryGetValue(issue.Volume!.Id, out var volumeImage))
            {
                issue.Image = volumeImage;
                enrichedCount++;
            }
        }

        if (enrichedCount > 0)
        {
            cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
            cachedWeek.LastRefreshed = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Week {WeekStart}: enriched {Count} issues with volume covers",
                cachedWeek.WeekStart, enrichedCount);
        }

        return enrichedCount;
    }

    /// <summary>
    /// Manually triggers cover enrichment. Called from the API endpoint.
    /// </summary>
    public async Task TriggerEnrichmentAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual cover enrichment triggered");
        await EnrichMissingCoversAsync(cancellationToken);
    }
}
