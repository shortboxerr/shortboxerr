using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Infrastructure.Notifications;
using Xunit;

namespace Shortboxerr.Tests;

public class WebhookNotificationProviderTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly WebhookNotificationProvider _provider;

    public WebhookNotificationProviderTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _provider = new WebhookNotificationProvider(_httpClient, null);
    }

    #region Properties Tests

    [Fact]
    public void ProviderType_ReturnsWebhook()
    {
        Assert.Equal("Webhook", _provider.ProviderType);
    }

    [Fact]
    public void DisplayName_ReturnsWebhook()
    {
        Assert.Equal("Webhook", _provider.DisplayName);
    }

    #endregion

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithInvalidSettingsType_ReturnsFailed()
    {
        // Arrange
        var notification = CreateTestNotification();
        var settings = new NotificationProviderSettings(); // Base class, not webhook

        // Act
        var result = await _provider.SendAsync(notification, settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid settings type", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithEmptyWebhookUrl_ReturnsFailed()
    {
        // Arrange
        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings { WebhookUrl = "" };

        // Act
        var result = await _provider.SendAsync(notification, settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Webhook URL is required", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsOk()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);
        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings { WebhookUrl = "https://example.com/webhook" };

        // Act
        var result = await _provider.SendAsync(notification, settings);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithFailedResponse_ReturnsFailed()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Server error");
        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings { WebhookUrl = "https://example.com/webhook" };

        // Act
        var result = await _provider.SendAsync(notification, settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("500", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithConnectionFailure_ReturnsFailed()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings { WebhookUrl = "https://example.com/webhook" };

        // Act
        var result = await _provider.SendAsync(notification, settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithBasicAuth_IncludesAuthHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://example.com/webhook",
            Username = "testuser",
            Password = "testpass"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Basic", capturedRequest.Headers.Authorization.Scheme);
        
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpass"));
        Assert.Equal(expectedCredentials, capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_WithCustomHeaders_IncludesHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://example.com/webhook",
            Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "CustomValue" },
                { "X-Another-Header", "AnotherValue" }
            }
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Custom-Header"));
        Assert.True(capturedRequest.Headers.Contains("X-Another-Header"));
        Assert.Equal("CustomValue", capturedRequest.Headers.GetValues("X-Custom-Header").First());
    }

    #endregion

    #region Discord Webhook Tests

    [Fact]
    public async Task SendAsync_ToDiscordWebhook_UsesDiscordFormat()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefg"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("\"username\":\"Shortboxerr\"", capturedContent);
        Assert.Contains("\"embeds\"", capturedContent);
        Assert.Contains("\"title\"", capturedContent);
        Assert.Contains("\"description\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_ToDiscordWebhook_WithSeriesInfo_IncludesFields()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.Grabbed,
            Title = "Issue Grabbed",
            Message = "Batman #1 has been grabbed.",
            SeriesTitle = "Batman",
            IssueNumber = 1
        };
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefg",
            IncludeSeries = true
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("\"fields\"", capturedContent);
        Assert.Contains("Batman", capturedContent);
    }

    [Theory]
    [InlineData(NotificationEventType.Test, 0x3498db)]
    [InlineData(NotificationEventType.NewRelease, 0x2ecc71)]
    [InlineData(NotificationEventType.Grabbed, 0x9b59b6)]
    [InlineData(NotificationEventType.DownloadFailed, 0xe74c3c)]
    [InlineData(NotificationEventType.Health, 0xf39c12)]
    public async Task SendAsync_ToDiscordWebhook_UsesCorrectColorForEventType(NotificationEventType eventType, int expectedColor)
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = new ExternalNotification
        {
            EventType = eventType,
            Title = "Test",
            Message = "Test message"
        };
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://discord.com/api/webhooks/123456789/abcdefg"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains($"\"color\":{expectedColor}", capturedContent);
    }

    #endregion

    #region Slack Webhook Tests

    [Fact]
    public async Task SendAsync_ToSlackWebhook_UsesSlackFormat()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("\"blocks\"", capturedContent);
        Assert.Contains("\"type\":\"header\"", capturedContent);
        Assert.Contains("\"type\":\"section\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_ToSlackWebhook_WithImage_IncludesImageBlock()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.NewRelease,
            Title = "New Release",
            Message = "Batman #1 is out today!",
            ImageUrl = "https://example.com/cover.jpg"
        };
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX",
            IncludeImages = true
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("\"type\":\"image\"", capturedContent);
        Assert.Contains("https://example.com/cover.jpg", capturedContent);
    }

    #endregion

    #region Generic Webhook Tests

    [Fact]
    public async Task SendAsync_ToGenericWebhook_UsesGenericFormat()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = CreateTestNotification();
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://example.com/custom-webhook"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("\"eventType\":\"Test\"", capturedContent);
        Assert.Contains("\"source\":\"Shortboxerr\"", capturedContent);
        Assert.Contains("\"title\"", capturedContent);
        Assert.Contains("\"message\"", capturedContent);
    }

    [Fact]
    public async Task SendAsync_ToGenericWebhook_IncludesAllNotificationData()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.Grabbed,
            Title = "Issue Grabbed",
            Message = "Batman #123 has been grabbed.",
            Url = "https://shortboxerr.local/series/1",
            ImageUrl = "https://example.com/cover.jpg",
            SeriesTitle = "Batman",
            IssueNumber = 123,
            Data = new Dictionary<string, object>
            {
                { "downloadSource", "GetComics" },
                { "size", 52428800 }
            }
        };
        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://example.com/custom-webhook"
        };

        // Act
        await _provider.SendAsync(notification, settings);

        // Assert
        Assert.NotNull(capturedContent);
        var json = JsonDocument.Parse(capturedContent);
        Assert.Equal("Grabbed", json.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("Issue Grabbed", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Batman #123 has been grabbed.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("https://shortboxerr.local/series/1", json.RootElement.GetProperty("url").GetString());
        Assert.Equal("https://example.com/cover.jpg", json.RootElement.GetProperty("imageUrl").GetString());
        Assert.Equal("Batman", json.RootElement.GetProperty("seriesTitle").GetString());
        Assert.Equal(123, json.RootElement.GetProperty("issueNumber").GetDecimal());
        Assert.Equal("Shortboxerr", json.RootElement.GetProperty("source").GetString());
    }

    #endregion

    #region TestAsync Tests

    [Fact]
    public async Task TestAsync_WithInvalidSettings_ReturnsFailed()
    {
        // Arrange
        var settings = new NotificationProviderSettings();

        // Act
        var result = await _provider.TestAsync(settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid settings type", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithEmptyUrl_ReturnsFailed()
    {
        // Arrange
        var settings = new WebhookProviderSettings { WebhookUrl = "" };

        // Act
        var result = await _provider.TestAsync(settings);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Webhook URL is required", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithSuccessfulWebhook_ReturnsOk()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);
        var settings = new WebhookProviderSettings { WebhookUrl = "https://example.com/webhook" };

        // Act
        var result = await _provider.TestAsync(settings);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Latency);
    }

    [Fact]
    public async Task TestAsync_WithFailedWebhook_ReturnsFailed()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.BadRequest, "Bad request");
        var settings = new WebhookProviderSettings { WebhookUrl = "https://example.com/webhook" };

        // Act
        var result = await _provider.TestAsync(settings);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestAsync_SendsTestNotification()
    {
        // Arrange
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
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var settings = new WebhookProviderSettings 
        { 
            WebhookUrl = "https://example.com/webhook" 
        };

        // Act
        await _provider.TestAsync(settings);

        // Assert
        Assert.NotNull(capturedContent);
        Assert.Contains("Test", capturedContent);
        Assert.Contains("Shortboxerr", capturedContent);
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
