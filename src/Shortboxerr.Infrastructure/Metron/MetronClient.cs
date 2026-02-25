using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Metron;

/// <summary>
/// HTTP client implementation for Metron comic database API.
/// 
/// Metron provides an official REST API with Basic Auth.
/// Key advantage: Direct ComicVine ID lookup via cv_id parameter.
/// 
/// Rate Limiting: This client implements multiple layers of rate limit protection:
/// 1. Minimum delay between requests (2 seconds = 30 req/min)
/// 2. Circuit breaker that stops requests when rate limited
/// 3. Exponential backoff on consecutive errors
/// 4. Detection of HTML rate limit pages (API returns 200 OK but HTML content)
/// </summary>
public class MetronClient : IMetronClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MetronClient>? _logger;
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    
    private MetronSettings? _cachedSettings;
    private DateTime _settingsCacheTime = DateTime.MinValue;
    private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(5);

    private const string BaseUrl = "https://metron.cloud/api/";
    private const string CacheKeyPrefix = "metron:";
    private const int MinDelayMs = 2000; // 30 requests/min = 2s between requests
    
    // Circuit breaker state
    private int _consecutiveErrors;
    private DateTime _circuitBreakerResetTime = DateTime.MinValue;
    private const int CircuitBreakerThreshold = 3; // Open circuit after 3 consecutive errors
    private static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public MetronClient(
        HttpClient httpClient,
        IMemoryCache cache,
        ISettingsService settingsService,
        ILogger<MetronClient>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _settingsService = settingsService;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(MetronSettings.DefaultTimeoutSeconds);

        // Set User-Agent (required by Metron - must not be a browser agent)
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shortboxerr/1.0 (+https://github.com/shortboxerr/shortboxerr)");
    }
    
    private async Task<MetronSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null && DateTime.UtcNow - _settingsCacheTime < SettingsCacheDuration)
        {
            return _cachedSettings;
        }
        
        _cachedSettings = await _settingsService.GetAsync<MetronSettings>("metron", new MetronSettings(), cancellationToken) 
            ?? new MetronSettings();
        _settingsCacheTime = DateTime.UtcNow;
        return _cachedSettings;
    }
    
    private async Task ConfigureAuthAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings.IsConfigured)
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.Username}:{settings.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public bool IsConfigured => _cachedSettings?.IsConfigured ?? false;

    public async Task<MetronIssueResult> GetIssueByCvIdAsync(
        int comicVineIssueId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return MetronIssueResult.Failed("Metron integration is disabled");
        }

        if (!settings.IsConfigured)
        {
            return MetronIssueResult.Failed("Metron credentials not configured");
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var cacheKey = $"{CacheKeyPrefix}cv:{comicVineIssueId}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronIssueResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for CV ID: {CvId}", comicVineIssueId);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Looking up Metron issue by CV ID: {CvId} (bypassCache: {Bypass})", comicVineIssueId, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"issue/?cv_id={comicVineIssueId}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                RecordError();
                return MetronIssueResult.Failed("Authentication failed", 401);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for CV ID {CvId}", 
                    (int)response.StatusCode, comicVineIssueId);
                RecordError();
                return MetronIssueResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            // Check for HTML rate limit page
            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return MetronIssueResult.Failed("Rate limited by Metron API", 429);
            }

            var apiResponse = JsonSerializer.Deserialize<MetronApiListResponse>(content!, JsonOptions);

            if (apiResponse == null || apiResponse.Results.Count == 0)
            {
                RecordSuccess();
                var notFoundResult = MetronIssueResult.NotFound($"No issue found with CV ID {comicVineIssueId}");
                // Cache not-found results for a shorter time
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            RecordSuccess();
            var issue = MapToMetronIssue(apiResponse.Results[0]);
            var result = MetronIssueResult.Found(issue);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours));

            _logger?.LogInformation(
                "Found Metron issue for CV ID {CvId}: {Series} #{Number} (cover: {HasCover})",
                comicVineIssueId, issue.Series?.Name, issue.Number, !string.IsNullOrEmpty(issue.ImageUrl));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error looking up Metron CV ID {CvId}", comicVineIssueId);
            RecordError();
            return MetronIssueResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("Metron lookup cancelled for CV ID {CvId}", comicVineIssueId);
            return MetronIssueResult.Failed("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error looking up Metron CV ID {CvId}", comicVineIssueId);
            RecordError();
            return MetronIssueResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<MetronSeriesResult> GetSeriesByCvIdAsync(
        int comicVineVolumeId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return MetronSeriesResult.Failed("Metron integration is disabled");
        }

        if (!settings.IsConfigured)
        {
            return MetronSeriesResult.Failed("Metron credentials not configured");
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var cacheKey = $"{CacheKeyPrefix}series_cv:{comicVineVolumeId}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronSeriesResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for series CV ID: {CvId}", comicVineVolumeId);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Looking up Metron series by CV ID: {CvId} (bypassCache: {Bypass})", comicVineVolumeId, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"series/?cv_id={comicVineVolumeId}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                RecordError();
                return MetronSeriesResult.Failed("Authentication failed", 401);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for series CV ID {CvId}", 
                    (int)response.StatusCode, comicVineVolumeId);
                RecordError();
                return MetronSeriesResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            // Check for HTML rate limit page
            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return MetronSeriesResult.Failed("Rate limited by Metron API", 429);
            }

            var apiResponse = JsonSerializer.Deserialize<MetronApiSeriesListResponse>(content!, JsonOptions);

            if (apiResponse == null || apiResponse.Results.Count == 0)
            {
                RecordSuccess();
                var notFoundResult = MetronSeriesResult.NotFound($"No series found with CV ID {comicVineVolumeId}");
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            RecordSuccess();
            var series = MapToMetronSeries(apiResponse.Results[0]);
            var result = MetronSeriesResult.Found(series);

            // Cache series mappings longer since they rarely change
            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours * 7));

            _logger?.LogInformation(
                "Found Metron series for CV ID {CvId}: {SeriesName} (Metron ID: {MetronId})",
                comicVineVolumeId, series.Name, series.Id);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error looking up Metron series CV ID {CvId}", comicVineVolumeId);
            RecordError();
            return MetronSeriesResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("Metron lookup cancelled for series CV ID {CvId}", comicVineVolumeId);
            return MetronSeriesResult.Failed("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error looking up Metron series CV ID {CvId}", comicVineVolumeId);
            RecordError();
            return MetronSeriesResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<MetronIssueResult> GetIssueBySeriesIdAsync(
        int metronSeriesId,
        string issueNumber,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return MetronIssueResult.Failed("Metron integration is disabled");
        }

        if (!settings.IsConfigured)
        {
            return MetronIssueResult.Failed("Metron credentials not configured");
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var normalizedNumber = NormalizeIssueNumber(issueNumber);
        var cacheKey = $"{CacheKeyPrefix}series_issue:{metronSeriesId}:{normalizedNumber}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronIssueResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for series {SeriesId} issue {Number}", metronSeriesId, issueNumber);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Looking up Metron issue by series ID {SeriesId} and number {Number} (bypassCache: {Bypass})", 
            metronSeriesId, issueNumber, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var encodedNumber = Uri.EscapeDataString(normalizedNumber);
            var url = $"issue/?series_id={metronSeriesId}&number={encodedNumber}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                RecordError();
                return MetronIssueResult.Failed("Authentication failed", 401);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for series {SeriesId} issue {Number}", 
                    (int)response.StatusCode, metronSeriesId, issueNumber);
                RecordError();
                return MetronIssueResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            // Check for HTML rate limit page
            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return MetronIssueResult.Failed("Rate limited by Metron API", 429);
            }

            var apiResponse = JsonSerializer.Deserialize<MetronApiListResponse>(content!, JsonOptions);

            if (apiResponse == null || apiResponse.Results.Count == 0)
            {
                RecordSuccess();
                var notFoundResult = MetronIssueResult.NotFound(
                    $"No issue found for series {metronSeriesId} issue {issueNumber}");
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            RecordSuccess();
            var issue = MapToMetronIssue(apiResponse.Results[0]);
            var result = MetronIssueResult.Found(issue);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours));

            _logger?.LogInformation(
                "Found Metron issue for series {SeriesId} #{Number}: {Series} (cover: {HasCover})",
                metronSeriesId, issueNumber, issue.Series?.Name, !string.IsNullOrEmpty(issue.ImageUrl));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error looking up Metron series {SeriesId} issue {Number}", metronSeriesId, issueNumber);
            RecordError();
            return MetronIssueResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("Metron lookup cancelled for series {SeriesId} issue {Number}", metronSeriesId, issueNumber);
            return MetronIssueResult.Failed("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error looking up Metron series {SeriesId} issue {Number}", metronSeriesId, issueNumber);
            RecordError();
            return MetronIssueResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    private static string NormalizeIssueNumber(string? issueNumber)
    {
        if (string.IsNullOrEmpty(issueNumber)) return "";

        var result = issueNumber.TrimStart('#').Trim();

        // If it's a pure integer, return without leading zeros
        if (int.TryParse(result, out var num))
        {
            return num.ToString();
        }

        return result;
    }

    public async Task<MetronSearchResult> SearchIssueAsync(
        string seriesName,
        string issueNumber,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return new MetronSearchResult { Success = false, Error = "Metron integration is disabled" };
        }

        if (!settings.IsConfigured)
        {
            return new MetronSearchResult { Success = false, Error = "Metron credentials not configured" };
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var cacheKey = $"{CacheKeyPrefix}search:{seriesName.ToLowerInvariant()}:{issueNumber.ToLowerInvariant()}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronSearchResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for search: {Series} #{Issue}", seriesName, issueNumber);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Searching Metron for: {Series} #{Issue} (bypassCache: {Bypass})", seriesName, issueNumber, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var encodedSeries = Uri.EscapeDataString(seriesName);
            var encodedNumber = Uri.EscapeDataString(issueNumber.TrimStart('#'));
            var url = $"issue/?series_name={encodedSeries}&number={encodedNumber}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                RecordError();
                return new MetronSearchResult { Success = false, Error = "Authentication failed", StatusCode = 401 };
            }

            if (!response.IsSuccessStatusCode)
            {
                RecordError();
                return new MetronSearchResult
                {
                    Success = false,
                    Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    StatusCode = (int)response.StatusCode
                };
            }

            // Check for HTML rate limit page
            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return new MetronSearchResult { Success = false, Error = "Rate limited by Metron API", StatusCode = 429 };
            }

            var apiResponse = JsonSerializer.Deserialize<MetronApiListResponse>(content!, JsonOptions);

            RecordSuccess();
            var result = new MetronSearchResult
            {
                Success = true,
                StatusCode = 200,
                TotalCount = apiResponse?.Count ?? 0,
                Issues = apiResponse?.Results.Select(MapToMetronIssue).ToList() ?? new List<MetronIssue>()
            };

            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours));

            _logger?.LogInformation("Metron search found {Count} results for {Series} #{Issue}",
                result.Issues.Count, seriesName, issueNumber);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error searching Metron");
            RecordError();
            return new MetronSearchResult { Success = false, Error = $"Network error: {ex.Message}" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error searching Metron");
            RecordError();
            return new MetronSearchResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<MetronIssueListResult> GetSeriesIssueListAsync(
        int metronSeriesId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return MetronIssueListResult.Failed(metronSeriesId, "Metron integration is disabled");
        }

        if (!settings.IsConfigured)
        {
            return MetronIssueListResult.Failed(metronSeriesId, "Metron credentials not configured");
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var cacheKey = $"{CacheKeyPrefix}series_issues:{metronSeriesId}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronIssueListResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for series issue list: {SeriesId}", metronSeriesId);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Fetching Metron issue list for series {SeriesId} (bypassCache: {Bypass})", metronSeriesId, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"series/{metronSeriesId}/issue_list/";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                RecordError();
                return MetronIssueListResult.Failed(metronSeriesId, "Authentication failed", 401);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                RecordSuccess();
                var notFoundResult = MetronIssueListResult.NotFound(metronSeriesId, $"Series {metronSeriesId} not found");
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for series {SeriesId} issue list", 
                    (int)response.StatusCode, metronSeriesId);
                RecordError();
                return MetronIssueListResult.Failed(
                    metronSeriesId,
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return MetronIssueListResult.Failed(metronSeriesId, "Rate limited by Metron API", 429);
            }

            var apiResponse = JsonSerializer.Deserialize<MetronApiListResponse>(content!, JsonOptions);

            if (apiResponse == null)
            {
                RecordSuccess();
                var emptyResult = MetronIssueListResult.Found(metronSeriesId, new List<MetronIssue>(), 0);
                _cache.Set(cacheKey, emptyResult, TimeSpan.FromHours(settings.CacheTtlHours));
                return emptyResult;
            }

            RecordSuccess();
            var issues = apiResponse.Results.Select(MapToMetronIssue).ToList();
            var result = MetronIssueListResult.Found(metronSeriesId, issues, apiResponse.Count);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours));

            _logger?.LogInformation(
                "Fetched Metron issue list for series {SeriesId}: {Count} issues",
                metronSeriesId, issues.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error fetching Metron series {SeriesId} issue list", metronSeriesId);
            RecordError();
            return MetronIssueListResult.Failed(metronSeriesId, $"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("Metron lookup cancelled for series {SeriesId} issue list", metronSeriesId);
            return MetronIssueListResult.Failed(metronSeriesId, "Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error fetching Metron series {SeriesId} issue list", metronSeriesId);
            RecordError();
            return MetronIssueListResult.Failed(metronSeriesId, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<MetronIssueResult> GetIssueByIdAsync(
        int metronIssueId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        
        if (!settings.Enabled)
        {
            return MetronIssueResult.Failed("Metron integration is disabled");
        }

        if (!settings.IsConfigured)
        {
            return MetronIssueResult.Failed("Metron credentials not configured");
        }
        
        await ConfigureAuthAsync(cancellationToken);

        var cacheKey = $"{CacheKeyPrefix}issue:{metronIssueId}";

        if (!bypassCache && _cache.TryGetValue(cacheKey, out MetronIssueResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for issue ID: {IssueId}", metronIssueId);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Fetching Metron issue by ID: {IssueId} (bypassCache: {Bypass})", metronIssueId, bypassCache);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"issue/{metronIssueId}/";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                RecordError();
                return MetronIssueResult.Failed("Authentication failed", 401);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                RecordSuccess();
                var notFoundResult = MetronIssueResult.NotFound($"Issue {metronIssueId} not found");
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for issue ID {IssueId}", 
                    (int)response.StatusCode, metronIssueId);
                RecordError();
                return MetronIssueResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            var (content, isRateLimited) = await ReadAndValidateResponseAsync(response, cancellationToken);
            if (isRateLimited)
            {
                RecordError(isRateLimitError: true);
                return MetronIssueResult.Failed("Rate limited by Metron API", 429);
            }

            var apiIssue = JsonSerializer.Deserialize<MetronApiIssue>(content!, JsonOptions);

            if (apiIssue == null)
            {
                RecordSuccess();
                var notFoundResult = MetronIssueResult.NotFound($"Issue {metronIssueId} not found");
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            RecordSuccess();
            var issue = MapToMetronIssue(apiIssue);
            var result = MetronIssueResult.Found(issue);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(settings.CacheTtlHours));

            _logger?.LogInformation(
                "Fetched Metron issue {IssueId}: {Series} #{Number} (title: {HasTitle}, desc: {HasDesc})",
                metronIssueId, issue.Series?.Name, issue.Number, 
                !string.IsNullOrEmpty(issue.Title) || issue.StoryNames.Count > 0,
                !string.IsNullOrEmpty(issue.Description));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error fetching Metron issue {IssueId}", metronIssueId);
            RecordError();
            return MetronIssueResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("Metron lookup cancelled for issue {IssueId}", metronIssueId);
            return MetronIssueResult.Failed("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error fetching Metron issue {IssueId}", metronIssueId);
            RecordError();
            return MetronIssueResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.Enabled || !settings.IsConfigured)
        {
            return false;
        }
        
        await ConfigureAuthAsync(cancellationToken);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Make a simple authenticated request
            var response = await _httpClient.GetAsync("publisher/?page=1", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task RateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Check circuit breaker
            if (IsCircuitBreakerOpen())
            {
                var waitTime = _circuitBreakerResetTime - DateTime.UtcNow;
                _logger?.LogWarning(
                    "Metron circuit breaker is open. Waiting {Seconds:F0}s before retry",
                    waitTime.TotalSeconds);
                await Task.Delay(waitTime, cancellationToken);
                // Reset circuit breaker after waiting
                _consecutiveErrors = 0;
            }
            
            // Calculate delay with exponential backoff
            var baseDelay = MinDelayMs;
            if (_consecutiveErrors > 0)
            {
                // Exponential backoff: 2s, 4s, 8s, 16s, max 30s
                var backoffMs = Math.Min(
                    baseDelay * (int)Math.Pow(2, _consecutiveErrors),
                    (int)MaxBackoffDelay.TotalMilliseconds);
                baseDelay = backoffMs;
                _logger?.LogDebug(
                    "Applying exponential backoff: {Delay}ms (consecutive errors: {Errors})",
                    backoffMs, _consecutiveErrors);
            }
            
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed.TotalMilliseconds < baseDelay)
            {
                var delayMs = baseDelay - (int)elapsed.TotalMilliseconds;
                await Task.Delay(delayMs, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
    
    private bool IsCircuitBreakerOpen()
    {
        return _consecutiveErrors >= CircuitBreakerThreshold && 
               DateTime.UtcNow < _circuitBreakerResetTime;
    }
    
    private void RecordSuccess()
    {
        _consecutiveErrors = 0;
    }
    
    private void RecordError(bool isRateLimitError = false)
    {
        _consecutiveErrors++;
        
        if (_consecutiveErrors >= CircuitBreakerThreshold)
        {
            // If it's a rate limit error, wait longer
            var duration = isRateLimitError 
                ? TimeSpan.FromMinutes(10) 
                : CircuitBreakerDuration;
            _circuitBreakerResetTime = DateTime.UtcNow + duration;
            _logger?.LogWarning(
                "Metron circuit breaker opened after {Errors} consecutive errors. Will reset at {ResetTime:HH:mm:ss}",
                _consecutiveErrors, _circuitBreakerResetTime);
        }
    }
    
    /// <summary>
    /// Reads response content and checks for HTML rate limit pages.
    /// Returns null if the response is valid JSON, otherwise returns an error result.
    /// </summary>
    private async Task<(string? content, bool isRateLimited)> ReadAndValidateResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Check for HTML response (rate limit page returns 200 OK but HTML)
        if (!string.IsNullOrEmpty(content) && 
            (content.TrimStart().StartsWith('<') || content.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)))
        {
            _logger?.LogWarning(
                "Metron returned HTML instead of JSON - likely rate limited. Response starts with: {Preview}",
                content.Length > 100 ? content[..100] + "..." : content);
            return (content, true);
        }
        
        return (content, false);
    }

    private static MetronIssue MapToMetronIssue(MetronApiIssue apiIssue)
    {
        return new MetronIssue
        {
            Id = apiIssue.Id,
            Number = apiIssue.Number ?? string.Empty,
            Title = apiIssue.Title,
            StoryNames = apiIssue.Name ?? new List<string>(),
            CoverDate = ParseDate(apiIssue.CoverDate),
            StoreDate = ParseDate(apiIssue.StoreDate),
            ImageUrl = apiIssue.Image,
            Price = apiIssue.Price,
            CvId = apiIssue.CvId,
            GcdId = apiIssue.GcdId,
            Description = apiIssue.Desc,
            DisplayName = apiIssue.Issue,
            Series = apiIssue.Series != null ? new MetronSeries
            {
                Id = apiIssue.Series.Id,
                Name = apiIssue.Series.Name ?? string.Empty,
                Volume = apiIssue.Series.Volume,
                YearBegan = apiIssue.Series.YearBegan,
                Publisher = apiIssue.Series.Publisher != null ? new MetronPublisher
                {
                    Id = apiIssue.Series.Publisher.Id,
                    Name = apiIssue.Series.Publisher.Name ?? string.Empty
                } : null
            } : null
        };
    }

    private static MetronSeries MapToMetronSeries(MetronApiSeriesDetail apiSeries)
    {
        return new MetronSeries
        {
            Id = apiSeries.Id,
            Name = apiSeries.Name ?? string.Empty,
            Volume = apiSeries.Volume,
            YearBegan = apiSeries.YearBegan,
            CvId = apiSeries.CvId,
            Publisher = apiSeries.Publisher != null ? new MetronPublisher
            {
                Id = apiSeries.Publisher.Id,
                Name = apiSeries.Publisher.Name ?? string.Empty
            } : null
        };
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return null;

        if (DateTime.TryParse(dateStr, out var date))
            return date;

        return null;
    }
}

// Internal DTOs for Metron API responses

internal class MetronApiListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<MetronApiIssue> Results { get; set; } = new();
}

internal class MetronApiIssue
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("series")]
    public MetronApiSeries? Series { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("name")]
    public List<string>? Name { get; set; }

    [JsonPropertyName("issue")]
    public string? Issue { get; set; }

    [JsonPropertyName("cover_date")]
    public string? CoverDate { get; set; }

    [JsonPropertyName("store_date")]
    public string? StoreDate { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("cv_id")]
    public int? CvId { get; set; }

    [JsonPropertyName("gcd_id")]
    public int? GcdId { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

internal class MetronApiSeries
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("volume")]
    public int? Volume { get; set; }

    [JsonPropertyName("year_began")]
    public int? YearBegan { get; set; }

    [JsonPropertyName("publisher")]
    public MetronApiPublisher? Publisher { get; set; }
}

internal class MetronApiPublisher
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal class MetronApiSeriesListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<MetronApiSeriesDetail> Results { get; set; } = new();
}

internal class MetronApiSeriesDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sort_name")]
    public string? SortName { get; set; }

    [JsonPropertyName("volume")]
    public int? Volume { get; set; }

    [JsonPropertyName("year_began")]
    public int? YearBegan { get; set; }

    [JsonPropertyName("year_end")]
    public int? YearEnd { get; set; }

    [JsonPropertyName("cv_id")]
    public int? CvId { get; set; }

    [JsonPropertyName("publisher")]
    public MetronApiPublisher? Publisher { get; set; }
}
