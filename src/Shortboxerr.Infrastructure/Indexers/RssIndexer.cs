using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Indexers;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Indexers;

/// <summary>
/// RSS/Atom feed indexer implementation.
/// Polls RSS feeds for comic releases and converts them to candidates.
/// </summary>
public class RssIndexer : IRssIndexer
{
    private readonly HttpClient _httpClient;
    private readonly IFilenameParser _filenameParser;
    private readonly ILogger<RssIndexer> _logger;
    private readonly RssIndexerSettings _settings;
    private DateTime? _lastPolledAt;

    public RssIndexer(
        HttpClient httpClient,
        IFilenameParser filenameParser,
        ILogger<RssIndexer> logger,
        RssIndexerSettings settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _filenameParser = filenameParser ?? throw new ArgumentNullException(nameof(filenameParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        
        ConfigureHttpClient();
    }

    #region IProvider Implementation

    public int Id => _settings.Id;
    public string Name => _settings.Name;
    public ProviderType Type => ProviderType.Rss;
    public bool IsEnabled { get => _settings.Enabled; set => _settings.Enabled = value; }
    public int Priority { get => _settings.Priority; set => _settings.Priority = value; }

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await FetchFeedAsync(cancellationToken);
            stopwatch.Stop();
            
            if (result.Success)
            {
                return ProviderTestResult.Ok(
                    $"Successfully fetched {result.Items.Count} items from feed",
                    result.Items.Count,
                    stopwatch.ElapsedMilliseconds);
            }
            
            return ProviderTestResult.Fail(result.Error ?? "Unknown error", result.Error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "RSS indexer test failed for {Name}", Name);
            return ProviderTestResult.Fail(ex.Message, ex.Message);
        }
    }

    public Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var isHealthy = IsEnabled && !string.IsNullOrEmpty(_settings.FeedUrl);
        
        return Task.FromResult(new ProviderHealth
        {
            Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Disabled,
            Message = isHealthy ? "RSS indexer is configured" : "RSS indexer is disabled or misconfigured",
            CheckedAt = DateTime.UtcNow,
            LastSuccessAt = _lastPolledAt
        });
    }

    #endregion

    #region IIndexerProvider Implementation

    public bool SupportsRss => true;
    public bool SupportsSearch => false; // RSS feeds don't support search queries

    public async Task<IndexerSearchResult> SearchAsync(IndexerSearchQuery query, CancellationToken cancellationToken = default)
    {
        // RSS feeds don't support search - fetch all and filter locally
        var latest = await GetLatestAsync(query.Limit, cancellationToken);
        
        if (!latest.Success)
            return latest;
        
        // Filter results based on query
        var filtered = latest.Candidates.AsEnumerable();
        
        if (!string.IsNullOrEmpty(query.SeriesTitle))
        {
            filtered = filtered.Where(c => 
                c.SeriesTitle?.Contains(query.SeriesTitle, StringComparison.OrdinalIgnoreCase) == true);
        }
        
        if (query.IssueNumber.HasValue)
        {
            filtered = filtered.Where(c => c.IssueNumber == query.IssueNumber);
        }
        
        if (query.Year.HasValue)
        {
            filtered = filtered.Where(c => c.Year == query.Year);
        }
        
        if (!string.IsNullOrEmpty(query.Query))
        {
            filtered = filtered.Where(c => 
                c.ReleaseTitle.Contains(query.Query, StringComparison.OrdinalIgnoreCase));
        }
        
        var results = filtered.Take(query.Limit).ToList();
        
        return new IndexerSearchResult
        {
            Success = true,
            Candidates = results,
            TotalResults = results.Count,
            Query = query,
            Duration = latest.Duration
        };
    }

    public async Task<IndexerSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var feedResult = await FetchFeedAsync(cancellationToken);
            
            if (!feedResult.Success)
            {
                return IndexerSearchResult.Fail(feedResult.Error ?? "Failed to fetch feed");
            }
            
            var candidates = feedResult.Items
                .Take(limit)
                .Select(ConvertToCandidate)
                .Where(c => c != null)
                .Cast<Candidate>()
                .ToList();
            
            stopwatch.Stop();
            
            return IndexerSearchResult.Ok(candidates, feedResult.Items.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to get latest from RSS feed {Name}", Name);
            return IndexerSearchResult.Fail(ex.Message);
        }
    }

    #endregion

    #region IRssIndexer Implementation

    public string FeedUrl => _settings.FeedUrl;
    public TimeSpan PollInterval => TimeSpan.FromMinutes(_settings.PollIntervalMinutes);
    public DateTime? LastPolledAt => _lastPolledAt;

    public async Task<RssFeedResult> FetchFeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching RSS feed from {Url}", _settings.FeedUrl);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, _settings.FeedUrl);
            
            // Add authentication if configured
            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RSS feed {Url} returned status {Status}", 
                    _settings.FeedUrl, response.StatusCode);
                return RssFeedResult.Fail(
                    $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    (int)response.StatusCode);
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = ParseFeed(content);
            
            _lastPolledAt = DateTime.UtcNow;
            
            _logger.LogInformation("Fetched {Count} items from RSS feed {Name}", items.Count, Name);
            
            return new RssFeedResult
            {
                Success = true,
                Items = items,
                FetchedAt = DateTime.UtcNow,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching RSS feed {Url}", _settings.FeedUrl);
            return RssFeedResult.Fail($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching RSS feed {Url}", _settings.FeedUrl);
            return RssFeedResult.Fail(ex.Message);
        }
    }

    #endregion

    #region Private Methods

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        
        if (!string.IsNullOrEmpty(_settings.UserAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shortboxerr/1.0 (RSS Indexer)");
        }
        
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/xml"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/xml"));
    }

    private List<RssFeedItem> ParseFeed(string content)
    {
        var items = new List<RssFeedItem>();
        
        try
        {
            using var reader = XmlReader.Create(new StringReader(content));
            var feed = SyndicationFeed.Load(reader);
            
            foreach (var syndicationItem in feed.Items.Take(_settings.MaxItemsPerPoll))
            {
                var item = ConvertSyndicationItem(syndicationItem);
                if (item != null)
                {
                    // Apply category filter if configured
                    if (_settings.FilterCategories.Count == 0 ||
                        item.Categories.Intersect(_settings.FilterCategories, StringComparer.OrdinalIgnoreCase).Any())
                    {
                        items.Add(item);
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Failed to parse RSS/Atom feed XML");
            throw new InvalidOperationException($"Invalid RSS/Atom feed: {ex.Message}", ex);
        }
        
        return items;
    }

    private RssFeedItem? ConvertSyndicationItem(SyndicationItem syndicationItem)
    {
        try
        {
            var id = syndicationItem.Id ?? syndicationItem.Links.FirstOrDefault()?.Uri?.ToString() ?? Guid.NewGuid().ToString();
            var title = syndicationItem.Title?.Text ?? "";
            
            if (string.IsNullOrWhiteSpace(title))
                return null;
            
            var link = syndicationItem.Links.FirstOrDefault(l => l.RelationshipType == "alternate")?.Uri?.ToString()
                ?? syndicationItem.Links.FirstOrDefault()?.Uri?.ToString();
            
            // Find download link (enclosure or specific link type)
            var downloadLink = syndicationItem.Links.FirstOrDefault(l => 
                    l.RelationshipType == "enclosure" || 
                    l.MediaType?.Contains("application/") == true)?.Uri?.ToString();
            
            // Try to extract from link pattern if configured
            if (string.IsNullOrEmpty(downloadLink) && !string.IsNullOrEmpty(_settings.DownloadLinkPattern) && !string.IsNullOrEmpty(link))
            {
                var match = Regex.Match(link, _settings.DownloadLinkPattern);
                if (match.Success)
                {
                    downloadLink = match.Value;
                }
            }
            
            // Get size from enclosure
            long? size = null;
            var enclosure = syndicationItem.Links.FirstOrDefault(l => l.RelationshipType == "enclosure");
            if (enclosure?.Length > 0)
            {
                size = enclosure.Length;
            }
            
            return new RssFeedItem
            {
                Id = id,
                Title = title,
                Description = syndicationItem.Summary?.Text,
                Link = link,
                DownloadLink = downloadLink ?? link,
                Size = size,
                PublishedAt = syndicationItem.PublishDate.UtcDateTime,
                Categories = syndicationItem.Categories.Select(c => c.Name).ToList(),
                Author = syndicationItem.Authors.FirstOrDefault()?.Name,
                EnclosureUrl = enclosure?.Uri?.ToString(),
                EnclosureType = enclosure?.MediaType
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert syndication item");
            return null;
        }
    }

    private Candidate? ConvertToCandidate(RssFeedItem feedItem)
    {
        try
        {
            var (parsedInfo, confidence, isCollection) = _filenameParser.Parse(feedItem.Title);
            
            return new Candidate
            {
                Id = feedItem.Id,
                ReleaseTitle = feedItem.Title,
                Source = Name,
                SourcePriority = Priority,
                SeriesTitle = parsedInfo.SeriesTitle,
                IssueNumber = parsedInfo.IssueNumber,
                VolumeNumber = parsedInfo.VolumeNumber,
                Year = parsedInfo.Year,
                Format = Path.GetExtension(feedItem.Title)?.TrimStart('.'),
                Size = feedItem.Size,
                IsCollection = isCollection,
                EditionType = parsedInfo.EditionIndicator,
                DownloadUrl = feedItem.DownloadLink,
                DiscoveredAt = feedItem.PublishedAt ?? DateTime.UtcNow,
                Tags = parsedInfo.Tags
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert feed item to candidate: {Title}", feedItem.Title);
            return null;
        }
    }

    #endregion
}
