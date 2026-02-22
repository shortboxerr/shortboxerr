using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Torrent;

namespace Shortboxerr.Infrastructure.Torrent;

/// <summary>
/// Transmission client implementation using the JSON-RPC API.
/// Reference: https://github.com/transmission/transmission/blob/main/docs/rpc-spec.md
/// </summary>
public class TransmissionClient : ITransmissionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TransmissionClient>? _logger;
    private TransmissionSettings _settings;
    private string? _sessionId;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string SessionIdHeader = "X-Transmission-Session-Id";

    public TransmissionClient(HttpClient httpClient, TransmissionSettings settings, ILogger<TransmissionClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        // Set up basic auth if credentials provided
        if (!string.IsNullOrEmpty(settings.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.Username}:{settings.Password ?? ""}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public TorrentClientType ClientType => TorrentClientType.Transmission;

    public void Configure(TransmissionSettings settings)
    {
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _sessionId = null;

        if (!string.IsNullOrEmpty(settings.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.Username}:{settings.Password ?? ""}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<TorrentClientTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var session = await GetSessionInfoAsync(cancellationToken);
            stopwatch.Stop();

            if (session != null)
            {
                _logger?.LogInformation("Transmission connection successful. Version: {Version}", session.Version);
                return TorrentClientTestResult.Ok(
                    $"Connected to Transmission {session.Version}",
                    session.Version,
                    stopwatch.ElapsedMilliseconds);
            }

            return TorrentClientTestResult.Failed("Failed to retrieve Transmission session info");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            stopwatch.Stop();
            _logger?.LogWarning("Transmission authentication failed");
            return TorrentClientTestResult.Failed("Authentication failed: Invalid username or password");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "Transmission connection failed");
            return TorrentClientTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Transmission connection error");
            return TorrentClientTestResult.Failed($"Error: {ex.Message}");
        }
    }

    public async Task<TransmissionSessionInfo?> GetSessionInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<TransmissionSessionResponse>(
            "session-get",
            null,
            cancellationToken);

        if (response?.Arguments == null) return null;

        var args = response.Arguments;
        return new TransmissionSessionInfo
        {
            Version = args.Version,
            RpcVersion = args.RpcVersion,
            RpcVersionMinimum = args.RpcVersionMinimum,
            DownloadDir = args.DownloadDir,
            ConfigDir = args.ConfigDir,
            SpeedLimitDownKBps = args.SpeedLimitDown,
            SpeedLimitDownEnabled = args.SpeedLimitDownEnabled,
            SpeedLimitUpKBps = args.SpeedLimitUp,
            SpeedLimitUpEnabled = args.SpeedLimitUpEnabled,
            SeedRatioLimit = args.SeedRatioLimit,
            SeedRatioLimited = args.SeedRatioLimited,
            IncompleteDirEnabled = args.IncompleteDirEnabled,
            IncompleteDir = args.IncompleteDir
        };
    }

    public async Task<TransmissionSessionStats?> GetSessionStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<TransmissionSessionStatsResponse>(
            "session-stats",
            null,
            cancellationToken);

        if (response?.Arguments == null) return null;

        var args = response.Arguments;
        return new TransmissionSessionStats
        {
            ActiveTorrentCount = args.ActiveTorrentCount,
            PausedTorrentCount = args.PausedTorrentCount,
            TorrentCount = args.TorrentCount,
            DownloadSpeedBps = args.DownloadSpeed,
            UploadSpeedBps = args.UploadSpeed,
            CurrentStats = args.CurrentStats != null ? new TransmissionCumulativeStats
            {
                DownloadedBytes = args.CurrentStats.DownloadedBytes,
                UploadedBytes = args.CurrentStats.UploadedBytes,
                FilesAdded = args.CurrentStats.FilesAdded,
                SessionCount = args.CurrentStats.SessionCount,
                SecondsActive = args.CurrentStats.SecondsActive
            } : null,
            CumulativeStats = args.CumulativeStats != null ? new TransmissionCumulativeStats
            {
                DownloadedBytes = args.CumulativeStats.DownloadedBytes,
                UploadedBytes = args.CumulativeStats.UploadedBytes,
                FilesAdded = args.CumulativeStats.FilesAdded,
                SessionCount = args.CumulativeStats.SessionCount,
                SecondsActive = args.CumulativeStats.SecondsActive
            } : null
        };
    }

    public async Task<TorrentAddResult> AddTorrentMagnetAsync(string magnetUri, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await AddTorrentInternalAsync(magnetUri, null, options, cancellationToken);
    }

    public async Task<TorrentAddResult> AddTorrentUrlAsync(string torrentUrl, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await AddTorrentInternalAsync(torrentUrl, null, options, cancellationToken);
    }

    public async Task<TorrentAddResult> AddTorrentFileAsync(byte[] torrentContent, string filename, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        var metainfo = Convert.ToBase64String(torrentContent);
        return await AddTorrentInternalAsync(null, metainfo, options, cancellationToken);
    }

    private async Task<TorrentAddResult> AddTorrentInternalAsync(string? filename, string? metainfo, TorrentAddOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            var args = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(filename))
            {
                args["filename"] = filename;
            }
            else if (!string.IsNullOrEmpty(metainfo))
            {
                args["metainfo"] = metainfo;
            }
            else
            {
                return TorrentAddResult.Failed("No torrent source provided");
            }

            var downloadDir = options?.SavePath ?? _settings.DownloadDir;
            if (!string.IsNullOrEmpty(downloadDir))
            {
                args["download-dir"] = downloadDir;
            }

            args["paused"] = options?.AddPaused ?? _settings.AddPaused;

            var response = await SendRequestAsync<TransmissionTorrentAddResponse>(
                "torrent-add",
                args,
                cancellationToken);

            if (response?.Result != "success")
            {
                return TorrentAddResult.Failed(response?.Result ?? "Unknown error");
            }

            var added = response.Arguments?.TorrentAdded ?? response.Arguments?.TorrentDuplicate;
            if (added != null)
            {
                _logger?.LogInformation("Torrent added: {Name} ({Hash})", added.Name, added.HashString);
                return TorrentAddResult.Ok(added.HashString);
            }

            return TorrentAddResult.Ok("added");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add torrent");
            return TorrentAddResult.Failed(ex.Message);
        }
    }

    public async Task<TorrentStatus?> GetStatusAsync(string hash, CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<TransmissionTorrentGetResponse>(
            "torrent-get",
            new Dictionary<string, object>
            {
                ["ids"] = new[] { hash },
                ["fields"] = TorrentFields
            },
            cancellationToken);

        var torrent = response?.Arguments?.Torrents?.FirstOrDefault();
        return torrent != null ? MapToTorrentStatus(torrent) : null;
    }

    public async Task<IReadOnlyList<TorrentStatus>> GetAllTorrentsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync<TransmissionTorrentGetResponse>(
            "torrent-get",
            new Dictionary<string, object>
            {
                ["fields"] = TorrentFields
            },
            cancellationToken);

        return response?.Arguments?.Torrents?
            .Select(MapToTorrentStatus)
            .ToList() ?? new List<TorrentStatus>();
    }

    public async Task<bool> RemoveTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-remove",
                new Dictionary<string, object>
                {
                    ["ids"] = new[] { hash },
                    ["delete-local-data"] = deleteFiles
                },
                cancellationToken);

            return response?.Result == "success";
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
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-stop",
                new Dictionary<string, object> { ["ids"] = new[] { hash } },
                cancellationToken);

            return response?.Result == "success";
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
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-start",
                new Dictionary<string, object> { ["ids"] = new[] { hash } },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> StartAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-start",
                null,
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start all torrents");
            return false;
        }
    }

    public async Task<bool> StopAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-stop",
                null,
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop all torrents");
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // Transmission doesn't have built-in category support like qBittorrent
        // Labels were added in Transmission 4.0
        // Return empty list for now
        await Task.CompletedTask;
        return Array.Empty<string>();
    }

    public async Task<TorrentDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        var session = await GetSessionInfoAsync(cancellationToken);
        if (session?.DownloadDir == null) return null;

        var freeSpace = await GetFreeSpaceAsync(session.DownloadDir, cancellationToken);
        if (freeSpace == null) return null;

        return new TorrentDiskSpace
        {
            FreeBytes = freeSpace.Value,
            TotalBytes = 0,
            IsLow = freeSpace.Value < 1L * 1024 * 1024 * 1024,
            Path = session.DownloadDir
        };
    }

    public async Task<long?> GetFreeSpaceAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionFreeSpaceResponse>(
                "free-space",
                new Dictionary<string, object> { ["path"] = path },
                cancellationToken);

            return response?.Arguments?.SizeBytes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get free space for {Path}", path);
            return null;
        }
    }

    public async Task<bool> MoveTorrentAsync(string hash, string newLocation, bool moveData = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-set-location",
                new Dictionary<string, object>
                {
                    ["ids"] = new[] { hash },
                    ["location"] = newLocation,
                    ["move"] = moveData
                },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> RenameTorrentPathAsync(string hash, string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-rename-path",
                new Dictionary<string, object>
                {
                    ["ids"] = new[] { hash },
                    ["path"] = oldPath,
                    ["name"] = newPath
                },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rename path for torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> VerifyTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-verify",
                new Dictionary<string, object> { ["ids"] = new[] { hash } },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to verify torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ReannounceAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "torrent-reannounce",
                new Dictionary<string, object> { ["ids"] = new[] { hash } },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reannounce torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> SetDownloadDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "session-set",
                new Dictionary<string, object> { ["download-dir"] = path },
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set download directory");
            return false;
        }
    }

    public async Task<bool> SetSpeedLimitsAsync(long? downloadLimitKBps, long? uploadLimitKBps, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new Dictionary<string, object>();

            if (downloadLimitKBps.HasValue)
            {
                args["speed-limit-down"] = downloadLimitKBps.Value;
                args["speed-limit-down-enabled"] = downloadLimitKBps.Value > 0;
            }

            if (uploadLimitKBps.HasValue)
            {
                args["speed-limit-up"] = uploadLimitKBps.Value;
                args["speed-limit-up-enabled"] = uploadLimitKBps.Value > 0;
            }

            var response = await SendRequestAsync<TransmissionBaseResponse>(
                "session-set",
                args,
                cancellationToken);

            return response?.Result == "success";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set speed limits");
            return false;
        }
    }

    #region Private Methods

    private static readonly string[] TorrentFields = new[]
    {
        "id", "hashString", "name", "status", "totalSize", "downloadedEver",
        "uploadedEver", "rateDownload", "rateUpload", "eta", "percentDone",
        "isFinished", "addedDate", "doneDate", "downloadDir", "error",
        "errorString", "peersConnected", "seeders", "leechers", "uploadRatio"
    };

    private async Task<T?> SendRequestAsync<T>(string method, object? arguments, CancellationToken cancellationToken)
        where T : TransmissionBaseResponse
    {
        var request = new TransmissionRequest
        {
            Method = method,
            Arguments = arguments
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Add session ID if we have one
        if (!string.IsNullOrEmpty(_sessionId))
        {
            content.Headers.Add(SessionIdHeader, _sessionId);
        }

        var response = await _httpClient.PostAsync(_settings.RpcUrl, content, cancellationToken);

        // Handle 409 Conflict - need to get new session ID
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            if (response.Headers.TryGetValues(SessionIdHeader, out var values))
            {
                _sessionId = values.FirstOrDefault();
                _logger?.LogDebug("Updated Transmission session ID");

                // Retry with new session ID
                content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.Add(SessionIdHeader, _sessionId);
                response = await _httpClient.PostAsync(_settings.RpcUrl, content, cancellationToken);
            }
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(responseJson, JsonOptions);
    }

    private static TorrentStatus MapToTorrentStatus(TransmissionTorrent torrent)
    {
        return new TorrentStatus
        {
            Hash = torrent.HashString,
            Name = torrent.Name,
            State = MapState(torrent.Status, torrent.Error),
            Category = null,
            TotalBytes = torrent.TotalSize,
            DownloadedBytes = torrent.DownloadedEver,
            UploadedBytes = torrent.UploadedEver,
            DownloadSpeedBps = torrent.RateDownload,
            UploadSpeedBps = torrent.RateUpload,
            Seeds = torrent.Seeders,
            Peers = torrent.Leechers,
            Ratio = torrent.UploadRatio,
            EtaSeconds = torrent.Eta >= 0 ? (int)torrent.Eta : null,
            SavePath = torrent.DownloadDir,
            ContentPath = torrent.DownloadDir,
            AddedOn = torrent.AddedDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(torrent.AddedDate).UtcDateTime : null,
            CompletedOn = torrent.DoneDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(torrent.DoneDate).UtcDateTime : null,
            Error = torrent.Error > 0 ? torrent.ErrorString : null
        };
    }

    private static TorrentState MapState(int status, int error)
    {
        if (error > 0) return TorrentState.Error;

        // Transmission status values:
        // 0 = stopped, 1 = check pending, 2 = checking, 3 = download pending
        // 4 = downloading, 5 = seed pending, 6 = seeding
        return status switch
        {
            0 => TorrentState.Paused,
            1 => TorrentState.Queued,
            2 => TorrentState.Checking,
            3 => TorrentState.Queued,
            4 => TorrentState.Downloading,
            5 => TorrentState.Queued,
            6 => TorrentState.Seeding,
            _ => TorrentState.Unknown
        };
    }

    #endregion

    #region Internal Models

    private class TransmissionRequest
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public object? Arguments { get; set; }
    }

    private class TransmissionBaseResponse
    {
        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;
    }

    private class TransmissionSessionResponse : TransmissionBaseResponse
    {
        [JsonPropertyName("arguments")]
        public TransmissionSessionArgs? Arguments { get; set; }
    }

    private class TransmissionSessionArgs
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("rpc-version")]
        public int RpcVersion { get; set; }

        [JsonPropertyName("rpc-version-minimum")]
        public int RpcVersionMinimum { get; set; }

        [JsonPropertyName("download-dir")]
        public string? DownloadDir { get; set; }

        [JsonPropertyName("config-dir")]
        public string? ConfigDir { get; set; }

        [JsonPropertyName("speed-limit-down")]
        public long SpeedLimitDown { get; set; }

        [JsonPropertyName("speed-limit-down-enabled")]
        public bool SpeedLimitDownEnabled { get; set; }

        [JsonPropertyName("speed-limit-up")]
        public long SpeedLimitUp { get; set; }

        [JsonPropertyName("speed-limit-up-enabled")]
        public bool SpeedLimitUpEnabled { get; set; }

        [JsonPropertyName("seedRatioLimit")]
        public double SeedRatioLimit { get; set; }

        [JsonPropertyName("seedRatioLimited")]
        public bool SeedRatioLimited { get; set; }

        [JsonPropertyName("incomplete-dir-enabled")]
        public bool IncompleteDirEnabled { get; set; }

        [JsonPropertyName("incomplete-dir")]
        public string? IncompleteDir { get; set; }
    }

    private class TransmissionSessionStatsResponse : TransmissionBaseResponse
    {
        [JsonPropertyName("arguments")]
        public TransmissionSessionStatsArgs? Arguments { get; set; }
    }

    private class TransmissionSessionStatsArgs
    {
        [JsonPropertyName("activeTorrentCount")]
        public int ActiveTorrentCount { get; set; }

        [JsonPropertyName("pausedTorrentCount")]
        public int PausedTorrentCount { get; set; }

        [JsonPropertyName("torrentCount")]
        public int TorrentCount { get; set; }

        [JsonPropertyName("downloadSpeed")]
        public long DownloadSpeed { get; set; }

        [JsonPropertyName("uploadSpeed")]
        public long UploadSpeed { get; set; }

        [JsonPropertyName("current-stats")]
        public TransmissionStatsData? CurrentStats { get; set; }

        [JsonPropertyName("cumulative-stats")]
        public TransmissionStatsData? CumulativeStats { get; set; }
    }

    private class TransmissionStatsData
    {
        [JsonPropertyName("downloadedBytes")]
        public long DownloadedBytes { get; set; }

        [JsonPropertyName("uploadedBytes")]
        public long UploadedBytes { get; set; }

        [JsonPropertyName("filesAdded")]
        public int FilesAdded { get; set; }

        [JsonPropertyName("sessionCount")]
        public int SessionCount { get; set; }

        [JsonPropertyName("secondsActive")]
        public long SecondsActive { get; set; }
    }

    private class TransmissionTorrentAddResponse : TransmissionBaseResponse
    {
        [JsonPropertyName("arguments")]
        public TransmissionTorrentAddArgs? Arguments { get; set; }
    }

    private class TransmissionTorrentAddArgs
    {
        [JsonPropertyName("torrent-added")]
        public TransmissionTorrentAddedInfo? TorrentAdded { get; set; }

        [JsonPropertyName("torrent-duplicate")]
        public TransmissionTorrentAddedInfo? TorrentDuplicate { get; set; }
    }

    private class TransmissionTorrentAddedInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("hashString")]
        public string HashString { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private class TransmissionTorrentGetResponse : TransmissionBaseResponse
    {
        [JsonPropertyName("arguments")]
        public TransmissionTorrentGetArgs? Arguments { get; set; }
    }

    private class TransmissionTorrentGetArgs
    {
        [JsonPropertyName("torrents")]
        public List<TransmissionTorrent>? Torrents { get; set; }
    }

    private class TransmissionTorrent
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("hashString")]
        public string HashString { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("totalSize")]
        public long TotalSize { get; set; }

        [JsonPropertyName("downloadedEver")]
        public long DownloadedEver { get; set; }

        [JsonPropertyName("uploadedEver")]
        public long UploadedEver { get; set; }

        [JsonPropertyName("rateDownload")]
        public long RateDownload { get; set; }

        [JsonPropertyName("rateUpload")]
        public long RateUpload { get; set; }

        [JsonPropertyName("eta")]
        public long Eta { get; set; }

        [JsonPropertyName("percentDone")]
        public double PercentDone { get; set; }

        [JsonPropertyName("isFinished")]
        public bool IsFinished { get; set; }

        [JsonPropertyName("addedDate")]
        public long AddedDate { get; set; }

        [JsonPropertyName("doneDate")]
        public long DoneDate { get; set; }

        [JsonPropertyName("downloadDir")]
        public string? DownloadDir { get; set; }

        [JsonPropertyName("error")]
        public int Error { get; set; }

        [JsonPropertyName("errorString")]
        public string? ErrorString { get; set; }

        [JsonPropertyName("peersConnected")]
        public int PeersConnected { get; set; }

        [JsonPropertyName("seeders")]
        public int Seeders { get; set; }

        [JsonPropertyName("leechers")]
        public int Leechers { get; set; }

        [JsonPropertyName("uploadRatio")]
        public double UploadRatio { get; set; }
    }

    private class TransmissionFreeSpaceResponse : TransmissionBaseResponse
    {
        [JsonPropertyName("arguments")]
        public TransmissionFreeSpaceArgs? Arguments { get; set; }
    }

    private class TransmissionFreeSpaceArgs
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("size-bytes")]
        public long SizeBytes { get; set; }
    }

    #endregion
}
