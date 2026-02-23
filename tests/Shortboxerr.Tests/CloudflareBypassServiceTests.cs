using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for the Cloudflare bypass service and FlareSolverr integration.
/// </summary>
public class CloudflareBypassServiceTests
{
    #region Settings Tests

    [Fact]
    public void CloudflareBypassSettings_HasCorrectDefaults()
    {
        var settings = new CloudflareBypassSettings();

        Assert.False(settings.Enabled);
        Assert.Equal("http://localhost:8191", settings.ServerUrl);
        Assert.Equal(60, settings.DefaultTimeoutSeconds);
        Assert.Equal(120, settings.SessionCacheMinutes);
        Assert.Equal(2, settings.MaxConcurrentSessions);
        Assert.True(settings.AutoRetry);
        Assert.Equal(3, settings.MaxRetries);
    }

    [Fact]
    public void CloudflareBypassSettings_CanBeCustomized()
    {
        var settings = new CloudflareBypassSettings
        {
            Enabled = true,
            ServerUrl = "http://flaresolverr:8191",
            DefaultTimeoutSeconds = 90,
            SessionCacheMinutes = 60,
            MaxConcurrentSessions = 4,
            AutoRetry = false,
            MaxRetries = 5
        };

        Assert.True(settings.Enabled);
        Assert.Equal("http://flaresolverr:8191", settings.ServerUrl);
        Assert.Equal(90, settings.DefaultTimeoutSeconds);
        Assert.Equal(60, settings.SessionCacheMinutes);
        Assert.Equal(4, settings.MaxConcurrentSessions);
        Assert.False(settings.AutoRetry);
        Assert.Equal(5, settings.MaxRetries);
    }

    #endregion

    #region Options Tests

    [Fact]
    public void CloudflareBypassOptions_HasCorrectDefaults()
    {
        var options = new CloudflareBypassOptions();

        Assert.Equal(TimeSpan.FromSeconds(60), options.Timeout);
        Assert.False(options.ReturnHtmlContent);
        Assert.Null(options.UserAgent);
        Assert.Equal("GET", options.HttpMethod);
        Assert.Null(options.PostData);
        Assert.Empty(options.Headers);
        Assert.Equal(5, options.MaxRedirects);
    }

    [Fact]
    public void CloudflareBypassOptions_CanBeCustomized()
    {
        var options = new CloudflareBypassOptions
        {
            Timeout = TimeSpan.FromSeconds(120),
            ReturnHtmlContent = true,
            UserAgent = "Custom Agent",
            HttpMethod = "POST",
            PostData = "key=value",
            MaxRedirects = 10
        };
        options.Headers["X-Custom"] = "test";

        Assert.Equal(TimeSpan.FromSeconds(120), options.Timeout);
        Assert.True(options.ReturnHtmlContent);
        Assert.Equal("Custom Agent", options.UserAgent);
        Assert.Equal("POST", options.HttpMethod);
        Assert.Equal("key=value", options.PostData);
        Assert.Equal(10, options.MaxRedirects);
        Assert.Contains("X-Custom", options.Headers.Keys);
    }

    #endregion

    #region Cookie Session Tests

    [Fact]
    public void CloudflareCookieSession_HasCorrectDefaults()
    {
        var session = new CloudflareCookieSession();

        Assert.Equal("", session.Domain);
        Assert.Empty(session.Cookies);
        Assert.Equal("", session.UserAgent);
        Assert.False(session.IsExpired);
        Assert.True(session.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CloudflareCookieSession_CanStoreCookies()
    {
        var session = new CloudflareCookieSession
        {
            Domain = "example.com",
            UserAgent = "Mozilla/5.0",
            Cookies = new Dictionary<string, string>
            {
                ["cf_clearance"] = "abc123",
                ["__cf_bm"] = "xyz789"
            }
        };

        Assert.Equal("example.com", session.Domain);
        Assert.Equal("Mozilla/5.0", session.UserAgent);
        Assert.Equal(2, session.Cookies.Count);
        Assert.Equal("abc123", session.CfClearance);
    }

    [Fact]
    public void CloudflareCookieSession_CfClearance_ReturnsNullWhenMissing()
    {
        var session = new CloudflareCookieSession
        {
            Cookies = new Dictionary<string, string>
            {
                ["other_cookie"] = "value"
            }
        };

        Assert.Null(session.CfClearance);
    }

    [Fact]
    public void CloudflareCookieSession_IsExpired_ReturnsTrueWhenPastExpiry()
    {
        var session = new CloudflareCookieSession
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        Assert.True(session.IsExpired);
    }

    [Fact]
    public void CloudflareCookieSession_IsExpired_ReturnsFalseWhenNotExpired()
    {
        var session = new CloudflareCookieSession
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        Assert.False(session.IsExpired);
    }

    #endregion

    #region Result Tests

    [Fact]
    public void CloudflareBypassResult_Succeeded_CreatesCorrectResult()
    {
        var session = new CloudflareCookieSession
        {
            Domain = "test.com",
            Cookies = new Dictionary<string, string> { ["cf_clearance"] = "token" }
        };

        var result = CloudflareBypassResult.Succeeded(session, "Mozilla/5.0", "<html>", "https://test.com/page");

        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.Equal("test.com", result.Session.Domain);
        Assert.Equal("Mozilla/5.0", result.UserAgent);
        Assert.Equal("<html>", result.HtmlContent);
        Assert.Equal("https://test.com/page", result.FinalUrl);
        Assert.Equal(CloudflareBypassFailureReason.None, result.FailureReason);
    }

    [Fact]
    public void CloudflareBypassResult_Failed_CreatesCorrectResult()
    {
        var result = CloudflareBypassResult.Failed(
            CloudflareBypassFailureReason.ChallengeFailed,
            "Could not solve challenge"
        );

        Assert.False(result.Success);
        Assert.Null(result.Session);
        Assert.Equal(CloudflareBypassFailureReason.ChallengeFailed, result.FailureReason);
        Assert.Equal("Could not solve challenge", result.ErrorMessage);
    }

    [Fact]
    public void CloudflareBypassResult_CanSetDuration()
    {
        var result = new CloudflareBypassResult
        {
            Success = true,
            Duration = TimeSpan.FromSeconds(5)
        };

        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
    }

    #endregion

    #region Test Result Tests

    [Fact]
    public void CloudflareBypassTestResult_Available_HasCorrectProperties()
    {
        var result = new CloudflareBypassTestResult
        {
            IsAvailable = true,
            Version = "3.3.10",
            ResponseTimeMs = 150
        };

        Assert.True(result.IsAvailable);
        Assert.Equal("3.3.10", result.Version);
        Assert.Equal(150, result.ResponseTimeMs);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void CloudflareBypassTestResult_Unavailable_HasCorrectProperties()
    {
        var result = new CloudflareBypassTestResult
        {
            IsAvailable = false,
            ErrorMessage = "Connection refused",
            ResponseTimeMs = 30
        };

        Assert.False(result.IsAvailable);
        Assert.Null(result.Version);
        Assert.Equal("Connection refused", result.ErrorMessage);
        Assert.Equal(30, result.ResponseTimeMs);
    }

    #endregion

    #region Failure Reason Tests

    [Theory]
    [InlineData(CloudflareBypassFailureReason.None, 0)]
    [InlineData(CloudflareBypassFailureReason.ServiceUnavailable, 1)]
    [InlineData(CloudflareBypassFailureReason.ConnectionFailed, 2)]
    [InlineData(CloudflareBypassFailureReason.ChallengeFailed, 3)]
    [InlineData(CloudflareBypassFailureReason.CaptchaRequired, 4)]
    [InlineData(CloudflareBypassFailureReason.Timeout, 5)]
    [InlineData(CloudflareBypassFailureReason.InvalidUrl, 6)]
    [InlineData(CloudflareBypassFailureReason.InvalidResponse, 7)]
    [InlineData(CloudflareBypassFailureReason.Disabled, 8)]
    [InlineData(CloudflareBypassFailureReason.TooManyRequests, 9)]
    [InlineData(CloudflareBypassFailureReason.Unknown, 99)]
    public void CloudflareBypassFailureReason_HasCorrectValues(CloudflareBypassFailureReason reason, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)reason);
    }

    #endregion

    #region Service Tests

    [Fact]
    public async Task FlareSolverrService_BypassAsync_WhenDisabled_ReturnsDisabledFailure()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        var result = await service.BypassAsync("https://example.com");

        Assert.False(result.Success);
        Assert.Equal(CloudflareBypassFailureReason.Disabled, result.FailureReason);
    }

    [Fact]
    public async Task FlareSolverrService_BypassAsync_WithInvalidUrl_ReturnsInvalidUrlFailure()
    {
        var mockSettings = new MockSettingsService();
        await mockSettings.SetAsync("CloudflareBypass", 
            """{"Enabled":true,"ServerUrl":"http://localhost:8191"}""");
        var service = new FlareSolverrService(mockSettings);

        var result = await service.BypassAsync("not-a-valid-url");

        Assert.False(result.Success);
        Assert.Equal(CloudflareBypassFailureReason.InvalidUrl, result.FailureReason);
    }

    [Fact]
    public async Task FlareSolverrService_TestConnectionAsync_WhenDisabled_ReturnsNotAvailable()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        var result = await service.TestConnectionAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("disabled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FlareSolverrService_GetSettingsAsync_ReturnsDefaultSettings()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        var settings = await service.GetSettingsAsync();

        Assert.NotNull(settings);
        Assert.False(settings.Enabled);
        Assert.Equal("http://localhost:8191", settings.ServerUrl);
    }

    [Fact]
    public async Task FlareSolverrService_SaveSettingsAsync_PersistsSettings()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        var newSettings = new CloudflareBypassSettings
        {
            Enabled = true,
            ServerUrl = "http://custom:8191"
        };

        await service.SaveSettingsAsync(newSettings);

        var retrieved = await service.GetSettingsAsync();
        Assert.True(retrieved.Enabled);
        Assert.Equal("http://custom:8191", retrieved.ServerUrl);
    }

    [Fact]
    public async Task FlareSolverrService_GetCachedSessionAsync_WhenNoCached_ReturnsNull()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        var session = await service.GetCachedSessionAsync("example.com");

        Assert.Null(session);
    }

    [Fact]
    public async Task FlareSolverrService_ClearSessionAsync_DoesNotThrow()
    {
        var mockSettings = new MockSettingsService();
        var service = new FlareSolverrService(mockSettings);

        await service.ClearSessionAsync("example.com");
        // No exception means success
    }

    #endregion

    #region Mock Settings Service

    private class MockSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string> _settings = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _settings.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
        {
            if (_settings.TryGetValue(key, out var json))
            {
                try
                {
                    return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(json));
                }
                catch
                {
                    return Task.FromResult(defaultValue);
                }
            }
            return Task.FromResult(defaultValue);
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _settings[key] = value;
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _settings[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task<IDictionary<string, string>> GetAllAsync(string? prefix = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(prefix))
                return Task.FromResult<IDictionary<string, string>>(_settings);
            return Task.FromResult<IDictionary<string, string>>(
                _settings.Where(kv => kv.Key.StartsWith(prefix)).ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _settings.Remove(key);
            return Task.CompletedTask;
        }

        public Task<UiSettings> GetUiSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UiSettings());

        public Task SetUiSettingsAsync(UiSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<GeneralSettings> GetGeneralSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GeneralSettings());

        public Task SetGeneralSettingsAsync(GeneralSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ApiKeyInfo> GetApiKeyAsync(bool includeFull = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = true, MaskedKey = "xxxx" });

        public Task<ApiKeyInfo> RegenerateApiKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = true, MaskedKey = "xxxx" });

        public Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ApiKeyInfo> SetApiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyInfo { IsEnabled = enabled, MaskedKey = "xxxx" });
    }

    #endregion
}
