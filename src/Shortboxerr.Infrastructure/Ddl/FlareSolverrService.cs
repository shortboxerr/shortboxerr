using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Cloudflare bypass service using FlareSolverr.
/// FlareSolverr is a proxy server that solves Cloudflare's JavaScript challenges using a real browser.
/// </summary>
public class FlareSolverrService : ICloudflareBypassService
{
    private const string SettingsKey = "CloudflareBypass";
    private const string ApiEndpoint = "/v1";

    private readonly ILogger<FlareSolverrService>? _logger;
    private readonly ISettingsService _settingsService;
    private readonly ConcurrentDictionary<string, CloudflareCookieSession> _sessionCache = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly HttpClient _httpClient;

    private CloudflareBypassSettings? _cachedSettings;

    public FlareSolverrService(
        ISettingsService settingsService,
        ILogger<FlareSolverrService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(2); // Default max concurrent

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120) // Allow long timeouts for challenge solving
        };
    }

    public async Task<CloudflareBypassTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var settings = await GetSettingsAsync(cancellationToken);

            if (!settings.Enabled)
            {
                return new CloudflareBypassTestResult
                {
                    IsAvailable = false,
                    ErrorMessage = "Cloudflare bypass is disabled in settings"
                };
            }

            var requestUrl = $"{settings.ServerUrl.TrimEnd('/')}{ApiEndpoint}";

            var request = new FlareSolverrRequest
            {
                Cmd = "sessions.list"
            };

            var response = await SendRequestAsync<FlareSolverrResponse>(requestUrl, request, cancellationToken);

            stopwatch.Stop();

            if (response?.Status == "ok")
            {
                return new CloudflareBypassTestResult
                {
                    IsAvailable = true,
                    Version = response.Version,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            return new CloudflareBypassTestResult
            {
                IsAvailable = false,
                ErrorMessage = response?.Message ?? "Unknown error",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger?.LogWarning(ex, "Failed to connect to FlareSolverr");
            return new CloudflareBypassTestResult
            {
                IsAvailable = false,
                ErrorMessage = $"Connection failed: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Unexpected error testing FlareSolverr connection");
            return new CloudflareBypassTestResult
            {
                IsAvailable = false,
                ErrorMessage = ex.Message,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<CloudflareBypassResult> BypassAsync(
        string url,
        CloudflareBypassOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CloudflareBypassOptions();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var settings = await GetSettingsAsync(cancellationToken);

            if (!settings.Enabled)
            {
                return CloudflareBypassResult.Failed(
                    CloudflareBypassFailureReason.Disabled,
                    "Cloudflare bypass is disabled in settings"
                );
            }

            // Validate URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return CloudflareBypassResult.Failed(
                    CloudflareBypassFailureReason.InvalidUrl,
                    "Invalid URL format"
                );
            }

            // Check cache first
            var cachedSession = await GetCachedSessionAsync(uri.Host, cancellationToken);
            if (cachedSession != null && !cachedSession.IsExpired)
            {
                _logger?.LogDebug("Using cached Cloudflare session for {Domain}", uri.Host);
                return CloudflareBypassResult.Succeeded(
                    cachedSession,
                    cachedSession.UserAgent
                ) with { Duration = stopwatch.Elapsed };
            }

            // Acquire semaphore for concurrency limiting
            if (!await _concurrencyLimiter.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
            {
                return CloudflareBypassResult.Failed(
                    CloudflareBypassFailureReason.TooManyRequests,
                    "Too many concurrent bypass requests"
                );
            }

            try
            {
                return await ExecuteBypassWithRetryAsync(url, options, settings, stopwatch, cancellationToken);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }
        catch (TaskCanceledException)
        {
            return CloudflareBypassResult.Failed(
                CloudflareBypassFailureReason.Timeout,
                "Request was cancelled"
            ) with { Duration = stopwatch.Elapsed };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during Cloudflare bypass for {Url}", url);
            return CloudflareBypassResult.Failed(
                CloudflareBypassFailureReason.Unknown,
                ex.Message
            ) with { Duration = stopwatch.Elapsed };
        }
    }

    private async Task<CloudflareBypassResult> ExecuteBypassWithRetryAsync(
        string url,
        CloudflareBypassOptions options,
        CloudflareBypassSettings settings,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var maxRetries = settings.AutoRetry ? settings.MaxRetries : 1;
        CloudflareBypassResult? lastResult = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            _logger?.LogDebug("Cloudflare bypass attempt {Attempt}/{MaxRetries} for {Url}", attempt, maxRetries, url);

            lastResult = await ExecuteBypassAsync(url, options, settings, cancellationToken);

            if (lastResult.Success)
            {
                lastResult = lastResult with { Duration = stopwatch.Elapsed };

                // Cache the session
                if (lastResult.Session != null)
                {
                    _sessionCache[lastResult.Session.Domain] = lastResult.Session;
                }

                return lastResult;
            }

            // Don't retry certain failures
            if (lastResult.FailureReason is CloudflareBypassFailureReason.CaptchaRequired
                or CloudflareBypassFailureReason.InvalidUrl
                or CloudflareBypassFailureReason.Disabled
                or CloudflareBypassFailureReason.ServiceUnavailable)
            {
                break;
            }

            // Wait before retry
            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }

        return lastResult ?? CloudflareBypassResult.Failed(
            CloudflareBypassFailureReason.Unknown,
            "Bypass failed after all retries"
        ) with { Duration = stopwatch.Elapsed };
    }

    private async Task<CloudflareBypassResult> ExecuteBypassAsync(
        string url,
        CloudflareBypassOptions options,
        CloudflareBypassSettings settings,
        CancellationToken cancellationToken)
    {
        var requestUrl = $"{settings.ServerUrl.TrimEnd('/')}{ApiEndpoint}";

        var request = new FlareSolverrRequest
        {
            Cmd = "request.get",
            Url = url,
            MaxTimeout = (int)options.Timeout.TotalMilliseconds
        };

        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            // FlareSolverr doesn't directly support custom user agent in request
            // but we track it for session reuse
        }

        if (options.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(options.PostData))
        {
            request.Cmd = "request.post";
            request.PostData = options.PostData;
        }

        try
        {
            var response = await SendRequestAsync<FlareSolverrResponse>(requestUrl, request, cancellationToken);

            if (response == null)
            {
                return CloudflareBypassResult.Failed(
                    CloudflareBypassFailureReason.InvalidResponse,
                    "No response from FlareSolverr"
                );
            }

            if (response.Status != "ok")
            {
                var failureReason = ClassifyFlareSolverrError(response.Message);
                return CloudflareBypassResult.Failed(failureReason, response.Message ?? "Unknown error");
            }

            if (response.Solution == null)
            {
                return CloudflareBypassResult.Failed(
                    CloudflareBypassFailureReason.InvalidResponse,
                    "No solution in FlareSolverr response"
                );
            }

            // Build session from response
            var uri = new Uri(url);
            var session = new CloudflareCookieSession
            {
                Domain = uri.Host,
                UserAgent = response.Solution.UserAgent ?? "",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(settings.SessionCacheMinutes)
            };

            // Extract cookies
            if (response.Solution.Cookies != null)
            {
                foreach (var cookie in response.Solution.Cookies)
                {
                    if (!string.IsNullOrEmpty(cookie.Name) && !string.IsNullOrEmpty(cookie.Value))
                    {
                        session.Cookies[cookie.Name] = cookie.Value;
                    }
                }
            }

            _logger?.LogInformation("Cloudflare bypass successful for {Domain} with {CookieCount} cookies",
                uri.Host, session.Cookies.Count);

            return CloudflareBypassResult.Succeeded(
                session,
                response.Solution.UserAgent,
                options.ReturnHtmlContent ? response.Solution.Response : null,
                response.Solution.Url
            );
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "HTTP error during Cloudflare bypass");
            return CloudflareBypassResult.Failed(
                CloudflareBypassFailureReason.ConnectionFailed,
                ex.Message
            );
        }
        catch (TaskCanceledException)
        {
            return CloudflareBypassResult.Failed(
                CloudflareBypassFailureReason.Timeout,
                "Request timed out"
            );
        }
    }

    public Task<CloudflareCookieSession?> GetCachedSessionAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (_sessionCache.TryGetValue(domain, out var session) && !session.IsExpired)
        {
            return Task.FromResult<CloudflareCookieSession?>(session);
        }

        // Remove expired session
        if (session != null && session.IsExpired)
        {
            _sessionCache.TryRemove(domain, out _);
        }

        return Task.FromResult<CloudflareCookieSession?>(null);
    }

    public Task ClearSessionAsync(string domain, CancellationToken cancellationToken = default)
    {
        _sessionCache.TryRemove(domain, out _);
        _logger?.LogDebug("Cleared Cloudflare session cache for {Domain}", domain);
        return Task.CompletedTask;
    }

    public async Task<CloudflareBypassSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        var json = await _settingsService.GetAsync(SettingsKey, cancellationToken);

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _cachedSettings = JsonSerializer.Deserialize<CloudflareBypassSettings>(json);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Failed to deserialize Cloudflare bypass settings");
            }
        }

        _cachedSettings ??= new CloudflareBypassSettings();
        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(CloudflareBypassSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings);
        await _settingsService.SetAsync(SettingsKey, json, cancellationToken);
        _cachedSettings = settings;

        // Update concurrency limiter if needed
        // Note: This is simplified; a full implementation would recreate the semaphore
        _logger?.LogInformation("Cloudflare bypass settings saved");
    }

    private async Task<T?> SendRequestAsync<T>(string url, object request, CancellationToken cancellationToken)
        where T : class
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(responseJson, jsonOptions);
    }

    private static CloudflareBypassFailureReason ClassifyFlareSolverrError(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return CloudflareBypassFailureReason.Unknown;
        }

        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("captcha"))
        {
            return CloudflareBypassFailureReason.CaptchaRequired;
        }

        if (lowerMessage.Contains("timeout") || lowerMessage.Contains("timed out"))
        {
            return CloudflareBypassFailureReason.Timeout;
        }

        if (lowerMessage.Contains("challenge"))
        {
            return CloudflareBypassFailureReason.ChallengeFailed;
        }

        if (lowerMessage.Contains("connection") || lowerMessage.Contains("connect"))
        {
            return CloudflareBypassFailureReason.ConnectionFailed;
        }

        return CloudflareBypassFailureReason.Unknown;
    }

    #region FlareSolverr API Models

    private class FlareSolverrRequest
    {
        [JsonPropertyName("cmd")]
        public string Cmd { get; set; } = "";

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("maxTimeout")]
        public int? MaxTimeout { get; set; }

        [JsonPropertyName("postData")]
        public string? PostData { get; set; }

        [JsonPropertyName("session")]
        public string? Session { get; set; }

        [JsonPropertyName("session_ttl_minutes")]
        public int? SessionTtlMinutes { get; set; }
    }

    private class FlareSolverrResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("solution")]
        public FlareSolverrSolution? Solution { get; set; }

        [JsonPropertyName("startTimestamp")]
        public long? StartTimestamp { get; set; }

        [JsonPropertyName("endTimestamp")]
        public long? EndTimestamp { get; set; }
    }

    private class FlareSolverrSolution
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("cookies")]
        public List<FlareSolverrCookie>? Cookies { get; set; }

        [JsonPropertyName("userAgent")]
        public string? UserAgent { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }

    private class FlareSolverrCookie
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("expires")]
        public double? Expires { get; set; }

        [JsonPropertyName("httpOnly")]
        public bool HttpOnly { get; set; }

        [JsonPropertyName("secure")]
        public bool Secure { get; set; }

        [JsonPropertyName("sameSite")]
        public string? SameSite { get; set; }
    }

    #endregion
}
