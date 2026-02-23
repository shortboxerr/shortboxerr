using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Client for interacting with SABnzbd download client.
/// </summary>
public class SabnzbdClient : ISabnzbdClient
{
    private readonly HttpClient _httpClient;
    private readonly SabnzbdSettings _settings;
    private readonly ILogger<SabnzbdClient>? _logger;
    
    private bool _connectionFailureLogged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public NzbDownloadClientType ClientType => NzbDownloadClientType.SABnzbd;
    
    /// <summary>
    /// Indicates whether the client has minimum required configuration.
    /// </summary>
    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>
    /// Primary constructor for dependency injection.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public SabnzbdClient(HttpClient httpClient, IOptions<SabnzbdSettings> settings, ILogger<SabnzbdClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        ConfigureHttpClient();
    }

    /// <summary>
    /// Constructor for testing and direct instantiation with explicit settings.
    /// </summary>
    public SabnzbdClient(HttpClient httpClient, SabnzbdSettings settings, ILogger<SabnzbdClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    #region INzbDownloadClient Implementation

    public async Task<NzbClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger?.LogDebug("Testing SABnzbd connection at {Host}", _settings.Host);

            var response = await CallApiAsync<SabnzbdVersionResponse>("version", cancellationToken: cancellationToken);
            stopwatch.Stop();

            if (response?.Version != null)
            {
                _logger?.LogInformation("SABnzbd connection successful. Version: {Version}", response.Version);
                return NzbClientTestResult.Ok(
                    $"Connected to SABnzbd {response.Version}",
                    response.Version,
                    stopwatch.ElapsedMilliseconds
                );
            }

            return NzbClientTestResult.Failed("Invalid response from SABnzbd");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "SABnzbd connection test failed");
            return NzbClientTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error testing SABnzbd connection");
            return NzbClientTestResult.Failed($"Error: {ex.Message}");
        }
    }

    public async Task<NzbAddResult> AddNzbAsync(byte[] nzbContent, string filename, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            options ??= new NzbDownloadOptions();
            _logger?.LogDebug("Adding NZB to SABnzbd: {Filename}", filename);

            using var content = new MultipartFormDataContent();
            var nzbFile = new ByteArrayContent(nzbContent);
            nzbFile.Headers.ContentType = new MediaTypeHeaderValue("application/x-nzb");
            content.Add(nzbFile, "nzbfile", filename);

            var parameters = new Dictionary<string, string>
            {
                ["mode"] = "addfile",
                ["apikey"] = _settings.ApiKey,
                ["output"] = "json"
            };

            if (!string.IsNullOrEmpty(options.Category ?? _settings.Category))
            {
                parameters["cat"] = options.Category ?? _settings.Category;
            }

            if (options.Priority != NzbPriority.Normal)
            {
                parameters["priority"] = ((int)options.Priority).ToString();
            }

            if (!string.IsNullOrEmpty(options.Name))
            {
                parameters["nzbname"] = options.Name;
            }

            if (!string.IsNullOrEmpty(options.PostProcessingScript ?? _settings.PostProcessingScript))
            {
                parameters["script"] = options.PostProcessingScript ?? _settings.PostProcessingScript;
            }

            foreach (var param in parameters)
            {
                content.Add(new StringContent(param.Value), param.Key);
            }

            var url = BuildApiUrl();
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SabnzbdAddResponse>(json, JsonOptions);

            if (result?.Status == true && result.NzoIds?.Count > 0)
            {
                var downloadId = result.NzoIds[0];
                _logger?.LogInformation("NZB added to SABnzbd successfully. ID: {DownloadId}", downloadId);
                return NzbAddResult.Ok(downloadId);
            }

            var error = result?.Error ?? "Unknown error";
            _logger?.LogWarning("Failed to add NZB to SABnzbd: {Error}", error);
            return NzbAddResult.Failed(error);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding NZB to SABnzbd");
            return NzbAddResult.Failed(ex.Message);
        }
    }

    public async Task<NzbAddResult> AddNzbUrlAsync(string nzbUrl, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            options ??= new NzbDownloadOptions();
            _logger?.LogDebug("Adding NZB URL to SABnzbd: {Url}", nzbUrl);

            var parameters = new Dictionary<string, string>
            {
                ["name"] = nzbUrl
            };

            if (!string.IsNullOrEmpty(options.Category ?? _settings.Category))
            {
                parameters["cat"] = options.Category ?? _settings.Category;
            }

            if (options.Priority != NzbPriority.Normal)
            {
                parameters["priority"] = ((int)options.Priority).ToString();
            }

            if (!string.IsNullOrEmpty(options.Name))
            {
                parameters["nzbname"] = options.Name;
            }

            if (!string.IsNullOrEmpty(options.PostProcessingScript ?? _settings.PostProcessingScript))
            {
                parameters["script"] = options.PostProcessingScript ?? _settings.PostProcessingScript;
            }

            var response = await CallApiAsync<SabnzbdAddResponse>("addurl", parameters, cancellationToken);

            if (response?.Status == true && response.NzoIds?.Count > 0)
            {
                var downloadId = response.NzoIds[0];
                _logger?.LogInformation("NZB URL added to SABnzbd successfully. ID: {DownloadId}", downloadId);
                return NzbAddResult.Ok(downloadId);
            }

            var error = response?.Error ?? "Unknown error";
            _logger?.LogWarning("Failed to add NZB URL to SABnzbd: {Error}", error);
            return NzbAddResult.Failed(error);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding NZB URL to SABnzbd");
            return NzbAddResult.Failed(ex.Message);
        }
    }

    public async Task<NzbDownloadStatus?> GetDownloadStatusAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check queue first
            var queue = await GetQueueAsync(cancellationToken);
            var queueItem = queue.FirstOrDefault(d => d.Id == downloadId);
            if (queueItem != null)
            {
                return queueItem;
            }

            // Check history
            var history = await GetHistoryAsync(100, cancellationToken);
            return history.FirstOrDefault(d => d.Id == downloadId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting download status from SABnzbd");
            return null;
        }
    }

    public async Task<IReadOnlyList<NzbDownloadStatus>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<NzbDownloadStatus>();
        }
        
        try
        {
            var response = await CallApiAsync<SabnzbdQueueResponse>("queue", cancellationToken: cancellationToken);
            _connectionFailureLogged = false;

            if (response?.Queue?.Slots == null)
            {
                return Array.Empty<NzbDownloadStatus>();
            }

            return response.Queue.Slots.Select(MapQueueSlotToStatus).ToList();
        }
        catch (HttpRequestException ex)
        {
            LogConnectionFailure("queue", ex);
            return Array.Empty<NzbDownloadStatus>();
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            LogConnectionFailure("queue", ex);
            return Array.Empty<NzbDownloadStatus>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error getting queue from SABnzbd at {Url}", _settings.BaseUrl);
            return Array.Empty<NzbDownloadStatus>();
        }
    }

    public async Task<IReadOnlyList<NzbDownloadStatus>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<NzbDownloadStatus>();
        }
        
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["limit"] = limit.ToString()
            };

            var response = await CallApiAsync<SabnzbdHistoryResponse>("history", parameters, cancellationToken);
            _connectionFailureLogged = false;

            if (response?.History?.Slots == null)
            {
                return Array.Empty<NzbDownloadStatus>();
            }

            return response.History.Slots.Select(MapHistorySlotToStatus).ToList();
        }
        catch (HttpRequestException ex)
        {
            LogConnectionFailure("history", ex);
            return Array.Empty<NzbDownloadStatus>();
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            LogConnectionFailure("history", ex);
            return Array.Empty<NzbDownloadStatus>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error getting history from SABnzbd at {Url}", _settings.BaseUrl);
            return Array.Empty<NzbDownloadStatus>();
        }
    }

    public async Task<bool> RemoveDownloadAsync(string downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["value"] = downloadId,
                ["del_files"] = deleteFiles ? "1" : "0"
            };

            // Try queue first
            var queueResponse = await CallApiAsync<SabnzbdStatusResponse>("queue", parameters.Concat(new[] { new KeyValuePair<string, string>("name", "delete") }).ToDictionary(x => x.Key, x => x.Value), cancellationToken);
            if (queueResponse?.Status == true)
            {
                return true;
            }

            // Try history
            var historyResponse = await CallApiAsync<SabnzbdStatusResponse>("history", parameters.Concat(new[] { new KeyValuePair<string, string>("name", "delete") }).ToDictionary(x => x.Key, x => x.Value), cancellationToken);
            return historyResponse?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing download from SABnzbd");
            return false;
        }
    }

    public async Task<bool> PauseDownloadAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["name"] = "pause",
                ["value"] = downloadId
            };

            var response = await CallApiAsync<SabnzbdStatusResponse>("queue", parameters, cancellationToken);
            return response?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error pausing download in SABnzbd");
            return false;
        }
    }

    public async Task<bool> ResumeDownloadAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["name"] = "resume",
                ["value"] = downloadId
            };

            var response = await CallApiAsync<SabnzbdStatusResponse>("queue", parameters, cancellationToken);
            return response?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resuming download in SABnzbd");
            return false;
        }
    }

    public async Task<NzbDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdQueueResponse>("queue", cancellationToken: cancellationToken);

            if (response?.Queue == null)
            {
                return null;
            }

            var freeBytes = ParseSizeToBytes(response.Queue.DiskSpaceLeft1 ?? response.Queue.DiskSpace1 ?? "0");
            var totalBytes = ParseSizeToBytes(response.Queue.DiskSpaceTotal1 ?? "0");

            return new NzbDiskSpace
            {
                FreeBytes = freeBytes,
                TotalBytes = totalBytes,
                IsLow = response.Queue.HaveWarnings == "true",
                Path = response.Queue.DownloadDir
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting disk space from SABnzbd");
            return null;
        }
    }

    #endregion

    #region ISabnzbdClient Implementation

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdVersionResponse>("version", cancellationToken: cancellationToken);
            return response?.Version;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdCategoriesResponse>("get_cats", cancellationToken: cancellationToken);
            return response?.Categories?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting categories from SABnzbd");
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> GetScriptsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdScriptsResponse>("get_scripts", cancellationToken: cancellationToken);
            return response?.Scripts?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting scripts from SABnzbd");
            return Array.Empty<string>();
        }
    }

    public async Task<bool> PauseQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdStatusResponse>("pause", cancellationToken: cancellationToken);
            return response?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error pausing SABnzbd queue");
            return false;
        }
    }

    public async Task<bool> ResumeQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdStatusResponse>("resume", cancellationToken: cancellationToken);
            return response?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resuming SABnzbd queue");
            return false;
        }
    }

    public async Task<bool> SetSpeedLimitAsync(int speedKbps, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["value"] = speedKbps.ToString()
            };

            var response = await CallApiAsync<SabnzbdStatusResponse>("config", parameters.Concat(new[] { new KeyValuePair<string, string>("name", "speedlimit") }).ToDictionary(x => x.Key, x => x.Value), cancellationToken);
            return response?.Status == true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting speed limit in SABnzbd");
            return false;
        }
    }

    public async Task<SabnzbdServerStats?> GetServerStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await CallApiAsync<SabnzbdQueueResponse>("queue", cancellationToken: cancellationToken);

            if (response?.Queue == null)
            {
                return null;
            }

            return new SabnzbdServerStats
            {
                SpeedBytesPerSecond = ParseSpeedToBytes(response.Queue.Speed ?? response.Queue.Kbpersec ?? "0"),
                QueueCount = response.Queue.NoOfSlots,
                QueueSizeBytes = ParseSizeToBytes(response.Queue.Sizeleft ?? response.Queue.MbLeft ?? "0"),
                TimeRemaining = ParseTimeRemaining(response.Queue.TimeLeft),
                IsPaused = response.Queue.Paused == true,
                SpeedLimitKbps = int.TryParse(response.Queue.Speedlimit, out var limit) ? limit : 0
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting server stats from SABnzbd");
            return null;
        }
    }

    #endregion

    #region Helper Methods
    
    private void LogConnectionFailure(string operation, Exception ex)
    {
        if (!_connectionFailureLogged)
        {
            _logger?.LogWarning("SABnzbd unreachable at {Url} during {Operation}: {Message}", 
                _settings.BaseUrl, operation, ex.Message);
            _connectionFailureLogged = true;
        }
        else
        {
            _logger?.LogDebug("SABnzbd still unreachable at {Url} during {Operation}", 
                _settings.BaseUrl, operation);
        }
    }

    private string BuildApiUrl()
    {
        return $"{_settings.BaseUrl}/api";
    }

    private async Task<T?> CallApiAsync<T>(string mode, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) where T : class
    {
        var url = BuildApiUrl();
        var queryParams = new Dictionary<string, string>
        {
            ["mode"] = mode,
            ["apikey"] = _settings.ApiKey ?? string.Empty,
            ["output"] = "json"
        };

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                if (value != null)
                {
                    queryParams[key] = value;
                }
            }
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var fullUrl = $"{url}?{queryString}";

        _logger?.LogDebug("SABnzbd API call: {Mode}", mode);

        var response = await _httpClient.GetAsync(fullUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static NzbDownloadStatus MapQueueSlotToStatus(SabnzbdQueueSlot slot)
    {
        return new NzbDownloadStatus
        {
            Id = slot.NzoId ?? "",
            Name = slot.Filename ?? "",
            State = MapSabnzbdStatus(slot.Status),
            Category = slot.Cat,
            TotalBytes = ParseSizeToBytes(slot.Size ?? slot.Mb ?? "0"),
            DownloadedBytes = ParseSizeToBytes(slot.Size ?? slot.Mb ?? "0") - ParseSizeToBytes(slot.SizeLeft ?? slot.MbLeft ?? "0"),
            SpeedBytesPerSecond = 0, // Not available per-item
            TimeRemaining = ParseTimeRemaining(slot.TimeLeft),
            Priority = MapPriority(slot.Priority)
        };
    }

    private static NzbDownloadStatus MapHistorySlotToStatus(SabnzbdHistorySlot slot)
    {
        return new NzbDownloadStatus
        {
            Id = slot.NzoId ?? "",
            Name = slot.Name ?? "",
            State = MapSabnzbdHistoryStatus(slot.Status),
            Category = slot.Category,
            TotalBytes = (long)(slot.Bytes ?? 0),
            DownloadedBytes = (long)(slot.Bytes ?? 0),
            CompletedAt = slot.CompletedTimestamp.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(slot.CompletedTimestamp.Value).UtcDateTime
                : null,
            DownloadPath = slot.StoragePath,
            ErrorMessage = slot.FailMessage
        };
    }

    private static NzbDownloadState MapSabnzbdStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "queued" => NzbDownloadState.Queued,
            "downloading" => NzbDownloadState.Downloading,
            "paused" => NzbDownloadState.Paused,
            "verifying" => NzbDownloadState.Verifying,
            "repairing" => NzbDownloadState.Repairing,
            "extracting" => NzbDownloadState.Extracting,
            "running" or "postprocessing" => NzbDownloadState.PostProcessing,
            _ => NzbDownloadState.Queued
        };
    }

    private static NzbDownloadState MapSabnzbdHistoryStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "completed" => NzbDownloadState.Completed,
            "failed" => NzbDownloadState.Failed,
            "deleted" => NzbDownloadState.Deleted,
            _ => NzbDownloadState.Completed
        };
    }

    private static NzbPriority MapPriority(string? priority)
    {
        return priority switch
        {
            "Force" or "2" => NzbPriority.Force,
            "High" or "1" => NzbPriority.High,
            "Low" or "-1" => NzbPriority.Low,
            _ => NzbPriority.Normal
        };
    }

    private static long ParseSizeToBytes(string size)
    {
        if (string.IsNullOrEmpty(size))
            return 0;

        // Handle formats like "1.5 GB", "500 MB", "1024 KB", "1234567890" (bytes)
        size = size.Trim();

        if (double.TryParse(size, out var directValue))
        {
            // If it's a plain number, assume it's already in a standard unit (MB for SABnzbd typically)
            // SABnzbd often returns MB as plain numbers
            return (long)(directValue * 1024 * 1024);
        }

        var parts = size.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !double.TryParse(parts[0], out var value))
        {
            return 0;
        }

        var unit = parts[1].ToUpperInvariant();
        return unit switch
        {
            "KB" or "K" => (long)(value * 1024),
            "MB" or "M" => (long)(value * 1024 * 1024),
            "GB" or "G" => (long)(value * 1024 * 1024 * 1024),
            "TB" or "T" => (long)(value * 1024 * 1024 * 1024 * 1024),
            "B" => (long)value,
            _ => (long)value
        };
    }

    private static long ParseSpeedToBytes(string speed)
    {
        if (string.IsNullOrEmpty(speed))
            return 0;

        speed = speed.Trim();

        // SABnzbd often returns speed in KB/s as a number
        if (double.TryParse(speed, out var kbps))
        {
            return (long)(kbps * 1024);
        }

        // Handle "1.5 MB/s" format
        var parts = speed.Replace("/s", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !double.TryParse(parts[0], out var value))
        {
            return 0;
        }

        var unit = parts[1].ToUpperInvariant();
        return unit switch
        {
            "KB" or "K" => (long)(value * 1024),
            "MB" or "M" => (long)(value * 1024 * 1024),
            "GB" or "G" => (long)(value * 1024 * 1024 * 1024),
            _ => (long)value
        };
    }

    private static TimeSpan? ParseTimeRemaining(string? timeString)
    {
        if (string.IsNullOrEmpty(timeString))
            return null;

        // SABnzbd returns time as "HH:MM:SS" or "0:00:00"
        if (TimeSpan.TryParse(timeString, out var timeSpan))
        {
            return timeSpan;
        }

        return null;
    }

    #endregion

    #region Response Models

    private class SabnzbdVersionResponse
    {
        public string? Version { get; set; }
    }

    private class SabnzbdStatusResponse
    {
        public bool Status { get; set; }
        public string? Error { get; set; }
    }

    private class SabnzbdAddResponse
    {
        public bool Status { get; set; }
        [JsonPropertyName("nzo_ids")]
        public List<string>? NzoIds { get; set; }
        public string? Error { get; set; }
    }

    private class SabnzbdCategoriesResponse
    {
        public List<string>? Categories { get; set; }
    }

    private class SabnzbdScriptsResponse
    {
        public List<string>? Scripts { get; set; }
    }

    private class SabnzbdQueueResponse
    {
        public SabnzbdQueue? Queue { get; set; }
    }

    private class SabnzbdQueue
    {
        public bool? Paused { get; set; }
        public string? Speed { get; set; }
        public string? Kbpersec { get; set; }
        public string? Sizeleft { get; set; }
        public string? MbLeft { get; set; }
        public string? Size { get; set; }
        public string? Mb { get; set; }
        public string? TimeLeft { get; set; }
        [JsonPropertyName("noofslots")]
        public int NoOfSlots { get; set; }
        public string? Speedlimit { get; set; }
        public List<SabnzbdQueueSlot>? Slots { get; set; }

        // Disk space
        [JsonPropertyName("diskspace1")]
        public string? DiskSpace1 { get; set; }
        [JsonPropertyName("diskspacetotal1")]
        public string? DiskSpaceTotal1 { get; set; }
        [JsonPropertyName("diskspace_left1")]
        public string? DiskSpaceLeft1 { get; set; }
        [JsonPropertyName("have_warnings")]
        public string? HaveWarnings { get; set; }
        [JsonPropertyName("download_dir")]
        public string? DownloadDir { get; set; }
    }

    private class SabnzbdQueueSlot
    {
        [JsonPropertyName("nzo_id")]
        public string? NzoId { get; set; }
        public string? Filename { get; set; }
        public string? Status { get; set; }
        public string? Cat { get; set; }
        public string? Size { get; set; }
        public string? Mb { get; set; }
        [JsonPropertyName("sizeleft")]
        public string? SizeLeft { get; set; }
        [JsonPropertyName("mbleft")]
        public string? MbLeft { get; set; }
        [JsonPropertyName("timeleft")]
        public string? TimeLeft { get; set; }
        public string? Priority { get; set; }
    }

    private class SabnzbdHistoryResponse
    {
        public SabnzbdHistory? History { get; set; }
    }

    private class SabnzbdHistory
    {
        public List<SabnzbdHistorySlot>? Slots { get; set; }
    }

    private class SabnzbdHistorySlot
    {
        [JsonPropertyName("nzo_id")]
        public string? NzoId { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public double? Bytes { get; set; }
        [JsonPropertyName("completed")]
        public long? CompletedTimestamp { get; set; }
        [JsonPropertyName("storage")]
        public string? StoragePath { get; set; }
        [JsonPropertyName("fail_message")]
        public string? FailMessage { get; set; }
    }

    #endregion
}
