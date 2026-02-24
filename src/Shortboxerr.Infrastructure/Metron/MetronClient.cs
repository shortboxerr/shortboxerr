using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Metron;

namespace Shortboxerr.Infrastructure.Metron;

/// <summary>
/// HTTP client implementation for Metron comic database API.
/// 
/// Metron provides an official REST API with Basic Auth.
/// Key advantage: Direct ComicVine ID lookup via cv_id parameter.
/// </summary>
public class MetronClient : IMetronClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MetronClient>? _logger;
    private readonly MetronSettings _settings;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;

    private const string BaseUrl = "https://metron.cloud/api";
    private const string CacheKeyPrefix = "metron:";
    private const int MinDelayMs = 2000; // 30 requests/min = 2s between requests

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public MetronClient(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<MetronSettings> settings,
        ILogger<MetronClient>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Set User-Agent (required by Metron - must not be a browser agent)
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shortboxerr/1.0 (+https://github.com/shortboxerr/shortboxerr)");

        // Set Basic Auth if credentials are configured
        if (_settings.IsConfigured)
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<MetronIssueResult> GetIssueByCvIdAsync(
        int comicVineIssueId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return MetronIssueResult.Failed("Metron integration is disabled");
        }

        if (!IsConfigured)
        {
            return MetronIssueResult.Failed("Metron credentials not configured");
        }

        var cacheKey = $"{CacheKeyPrefix}cv:{comicVineIssueId}";

        if (_cache.TryGetValue(cacheKey, out MetronIssueResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for CV ID: {CvId}", comicVineIssueId);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Looking up Metron issue by CV ID: {CvId}", comicVineIssueId);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"/issue/?cv_id={comicVineIssueId}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger?.LogWarning("Metron authentication failed - check credentials");
                return MetronIssueResult.Failed("Authentication failed", 401);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Metron returned {StatusCode} for CV ID {CvId}", 
                    (int)response.StatusCode, comicVineIssueId);
                return MetronIssueResult.Failed(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<MetronApiListResponse>(
                JsonOptions, cancellationToken);

            if (apiResponse == null || apiResponse.Results.Count == 0)
            {
                var notFoundResult = MetronIssueResult.NotFound($"No issue found with CV ID {comicVineIssueId}");
                // Cache not-found results for a shorter time
                _cache.Set(cacheKey, notFoundResult, TimeSpan.FromHours(4));
                return notFoundResult;
            }

            var issue = MapToMetronIssue(apiResponse.Results[0]);
            var result = MetronIssueResult.Found(issue);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheTtlHours));

            _logger?.LogInformation(
                "Found Metron issue for CV ID {CvId}: {Series} #{Number} (cover: {HasCover})",
                comicVineIssueId, issue.Series?.Name, issue.Number, !string.IsNullOrEmpty(issue.ImageUrl));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error looking up Metron CV ID {CvId}", comicVineIssueId);
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
            return MetronIssueResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<MetronSearchResult> SearchIssueAsync(
        string seriesName,
        string issueNumber,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return new MetronSearchResult { Success = false, Error = "Metron integration is disabled" };
        }

        if (!IsConfigured)
        {
            return new MetronSearchResult { Success = false, Error = "Metron credentials not configured" };
        }

        var cacheKey = $"{CacheKeyPrefix}search:{seriesName.ToLowerInvariant()}:{issueNumber.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out MetronSearchResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("Metron cache HIT for search: {Series} #{Issue}", seriesName, issueNumber);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogDebug("Searching Metron for: {Series} #{Issue}", seriesName, issueNumber);

        try
        {
            await RateLimitAsync(cancellationToken);

            var encodedSeries = Uri.EscapeDataString(seriesName);
            var encodedNumber = Uri.EscapeDataString(issueNumber.TrimStart('#'));
            var url = $"/issue/?series_name={encodedSeries}&number={encodedNumber}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new MetronSearchResult { Success = false, Error = "Authentication failed", StatusCode = 401 };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new MetronSearchResult
                {
                    Success = false,
                    Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    StatusCode = (int)response.StatusCode
                };
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<MetronApiListResponse>(
                JsonOptions, cancellationToken);

            var result = new MetronSearchResult
            {
                Success = true,
                StatusCode = 200,
                TotalCount = apiResponse?.Count ?? 0,
                Issues = apiResponse?.Results.Select(MapToMetronIssue).ToList() ?? new List<MetronIssue>()
            };

            _cache.Set(cacheKey, result, TimeSpan.FromHours(_settings.CacheTtlHours));

            _logger?.LogInformation("Metron search found {Count} results for {Series} #{Issue}",
                result.Issues.Count, seriesName, issueNumber);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error searching Metron");
            return new MetronSearchResult { Success = false, Error = $"Network error: {ex.Message}" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error searching Metron");
            return new MetronSearchResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || !IsConfigured)
        {
            return false;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Make a simple authenticated request
            var response = await _httpClient.GetAsync("/publisher/?page=1", cts.Token);
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
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed.TotalMilliseconds < MinDelayMs)
            {
                var delayMs = MinDelayMs - (int)elapsed.TotalMilliseconds;
                await Task.Delay(delayMs, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private static MetronIssue MapToMetronIssue(MetronApiIssue apiIssue)
    {
        return new MetronIssue
        {
            Id = apiIssue.Id,
            Number = apiIssue.Number ?? string.Empty,
            CoverDate = ParseDate(apiIssue.CoverDate),
            StoreDate = ParseDate(apiIssue.StoreDate),
            ImageUrl = apiIssue.Image,
            CvId = apiIssue.CvId,
            GcdId = apiIssue.GcdId,
            Description = apiIssue.Desc,
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

    [JsonPropertyName("cover_date")]
    public string? CoverDate { get; set; }

    [JsonPropertyName("store_date")]
    public string? StoreDate { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

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
