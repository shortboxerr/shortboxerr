using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Shortboxerr.Infrastructure.Logging;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for log formatting components: ShortSourceContextEnricher, output templates.
/// </summary>
public class LogFormattingTests
{
    #region ShortSourceContextEnricher.ExtractShortName Tests

    [Theory]
    [InlineData("Shortboxerr.Infrastructure.ComicVine.ComicVineClient", "ComicVineClient")]
    [InlineData("Shortboxerr.Api.Program", "Program")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics", "Diagnostics")]
    [InlineData("SimpleClassName", "SimpleClassName")]
    [InlineData("Namespace.Class", "Class")]
    [InlineData("A.B.C.D.E.F", "F")]
    public void ExtractShortName_FullyQualifiedName_ReturnsClassName(string fullName, string expected)
    {
        var result = ShortSourceContextEnricher.ExtractShortName(fullName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractShortName_EmptyString_ReturnsEmpty()
    {
        var result = ShortSourceContextEnricher.ExtractShortName("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractShortName_NullString_ReturnsEmpty()
    {
        var result = ShortSourceContextEnricher.ExtractShortName(null!);
        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractShortName_WhitespaceOnly_ReturnsEmpty()
    {
        var result = ShortSourceContextEnricher.ExtractShortName("   ");
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("GenericClass`1", "GenericClass")]
    [InlineData("Namespace.GenericClass`2", "GenericClass")]
    [InlineData("Dictionary`2", "Dictionary")]
    [InlineData("ILogger`1", "ILogger")]
    public void ExtractShortName_GenericType_RemovesGenericSuffix(string fullName, string expected)
    {
        var result = ShortSourceContextEnricher.ExtractShortName(fullName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractShortName_EndsWithDot_ReturnsEmpty()
    {
        var result = ShortSourceContextEnricher.ExtractShortName("Namespace.");
        Assert.Equal("", result);
    }

    #endregion

    #region ShortSourceContextEnricher Integration Tests

    [Fact]
    public void Enricher_WithSourceContext_AddsShortSourceContext()
    {
        var enricher = new ShortSourceContextEnricher { MaxLength = 30, PadToMaxLength = false };
        var factory = new TestLogEventPropertyFactory();
        var logEvent = CreateLogEventWithSourceContext("Shortboxerr.Infrastructure.ComicVine.ComicVineClient");

        enricher.Enrich(logEvent, factory);

        Assert.True(logEvent.Properties.ContainsKey(ShortSourceContextEnricher.ShortSourceContextPropertyName));
        var shortContext = logEvent.Properties[ShortSourceContextEnricher.ShortSourceContextPropertyName];
        Assert.Contains("ComicVineClient", shortContext.ToString());
    }

    [Fact]
    public void Enricher_WithoutSourceContext_AddsEmptyPlaceholder()
    {
        var enricher = new ShortSourceContextEnricher { MaxLength = 25, PadToMaxLength = true };
        var factory = new TestLogEventPropertyFactory();
        var logEvent = CreateLogEventWithoutSourceContext();

        enricher.Enrich(logEvent, factory);

        Assert.True(logEvent.Properties.ContainsKey(ShortSourceContextEnricher.ShortSourceContextPropertyName));
        var shortContext = logEvent.Properties[ShortSourceContextEnricher.ShortSourceContextPropertyName];
        // Should be 25 spaces for alignment
        Assert.Equal(25, shortContext.ToString().Trim('"').Length);
    }

    [Fact]
    public void Enricher_LongClassName_TruncatesWithEllipsis()
    {
        var enricher = new ShortSourceContextEnricher { MaxLength = 15, PadToMaxLength = false };
        var factory = new TestLogEventPropertyFactory();
        var logEvent = CreateLogEventWithSourceContext("Namespace.VeryLongClassNameThatExceedsLimit");

        enricher.Enrich(logEvent, factory);

        var shortContext = logEvent.Properties[ShortSourceContextEnricher.ShortSourceContextPropertyName];
        var value = shortContext.ToString().Trim('"');
        Assert.Equal(15, value.Length);
        Assert.EndsWith("...", value);
    }

    [Fact]
    public void Enricher_ShortClassName_PadsToMaxLength()
    {
        var enricher = new ShortSourceContextEnricher { MaxLength = 20, PadToMaxLength = true };
        var factory = new TestLogEventPropertyFactory();
        var logEvent = CreateLogEventWithSourceContext("Namespace.Short");

        enricher.Enrich(logEvent, factory);

        var shortContext = logEvent.Properties[ShortSourceContextEnricher.ShortSourceContextPropertyName];
        var value = shortContext.ToString().Trim('"');
        Assert.Equal(20, value.Length);
        Assert.StartsWith("Short", value);
    }

    [Fact]
    public void Enricher_NoPadding_DoesNotPad()
    {
        var enricher = new ShortSourceContextEnricher { MaxLength = 20, PadToMaxLength = false };
        var factory = new TestLogEventPropertyFactory();
        var logEvent = CreateLogEventWithSourceContext("Namespace.Short");

        enricher.Enrich(logEvent, factory);

        var shortContext = logEvent.Properties[ShortSourceContextEnricher.ShortSourceContextPropertyName];
        var value = shortContext.ToString().Trim('"');
        Assert.Equal("Short", value);
    }

    #endregion

    #region SerilogConfiguration Tests

    [Fact]
    public void GetOutputTemplate_NoEnvVar_ReturnsDefault()
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);

        var template = SerilogConfiguration.GetOutputTemplate();

        Assert.Equal(SerilogConfiguration.DefaultOutputTemplate, template);
    }

    [Fact]
    public void GetOutputTemplate_EmptyEnvVar_ReturnsDefault()
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", "");
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.DefaultOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    [Theory]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("Default")]
    public void GetOutputTemplate_DefaultPreset_ReturnsDefaultTemplate(string preset)
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", preset);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.DefaultOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("COMPACT")]
    public void GetOutputTemplate_CompactPreset_ReturnsCompactTemplate(string preset)
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", preset);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.CompactOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("VERBOSE")]
    public void GetOutputTemplate_VerbosePreset_ReturnsVerboseTemplate(string preset)
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", preset);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.VerboseOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    public void GetOutputTemplate_JsonPreset_ReturnsJsonTemplate(string preset)
    {
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", preset);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(SerilogConfiguration.JsonOutputTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    [Fact]
    public void GetOutputTemplate_CustomTemplate_ReturnsCustom()
    {
        var customTemplate = "[{Timestamp:HH:mm}] {Message}{NewLine}";
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", customTemplate);
        try
        {
            var template = SerilogConfiguration.GetOutputTemplate();
            Assert.Equal(customTemplate, template);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_TEMPLATE", null);
        }
    }

    #endregion

    #region Output Template Format Verification

    [Fact]
    public void DefaultOutputTemplate_ContainsRequiredElements()
    {
        var template = SerilogConfiguration.DefaultOutputTemplate;

        Assert.Contains("{Timestamp:", template);
        Assert.Contains("{Level:u3}", template);
        Assert.Contains("{ShortSourceContext}", template);
        Assert.Contains("{Message:lj}", template);
        Assert.Contains("{Exception}", template);
    }

    [Fact]
    public void CompactOutputTemplate_IsMinimal()
    {
        var template = SerilogConfiguration.CompactOutputTemplate;

        Assert.Contains("{Level:u3}", template);
        Assert.Contains("{Message:lj}", template);
        Assert.DoesNotContain("{ShortSourceContext}", template);
        Assert.DoesNotContain("{Exception}", template);
    }

    [Fact]
    public void VerboseOutputTemplate_HasAllDetails()
    {
        var template = SerilogConfiguration.VerboseOutputTemplate;

        Assert.Contains("{Timestamp:", template);
        Assert.Contains("{Level:u3}", template);
        Assert.Contains("{ShortSourceContext}", template);
        Assert.Contains("{MachineName}", template);
        Assert.Contains("{Properties:j}", template);
        Assert.Contains("{Exception}", template);
    }

    [Fact]
    public void JsonOutputTemplate_IsValidJsonStructure()
    {
        var template = SerilogConfiguration.JsonOutputTemplate;

        Assert.Contains("\"timestamp\"", template);
        Assert.Contains("\"level\"", template);
        Assert.Contains("\"source\"", template);
        Assert.Contains("\"message\"", template);
        Assert.Contains("\"properties\"", template);
    }

    #endregion

    #region End-to-End Log Formatting Tests

    [Fact]
    public void EndToEnd_LogWithSourceContext_UsesShortName()
    {
        var sink = new StringWriterSink("[{Level:u3}] [{ShortSourceContext}] {Message:lj}{NewLine}");
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With(new ShortSourceContextEnricher { MaxLength = 25, PadToMaxLength = false })
            .WriteTo.Sink(sink)
            .CreateLogger();

        var contextLogger = logger.ForContext<LogFormattingTests>();
        contextLogger.Information("Test message");

        logger.Dispose();
        var logOutput = sink.Output.ToString();

        Assert.Contains("[INF]", logOutput);
        Assert.Contains("[LogFormattingTests]", logOutput);
        Assert.Contains("Test message", logOutput);
        // Should NOT contain the full namespace
        Assert.DoesNotContain("Shortboxerr.Tests.LogFormattingTests", logOutput);
    }

    [Fact]
    public void EndToEnd_LogWithException_FormatsStackTrace()
    {
        var sink = new StringWriterSink(SerilogConfiguration.DefaultOutputTemplate);
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With(new ShortSourceContextEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        var exception = new InvalidOperationException("Test exception");
        logger.Error(exception, "An error occurred");

        logger.Dispose();
        var logOutput = sink.Output.ToString();

        Assert.Contains("[ERR]", logOutput);
        Assert.Contains("An error occurred", logOutput);
        Assert.Contains("InvalidOperationException", logOutput);
        Assert.Contains("Test exception", logOutput);
    }

    [Fact]
    public void EndToEnd_LogLevelIndicators_AreThreeCharacters()
    {
        var sink = new StringWriterSink("[{Level:u3}]{NewLine}");
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Verbose("v");
        logger.Debug("d");
        logger.Information("i");
        logger.Warning("w");
        logger.Error("e");
        logger.Fatal("f");

        logger.Dispose();
        var logOutput = sink.Output.ToString();

        Assert.Contains("[VRB]", logOutput);
        Assert.Contains("[DBG]", logOutput);
        Assert.Contains("[INF]", logOutput);
        Assert.Contains("[WRN]", logOutput);
        Assert.Contains("[ERR]", logOutput);
        Assert.Contains("[FTL]", logOutput);
    }

    #endregion

    #region Helpers

    private static LogEvent CreateLogEventWithSourceContext(string sourceContext)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse("Test message");
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            template,
            new[]
            {
                new LogEventProperty("SourceContext", new ScalarValue(sourceContext))
            });
    }

    private static LogEvent CreateLogEventWithoutSourceContext()
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

    private class TestLogEventPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }

    /// <summary>
    /// Custom sink that writes to a StringWriter using a specified output template.
    /// </summary>
    private class StringWriterSink : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter;

        public StringWriter Output { get; } = new StringWriter();

        public StringWriterSink(string outputTemplate)
        {
            _formatter = new MessageTemplateTextFormatter(outputTemplate);
        }

        public void Emit(LogEvent logEvent)
        {
            _formatter.Format(logEvent, Output);
        }
    }

    #endregion
}
