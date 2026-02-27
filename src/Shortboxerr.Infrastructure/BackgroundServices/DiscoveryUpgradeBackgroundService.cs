using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.WalkSoftly;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that upgrades interim enrichment data to authoritative ComicVine data.
/// 
/// Part of EPIC 11.27 - Pull List Data Flow Refactoring.
/// 
/// This service periodically:
/// 1. Checks cached discovery weeks for issues that are not yet finalized
/// 2. Re-queries WalkSoftly to see if ComicVine issue IDs have become available
/// 3. For issues with newly available CV IDs, fetches full ComicVine data
/// 4. Upgrades enrichment status from MetronInterim/Pending to ComicVineFinalized
/// 
/// Interval: Every 4 hours (matching Mylar3's refresh interval)
/// </summary>
public class DiscoveryUpgradeBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DiscoveryUpgradeBackgroundService> _logger;
    
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _delayBetweenWeeks = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _settingsCheckInterval = TimeSpan.FromMinutes(15);
    private DateTime _lastUpgradeCheck = DateTime.MinValue;

    public DiscoveryUpgradeBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DiscoveryUpgradeBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Discovery upgrade service starting");

        try
        {
            await Task.Delay(_initialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Discovery upgrade service cancelled during startup delay");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get settings to check if enabled and get interval
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetAsync<PullListSettings>("pulllist", new(), stoppingToken)
                    ?? new PullListSettings();

                if (!settings.DiscoveryUpgradeEnabled)
                {
                    _logger.LogDebug("Discovery upgrade service is disabled");
                    await Task.Delay(_settingsCheckInterval, stoppingToken);
                    continue;
                }

                var checkInterval = TimeSpan.FromHours(settings.DiscoveryUpgradeIntervalHours);

                // Check if enough time has passed since last upgrade
                if (DateTime.UtcNow - _lastUpgradeCheck >= checkInterval)
                {
                    _logger.LogDebug("Starting discovery upgrade check (interval: {Interval}h)", settings.DiscoveryUpgradeIntervalHours);
                    await CheckAndUpgradeAsync(settings, stoppingToken);
                }
                else
                {
                    var nextCheck = _lastUpgradeCheck + checkInterval;
                    _logger.LogDebug("Next upgrade check in {Time}", nextCheck - DateTime.UtcNow);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in discovery upgrade service");
            }

            await Task.Delay(_settingsCheckInterval, stoppingToken);
        }

        _logger.LogInformation("Discovery upgrade service stopping");
    }

    private async Task CheckAndUpgradeAsync(PullListSettings settings, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortboxerrDbContext>();
        var walkSoftlyClient = scope.ServiceProvider.GetRequiredService<IWalkSoftlyClient>();
        var comicVineClient = scope.ServiceProvider.GetRequiredService<IComicVineClient>();
        var coverService = scope.ServiceProvider.GetRequiredService<ICoverService>();

        // Get all cached discovery weeks (current week and configured weeks ahead)
        var today = DateTime.UtcNow.Date;
        var weekStart = GetWeekStart(today);
        
        // Check current week + configured weeks ahead
        var weeksAhead = settings.DiscoveryUpgradeWeeksAhead;
        var weeksToCheck = new List<DateTime>();
        for (var i = 0; i < weeksAhead; i++)
        {
            weeksToCheck.Add(weekStart.AddDays(i * 7));
        }

        var cachedWeeks = await dbContext.CachedDiscoveryWeeks
            .Where(c => weeksToCheck.Contains(c.WeekStart.Date))
            .ToListAsync(cancellationToken);

        if (cachedWeeks.Count == 0)
        {
            _logger.LogDebug("No cached discovery weeks to check for upgrades");
            return;
        }

        _logger.LogInformation(
            "Checking {Count} cached weeks for enrichment upgrades",
            cachedWeeks.Count);

        var totalUpgraded = 0;
        var totalChecked = 0;

        foreach (var cachedWeek in cachedWeeks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var (upgraded, checked_) = await UpgradeWeekAsync(
                    cachedWeek,
                    walkSoftlyClient,
                    comicVineClient,
                    coverService,
                    dbContext,
                    cancellationToken);

                totalUpgraded += upgraded;
                totalChecked += checked_;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to upgrade cached week {WeekStart}",
                    cachedWeek.WeekStart);
            }

            await Task.Delay(_delayBetweenWeeks, cancellationToken);
        }

        _lastUpgradeCheck = DateTime.UtcNow;

        _logger.LogInformation(
            "Discovery upgrade check complete: {Upgraded} issues upgraded out of {Checked} non-finalized",
            totalUpgraded, totalChecked);
    }

    private async Task<(int upgraded, int checked_)> UpgradeWeekAsync(
        CachedDiscoveryWeek cachedWeek,
        IWalkSoftlyClient walkSoftlyClient,
        IComicVineClient comicVineClient,
        ICoverService coverService,
        ShortboxerrDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Deserialize cached issues
        List<ComicVineIssue> issues;
        try
        {
            issues = JsonSerializer.Deserialize<List<ComicVineIssue>>(cachedWeek.IssuesJson)
                ?? new List<ComicVineIssue>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached issues for week {WeekStart}", cachedWeek.WeekStart);
            return (0, 0);
        }

        // Find non-finalized issues (those without CV issue ID or with interim status)
        var nonFinalizedIssues = issues
            .Where(i => i.Id <= 0 || i.EnrichmentStatus != CoverEnrichmentStatus.HasComicVineCover)
            .ToList();

        if (nonFinalizedIssues.Count == 0)
        {
            _logger.LogDebug("Week {WeekStart}: All {Count} issues already finalized",
                cachedWeek.WeekStart, issues.Count);
            return (0, 0);
        }

        _logger.LogDebug("Week {WeekStart}: Found {NonFinalized} non-finalized issues out of {Total}",
            cachedWeek.WeekStart, nonFinalizedIssues.Count, issues.Count);

        // Re-query WalkSoftly for this week to get fresh CV issue IDs
        var releaseDay = cachedWeek.WeekStart.AddDays((int)DayOfWeek.Wednesday);
        var cal = CultureInfo.InvariantCulture.Calendar;
        var isoWeek = cal.GetWeekOfYear(releaseDay, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var weekNumber = isoWeek - 1; // WalkSoftly week = ISO week - 1
        var year = releaseDay.Year;

        var wsResult = await walkSoftlyClient.GetWeeklyReleasesAsync(weekNumber, year, cancellationToken);

        if (!wsResult.Success || wsResult.Releases.Count == 0)
        {
            _logger.LogDebug("Week {WeekStart}: WalkSoftly returned no releases",
                cachedWeek.WeekStart);
            return (0, nonFinalizedIssues.Count);
        }

        // Build lookup by series title + issue number
        var wsLookup = wsResult.Releases
            .Where(r => r.IssueId.HasValue && r.IssueId.Value > 0)
            .ToDictionary(
                r => $"{NormalizeTitle(r.Series)}|{NormalizeIssueNumber(r.Issue)}",
                r => r,
                StringComparer.OrdinalIgnoreCase);

        var upgraded = 0;
        var newCvIds = new List<int>();

        // Check each non-finalized issue
        foreach (var issue in nonFinalizedIssues)
        {
            var seriesTitle = issue.Volume?.Name ?? "";
            var issueNum = issue.IssueNumber ?? "0";
            var lookupKey = $"{NormalizeTitle(seriesTitle)}|{NormalizeIssueNumber(issueNum)}";

            if (wsLookup.TryGetValue(lookupKey, out var wsRelease))
            {
                // WalkSoftly now has a CV issue ID for this issue!
                if (issue.Id <= 0 && wsRelease.IssueId.HasValue && wsRelease.IssueId.Value > 0)
                {
                    _logger.LogDebug(
                        "Found new CV issue ID for '{Series}' #{Issue}: {CvId}",
                        seriesTitle, issueNum, wsRelease.IssueId.Value);
                    
                    issue.Id = wsRelease.IssueId.Value;
                    newCvIds.Add(issue.Id);
                }
            }
        }

        // Batch fetch full data from ComicVine for issues with newly discovered CV IDs
        if (newCvIds.Count > 0)
        {
            try
            {
                var cvResult = await comicVineClient.GetIssuesByIdsAsync(newCvIds, cancellationToken);

                if (cvResult.Success && cvResult.Results != null)
                {
                    var cvLookup = cvResult.Results.ToDictionary(i => i.Id);

                    foreach (var issue in issues.Where(i => newCvIds.Contains(i.Id)))
                    {
                        if (cvLookup.TryGetValue(issue.Id, out var cvIssue))
                        {
                            // Upgrade with ComicVine data
                            if (!string.IsNullOrEmpty(cvIssue.Name))
                                issue.Name = cvIssue.Name;
                            if (!string.IsNullOrEmpty(cvIssue.Description))
                                issue.Description = cvIssue.Description;
                            if (cvIssue.StoreDate.HasValue)
                                issue.StoreDate = cvIssue.StoreDate;
                            if (cvIssue.CoverDate.HasValue)
                                issue.CoverDate = cvIssue.CoverDate;
                            if (cvIssue.Image != null)
                            {
                                // Download ComicVine cover locally for caching
                                var coverUrl = cvIssue.Image.MediumUrl ?? cvIssue.Image.SmallUrl;
                                if (!string.IsNullOrEmpty(coverUrl))
                                {
                                    var downloadResult = await coverService.DownloadExternalCoverAsync(
                                        coverUrl,
                                        CoverType.Discovery,
                                        issue.Id,
                                        CoverCacheSource.ComicVine,
                                        CoverSize.Medium,
                                        cancellationToken);
                                    
                                    if (downloadResult.Success)
                                    {
                                        // Use local path for the cover URL
                                        issue.Image = new ComicVineImage
                                        {
                                            MediumUrl = $"/api/v1/covers/discovery/{issue.Id}/medium",
                                            SmallUrl = $"/api/v1/covers/discovery/{issue.Id}/small",
                                            OriginalUrl = coverUrl
                                        };
                                    }
                                    else
                                    {
                                        // Fallback to remote URL if download fails
                                        issue.Image = cvIssue.Image;
                                    }
                                }
                                else
                                {
                                    issue.Image = cvIssue.Image;
                                }
                                issue.CoverSource = "ComicVine";
                                issue.CoverMatchMethod = "CvIssueIdUpgrade";
                            }

                            // Mark as finalized
                            issue.EnrichmentStatus = CoverEnrichmentStatus.HasComicVineCover;
                            issue.LastEnrichmentAttempt = DateTime.UtcNow;

                            upgraded++;

                            _logger.LogInformation(
                                "Upgraded '{Series}' #{Issue} to ComicVineFinalized (CV: {CvId})",
                                issue.Volume?.Name, issue.IssueNumber, issue.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ComicVine data for {Count} issues", newCvIds.Count);
            }
        }

        // Save updated cache if any upgrades were made
        if (upgraded > 0)
        {
            cachedWeek.IssuesJson = JsonSerializer.Serialize(issues);
            cachedWeek.LastRefreshed = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Saved {Upgraded} upgraded issues for week {WeekStart}",
                upgraded, cachedWeek.WeekStart);
        }

        return (upgraded, nonFinalizedIssues.Count);
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;
        
        return title.Trim().ToUpperInvariant();
    }

    private static string NormalizeIssueNumber(string issueNumber)
    {
        if (string.IsNullOrWhiteSpace(issueNumber))
            return "0";
        
        return issueNumber.Trim().TrimStart('#').ToUpperInvariant();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
