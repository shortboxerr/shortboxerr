using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for downloading files from DDL sources.
/// Implements retry logic, progress tracking, failure handling, and host resolution.
/// </summary>
public class DdlDownloadService : IDdlDownloadService
{
    private readonly ILogger<DdlDownloadService>? _logger;
    private readonly IDownloadHostResolverFactory? _resolverFactory;
    private readonly IHostBlacklistService? _blacklistService;
    private readonly IActivityService? _activityService;
    private readonly ConcurrentDictionary<string, DdlDownloadStatus> _activeDownloads = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly List<DdlDownloadHistoryEntry> _downloadHistory = new();
    private readonly object _historyLock = new();
    
    // Default User-Agent (matches common browser)
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    
    // Buffer size for downloads
    private const int BufferSize = 81920; // 80KB
    
    // Magic bytes for comic file formats
    private static readonly byte[] ZipMagic = { 0x50, 0x4B }; // PK (ZIP, CBZ)
    private static readonly byte[] RarMagic = { 0x52, 0x61, 0x72 }; // Rar
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    private static readonly byte[] SevenZipMagic = { 0x37, 0x7A, 0xBC, 0xAF }; // 7z
    
    public DdlDownloadService(
        IDownloadHostResolverFactory? resolverFactory = null, 
        IHostBlacklistService? blacklistService = null,
        ILogger<DdlDownloadService>? logger = null,
        IActivityService? activityService = null)
    {
        _resolverFactory = resolverFactory;
        _blacklistService = blacklistService;
        _logger = logger;
        _activityService = activityService;
    }

    public async Task<DdlDownloadResult> DownloadAsync(DdlCandidate candidate, DdlDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DdlDownloadOptions();
        
        _logger?.LogInformation("Download initiated: {Title} from {Source}", candidate.ReleaseTitle, candidate.SourceSite);
        _logger?.LogDebug("Candidate has {LinkCount} download links", candidate.DownloadLinks.Count);
        
        if (candidate.DownloadLinks.Count == 0)
        {
            _logger?.LogWarning("Download failed: No valid links for {Title}", candidate.ReleaseTitle);
            return DdlDownloadResult.Failed(
                Guid.NewGuid().ToString(),
                DdlDownloadFailureReason.NoValidLinks,
                "No download links available for candidate"
            );
        }
        
        // Sort links by priority (Direct links first, then by configured priority)
        // Filter out blacklisted hosts
        var sortedLinks = candidate.DownloadLinks
            .Where(l => !IsLinkBlacklisted(l))
            .OrderBy(l => l.LinkType == DdlLinkType.Direct ? 0 : 1)
            .ThenBy(l => l.Priority)
            .ToList();
        
        if (sortedLinks.Count == 0 && candidate.DownloadLinks.Count > 0)
        {
            _logger?.LogWarning("All {Count} download links are blacklisted for {Title}", 
                candidate.DownloadLinks.Count, candidate.ReleaseTitle);
            return DdlDownloadResult.Failed(
                Guid.NewGuid().ToString(),
                DdlDownloadFailureReason.NoValidLinks,
                "All download links are from blacklisted hosts"
            );
        }
        
        // Determine filename from first link (may be updated by resolver)
        var baseFilename = options.CustomFilename ?? DeriveFilename(candidate, sortedLinks[0].Url);
        var destinationFolder = options.DestinationFolder ?? Path.GetTempPath();
        var destinationPath = Path.Combine(destinationFolder, baseFilename);
        
        DdlDownloadResult? lastResult = null;
        
        // Try each link in priority order with automatic fallback
        for (var linkIndex = 0; linkIndex < sortedLinks.Count; linkIndex++)
        {
            var currentLink = sortedLinks[linkIndex];
            
            if (linkIndex > 0)
            {
                _logger?.LogInformation("Trying alternate link {Num}/{Total}: {LinkType} ({Host})", 
                    linkIndex + 1, sortedLinks.Count, currentLink.LinkType, currentLink.HostName ?? "unknown");
            }
            else
            {
                _logger?.LogDebug("Selected link: {LinkType} priority {Priority} ({Host})", 
                    currentLink.LinkType, currentLink.Priority, currentLink.HostName ?? "unknown");
            }
            
            // Resolve hoster links to direct download URLs
            var downloadUrl = currentLink.Url;
            var resolvedFilename = baseFilename;
            
            if (currentLink.LinkType == DdlLinkType.Hoster && _resolverFactory != null)
            {
                var resolveResult = await ResolveHostedLinkAsync(currentLink, options, cancellationToken);
                if (!resolveResult.Success)
                {
                    _logger?.LogWarning("Failed to resolve {Host} link: {Error}", 
                        currentLink.HostName, resolveResult.ErrorMessage);
                    
                    // Record failure for blacklist tracking
                    RecordHostFailure(currentLink.HostName, resolveResult.FailureReason, resolveResult.ErrorMessage);
                    
                    lastResult = DdlDownloadResult.Failed(
                        Guid.NewGuid().ToString(),
                        DdlDownloadFailureReason.LinkResolutionFailed,
                        resolveResult.ErrorMessage ?? "Failed to resolve hosted link",
                        sourceUrl: currentLink.Url
                    );
                    continue; // Try next link
                }
                
                downloadUrl = resolveResult.DirectUrl!;
                
                // Use filename from resolver if available and not custom
                if (options.CustomFilename == null && !string.IsNullOrEmpty(resolveResult.Filename))
                {
                    resolvedFilename = SanitizeFilename(resolveResult.Filename);
                    destinationPath = Path.Combine(destinationFolder, resolvedFilename);
                }
                
                _logger?.LogDebug("Resolved {Host} to direct URL, filename: {Filename}", 
                    currentLink.HostName, resolvedFilename);
            }
            
            // Attempt download
            lastResult = await DownloadWithRetriesAsync(downloadUrl, destinationPath, options, cancellationToken);
            
            if (lastResult.Success)
            {
                // Record success for blacklist tracking
                RecordHostSuccess(currentLink.HostName);
                break; // Success - no need to try more links
            }
            
            // Record failure for blacklist tracking
            var failureReason = MapToHostFailureReason(lastResult.FailureReason);
            RecordHostFailure(currentLink.HostName, failureReason, lastResult.ErrorMessage);
            
            _logger?.LogWarning("Download attempt failed for {Host}: {Reason}", 
                currentLink.HostName ?? "direct", lastResult.FailureReason);
        }
        
        // Record history
        if (lastResult != null)
        {
            RecordHistory(candidate, lastResult);
            
            // Log final result
            if (lastResult.Success)
            {
                _logger?.LogInformation("Download completed: {Title}, Size: {Size:N0} bytes, Duration: {Duration}", 
                    candidate.ReleaseTitle, lastResult.FileSize, lastResult.Duration);
            }
            else
            {
                _logger?.LogWarning("Download failed: {Title}, Reason: {Reason}, Error: {Error}", 
                    candidate.ReleaseTitle, lastResult.FailureReason, lastResult.ErrorMessage);
            }
            
            return lastResult;
        }
        
        return DdlDownloadResult.Failed(
            Guid.NewGuid().ToString(),
            DdlDownloadFailureReason.NoValidLinks,
            "All download attempts failed"
        );
    }
    
    /// <summary>
    /// Resolves a hosted link to a direct download URL using the appropriate resolver.
    /// </summary>
    private async Task<HostResolverResult> ResolveHostedLinkAsync(DdlDownloadLink link, DdlDownloadOptions options, CancellationToken cancellationToken)
    {
        if (_resolverFactory == null)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.NotSupported, "No resolver factory available");
        }
        
        var resolver = _resolverFactory.GetResolver(link.Url);
        if (resolver == null)
        {
            _logger?.LogDebug("No resolver found for URL: {Url}", link.Url);
            // If no resolver but URL looks like a direct link, treat it as resolvable
            return HostResolverResult.Succeeded(link.Url);
        }
        
        _logger?.LogDebug("Using {Resolver} to resolve {Url}", resolver.DisplayName, link.Url);
        
        var resolverOptions = new HostResolverOptions
        {
            TimeoutSeconds = Math.Min(options.TimeoutSeconds, 60),
            UserAgent = options.UserAgent
        };
        
        return await resolver.ResolveAsync(link.Url, resolverOptions, cancellationToken);
    }
    
    /// <summary>
    /// Sanitizes a filename for safe filesystem use.
    /// </summary>
    private static string SanitizeFilename(string filename)
    {
        var sanitized = filename;
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized;
    }

    public async Task<DdlDownloadResult> DownloadUrlAsync(string url, string destinationPath, DdlDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DdlDownloadOptions();
        return await DownloadWithRetriesAsync(url, destinationPath, options, cancellationToken);
    }

    public DdlDownloadStatus? GetDownloadStatus(string downloadId)
    {
        return _activeDownloads.TryGetValue(downloadId, out var status) ? status : null;
    }

    public bool CancelDownload(string downloadId)
    {
        if (_cancellationTokens.TryRemove(downloadId, out var cts))
        {
            cts.Cancel();
            
            if (_activeDownloads.TryGetValue(downloadId, out var status))
            {
                status.State = DdlDownloadState.Cancelled;
            }
            
            return true;
        }
        
        return false;
    }

    public IReadOnlyList<DdlDownloadStatus> GetActiveDownloads()
    {
        return _activeDownloads.Values
            .Where(d => d.State is DdlDownloadState.Queued or DdlDownloadState.Connecting or DdlDownloadState.Downloading or DdlDownloadState.Retrying)
            .ToList();
    }

    public IReadOnlyList<DdlDownloadHistoryEntry> GetDownloadHistory(int limit = 50)
    {
        lock (_historyLock)
        {
            return _downloadHistory
                .OrderByDescending(h => h.CompletedAt)
                .Take(limit)
                .ToList();
        }
    }

    public async Task<bool> CanResumeAsync(string url, string destinationPath)
    {
        // Check if partial file exists
        var partialPath = destinationPath + ".partial";
        if (!File.Exists(partialPath))
        {
            return false;
        }
        
        // Check if server supports range requests
        try
        {
            using var client = CreateHttpClient(new DdlDownloadOptions());
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request);
            
            return response.Headers.AcceptRanges.Contains("bytes");
        }
        catch
        {
            return false;
        }
    }

    private async Task<DdlDownloadResult> DownloadWithRetriesAsync(string url, string destinationPath, DdlDownloadOptions options, CancellationToken cancellationToken)
    {
        var downloadId = Guid.NewGuid().ToString();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[downloadId] = cts;
        
        var status = new DdlDownloadStatus
        {
            DownloadId = downloadId,
            SourceUrl = url,
            DestinationPath = destinationPath,
            State = DdlDownloadState.Queued,
            StartedAt = DateTime.UtcNow
        };
        _activeDownloads[downloadId] = status;
        
        var stopwatch = Stopwatch.StartNew();
        var retryAttempt = 0;
        DdlDownloadResult? lastResult = null;
        
        try
        {
            while (retryAttempt <= options.MaxRetries)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return DdlDownloadResult.Failed(downloadId, DdlDownloadFailureReason.Cancelled, "Download cancelled", retryAttempt, sourceUrl: url);
                }
                
                status.CurrentRetry = retryAttempt;
                status.State = retryAttempt > 0 ? DdlDownloadState.Retrying : DdlDownloadState.Connecting;
                
                try
                {
                    lastResult = await DownloadCoreAsync(downloadId, url, destinationPath, options, status, cts.Token);
                    
                    if (lastResult.Success)
                    {
                        status.State = DdlDownloadState.Completed;
                        return lastResult;
                    }
                    
                    // Don't retry for certain failure types
                    if (!ShouldRetry(lastResult.FailureReason))
                    {
                        status.State = DdlDownloadState.Failed;
                        status.LastError = lastResult.ErrorMessage;
                        return lastResult;
                    }
                }
                catch (OperationCanceledException)
                {
                    status.State = DdlDownloadState.Cancelled;
                    return DdlDownloadResult.Failed(downloadId, DdlDownloadFailureReason.Cancelled, "Download cancelled", retryAttempt, sourceUrl: url);
                }
                catch (Exception ex)
                {
                    status.LastError = ex.Message;
                    lastResult = DdlDownloadResult.Failed(downloadId, ClassifyException(ex), ex.Message, retryAttempt, sourceUrl: url);
                    
                    if (!ShouldRetry(lastResult.FailureReason))
                    {
                        status.State = DdlDownloadState.Failed;
                        return lastResult;
                    }
                }
                
                retryAttempt++;
                
                if (retryAttempt <= options.MaxRetries)
                {
                    // Exponential backoff
                    var delay = CalculateBackoff(retryAttempt, options.RetryDelayMs, options.MaxRetryDelayMs);
                    _logger?.LogInformation("Retry {Attempt}/{Max} for {Url} in {Delay}ms", retryAttempt, options.MaxRetries, url, delay);
                    
                    await Task.Delay(delay, cts.Token);
                }
            }
            
            // Max retries exceeded
            status.State = DdlDownloadState.Failed;
            return DdlDownloadResult.Failed(
                downloadId,
                DdlDownloadFailureReason.MaxRetriesExceeded,
                $"Download failed after {options.MaxRetries} retries: {lastResult?.ErrorMessage}",
                retryAttempt,
                lastResult?.HttpStatusCode,
                url
            );
        }
        finally
        {
            _cancellationTokens.TryRemove(downloadId, out _);
            stopwatch.Stop();
        }
    }

    private async Task<DdlDownloadResult> DownloadCoreAsync(string downloadId, string url, string destinationPath, DdlDownloadOptions options, DdlDownloadStatus status, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var partialPath = destinationPath + ".partial";
        long startPosition = 0;
        var wasResumed = false;
        
        // Check for resume
        if (options.EnableResume && File.Exists(partialPath))
        {
            var fileInfo = new FileInfo(partialPath);
            startPosition = fileInfo.Length;
            wasResumed = startPosition > 0;
            _logger?.LogInformation("Resuming download from byte {Position}", startPosition);
        }
        
        using var client = CreateHttpClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // Add range header for resume
        if (startPosition > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startPosition, null);
        }
        
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        // Handle response status
        if (!response.IsSuccessStatusCode)
        {
            var failureReason = ClassifyHttpStatus(response.StatusCode);
            return DdlDownloadResult.Failed(
                downloadId,
                failureReason,
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                status.CurrentRetry,
                (int)response.StatusCode,
                url
            );
        }
        
        // Get content length
        var totalBytes = response.Content.Headers.ContentLength;
        if (startPosition > 0 && response.StatusCode == HttpStatusCode.PartialContent)
        {
            // Add the already downloaded bytes to total
            totalBytes = (totalBytes ?? 0) + startPosition;
        }
        else if (startPosition > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            // Server doesn't support range requests, start over
            startPosition = 0;
            wasResumed = false;
        }
        
        status.TotalBytes = totalBytes;
        status.BytesDownloaded = startPosition;
        status.State = DdlDownloadState.Downloading;
        
        // Download to partial file
        var directory = Path.GetDirectoryName(partialPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var bytesRead = 0L;
        
        // Use explicit scope for file stream so it's closed before verification
        {
            var fileMode = startPosition > 0 ? FileMode.Append : FileMode.Create;
            await using var fileStream = new FileStream(partialPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, true);
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            
            var buffer = new byte[BufferSize];
            var lastProgressReport = DateTime.UtcNow;
            var lastBytesForSpeed = startPosition;
            var lastTimeForSpeed = stopwatch.Elapsed;
            
            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;
                status.BytesDownloaded = startPosition + bytesRead;
                
                // Calculate speed
                var elapsed = stopwatch.Elapsed;
                var timeDiff = (elapsed - lastTimeForSpeed).TotalSeconds;
                if (timeDiff >= 1)
                {
                    var bytesDiff = status.BytesDownloaded - lastBytesForSpeed;
                    status.BytesPerSecond = bytesDiff / timeDiff;
                    lastBytesForSpeed = status.BytesDownloaded;
                    lastTimeForSpeed = elapsed;
                }
                
                // Report progress
                if (options.OnProgress != null && (DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 500)
                {
                    var progress = new DdlDownloadProgress
                    {
                        DownloadId = downloadId,
                        BytesDownloaded = status.BytesDownloaded,
                        TotalBytes = totalBytes,
                        ProgressPercent = status.ProgressPercent,
                        BytesPerSecond = status.BytesPerSecond,
                        EstimatedTimeRemaining = CalculateEta(status.BytesDownloaded, totalBytes, status.BytesPerSecond)
                    };
                    
                    options.OnProgress(progress);
                    lastProgressReport = DateTime.UtcNow;
                }
            }
            
            await fileStream.FlushAsync(cancellationToken);
            // fileStream is disposed here when exiting scope
        }
        
        stopwatch.Stop();
        
        var finalSize = startPosition + bytesRead;
        
        // Verify download (file stream is now closed)
        if (options.VerifyDownload)
        {
            status.State = DdlDownloadState.Verifying;
            
            var verifyResult = await VerifyDownloadAsync(partialPath, finalSize, options);
            if (!verifyResult.Success)
            {
                // Delete partial file on verification failure
                try { File.Delete(partialPath); } catch { }
                
                return DdlDownloadResult.Failed(
                    downloadId,
                    verifyResult.FailureReason,
                    verifyResult.ErrorMessage ?? "Verification failed",
                    status.CurrentRetry,
                    sourceUrl: url
                );
            }
        }
        
        // Move partial file to final destination
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }
        File.Move(partialPath, destinationPath);
        
        var filename = Path.GetFileName(destinationPath);
        
        return DdlDownloadResult.Succeeded(
            downloadId,
            destinationPath,
            filename,
            finalSize,
            stopwatch.Elapsed,
            status.CurrentRetry,
            wasResumed,
            url
        );
    }

    private async Task<(bool Success, DdlDownloadFailureReason FailureReason, string? ErrorMessage)> VerifyDownloadAsync(string filePath, long fileSize, DdlDownloadOptions options)
    {
        // Check file size
        if (fileSize == 0)
        {
            return (false, DdlDownloadFailureReason.EmptyFile, "Downloaded file is empty");
        }
        
        if (options.MinExpectedSize.HasValue && fileSize < options.MinExpectedSize.Value)
        {
            return (false, DdlDownloadFailureReason.FileTooSmall, $"File size {fileSize} is below minimum {options.MinExpectedSize}");
        }
        
        if (options.MaxExpectedSize.HasValue && fileSize > options.MaxExpectedSize.Value)
        {
            return (false, DdlDownloadFailureReason.FileTooLarge, $"File size {fileSize} exceeds maximum {options.MaxExpectedSize}");
        }
        
        // Check magic bytes (need at least 16 bytes to detect HTML doctype)
        try
        {
            var magicBytes = new byte[16];
            await using var fs = File.OpenRead(filePath);
            await fs.ReadAsync(magicBytes.AsMemory(0, Math.Min(16, (int)fileSize)));
            
            // Check if it's an HTML error page
            var isHtml = IsHtmlContent(magicBytes);
            if (isHtml)
            {
                return (false, DdlDownloadFailureReason.HtmlErrorPage, "Downloaded file appears to be an HTML error page");
            }
            
            // Check for valid archive formats
            var isValidFormat = IsValidComicFormat(magicBytes);
            if (!isValidFormat && fileSize > 1000) // Only check for larger files
            {
                _logger?.LogWarning("Downloaded file may not be a valid comic archive (magic bytes: {Bytes})", BitConverter.ToString(magicBytes.Take(4).ToArray()));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to verify file magic bytes");
        }
        
        return (true, DdlDownloadFailureReason.None, null);
    }

    private static bool IsHtmlContent(byte[] bytes)
    {
        // Check for common HTML starts
        var start = System.Text.Encoding.ASCII.GetString(bytes).ToLowerInvariant();
        return start.StartsWith("<!doctype") || start.StartsWith("<html") || start.StartsWith("<head") || start.StartsWith("<?xml");
    }

    private static bool IsValidComicFormat(byte[] bytes)
    {
        return StartsWithMagic(bytes, ZipMagic) ||  // ZIP/CBZ
               StartsWithMagic(bytes, RarMagic) ||  // RAR/CBR
               StartsWithMagic(bytes, PdfMagic) ||  // PDF
               StartsWithMagic(bytes, SevenZipMagic); // 7z/CB7
    }

    private static bool StartsWithMagic(byte[] bytes, byte[] magic)
    {
        if (bytes.Length < magic.Length)
        {
            return false;
        }
        
        for (int i = 0; i < magic.Length; i++)
        {
            if (bytes[i] != magic[i])
            {
                return false;
            }
        }
        
        return true;
    }

    private HttpClient CreateHttpClient(DdlDownloadOptions options)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        
        // Add cookies
        if (options.Cookies.Count > 0)
        {
            handler.CookieContainer = new CookieContainer();
            // Note: Cookie domain would need to be extracted from URL in a real implementation
        }
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 300)
        };
        
        // Set User-Agent
        var userAgent = options.UserAgent ?? DefaultUserAgent;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        
        // Add custom headers
        foreach (var (key, value) in options.CustomHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }
        
        return client;
    }

    private static string DeriveFilename(DdlCandidate candidate, string url)
    {
        // Try to get filename from URL
        try
        {
            var uri = new Uri(url);
            var urlFilename = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrEmpty(urlFilename) && urlFilename.Contains('.'))
            {
                return urlFilename;
            }
        }
        catch { }
        
        // Fall back to release title
        var title = candidate.ReleaseTitle;
        
        // Sanitize filename
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            title = title.Replace(c, '_');
        }
        
        // Ensure it has an extension
        if (!title.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase) &&
            !title.EndsWith(".cbr", StringComparison.OrdinalIgnoreCase))
        {
            title += ".cbz";
        }
        
        return title;
    }

    private static bool ShouldRetry(DdlDownloadFailureReason reason)
    {
        return reason switch
        {
            DdlDownloadFailureReason.Timeout => true,
            DdlDownloadFailureReason.ConnectionFailed => true,
            DdlDownloadFailureReason.DnsFailure => true,
            DdlDownloadFailureReason.ServerError => true,
            DdlDownloadFailureReason.RateLimited => true,
            _ => false
        };
    }

    private static DdlDownloadFailureReason ClassifyHttpStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.NotFound => DdlDownloadFailureReason.NotFound,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => DdlDownloadFailureReason.Unauthorized,
            HttpStatusCode.TooManyRequests => DdlDownloadFailureReason.RateLimited,
            >= HttpStatusCode.InternalServerError => DdlDownloadFailureReason.ServerError,
            _ => DdlDownloadFailureReason.HttpError
        };
    }

    private static DdlDownloadFailureReason ClassifyException(Exception ex)
    {
        return ex switch
        {
            TaskCanceledException or OperationCanceledException => DdlDownloadFailureReason.Timeout,
            HttpRequestException httpEx when httpEx.Message.Contains("Name or service not known") => DdlDownloadFailureReason.DnsFailure,
            HttpRequestException => DdlDownloadFailureReason.ConnectionFailed,
            IOException => DdlDownloadFailureReason.DiskError,
            _ => DdlDownloadFailureReason.Unknown
        };
    }

    private static int CalculateBackoff(int attempt, int baseDelayMs, int maxDelayMs)
    {
        // Exponential backoff with jitter
        var delay = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
        delay = Math.Min(delay, maxDelayMs);
        
        // Add jitter (±25%)
        var jitter = Random.Shared.Next(-delay / 4, delay / 4);
        return delay + jitter;
    }

    private static TimeSpan? CalculateEta(long bytesDownloaded, long? totalBytes, double bytesPerSecond)
    {
        if (!totalBytes.HasValue || bytesPerSecond <= 0)
        {
            return null;
        }
        
        var remaining = totalBytes.Value - bytesDownloaded;
        if (remaining <= 0)
        {
            return TimeSpan.Zero;
        }
        
        return TimeSpan.FromSeconds(remaining / bytesPerSecond);
    }

    private void RecordHistory(DdlCandidate candidate, DdlDownloadResult result)
    {
        var startedAt = DateTime.UtcNow - result.Duration;
        var completedAt = DateTime.UtcNow;
        
        var entry = new DdlDownloadHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            DownloadId = result.DownloadId,
            SourceUrl = result.SourceUrl ?? candidate.DownloadLinks.FirstOrDefault()?.Url ?? "",
            SourceSite = candidate.SourceSite,
            ReleaseTitle = candidate.ReleaseTitle,
            DestinationPath = result.FilePath,
            FileSize = result.FileSize,
            Success = result.Success,
            FailureReason = result.Success ? null : result.FailureReason,
            ErrorMessage = result.ErrorMessage,
            RetryAttempts = result.RetryAttempts,
            Duration = result.Duration,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
        
        lock (_historyLock)
        {
            _downloadHistory.Add(entry);
            
            // Keep only last 1000 entries
            while (_downloadHistory.Count > 1000)
            {
                _downloadHistory.RemoveAt(0);
            }
        }
        
        // Also add to unified activity service for Activity page visibility
        _activityService?.AddToHistory(new DownloadActivity
        {
            Id = result.DownloadId,
            SourceType = DownloadSourceType.Ddl,
            ClientName = "DDL",
            Title = candidate.ReleaseTitle,
            State = result.Success ? ActivityState.Completed : ActivityState.Failed,
            Progress = result.Success ? 100 : 0,
            TotalBytes = result.FileSize > 0 ? result.FileSize : null,
            DownloadedBytes = result.FileSize,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ErrorMessage = result.ErrorMessage,
            RetryCount = result.RetryAttempts,
            OutputPath = result.FilePath,
            SourceUrl = result.SourceUrl ?? candidate.DownloadLinks.FirstOrDefault()?.Url,
            Category = candidate.SourceSite
        });
    }

    private bool IsLinkBlacklisted(DdlDownloadLink link)
    {
        if (_blacklistService == null)
            return false;

        // Check by host name
        if (!string.IsNullOrEmpty(link.HostName) && _blacklistService.IsBlacklisted(link.HostName))
        {
            _logger?.LogDebug("Link skipped - host {Host} is blacklisted", link.HostName);
            return true;
        }

        // Check by URL
        if (_blacklistService.IsUrlBlacklisted(link.Url))
        {
            _logger?.LogDebug("Link skipped - URL host is blacklisted: {Url}", link.Url);
            return true;
        }

        return false;
    }

    private void RecordHostSuccess(string? hostName)
    {
        if (_blacklistService == null || string.IsNullOrEmpty(hostName))
            return;

        _blacklistService.RecordSuccess(hostName);
    }

    private void RecordHostFailure(string? hostName, HostResolverFailureReason reason, string? errorMessage)
    {
        if (_blacklistService == null || string.IsNullOrEmpty(hostName))
            return;

        _blacklistService.RecordFailure(hostName, reason, errorMessage);
    }

    private static HostResolverFailureReason MapToHostFailureReason(DdlDownloadFailureReason reason)
    {
        return reason switch
        {
            DdlDownloadFailureReason.NotFound => HostResolverFailureReason.FileNotFound,
            DdlDownloadFailureReason.Unauthorized => HostResolverFailureReason.AuthenticationRequired,
            DdlDownloadFailureReason.RateLimited => HostResolverFailureReason.RateLimited,
            DdlDownloadFailureReason.Timeout => HostResolverFailureReason.Timeout,
            DdlDownloadFailureReason.ConnectionFailed or DdlDownloadFailureReason.DnsFailure => HostResolverFailureReason.NetworkError,
            DdlDownloadFailureReason.ServerError => HostResolverFailureReason.HostUnavailable,
            DdlDownloadFailureReason.LinkResolutionFailed => HostResolverFailureReason.ParseError,
            _ => HostResolverFailureReason.Unknown
        };
    }
}



