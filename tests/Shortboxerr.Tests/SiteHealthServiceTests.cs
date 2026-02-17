using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for SiteHealthService.
/// </summary>
public class SiteHealthServiceTests
{
    private readonly Mock<IDdlSiteAdapterFactory> _mockFactory;
    private readonly Mock<ILogger<SiteHealthService>> _mockLogger;
    private readonly SiteHealthService _service;

    public SiteHealthServiceTests()
    {
        _mockFactory = new Mock<IDdlSiteAdapterFactory>();
        _mockLogger = new Mock<ILogger<SiteHealthService>>();

        // Setup default factory behavior
        _mockFactory.Setup(f => f.GetRegisteredSiteTypes())
            .Returns(new List<string> { "GetComics", "ReadComicOnline" });

        _mockFactory.Setup(f => f.GetAvailableSiteInfos())
            .Returns(new List<DdlSiteInfo>
            {
                new() { SiteType = "GetComics", DisplayName = "GetComics.org", DefaultBaseUrl = "https://getcomics.org", RequiresAuthentication = false, DefaultRateLimitPerMinute = 10 },
                new() { SiteType = "ReadComicOnline", DisplayName = "ReadComicOnline", DefaultBaseUrl = "https://readcomiconline.li", RequiresAuthentication = false, DefaultRateLimitPerMinute = 10 }
            });

        _mockFactory.Setup(f => f.GetEnabledSites())
            .Returns(new List<string> { "GetComics", "ReadComicOnline" });

        _mockFactory.Setup(f => f.IsRegistered(It.IsAny<string>()))
            .Returns<string>(s => s == "GetComics" || s == "ReadComicOnline");

        _service = new SiteHealthService(_mockFactory.Object, _mockLogger.Object);
    }

    #region GetAllHealthStatusesAsync Tests

    [Fact]
    public async Task GetAllHealthStatusesAsync_ReturnsAllRegisteredSites()
    {
        // Act
        var statuses = await _service.GetAllHealthStatusesAsync();

        // Assert
        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, s => s.SiteType == "GetComics");
        Assert.Contains(statuses, s => s.SiteType == "ReadComicOnline");
    }

    [Fact]
    public async Task GetAllHealthStatusesAsync_InitialState_IsUnknown()
    {
        // Act
        var statuses = await _service.GetAllHealthStatusesAsync();

        // Assert
        Assert.All(statuses, s => Assert.Equal(SiteHealthState.Unknown, s.State));
        Assert.All(statuses, s => Assert.Equal(0, s.ConsecutiveFailures));
    }

    [Fact]
    public async Task GetAllHealthStatusesAsync_IncludesDisplayNames()
    {
        // Act
        var statuses = await _service.GetAllHealthStatusesAsync();

        // Assert
        var getComics = statuses.First(s => s.SiteType == "GetComics");
        Assert.Equal("GetComics.org", getComics.DisplayName);
    }

    #endregion

    #region GetHealthStatusAsync Tests

    [Fact]
    public async Task GetHealthStatusAsync_ExistingSite_ReturnsStatus()
    {
        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal("GetComics", status.SiteType);
    }

    [Fact]
    public async Task GetHealthStatusAsync_NonExistentSite_ReturnsNull()
    {
        // Act
        var status = await _service.GetHealthStatusAsync("NonExistent");

        // Assert
        Assert.Null(status);
    }

    #endregion

    #region CheckSiteHealthAsync Tests

    [Fact]
    public async Task CheckSiteHealthAsync_NonRegisteredSite_ReturnsFailure()
    {
        // Act
        var result = await _service.CheckSiteHealthAsync("NonExistent");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not registered", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_SuccessfulTest_ReturnsSuccess()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult
            {
                Success = true,
                Message = "OK",
                SampleResultCount = 5,
                LatencyMs = 100,
                Warnings = Array.Empty<string>()
            });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(100, result.LatencyMs);
        Assert.Equal(5, result.ResultCount);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_FailedTest_ReturnsFailure()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult
            {
                Success = false,
                Message = "Connection failed",
                ErrorDetails = "Network error",
                LatencyMs = 500,
                Warnings = Array.Empty<string>()
            });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_UpdatesTracker_OnSuccess()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(SiteHealthState.Healthy, status.State);
        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.NotNull(status.LastSuccessTime);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_UpdatesTracker_OnFailure()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 500, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(1, status.ConsecutiveFailures);
        Assert.NotNull(status.LastFailureTime);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_Timeout_ReturnsTimeoutFailure()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Update settings to short timeout
        _service.UpdateSettings(new SiteHealthSettings { CheckTimeoutSeconds = 1 });

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HealthCheckFailureType.Timeout, result.FailureType);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_HttpException_ClassifiesError()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SSL certificate error"));

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HealthCheckFailureType.SslError, result.FailureType);
    }

    [Fact]
    public async Task CheckSiteHealthAsync_AddsWarningForHighLatency()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 10000, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("latency"));
    }

    #endregion

    #region CheckAllSitesAsync Tests

    [Fact]
    public async Task CheckAllSitesAsync_ChecksAllEnabledSites()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter(It.IsAny<string>())).Returns(mockAdapter.Object);

        // Act
        var results = await _service.CheckAllSitesAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.SiteType == "GetComics");
        Assert.Contains(results, r => r.SiteType == "ReadComicOnline");
    }

    [Fact]
    public async Task CheckAllSitesAsync_CancellationRequested_StopsEarly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var results = await _service.CheckAllSitesAsync(cts.Token);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Health History Tests

    [Fact]
    public async Task GetHealthHistoryAsync_NoHistory_ReturnsEmpty()
    {
        // Act
        var history = await _service.GetHealthHistoryAsync("GetComics");

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_AfterChecks_ReturnsHistory()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        await _service.CheckSiteHealthAsync("GetComics");
        var history = await _service.GetHealthHistoryAsync("GetComics");

        // Assert
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_RespectsLimit()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Perform multiple checks
        for (int i = 0; i < 10; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        // Act
        var history = await _service.GetHealthHistoryAsync("GetComics", limit: 5);

        // Assert
        Assert.Equal(5, history.Count);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_ReturnsNewestFirst()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        var callCount = 0;
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = callCount * 100, Warnings = Array.Empty<string>() };
            });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        await Task.Delay(10); // Ensure different timestamps
        await _service.CheckSiteHealthAsync("GetComics");
        var history = await _service.GetHealthHistoryAsync("GetComics");

        // Assert
        Assert.True(history[0].CheckedAt >= history[1].CheckedAt);
    }

    [Fact]
    public async Task ClearHealthHistoryAsync_ClearsHistory()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        await _service.CheckSiteHealthAsync("GetComics");

        // Act
        await _service.ClearHealthHistoryAsync("GetComics");
        var history = await _service.GetHealthHistoryAsync("GetComics");

        // Assert
        Assert.Empty(history);
    }

    #endregion

    #region Auto-Disable Tests

    [Fact]
    public async Task CheckSiteHealthAsync_ReachesThreshold_AutoDisablesSite()
    {
        // Arrange - use the interface mock from the constructor
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);
        
        // Set low threshold for testing
        _service.UpdateSettings(new SiteHealthSettings { AutoDisableThreshold = 3, AutoDisableEnabled = true });

        // Act - fail multiple times to trigger auto-disable
        for (int i = 0; i < 3; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert - verify internal state tracking
        Assert.NotNull(status);
        Assert.True(status.IsAutoDisabled);
        Assert.Equal(SiteHealthState.Disabled, status.State);
        // Note: Cannot verify DisableSite call without concrete DdlSiteAdapterFactory,
        // but the internal state tracking (IsAutoDisabled) confirms the logic worked
    }

    [Fact]
    public async Task CheckSiteHealthAsync_AutoDisableDisabled_DoesNotAutoDisable()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Disable auto-disable
        _service.UpdateSettings(new SiteHealthSettings { AutoDisableEnabled = false, AutoDisableThreshold = 2 });

        // Act
        for (int i = 0; i < 5; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.False(status.IsAutoDisabled);
    }

    [Fact]
    public async Task ReEnableSiteAsync_AutoDisabledSite_ReEnablesSite()
    {
        // Arrange - use the interface mock from the constructor
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);
        _service.UpdateSettings(new SiteHealthSettings { AutoDisableThreshold = 2 });

        // Auto-disable first
        for (int i = 0; i < 3; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        // Verify auto-disabled
        var beforeStatus = await _service.GetHealthStatusAsync("GetComics");
        Assert.NotNull(beforeStatus);
        Assert.True(beforeStatus.IsAutoDisabled);

        // Act
        var result = await _service.ReEnableSiteAsync("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.True(result);
        Assert.NotNull(status);
        Assert.False(status.IsAutoDisabled);
        Assert.Equal(0, status.ConsecutiveFailures);
        // Note: Cannot verify EnableSite call without concrete DdlSiteAdapterFactory,
        // but the internal state tracking (IsAutoDisabled = false) confirms the logic worked
    }

    [Fact]
    public async Task ReEnableSiteAsync_NotAutoDisabled_ReturnsFalse()
    {
        // Act
        var result = await _service.ReEnableSiteAsync("GetComics");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReEnableSiteAsync_NonExistentSite_ReturnsFalse()
    {
        // Act
        var result = await _service.ReEnableSiteAsync("NonExistent");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RecordSuccess/RecordFailure Tests

    [Fact]
    public async Task RecordSuccess_ResetsConsecutiveFailures()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Create some failures
        await _service.CheckSiteHealthAsync("GetComics");
        await _service.CheckSiteHealthAsync("GetComics");

        // Act
        _service.RecordSuccess("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(0, status.ConsecutiveFailures);
    }

    [Fact]
    public async Task RecordFailure_IncrementsConsecutiveFailures()
    {
        // Arrange
        _service.RecordFailure("GetComics", "Error 1");
        _service.RecordFailure("GetComics", "Error 2");

        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(2, status.ConsecutiveFailures);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public void GetSettings_ReturnsDefaultSettings()
    {
        // Act
        var settings = _service.GetSettings();

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(30, settings.CheckIntervalMinutes);
        Assert.Equal(3, settings.UnhealthyThreshold);
        Assert.Equal(5, settings.AutoDisableThreshold);
    }

    [Fact]
    public void UpdateSettings_UpdatesSettings()
    {
        // Arrange
        var newSettings = new SiteHealthSettings
        {
            Enabled = false,
            CheckIntervalMinutes = 60,
            UnhealthyThreshold = 5,
            AutoDisableThreshold = 10
        };

        // Act
        _service.UpdateSettings(newSettings);
        var settings = _service.GetSettings();

        // Assert
        Assert.False(settings.Enabled);
        Assert.Equal(60, settings.CheckIntervalMinutes);
        Assert.Equal(5, settings.UnhealthyThreshold);
        Assert.Equal(10, settings.AutoDisableThreshold);
    }

    #endregion

    #region State Determination Tests

    [Fact]
    public async Task State_NeverChecked_IsUnknown()
    {
        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(SiteHealthState.Unknown, status.State);
    }

    [Fact]
    public async Task State_AllSuccessful_IsHealthy()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(SiteHealthState.Healthy, status.State);
    }

    [Fact]
    public async Task State_OneFailure_IsDegraded()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        await _service.CheckSiteHealthAsync("GetComics");
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(SiteHealthState.Degraded, status.State);
    }

    [Fact]
    public async Task State_ManyFailures_IsUnhealthy()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        _service.UpdateSettings(new SiteHealthSettings { UnhealthyThreshold = 3, AutoDisableEnabled = false });

        // Act - fail 3 times
        for (int i = 0; i < 3; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(SiteHealthState.Unhealthy, status.State);
    }

    #endregion

    #region Success Rate Calculation Tests

    [Fact]
    public async Task SuccessRate_NoHistory_ReturnsZero()
    {
        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(0, status.SuccessRate);
    }

    [Fact]
    public async Task SuccessRate_AllSuccessful_Returns100()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = true, Message = "OK", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        for (int i = 0; i < 5; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(100, status.SuccessRate);
    }

    [Fact]
    public async Task SuccessRate_MixedResults_ReturnsCorrectRate()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        var callCount = 0;
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First 3 succeed, last 2 fail
                return new DdlSiteTestResult
                {
                    Success = callCount <= 3,
                    Message = callCount <= 3 ? "OK" : "Failed",
                    LatencyMs = 100,
                    Warnings = Array.Empty<string>()
                };
            });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        for (int i = 0; i < 5; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Equal(60, status.SuccessRate); // 3/5 = 60%
    }

    #endregion

    #region Failure Classification Tests

    [Theory]
    [InlineData("timeout", HealthCheckFailureType.Timeout)]
    [InlineData("connection timed out", HealthCheckFailureType.Timeout)]
    [InlineData("DNS resolution failed", HealthCheckFailureType.DnsError)]
    [InlineData("name resolution failure", HealthCheckFailureType.DnsError)]
    [InlineData("SSL certificate error", HealthCheckFailureType.SslError)]
    [InlineData("TLS handshake failed", HealthCheckFailureType.SslError)]
    [InlineData("Cloudflare challenge detected", HealthCheckFailureType.CloudflareChallenge)]
    [InlineData("captcha required", HealthCheckFailureType.CloudflareChallenge)]
    [InlineData("rate limit exceeded", HealthCheckFailureType.RateLimited)]
    [InlineData("429 Too Many Requests", HealthCheckFailureType.RateLimited)]
    [InlineData("401 Unauthorized", HealthCheckFailureType.AuthenticationFailed)]
    [InlineData("403 Forbidden", HealthCheckFailureType.AuthenticationFailed)]
    [InlineData("500 Internal Server Error", HealthCheckFailureType.HttpError)]
    [InlineData("parsing failed", HealthCheckFailureType.ParseError)]
    [InlineData("no results returned", HealthCheckFailureType.NoResults)]
    [InlineData("network unreachable", HealthCheckFailureType.NetworkError)]
    public async Task CheckSiteHealthAsync_ClassifiesFailureType(string errorMessage, HealthCheckFailureType expectedType)
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult
            {
                Success = false,
                Message = errorMessage,
                ErrorDetails = errorMessage,
                LatencyMs = 100,
                Warnings = Array.Empty<string>()
            });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        // Act
        var result = await _service.CheckSiteHealthAsync("GetComics");

        // Assert
        Assert.Equal(expectedType, result.FailureType);
    }

    #endregion

    #region Detected Issues Tests

    [Fact]
    public async Task DetectedIssues_ManyFailures_ReportsIssue()
    {
        // Arrange
        var mockAdapter = new Mock<IDdlSiteAdapter>();
        mockAdapter.Setup(a => a.TestConnectionAsync(It.IsAny<DdlSiteCredentials?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DdlSiteTestResult { Success = false, Message = "Failed", LatencyMs = 100, Warnings = Array.Empty<string>() });

        _mockFactory.Setup(f => f.GetAdapter("GetComics")).Returns(mockAdapter.Object);

        _service.UpdateSettings(new SiteHealthSettings { UnhealthyThreshold = 3, AutoDisableEnabled = false });

        for (int i = 0; i < 4; i++)
        {
            await _service.CheckSiteHealthAsync("GetComics");
        }

        // Act
        var status = await _service.GetHealthStatusAsync("GetComics");

        // Assert
        Assert.NotNull(status);
        Assert.Contains(status.DetectedIssues, i => i.Contains("consecutive failures"));
    }

    #endregion
}
