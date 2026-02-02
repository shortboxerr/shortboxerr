using System.Collections.Concurrent;
using System.Diagnostics;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for searching across multiple DDL sites.
/// Coordinates site adapters, applies rate limiting, and aggregates results.
/// </summary>
public class DdlSearchService : IDdlSearchService
{
    private readonly IDdlSiteAdapterFactory _adapterFactory;
    private readonly IDdlReleaseParser _releaseParser;
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
    
    public DdlSearchService(IDdlSiteAdapterFactory adapterFactory, IDdlReleaseParser releaseParser)
    {
        _adapterFactory = adapterFactory;
        _releaseParser = releaseParser;
    }

    public async Task<DdlAggregatedSearchResult> SearchAllAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var sites = _adapterFactory.GetEnabledSites();
        var resultsBySite = new Dictionary<string, DdlSearchResult>();
        var successfulSites = new List<string>();
        var failedSites = new List<string>();
        var warnings = new List<string>();
        
        // Search each site (could be parallelized with rate limiting)
        foreach (var siteType in sites)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Apply rate limiting
                await ApplyRateLimitAsync(siteType, cancellationToken);
                
                var adapter = _adapterFactory.GetAdapter(siteType);
                var result = await adapter.SearchAsync(query, cancellationToken);
                
                resultsBySite[siteType] = result;
                
                if (result.Success)
                {
                    successfulSites.Add(siteType);
                }
                else
                {
                    failedSites.Add(siteType);
                    warnings.Add($"Site {siteType} search failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedSites.Add(siteType);
                warnings.Add($"Site {siteType} error: {ex.Message}");
                resultsBySite[siteType] = DdlSearchResult.Error(ex.Message, siteType);
            }
        }
        
        // Aggregate and deduplicate candidates
        var allRawCandidates = resultsBySite.Values
            .Where(r => r.Success)
            .SelectMany(r => r.Candidates)
            .ToList();
        
        var deduplicatedCandidates = DeduplicateCandidates(allRawCandidates);
        
        stopwatch.Stop();
        
        return new DdlAggregatedSearchResult
        {
            AllCandidates = deduplicatedCandidates,
            ResultsBySite = resultsBySite,
            SuccessfulSites = successfulSites,
            FailedSites = failedSites,
            TotalRawCandidates = allRawCandidates.Count,
            DuplicatesRemoved = allRawCandidates.Count - deduplicatedCandidates.Count,
            TotalDuration = stopwatch.Elapsed,
            Warnings = warnings
        };
    }

    public async Task<DdlSearchResult> SearchSiteAsync(string siteType, DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            await ApplyRateLimitAsync(siteType, cancellationToken);
            
            var adapter = _adapterFactory.GetAdapter(siteType);
            return await adapter.SearchAsync(query, cancellationToken);
        }
        catch (Exception ex)
        {
            return DdlSearchResult.Error(ex.Message, siteType);
        }
    }

    public async Task<DdlAggregatedSearchResult> GetLatestFromAllAsync(int limitPerSite = 20, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var sites = _adapterFactory.GetEnabledSites();
        var resultsBySite = new Dictionary<string, DdlSearchResult>();
        var successfulSites = new List<string>();
        var failedSites = new List<string>();
        var warnings = new List<string>();
        
        foreach (var siteType in sites)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                await ApplyRateLimitAsync(siteType, cancellationToken);
                
                var adapter = _adapterFactory.GetAdapter(siteType);
                var result = await adapter.GetLatestAsync(limitPerSite, cancellationToken);
                
                resultsBySite[siteType] = result;
                
                if (result.Success)
                {
                    successfulSites.Add(siteType);
                }
                else
                {
                    failedSites.Add(siteType);
                    warnings.Add($"Site {siteType} failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedSites.Add(siteType);
                warnings.Add($"Site {siteType} error: {ex.Message}");
                resultsBySite[siteType] = DdlSearchResult.Error(ex.Message, siteType);
            }
        }
        
        var allRawCandidates = resultsBySite.Values
            .Where(r => r.Success)
            .SelectMany(r => r.Candidates)
            .ToList();
        
        var deduplicatedCandidates = DeduplicateCandidates(allRawCandidates);
        
        // Sort by date found (newest first)
        deduplicatedCandidates = deduplicatedCandidates
            .OrderByDescending(c => c.DateFound)
            .ToList();
        
        stopwatch.Stop();
        
        return new DdlAggregatedSearchResult
        {
            AllCandidates = deduplicatedCandidates,
            ResultsBySite = resultsBySite,
            SuccessfulSites = successfulSites,
            FailedSites = failedSites,
            TotalRawCandidates = allRawCandidates.Count,
            DuplicatesRemoved = allRawCandidates.Count - deduplicatedCandidates.Count,
            TotalDuration = stopwatch.Elapsed,
            Warnings = warnings
        };
    }

    public async Task<DdlLinkExtractionResult> ExtractLinksAsync(string siteType, string pageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            await ApplyRateLimitAsync(siteType, cancellationToken);
            
            var adapter = _adapterFactory.GetAdapter(siteType);
            var links = await adapter.ExtractLinksAsync(pageUrl, cancellationToken);
            
            // Optionally verify links
            var deadLinks = new List<string>();
            var validLinks = new List<DdlDownloadLink>();
            
            foreach (var link in links)
            {
                // Quick verification (HEAD request)
                var isValid = await adapter.VerifyLinkAsync(link.Url, cancellationToken);
                if (isValid)
                {
                    validLinks.Add(link with { IsVerified = true });
                }
                else
                {
                    deadLinks.Add(link.Url);
                }
            }
            
            return new DdlLinkExtractionResult
            {
                Success = true,
                Links = validLinks,
                SourceUrl = pageUrl,
                DeadLinks = deadLinks
            };
        }
        catch (Exception ex)
        {
            return new DdlLinkExtractionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SourceUrl = pageUrl
            };
        }
    }

    public async Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        // Try to determine site type from URL
        var siteType = _adapterFactory.GetSiteTypeFromUrl(downloadUrl);
        
        if (siteType != null)
        {
            var adapter = _adapterFactory.GetAdapter(siteType);
            return await adapter.VerifyLinkAsync(downloadUrl, cancellationToken);
        }
        
        // Generic HTTP HEAD check
        return await GenericLinkVerifyAsync(downloadUrl, cancellationToken);
    }

    public IReadOnlyList<DdlSiteInfo> GetAvailableSites()
    {
        return _adapterFactory.GetAvailableSiteInfos();
    }

    public async Task<DdlSiteTestResult> TestSiteAsync(string siteType, DdlSiteConfiguration? config = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var adapter = _adapterFactory.GetAdapter(siteType);
            
            if (config != null)
            {
                adapter.Configure(config);
            }
            
            return await adapter.TestConnectionAsync(config?.Credentials, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DdlSiteTestResult
            {
                Success = false,
                Message = "Failed to test site",
                ErrorDetails = ex.Message,
                LatencyMs = 0
            };
        }
    }

    private async Task ApplyRateLimitAsync(string siteType, CancellationToken cancellationToken)
    {
        var adapter = _adapterFactory.GetAdapter(siteType);
        var rateLimit = adapter.DefaultRateLimitPerMinute;
        
        if (rateLimit <= 0)
        {
            return; // No rate limiting
        }
        
        var minInterval = TimeSpan.FromMinutes(1.0 / rateLimit);
        
        if (_lastRequestTimes.TryGetValue(siteType, out var lastRequest))
        {
            var elapsed = DateTime.UtcNow - lastRequest;
            if (elapsed < minInterval)
            {
                var delay = minInterval - elapsed;
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        _lastRequestTimes[siteType] = DateTime.UtcNow;
    }

    private static List<DdlCandidate> DeduplicateCandidates(List<DdlCandidate> candidates)
    {
        // Deduplicate by normalized release title
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DdlCandidate>();
        
        foreach (var candidate in candidates)
        {
            var key = NormalizeForDedup(candidate.ReleaseTitle);
            if (seen.Add(key))
            {
                result.Add(candidate);
            }
        }
        
        return result;
    }

    private static string NormalizeForDedup(string title)
    {
        // Simple normalization for deduplication
        return title
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace(".", "");
    }

    private static async Task<bool> GenericLinkVerifyAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

