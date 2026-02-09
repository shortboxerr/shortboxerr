using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for the DDL rate limiter.
/// </summary>
public class DdlRateLimiterTests
{
    #region Basic Functionality Tests

    [Fact]
    public async Task AcquireAsync_AllowsRequestsWithinLimit()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        using var token = await limiter.AcquireAsync("TestSite");
        Assert.NotNull(token);
    }

    [Fact]
    public void TryAcquire_ReturnsTrueWhenUnderLimit()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        var result = limiter.TryAcquire("TestSite", out var token);
        
        Assert.True(result);
        Assert.NotNull(token);
        token?.Dispose();
    }

    [Fact]
    public void TryAcquire_ReturnsFalseWhenAtLimit()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 2, minDelayMs: 0);

        // Exhaust the limit
        limiter.TryAcquire("TestSite", out var t1);
        limiter.TryAcquire("TestSite", out var t2);

        // Third request should fail
        var result = limiter.TryAcquire("TestSite", out var token);
        
        Assert.False(result);
        Assert.Null(token);

        t1?.Dispose();
        t2?.Dispose();
    }

    [Fact]
    public void GetStatus_ReturnsCorrectStatus()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 50);

        limiter.TryAcquire("TestSite", out var token);
        token?.Dispose();

        var status = limiter.GetStatus("TestSite");

        Assert.Equal("TestSite", status.SiteType);
        Assert.Equal(10, status.RequestsPerMinute);
        Assert.Equal(50, status.MinDelayMs);
        Assert.Equal(1, status.RequestsInWindow);
        Assert.Equal(9, status.RequestsRemaining);
        Assert.Equal(1, status.TotalRequests);
        Assert.False(status.IsInBackoff);
    }

    [Fact]
    public void GetAllStatuses_ReturnsAllConfiguredSites()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("Site1", requestsPerMinute: 10, minDelayMs: 0);
        limiter.Configure("Site2", requestsPerMinute: 20, minDelayMs: 0);

        var statuses = limiter.GetAllStatuses();

        Assert.Equal(2, statuses.Count);
        Assert.Contains("Site1", statuses.Keys);
        Assert.Contains("Site2", statuses.Keys);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public void ReportRateLimited_SetsBackoff()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        limiter.ReportRateLimited("TestSite");

        var status = limiter.GetStatus("TestSite");
        Assert.True(status.IsInBackoff);
        Assert.NotNull(status.BackoffUntil);
        Assert.Equal(1, status.RateLimitViolations);
    }

    [Fact]
    public void ReportRateLimited_RespectsRetryAfter()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        var retryAfter = TimeSpan.FromSeconds(30);
        limiter.ReportRateLimited("TestSite", retryAfter);

        var status = limiter.GetStatus("TestSite");
        Assert.True(status.IsInBackoff);
        Assert.NotNull(status.BackoffUntil);
        
        // BackoffUntil should be approximately 30 seconds in the future
        var expectedBackoff = DateTime.UtcNow.Add(retryAfter);
        Assert.InRange(status.BackoffUntil!.Value, expectedBackoff.AddSeconds(-1), expectedBackoff.AddSeconds(1));
    }

    [Fact]
    public void TryAcquire_FailsWhileInBackoff()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        limiter.ReportRateLimited("TestSite", TimeSpan.FromMinutes(5));

        var result = limiter.TryAcquire("TestSite", out var token);
        
        Assert.False(result);
        Assert.Null(token);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Configure_UpdatesSettings()
    {
        var limiter = new DdlRateLimiter();
        
        limiter.Configure("TestSite", requestsPerMinute: 20, minDelayMs: 100);

        var status = limiter.GetStatus("TestSite");
        Assert.Equal(20, status.RequestsPerMinute);
        Assert.Equal(100, status.MinDelayMs);
    }

    [Fact]
    public void Configure_EnforcesMinimumValues()
    {
        var limiter = new DdlRateLimiter();
        
        limiter.Configure("TestSite", requestsPerMinute: 0, minDelayMs: -10);

        var status = limiter.GetStatus("TestSite");
        Assert.Equal(1, status.RequestsPerMinute); // Minimum is 1
        Assert.Equal(0, status.MinDelayMs); // Minimum is 0
    }

    [Fact]
    public void DefaultConfiguration_IsReasonable()
    {
        var limiter = new DdlRateLimiter();

        // Access a site without explicit configuration
        limiter.TryAcquire("UnconfiguredSite", out var token);
        token?.Dispose();

        var status = limiter.GetStatus("UnconfiguredSite");
        Assert.True(status.RequestsPerMinute > 0);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsStateForSite()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);
        limiter.TryAcquire("TestSite", out var token);
        token?.Dispose();
        limiter.ReportRateLimited("TestSite");

        limiter.Reset("TestSite");

        var status = limiter.GetStatus("TestSite");
        Assert.Equal(0, status.RequestsInWindow);
        Assert.Equal(0, status.RateLimitViolations);
        Assert.False(status.IsInBackoff);
    }

    [Fact]
    public void ResetAll_ClearsAllStates()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("Site1", requestsPerMinute: 10, minDelayMs: 0);
        limiter.Configure("Site2", requestsPerMinute: 10, minDelayMs: 0);
        limiter.TryAcquire("Site1", out var t1);
        limiter.TryAcquire("Site2", out var t2);
        t1?.Dispose();
        t2?.Dispose();

        limiter.ResetAll();

        var statuses = limiter.GetAllStatuses();
        Assert.Empty(statuses);
    }

    #endregion

    #region Multi-Site Isolation Tests

    [Fact]
    public void RateLimits_ArePerSite()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("Site1", requestsPerMinute: 2, minDelayMs: 0);
        limiter.Configure("Site2", requestsPerMinute: 2, minDelayMs: 0);

        // Exhaust Site1's limit
        limiter.TryAcquire("Site1", out var t1);
        limiter.TryAcquire("Site1", out var t2);

        // Site2 should still be available
        var result = limiter.TryAcquire("Site2", out var token);
        
        Assert.True(result);
        Assert.NotNull(token);

        t1?.Dispose();
        t2?.Dispose();
        token?.Dispose();
    }

    [Fact]
    public void Backoff_IsPerSite()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("Site1", requestsPerMinute: 10, minDelayMs: 0);
        limiter.Configure("Site2", requestsPerMinute: 10, minDelayMs: 0);

        // Put Site1 in backoff
        limiter.ReportRateLimited("Site1", TimeSpan.FromMinutes(5));

        // Site1 should be in backoff
        var site1Status = limiter.GetStatus("Site1");
        Assert.True(site1Status.IsInBackoff);

        // Site2 should not be in backoff
        var site2Status = limiter.GetStatus("Site2");
        Assert.False(site2Status.IsInBackoff);

        // Should be able to acquire Site2
        var result = limiter.TryAcquire("Site2", out var token);
        Assert.True(result);
        token?.Dispose();
    }

    #endregion

    #region Request Tracking Tests

    [Fact]
    public void TotalRequests_TracksAcrossWindows()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire("TestSite", out var token);
            token?.Dispose();
        }

        var status = limiter.GetStatus("TestSite");
        Assert.Equal(5, status.TotalRequests);
    }

    [Fact]
    public void RateLimitViolations_Accumulate()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        limiter.ReportRateLimited("TestSite", TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10); // Let backoff expire
        limiter.ReportRateLimited("TestSite", TimeSpan.FromMilliseconds(1));

        var status = limiter.GetStatus("TestSite");
        Assert.Equal(2, status.RateLimitViolations);
    }

    #endregion

    #region Minimum Delay Tests

    [Fact]
    public void TryAcquire_RespectsMinDelay()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 100, minDelayMs: 100);

        // First request should succeed
        var result1 = limiter.TryAcquire("TestSite", out var t1);
        Assert.True(result1);
        t1?.Dispose();

        // Immediate second request should fail due to min delay
        var result2 = limiter.TryAcquire("TestSite", out var t2);
        Assert.False(result2);
        Assert.Null(t2);
    }

    [Fact]
    public void TryAcquire_SucceedsAfterMinDelay()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 100, minDelayMs: 50);

        // First request
        limiter.TryAcquire("TestSite", out var t1);
        t1?.Dispose();

        // Wait for min delay
        Thread.Sleep(60);

        // Second request should succeed
        var result = limiter.TryAcquire("TestSite", out var t2);
        Assert.True(result);
        t2?.Dispose();
    }

    #endregion

    #region Async Acquisition Tests

    [Fact]
    public async Task AcquireAsync_WaitsForBackoffToExpire()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 10, minDelayMs: 0);

        // Put in short backoff
        limiter.ReportRateLimited("TestSite", TimeSpan.FromMilliseconds(100));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var token = await limiter.AcquireAsync("TestSite");
        sw.Stop();

        // Should have waited for backoff
        Assert.True(sw.ElapsedMilliseconds >= 90); // Allow some tolerance
    }

    [Fact]
    public async Task AcquireAsync_IsCancellable()
    {
        var limiter = new DdlRateLimiter();
        limiter.Configure("TestSite", requestsPerMinute: 1, minDelayMs: 0);

        // Exhaust the limit
        limiter.TryAcquire("TestSite", out var t1);
        t1?.Dispose();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var token = await limiter.AcquireAsync("TestSite", cts.Token);
        });
    }

    #endregion
}
