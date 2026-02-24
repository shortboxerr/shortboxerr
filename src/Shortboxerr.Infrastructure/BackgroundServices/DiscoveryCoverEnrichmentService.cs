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
/// 2. Tries Metron for issue-specific covers first (via CoverFallbackService)
/// 3. Falls back to ComicVine volume (series) covers
/// 4. Updates the cached data with the enriched cover URLs
/// 
/// Priority:
/// 1. Metron issue-specific cover (via CoverFallbackService with CV ID lookup)
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
    private readonly TimeSpan _notFoundCooldown = TimeSpan.FromDays(7);
    private readonly int _maxVolumesPerBatch = 50;
    private readonly int _maxIssuesPerRefreshBatch = 25;
    
    private DateTime _lastCoverRefresh = DateTime.MinValue;
    
    // Stats for this run
    private int _skippedHasComicVine;
    private int _skippedRecentlyChecked;
    private int _skippedAlreadyEnriched;

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
        // Reset run stats
        _skippedHasComicVine = 0;
        _skippedRecentlyChecked = 0;
        _skippedAlreadyEnriched = 0;
        
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();
        var coverFallbackService = scope.ServiceProvider.GetRequiredService<ICoverFallbackService>();
        var coverService = scope.ServiceProvider.GetRequiredService<ICoverService>();

        var allCachedWeeks = await dbContext.CachedDiscoveryWeeks
            .Where(c => c.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        if (allCachedWeeks.Count == 0)
        {
            _logger.LogDebug("No cached discovery weeks to enrich");
            return;
        }

        // Order: current week first, then future weeks (ascending), then past weeks (descending)
        // Rationale: Future issues are more likely to need Metron enrichment since ComicVine
        // may not have indexed them yet
        var currentWeekStart = GetWeekStart(DateTime.UtcNow);
        var cachedWeeks = allCachedWeeks
            .OrderBy(c => c.WeekStart == currentWeekStart ? 0 : 1) // Current week first
            .ThenBy(c => c.WeekStart >= currentWeekStart ? 0 : 1) // Then future weeks before past
            .ThenBy(c => c.WeekStart >= currentWeekStart ? c.WeekStart : DateTime.MaxValue) // Future ascending
            .ThenByDescending(c => c.WeekStart < currentWeekStart ? c.WeekStart : DateTime.MinValue) // Past descending
            .ToList();
        
        var weekOrder = string.Join(", ", cachedWeeks.Select(w => w.WeekStart.ToString("yyyy-MM-dd")));
        _logger.LogInformation(
            "Processing {Count} cached weeks for cover enrichment. Order: current week ({CurrentWeek}), then future, then past. Weeks: [{WeekOrder}]",
            cachedWeeks.Count, currentWeekStart.ToString("yyyy-MM-dd"), weekOrder);

        var totalEnriched = 0;
        var comicVineHits = 0;
        var metronHits = 0;
        var volumeHits = 0;
        var notFoundCount = 0;
        var weekIndex = 0;

        foreach (var cachedWeek in cachedWeeks)
        {
            weekIndex++;
            _logger.LogDebug("Processing week {Index}/{Total}: {WeekStart}", 
                weekIndex, cachedWeeks.Count, cachedWeek.WeekStart.ToString("yyyy-MM-dd"));
            try
            {
                var (enrichedCount, cvCount, metronCount, volumeCount, notFound) = await EnrichCachedWeekAsync(
                    cachedWeek, comicVineClient, coverFallbackService, coverService, dbContext, cancellationToken);
                totalEnriched += enrichedCount;
                comicVineHits += cvCount;
                metronHits += metronCount;
                volumeHits += volumeCount;
                notFoundCount += notFound;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to enrich cached week {WeekStart}",
                    cachedWeek.WeekStart);
            }
        }

        _logger.LogInformation(
            "Cover enrichment complete across {Weeks} weeks: enriched {Count} (ComicVine: {CV}, Metron: {Metron}, volume: {Volume}), not found: {NotFound}, skipped: {HasCV} have CV / {Recent} recently checked / {Already} already enriched",
            cachedWeeks.Count, totalEnriched, comicVineHits, metronHits, volumeHits, notFoundCount, 
            _skippedHasComicVine, _skippedRecentlyChecked, _skippedAlreadyEnriched);
    }

    private async Task<(int enrichedCount, int comicVineCount, int metronCount, int volumeCount, int notFoundCount)> EnrichCachedWeekAsync(
        Core.Entities.CachedDiscoveryWeek cachedWeek,
        IComicVineClient comicVineClient,
        ICoverFallbackService coverFallbackService,
        ICoverService coverService,
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
            return (0, 0, 0, 0, 0);
        }

        if (issues == null || issues.Count == 0)
        {
            return (0, 0, 0, 0, 0);
        }

        // First pass: identify issues that need tagging
        // - Issues with Image but no CoverSource are legacy data (likely volume fallbacks)
        // - Issues with CoverSource = "ComicVine" or "Metron" are properly tagged
        var dataModified = false;
        
        // Log stats about issue state before processing
        var issuesWithImage = issues.Count(i => i.Image != null);
        var issuesNoSource = issues.Count(i => i.Image != null && string.IsNullOrEmpty(i.CoverSource));
        var issuesNone = issues.Count(i => i.EnrichmentStatus == CoverEnrichmentStatus.None);
        var sourceValues = issues
            .Where(i => i.Image != null)
            .GroupBy(i => i.CoverSource ?? "(null)")
            .Select(g => $"{g.Key}:{g.Count()}")
            .ToList();
        _logger.LogInformation(
            "Week {WeekStart}: {Total} issues, {WithImage} have images ({Sources}), {NoneStatus} have None status",
            cachedWeek.WeekStart.ToString("yyyy-MM-dd"), issues.Count, issuesWithImage, 
            string.Join(", ", sourceValues), issuesNone);

        // Mark legacy issues (have image, no source tag, no enrichment status) as volume fallbacks
        // These are issues from before we added proper source tracking
        var legacyCount = 0;
        foreach (var issue in issues.Where(i => i.Image != null && 
                                                string.IsNullOrEmpty(i.CoverSource) && 
                                                i.EnrichmentStatus == CoverEnrichmentStatus.None))
        {
            // Assume these are volume fallbacks - they need real issue covers
            issue.CoverSource = "VolumeFallback";
            issue.EnrichmentStatus = CoverEnrichmentStatus.HasVolumeFallback;
            dataModified = true;
            legacyCount++;
        }
        
        if (legacyCount > 0)
        {
            _logger.LogInformation(
                "Week {WeekStart}: Marked {Count} legacy issues as VolumeFallback",
                cachedWeek.WeekStart.ToString("yyyy-MM-dd"), legacyCount);
        }

        // Find issues that need enrichment:
        // - Issues with no cover at all
        // - Issues with only volume fallback covers (need real issue covers)
        // - Issues marked as HasVolumeFallback (need real issue covers)
        var issuesToProcess = issues
            .Where(i => i.Volume?.Id > 0)
            .Where(i => i.Image == null || 
                        i.CoverSource == "VolumeFallback" || 
                        i.EnrichmentStatus == CoverEnrichmentStatus.HasVolumeFallback)
            .Where(i => ShouldAttemptEnrichment(i))
            .ToList();

        if (issuesToProcess.Count == 0)
        {
            if (dataModified)
            {
                cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
                cachedWeek.LastRefreshed = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            
            _logger.LogDebug(
                "Week {WeekStart}: no issues need enrichment ({Total} total, {HasCV} have CV covers, {RecentlyChecked} recently checked)",
                cachedWeek.WeekStart, issues.Count, _skippedHasComicVine, _skippedRecentlyChecked);
            return (0, 0, 0, 0, 0);
        }

        _logger.LogInformation(
            "Week {WeekStart}: processing {Count} issues for enrichment (skipped: {HasCV} have CV, {RecentlyChecked} recently checked, {AlreadyEnriched} already enriched)",
            cachedWeek.WeekStart, issuesToProcess.Count, _skippedHasComicVine, _skippedRecentlyChecked, _skippedAlreadyEnriched);

        // Batch fetch volumes for fallback covers
        var volumeIds = issuesToProcess
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

        // Batch fetch issue-specific covers from ComicVine for issues with IDs
        var issueIds = issuesToProcess
            .Where(i => i.Id > 0)
            .Select(i => i.Id)
            .Distinct()
            .Take(_maxVolumesPerBatch)
            .ToList();
        
        var issueCoverLookup = new Dictionary<int, ComicVineImage>();
        if (issueIds.Count > 0)
        {
            _logger.LogDebug("Fetching issue-specific covers from ComicVine for {Count} issues", issueIds.Count);
            var issueResult = await comicVineClient.GetIssuesByIdsAsync(issueIds, cancellationToken);
            
            if (issueResult.Success && issueResult.Results != null)
            {
                issueCoverLookup = issueResult.Results
                    .Where(i => i.Image != null)
                    .ToDictionary(i => i.Id, i => i.Image!);
                
                _logger.LogInformation(
                    "ComicVine returned covers for {Found}/{Requested} issues",
                    issueCoverLookup.Count, issueIds.Count);
            }
        }

        var enrichedCount = 0;
        var comicVineCount = 0;
        var metronCount = 0;
        var volumeCount = 0;
        var notFoundCount = 0;

        foreach (var issue in issuesToProcess)
        {
            var seriesName = issue.Volume?.Name ?? "";
            var issueNumber = issue.IssueNumber ?? "";
            string? volumeCoverUrl = null;

            if (volumeCoverLookup.TryGetValue(issue.Volume!.Id, out var volumeImage))
            {
                volumeCoverUrl = volumeImage.MediumUrl ?? volumeImage.SmallUrl;
            }

            // Mark that we're attempting enrichment
            issue.LastEnrichmentAttempt = DateTime.UtcNow;

            // Priority 1: Check if ComicVine has issue-specific cover
            if (issue.Id > 0 && issueCoverLookup.TryGetValue(issue.Id, out var cvIssueImage))
            {
                var cvCoverUrl = cvIssueImage.MediumUrl ?? cvIssueImage.SmallUrl;
                if (!string.IsNullOrEmpty(cvCoverUrl))
                {
                    issue.Image = cvIssueImage;
                    issue.EnrichmentStatus = CoverEnrichmentStatus.HasComicVineCover;
                    issue.CoverSource = "ComicVine";
                    comicVineCount++;
                    enrichedCount++;
                    dataModified = true;
                    continue;
                }
            }

            // Priority 2: Try Metron
            CoverFallbackResult fallbackResult;
            if (issue.Id > 0)
            {
                fallbackResult = await coverFallbackService.GetCoverByCvIdAsync(
                    issue.Id,
                    volumeCoverUrl,
                    cancellationToken);
            }
            else
            {
                // Fall back to series name/issue number search
                fallbackResult = await coverFallbackService.GetCoverAsync(
                    seriesName,
                    issueNumber,
                    null,
                    volumeCoverUrl,
                    cancellationToken);
            }

            if (fallbackResult.Success && !string.IsNullOrEmpty(fallbackResult.CoverUrl))
            {
                // Download cover to local cache for Metron covers
                if (fallbackResult.Source == CoverSource.Metron && issue.Id > 0)
                {
                    var downloadResult = await coverService.DownloadExternalCoverAsync(
                        fallbackResult.CoverUrl,
                        CoverType.Discovery,
                        issue.Id,
                        CoverCacheSource.Metron,
                        CoverSize.Medium,
                        cancellationToken);
                    
                    if (downloadResult.Success && !string.IsNullOrEmpty(downloadResult.FilePath))
                    {
                        // Use local path for the cover URL
                        issue.Image = new ComicVineImage
                        {
                            MediumUrl = $"/api/v1/covers/discovery/{issue.Id}/medium",
                            SmallUrl = $"/api/v1/covers/discovery/{issue.Id}/small",
                            OriginalUrl = fallbackResult.CoverUrl
                        };
                        issue.EnrichmentStatus = CoverEnrichmentStatus.Enriched;
                        issue.CoverSource = "Metron";
                        metronCount++;
                        enrichedCount++;
                        dataModified = true;

                        // Track Metron covers for future ComicVine refresh checks
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
                    else
                    {
                        // Download failed, store URL directly as fallback
                        issue.Image = new ComicVineImage
                        {
                            MediumUrl = fallbackResult.CoverUrl,
                            SmallUrl = fallbackResult.CoverUrl,
                            OriginalUrl = fallbackResult.CoverUrl
                        };
                        issue.EnrichmentStatus = CoverEnrichmentStatus.Enriched;
                        issue.CoverSource = "Metron";
                        metronCount++;
                        enrichedCount++;
                        dataModified = true;
                    }
                }
                else if (fallbackResult.Source == CoverSource.ComicVineVolume)
                {
                    // Volume covers are NOT issue-specific covers - they're series covers
                    // If issue doesn't have any image, apply the volume cover as fallback
                    // But mark as HasVolumeFallback so we retry for real issue covers later
                    if (issue.Image == null)
                    {
                        issue.Image = new ComicVineImage
                        {
                            MediumUrl = fallbackResult.CoverUrl,
                            SmallUrl = fallbackResult.CoverUrl,
                            OriginalUrl = fallbackResult.CoverUrl
                        };
                        issue.CoverSource = "VolumeFallback";
                    }
                    // Keep or set status to HasVolumeFallback - still needs real issue cover
                    issue.EnrichmentStatus = CoverEnrichmentStatus.HasVolumeFallback;
                    volumeCount++;
                    dataModified = true;
                }
            }
            else
            {
                // No cover found at all - mark as NotFound to apply cooldown
                issue.EnrichmentStatus = CoverEnrichmentStatus.NotFound;
                notFoundCount++;
                dataModified = true;
            }
        }

        if (dataModified)
        {
            cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
            cachedWeek.LastRefreshed = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (enrichedCount > 0)
            {
                _logger.LogInformation(
                    "Week {WeekStart}: enriched {Count} issues ({Metron} Metron, {Volume} volume), {NotFound} not found",
                    cachedWeek.WeekStart, enrichedCount, metronCount, volumeCount, notFoundCount);
            }
        }

        return (enrichedCount, comicVineCount, metronCount, volumeCount, notFoundCount);
    }

    /// <summary>
    /// Determines if enrichment should be attempted for an issue.
    /// </summary>
    private bool ShouldAttemptEnrichment(ComicVineIssue issue)
    {
        // Already has ComicVine issue cover (not volume fallback)
        if (issue.EnrichmentStatus == CoverEnrichmentStatus.HasComicVineCover)
        {
            _skippedHasComicVine++;
            return false;
        }

        // Already enriched with Metron cover
        if (issue.EnrichmentStatus == CoverEnrichmentStatus.Enriched)
        {
            _skippedAlreadyEnriched++;
            return false;
        }

        // NotFound but cooldown hasn't elapsed
        if (issue.EnrichmentStatus == CoverEnrichmentStatus.NotFound && 
            issue.LastEnrichmentAttempt.HasValue)
        {
            var timeSinceAttempt = DateTime.UtcNow - issue.LastEnrichmentAttempt.Value;
            if (timeSinceAttempt < _notFoundCooldown)
            {
                _skippedRecentlyChecked++;
                return false;
            }
        }
        
        // HasVolumeFallback - retry periodically (same cooldown as NotFound)
        if (issue.EnrichmentStatus == CoverEnrichmentStatus.HasVolumeFallback && 
            issue.LastEnrichmentAttempt.HasValue)
        {
            var timeSinceAttempt = DateTime.UtcNow - issue.LastEnrichmentAttempt.Value;
            if (timeSinceAttempt < _notFoundCooldown)
            {
                _skippedRecentlyChecked++;
                return false;
            }
        }
        
        // None - always try to enrich
        return true;
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
    /// Called when Metron provides a cover during enrichment.
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
        // Only track Metron covers (not volume covers, which are a different type of fallback)
        if (source != CoverSource.Metron)
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

    /// <summary>
    /// Gets the Monday of the week containing the given date.
    /// </summary>
    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
