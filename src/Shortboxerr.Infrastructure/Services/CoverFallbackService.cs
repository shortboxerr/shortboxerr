using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Implements cover image fallback logic, querying multiple sources in priority order.
/// 
/// Priority:
/// 1. Metron via ComicVine issue ID lookup (exact matching via cv_id field)
/// 2. Metron via ComicVine volume ID + issue number (series ID mapping then issue lookup)
/// 3. Metron via series name/issue number search (fuzzy fallback when IDs not available)
/// 4. ComicVine volume cover (final fallback, always available)
/// </summary>
public class CoverFallbackService : ICoverFallbackService
{
    private readonly IMetronClient _metronClient;
    private readonly ISettingsService _settingsService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CoverFallbackService> _logger;

    private const string CacheKeyPrefix = "cover_fallback:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private long _totalRequests;
    private long _metronHits;
    private long _volumeHits;
    private long _misses;
    private long _cacheHits;
    private long _totalResolutionTimeMs;

    public CoverFallbackService(
        IMetronClient metronClient,
        ISettingsService settingsService,
        IMemoryCache cache,
        ILogger<CoverFallbackService> logger)
    {
        _metronClient = metronClient;
        _settingsService = settingsService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CoverFallbackResult> GetCoverByCvIdAsync(
        int comicVineIssueId,
        int? comicVineVolumeId = null,
        string? issueNumber = null,
        string? volumeCoverUrl = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);
        var stopwatch = Stopwatch.StartNew();

        var cacheKey = GenerateCacheKey(comicVineIssueId);

        if (!bypassCache && _cache.TryGetValue(cacheKey, out CoverFallbackResult? cachedResult) && cachedResult != null)
        {
            Interlocked.Increment(ref _cacheHits);
            cachedResult.FromCache = true;
            stopwatch.Stop();
            cachedResult.ResolutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogDebug(
                "Cover fallback cache HIT for CV ID {CvId}: {Source}",
                comicVineIssueId, cachedResult.Source);

            return cachedResult;
        }

        _logger.LogDebug("Cover fallback lookup for CV ID {CvId} (bypassCache: {Bypass})", comicVineIssueId, bypassCache);

        CoverFallbackResult result;

        // Priority 1: Try Metron by CV issue ID
        try
        {
            result = await TryMetronByCvIdAsync(comicVineIssueId, bypassCache, cancellationToken);

            if (result.Success)
            {
                Interlocked.Increment(ref _metronHits);
                _logger.LogInformation(
                    "Cover found via Metron CV issue ID for {CvId}",
                    comicVineIssueId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metron CV issue ID lookup failed for {CvId}", comicVineIssueId);
            result = CoverFallbackResult.NotFound();
        }

        // Priority 2: Try Metron by CV volume ID + issue number (skip if rate limited)
        if (!result.Success && !result.WasRateLimited && comicVineVolumeId.HasValue && !string.IsNullOrEmpty(issueNumber))
        {
            try
            {
                result = await TryMetronByCvVolumeIdAsync(comicVineVolumeId.Value, issueNumber, bypassCache, cancellationToken);

                if (result.Success)
                {
                    Interlocked.Increment(ref _metronHits);
                    _logger.LogInformation(
                        "Cover found via Metron CV volume ID {VolumeId} + issue #{Number} for CV issue {CvId}",
                        comicVineVolumeId, issueNumber, comicVineIssueId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metron CV volume ID lookup failed for volume {VolumeId} issue {Number}", 
                    comicVineVolumeId, issueNumber);
            }
        }

        // Priority 3: Volume cover fallback (use even if rate limited)
        if (!result.Success && !string.IsNullOrEmpty(volumeCoverUrl))
        {
            Interlocked.Increment(ref _volumeHits);
            result = CoverFallbackResult.Found(volumeCoverUrl, CoverSource.ComicVineVolume, matchMethod: "VolumeFallback");

            _logger.LogDebug(
                "Using volume cover as fallback for CV ID {CvId}",
                comicVineIssueId);
        }

        if (!result.Success)
        {
            Interlocked.Increment(ref _misses);
            _logger.LogDebug(
                "No cover found for CV ID {CvId} in any source",
                comicVineIssueId);
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

    public async Task<CoverFallbackResult> GetCoverAsync(
        string seriesName,
        string issueNumber,
        int? comicVineVolumeId = null,
        string? publisher = null,
        DateTime? expectedStoreDate = null,
        string? volumeCoverUrl = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);
        var stopwatch = Stopwatch.StartNew();

        var cacheKey = GenerateCacheKey(seriesName, issueNumber);

        if (!bypassCache && _cache.TryGetValue(cacheKey, out CoverFallbackResult? cachedResult) && cachedResult != null)
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
            "Cover fallback lookup for {Series} #{Issue} (publisher: {Publisher}, cvVolumeId: {VolumeId}, bypassCache: {Bypass})",
            seriesName, issueNumber, publisher ?? "unknown", comicVineVolumeId?.ToString() ?? "none", bypassCache);

        CoverFallbackResult result = CoverFallbackResult.NotFound();

        // Priority 1: Try Metron by CV volume ID + issue number (if available)
        if (comicVineVolumeId.HasValue)
        {
            try
            {
                result = await TryMetronByCvVolumeIdAsync(comicVineVolumeId.Value, issueNumber, bypassCache, cancellationToken);

                if (result.Success)
                {
                    Interlocked.Increment(ref _metronHits);
                    _logger.LogInformation(
                        "Cover found via Metron CV volume ID {VolumeId} + issue #{Number} for {Series}",
                        comicVineVolumeId, issueNumber, seriesName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metron CV volume ID lookup failed for volume {VolumeId} issue {Number}", 
                    comicVineVolumeId, issueNumber);
            }
        }

        // Priority 2: Try Metron by series name/issue number search (skip if rate limited)
        if (!result.Success && !result.WasRateLimited)
        {
            try
            {
                result = await TryMetronBySearchAsync(seriesName, issueNumber, publisher, expectedStoreDate, bypassCache, cancellationToken);

                if (result.Success)
                {
                    Interlocked.Increment(ref _metronHits);
                    _logger.LogInformation(
                        "Cover found via Metron search for {Series} #{Issue}",
                        seriesName, issueNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metron search failed for {Series} #{Issue}", seriesName, issueNumber);
                result = CoverFallbackResult.NotFound();
            }
        }

        // Priority 3: Volume cover fallback (use even if rate limited)
        var wasConfidenceRejected = result.WasConfidenceRejected;
        var wasRateLimited = result.WasRateLimited;
        if (!result.Success && !string.IsNullOrEmpty(volumeCoverUrl))
        {
            Interlocked.Increment(ref _volumeHits);
            result = CoverFallbackResult.Found(volumeCoverUrl, CoverSource.ComicVineVolume, matchMethod: "VolumeFallback");
            result.WasConfidenceRejected = wasConfidenceRejected;
            result.WasRateLimited = wasRateLimited;

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

    private async Task<CoverFallbackResult> TryMetronByCvIdAsync(
        int comicVineIssueId,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var metronResult = await _metronClient.GetIssueByCvIdAsync(comicVineIssueId, bypassCache, cancellationToken);

        if (metronResult.StatusCode == 429)
        {
            _logger.LogWarning("Rate limited by Metron API during issue lookup for CV issue ID {CvId}", comicVineIssueId);
            return CoverFallbackResult.RateLimited();
        }

        if (!metronResult.Success || metronResult.Issue == null)
        {
            return CoverFallbackResult.NotFound(metronResult.Error);
        }

        if (!string.IsNullOrEmpty(metronResult.Issue.ImageUrl))
        {
            return CoverFallbackResult.Found(metronResult.Issue.ImageUrl, CoverSource.Metron, matchMethod: "CvIssueId", matchConfidence: 1.0);
        }

        return CoverFallbackResult.NotFound("Metron issue found but no cover image available");
    }

    private async Task<CoverFallbackResult> TryMetronByCvVolumeIdAsync(
        int comicVineVolumeId,
        string issueNumber,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        // Step 1: Look up Metron series by CV volume ID
        var seriesResult = await _metronClient.GetSeriesByCvIdAsync(comicVineVolumeId, bypassCache, cancellationToken);

        if (seriesResult.StatusCode == 429)
        {
            _logger.LogWarning("Rate limited by Metron API during series lookup for CV volume ID {VolumeId}", comicVineVolumeId);
            return CoverFallbackResult.RateLimited();
        }

        if (!seriesResult.Success || seriesResult.Series == null)
        {
            _logger.LogDebug("No Metron series found for CV volume ID {VolumeId}: {Error}", 
                comicVineVolumeId, seriesResult.Error);
            return CoverFallbackResult.NotFound(seriesResult.Error);
        }

        var metronSeriesId = seriesResult.Series.Id;
        _logger.LogDebug("Found Metron series {SeriesId} ({SeriesName}) for CV volume ID {VolumeId}",
            metronSeriesId, seriesResult.Series.Name, comicVineVolumeId);

        // Step 2: Look up issue by Metron series ID + issue number
        var issueResult = await _metronClient.GetIssueBySeriesIdAsync(metronSeriesId, issueNumber, bypassCache, cancellationToken);

        if (issueResult.StatusCode == 429)
        {
            _logger.LogWarning("Rate limited by Metron API during issue lookup for series {SeriesId} issue #{Number}", metronSeriesId, issueNumber);
            return CoverFallbackResult.RateLimited();
        }

        if (!issueResult.Success || issueResult.Issue == null)
        {
            _logger.LogDebug("No Metron issue found for series {SeriesId} issue #{Number}: {Error}",
                metronSeriesId, issueNumber, issueResult.Error);
            return CoverFallbackResult.NotFound(issueResult.Error);
        }

        if (!string.IsNullOrEmpty(issueResult.Issue.ImageUrl))
        {
            return CoverFallbackResult.Found(
                issueResult.Issue.ImageUrl, 
                CoverSource.Metron, 
                matchMethod: "CvVolumeId", 
                matchConfidence: 1.0);
        }

        return CoverFallbackResult.NotFound("Metron issue found but no cover image available");
    }

    private async Task<CoverFallbackResult> TryMetronBySearchAsync(
        string seriesName,
        string issueNumber,
        string? publisher,
        DateTime? expectedStoreDate,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        // MetronClient checks configuration internally after loading settings from database
        var searchResult = await _metronClient.SearchIssueAsync(seriesName, issueNumber, bypassCache, cancellationToken);

        if (searchResult.StatusCode == 429)
        {
            _logger.LogWarning("Rate limited by Metron API during search for {Series} #{Issue}", seriesName, issueNumber);
            return CoverFallbackResult.RateLimited();
        }

        if (!searchResult.Success || searchResult.Issues.Count == 0)
        {
            return CoverFallbackResult.NotFound(searchResult.Error);
        }

        var minConfidence = await GetMinMatchConfidenceAsync(cancellationToken);
        var bestMatch = FindBestMatch(searchResult.Issues, seriesName, issueNumber, publisher, expectedStoreDate);

        if (bestMatch != null && !string.IsNullOrEmpty(bestMatch.Issue.ImageUrl))
        {
            if (bestMatch.Confidence < minConfidence / 100.0)
            {
                _logger.LogInformation(
                    "Rejected ID-less Metron match for {Series} #{Issue}: score {Score:P0} below threshold {Threshold}%",
                    seriesName, issueNumber, bestMatch.Confidence, minConfidence);
                return CoverFallbackResult.NotFound(
                    $"Metron candidate confidence {bestMatch.Confidence:P0} below threshold {minConfidence}%",
                    wasConfidenceRejected: true);
            }

            return CoverFallbackResult.Found(
                bestMatch.Issue.ImageUrl,
                CoverSource.Metron,
                matchMethod: "IdLessHeuristic",
                matchConfidence: bestMatch.Confidence);
        }

        return CoverFallbackResult.NotFound("No matching issue found in Metron results");
    }

    private static MetronMatchCandidate? FindBestMatch(
        List<MetronIssue> issues,
        string targetSeries,
        string targetIssueNumber,
        string? targetPublisher,
        DateTime? expectedStoreDate)
    {
        var normalizedTargetSeries = NormalizeName(targetSeries);
        var normalizedTargetIssue = NormalizeIssueNumber(targetIssueNumber);

        var candidates = issues
            .Select(issue => new
            {
                Issue = issue,
                SeriesScore = CalculateNameSimilarity(NormalizeName(issue.Series?.Name ?? ""), normalizedTargetSeries),
                IssueMatch = NormalizeIssueNumber(issue.Number) == normalizedTargetIssue,
                PublisherMatch = string.IsNullOrEmpty(targetPublisher) ||
                    (issue.Series?.Publisher?.Name != null &&
                        (issue.Series.Publisher.Name.Contains(targetPublisher, StringComparison.OrdinalIgnoreCase) ||
                         targetPublisher.Contains(issue.Series.Publisher.Name, StringComparison.OrdinalIgnoreCase))),
                DateScore = CalculateDateProximity(issue.StoreDate, expectedStoreDate)
            })
            .Where(c => c.SeriesScore >= 0.7 && c.IssueMatch)
            .Select(c => new MetronMatchCandidate
            {
                Issue = c.Issue,
                Confidence = (c.SeriesScore * 0.50) + (c.PublisherMatch ? 0.20 : 0.0) + (c.DateScore * 0.30)
            })
            .OrderByDescending(c => c.Confidence)
            .FirstOrDefault();

        return candidates;
    }

    private static double CalculateDateProximity(DateTime? candidateStoreDate, DateTime? expectedStoreDate)
    {
        if (!candidateStoreDate.HasValue || !expectedStoreDate.HasValue)
            return 0.5;

        var days = Math.Abs((candidateStoreDate.Value.Date - expectedStoreDate.Value.Date).TotalDays);
        return days switch
        {
            <= 3 => 1.0,
            <= 7 => 0.8,
            <= 14 => 0.6,
            <= 21 => 0.4,
            _ => 0.1
        };
    }

    private async Task<int> GetMinMatchConfidenceAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync<MetronSettings>("metron", new MetronSettings(), cancellationToken)
            ?? new MetronSettings();
        return Math.Clamp(settings.MinMatchConfidence, 50, 100);
    }

    private static string NormalizeName(string? name)
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

    private static string NormalizeIssueNumber(string? issueNumber)
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

    private static string GenerateCacheKey(int comicVineIssueId)
    {
        return $"{CacheKeyPrefix}cv:{comicVineIssueId}";
    }

    private static string GenerateCacheKey(string seriesName, string issueNumber)
    {
        var normalized = $"{NormalizeName(seriesName)}_{NormalizeIssueNumber(issueNumber)}";
        return $"{CacheKeyPrefix}{normalized}";
    }

    private sealed class MetronMatchCandidate
    {
        public required MetronIssue Issue { get; init; }
        public required double Confidence { get; init; }
    }

    public Task<CoverFallbackStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var total = Interlocked.Read(ref _totalRequests);
        var stats = new CoverFallbackStats
        {
            TotalRequests = total,
            MetronHits = Interlocked.Read(ref _metronHits),
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

    /// <summary>
    /// Clears cached fallback cover data for a specific ComicVine issue ID.
    /// </summary>
    public Task ClearCacheAsync(int comicVineIssueId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(comicVineIssueId);
        _cache.Remove(cacheKey);

        _logger.LogDebug("Cleared cover fallback cache for CV ID {CvId}", comicVineIssueId);

        return Task.CompletedTask;
    }
}
