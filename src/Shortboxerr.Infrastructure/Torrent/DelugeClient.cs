using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Torrent;

namespace Shortboxerr.Infrastructure.Torrent;

/// <summary>
/// Deluge client implementation using the JSON-RPC Web UI API.
/// Reference: https://deluge.readthedocs.io/en/latest/devguide/how-to/curl-jsonrpc.html
/// </summary>
public class DelugeClient : IDelugeClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DelugeClient>? _logger;
    private DelugeSettings _settings;
    private bool _isAuthenticated;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private int _requestId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DelugeClient(HttpClient httpClient, DelugeSettings settings, ILogger<DelugeClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    public TorrentClientType ClientType => TorrentClientType.Deluge;

    public void Configure(DelugeSettings settings)
    {
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _isAuthenticated = false;
    }

    public async Task<TorrentClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);
            var version = await GetVersionAsync(cancellationToken);
            stopwatch.Stop();

            if (!string.IsNullOrEmpty(version))
            {
                _logger?.LogInformation("Deluge connection successful. Version: {Version}", version);
                return TorrentClientTestResult.Ok(
                    $"Connected to Deluge {version}",
                    version,
                    stopwatch.ElapsedMilliseconds);
            }

            return TorrentClientTestResult.Failed("Failed to retrieve Deluge version");
        }
        catch (DelugeAuthenticationException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning("Deluge authentication failed: {Message}", ex.Message);
            return TorrentClientTestResult.Failed($"Authentication failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "Deluge connection failed");
            return TorrentClientTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Deluge connection error");
            return TorrentClientTestResult.Failed($"Error: {ex.Message}");
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallMethodAsync<string>("daemon.info", null, cancellationToken);
        return result;
    }

    public async Task<string?> GetLibtorrentVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallMethodAsync<string>("core.get_libtorrent_version", null, cancellationToken);
        return result;
    }

    public async Task<TorrentAddResult> AddTorrentMagnetAsync(string magnetUri, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var delugeOptions = BuildAddTorrentOptions(options);
            var result = await CallMethodAsync<string>(
                "core.add_torrent_magnet",
                new object[] { magnetUri, delugeOptions },
                cancellationToken);

            if (!string.IsNullOrEmpty(result))
            {
                _logger?.LogInformation("Torrent added via magnet: {Hash}", result);
                
                // Set label if configured
                await TrySetLabelAsync(result, options?.Category, cancellationToken);
                
                return TorrentAddResult.Ok(result);
            }

            return TorrentAddResult.Failed("Failed to add torrent");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add torrent magnet");
            return TorrentAddResult.Failed(ex.Message);
        }
    }

    public async Task<TorrentAddResult> AddTorrentUrlAsync(string torrentUrl, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var delugeOptions = BuildAddTorrentOptions(options);
            var result = await CallMethodAsync<string>(
                "core.add_torrent_url",
                new object[] { torrentUrl, delugeOptions },
                cancellationToken);

            if (!string.IsNullOrEmpty(result))
            {
                _logger?.LogInformation("Torrent added via URL: {Hash}", result);
                await TrySetLabelAsync(result, options?.Category, cancellationToken);
                return TorrentAddResult.Ok(result);
            }

            return TorrentAddResult.Failed("Failed to add torrent");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add torrent URL: {Url}", torrentUrl);
            return TorrentAddResult.Failed(ex.Message);
        }
    }

    public async Task<TorrentAddResult> AddTorrentFileAsync(byte[] torrentContent, string filename, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var base64Content = Convert.ToBase64String(torrentContent);
            var delugeOptions = BuildAddTorrentOptions(options);
            
            var result = await CallMethodAsync<string>(
                "core.add_torrent_file",
                new object[] { filename, base64Content, delugeOptions },
                cancellationToken);

            if (!string.IsNullOrEmpty(result))
            {
                _logger?.LogInformation("Torrent file added: {Filename} ({Hash})", filename, result);
                await TrySetLabelAsync(result, options?.Category, cancellationToken);
                return TorrentAddResult.Ok(result);
            }

            return TorrentAddResult.Failed("Failed to add torrent file");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add torrent file: {Filename}", filename);
            return TorrentAddResult.Failed(ex.Message);
        }
    }

    public async Task<TorrentStatus?> GetStatusAsync(string hash, CancellationToken cancellationToken = default)
    {
        var torrents = await GetTorrentsStatusAsync(new[] { hash }, cancellationToken);
        return torrents.FirstOrDefault();
    }

    public async Task<IReadOnlyList<TorrentStatus>> GetAllTorrentsAsync(CancellationToken cancellationToken = default)
    {
        return await GetTorrentsStatusAsync(null, cancellationToken);
    }

    private async Task<IReadOnlyList<TorrentStatus>> GetTorrentsStatusAsync(string[]? hashes, CancellationToken cancellationToken)
    {
        var filterDict = hashes != null && hashes.Length > 0
            ? new Dictionary<string, object> { ["id"] = hashes }
            : new Dictionary<string, object>();

        var fields = new[]
        {
            "hash", "name", "state", "save_path", "total_size", "total_done",
            "total_uploaded", "download_payload_rate", "upload_payload_rate",
            "eta", "ratio", "num_seeds", "num_peers", "time_added",
            "label", "progress", "message", "tracker_host"
        };

        var result = await CallMethodAsync<Dictionary<string, JsonElement>>(
            "core.get_torrents_status",
            new object[] { filterDict, fields },
            cancellationToken);

        if (result == null) return Array.Empty<TorrentStatus>();

        var torrents = new List<TorrentStatus>();
        foreach (var kvp in result)
        {
            try
            {
                var torrent = ParseTorrentStatus(kvp.Key, kvp.Value);
                if (torrent != null) torrents.Add(torrent);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse torrent {Hash}", kvp.Key);
            }
        }

        return torrents;
    }

    public async Task<bool> RemoveTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallMethodAsync<bool>(
                "core.remove_torrent",
                new object[] { hash, deleteFiles },
                cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> PauseTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "core.pause_torrent",
                new object[] { new[] { hash } },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pause torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ResumeTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "core.resume_torrent",
                new object[] { new[] { hash } },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> PauseAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>("core.pause_all_torrents", null, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pause all torrents");
            return false;
        }
    }

    public async Task<bool> ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>("core.resume_all_torrents", null, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume all torrents");
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetLabelsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetLabelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallMethodAsync<string[]>("label.get_labels", null, cancellationToken);
            return result ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get labels (Label plugin may not be enabled)");
            return Array.Empty<string>();
        }
    }

    public async Task<bool> SetLabelAsync(string hash, string label, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "label.set_torrent",
                new object[] { hash, label },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set label for torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> AddLabelAsync(string label, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "label.add",
                new object[] { label },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to add label {Label}", label);
            return false;
        }
    }

    public async Task<DelugeSessionStatus?> GetSessionStatusAsync(CancellationToken cancellationToken = default)
    {
        var keys = new[]
        {
            "download_rate", "upload_rate", "total_download", "total_upload",
            "num_downloading", "num_seeding", "num_torrents", "dht_running",
            "dht_nodes", "free_space"
        };

        var result = await CallMethodAsync<Dictionary<string, JsonElement>>(
            "core.get_session_status",
            new object[] { keys },
            cancellationToken);

        if (result == null) return null;

        return new DelugeSessionStatus
        {
            DownloadRateBps = GetLong(result, "download_rate"),
            UploadRateBps = GetLong(result, "upload_rate"),
            TotalDownloadedBytes = GetLong(result, "total_download"),
            TotalUploadedBytes = GetLong(result, "total_upload"),
            NumDownloading = GetInt(result, "num_downloading"),
            NumSeeding = GetInt(result, "num_seeding"),
            NumTorrents = GetInt(result, "num_torrents"),
            DhtRunning = GetBool(result, "dht_running"),
            DhtNodes = GetInt(result, "dht_nodes"),
            FreeDiskSpace = GetLong(result, "free_space")
        };
    }

    public async Task<TorrentDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken);
        var freeSpace = await GetFreeSpaceAsync(config?.DownloadLocation, cancellationToken);

        if (freeSpace == null) return null;

        return new TorrentDiskSpace
        {
            FreeBytes = freeSpace.Value,
            TotalBytes = 0,
            IsLow = freeSpace.Value < 1L * 1024 * 1024 * 1024,
            Path = config?.DownloadLocation
        };
    }

    public async Task<long?> GetFreeSpaceAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                var config = await GetConfigAsync(cancellationToken);
                path = config?.DownloadLocation;
            }

            if (string.IsNullOrEmpty(path)) return null;

            var result = await CallMethodAsync<long>(
                "core.get_free_space",
                new object[] { path },
                cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get free space");
            return null;
        }
    }

    public async Task<bool> MoveStorageAsync(string hash, string destination, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "core.move_storage",
                new object[] { new[] { hash }, destination },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move storage for torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ForceRecheckAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "core.force_recheck",
                new object[] { new[] { hash } },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to force recheck torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ForceReannounceAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await CallMethodAsync<object>(
                "core.force_reannounce",
                new object[] { new[] { hash } },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to force reannounce torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> SetTorrentOptionsAsync(string hash, DelugeTorrentOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var optionsDict = new Dictionary<string, object>();

            if (options.MaxDownloadSpeed.HasValue)
                optionsDict["max_download_speed"] = options.MaxDownloadSpeed.Value;
            if (options.MaxUploadSpeed.HasValue)
                optionsDict["max_upload_speed"] = options.MaxUploadSpeed.Value;
            if (options.MaxConnections.HasValue)
                optionsDict["max_connections"] = options.MaxConnections.Value;
            if (options.MaxUploadSlots.HasValue)
                optionsDict["max_upload_slots"] = options.MaxUploadSlots.Value;
            if (options.PrioritizeFirstLastPieces.HasValue)
                optionsDict["prioritize_first_last_pieces"] = options.PrioritizeFirstLastPieces.Value;
            if (options.SequentialDownload.HasValue)
                optionsDict["sequential_download"] = options.SequentialDownload.Value;
            if (options.StopAtRatio.HasValue)
                optionsDict["stop_at_ratio"] = options.StopAtRatio.Value;
            if (options.RemoveAtRatio.HasValue)
                optionsDict["remove_at_ratio"] = options.RemoveAtRatio.Value;
            if (options.MoveCompleted.HasValue)
                optionsDict["move_completed"] = options.MoveCompleted.Value;
            if (!string.IsNullOrEmpty(options.MoveCompletedPath))
                optionsDict["move_completed_path"] = options.MoveCompletedPath;
            if (options.AutoManaged.HasValue)
                optionsDict["auto_managed"] = options.AutoManaged.Value;

            await CallMethodAsync<object>(
                "core.set_torrent_options",
                new object[] { new[] { hash }, optionsDict },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set torrent options for {Hash}", hash);
            return false;
        }
    }

    public async Task<DelugeConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await CallMethodAsync<Dictionary<string, JsonElement>>(
                "core.get_config",
                null,
                cancellationToken);

            if (result == null) return null;

            return new DelugeConfig
            {
                DownloadLocation = GetString(result, "download_location"),
                MoveCompleted = GetBool(result, "move_completed"),
                MoveCompletedPath = GetString(result, "move_completed_path"),
                MaxDownloadSpeed = GetInt(result, "max_download_speed"),
                MaxUploadSpeed = GetInt(result, "max_upload_speed"),
                MaxConnections = GetInt(result, "max_connections_global"),
                MaxActiveDownloading = GetInt(result, "max_active_downloading"),
                MaxActiveSeeding = GetInt(result, "max_active_seeding"),
                MaxActiveLimit = GetInt(result, "max_active_limit"),
                DhtEnabled = GetBool(result, "dht"),
                ListenPortStart = GetInt(result, "listen_ports", 0),
                ListenPortEnd = GetInt(result, "listen_ports", 1)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get config");
            return null;
        }
    }

    #region Private Methods

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_isAuthenticated) return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_isAuthenticated) return;

            var result = await CallMethodInternalAsync<bool>(
                "auth.login",
                new object[] { _settings.Password },
                cancellationToken,
                skipAuth: true);

            if (!result)
            {
                throw new DelugeAuthenticationException("Authentication failed: Invalid password");
            }

            _isAuthenticated = true;
            _logger?.LogDebug("Deluge authentication successful");
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task<T?> CallMethodAsync<T>(string method, object[]? parameters, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        return await CallMethodInternalAsync<T>(method, parameters, cancellationToken);
    }

    private async Task<T?> CallMethodInternalAsync<T>(string method, object[]? parameters, CancellationToken cancellationToken, bool skipAuth = false)
    {
        var requestId = Interlocked.Increment(ref _requestId);

        var request = new DelugeJsonRpcRequest
        {
            Id = requestId,
            Method = method,
            Params = parameters ?? Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_settings.JsonRpcUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var rpcResponse = JsonSerializer.Deserialize<DelugeJsonRpcResponse<T>>(responseJson, JsonOptions);

        if (rpcResponse?.Error != null)
        {
            var errorMessage = rpcResponse.Error.Message ?? "Unknown error";
            _logger?.LogWarning("Deluge RPC error: {Method} - {Error}", method, errorMessage);
            
            if (!skipAuth && errorMessage.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase))
            {
                _isAuthenticated = false;
                throw new DelugeAuthenticationException("Session expired");
            }

            throw new DelugeRpcException(errorMessage, rpcResponse.Error.Code);
        }

        return rpcResponse != null ? rpcResponse.Result : default;
    }

    private Dictionary<string, object> BuildAddTorrentOptions(TorrentAddOptions? options)
    {
        var delugeOptions = new Dictionary<string, object>();

        var downloadPath = options?.SavePath ?? _settings.DownloadPath;
        if (!string.IsNullOrEmpty(downloadPath))
        {
            delugeOptions["download_location"] = downloadPath;
        }

        var paused = options?.AddPaused ?? _settings.AddPaused;
        if (paused)
        {
            delugeOptions["add_paused"] = true;
        }

        if (_settings.MoveCompleted && !string.IsNullOrEmpty(_settings.MoveCompletedPath))
        {
            delugeOptions["move_completed"] = true;
            delugeOptions["move_completed_path"] = _settings.MoveCompletedPath;
        }

        if (options?.RatioLimit.HasValue == true)
        {
            delugeOptions["stop_at_ratio"] = true;
            delugeOptions["stop_ratio"] = options.RatioLimit.Value;
        }

        if (options?.SequentialDownload == true)
        {
            delugeOptions["sequential_download"] = true;
        }

        if (options?.FirstLastPiecePriority == true)
        {
            delugeOptions["prioritize_first_last_pieces"] = true;
        }

        return delugeOptions;
    }

    private async Task TrySetLabelAsync(string hash, string? label, CancellationToken cancellationToken)
    {
        label ??= _settings.Label;
        if (string.IsNullOrEmpty(label)) return;

        try
        {
            // First ensure the label exists
            var labels = await GetLabelsAsync(cancellationToken);
            if (!labels.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                await AddLabelAsync(label, cancellationToken);
            }

            await SetLabelAsync(hash, label, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to set label (Label plugin may not be enabled)");
        }
    }

    private static TorrentStatus? ParseTorrentStatus(string hash, JsonElement element)
    {
        var state = GetString(element, "state") ?? "Unknown";
        var errorMessage = GetString(element, "message");

        return new TorrentStatus
        {
            Hash = hash,
            Name = GetString(element, "name") ?? hash,
            State = MapState(state, errorMessage),
            Category = GetString(element, "label"),
            TotalBytes = GetLong(element, "total_size"),
            DownloadedBytes = GetLong(element, "total_done"),
            UploadedBytes = GetLong(element, "total_uploaded"),
            DownloadSpeedBps = GetLong(element, "download_payload_rate"),
            UploadSpeedBps = GetLong(element, "upload_payload_rate"),
            Seeds = GetInt(element, "num_seeds"),
            Peers = GetInt(element, "num_peers"),
            Ratio = GetDouble(element, "ratio"),
            EtaSeconds = GetInt(element, "eta") > 0 ? GetInt(element, "eta") : null,
            SavePath = GetString(element, "save_path"),
            ContentPath = GetString(element, "save_path"),
            AddedOn = GetLong(element, "time_added") > 0
                ? DateTimeOffset.FromUnixTimeSeconds(GetLong(element, "time_added")).UtcDateTime
                : null,
            Error = !string.IsNullOrEmpty(errorMessage) ? errorMessage : null
        };
    }

    private static TorrentState MapState(string state, string? errorMessage)
    {
        if (!string.IsNullOrEmpty(errorMessage) && state.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            return TorrentState.Error;
        }

        return state.ToLowerInvariant() switch
        {
            "downloading" => TorrentState.Downloading,
            "seeding" => TorrentState.Seeding,
            "paused" => TorrentState.Paused,
            "checking" => TorrentState.Checking,
            "queued" => TorrentState.Queued,
            "error" => TorrentState.Error,
            "moving" => TorrentState.Moving,
            "allocating" => TorrentState.Queued,
            _ => TorrentState.Unknown
        };
    }

    private static string? GetString(Dictionary<string, JsonElement> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetString(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int GetInt(Dictionary<string, JsonElement> dict, string key, int arrayIndex = -1)
    {
        if (!dict.TryGetValue(key, out var value)) return 0;

        if (arrayIndex >= 0 && value.ValueKind == JsonValueKind.Array)
        {
            var arr = value.EnumerateArray().ToArray();
            return arrayIndex < arr.Length ? arr[arrayIndex].GetInt32() : 0;
        }

        return value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
    }

    private static int GetInt(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static long GetLong(Dictionary<string, JsonElement> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;
    }

    private static long GetLong(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;
    }

    private static double GetDouble(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;
    }

    private static bool GetBool(Dictionary<string, JsonElement> dict, string key)
    {
        return dict.TryGetValue(key, out var value) &&
               (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) &&
               value.GetBoolean();
    }

    #endregion

    #region Internal Models

    private class DelugeJsonRpcRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object[] Params { get; set; } = Array.Empty<object>();
    }

    private class DelugeJsonRpcResponse<T>
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("error")]
        public DelugeJsonRpcError? Error { get; set; }
    }

    private class DelugeJsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when Deluge authentication fails.
/// </summary>
public class DelugeAuthenticationException : Exception
{
    public DelugeAuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when a Deluge RPC call fails.
/// </summary>
public class DelugeRpcException : Exception
{
    public int ErrorCode { get; }

    public DelugeRpcException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}
