using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog.Events;
using Serilog.Parsing;
using Shortboxerr.Api.Middleware;
using Shortboxerr.Infrastructure.Logging;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for correlation ID middleware and enricher functionality.
/// </summary>
public class CorrelationIdTests
{
    #region CorrelationIdMiddleware Tests

    [Fact]
    public async Task Middleware_WithExistingCorrelationId_UsesProvidedId()
    {
        // Arrange
        var correlationId = "test-correlation-123";
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;

        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task Middleware_WithRequestId_UsesRequestIdAsCorrelation()
    {
        // Arrange
        var requestId = "request-456";
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.RequestIdHeader] = requestId;

        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        Assert.Equal(requestId, context.TraceIdentifier);
    }

    [Fact]
    public async Task Middleware_WithBothHeaders_PrefersCorrelationId()
    {
        // Arrange
        var correlationId = "correlation-789";
        var requestId = "request-456";
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;
        context.Request.Headers[CorrelationIdMiddleware.RequestIdHeader] = requestId;

        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task Middleware_WithNoHeaders_GeneratesNewId()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        Assert.NotEmpty(context.TraceIdentifier);
        Assert.Contains("-", context.TraceIdentifier); // Format: timestamp-random
    }

    [Fact]
    public void GenerateCorrelationId_ReturnsExpectedFormat()
    {
        // Act
        var id = CorrelationIdMiddleware.GenerateCorrelationId();

        // Assert
        Assert.NotNull(id);
        Assert.Contains("-", id);

        var parts = id.Split('-');
        Assert.Equal(2, parts.Length);

        // Timestamp part should be 14 chars (yyyyMMddHHmmss)
        Assert.Equal(14, parts[0].Length);

        // Random part should be 8 chars
        Assert.Equal(8, parts[1].Length);
    }

    [Fact]
    public void GenerateCorrelationId_GeneratesUniqueIds()
    {
        // Generate multiple IDs and check uniqueness
        var ids = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            ids.Add(CorrelationIdMiddleware.GenerateCorrelationId());
        }

        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public async Task Middleware_SetsTraceIdentifierFromCorrelationId()
    {
        // Arrange
        var correlationId = "test-trace-identifier";
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;

        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        // The correlation ID should have been set in TraceIdentifier
        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task Middleware_SetsEmptyHeadersFromIncomingRequest()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "";

        var middleware = CreateMiddleware(out var tracker);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(tracker.WasCalled);
        // Empty header should cause generation of new ID
        Assert.NotEmpty(context.TraceIdentifier);
        Assert.Contains("-", context.TraceIdentifier);
    }

    #endregion

    #region CorrelationIdEnricher Tests

    [Fact]
    public void Enricher_WithHttpContext_AddsCorrelationId()
    {
        // Arrange
        var correlationId = "enricher-test-123";
        var httpContext = new DefaultHttpContext { TraceIdentifier = correlationId };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var enricher = new CorrelationIdEnricher(accessorMock.Object);
        var logEvent = CreateLogEvent();
        var factory = new TestLogEventPropertyFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey(CorrelationIdEnricher.CorrelationIdPropertyName));
        var value = logEvent.Properties[CorrelationIdEnricher.CorrelationIdPropertyName];
        Assert.Contains(correlationId, value.ToString());
    }

    [Fact]
    public void Enricher_WithoutHttpContext_AddsPlaceholder()
    {
        // Arrange
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var enricher = new CorrelationIdEnricher(accessorMock.Object);
        var logEvent = CreateLogEvent();
        var factory = new TestLogEventPropertyFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey(CorrelationIdEnricher.CorrelationIdPropertyName));
        var value = logEvent.Properties[CorrelationIdEnricher.CorrelationIdPropertyName];
        Assert.Contains("-", value.ToString()); // Placeholder is "-"
    }

    [Fact]
    public void Enricher_WithNullAccessor_AddsPlaceholder()
    {
        // Arrange
        var enricher = new CorrelationIdEnricher(null);
        var logEvent = CreateLogEvent();
        var factory = new TestLogEventPropertyFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey(CorrelationIdEnricher.CorrelationIdPropertyName));
    }

    [Fact]
    public void Enricher_WithEmptyTraceIdentifier_AddsPlaceholder()
    {
        // Arrange
        var httpContext = new DefaultHttpContext { TraceIdentifier = "" };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var enricher = new CorrelationIdEnricher(accessorMock.Object);
        var logEvent = CreateLogEvent();
        var factory = new TestLogEventPropertyFactory();

        // Act
        enricher.Enrich(logEvent, factory);

        // Assert
        Assert.True(logEvent.Properties.ContainsKey(CorrelationIdEnricher.CorrelationIdPropertyName));
        var value = logEvent.Properties[CorrelationIdEnricher.CorrelationIdPropertyName];
        Assert.Contains("-", value.ToString()); // Placeholder
    }

    #endregion

    #region Output Template Tests

    [Fact]
    public void CorrelationOutputTemplate_ContainsCorrelationId()
    {
        Assert.Contains("{CorrelationId}", SerilogConfiguration.CorrelationOutputTemplate);
    }

    [Fact]
    public void VerboseOutputTemplate_ContainsCorrelationId()
    {
        Assert.Contains("{CorrelationId}", SerilogConfiguration.VerboseOutputTemplate);
    }

    [Fact]
    public void JsonOutputTemplate_ContainsCorrelationId()
    {
        Assert.Contains("correlationId", SerilogConfiguration.JsonOutputTemplate);
    }

    [Theory]
    [InlineData("correlation")]
    [InlineData("CORRELATION")]
    public void GetOutputTemplate_CorrelationPreset_ReturnsCorrelationTemplate(string preset)
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", preset);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.CorrelationOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    #endregion

    #region Helpers

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static CorrelationIdMiddleware CreateMiddleware(out NextCallTracker tracker)
    {
        tracker = new NextCallTracker();
        var trackerRef = tracker;

        var next = new RequestDelegate(_ =>
        {
            trackerRef.WasCalled = true;
            return Task.CompletedTask;
        });

        var loggerMock = new Mock<ILogger<CorrelationIdMiddleware>>();
        return new CorrelationIdMiddleware(next, loggerMock.Object);
    }

    private static LogEvent CreateLogEvent()
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse("Test message");
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            template,
            Array.Empty<LogEventProperty>());
    }

    private class TestLogEventPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }

    /// <summary>
    /// Tracks if the next delegate was called.
    /// </summary>
    private class NextCallTracker
    {
        public bool WasCalled { get; set; }
    }

    #endregion
}
