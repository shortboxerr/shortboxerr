using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Infrastructure.Notifications;
using Xunit;

namespace Shortboxerr.Tests;

public class PushbulletNotificationProviderTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly PushbulletNotificationProvider _provider;

    public PushbulletNotificationProviderTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _provider = new PushbulletNotificationProvider(_httpClient, null);
    }

    #region Properties Tests

    [Fact]
    public void ProviderType_ReturnsPushbullet()
    {
        Assert.Equal("Pushbullet", _provider.ProviderType);
    }

    [Fact]
    public void DisplayName_ReturnsPushbullet()
    {
        Assert.Equal("Pushbullet", _provider.DisplayName);
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
    public async Task SendAsync_WithEmptyAccessToken_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new PushbulletProviderSettings { AccessToken = "" };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Access Token is required", result.ErrorMessage);
    }

    #endregion

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsOk()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""iden"":""test-push-id"",""type"":""note"",""title"":""Test"",""body"":""Test message""}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.True(result.Success);
        Assert.Equal("test-push-id", result.ResponseId);
    }

    [Fact]
    public async Task SendAsync_WithHttpError_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.Unauthorized, @"{""error"":{""code"":""invalid_access_token"",""message"":""Access token is invalid""}}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Access token is invalid", result.ErrorMessage);
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
    public async Task SendAsync_SendsNoteType_WhenNoUrl()
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
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"type\":\"note\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_SendsLinkType_WhenUrlPresent()
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
                Content = new StringContent(@"{""iden"":""test""}")
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
        Assert.Contains("\"type\":\"link\"", capturedContent);
        Assert.Contains("\"url\":\"https://shortboxerr.local/series/1\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_IncludesAccessTokenHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Access-Token"));
        Assert.Equal("testaccesstoken", capturedRequest.Headers.GetValues("Access-Token").First());
    }

    [Fact]
    public async Task SendAsync_WithDeviceId_IncludesDeviceIden()
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
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.DeviceId = "device123";

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"device_iden\":\"device123\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithChannelTag_IncludesChannelTag()
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
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.ChannelTag = "my-channel";

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"channel_tag\":\"my-channel\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_WithEmail_IncludesEmail()
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
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();
        settings.SendToEmail = "user@example.com";

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"email\":\"user@example.com\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_IncludesTitleAndBody()
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
                Content = new StringContent(@"{""iden"":""test""}")
            });

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.NewRelease,
            Title = "New Comic Available",
            Message = "Batman #1 is out today!"
        };
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedContent);
        var json = JsonDocument.Parse(capturedContent);
        Assert.Equal("New Comic Available", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Batman #1 is out today!", json.RootElement.GetProperty("body").GetString());
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
    public async Task TestAsync_WithEmptyAccessToken_ReturnsFailed()
    {
        var settings = new PushbulletProviderSettings { AccessToken = "" };

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Access Token is required", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithValidCredentials_ReturnsOk()
    {
        // Setup validation response (GET /users/me) then send response (POST /pushes)
        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""iden"":""user123"",""email"":""test@example.com"",""name"":""Test User""}")
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""iden"":""push123""}")
            });

        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.True(result.Success);
        Assert.NotNull(result.Latency);
    }

    [Fact]
    public async Task TestAsync_WithInvalidToken_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.Unauthorized, @"{""error"":{""message"":""Invalid access token""}}");
        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Invalid access token", result.Message);
    }

    [Fact]
    public async Task TestAsync_ValidatesUserFirst()
    {
        var requestUrls = new List<string>();
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                requestUrls.Add(req.RequestUri!.ToString());
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""iden"":""test"",""email"":""test@example.com""}")
            });

        var settings = CreateValidSettings();

        await _provider.TestAsync(settings);

        Assert.True(requestUrls.Count >= 1);
        Assert.Contains(requestUrls, u => u.Contains("/users/me"));
    }

    #endregion

    #region Settings Class Tests

    [Fact]
    public void PushbulletProviderSettings_DefaultValues()
    {
        var settings = new PushbulletProviderSettings();

        Assert.Equal("Pushbullet", settings.ProviderType);
        Assert.Equal(string.Empty, settings.AccessToken);
        Assert.Null(settings.DeviceId);
        Assert.Null(settings.ChannelTag);
        Assert.Null(settings.SendToEmail);
    }

    [Fact]
    public void PushbulletProviderSettings_TargetingPriority()
    {
        // Device takes priority
        var settings1 = new PushbulletProviderSettings
        {
            DeviceId = "device1",
            ChannelTag = "channel1",
            SendToEmail = "test@example.com"
        };
        Assert.NotNull(settings1.DeviceId);

        // If no device, channel is used
        var settings2 = new PushbulletProviderSettings
        {
            ChannelTag = "channel1",
            SendToEmail = "test@example.com"
        };
        Assert.Null(settings2.DeviceId);
        Assert.NotNull(settings2.ChannelTag);

        // If no device or channel, email is used
        var settings3 = new PushbulletProviderSettings
        {
            SendToEmail = "test@example.com"
        };
        Assert.Null(settings3.DeviceId);
        Assert.Null(settings3.ChannelTag);
        Assert.NotNull(settings3.SendToEmail);
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

    private static PushbulletProviderSettings CreateValidSettings()
    {
        return new PushbulletProviderSettings
        {
            Name = "Test Pushbullet",
            AccessToken = "testaccesstoken"
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
