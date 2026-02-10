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
/// qBittorrent client implementation using Web API v2.
/// Reference: https://github.com/qbittorrent/qBittorrent/wiki/WebUI-API-(qBittorrent-4.1)
/// </summary>
public class QBittorrentClient : IQBittorrentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QBittorrentClient>? _logger;
    private QBittorrentSettings _settings;
    private bool _isAuthenticated;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public QBittorrentClient(HttpClient httpClient, QBittorrentSettings settings, ILogger<QBittorrentClient>? logger = null)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        
        // qBittorrent uses cookies for session management
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
    }

    public TorrentClientType ClientType => TorrentClientType.QBittorrent;

    /// <summary>
    /// Updates the client settings.
    /// </summary>
    public void Configure(QBittorrentSettings settings)
    {
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _isAuthenticated = false; // Force re-authentication
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
                _logger?.LogInformation("qBittorrent connection successful. Version: {Version}", version);
                return TorrentClientTestResult.Ok($"Connected to qBittorrent {version}", version, stopwatch.ElapsedMilliseconds);
            }

            return TorrentClientTestResult.Failed("Failed to retrieve qBittorrent version");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "qBittorrent connection failed");
            return TorrentClientTestResult.Failed($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "qBittorrent connection error");
            return TorrentClientTestResult.Failed($"Error: {ex.Message}");
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/app/version", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetApiVersionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/app/webapiVersion", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<TorrentAddResult> AddTorrentMagnetAsync(string magnetUri, TorrentAddOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = CreateAddTorrentContent(urls: magnetUri, options: options);
            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/add", content, cancellationToken);

            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                return TorrentAddResult.Failed("Invalid torrent parameters");
            }

            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // qBittorrent returns "Ok." on success
            if (responseText.Contains("Ok", StringComparison.OrdinalIgnoreCase))
            {
                // Extract hash from magnet if possible
                var hash = ExtractHashFromMagnet(magnetUri);
                _logger?.LogInformation("Torrent added via magnet: {Hash}", hash ?? "unknown");
                return TorrentAddResult.Ok(hash ?? "added");
            }

            return TorrentAddResult.Failed($"Unexpected response: {responseText}");
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
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = CreateAddTorrentContent(urls: torrentUrl, options: options);
            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/add", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (responseText.Contains("Ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("Torrent added via URL: {Url}", torrentUrl);
                return TorrentAddResult.Ok("added");
            }

            return TorrentAddResult.Failed($"Unexpected response: {responseText}");
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
            await EnsureAuthenticatedAsync(cancellationToken);

            using var content = new MultipartFormDataContent();
            
            // Add torrent file
            var fileContent = new ByteArrayContent(torrentContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-bittorrent");
            content.Add(fileContent, "torrents", filename);

            // Add options
            AddOptionsToContent(content, options);

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/add", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (responseText.Contains("Ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("Torrent file added: {Filename}", filename);
                return TorrentAddResult.Ok("added");
            }

            return TorrentAddResult.Failed($"Unexpected response: {responseText}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add torrent file: {Filename}", filename);
            return TorrentAddResult.Failed(ex.Message);
        }
    }

    public async Task<TorrentStatus?> GetStatusAsync(string hash, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync(
            $"{_settings.ApiUrl}/torrents/info?hashes={hash}", 
            cancellationToken);
        
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var torrents = JsonSerializer.Deserialize<List<QBittorrentTorrent>>(json, JsonOptions);

        return torrents?.FirstOrDefault() is { } torrent ? MapToTorrentStatus(torrent) : null;
    }

    public async Task<IReadOnlyList<TorrentStatus>> GetAllTorrentsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/torrents/info", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var torrents = JsonSerializer.Deserialize<List<QBittorrentTorrent>>(json, JsonOptions);

        return torrents?.Select(MapToTorrentStatus).ToList() ?? new List<TorrentStatus>();
    }

    public async Task<bool> RemoveTorrentAsync(string hash, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash),
                new KeyValuePair<string, string>("deleteFiles", deleteFiles.ToString().ToLowerInvariant())
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/delete", content, cancellationToken);
            return response.IsSuccessStatusCode;
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
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash)
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/pause", content, cancellationToken);
            return response.IsSuccessStatusCode;
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
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash)
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/resume", content, cancellationToken);
            return response.IsSuccessStatusCode;
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
            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/pause", 
                new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", "all") }), 
                cancellationToken);
            return response.IsSuccessStatusCode;
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
            await EnsureAuthenticatedAsync(cancellationToken);
            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/resume",
                new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", "all") }),
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume all torrents");
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/torrents/categories", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // qBittorrent returns categories as an object with category names as keys
        var categories = JsonSerializer.Deserialize<Dictionary<string, QBittorrentCategory>>(json, JsonOptions);
        
        return categories?.Keys.ToList() ?? new List<string>();
    }

    public async Task<bool> CreateCategoryAsync(string name, string? savePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var formData = new List<KeyValuePair<string, string>>
            {
                new("category", name)
            };

            if (!string.IsNullOrEmpty(savePath))
            {
                formData.Add(new KeyValuePair<string, string>("savePath", savePath));
            }

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/createCategory", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create category {Name}", name);
            return false;
        }
    }

    public async Task<bool> SetDownloadLimitAsync(long speedBps, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("limit", speedBps.ToString())
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/transfer/setDownloadLimit", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set download limit");
            return false;
        }
    }

    public async Task<bool> SetUploadLimitAsync(long speedBps, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("limit", speedBps.ToString())
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/transfer/setUploadLimit", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set upload limit");
            return false;
        }
    }

    public async Task<QBittorrentTransferInfo?> GetTransferInfoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/transfer/info", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<QBittorrentTransferInfoRaw>(json, JsonOptions);

        if (info == null) return null;

        return new QBittorrentTransferInfo
        {
            DownloadSpeedBps = info.DlInfoSpeed,
            UploadSpeedBps = info.UpInfoSpeed,
            DownloadLimitBps = info.DlRateLimit,
            UploadLimitBps = info.UpRateLimit,
            SessionDownloadedBytes = info.DlInfoData,
            SessionUploadedBytes = info.UpInfoData,
            AllTimeDownloadedBytes = info.AlltimeDl,
            AllTimeUploadedBytes = info.AlltimeUl,
            ConnectionStatus = info.ConnectionStatus,
            DhtNodes = info.DhtNodes,
            FreeDiskSpaceBytes = info.FreeSpaceOnDisk
        };
    }

    public async Task<QBittorrentPreferences?> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/app/preferences", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<QBittorrentPreferences>(json, JsonOptions);
    }

    public async Task<TorrentDiskSpace?> GetDiskSpaceAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetTransferInfoAsync(cancellationToken);
        if (info == null) return null;

        return new TorrentDiskSpace
        {
            FreeBytes = info.FreeDiskSpaceBytes,
            TotalBytes = 0, // qBittorrent API doesn't provide total space
            IsLow = info.FreeDiskSpaceBytes < 1L * 1024 * 1024 * 1024, // Less than 1GB
            Path = _settings.SavePath
        };
    }

    public async Task<bool> RecheckTorrentAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash)
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/recheck", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to recheck torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> ForceStartAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash),
                new KeyValuePair<string, string>("value", "true")
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/setForceStart", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to force start torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> SetCategoryAsync(string hash, string category, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash),
                new KeyValuePair<string, string>("category", category)
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/setCategory", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set category for torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<bool> SetPriorityAsync(string hash, QBittorrentPriority priority, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            var endpoint = priority switch
            {
                QBittorrentPriority.TopPriority => "topPrio",
                QBittorrentPriority.BottomPriority => "bottomPrio",
                QBittorrentPriority.IncreasePriority => "increasePrio",
                QBittorrentPriority.DecreasePriority => "decreasePrio",
                _ => throw new ArgumentOutOfRangeException(nameof(priority))
            };

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("hashes", hash)
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/torrents/{endpoint}", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set priority for torrent {Hash}", hash);
            return false;
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

            // qBittorrent 4.1+ uses /api/v2/auth/login
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", _settings.Username ?? "admin"),
                new KeyValuePair<string, string>("password", _settings.Password ?? "")
            });

            var response = await _httpClient.PostAsync($"{_settings.ApiUrl}/auth/login", content, cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode && responseText.Contains("Ok", StringComparison.OrdinalIgnoreCase))
            {
                _isAuthenticated = true;
                _logger?.LogDebug("qBittorrent authentication successful");
            }
            else if (responseText.Contains("Fails", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("qBittorrent authentication failed: Invalid username or password");
            }
            else
            {
                // Some versions return empty body on success
                _isAuthenticated = response.IsSuccessStatusCode;
            }
        }
        finally
        {
            _authLock.Release();
        }
    }

    private MultipartFormDataContent CreateAddTorrentContent(string? urls = null, TorrentAddOptions? options = null)
    {
        var content = new MultipartFormDataContent();

        if (!string.IsNullOrEmpty(urls))
        {
            content.Add(new StringContent(urls), "urls");
        }

        AddOptionsToContent(content, options);
        return content;
    }

    private void AddOptionsToContent(MultipartFormDataContent content, TorrentAddOptions? options)
    {
        var category = options?.Category ?? _settings.Category;
        if (!string.IsNullOrEmpty(category))
        {
            content.Add(new StringContent(category), "category");
        }

        var savePath = options?.SavePath ?? _settings.SavePath;
        if (!string.IsNullOrEmpty(savePath))
        {
            content.Add(new StringContent(savePath), "savepath");
        }

        var paused = options?.AddPaused ?? _settings.AddPaused;
        if (paused)
        {
            content.Add(new StringContent("true"), "paused");
        }

        if (options?.SkipHashCheck == true)
        {
            content.Add(new StringContent("true"), "skip_checking");
        }

        var sequential = options?.SequentialDownload ?? _settings.SequentialDownload;
        if (sequential)
        {
            content.Add(new StringContent("true"), "sequentialDownload");
        }

        var firstLast = options?.FirstLastPiecePriority ?? _settings.FirstLastPiecePriority;
        if (firstLast)
        {
            content.Add(new StringContent("true"), "firstLastPiecePrio");
        }

        var ratioLimit = options?.RatioLimit ?? _settings.DefaultRatioLimit;
        if (ratioLimit.HasValue)
        {
            content.Add(new StringContent(ratioLimit.Value.ToString("F2")), "ratioLimit");
        }

        var seedingTimeLimit = options?.SeedingTimeLimitMinutes ?? _settings.DefaultSeedingTimeLimit;
        if (seedingTimeLimit.HasValue)
        {
            content.Add(new StringContent(seedingTimeLimit.Value.ToString()), "seedingTimeLimit");
        }
    }

    private static string? ExtractHashFromMagnet(string magnetUri)
    {
        // magnet:?xt=urn:btih:HASH&...
        const string btihPrefix = "urn:btih:";
        var startIndex = magnetUri.IndexOf(btihPrefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return null;

        startIndex += btihPrefix.Length;
        var endIndex = magnetUri.IndexOf('&', startIndex);
        
        return endIndex < 0 
            ? magnetUri[startIndex..].ToLowerInvariant() 
            : magnetUri[startIndex..endIndex].ToLowerInvariant();
    }

    private static TorrentStatus MapToTorrentStatus(QBittorrentTorrent torrent)
    {
        return new TorrentStatus
        {
            Hash = torrent.Hash,
            Name = torrent.Name,
            State = MapState(torrent.State),
            Category = torrent.Category,
            TotalBytes = torrent.TotalSize,
            DownloadedBytes = torrent.Downloaded,
            UploadedBytes = torrent.Uploaded,
            DownloadSpeedBps = torrent.DlSpeed,
            UploadSpeedBps = torrent.UpSpeed,
            Seeds = torrent.NumSeeds,
            Peers = torrent.NumLeechers,
            Ratio = torrent.Ratio,
            EtaSeconds = torrent.Eta < int.MaxValue ? (int)torrent.Eta : null,
            SavePath = torrent.SavePath,
            ContentPath = torrent.ContentPath,
            AddedOn = torrent.AddedOn > 0 ? DateTimeOffset.FromUnixTimeSeconds(torrent.AddedOn).UtcDateTime : null,
            CompletedOn = torrent.CompletionOn > 0 ? DateTimeOffset.FromUnixTimeSeconds(torrent.CompletionOn).UtcDateTime : null,
            Error = string.IsNullOrEmpty(torrent.State) || !torrent.State.Contains("error", StringComparison.OrdinalIgnoreCase) 
                ? null 
                : "Error state detected"
        };
    }

    private static TorrentState MapState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "error" => TorrentState.Error,
            "missingfiles" => TorrentState.Error,
            "uploading" => TorrentState.Seeding,
            "pausedup" => TorrentState.Paused,
            "queuedup" => TorrentState.Queued,
            "stalledup" => TorrentState.Seeding,
            "checkingup" => TorrentState.Checking,
            "forcedup" => TorrentState.Seeding,
            "allocating" => TorrentState.Queued,
            "downloading" => TorrentState.Downloading,
            "metadl" => TorrentState.FetchingMetadata,
            "pauseddl" => TorrentState.Paused,
            "queueddl" => TorrentState.Queued,
            "stalleddl" => TorrentState.Stalled,
            "checkingdl" => TorrentState.Checking,
            "forceddl" => TorrentState.Downloading,
            "checkingresumedata" => TorrentState.Checking,
            "moving" => TorrentState.Moving,
            _ => TorrentState.Unknown
        };
    }

    #endregion

    #region Internal Models

    private class QBittorrentTorrent
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("total_size")]
        public long TotalSize { get; set; }

        [JsonPropertyName("downloaded")]
        public long Downloaded { get; set; }

        [JsonPropertyName("uploaded")]
        public long Uploaded { get; set; }

        [JsonPropertyName("dlspeed")]
        public long DlSpeed { get; set; }

        [JsonPropertyName("upspeed")]
        public long UpSpeed { get; set; }

        [JsonPropertyName("num_seeds")]
        public int NumSeeds { get; set; }

        [JsonPropertyName("num_leechs")]
        public int NumLeechers { get; set; }

        [JsonPropertyName("ratio")]
        public double Ratio { get; set; }

        [JsonPropertyName("eta")]
        public long Eta { get; set; }

        [JsonPropertyName("save_path")]
        public string? SavePath { get; set; }

        [JsonPropertyName("content_path")]
        public string? ContentPath { get; set; }

        [JsonPropertyName("added_on")]
        public long AddedOn { get; set; }

        [JsonPropertyName("completion_on")]
        public long CompletionOn { get; set; }

        [JsonPropertyName("progress")]
        public double Progress { get; set; }
    }

    private class QBittorrentTransferInfoRaw
    {
        [JsonPropertyName("dl_info_speed")]
        public long DlInfoSpeed { get; set; }

        [JsonPropertyName("up_info_speed")]
        public long UpInfoSpeed { get; set; }

        [JsonPropertyName("dl_rate_limit")]
        public long DlRateLimit { get; set; }

        [JsonPropertyName("up_rate_limit")]
        public long UpRateLimit { get; set; }

        [JsonPropertyName("dl_info_data")]
        public long DlInfoData { get; set; }

        [JsonPropertyName("up_info_data")]
        public long UpInfoData { get; set; }

        [JsonPropertyName("alltime_dl")]
        public long AlltimeDl { get; set; }

        [JsonPropertyName("alltime_ul")]
        public long AlltimeUl { get; set; }

        [JsonPropertyName("connection_status")]
        public string ConnectionStatus { get; set; } = string.Empty;

        [JsonPropertyName("dht_nodes")]
        public int DhtNodes { get; set; }

        [JsonPropertyName("free_space_on_disk")]
        public long FreeSpaceOnDisk { get; set; }
    }

    private class QBittorrentCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("savePath")]
        public string? SavePath { get; set; }
    }

    #endregion
}
