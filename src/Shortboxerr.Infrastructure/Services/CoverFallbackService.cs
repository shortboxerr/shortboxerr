using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.LeagueOfComicGeeks;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Implements cover image fallback logic, querying multiple sources in priority order.
/// 
/// Priority:
/// 1. League of Comic Geeks (unofficial, has good new release coverage)
/// 2. ComicVine volume cover (final fallback, always available)
/// </summary>
public class CoverFallbackService : ICoverFallbackService
{
    private readonly ILeagueOfComicGeeksClient _locgClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CoverFallbackService> _logger;

    private const string CacheKeyPrefix = "cover_fallback:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private long _totalRequests;
    private long _locgHits;
    private long _volumeHits;
    private long _misses;
    private long _cacheHits;
    private long _totalResolutionTimeMs;

    public CoverFallbackService(
        ILeagueOfComicGeeksClient locgClient,
        IMemoryCache cache,
        ILogger<CoverFallbackService> logger)
    {
        _locgClient = locgClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CoverFallbackResult> GetCoverAsync(
        string seriesName,
        string issueNumber,
        string? publisher = null,
        string? volumeCoverUrl = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);
        var stopwatch = Stopwatch.StartNew();

        var cacheKey = GenerateCacheKey(seriesName, issueNumber);

        if (_cache.TryGetValue(cacheKey, out CoverFallbackResult? cachedResult) && cachedResult != null)
        {
            Interlocked.Increment(ref _cacheHits);
            cachedResult.FromCache = true;
            stopwatch.Stop();
            cachedResult.ResolutionTimeMs = stopwatch.ElapsedMilliseconds;
            
            _logger.LogDebug(
                "Cover fallback cache HIT for {Series} #{Issue}: {Source}",
                seriesName, issueNumber, cachedResult.Source);
            
            return cachedResult;
        }

        _logger.LogDebug(
            "Cover fallback lookup for {Series} #{Issue} (publisher: {Publisher})",
            seriesName, issueNumber, publisher ?? "unknown");

        CoverFallbackResult result;

        try
        {
            result = await TryLocgAsync(seriesName, issueNumber, publisher, cancellationToken);

            if (result.Success)
            {
                Interlocked.Increment(ref _locgHits);
                _logger.LogInformation(
                    "Cover found via LOCG for {Series} #{Issue}",
                    seriesName, issueNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LOCG fallback failed for {Series} #{Issue}", seriesName, issueNumber);
            result = CoverFallbackResult.NotFound();
        }

        if (!result.Success && !string.IsNullOrEmpty(volumeCoverUrl))
        {
            Interlocked.Increment(ref _volumeHits);
            result = CoverFallbackResult.Found(volumeCoverUrl, CoverSource.ComicVineVolume);
            
            _logger.LogDebug(
                "Using volume cover as fallback for {Series} #{Issue}",
                seriesName, issueNumber);
        }

        if (!result.Success)
        {
            Interlocked.Increment(ref _misses);
            _logger.LogDebug(
                "No cover found for {Series} #{Issue} in any source",
                seriesName, issueNumber);
        }

        stopwatch.Stop();
        result.ResolutionTimeMs = stopwatch.ElapsedMilliseconds;
        Interlocked.Add(ref _totalResolutionTimeMs, stopwatch.ElapsedMilliseconds);

        if (result.Success)
        {
            _cache.Set(cacheKey, result, CacheDuration);
        }

        return result;
    }

    private async Task<CoverFallbackResult> TryLocgAsync(
        string seriesName,
        string issueNumber,
        string? publisher,
        CancellationToken cancellationToken)
    {
        var searchResult = await _locgClient.SearchIssueAsync(seriesName, issueNumber, cancellationToken);

        if (!searchResult.Success || searchResult.Issues.Count == 0)
        {
            return CoverFallbackResult.NotFound(searchResult.Error);
        }

        var bestMatch = FindBestMatch(searchResult.Issues, seriesName, issueNumber, publisher);

        if (bestMatch != null && !string.IsNullOrEmpty(bestMatch.CoverUrl))
        {
            return CoverFallbackResult.Found(bestMatch.CoverUrl, CoverSource.LeagueOfComicGeeks);
        }

        return CoverFallbackResult.NotFound("No matching issue found in LOCG results");
    }

    private static LocgIssue? FindBestMatch(
        List<LocgIssue> issues,
        string targetSeries,
        string targetIssueNumber,
        string? targetPublisher)
    {
        var normalizedTargetSeries = NormalizeName(targetSeries);
        var normalizedTargetIssue = NormalizeIssueNumber(targetIssueNumber);

        var candidates = issues
            .Select(issue => new
            {
                Issue = issue,
                SeriesScore = CalculateNameSimilarity(NormalizeName(issue.SeriesName), normalizedTargetSeries),
                IssueMatch = NormalizeIssueNumber(issue.IssueNumber ?? "") == normalizedTargetIssue,
                PublisherMatch = string.IsNullOrEmpty(targetPublisher) || 
                    issue.Publisher.Contains(targetPublisher, StringComparison.OrdinalIgnoreCase) ||
                    targetPublisher.Contains(issue.Publisher, StringComparison.OrdinalIgnoreCase)
            })
            .Where(c => c.SeriesScore >= 0.7 && c.IssueMatch)
            .OrderByDescending(c => c.PublisherMatch)
            .ThenByDescending(c => c.SeriesScore)
            .FirstOrDefault();

        return candidates?.Issue;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";

        return name
            .ToLowerInvariant()
            .Replace("'", "")
            .Replace("\"", "")
            .Replace(":", "")
            .Replace("-", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static string NormalizeIssueNumber(string issueNumber)
    {
        if (string.IsNullOrEmpty(issueNumber)) return "";

        var result = issueNumber.TrimStart('#').Trim().ToLowerInvariant();
        
        if (int.TryParse(result, out var num))
        {
            return num.ToString();
        }
        
        return result;
    }

    private static double CalculateNameSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        if (a == b)
            return 1.0;

        if (a.Contains(b) || b.Contains(a))
            return 0.9;

        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = wordsA.Intersect(wordsB).Count();
        var totalWords = Math.Max(wordsA.Length, wordsB.Length);

        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }

    private static string GenerateCacheKey(string seriesName, string issueNumber)
    {
        var normalized = $"{NormalizeName(seriesName)}_{NormalizeIssueNumber(issueNumber)}";
        return $"{CacheKeyPrefix}{normalized}";
    }

    public Task<CoverFallbackStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var total = Interlocked.Read(ref _totalRequests);
        var stats = new CoverFallbackStats
        {
            TotalRequests = total,
            LocgHits = Interlocked.Read(ref _locgHits),
            ComicVineVolumeHits = Interlocked.Read(ref _volumeHits),
            Misses = Interlocked.Read(ref _misses),
            CacheHitRatio = total > 0 ? (double)Interlocked.Read(ref _cacheHits) / total : 0,
            AverageResolutionTimeMs = total > 0 ? (double)Interlocked.Read(ref _totalResolutionTimeMs) / total : 0
        };

        return Task.FromResult(stats);
    }

    public Task ClearCacheAsync(string seriesName, string issueNumber, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(seriesName, issueNumber);
        _cache.Remove(cacheKey);
        
        _logger.LogDebug(
            "Cleared cover fallback cache for {Series} #{Issue}",
            seriesName, issueNumber);
        
        return Task.CompletedTask;
    }
}
