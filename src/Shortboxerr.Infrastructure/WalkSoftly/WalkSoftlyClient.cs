using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.WalkSoftly;

namespace Shortboxerr.Infrastructure.WalkSoftly;

/// <summary>
/// HTTP client implementation for the WalkSoftly comic release aggregator.
/// </summary>
public class WalkSoftlyClient : IWalkSoftlyClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WalkSoftlyClient>? _logger;

    private const string BaseUrl = "https://walksoftly.itsaninja.party";
    private const string NewComicsEndpoint = "/newcomics.php";
    private const string CacheKeyPrefix = "walksoftly:week:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public WalkSoftlyClient(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<WalkSoftlyClient>? logger = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Shortboxerr/1.0");
    }

    public async Task<WalkSoftlyResult> GetWeeklyReleasesAsync(
        int weekNumber,
        int year,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{year}-{weekNumber:D2}";

        if (_cache.TryGetValue(cacheKey, out WalkSoftlyResult? cachedResult) && cachedResult != null)
        {
            _logger?.LogDebug("WalkSoftly cache HIT for week {Year}-{Week}", year, weekNumber);
            cachedResult.FromCache = true;
            return cachedResult;
        }

        _logger?.LogInformation("Fetching WalkSoftly releases for week {Week}, {Year}", weekNumber, year);

        try
        {
            var url = $"{NewComicsEndpoint}?week={weekNumber}&year={year}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            var result = new WalkSoftlyResult
            {
                WeekNumber = weekNumber,
                Year = year,
                StatusCode = (int)response.StatusCode,
                FetchedAt = DateTime.UtcNow
            };

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Error = HandleErrorResponse(response);
                _logger?.LogWarning("WalkSoftly returned status {StatusCode}: {Error}", 
                    result.StatusCode, result.Error);
                return result;
            }

            var apiReleases = await response.Content.ReadFromJsonAsync<List<WalkSoftlyApiRelease>>(
                JsonOptions, cancellationToken);

            if (apiReleases == null)
            {
                result.Success = false;
                result.Error = "Failed to parse WalkSoftly response";
                return result;
            }

            result.Success = true;
            result.Releases = apiReleases.Select(MapToRelease).ToList();

            _logger?.LogInformation("Retrieved {Count} releases from WalkSoftly for week {Week}, {Year}",
                result.Releases.Count, weekNumber, year);

            _cache.Set(cacheKey, result, TimeSpan.FromHours(4));

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error fetching WalkSoftly releases");
            return new WalkSoftlyResult
            {
                Success = false,
                Error = $"Network error: {ex.Message}",
                WeekNumber = weekNumber,
                Year = year,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDebug("WalkSoftly request cancelled");
            return new WalkSoftlyResult
            {
                Success = false,
                Error = "Request cancelled",
                WeekNumber = weekNumber,
                Year = year,
                FetchedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error fetching WalkSoftly releases");
            return new WalkSoftlyResult
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}",
                WeekNumber = weekNumber,
                Year = year,
                FetchedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var currentWeek = GetCurrentWeekNumber();
            var currentYear = DateTime.UtcNow.Year;

            var url = $"{NewComicsEndpoint}?week={currentWeek}&year={currentYear}";
            var response = await _httpClient.GetAsync(url, cts.Token);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string HandleErrorResponse(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;

        return statusCode switch
        {
            619 => "Invalid date or no date supplied",
            522 => "WalkSoftly service is currently offline",
            999 or 111 => "Site-specific error - unable to retrieve data",
            666 => "Client update required",
            _ => $"HTTP {statusCode}: {response.ReasonPhrase}"
        };
    }

    private static WalkSoftlyRelease MapToRelease(WalkSoftlyApiRelease api)
    {
        return new WalkSoftlyRelease
        {
            Series = api.Series ?? string.Empty,
            Alias = api.Alias,
            Issue = api.Issue ?? string.Empty,
            Publisher = api.Publisher ?? string.Empty,
            ShipDate = ParseDate(api.ShipDate),
            CoverDate = ParseDate(api.CoverDate),
            ComicId = api.ComicId,
            IssueId = api.IssueId,
            WeekNumber = api.WeekNumber ?? 0,
            Year = api.Year ?? DateTime.UtcNow.Year,
            Volume = api.Volume,
            SeriesYear = api.SeriesYear,
            AnnualLink = api.Link,
            Format = api.Type
        };
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        return null;
    }

    private static int GetCurrentWeekNumber()
    {
        var today = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        return cal.GetWeekOfYear(today, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}

/// <summary>
/// Internal DTO for WalkSoftly API response parsing.
/// Matches the JSON structure from walksoftly.itsaninja.party/newcomics.php
/// </summary>
internal class WalkSoftlyApiRelease
{
    [JsonPropertyName("series")]
    public string? Series { get; set; }

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("issue")]
    public string? Issue { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("shipdate")]
    public string? ShipDate { get; set; }

    [JsonPropertyName("coverdate")]
    public string? CoverDate { get; set; }

    [JsonPropertyName("comicid")]
    public int? ComicId { get; set; }

    [JsonPropertyName("issueid")]
    public int? IssueId { get; set; }

    [JsonPropertyName("weeknumber")]
    public int? WeekNumber { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("volume")]
    public string? Volume { get; set; }

    [JsonPropertyName("seriesyear")]
    public string? SeriesYear { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Helper class for publisher filtering with wildcard support.
/// </summary>
public static class PublisherFilter
{
    /// <summary>
    /// Checks if a publisher should be ignored based on the ignored publishers list.
    /// Supports wildcards: "*Manga*" matches any publisher containing "Manga".
    /// </summary>
    /// <param name="publisher">Publisher name to check</param>
    /// <param name="ignoredPublishers">List of ignored publisher patterns</param>
    /// <returns>True if publisher should be ignored</returns>
    public static bool ShouldIgnore(string? publisher, IEnumerable<string>? ignoredPublishers)
    {
        if (string.IsNullOrWhiteSpace(publisher) || ignoredPublishers == null)
            return false;

        foreach (var pattern in ignoredPublishers)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(publisher, regexPattern, RegexOptions.IgnoreCase))
                    return true;
            }
            else
            {
                if (publisher.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Filters a list of releases by removing ignored publishers.
    /// </summary>
    public static List<WalkSoftlyRelease> FilterByPublisher(
        IEnumerable<WalkSoftlyRelease> releases,
        IEnumerable<string>? ignoredPublishers)
    {
        if (ignoredPublishers == null || !ignoredPublishers.Any())
            return releases.ToList();

        var ignored = ignoredPublishers.ToList();
        return releases.Where(r => !ShouldIgnore(r.Publisher, ignored)).ToList();
    }
}
