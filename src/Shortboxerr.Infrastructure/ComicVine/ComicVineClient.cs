using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of the ComicVine API client.
/// </summary>
public class ComicVineClient : IComicVineClient
{
    private const string BaseUrl = "https://comicvine.gamespot.com/api";
    private const int RateLimitPerHour = 200;
    private const string UserAgent = "Shortboxerr/1.0 (Comic Management App)";

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ComicVineClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Rate limiting state
    private readonly object _rateLimitLock = new();
    private int _requestsThisWindow;
    private DateTime _windowStart = DateTime.UtcNow;

    // API key cache
    private string? _cachedApiKey;
    private DateTime _apiKeyLastChecked = DateTime.MinValue;
    private readonly TimeSpan _apiKeyCheckInterval = TimeSpan.FromMinutes(5);

    public ComicVineClient(
        HttpClient httpClient,
        ISettingsService settingsService,
        IMemoryCache cache,
        ILogger<ComicVineClient> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _cache = cache;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public bool IsConfigured => !string.IsNullOrEmpty(GetApiKeySync());

    public async Task<ComicVineTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apiKey = await GetApiKeyAsync(cancellationToken);
            if (string.IsNullOrEmpty(apiKey))
            {
                return new ComicVineTestResult
                {
                    Success = false,
                    Message = "ComicVine API key not configured"
                };
            }

            // Make a simple search request to validate the API key
            var response = await MakeRequestAsync<ComicVineApiResponse<List<object>>>(
                $"/search/?api_key={apiKey}&format=json&resources=volume&limit=1&query=test",
                cancellationToken);

            stopwatch.Stop();

            if (response.StatusCode == 1)
            {
                return new ComicVineTestResult
                {
                    Success = true,
                    Message = "ComicVine connection successful",
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    ApiVersion = response.Version
                };
            }

            return new ComicVineTestResult
            {
                Success = false,
                Message = $"ComicVine API error: {response.Error}",
                LatencyMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineTestResult
            {
                Success = false,
                Message = "ComicVine rate limit exceeded. Please try again later.",
                LatencyMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (ComicVineApiKeyInvalidException)
        {
            return new ComicVineTestResult
            {
                Success = false,
                Message = "Invalid ComicVine API key. Please check your key and try again.",
                LatencyMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test ComicVine connection");
            return new ComicVineTestResult
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}",
                LatencyMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<ComicVineSearchResult<ComicVineVolume>> SearchVolumesAsync(
        string query,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var offset = (page - 1) * limit;
        var cacheKey = $"cv:search:volumes:{query}:{page}:{limit}";

        if (_cache.TryGetValue(cacheKey, out ComicVineSearchResult<ComicVineVolume>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineSearchResult<ComicVineVolume>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/search/?api_key={apiKey}&format=json&resources=volume&limit={limit}&offset={offset}&query={Uri.EscapeDataString(query)}";
            var response = await MakeRequestAsync<ComicVineApiResponse<List<ComicVineApiVolume>>>(url, cancellationToken);

            var result = new ComicVineSearchResult<ComicVineVolume>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Results = response.Results?.Select(MapVolume).ToList() ?? new(),
                TotalResults = response.NumberOfTotalResults,
                Page = page,
                Limit = limit,
                NumberOfPageResults = response.NumberOfPageResults
            };

            if (result.Success)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineSearchResult<ComicVineVolume>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public async Task<ComicVineSearchResult<ComicVineIssue>> SearchIssuesAsync(
        string query,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var offset = (page - 1) * limit;
        var cacheKey = $"cv:search:issues:{query}:{page}:{limit}";

        if (_cache.TryGetValue(cacheKey, out ComicVineSearchResult<ComicVineIssue>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineSearchResult<ComicVineIssue>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/search/?api_key={apiKey}&format=json&resources=issue&limit={limit}&offset={offset}&query={Uri.EscapeDataString(query)}";
            var response = await MakeRequestAsync<ComicVineApiResponse<List<ComicVineApiIssue>>>(url, cancellationToken);

            var result = new ComicVineSearchResult<ComicVineIssue>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Results = response.Results?.Select(MapIssue).ToList() ?? new(),
                TotalResults = response.NumberOfTotalResults,
                Page = page,
                Limit = limit,
                NumberOfPageResults = response.NumberOfPageResults
            };

            if (result.Success)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineSearchResult<ComicVineIssue>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public async Task<ComicVineResult<ComicVineVolume>> GetVolumeAsync(
        int volumeId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"cv:volume:{volumeId}";

        if (_cache.TryGetValue(cacheKey, out ComicVineResult<ComicVineVolume>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineResult<ComicVineVolume>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/volume/4050-{volumeId}/?api_key={apiKey}&format=json";
            var response = await MakeRequestAsync<ComicVineApiResponse<ComicVineApiVolume>>(url, cancellationToken);

            var result = new ComicVineResult<ComicVineVolume>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Data = response.Results != null ? MapVolume(response.Results) : null
            };

            if (result.Success && result.Data != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineResult<ComicVineVolume>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public async Task<ComicVineResult<ComicVineIssue>> GetIssueAsync(
        int issueId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"cv:issue:{issueId}";

        if (_cache.TryGetValue(cacheKey, out ComicVineResult<ComicVineIssue>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineResult<ComicVineIssue>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/issue/4000-{issueId}/?api_key={apiKey}&format=json";
            var response = await MakeRequestAsync<ComicVineApiResponse<ComicVineApiIssue>>(url, cancellationToken);

            var result = new ComicVineResult<ComicVineIssue>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Data = response.Results != null ? MapIssue(response.Results) : null
            };

            if (result.Success && result.Data != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineResult<ComicVineIssue>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public async Task<ComicVineSearchResult<ComicVineIssue>> GetVolumeIssuesAsync(
        int volumeId,
        int page = 1,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var offset = (page - 1) * limit;
        var cacheKey = $"cv:volume:{volumeId}:issues:{page}:{limit}";

        if (_cache.TryGetValue(cacheKey, out ComicVineSearchResult<ComicVineIssue>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineSearchResult<ComicVineIssue>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/issues/?api_key={apiKey}&format=json&filter=volume:{volumeId}&limit={limit}&offset={offset}&sort=issue_number:asc";
            var response = await MakeRequestAsync<ComicVineApiResponse<List<ComicVineApiIssue>>>(url, cancellationToken);

            var result = new ComicVineSearchResult<ComicVineIssue>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Results = response.Results?.Select(MapIssue).ToList() ?? new(),
                TotalResults = response.NumberOfTotalResults,
                Page = page,
                Limit = limit,
                NumberOfPageResults = response.NumberOfPageResults
            };

            if (result.Success)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineSearchResult<ComicVineIssue>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public async Task<ComicVineResult<ComicVinePublisher>> GetPublisherAsync(
        int publisherId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"cv:publisher:{publisherId}";

        if (_cache.TryGetValue(cacheKey, out ComicVineResult<ComicVinePublisher>? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ComicVineResult<ComicVinePublisher>
            {
                Success = false,
                Error = "ComicVine API key not configured"
            };
        }

        try
        {
            var url = $"/publisher/4010-{publisherId}/?api_key={apiKey}&format=json";
            var response = await MakeRequestAsync<ComicVineApiResponse<ComicVineApiPublisher>>(url, cancellationToken);

            var result = new ComicVineResult<ComicVinePublisher>
            {
                Success = response.StatusCode == 1,
                Error = response.StatusCode != 1 ? response.Error : null,
                StatusCode = response.StatusCode,
                Data = response.Results != null ? MapPublisher(response.Results) : null
            };

            if (result.Success && result.Data != null)
            {
                // Publishers rarely change, cache for a week
                _cache.Set(cacheKey, result, TimeSpan.FromDays(7));
            }

            return result;
        }
        catch (ComicVineRateLimitException)
        {
            return new ComicVineResult<ComicVinePublisher>
            {
                Success = false,
                Error = "Rate limit exceeded"
            };
        }
    }

    public ComicVineRateLimitStatus GetRateLimitStatus()
    {
        lock (_rateLimitLock)
        {
            ResetRateLimitWindowIfNeeded();
            return new ComicVineRateLimitStatus
            {
                RequestsUsed = _requestsThisWindow,
                RequestLimit = RateLimitPerHour,
                WindowResetTime = _windowStart.AddHours(1)
            };
        }
    }

    #region Private Methods

    private async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        // Check cache first
        if (_cachedApiKey != null && DateTime.UtcNow - _apiKeyLastChecked < _apiKeyCheckInterval)
        {
            return _cachedApiKey;
        }

        var settings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", null, cancellationToken);
        _cachedApiKey = settings?.ApiKey;
        _apiKeyLastChecked = DateTime.UtcNow;
        return _cachedApiKey;
    }

    private string? GetApiKeySync()
    {
        // For IsConfigured property, return cached value or null
        if (_cachedApiKey != null && DateTime.UtcNow - _apiKeyLastChecked < _apiKeyCheckInterval)
        {
            return _cachedApiKey;
        }
        return null;
    }

    private async Task<T> MakeRequestAsync<T>(string url, CancellationToken cancellationToken)
    {
        await WaitForRateLimitAsync(cancellationToken);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ComicVineRateLimitException("ComicVine rate limit exceeded");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Check if response is HTML (invalid API key or error page)
            if (content.TrimStart().StartsWith("<"))
            {
                if (content.Contains("Invalid API Key", StringComparison.OrdinalIgnoreCase) ||
                    response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ComicVineApiKeyInvalidException("Invalid ComicVine API key");
                }
                throw new InvalidOperationException("ComicVine returned an unexpected HTML response. Please verify your API key.");
            }

            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize ComicVine response");
            }

            return result;
        }
        finally
        {
            IncrementRequestCount();
        }
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        TimeSpan delay;

        lock (_rateLimitLock)
        {
            ResetRateLimitWindowIfNeeded();

            if (_requestsThisWindow < RateLimitPerHour)
            {
                return;
            }

            delay = _windowStart.AddHours(1) - DateTime.UtcNow;
        }

        if (delay > TimeSpan.Zero)
        {
            _logger.LogWarning("Rate limit reached. Waiting {Delay} before next request", delay);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private void IncrementRequestCount()
    {
        lock (_rateLimitLock)
        {
            ResetRateLimitWindowIfNeeded();
            _requestsThisWindow++;
        }
    }

    private void ResetRateLimitWindowIfNeeded()
    {
        if (DateTime.UtcNow - _windowStart > TimeSpan.FromHours(1))
        {
            _windowStart = DateTime.UtcNow;
            _requestsThisWindow = 0;
        }
    }

    #endregion

    #region Mapping Methods

    private static ComicVineVolume MapVolume(ComicVineApiVolume api)
    {
        return new ComicVineVolume
        {
            Id = api.Id,
            Name = api.Name ?? "",
            Aliases = ParseAliases(api.Aliases),
            StartYear = api.StartYear,
            Description = StripHtml(api.Description),
            Deck = api.Deck,
            Publisher = api.Publisher != null ? new ComicVinePublisherRef
            {
                Id = api.Publisher.Id,
                Name = api.Publisher.Name ?? "",
                ApiDetailUrl = api.Publisher.ApiDetailUrl
            } : null,
            IssueCount = api.CountOfIssues,
            Image = MapImage(api.Image),
            FirstIssue = api.FirstIssue != null ? new ComicVineIssueRef
            {
                Id = api.FirstIssue.Id,
                Name = api.FirstIssue.Name,
                IssueNumber = api.FirstIssue.IssueNumber ?? "",
                ApiDetailUrl = api.FirstIssue.ApiDetailUrl
            } : null,
            LastIssue = api.LastIssue != null ? new ComicVineIssueRef
            {
                Id = api.LastIssue.Id,
                Name = api.LastIssue.Name,
                IssueNumber = api.LastIssue.IssueNumber ?? "",
                ApiDetailUrl = api.LastIssue.ApiDetailUrl
            } : null,
            ApiDetailUrl = api.ApiDetailUrl,
            SiteDetailUrl = api.SiteDetailUrl,
            DateAdded = api.DateAdded,
            DateLastUpdated = api.DateLastUpdated
        };
    }

    private static ComicVineIssue MapIssue(ComicVineApiIssue api)
    {
        return new ComicVineIssue
        {
            Id = api.Id,
            Name = api.Name,
            IssueNumber = api.IssueNumber ?? "",
            Description = StripHtml(api.Description),
            CoverDate = api.CoverDate,
            StoreDate = api.StoreDate,
            Volume = api.Volume != null ? new ComicVineVolumeRef
            {
                Id = api.Volume.Id,
                Name = api.Volume.Name ?? "",
                ApiDetailUrl = api.Volume.ApiDetailUrl
            } : null,
            Image = MapImage(api.Image),
            ApiDetailUrl = api.ApiDetailUrl,
            SiteDetailUrl = api.SiteDetailUrl,
            DateAdded = api.DateAdded,
            DateLastUpdated = api.DateLastUpdated,
            StoryArcs = api.StoryArcCredits?.Select(sa => new ComicVineStoryArcRef
            {
                Id = sa.Id,
                Name = sa.Name ?? "",
                ApiDetailUrl = sa.ApiDetailUrl
            }).ToList() ?? new()
        };
    }

    private static ComicVinePublisher MapPublisher(ComicVineApiPublisher api)
    {
        return new ComicVinePublisher
        {
            Id = api.Id,
            Name = api.Name ?? "",
            Aliases = ParseAliases(api.Aliases),
            Description = StripHtml(api.Description),
            Image = MapImage(api.Image),
            SiteDetailUrl = api.SiteDetailUrl
        };
    }

    private static ComicVineImage? MapImage(ComicVineApiImage? api)
    {
        if (api == null) return null;
        return new ComicVineImage
        {
            IconUrl = api.IconUrl,
            MediumUrl = api.MediumUrl,
            ScreenUrl = api.ScreenUrl,
            ScreenLargeUrl = api.ScreenLargeUrl,
            SmallUrl = api.SmallUrl,
            SuperUrl = api.SuperUrl,
            ThumbUrl = api.ThumbUrl,
            TinyUrl = api.TinyUrl,
            OriginalUrl = api.OriginalUrl
        };
    }

    private static List<string> ParseAliases(string? aliases)
    {
        if (string.IsNullOrWhiteSpace(aliases))
            return new List<string>();

        return aliases
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        // Simple HTML stripping - removes tags
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "").Trim();
    }

    #endregion
}

#region API Response DTOs (internal)

internal class ComicVineApiResponse<T>
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("number_of_page_results")]
    public int NumberOfPageResults { get; set; }

    [JsonPropertyName("number_of_total_results")]
    public int NumberOfTotalResults { get; set; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("results")]
    public T? Results { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

internal class ComicVineApiVolume
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("aliases")]
    public string? Aliases { get; set; }

    [JsonPropertyName("start_year")]
    public int? StartYear { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("publisher")]
    public ComicVineApiPublisherRef? Publisher { get; set; }

    [JsonPropertyName("count_of_issues")]
    public int CountOfIssues { get; set; }

    [JsonPropertyName("image")]
    public ComicVineApiImage? Image { get; set; }

    [JsonPropertyName("first_issue")]
    public ComicVineApiIssueRef? FirstIssue { get; set; }

    [JsonPropertyName("last_issue")]
    public ComicVineApiIssueRef? LastIssue { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }

    [JsonPropertyName("site_detail_url")]
    public string? SiteDetailUrl { get; set; }

    [JsonPropertyName("date_added")]
    public DateTime? DateAdded { get; set; }

    [JsonPropertyName("date_last_updated")]
    public DateTime? DateLastUpdated { get; set; }
}

internal class ComicVineApiIssue
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("issue_number")]
    public string? IssueNumber { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("cover_date")]
    public DateTime? CoverDate { get; set; }

    [JsonPropertyName("store_date")]
    public DateTime? StoreDate { get; set; }

    [JsonPropertyName("volume")]
    public ComicVineApiVolumeRef? Volume { get; set; }

    [JsonPropertyName("image")]
    public ComicVineApiImage? Image { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }

    [JsonPropertyName("site_detail_url")]
    public string? SiteDetailUrl { get; set; }

    [JsonPropertyName("date_added")]
    public DateTime? DateAdded { get; set; }

    [JsonPropertyName("date_last_updated")]
    public DateTime? DateLastUpdated { get; set; }

    [JsonPropertyName("story_arc_credits")]
    public List<ComicVineApiStoryArcRef>? StoryArcCredits { get; set; }
}

internal class ComicVineApiPublisher
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("aliases")]
    public string? Aliases { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public ComicVineApiImage? Image { get; set; }

    [JsonPropertyName("site_detail_url")]
    public string? SiteDetailUrl { get; set; }
}

internal class ComicVineApiPublisherRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }
}

internal class ComicVineApiVolumeRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }
}

internal class ComicVineApiIssueRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("issue_number")]
    public string? IssueNumber { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }
}

internal class ComicVineApiStoryArcRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("api_detail_url")]
    public string? ApiDetailUrl { get; set; }
}

internal class ComicVineApiImage
{
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("medium_url")]
    public string? MediumUrl { get; set; }

    [JsonPropertyName("screen_url")]
    public string? ScreenUrl { get; set; }

    [JsonPropertyName("screen_large_url")]
    public string? ScreenLargeUrl { get; set; }

    [JsonPropertyName("small_url")]
    public string? SmallUrl { get; set; }

    [JsonPropertyName("super_url")]
    public string? SuperUrl { get; set; }

    [JsonPropertyName("thumb_url")]
    public string? ThumbUrl { get; set; }

    [JsonPropertyName("tiny_url")]
    public string? TinyUrl { get; set; }

    [JsonPropertyName("original_url")]
    public string? OriginalUrl { get; set; }
}

#endregion

/// <summary>
/// Exception thrown when ComicVine rate limit is exceeded.
/// </summary>
public class ComicVineRateLimitException : Exception
{
    public ComicVineRateLimitException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the ComicVine API key is invalid.
/// </summary>
public class ComicVineApiKeyInvalidException : Exception
{
    public ComicVineApiKeyInvalidException(string message) : base(message) { }
}

