using System.Diagnostics;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Client for interacting with Newznab-compatible NZB indexer APIs.
/// </summary>
public class NewznabClient : INewznabClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NewznabClient>? _logger;

    // Newznab standard namespaces
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace NewznabNs = "http://www.newznab.com/DTD/2010/feeds/attributes/";
    
    // NZBHydra2-specific namespace for extended attributes
    private static readonly XNamespace HydraNs = "https://github.com/theotherp/nzbhydra2/attributes/";

    public NewznabClient(HttpClient httpClient, ILogger<NewznabClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NewznabSearchResult> SearchAsync(NewznabIndexer indexer, NewznabSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var url = BuildSearchUrl(indexer, query);
            _logger?.LogDebug("Searching Newznab indexer {IndexerName}: {Url}", indexer.Name, MaskApiKey(url, indexer.ApiKey));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Newznab search failed for {IndexerName}: HTTP {StatusCode}", indexer.Name, response.StatusCode);
                return NewznabSearchResult.Error($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", (int)response.StatusCode);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for Newznab API error
            if (TryParseApiError(content, out var errorMessage))
            {
                _logger?.LogWarning("Newznab API error from {IndexerName}: {Error}", indexer.Name, errorMessage);
                return NewznabSearchResult.Error(errorMessage);
            }

            var (releases, totalResults, offset) = ParseSearchResponse(content, indexer);

            _logger?.LogDebug("Newznab search returned {Count} results from {IndexerName} in {Duration}ms",
                releases.Count, indexer.Name, stopwatch.ElapsedMilliseconds);

            return NewznabSearchResult.Ok(releases, totalResults, offset, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error searching Newznab indexer {IndexerName}", indexer.Name);
            return NewznabSearchResult.Error($"Connection error: {ex.Message}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogDebug("Search cancelled for {IndexerName}", indexer.Name);
            return NewznabSearchResult.Error("Search cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error searching Newznab indexer {IndexerName}", indexer.Name);
            return NewznabSearchResult.Error($"Unexpected error: {ex.Message}");
        }
    }

    public async Task<NewznabCapabilities> GetCapabilitiesAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildCapsUrl(indexer);
            _logger?.LogDebug("Getting capabilities from {IndexerName}: {Url}", indexer.Name, MaskApiKey(url, indexer.ApiKey));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new NewznabCapabilities
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for API error
            if (TryParseApiError(content, out var errorMessage))
            {
                return new NewznabCapabilities
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            return ParseCapabilitiesResponse(content);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting capabilities from {IndexerName}", indexer.Name);
            return new NewznabCapabilities
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<NewznabTestResult> TestConnectionAsync(NewznabIndexer indexer, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogInformation("Testing connection to Newznab indexer {IndexerName} at {BaseUrl}", indexer.Name, indexer.BaseUrl);

            // First, test connectivity by getting capabilities
            var caps = await GetCapabilitiesAsync(indexer, cancellationToken);
            stopwatch.Stop();

            if (!caps.Success)
            {
                return NewznabTestResult.Failed($"Failed to connect: {caps.ErrorMessage}");
            }

            // Verify the API key by doing a minimal search
            var searchResult = await SearchAsync(indexer, new NewznabSearchQuery { Limit = 1 }, cancellationToken);

            if (!searchResult.Success)
            {
                // Some indexers return error for API key issues
                if (searchResult.ErrorMessage?.Contains("Invalid API", StringComparison.OrdinalIgnoreCase) == true ||
                    searchResult.ErrorMessage?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true ||
                    searchResult.StatusCode == 401 || searchResult.StatusCode == 403)
                {
                    return NewznabTestResult.Failed("Invalid API key", searchResult.StatusCode);
                }
            }

            // Detect if this is an NZBHydra2 instance
            var isHydra = IsNzbHydra2(caps);
            var message = isHydra
                ? $"Connected successfully to {indexer.Name} (NZBHydra2 detected)"
                : $"Connected successfully to {indexer.Name}";

            var result = NewznabTestResult.Ok(message, caps, stopwatch.ElapsedMilliseconds);
            
            // Return with Hydra detection flag
            return result with { IsHydra = isHydra };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Connection test failed for {IndexerName}", indexer.Name);
            return NewznabTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error testing {IndexerName}", indexer.Name);
            return NewznabTestResult.Failed($"Test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects if the indexer is an NZBHydra2 instance based on capabilities.
    /// </summary>
    public static bool IsNzbHydra2(NewznabCapabilities caps)
    {
        if (caps.Server == null)
            return false;

        // NZBHydra2 identifies itself in the server info
        var title = caps.Server.Title?.ToLowerInvariant() ?? "";
        var version = caps.Server.Version?.ToLowerInvariant() ?? "";
        var strapline = caps.Server.Strapline?.ToLowerInvariant() ?? "";

        return title.Contains("nzbhydra") ||
               title.Contains("hydra") ||
               version.Contains("nzbhydra") ||
               strapline.Contains("nzbhydra") ||
               strapline.Contains("hydra2");
    }

    public async Task<byte[]> DownloadNzbAsync(NewznabIndexer indexer, string nzbUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine if nzbUrl is a full URL or just a GUID
            string downloadUrl;
            if (Uri.TryCreate(nzbUrl, UriKind.Absolute, out _))
            {
                // Full URL provided
                downloadUrl = nzbUrl;

                // Append API key if not present
                if (!downloadUrl.Contains("apikey=", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl += (downloadUrl.Contains('?') ? "&" : "?") + $"apikey={indexer.ApiKey}";
                }
            }
            else
            {
                // Assume it's a GUID, build the download URL
                downloadUrl = BuildNzbDownloadUrl(indexer, nzbUrl);
            }

            _logger?.LogDebug("Downloading NZB from {IndexerName}: {Url}", indexer.Name, MaskApiKey(downloadUrl, indexer.ApiKey));

            using var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to download NZB: HTTP {(int)response.StatusCode}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error downloading NZB from {IndexerName}", indexer.Name);
            throw;
        }
    }

    private string BuildSearchUrl(NewznabIndexer indexer, NewznabSearchQuery query)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        var sb = new StringBuilder($"{baseUrl}/api?t=search");

        // Add API key
        sb.Append($"&apikey={Uri.EscapeDataString(indexer.ApiKey)}");

        // Add search query
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            sb.Append($"&q={Uri.EscapeDataString(query.Query)}");
        }

        // Add categories
        var categories = query.Categories ?? indexer.Categories;
        if (categories.Count > 0)
        {
            sb.Append($"&cat={string.Join(",", categories)}");
        }

        // Add pagination
        sb.Append($"&limit={query.Limit}");
        if (query.Offset > 0)
        {
            sb.Append($"&offset={query.Offset}");
        }

        // Add optional parameters
        if (query.MaxAge.HasValue)
        {
            sb.Append($"&maxage={query.MaxAge.Value}");
        }

        // Add title/episode for book search (if indexer supports it)
        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            sb.Append($"&title={Uri.EscapeDataString(query.Title)}");
        }

        // Add additional indexer parameters
        foreach (var (key, value) in indexer.AdditionalParameters)
        {
            sb.Append($"&{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return sb.ToString();
    }

    private string BuildCapsUrl(NewznabIndexer indexer)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/api?t=caps&apikey={Uri.EscapeDataString(indexer.ApiKey)}";
    }

    private string BuildNzbDownloadUrl(NewznabIndexer indexer, string guid)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/api?t=get&apikey={Uri.EscapeDataString(indexer.ApiKey)}&id={Uri.EscapeDataString(guid)}";
    }

    private static bool TryParseApiError(string content, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var doc = XDocument.Parse(content);

            // Error can be the root element itself or a child element
            XElement? errorElement = null;

            if (doc.Root?.Name.LocalName.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
            {
                errorElement = doc.Root;
            }
            else
            {
                errorElement = doc.Root?.Element("error");
            }

            if (errorElement != null)
            {
                var code = errorElement.Attribute("code")?.Value ?? "unknown";
                var description = errorElement.Attribute("description")?.Value ?? "Unknown error";
                errorMessage = $"API Error {code}: {description}";
                return true;
            }
        }
        catch
        {
            // Not valid XML or no error element, not an API error
        }

        return false;
    }

    private (List<NewznabRelease> releases, int totalResults, int offset) ParseSearchResponse(string content, NewznabIndexer indexer)
    {
        var releases = new List<NewznabRelease>();
        var totalResults = 0;
        var offset = 0;

        try
        {
            var doc = XDocument.Parse(content);
            var channel = doc.Root?.Element("channel");

            if (channel == null)
            {
                return (releases, totalResults, offset);
            }

            // Parse response metadata
            var responseElement = channel.Element(NewznabNs + "response");
            if (responseElement != null)
            {
                int.TryParse(responseElement.Attribute("total")?.Value, out totalResults);
                int.TryParse(responseElement.Attribute("offset")?.Value, out offset);
            }

            // Parse items
            foreach (var item in channel.Elements("item"))
            {
                var release = ParseItem(item, indexer);
                if (release != null)
                {
                    releases.Add(release);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing Newznab search response");
        }

        return (releases, totalResults, offset);
    }

    private NewznabRelease? ParseItem(XElement item, NewznabIndexer indexer)
    {
        try
        {
            var title = item.Element("title")?.Value;
            var guid = item.Element("guid")?.Value;
            var link = item.Element("link")?.Value;

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var release = new NewznabRelease
            {
                Title = title,
                Guid = guid,
                NzbUrl = link ?? BuildNzbDownloadUrl(indexer, guid),
                IndexerName = indexer.Name,
                IndexerId = indexer.Id,
                InfoUrl = item.Element("comments")?.Value,
                IsFromHydra = indexer.IsHydra
            };

            // Parse enclosure for size
            var enclosure = item.Element("enclosure");
            if (enclosure != null && long.TryParse(enclosure.Attribute("length")?.Value, out var size))
            {
                release = release with { Size = size };
            }

            // Parse pubDate
            var pubDateStr = item.Element("pubDate")?.Value;
            if (!string.IsNullOrEmpty(pubDateStr) && DateTime.TryParse(pubDateStr, out var pubDate))
            {
                release = release with { PublishedDate = pubDate.ToUniversalTime() };
            }

            // Parse categories
            var categories = new List<int>();
            var categoryNames = new List<string>();
            foreach (var cat in item.Elements("category"))
            {
                var catValue = cat.Value;
                categoryNames.Add(catValue);

                // Try to extract numeric ID if it's in format "1000 > Subcategory"
                var parts = catValue.Split('>');
                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out var catId))
                {
                    categories.Add(catId);
                }
            }

            release = release with
            {
                Categories = categories,
                CategoryNames = categoryNames
            };

            // Parse Newznab attributes
            var attributes = new Dictionary<string, string>();
            foreach (var attr in item.Elements(NewznabNs + "attr"))
            {
                var name = attr.Attribute("name")?.Value;
                var value = attr.Attribute("value")?.Value;

                if (!string.IsNullOrEmpty(name) && value != null)
                {
                    attributes[name] = value;

                    // Extract known attributes
                    switch (name.ToLowerInvariant())
                    {
                        case "size" when long.TryParse(value, out var attrSize):
                            release = release with { Size = attrSize };
                            break;
                        case "grabs" when int.TryParse(value, out var grabs):
                            release = release with { Grabs = grabs };
                            break;
                        case "files" when int.TryParse(value, out var files):
                            release = release with { Files = files };
                            break;
                        case "poster":
                            release = release with { Poster = value };
                            break;
                        case "group":
                            release = release with { Group = value };
                            break;
                        case "password" when int.TryParse(value, out var pwd):
                            release = release with { PasswordStatus = pwd };
                            break;
                        case "category" when int.TryParse(value, out var catId) && !categories.Contains(catId):
                            categories.Add(catId);
                            release = release with { Categories = categories };
                            break;
                    }
                }
            }

            release = release with { Attributes = attributes };

            // Parse NZBHydra2-specific attributes if this is from a Hydra indexer
            if (indexer.IsHydra)
            {
                release = ParseHydraAttributes(item, release, attributes);
            }

            return release;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing Newznab item");
            return null;
        }
    }

    /// <summary>
    /// Parses NZBHydra2-specific attributes from a search result item.
    /// NZBHydra2 adds metadata about the backend indexer that provided each result.
    /// </summary>
    private NewznabRelease ParseHydraAttributes(XElement item, NewznabRelease release, Dictionary<string, string> attributes)
    {
        // NZBHydra2 can expose backend indexer info in multiple ways:
        // 1. Via newznab:attr with hydra-prefixed names
        // 2. Via dedicated hydra namespace attributes
        // 3. Via standard attributes with indexer info
        
        string? hydraIndexerName = null;
        string? hydraIndexerId = null;
        string? hydraOriginalGuid = null;
        string? hydraIndexerHost = null;
        int? hydraScore = null;

        // Check for hydra attributes in the newznab namespace (common pattern)
        if (attributes.TryGetValue("hydraIndexerName", out var indexerName))
        {
            hydraIndexerName = indexerName;
        }
        if (attributes.TryGetValue("hydraIndexerId", out var indexerId))
        {
            hydraIndexerId = indexerId;
        }
        if (attributes.TryGetValue("hydraIndexerGuid", out var originalGuid))
        {
            hydraOriginalGuid = originalGuid;
        }
        if (attributes.TryGetValue("hydraIndexerHost", out var indexerHost))
        {
            hydraIndexerHost = indexerHost;
        }
        if (attributes.TryGetValue("hydraIndexerScore", out var scoreStr) && int.TryParse(scoreStr, out var score))
        {
            hydraScore = score;
        }

        // Also check for snake_case variants (some versions use this)
        if (hydraIndexerName == null && attributes.TryGetValue("indexer", out var indexerAlt))
        {
            hydraIndexerName = indexerAlt;
        }
        if (hydraScore == null && attributes.TryGetValue("indexerscore", out var scoreAlt) && int.TryParse(scoreAlt, out var scoreAltInt))
        {
            hydraScore = scoreAltInt;
        }

        // Parse Hydra namespace attributes if present
        foreach (var attr in item.Elements(HydraNs + "attr"))
        {
            var name = attr.Attribute("name")?.Value;
            var value = attr.Attribute("value")?.Value;

            if (string.IsNullOrEmpty(name) || value == null)
                continue;

            switch (name.ToLowerInvariant())
            {
                case "indexername":
                    hydraIndexerName ??= value;
                    break;
                case "indexerid":
                    hydraIndexerId ??= value;
                    break;
                case "indexerguid" or "originalguid":
                    hydraOriginalGuid ??= value;
                    break;
                case "indexerhost":
                    hydraIndexerHost ??= value;
                    break;
                case "indexerscore" or "score" when int.TryParse(value, out var s):
                    hydraScore ??= s;
                    break;
            }
        }

        return release with
        {
            HydraIndexerName = hydraIndexerName,
            HydraIndexerId = hydraIndexerId,
            HydraOriginalGuid = hydraOriginalGuid,
            HydraIndexerHost = hydraIndexerHost,
            HydraScore = hydraScore
        };
    }

    private NewznabCapabilities ParseCapabilitiesResponse(string content)
    {
        try
        {
            var doc = XDocument.Parse(content);
            var root = doc.Root;

            if (root?.Name.LocalName != "caps")
            {
                return new NewznabCapabilities
                {
                    Success = false,
                    ErrorMessage = "Invalid capabilities response"
                };
            }

            // Parse server info
            var serverElement = root.Element("server");
            var serverInfo = serverElement != null ? new NewznabServerInfo
            {
                Version = serverElement.Attribute("version")?.Value,
                Title = serverElement.Attribute("title")?.Value,
                Strapline = serverElement.Attribute("strapline")?.Value,
                Email = serverElement.Attribute("email")?.Value,
                Url = serverElement.Attribute("url")?.Value
            } : null;

            // Parse search capabilities
            var searchingElement = root.Element("searching");
            var searching = new NewznabSearchCapabilities
            {
                SearchAvailable = IsSearchAvailable(searchingElement, "search"),
                TvSearchAvailable = IsSearchAvailable(searchingElement, "tv-search"),
                MovieSearchAvailable = IsSearchAvailable(searchingElement, "movie-search"),
                MusicSearchAvailable = IsSearchAvailable(searchingElement, "music-search"),
                BookSearchAvailable = IsSearchAvailable(searchingElement, "book-search"),
                AudioSearchAvailable = IsSearchAvailable(searchingElement, "audio-search")
            };

            // Parse limits
            var limitsElement = root.Element("limits");
            var limits = new NewznabLimits
            {
                Max = int.TryParse(limitsElement?.Attribute("max")?.Value, out var max) ? max : 100,
                Default = int.TryParse(limitsElement?.Attribute("default")?.Value, out var def) ? def : 100
            };

            // Parse categories
            var categories = new List<NewznabCategory>();
            var categoriesElement = root.Element("categories");
            if (categoriesElement != null)
            {
                foreach (var cat in categoriesElement.Elements("category"))
                {
                    var category = ParseCategory(cat);
                    if (category != null)
                    {
                        categories.Add(category);
                    }
                }
            }

            return new NewznabCapabilities
            {
                Success = true,
                Server = serverInfo,
                Searching = searching,
                Limits = limits,
                Categories = categories
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing capabilities response");
            return new NewznabCapabilities
            {
                Success = false,
                ErrorMessage = $"Parse error: {ex.Message}"
            };
        }
    }

    private static bool IsSearchAvailable(XElement? searchingElement, string searchType)
    {
        var element = searchingElement?.Element(searchType);
        return element?.Attribute("available")?.Value?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static NewznabCategory? ParseCategory(XElement element)
    {
        if (!int.TryParse(element.Attribute("id")?.Value, out var id))
        {
            return null;
        }

        var name = element.Attribute("name")?.Value ?? "Unknown";
        var subCategories = new List<NewznabCategory>();

        foreach (var subCat in element.Elements("subcat"))
        {
            var sub = ParseCategory(subCat);
            if (sub != null)
            {
                subCategories.Add(sub);
            }
        }

        return new NewznabCategory
        {
            Id = id,
            Name = name,
            SubCategories = subCategories
        };
    }

    private static string MaskApiKey(string url, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 4)
        {
            return url;
        }

        return url.Replace(apiKey, apiKey[..2] + "***" + apiKey[^2..]);
    }
}
