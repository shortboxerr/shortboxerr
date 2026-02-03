using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.DownloadClients;

namespace Shortboxerr.Infrastructure.DownloadClients;

/// <summary>
/// Built-in HTTP download client implementation.
/// This is an internal service, NOT a user-configurable download client provider.
/// It's always available and used by DDL providers and RSS indexers for direct HTTP downloads.
/// Similar to how Mylar3 handles DDL downloads internally.
/// </summary>
public class HttpDownloadClient : IHttpDownloadClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpDownloadClient> _logger;
    private readonly HttpDownloadClientSettings _settings;
    
    private readonly ConcurrentDictionary<string, ActiveDownload> _activeDownloads = new();
    private readonly SemaphoreSlim _downloadSemaphore;

    public HttpDownloadClient(
        HttpClient httpClient,
        ILogger<HttpDownloadClient> logger,
        HttpDownloadClientSettings settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        
        _downloadSemaphore = new SemaphoreSlim(_settings.MaxConcurrentDownloads);
        ConfigureHttpClient();
    }

    #region IHttpDownloadClient Implementation

    public async Task<HttpDownloadResult> DownloadUrlAsync(
        string url,
        string destinationPath,
        HttpDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var downloadId = Guid.NewGuid().ToString();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var retryCount = 0;
        var maxRetries = options?.MaxRetries ?? _settings.MaxRetries;
        
        await _downloadSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var active = new ActiveDownload
            {
                Id = downloadId,
                Url = url,
                Title = Path.GetFileName(destinationPath),
                StartedAt = DateTime.UtcNow,
                CancellationTokenSource = cts
            };
            
            _activeDownloads[downloadId] = active;
            
            while (retryCount <= maxRetries)
            {
                try
                {
                    return await PerformDownloadAsync(url, destinationPath, options, active, cts.Token);
                }
                catch (HttpRequestException ex) when (retryCount < maxRetries && IsRetryableError(ex))
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Download attempt {Attempt} failed, retrying: {Url}", retryCount, url);
                    
                    var delay = _settings.RetryDelayMs * (int)Math.Pow(2, retryCount - 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            
            return HttpDownloadResult.Fail($"Download failed after {retryCount} retries");
        }
        finally
        {
            _activeDownloads.TryRemove(downloadId, out _);
            _downloadSemaphore.Release();
        }
    }

    public async Task<long?> GetFileSizeAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return response.Content.Headers.ContentLength;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get file size for {Url}", url);
            return null;
        }
    }

    public async Task<bool> IsReachableAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
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
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Shortboxerr/1.0 (HTTP Download Client)");
        }
    }

    private async Task<HttpDownloadResult> PerformDownloadAsync(
        string url,
        string destinationPath,
        HttpDownloadOptions? options,
        ActiveDownload active,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // Configure request headers
        ConfigureRequest(request, options);
        
        // Check for partial download resume
        long resumeOffset = 0;
        if (options?.ResumePartial == true && File.Exists(destinationPath))
        {
            var existingSize = new FileInfo(destinationPath).Length;
            request.Headers.Range = new RangeHeaderValue(existingSize, null);
            resumeOffset = existingSize;
        }
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            return HttpDownloadResult.Fail(
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                (int)response.StatusCode);
        }
        
        var totalBytes = response.Content.Headers.ContentLength + resumeOffset;
        active.TotalBytes = totalBytes;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Download with progress tracking
        var fileMode = resumeOffset > 0 ? FileMode.Append : FileMode.Create;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, fileMode, FileAccess.Write, FileShare.None, 81920, true);
        
        var buffer = new byte[81920];
        var bytesRead = 0L;
        var lastProgressUpdate = DateTime.UtcNow;
        var lastBytesForSpeed = 0L;
        
        int read;
        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;
            active.DownloadedBytes = bytesRead + resumeOffset;
            
            // Update progress
            if ((DateTime.UtcNow - lastProgressUpdate).TotalMilliseconds >= 100)
            {
                var elapsed = (DateTime.UtcNow - lastProgressUpdate).TotalSeconds;
                var bytesSinceLastUpdate = bytesRead - lastBytesForSpeed;
                active.SpeedBytesPerSecond = (long)(bytesSinceLastUpdate / elapsed);
                active.Progress = totalBytes > 0 ? (double)(bytesRead + resumeOffset) / totalBytes.Value * 100 : 0;
                
                options?.Progress?.Report(new HttpDownloadProgress
                {
                    TotalBytes = totalBytes,
                    BytesDownloaded = bytesRead + resumeOffset,
                    SpeedBytesPerSecond = active.SpeedBytesPerSecond
                });
                
                lastProgressUpdate = DateTime.UtcNow;
                lastBytesForSpeed = bytesRead;
            }
        }
        
        stopwatch.Stop();
        var finalSize = new FileInfo(destinationPath).Length;
        
        _logger.LogInformation("Downloaded {Size} bytes to {Path} in {Duration}s", 
            finalSize, destinationPath, stopwatch.Elapsed.TotalSeconds.ToString("F1"));
        
        return new HttpDownloadResult
        {
            Success = true,
            FilePath = destinationPath,
            FileSize = finalSize,
            Duration = stopwatch.Elapsed,
            AverageSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 
                ? (long)(finalSize / stopwatch.Elapsed.TotalSeconds) 
                : null,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            StatusCode = (int)response.StatusCode
        };
    }

    private void ConfigureRequest(HttpRequestMessage request, HttpDownloadOptions? options)
    {
        if (options == null) return;
        
        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd(options.UserAgent);
        }
        
        if (!string.IsNullOrEmpty(options.Referer))
        {
            request.Headers.Referrer = new Uri(options.Referer);
        }
        
        if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
        
        if (options.CustomHeaders != null)
        {
            foreach (var header in options.CustomHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        
        if (options.Cookies != null && options.Cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ", options.Cookies.Select(c => $"{c.Key}={c.Value}"));
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }
    }

    private static bool IsRetryableError(HttpRequestException ex)
    {
        // Retry on transient errors
        return ex.StatusCode is 
            System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
    }

    private static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(title.Length);
        
        foreach (var c in title)
        {
            sanitized.Append(invalid.Contains(c) ? '_' : c);
        }
        
        var result = sanitized.ToString().Trim();
        
        // Ensure it has a proper extension
        if (!result.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase) &&
            !result.EndsWith(".cbr", StringComparison.OrdinalIgnoreCase) &&
            !result.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            result += ".cbz";
        }
        
        return result;
    }

    #endregion

    #region Helper Classes

    private class ActiveDownload
    {
        public required string Id { get; init; }
        public required string Url { get; init; }
        public required string Title { get; init; }
        public DateTime StartedAt { get; init; }
        public long? TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public long SpeedBytesPerSecond { get; set; }
        public double Progress { get; set; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
    }

    #endregion
}

