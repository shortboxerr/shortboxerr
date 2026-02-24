using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
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
    private readonly TimeSpan _coverRefreshInterval = TimeSpan.FromDays(7);
    private readonly int _maxVolumesPerBatch = 50;
    private readonly int _maxIssuesPerRefreshBatch = 25;
    
    private DateTime _lastCoverRefresh = DateTime.MinValue;

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
                
                // Weekly check: re-query ComicVine for issues using fallback covers
                if (DateTime.UtcNow - _lastCoverRefresh > _coverRefreshInterval)
                {
                    await RefreshFallbackCoversFromComicVineAsync(stoppingToken);
                    _lastCoverRefresh = DateTime.UtcNow;
                }
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
                    
                    // Track LOCG covers for future ComicVine refresh checks
                    await TrackFallbackCoverAsync(
                        dbContext,
                        issue.Id,
                        issue.Volume!.Id,
                        seriesName,
                        issueNumber,
                        fallbackResult.CoverUrl,
                        fallbackResult.Source,
                        cachedWeek.WeekStart,
                        cancellationToken);
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

    /// <summary>
    /// Manually triggers ComicVine cover refresh check. Called from the API endpoint.
    /// </summary>
    public async Task TriggerCoverRefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual ComicVine cover refresh triggered");
        await RefreshFallbackCoversFromComicVineAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if ComicVine now has covers for issues that were previously using fallback covers.
    /// When ComicVine catches up, updates the cached data and clears the fallback cache.
    /// </summary>
    private async Task RefreshFallbackCoversFromComicVineAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();
        var coverFallbackService = scope.ServiceProvider.GetRequiredService<ICoverFallbackService>();

        // Get entries that haven't been checked recently (7+ days) or never checked
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var fallbackEntries = await dbContext.FallbackCoverEntries
            .Where(e => e.LastChecked == null || e.LastChecked < cutoffDate)
            .OrderBy(e => e.LastChecked ?? DateTime.MinValue)
            .Take(_maxIssuesPerRefreshBatch)
            .ToListAsync(cancellationToken);

        if (fallbackEntries.Count == 0)
        {
            _logger.LogDebug("No fallback cover entries to refresh");
            return;
        }

        _logger.LogInformation(
            "Checking ComicVine for {Count} issues with fallback covers",
            fallbackEntries.Count);

        var issueIds = fallbackEntries
            .Where(e => e.ComicVineIssueId > 0)
            .Select(e => e.ComicVineIssueId)
            .Distinct()
            .ToList();

        if (issueIds.Count == 0)
        {
            // No valid ComicVine IDs, mark as checked
            foreach (var entry in fallbackEntries)
            {
                entry.LastChecked = DateTime.UtcNow;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var issueResult = await comicVineClient.GetIssuesByIdsAsync(issueIds, cancellationToken);
        
        if (!issueResult.Success || issueResult.Results == null)
        {
            _logger.LogWarning(
                "Failed to fetch issues from ComicVine: {Error}",
                issueResult.Error ?? "Unknown error");
            return;
        }

        var issueLookup = issueResult.Results
            .Where(i => i.Image != null)
            .ToDictionary(i => i.Id);

        var updatedCount = 0;
        var entriesToRemove = new List<FallbackCoverEntry>();

        foreach (var entry in fallbackEntries)
        {
            entry.LastChecked = DateTime.UtcNow;

            if (issueLookup.TryGetValue(entry.ComicVineIssueId, out var issue) && issue.Image != null)
            {
                var coverUrl = issue.Image.MediumUrl ?? issue.Image.SmallUrl ?? issue.Image.OriginalUrl;
                if (!string.IsNullOrEmpty(coverUrl))
                {
                    // ComicVine now has a cover - update cached data
                    var updated = await UpdateCachedIssueCoverAsync(
                        dbContext, entry.WeekStart, entry.ComicVineIssueId, coverUrl, cancellationToken);

                    if (updated)
                    {
                        // Clear fallback cache
                        await coverFallbackService.ClearCacheAsync(
                            entry.SeriesName, entry.IssueNumber, cancellationToken);

                        entriesToRemove.Add(entry);
                        updatedCount++;

                        _logger.LogInformation(
                            "ComicVine now has cover for {Series} #{Issue}, updated cache",
                            entry.SeriesName, entry.IssueNumber);
                    }
                }
            }
        }

        // Remove entries that now have ComicVine covers
        if (entriesToRemove.Count > 0)
        {
            dbContext.FallbackCoverEntries.RemoveRange(entriesToRemove);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (updatedCount > 0)
        {
            _logger.LogInformation(
                "ComicVine cover refresh: {Updated} of {Checked} issues now have ComicVine covers",
                updatedCount, fallbackEntries.Count);
        }
    }

    /// <summary>
    /// Updates a specific issue's cover in the cached discovery week data.
    /// </summary>
    private async Task<bool> UpdateCachedIssueCoverAsync(
        ShortboxerrDbContext dbContext,
        DateTime weekStart,
        int comicVineIssueId,
        string newCoverUrl,
        CancellationToken cancellationToken)
    {
        var cachedWeek = await dbContext.CachedDiscoveryWeeks
            .FirstOrDefaultAsync(c => c.WeekStart == weekStart, cancellationToken);

        if (cachedWeek == null)
        {
            return false;
        }

        List<ComicVineIssue>? issues;
        try
        {
            issues = JsonSerializer.Deserialize<List<ComicVineIssue>>(cachedWeek.IssuesJson);
        }
        catch
        {
            return false;
        }

        if (issues == null)
        {
            return false;
        }

        var issue = issues.FirstOrDefault(i => i.Id == comicVineIssueId);
        if (issue == null)
        {
            return false;
        }

        issue.Image = new ComicVineImage
        {
            MediumUrl = newCoverUrl,
            SmallUrl = newCoverUrl,
            OriginalUrl = newCoverUrl
        };

        cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
        cachedWeek.LastRefreshed = DateTime.UtcNow;

        return true;
    }

    /// <summary>
    /// Tracks an issue that is using a fallback cover.
    /// Called when LOCG provides a cover during enrichment.
    /// </summary>
    internal async Task TrackFallbackCoverAsync(
        ShortboxerrDbContext dbContext,
        int comicVineIssueId,
        int comicVineVolumeId,
        string seriesName,
        string issueNumber,
        string fallbackCoverUrl,
        CoverSource source,
        DateTime weekStart,
        CancellationToken cancellationToken)
    {
        // Only track LOCG covers (not volume covers, which are a different type of fallback)
        if (source != CoverSource.LeagueOfComicGeeks)
        {
            return;
        }

        // Check if entry already exists
        var existing = await dbContext.FallbackCoverEntries
            .FirstOrDefaultAsync(e => 
                e.ComicVineIssueId == comicVineIssueId && 
                e.WeekStart == weekStart, 
                cancellationToken);

        if (existing != null)
        {
            existing.FallbackCoverUrl = fallbackCoverUrl;
            existing.FallbackSource = source.ToString();
        }
        else
        {
            dbContext.FallbackCoverEntries.Add(new FallbackCoverEntry
            {
                ComicVineIssueId = comicVineIssueId,
                ComicVineVolumeId = comicVineVolumeId,
                SeriesName = seriesName,
                IssueNumber = issueNumber,
                FallbackCoverUrl = fallbackCoverUrl,
                FallbackSource = source.ToString(),
                CreatedAt = DateTime.UtcNow,
                WeekStart = weekStart
            });
        }
    }
}
