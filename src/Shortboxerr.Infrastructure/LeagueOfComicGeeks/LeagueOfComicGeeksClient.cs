using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.LeagueOfComicGeeks;

namespace Shortboxerr.Infrastructure.LeagueOfComicGeeks;

/// <summary>
/// HTTP client implementation for League of Comic Geeks.
/// 
/// IMPORTANT: This uses unofficial HTML scraping as LOCG has no public API.
/// The site structure may change at any time, breaking this implementation.
/// Graceful degradation is implemented to handle parse failures.
/// </summary>
public partial class LeagueOfComicGeeksClient : ILeagueOfComicGeeksClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LeagueOfComicGeeksClient>? _logger;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;

    private const string BaseUrl = "https://leagueofcomicgeeks.com";
    private const string GetComicsEndpoint = "/comic/get_comics";
    private const string CacheKeyPrefix = "locg:";
    private const int MinDelayMs = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public LeagueOfComicGeeksClient(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<LeagueOfComicGeeksClient>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/html, */*");
    }

    public async Task<LocgSearchResult> SearchIssueAsync(
        string seriesName,
        string? issueNumber = null,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = string.IsNullOrWhiteSpace(issueNumber)
            ? seriesName.Trim()
            : $"{seriesName.Trim()} {issueNumber}".Trim();

        var cacheKey = $"{CacheKeyPrefix}search:{searchQuery.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out LocgSearchResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("LOCG cache HIT for search: {Query}", searchQuery);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogInformation("Searching LOCG for: {Query}", searchQuery);

        try
        {
            await RateLimitAsync(cancellationToken);

            var encodedQuery = Uri.EscapeDataString(searchQuery.ToLowerInvariant());
            var url = $"{GetComicsEndpoint}?list=search&list_option=series&view=thumbs&title={encodedQuery}&order=alpha-asc&format[]=1&format[]=6";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var result = await ParseResponseAsync(response, cancellationToken);

            if (result.Success)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error searching LOCG");
            return CreateErrorResult($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("LOCG search request cancelled");
            return CreateErrorResult("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error searching LOCG");
            return CreateErrorResult($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<LocgSearchResult> GetWeeklyReleasesAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var dateStr = date.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
        var cacheKey = $"{CacheKeyPrefix}releases:{dateStr}";

        if (_cache.TryGetValue(cacheKey, out LocgSearchResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("LOCG cache HIT for releases: {Date}", dateStr);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogInformation("Fetching LOCG releases for: {Date}", dateStr);

        try
        {
            await RateLimitAsync(cancellationToken);

            var url = $"{GetComicsEndpoint}?list=releases&view=thumbs&format[]=1&format[]=6&date_type=week&date={Uri.EscapeDataString(dateStr)}&order=pulls";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var result = await ParseResponseAsync(response, cancellationToken);

            if (result.Success)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromHours(4));
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error fetching LOCG releases");
            return CreateErrorResult($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error fetching LOCG releases");
            return CreateErrorResult($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var url = $"{GetComicsEndpoint}?list=releases&view=thumbs&format[]=1&date_type=week&order=pulls";
            var response = await _httpClient.GetAsync(url, cts.Token);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<LocgSearchResult> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var result = new LocgSearchResult
        {
            StatusCode = (int)response.StatusCode,
            FetchedAt = DateTime.UtcNow
        };

        if (!response.IsSuccessStatusCode)
        {
            result.Success = false;
            result.Error = $"HTTP {result.StatusCode}: {response.ReasonPhrase}";
            _logger?.LogWarning("LOCG returned status {StatusCode}", result.StatusCode);
            return result;
        }

        try
        {
            var apiResponse = await response.Content.ReadFromJsonAsync<LocgApiResponse>(
                JsonOptions, cancellationToken);

            if (apiResponse == null)
            {
                result.Success = false;
                result.Error = "Failed to parse LOCG response JSON";
                return result;
            }

            result.TotalCount = apiResponse.Count;

            if (apiResponse.Count == 0 || string.IsNullOrWhiteSpace(apiResponse.List))
            {
                result.Success = true;
                return result;
            }

            result.Issues = await ParseHtmlListAsync(apiResponse.List, cancellationToken);
            result.Success = true;

            _logger?.LogInformation("Parsed {Count} issues from LOCG", result.Issues.Count);
            return result;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse LOCG JSON response - site structure may have changed");
            result.Success = false;
            result.Error = "JSON parse error - site structure may have changed";
            return result;
        }
    }

    private async Task<List<LocgIssue>> ParseHtmlListAsync(string html, CancellationToken cancellationToken)
    {
        var issues = new List<LocgIssue>();

        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var parser = context.GetService<IHtmlParser>();

            if (parser == null)
            {
                _logger?.LogWarning("HTML parser not available");
                return issues;
            }

            var document = await parser.ParseDocumentAsync(html, cancellationToken);
            var listItems = document.QuerySelectorAll("li");

            foreach (var item in listItems)
            {
                try
                {
                    var issue = ParseIssueElement(item);
                    if (issue != null)
                    {
                        issues.Add(issue);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to parse individual LOCG issue element");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse LOCG HTML - site structure may have changed");
        }

        return issues;
    }

    private LocgIssue? ParseIssueElement(AngleSharp.Dom.IElement item)
    {
        var link = item.QuerySelector("a");
        if (link == null) return null;

        var href = link.GetAttribute("href");
        if (string.IsNullOrEmpty(href)) return null;

        var issueIdMatch = IssueIdRegex().Match(href);
        if (!issueIdMatch.Success || !int.TryParse(issueIdMatch.Groups[1].Value, out var issueId))
            return null;

        var issue = new LocgIssue
        {
            IssueId = issueId,
            Url = href
        };

        var titleElement = item.QuerySelector(".title");
        if (titleElement != null)
        {
            issue.Name = titleElement.TextContent.Trim();
            ParseIssueName(issue);
        }

        var publisherElement = item.QuerySelector(".publisher");
        if (publisherElement != null)
        {
            issue.Publisher = publisherElement.TextContent.Trim();
        }

        var imgElement = item.QuerySelector("img");
        if (imgElement != null)
        {
            issue.CoverUrl = imgElement.GetAttribute("data-src") ?? imgElement.GetAttribute("src");
        }

        if (string.IsNullOrEmpty(issue.CoverUrl))
        {
            issue.CoverUrl = $"https://s3.amazonaws.com/comicgeeks/comics/covers/large-{issueId}.jpg";
        }

        var pullsAttr = item.GetAttribute("data-pulls");
        if (!string.IsNullOrEmpty(pullsAttr) && int.TryParse(pullsAttr, out var pulls))
        {
            issue.PullCount = pulls;
        }

        var ratingAttr = item.GetAttribute("data-community");
        if (!string.IsNullOrEmpty(ratingAttr) && int.TryParse(ratingAttr, out var rating))
        {
            issue.Rating = rating;
        }

        var priceElement = item.QuerySelector(".price");
        if (priceElement != null)
        {
            var priceText = priceElement.TextContent;
            var priceMatch = PriceRegex().Match(priceText);
            if (priceMatch.Success && decimal.TryParse(priceMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                issue.Price = price;
            }
        }

        var dateElement = item.QuerySelector(".date");
        if (dateElement != null)
        {
            var dateAttr = dateElement.GetAttribute("data-date");
            if (!string.IsNullOrEmpty(dateAttr) && long.TryParse(dateAttr, out var timestamp))
            {
                issue.StoreDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            }
        }

        return issue;
    }

    private static void ParseIssueName(LocgIssue issue)
    {
        var name = issue.Name;
        if (string.IsNullOrEmpty(name)) return;

        var match = IssueNameRegex().Match(name);
        if (match.Success)
        {
            issue.SeriesName = match.Groups[1].Value.Trim();
            issue.IssueNumber = match.Groups[2].Value.Trim();
        }
        else
        {
            issue.SeriesName = name;
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

    private static LocgSearchResult CreateErrorResult(string error)
    {
        return new LocgSearchResult
        {
            Success = false,
            Error = error,
            FetchedAt = DateTime.UtcNow
        };
    }

    [GeneratedRegex(@"/comic/(\d+)/")]
    private static partial Regex IssueIdRegex();

    [GeneratedRegex(@"\$([0-9.]+)")]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"^(.+?)\s*#(\d+[A-Za-z]*)$")]
    private static partial Regex IssueNameRegex();
}

/// <summary>
/// Internal DTO for LOCG API response.
/// The API returns JSON with HTML embedded in the "list" field.
/// </summary>
internal class LocgApiResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("list")]
    public string? List { get; set; }
}
