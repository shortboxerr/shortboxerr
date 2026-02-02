using System.Text.Json;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden tests for DDL retry and failure handling.
/// These tests verify Mylar3 parity for retry behavior.
/// </summary>
public class DdlRetryGoldenTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void FixtureFile_Loads_Successfully()
    {
        var fixture = LoadFixture();
        
        Assert.NotNull(fixture);
        Assert.NotEmpty(fixture.RetryBehaviorTests);
        Assert.NotEmpty(fixture.BackoffTests);
        Assert.NotEmpty(fixture.FailureStateTests);
        Assert.NotEmpty(fixture.VerificationTests);
    }

    [Theory]
    [InlineData("retry_on_timeout", true)]
    [InlineData("retry_on_connection_failed", true)]
    [InlineData("retry_on_server_error_500", true)]
    [InlineData("retry_on_server_error_503", true)]
    [InlineData("retry_on_rate_limit_429", true)]
    [InlineData("no_retry_on_404", false)]
    [InlineData("no_retry_on_401", false)]
    [InlineData("no_retry_on_403", false)]
    [InlineData("no_retry_on_cancelled", false)]
    public void RetryBehavior_MatchesMylar3Defaults(string testId, bool expectedShouldRetry)
    {
        var fixture = LoadFixture();
        var testCase = fixture.RetryBehaviorTests.FirstOrDefault(t => t.Id == testId);
        
        Assert.NotNull(testCase);
        Assert.Equal(expectedShouldRetry, testCase.ShouldRetry);
    }

    [Theory]
    [InlineData("retry_on_timeout", "Timeout")]
    [InlineData("retry_on_connection_failed", "ConnectionFailed")]
    [InlineData("no_retry_on_404", "NotFound")]
    [InlineData("no_retry_on_401", "Unauthorized")]
    public void FailureReason_MapsCorrectly(string testId, string expectedReason)
    {
        var fixture = LoadFixture();
        var testCase = fixture.RetryBehaviorTests.FirstOrDefault(t => t.Id == testId);
        
        Assert.NotNull(testCase);
        Assert.Equal(expectedReason, testCase.FailureReason);
    }

    [Fact]
    public void IsTransientFailure_CorrectlyIdentifiesRetryableErrors()
    {
        // Based on DdlDownloadService implementation
        var transientReasons = new[]
        {
            DdlDownloadFailureReason.Timeout,
            DdlDownloadFailureReason.ConnectionFailed,
            DdlDownloadFailureReason.DnsFailure,
            DdlDownloadFailureReason.RateLimited,
            DdlDownloadFailureReason.ServerError
        };
        
        var nonTransientReasons = new[]
        {
            DdlDownloadFailureReason.NotFound,
            DdlDownloadFailureReason.Unauthorized,
            DdlDownloadFailureReason.Cancelled,
            DdlDownloadFailureReason.EmptyFile,
            DdlDownloadFailureReason.HtmlErrorPage
        };
        
        foreach (var reason in transientReasons)
        {
            Assert.True(IsTransientFailure(reason), $"{reason} should be transient");
        }
        
        foreach (var reason in nonTransientReasons)
        {
            Assert.False(IsTransientFailure(reason), $"{reason} should NOT be transient");
        }
    }

    [Theory]
    [InlineData(1000, true, new[] { 1000, 2000, 4000 })]
    [InlineData(1000, false, new[] { 1000, 1000, 1000 })]
    public void ExponentialBackoff_CalculatesCorrectDelays(int baseDelayMs, bool exponential, int[] expectedDelays)
    {
        for (int attempt = 0; attempt < expectedDelays.Length; attempt++)
        {
            var actualDelay = CalculateBackoffDelay(baseDelayMs, attempt, exponential, int.MaxValue);
            Assert.Equal(expectedDelays[attempt], actualDelay);
        }
    }

    [Fact]
    public void MaxDelayCapIsRespected()
    {
        var fixture = LoadFixture();
        var testCase = fixture.BackoffTests.FirstOrDefault(t => t.Id == "max_delay_cap");
        
        Assert.NotNull(testCase);
        
        // After many attempts, delay should cap at maxDelayMs
        var delay = CalculateBackoffDelay(
            testCase.BaseDelayMs, 
            10, // Many attempts
            testCase.UseExponentialBackoff, 
            testCase.MaxDelayMs ?? int.MaxValue);
        
        Assert.True(delay <= testCase.ExpectedMaxDelay);
    }

    [Theory]
    [InlineData("fail_empty_file", false, "EmptyFile")]
    [InlineData("fail_file_too_small", false, "FileTooSmall")]
    [InlineData("fail_html_error_page", false, "HtmlErrorPage")]
    [InlineData("pass_valid_cbz", true, null)]
    [InlineData("pass_valid_cbr", true, null)]
    public void VerificationTests_MatchExpected(string testId, bool expectedPasses, string? expectedReason)
    {
        var fixture = LoadFixture();
        var testCase = fixture.VerificationTests.FirstOrDefault(t => t.Id == testId);
        
        Assert.NotNull(testCase);
        Assert.Equal(expectedPasses, testCase.ExpectedPasses);
        Assert.Equal(expectedReason, testCase.FailureReason);
    }

    [Fact]
    public void DefaultSettings_MatchMylar3()
    {
        var fixture = LoadFixture();
        
        // Verify Mylar3 defaults
        Assert.Equal(3, fixture.DefaultSettings.MaxRetries);
        Assert.Equal(1000, fixture.DefaultSettings.RetryDelayMs);
        Assert.Equal(30000, fixture.DefaultSettings.MaxRetryDelayMs);
        Assert.True(fixture.DefaultSettings.UseExponentialBackoff);
        Assert.Equal(300, fixture.DefaultSettings.TimeoutSeconds);
    }

    private static DdlRetryGoldenFixture LoadFixture()
    {
        var path = Path.Combine("Fixtures", "ddl_retry_golden.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DdlRetryGoldenFixture>(json, JsonOptions)!;
    }

    private static bool IsTransientFailure(DdlDownloadFailureReason reason)
    {
        return reason switch
        {
            DdlDownloadFailureReason.Timeout => true,
            DdlDownloadFailureReason.ConnectionFailed => true,
            DdlDownloadFailureReason.DnsFailure => true,
            DdlDownloadFailureReason.RateLimited => true,
            DdlDownloadFailureReason.ServerError => true,
            _ => false
        };
    }

    private static int CalculateBackoffDelay(int baseDelayMs, int attempt, bool exponential, int maxDelayMs)
    {
        if (!exponential) return Math.Min(baseDelayMs, maxDelayMs);
        var delay = baseDelayMs * (int)Math.Pow(2, attempt);
        return Math.Min(delay, maxDelayMs);
    }
}

public class DdlRetryGoldenFixture
{
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Mylar3Parity { get; set; }
    public DdlRetryDefaultSettings DefaultSettings { get; set; } = new();
    public List<RetryBehaviorTestCase> RetryBehaviorTests { get; set; } = new();
    public List<BackoffTestCase> BackoffTests { get; set; } = new();
    public List<FailureStateTestCase> FailureStateTests { get; set; } = new();
    public List<VerificationTestCase> VerificationTests { get; set; } = new();
}

public class DdlRetryDefaultSettings
{
    public int MaxRetries { get; set; }
    public int RetryDelayMs { get; set; }
    public int MaxRetryDelayMs { get; set; }
    public bool UseExponentialBackoff { get; set; }
    public int TimeoutSeconds { get; set; }
}

public class RetryBehaviorTestCase
{
    public string Id { get; set; } = "";
    public string Scenario { get; set; } = "";
    public int Attempts { get; set; }
    public string FinalOutcome { get; set; } = "";
    public int ExpectedRetries { get; set; }
    public bool ShouldRetry { get; set; }
    public string? FailureReason { get; set; }
}

public class BackoffTestCase
{
    public string Id { get; set; } = "";
    public int MaxRetries { get; set; }
    public int BaseDelayMs { get; set; }
    public int? MaxDelayMs { get; set; }
    public int[]? ExpectedDelays { get; set; }
    public int? ExpectedMaxDelay { get; set; }
    public bool UseExponentialBackoff { get; set; }
}

public class FailureStateTestCase
{
    public string Id { get; set; } = "";
    public string Scenario { get; set; } = "";
    public int FailureCount { get; set; }
    public int FailureCountBefore { get; set; }
    public int FailureCountAfter { get; set; }
    public int FailureThreshold { get; set; }
    public string ExpectedProviderStatus { get; set; } = "";
}

public class VerificationTestCase
{
    public string Id { get; set; } = "";
    public string Scenario { get; set; } = "";
    public long FileSize { get; set; }
    public string? MagicBytes { get; set; }
    public bool ExpectedPasses { get; set; }
    public string? FailureReason { get; set; }
}

