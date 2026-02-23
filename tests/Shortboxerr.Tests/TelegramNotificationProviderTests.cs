using System.Net;
using Moq;
using Moq.Protected;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Infrastructure.Notifications;
using Xunit;

namespace Shortboxerr.Tests;

public class TelegramNotificationProviderTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly TelegramNotificationProvider _provider;

    public TelegramNotificationProviderTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _provider = new TelegramNotificationProvider(_httpClient, null);
    }

    #region Properties Tests

    [Fact]
    public void ProviderType_ReturnsTelegram()
    {
        Assert.Equal("Telegram", _provider.ProviderType);
    }

    [Fact]
    public void DisplayName_ReturnsTelegram()
    {
        Assert.Equal("Telegram", _provider.DisplayName);
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
    public async Task SendAsync_WithEmptyBotToken_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "", 
            ChatId = "123456789" 
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Bot Token is required", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithEmptyChatId_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz", 
            ChatId = "" 
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Chat ID is required", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithInvalidBotTokenFormat_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "invalid-token-without-colon", 
            ChatId = "123456789" 
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Bot Token format is invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithInvalidParseMode_ReturnsFailed()
    {
        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz", 
            ChatId = "123456789",
            ParseMode = "InvalidMode"
        };

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Invalid parse mode", result.ErrorMessage);
    }

    #endregion

    #region TestAsync Validation Tests

    [Fact]
    public async Task TestAsync_WithInvalidSettingsType_ReturnsFailed()
    {
        var settings = new NotificationProviderSettings();

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Invalid settings type", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithEmptyBotToken_ReturnsFailed()
    {
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "", 
            ChatId = "123456789" 
        };

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Bot Token is required", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithEmptyChatId_ReturnsFailed()
    {
        var settings = new TelegramProviderSettings 
        { 
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz", 
            ChatId = "" 
        };

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
        Assert.Contains("Chat ID is required", result.Message);
    }

    #endregion

    #region SendAsync Success Tests

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsOk()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""ok"":true,""result"":{""message_id"":123}}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.True(result.Success);
        Assert.Equal("123", result.ResponseId);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponseNoMessageId_ReturnsOk()
    {
        SetupHttpResponse(HttpStatusCode.OK, @"{""ok"":true,""result"":{}}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.True(result.Success);
    }

    #endregion

    #region SendAsync Error Tests

    [Fact]
    public async Task SendAsync_WithApiError_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.BadRequest, @"{""ok"":false,""error_code"":400,""description"":""Bad Request: chat not found""}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("chat not found", result.ErrorMessage);
        Assert.Contains("400", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithUnauthorizedError_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.Unauthorized, @"{""ok"":false,""error_code"":401,""description"":""Unauthorized""}");
        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithNetworkError_ReturnsFailed()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_WithCancellation_ReturnsFailed()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Operation was cancelled"));

        var notification = CreateTestNotification();
        var settings = CreateValidSettings();

        var result = await _provider.SendAsync(notification, settings);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage);
    }

    #endregion

    #region TestAsync Tests

    [Fact]
    public async Task TestAsync_WithValidToken_ReturnsOk()
    {
        SetupHttpResponses(new[]
        {
            (HttpStatusCode.OK, @"{""ok"":true,""result"":{""id"":123456789,""is_bot"":true,""first_name"":""TestBot"",""username"":""test_bot""}}"),
            (HttpStatusCode.OK, @"{""ok"":true,""result"":{""message_id"":1}}")
        });

        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.True(result.Success);
        Assert.Contains("@test_bot", result.Message);
    }

    [Fact]
    public async Task TestAsync_WithInvalidToken_ReturnsFailed()
    {
        SetupHttpResponse(HttpStatusCode.Unauthorized, @"{""ok"":false,""error_code"":401,""description"":""Unauthorized""}");
        var settings = CreateValidSettings();

        var result = await _provider.TestAsync(settings);

        Assert.False(result.Success);
    }

    #endregion

    #region Message Formatting Tests

    [Fact]
    public async Task SendAsync_WithSeriesInfo_IncludesSeriesInMessage()
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.Grabbed,
            Title = "Issue Grabbed",
            Message = "Downloaded Batman #100",
            SeriesTitle = "Batman",
            IssueNumber = 100
        };
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("Batman", capturedBody);
        Assert.Contains("100", capturedBody);
    }

    [Fact]
    public async Task SendAsync_WithUrl_IncludesLinkInMessage()
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Test",
            Message = "Test message",
            Url = "http://localhost:5000/series/123"
        };
        var settings = CreateValidSettings();

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("http://localhost:5000/series/123", capturedBody);
    }

    #endregion

    #region Settings Options Tests

    [Fact]
    public async Task SendAsync_WithSilentNotification_IncludesDisableNotificationFlag()
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings
        {
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
            ChatId = "123456789",
            SilentNotification = true
        };

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("disable_notification", capturedBody);
    }

    [Fact]
    public async Task SendAsync_WithDisabledLinkPreview_IncludesDisableWebPagePreviewFlag()
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings
        {
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
            ChatId = "123456789",
            EnableLinkPreview = false
        };

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("disable_web_page_preview", capturedBody);
    }

    [Fact]
    public async Task SendAsync_WithTopicId_IncludesMessageThreadId()
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings
        {
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
            ChatId = "123456789",
            TopicId = 42
        };

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("message_thread_id", capturedBody);
        Assert.Contains("42", capturedBody);
    }

    [Theory]
    [InlineData("HTML")]
    [InlineData("Markdown")]
    [InlineData("MarkdownV2")]
    public async Task SendAsync_WithParseMode_IncludesParseModeInRequest(string parseMode)
    {
        string? capturedBody = null;
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"{""ok"":true,""result"":{""message_id"":1}}")
            });

        var notification = CreateTestNotification();
        var settings = new TelegramProviderSettings
        {
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
            ChatId = "123456789",
            ParseMode = parseMode
        };

        await _provider.SendAsync(notification, settings);

        Assert.NotNull(capturedBody);
        Assert.Contains("parse_mode", capturedBody);
    }

    #endregion

    #region Helper Methods

    private static ExternalNotification CreateTestNotification()
    {
        return new ExternalNotification
        {
            EventType = NotificationEventType.Test,
            Title = "Test Notification",
            Message = "This is a test message"
        };
    }

    private static TelegramProviderSettings CreateValidSettings()
    {
        return new TelegramProviderSettings
        {
            BotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
            ChatId = "123456789",
            ParseMode = "HTML"
        };
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private void SetupHttpResponses((HttpStatusCode StatusCode, string Content)[] responses)
    {
        var sequence = _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach (var (statusCode, content) in responses)
        {
            sequence.ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        }
    }

    #endregion
}
