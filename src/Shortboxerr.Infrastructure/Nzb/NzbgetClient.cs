using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// NZBGet client implementation using JSON-RPC 2.0 API.
/// Reference: https://nzbget.net/api/
/// </summary>
public class NzbgetClient : INzbgetClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NzbgetClient>? _logger;
    private NzbgetSettings _settings;
    private int _requestId = 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public NzbgetClient(HttpClient httpClient, NzbgetSettings settings, ILogger<NzbgetClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    public NzbDownloadClientType ClientType => NzbDownloadClientType.NZBGet;

    /// <summary>
    /// Updates the client settings.
    /// </summary>
    public void Configure(NzbgetSettings settings)
    {
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    public async Task<NzbClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var version = await GetVersionAsync(cancellationToken);
            stopwatch.Stop();

            if (version != null)
            {
                _logger?.LogInformation("NZBGet connection successful. Version: {Version}", version);
                return NzbClientTestResult.Ok($"Connected to NZBGet {version}", version, stopwatch.ElapsedMilliseconds);
            }

            return NzbClientTestResult.Failed("Failed to retrieve NZBGet version");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "NZBGet connection failed");
            return NzbClientTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "NZBGet returned invalid JSON");
            return NzbClientTestResult.Failed($"Invalid response: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "NZBGet connection error");
            return NzbClientTestResult.Failed($"Error: {ex.Message}");
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var response = await CallAsync<string>("version", cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // NZBGet doesn't have a dedicated categories endpoint.
        // Categories are defined in config. We can get them via config() call.
        var config = await CallAsync<List<NzbgetConfigItem>>("config", cancellationToken);
        
        if (config == null) return Array.Empty<string>();

        // Categories are named Category1.Name, Category2.Name, etc.
        var categories = new List<string>();
        foreach (var item in config)
        {
            if (item.Name.StartsWith("Category") && item.Name.EndsWith(".Name") && !string.IsNullOrEmpty(item.Value))
            {
                categories.Add(item.Value);
            }
        }
        
        return categories;
    }

    public async Task<NzbgetStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return await CallAsync<NzbgetStatus>("status", cancellationToken);
    }

    public async Task<NzbAddResult> AddNzbAsync(byte[] nzbContent, string filename, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // NZBGet append method: append(NZBFilename, Content, Category, Priority, DupeKey, DupeScore, DupeMode, AddTop, AddPaused, PPParameters)
            var base64Content = Convert.ToBase64String(nzbContent);
            var category = options?.Category ?? _settings.Category;
            var priority = MapPriority(options?.Priority ?? NzbPriority.Normal);
            var addPaused = _settings.AddPaused;

            var result = await CallAsync<int>("append", 
                filename,                    // NZBFilename
                base64Content,               // Content (base64 encoded)
                category,                    // Category
                priority,                    // Priority
                false,                       // AddToTop
                addPaused,                   // AddPaused
                "",                          // DupeKey
                0,                           // DupeScore
                "SCORE",                     // DupeMode
                Array.Empty<object>(),       // Parameters
                cancellationToken);

            if (result > 0)
            {
                _logger?.LogInformation("NZB added to NZBGet queue: {Filename} (ID: {Id})", filename, result);
                return NzbAddResult.Ok(result.ToString());
            }

            return NzbAddResult.Failed("NZBGet returned invalid ID");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add NZB to NZBGet: {Filename}", filename);
            return NzbAddResult.Failed(ex.Message);
        }
    }

    public async Task<NzbAddResult> AddNzbUrlAsync(string nzbUrl, NzbDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Download the NZB first, then add via append
            var nzbContent = await _httpClient.GetByteArrayAsync(nzbUrl, cancellationToken);
            var filename = GetFilenameFromUrl(nzbUrl);
            return await AddNzbAsync(nzbContent, filename, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add NZB URL to NZBGet: {Url}", nzbUrl);
            return NzbAddResult.Failed(ex.Message);
        }
    }

    public async Task<NzbDownloadStatus?> GetDownloadStatusAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(downloadId, out var nzbId))
        {
            return null;
        }

        // Check queue first
        var queue = await GetQueueAsync(cancellationToken);
        var inQueue = queue.FirstOrDefault(d => d.Id == downloadId);
        if (inQueue != null)
        {
            return inQueue;
        }

        // Check history
        var history = await GetHistoryAsync(100, cancellationToken);
        return history.FirstOrDefault(d => d.Id == downloadId);
    }

    public async Task<IReadOnlyList<NzbDownloadStatus>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var groups = await CallAsync<List<NzbgetGroup>>("listgroups", cancellationToken);
        
        if (groups == null) return Array.Empty<NzbDownloadStatus>();

        return groups.Select(g => new NzbDownloadStatus
        {
            Id = g.NZBID.ToString(),
            Name = g.NZBName,
            State = MapGroupStatus(g.Status),
            Category = g.Category,
            TotalBytes = g.TotalSize,
            DownloadedBytes = g.DownloadedSize,
            Priority = MapNzbgetPriority(g.Priority),
            DownloadPath = g.DestDir
        }).ToList();
    }

    public async Task<IReadOnlyList<NzbDownloadStatus>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var history = await CallAsync<List<NzbgetHistoryItem>>("history", false, cancellationToken);
        
        if (history == null) return Array.Empty<NzbDownloadStatus>();

        return history.Take(limit).Select(h => new NzbDownloadStatus
        {
            Id = h.NZBID.ToString(),
            Name = h.Name,
            State = MapHistoryStatus(h.Status),
            Category = h.Category,
            TotalBytes = h.TotalSize,
            DownloadedBytes = h.TotalSize, // Completed downloads have full size
            CompletedAt = h.CompletedAt,
            DownloadPath = string.IsNullOrEmpty(h.FinalDir) ? h.DestDir : h.FinalDir,
            ErrorMessage = h.IsSuccess ? null : h.StatusText
        }).ToList();
    }

    public async Task<bool> RemoveDownloadAsync(string downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(downloadId, out var nzbId))
        {
            return false;
        }

        try
        {
            // editqueue(Command, Param, IDs) - "GroupDelete" removes from queue
            var result = await CallAsync<bool>("editqueue", 
                deleteFiles ? "GroupFinalDelete" : "GroupDelete",
                "",
                new[] { nzbId },
                cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove download {Id} from NZBGet", downloadId);
            return false;
        }
    }

    public async Task<bool> PauseDownloadAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(downloadId, out var nzbId))
        {
            return false;
        }

        try
        {
            var result = await CallAsync<bool>("editqueue", "GroupPause", "", new[] { nzbId }, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pause download {Id}", downloadId);
            return false;
        }
    }

    public async Task<bool> ResumeDownloadAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(downloadId, out var nzbId))
        {
            return false;
        }

        try
        {
            var result = await CallAsync<bool>("editqueue", "GroupResume", "", new[] { nzbId }, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume download {Id}", downloadId);
            return false;
        }
    }

    public async Task<bool> PauseQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("pausedownload", cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pause NZBGet queue");
            return false;
        }
    }

    public async Task<bool> ResumeQueueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("resumedownload", cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume NZBGet queue");
            return false;
        }
    }

    public async Task<bool> SetSpeedLimitAsync(int speedKbps, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("rate", speedKbps, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set speed limit");
            return false;
        }
    }

    public async Task<bool> ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("reload", cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reload NZBGet config");
            return false;
        }
    }

    public async Task<bool> ScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("scan", cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to trigger NZBGet scan");
            return false;
        }
    }

    public async Task<bool> WriteLogAsync(string kind, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallAsync<bool>("writelog", kind, message, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write to NZBGet log");
            return false;
        }
    }

    public async Task<NzbDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (status == null) return null;

        return new NzbDiskSpace
        {
            FreeBytes = status.FreeDiskSpaceMB * 1024 * 1024,
            TotalBytes = 0, // NZBGet doesn't provide total space
            IsLow = status.FreeDiskSpaceMB < 1024, // Less than 1GB
            Path = null
        };
    }

    /// <summary>
    /// Makes a JSON-RPC call to NZBGet.
    /// </summary>
    private Task<T?> CallAsync<T>(string method, CancellationToken cancellationToken = default)
    {
        return CallWithParamsAsync<T>(method, Array.Empty<object>(), cancellationToken);
    }

    private Task<T?> CallAsync<T>(string method, object param1, CancellationToken cancellationToken = default)
    {
        return CallWithParamsAsync<T>(method, new[] { param1 }, cancellationToken);
    }

    private Task<T?> CallAsync<T>(string method, object param1, object param2, CancellationToken cancellationToken = default)
    {
        return CallWithParamsAsync<T>(method, new[] { param1, param2 }, cancellationToken);
    }

    private Task<T?> CallAsync<T>(string method, object param1, object param2, object param3, CancellationToken cancellationToken = default)
    {
        return CallWithParamsAsync<T>(method, new[] { param1, param2, param3 }, cancellationToken);
    }

    private Task<T?> CallAsync<T>(string method, object param1, object param2, object param3, object param4, object param5, object param6, object param7, object param8, object param9, object param10, CancellationToken cancellationToken = default)
    {
        return CallWithParamsAsync<T>(method, new[] { param1, param2, param3, param4, param5, param6, param7, param8, param9, param10 }, cancellationToken);
    }

    private async Task<T?> CallWithParamsAsync<T>(string method, object[] parameters, CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(ref _requestId);
        
        var request = new JsonRpcRequest
        {
            Method = method,
            Params = parameters.Where(p => p != null).ToArray()!,
            Id = requestId
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger?.LogDebug("NZBGet RPC call: {Method} (id: {Id})", method, requestId);

        var response = await _httpClient.PostAsync(_settings.JsonRpcUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse<T>>(responseJson, JsonOptions);

        if (rpcResponse?.Error != null)
        {
            _logger?.LogWarning("NZBGet RPC error: {Code} - {Message}", rpcResponse.Error.Code, rpcResponse.Error.Message);
            throw new InvalidOperationException($"NZBGet error: {rpcResponse.Error.Message}");
        }

        return rpcResponse != null ? rpcResponse.Result : default;
    }

    private static int MapPriority(NzbPriority priority)
    {
        return priority switch
        {
            NzbPriority.Low => (int)NzbgetPriority.Low,
            NzbPriority.Normal => (int)NzbgetPriority.Normal,
            NzbPriority.High => (int)NzbgetPriority.High,
            NzbPriority.Force => (int)NzbgetPriority.Force,
            _ => (int)NzbgetPriority.Normal
        };
    }

    private static NzbPriority MapNzbgetPriority(int priority)
    {
        return priority switch
        {
            <= -100 => NzbPriority.Low,
            <= -1 => NzbPriority.Low,
            0 => NzbPriority.Normal,
            <= 100 => NzbPriority.High,
            _ => NzbPriority.Force
        };
    }

    private static NzbDownloadState MapGroupStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "QUEUED" => NzbDownloadState.Queued,
            "PAUSED" => NzbDownloadState.Paused,
            "DOWNLOADING" => NzbDownloadState.Downloading,
            "FETCHING" => NzbDownloadState.Downloading,
            "PP_QUEUED" => NzbDownloadState.PostProcessing,
            "LOADING_PARS" => NzbDownloadState.Verifying,
            "VERIFYING_SOURCES" => NzbDownloadState.Verifying,
            "REPAIRING" => NzbDownloadState.Repairing,
            "VERIFYING_REPAIRED" => NzbDownloadState.Verifying,
            "RENAMING" => NzbDownloadState.PostProcessing,
            "UNPACKING" => NzbDownloadState.Extracting,
            "MOVING" => NzbDownloadState.PostProcessing,
            "EXECUTING_SCRIPT" => NzbDownloadState.PostProcessing,
            _ => NzbDownloadState.Queued
        };
    }

    private static NzbDownloadState MapHistoryStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "SUCCESS" => NzbDownloadState.Completed,
            "FAILURE" => NzbDownloadState.Failed,
            "DELETED" => NzbDownloadState.Deleted,
            "DUPE" => NzbDownloadState.Deleted,
            "BAD" => NzbDownloadState.Failed,
            "GOOD" => NzbDownloadState.Completed,
            "COPY" => NzbDownloadState.Completed,
            "SCAN" => NzbDownloadState.Completed,
            "MARK/GOOD" => NzbDownloadState.Completed,
            "MARK/BAD" => NzbDownloadState.Failed,
            "MARK/SUCCESS" => NzbDownloadState.Completed,
            "MARK/FAILURE" => NzbDownloadState.Failed,
            _ => NzbDownloadState.Completed
        };
    }

    private static string GetFilenameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrEmpty(filename))
            {
                return filename;
            }
        }
        catch
        {
            // Ignore URI parsing errors
        }
        
        return "download.nzb";
    }

    // JSON-RPC request/response models
    private class JsonRpcRequest
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object[] Params { get; set; } = Array.Empty<object>();

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
    }

    private class JsonRpcResponse<T>
    {
        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private class NzbgetConfigItem
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Value")]
        public string Value { get; set; } = string.Empty;
    }
}
