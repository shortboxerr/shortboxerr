using System.Net;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Infrastructure.Notifications;
using Xunit;

namespace Shortboxerr.Tests;

public class PushoverNotificationProviderTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly PushoverNotificationProvider _provider;

    public PushoverNotificationProviderTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _provider = new PushoverNotificationProvider(_httpClient, null);
    }

    #region Properties Tests

    [Fact]
    public void ProviderType_ReturnsPushover()
    {
        Assert.Equal("Pushover", _provider.ProviderType);
    }

    [Fact]
    public void DisplayName_ReturnsPushover()
    {
        Assert.Equal("Pushover", _provider.DisplayName);
    }

    #endregion

    #region Settings Validation Tests

    [Fact]
    public async Task SendAsync_WithInvalidSettingsType_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new NotificationProviderSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Invalid settings type", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithEmptyApiToken_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new PushoverProviderSettings 
        { 
            ApiToken = "", 
            UserKey = "validuserkey" 
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("API Token is required", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithEmptyUserKey_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new PushoverProviderSettings 
        { 
            ApiToken = "validtoken", 
            UserKey = "" 
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("User Key is required", result.ErrorMessage);
    }

    #endregion

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsOk()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""status"":1,""request"":""test-request-id""}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.True(result.Success);
        Assert.Equal("test-request-id", result.ResponseId);
    }

    [Fact]
    public async Task SendAsync_WithApiError_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""status"":0,""errors"":[""invalid user key""]}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("invalid user key", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithHttpError_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Server error");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("500", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithConnectionFailure_ReturnsFailed()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_IncludesRequiredParameters()
    {
        string? capturedContent = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("token=testtoken", capturedContent);
        Assert.Contains("user=testuserkey", capturedContent);
        Assert.Contains("title=", capturedContent);
        Assert.Contains("message=", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithDevices_IncludesDevice()
    {
        string? capturedContent = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.Devices = "iphone,ipad";

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("device=iphone%2Cipad", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithSound_IncludesSound()
    {
        string? capturedContent = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.Sound = "cosmic";

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("sound=cosmic", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithEmergencyPriority_IncludesRetryAndExpire()
    {
        string? capturedContent = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.Priority = 2; // Emergency
        settings.RetrySeconds = 60;
        settings.ExpireSeconds = 3600;

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("priority=2", capturedContent);
        Assert.Contains("retry=60", capturedContent);
        Assert.Contains("expire=3600", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithUrl_IncludesUrl()
    {
        string? capturedContent = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedContent = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test""}")
            });

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Test",
            Message = "Test message",
            Url = "https://shortboxerr.local/series/1"
        };
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("url=", capturedContent);
        Assert.Contains("url_title=", capturedContent);
    }

    #endregion

    #region TestAsync Tests

    [Fact]
    public async Task TestAsync_WithInvalidSettings_ReturnsFailed()
    {
        var settings = new NotificationProviderSettings();

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Invalid settings type", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithEmptyApiToken_ReturnsFailed()
    {
        var settings = new PushoverProviderSettings { ApiToken = "", UserKey = "validkey" };

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("API Token is required", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithEmptyUserKey_ReturnsFailed()
    {
        var settings = new PushoverProviderSettings { ApiToken = "validtoken", UserKey = "" };

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("User Key is required", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithValidCredentials_ReturnsOk()
    {
        // Setup validation response
        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""devices"":[""iphone"",""ipad""]}")
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""status"":1,""request"":""test-id""}")
            });

        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.True(result.Success);
        Assert.NotNull(result.Latency);
    }

    [Fact]
    public async Task TestAsync_WithInvalidUserKey_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""status"":0,""errors"":[""user identifier is invalid""]}");
        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        // Error message comes from the errors array or falls back to default message
        Assert.True(result.Message.Contains("user identifier is invalid") || result.Message.Contains("Invalid user key"));
    }

    #endregion

    #region Settings Class Tests

    [Fact]
    public void PushoverProviderSettings_DefaultValues()
    {
        var settings = new PushoverProviderSettings();

        Assert.Equal("Pushover", settings.ProviderType);
        Assert.Equal(0, settings.Priority);
        Assert.Equal(60, settings.RetrySeconds);
        Assert.Equal(3600, settings.ExpireSeconds);
        Assert.Null(settings.Devices);
        Assert.Null(settings.Sound);
    }

    [Theory]
    [InlineData(-2, "Lowest")]
    [InlineData(-1, "Low")]
    [InlineData(0, "Normal")]
    [InlineData(1, "High")]
    [InlineData(2, "Emergency")]
    public void PushoverProviderSettings_PriorityLevels(int priority, string description)
    {
        var settings = new PushoverProviderSettings { Priority = priority };
        Assert.Equal(priority, settings.Priority);
        _ = description; // Just to confirm the test data makes sense
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(HttpStatusCode statusCode, string? content = null)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = content != null ? new StringContent(content) : null
            });
    }

    private static PushoverProviderSettings CreateValidSettings()
    {
        return new PushoverProviderSettings
        {
            Name = "Test Pushover",
            ApiToken = "testtoken",
            UserKey = "testuserkey"
        };
    }

    private static ExternalNotification CreateTestNotification()
    {
        return new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Test Notification",
            Message = "This is a test notification from Shortboxerr."
        };
    }

    #endregion
}
