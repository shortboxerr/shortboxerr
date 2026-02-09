using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Shortboxerr.Infrastructure.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests to verify no credentials appear in log output.
/// These tests ensure the SensitiveDataDestructuringPolicy and SensitiveDataEnricher
/// properly mask API keys, passwords, tokens, and other sensitive data.
/// </summary>
public class SensitiveDataMaskingTests
{
    private const string RedactedValue = "***REDACTED***";
    
    #region Query Parameter Masking Tests
    
    [Theory]
    [InlineData("", "")]
    [InlineData("?name=test", "?name=test")]
    [InlineData("?apikey=abc123", "?apikey=***")]
    [InlineData("?api_key=abc123", "?api_key=***")]
    [InlineData("?token=secret123", "?token=***")]
    [InlineData("?password=mysecret", "?password=***")]
    [InlineData("?secret=hidden", "?secret=***")]
    [InlineData("?name=test&apikey=abc123", "?name=test&apikey=***")]
    [InlineData("?apikey=abc123&name=test", "?apikey=***&name=test")]
    [InlineData("?APIKEY=ABC123", "?APIKEY=***")]
    [InlineData("?ApiKey=abc123", "?ApiKey=***")]
    [InlineData("?name=test&apikey=abc123&token=xyz789", "?name=test&apikey=***&token=***")]
    public void MaskSensitiveQueryParams_MasksCorrectly(string input, string expected)
    {
        // Act
        var result = Program.MaskSensitiveQueryParams(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void MaskSensitiveQueryParams_HandlesNull()
    {
        // Act
        var result = Program.MaskSensitiveQueryParams(null!);
        
        // Assert
        Assert.Null(result);
    }
    
    #endregion
    
    #region SensitiveDataDestructuringPolicy Tests
    
    [Fact]
    public void DestructuringPolicy_MasksApiKeyInDictionary()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "apikey", "my-secret-api-key-12345" },
            { "name", "test-user" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("my-secret-api-key-12345", rendered);
        Assert.Contains(RedactedValue, rendered);
        Assert.Contains("test-user", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksPasswordInDictionary()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "password", "super-secret-pass" },
            { "username", "admin" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("super-secret-pass", rendered);
        Assert.Contains(RedactedValue, rendered);
        Assert.Contains("admin", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksTokenInDictionary()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "access_token", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" },
            { "expires_in", "3600" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", rendered);
        Assert.Contains(RedactedValue, rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksSecretInDictionary()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "client_secret", "client-secret-xyz" },
            { "client_id", "app-123" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("client-secret-xyz", rendered);
        Assert.Contains("app-123", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksAuthorizationHeader()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" },
            { "Content-Type", "application/json" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", rendered);
        Assert.Contains("application/json", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksConnectionString()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "connectionString", "Server=myserver;Database=mydb;User Id=admin;Password=secret123;" },
            { "timeout", "30" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("admin", rendered);
        Assert.DoesNotContain("secret123", rendered);
        Assert.Contains(RedactedValue, rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksMultipleSensitiveFields()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "apiKey", "key-12345" },
            { "token", "tok-67890" },
            { "password", "pass-abcde" },
            { "username", "user1" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("key-12345", rendered);
        Assert.DoesNotContain("tok-67890", rendered);
        Assert.DoesNotContain("pass-abcde", rendered);
        Assert.Contains("user1", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_HandlesEmptyDictionary()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>();
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksObjectWithApiKeyProperty()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var settings = new TestSettingsWithApiKey
        {
            ApiKey = "secret-api-key-value",
            Name = "TestSettings",
            Enabled = true
        };
        
        // Act
        var result = policy.TryDestructure(settings, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("secret-api-key-value", rendered);
        Assert.Contains("TestSettings", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_MasksObjectWithPasswordProperty()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var credentials = new TestCredentials
        {
            Username = "admin",
            Password = "super-secret-password"
        };
        
        // Act
        var result = policy.TryDestructure(credentials, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("super-secret-password", rendered);
        Assert.Contains("admin", rendered);
    }
    
    [Fact]
    public void DestructuringPolicy_IgnoresPrimitiveTypes()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        
        // Act & Assert - primitives should not be destructured
        Assert.False(policy.TryDestructure(42, new TestPropertyValueFactory(), out _));
        Assert.False(policy.TryDestructure(3.14, new TestPropertyValueFactory(), out _));
        Assert.False(policy.TryDestructure(true, new TestPropertyValueFactory(), out _));
        Assert.False(policy.TryDestructure("simple string", new TestPropertyValueFactory(), out _));
    }
    
    [Fact]
    public void DestructuringPolicy_MasksCaseInsensitive()
    {
        // Arrange
        var policy = new SensitiveDataDestructuringPolicy();
        var dict = new Dictionary<string, string>
        {
            { "APIKEY", "secret1" },
            { "ApiKey", "secret2" },
            { "apikey", "secret3" },
            { "PASSWORD", "secret4" },
            { "Password", "secret5" }
        };
        
        // Act
        var result = policy.TryDestructure(dict, new TestPropertyValueFactory(), out var propertyValue);
        
        // Assert
        Assert.True(result);
        var rendered = propertyValue.ToString();
        Assert.DoesNotContain("secret1", rendered);
        Assert.DoesNotContain("secret2", rendered);
        Assert.DoesNotContain("secret3", rendered);
        Assert.DoesNotContain("secret4", rendered);
        Assert.DoesNotContain("secret5", rendered);
    }
    
    #endregion
    
    #region SensitiveDataEnricher Tests
    
    [Fact]
    public void Enricher_AddsSensitiveFieldsMaskedProperty_WhenSensitiveFieldsPresent()
    {
        // Arrange
        var enricher = new SensitiveDataEnricher();
        var logEvent = CreateLogEvent();
        logEvent.AddOrUpdateProperty(new LogEventProperty("ApiKey", new ScalarValue("test-key")));
        logEvent.AddOrUpdateProperty(new LogEventProperty("Name", new ScalarValue("test")));
        
        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());
        
        // Assert
        Assert.True(logEvent.Properties.ContainsKey("SensitiveFieldsMasked"));
    }
    
    [Fact]
    public void Enricher_DoesNotAddSensitiveFieldsMaskedProperty_WhenNoSensitiveFields()
    {
        // Arrange
        var enricher = new SensitiveDataEnricher();
        var logEvent = CreateLogEvent();
        logEvent.AddOrUpdateProperty(new LogEventProperty("Name", new ScalarValue("test")));
        logEvent.AddOrUpdateProperty(new LogEventProperty("Count", new ScalarValue(42)));
        
        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());
        
        // Assert
        Assert.False(logEvent.Properties.ContainsKey("SensitiveFieldsMasked"));
    }
    
    [Fact]
    public void Enricher_CountsMultipleSensitiveFields()
    {
        // Arrange
        var enricher = new SensitiveDataEnricher();
        var logEvent = CreateLogEvent();
        logEvent.AddOrUpdateProperty(new LogEventProperty("ApiKey", new ScalarValue("key1")));
        logEvent.AddOrUpdateProperty(new LogEventProperty("Password", new ScalarValue("pass1")));
        logEvent.AddOrUpdateProperty(new LogEventProperty("Token", new ScalarValue("tok1")));
        
        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());
        
        // Assert
        Assert.True(logEvent.Properties.ContainsKey("SensitiveFieldsMasked"));
        var value = ((ScalarValue)logEvent.Properties["SensitiveFieldsMasked"]).Value;
        Assert.Equal(3, value);
    }
    
    #endregion
    
    #region End-to-End Log Output Tests
    
    /// <summary>
    /// Verifies that sensitive data in objects is masked when logged.
    /// Note: Tests focus on the critical requirement - sensitive data must not appear in logs.
    /// </summary>
    [Fact]
    public void EndToEnd_LogWithApiKey_DoesNotContainActualKey()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var settings = new TestSettingsWithApiKey
        {
            ApiKey = "cv-12345-secret-api-key",
            Name = "ComicVineSettings",
            Enabled = true
        };
        
        // Act
        logger.Information("Testing with settings: {@Settings}", settings);
        logger.Dispose();
        
        // Assert - Critical: secret data must NOT appear in output
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("cv-12345-secret-api-key", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithPassword_DoesNotContainActualPassword()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var credentials = new TestCredentials
        {
            Username = "admin",
            Password = "my-super-secret-password-123"
        };
        
        // Act
        logger.Information("User attempting login: {@Credentials}", credentials);
        logger.Dispose();
        
        // Assert - Critical: password must NOT appear in output
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("my-super-secret-password-123", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithConnectionString_DoesNotContainPassword()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var config = new Dictionary<string, string>
        {
            { "ConnectionString", "Server=localhost;Database=shortboxerr;User=sa;Password=MyP@ssw0rd!" },
            { "Provider", "sqlite" }
        };
        
        // Act
        logger.Information("Database config: {@Config}", config);
        logger.Dispose();
        
        // Assert - Critical: connection string password must NOT appear
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("MyP@ssw0rd!", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithAuthorizationHeader_DoesNotContainToken()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U" },
            { "Content-Type", "application/json" },
            { "Accept", "application/json" }
        };
        
        // Act
        logger.Information("Request headers: {@Headers}", headers);
        logger.Dispose();
        
        // Assert - Critical: JWT token must NOT appear
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithMultipleSensitiveFields_DoesNotContainAnySecrets()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var fullConfig = new TestFullConfiguration
        {
            ApiKey = "comicvine-api-key-secret",
            Password = "admin-password-123",
            Token = "refresh-token-abc",
            Secret = "client-secret-xyz",
            Name = "MyConfig",
            Enabled = true
        };
        
        // Act
        logger.Information("Full configuration loaded: {@Config}", fullConfig);
        logger.Dispose();
        
        // Assert - Critical: ALL sensitive values must NOT appear
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("comicvine-api-key-secret", output);
        Assert.DoesNotContain("admin-password-123", output);
        Assert.DoesNotContain("refresh-token-abc", output);
        Assert.DoesNotContain("client-secret-xyz", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithSabnzbdSettings_DoesNotContainApiKey()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var sabnzbdSettings = new Dictionary<string, object>
        {
            { "Host", "localhost" },
            { "Port", 8080 },
            { "ApiKey", "sabnzbd-api-key-1234567890abcdef" },
            { "Category", "comics" },
            { "UseSsl", false }
        };
        
        // Act
        logger.Information("SABnzbd settings: {@Settings}", sabnzbdSettings);
        logger.Dispose();
        
        // Assert - Critical: API key must NOT appear
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("sabnzbd-api-key-1234567890abcdef", output);
    }
    
    [Fact]
    public void EndToEnd_LogWithNewznabSettings_DoesNotContainApiKey()
    {
        // Arrange
        var sink = new TestSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();
        
        var newznabSettings = new Dictionary<string, object>
        {
            { "Name", "NZBgeek" },
            { "BaseUrl", "https://api.nzbgeek.info" },
            { "ApiKey", "nzbgeek-api-key-abcdefghijklmnop" },
            { "Categories", "7030,7020" }
        };
        
        // Act
        logger.Information("Newznab indexer: {@Settings}", newznabSettings);
        logger.Dispose();
        
        // Assert - Critical: API key must NOT appear
        var output = sink.GetLogOutput();
        Assert.DoesNotContain("nzbgeek-api-key-abcdefghijklmnop", output);
    }
    
    #endregion
    
    #region Test Helper Classes
    
    /// <summary>
    /// Simple test sink for capturing log output in memory.
    /// Renders messages with full property values for testing.
    /// </summary>
    private class TestSink : ILogEventSink
    {
        private readonly ConcurrentBag<LogEvent> _events = new();
        
        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }
        
        public string GetLogOutput()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var evt in _events)
            {
                // Render message template with properties
                var message = evt.MessageTemplate.Render(evt.Properties);
                sb.AppendLine(message);
                
                // Also render all properties directly for complete coverage
                foreach (var prop in evt.Properties)
                {
                    sb.AppendLine($"{prop.Key}={prop.Value}");
                }
            }
            return sb.ToString();
        }
    }
    
    private class TestSettingsWithApiKey
    {
        public string ApiKey { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
    }
    
    private class TestCredentials
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
    
    private class TestFullConfiguration
    {
        public string ApiKey { get; set; } = "";
        public string Password { get; set; } = "";
        public string Token { get; set; } = "";
        public string Secret { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
    }
    
    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplate("Test", Array.Empty<Serilog.Parsing.MessageTemplateToken>()),
            Array.Empty<LogEventProperty>());
    }
    
    /// <summary>
    /// Simple test implementation of ILogEventPropertyValueFactory for unit testing.
    /// </summary>
    private class TestPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
        {
            if (value is IDictionary<object, object?> dict)
            {
                var elements = new List<LogEventProperty>();
                foreach (var kvp in dict)
                {
                    var key = kvp.Key?.ToString() ?? "null";
                    var val = new ScalarValue(kvp.Value);
                    elements.Add(new LogEventProperty(key, val));
                }
                return new StructureValue(elements);
            }
            
            if (value is IDictionary<string, object?> strDict)
            {
                var elements = new List<LogEventProperty>();
                foreach (var kvp in strDict)
                {
                    elements.Add(new LogEventProperty(kvp.Key, new ScalarValue(kvp.Value)));
                }
                return new StructureValue(elements);
            }
            
            if (value is IDictionary<string, string> strStrDict)
            {
                var elements = new List<LogEventProperty>();
                foreach (var kvp in strStrDict)
                {
                    elements.Add(new LogEventProperty(kvp.Key, new ScalarValue(kvp.Value)));
                }
                return new StructureValue(elements);
            }
            
            return new ScalarValue(value);
        }
    }
    
    /// <summary>
    /// Simple test implementation of ILogEventPropertyFactory for unit testing.
    /// </summary>
    private class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
    
    #endregion
}
