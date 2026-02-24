using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that enriches cached discovery data with cover images.
/// 
/// WalkSoftly data doesn't include cover images, so this service periodically:
/// 1. Scans cached discovery weeks for issues missing cover images
/// 2. Tries LOCG for issue-specific covers first (via CoverFallbackService)
/// 3. Falls back to ComicVine volume (series) covers
/// 4. Updates the cached data with the enriched cover URLs
/// 
/// Priority:
/// 1. LOCG issue-specific cover (via CoverFallbackService)
/// 2. ComicVine volume cover (final fallback)
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
        var coverFallbackService = scope.ServiceProvider.GetRequiredService<ICoverFallbackService>();

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
        var locgHits = 0;
        var volumeHits = 0;

        foreach (var cachedWeek in cachedWeeks)
        {
            try
            {
                var (enrichedCount, locgCount, volumeCount) = await EnrichCachedWeekAsync(
                    cachedWeek, comicVineClient, coverFallbackService, dbContext, cancellationToken);
                totalEnriched += enrichedCount;
                locgHits += locgCount;
                volumeHits += volumeCount;
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
                "Cover enrichment complete: enriched {Count} issues ({LocgHits} from LOCG, {VolumeHits} from volume covers) across {Weeks} weeks",
                totalEnriched, locgHits, volumeHits, cachedWeeks.Count);
        }
    }

    private async Task<(int enrichedCount, int locgCount, int volumeCount)> EnrichCachedWeekAsync(
        Core.Entities.CachedDiscoveryWeek cachedWeek,
        IComicVineClient comicVineClient,
        ICoverFallbackService coverFallbackService,
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
            return (0, 0, 0);
        }

        if (issues == null || issues.Count == 0)
        {
            return (0, 0, 0);
        }

        var issuesMissingCovers = issues
            .Where(i => i.Image == null && i.Volume?.Id > 0)
            .ToList();

        if (issuesMissingCovers.Count == 0)
        {
            _logger.LogDebug(
                "Week {WeekStart}: all {Count} issues already have covers",
                cachedWeek.WeekStart, issues.Count);
            return (0, 0, 0);
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

        var volumeCoverLookup = new Dictionary<int, ComicVineImage>();
        if (volumeResult.Success && volumeResult.Results != null)
        {
            volumeCoverLookup = volumeResult.Results
                .Where(v => v.Image != null)
                .ToDictionary(v => v.Id, v => v.Image!);
        }
        else
        {
            _logger.LogWarning(
                "Failed to fetch volumes for cover enrichment: {Error}",
                volumeResult.Error ?? "Unknown error");
        }

        var enrichedCount = 0;
        var locgCount = 0;
        var volumeCount = 0;

        foreach (var issue in issues.Where(i => i.Image == null && i.Volume?.Id > 0))
        {
            var seriesName = issue.Volume?.Name ?? "";
            var issueNumber = issue.IssueNumber ?? "";
            string? volumeCoverUrl = null;

            if (volumeCoverLookup.TryGetValue(issue.Volume!.Id, out var volumeImage))
            {
                volumeCoverUrl = volumeImage.MediumUrl ?? volumeImage.SmallUrl;
            }

            var fallbackResult = await coverFallbackService.GetCoverAsync(
                seriesName,
                issueNumber,
                null, // Publisher not available in cached issue data
                volumeCoverUrl,
                cancellationToken);

            if (fallbackResult.Success && !string.IsNullOrEmpty(fallbackResult.CoverUrl))
            {
                issue.Image = new ComicVineImage
                {
                    MediumUrl = fallbackResult.CoverUrl,
                    SmallUrl = fallbackResult.CoverUrl,
                    OriginalUrl = fallbackResult.CoverUrl
                };
                enrichedCount++;

                if (fallbackResult.Source == CoverSource.LeagueOfComicGeeks)
                {
                    locgCount++;
                }
                else if (fallbackResult.Source == CoverSource.ComicVineVolume)
                {
                    volumeCount++;
                }
            }
        }

        if (enrichedCount > 0)
        {
            cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
            cachedWeek.LastRefreshed = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Week {WeekStart}: enriched {Count} issues ({Locg} LOCG, {Volume} volume)",
                cachedWeek.WeekStart, enrichedCount, locgCount, volumeCount);
        }

        return (enrichedCount, locgCount, volumeCount);
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
