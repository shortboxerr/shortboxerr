using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for monitoring DDL site health and availability with periodic checks.
/// </summary>
public class SiteHealthService : ISiteHealthService, IHostedService, IDisposable
{
    private readonly IDdlSiteAdapterFactory _adapterFactory;
    private readonly ILogger<SiteHealthService> _logger;
    
    private readonly ConcurrentDictionary<string, SiteHealthTracker> _healthTrackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<SiteHealthCheckResult>> _healthHistory = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _autoDisabledSites = new(StringComparer.OrdinalIgnoreCase);
    
    private SiteHealthSettings _settings = new();
    private Timer? _checkTimer;
    private bool _disposed;

    public SiteHealthService(
        IDdlSiteAdapterFactory adapterFactory,
        ILogger<SiteHealthService> logger)
    {
        _adapterFactory = adapterFactory;
        _logger = logger;
        
        InitializeTrackers();
    }

    private void InitializeTrackers()
    {
        foreach (var siteType in _adapterFactory.GetRegisteredSiteTypes())
        {
            _healthTrackers.TryAdd(siteType, new SiteHealthTracker { SiteType = siteType });
            _healthHistory.TryAdd(siteType, new List<SiteHealthCheckResult>());
        }
    }

    public Task<IReadOnlyList<SiteHealthStatus>> GetAllHealthStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = _adapterFactory.GetAvailableSiteInfos()
            .Select(info => BuildHealthStatus(info.SiteType, info.DisplayName))
            .ToList();

        return Task.FromResult<IReadOnlyList<SiteHealthStatus>>(statuses);
    }

    public Task<SiteHealthStatus?> GetHealthStatusAsync(string siteType, CancellationToken cancellationToken = default)
    {
        if (!_adapterFactory.IsRegistered(siteType))
        {
            return Task.FromResult<SiteHealthStatus?>(null);
        }

        var info = _adapterFactory.GetAvailableSiteInfos().FirstOrDefault(s => s.SiteType == siteType);
        if (info == null)
        {
            return Task.FromResult<SiteHealthStatus?>(null);
        }

        return Task.FromResult<SiteHealthStatus?>(BuildHealthStatus(siteType, info.DisplayName));
    }

    public async Task<SiteHealthCheckResult> CheckSiteHealthAsync(string siteType, CancellationToken cancellationToken = default)
    {
        if (!_adapterFactory.IsRegistered(siteType))
        {
            return new SiteHealthCheckResult
            {
                SiteType = siteType,
                Success = false,
                ErrorMessage = $"Site type '{siteType}' is not registered.",
                FailureType = HealthCheckFailureType.Unknown
            };
        }

        var adapter = _adapterFactory.GetAdapter(siteType);
        var tracker = _healthTrackers.GetOrAdd(siteType, _ => new SiteHealthTracker { SiteType = siteType });
        
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        SiteHealthCheckResult result;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.CheckTimeoutSeconds));

            // Perform test search
            var testResult = await adapter.TestConnectionAsync(cancellationToken: timeoutCts.Token);
            stopwatch.Stop();

            var diagnostics = new HealthCheckDiagnostics
            {
                ExpectedStructureFound = testResult.Success,
                HttpStatusCode = testResult.Success ? 200 : null
            };

            // Check for warnings
            if (testResult.Warnings.Any())
            {
                warnings.AddRange(testResult.Warnings);
            }

            if (testResult.LatencyMs > _settings.DegradedLatencyThresholdMs)
            {
                warnings.Add($"High latency detected: {testResult.LatencyMs}ms");
            }

            if (testResult.Success && testResult.SampleResultCount == 0)
            {
                warnings.Add("No results returned from test search - site may be experiencing issues");
            }

            result = new SiteHealthCheckResult
            {
                SiteType = siteType,
                Success = testResult.Success,
                LatencyMs = testResult.LatencyMs,
                ResultCount = testResult.SampleResultCount,
                ErrorMessage = testResult.Success ? null : (testResult.ErrorDetails ?? testResult.Message),
                FailureType = testResult.Success ? null : ClassifyFailure(testResult.ErrorDetails ?? testResult.Message),
                Diagnostics = diagnostics,
                Warnings = warnings
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result = new SiteHealthCheckResult
            {
                SiteType = siteType,
                Success = false,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = "Health check timed out",
                FailureType = HealthCheckFailureType.Timeout,
                Warnings = warnings
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            result = new SiteHealthCheckResult
            {
                SiteType = siteType,
                Success = false,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                FailureType = ClassifyHttpException(ex),
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Health check failed for site {SiteType}", siteType);
            
            result = new SiteHealthCheckResult
            {
                SiteType = siteType,
                Success = false,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                FailureType = ClassifyFailure(ex.Message),
                Warnings = warnings
            };
        }

        // Update tracker
        UpdateTracker(tracker, result);
        
        // Add to history
        AddToHistory(siteType, result);
        
        // Check for auto-disable
        CheckAutoDisable(siteType, tracker);

        _logger.LogDebug(
            "Health check for {SiteType}: Success={Success}, Latency={LatencyMs}ms, ConsecutiveFailures={ConsecutiveFailures}",
            siteType, result.Success, result.LatencyMs, tracker.ConsecutiveFailures);

        return result;
    }

    public async Task<IReadOnlyList<SiteHealthCheckResult>> CheckAllSitesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SiteHealthCheckResult>();
        var enabledSites = _adapterFactory.GetEnabledSites();

        foreach (var siteType in enabledSites)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await CheckSiteHealthAsync(siteType, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for site {SiteType}", siteType);
                results.Add(new SiteHealthCheckResult
                {
                    SiteType = siteType,
                    Success = false,
                    ErrorMessage = ex.Message,
                    FailureType = HealthCheckFailureType.Unknown
                });
            }
        }

        return results;
    }

    public Task<IReadOnlyList<SiteHealthCheckResult>> GetHealthHistoryAsync(string siteType, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!_healthHistory.TryGetValue(siteType, out var history))
        {
            return Task.FromResult<IReadOnlyList<SiteHealthCheckResult>>(Array.Empty<SiteHealthCheckResult>());
        }

        lock (history)
        {
            var results = history
                .OrderByDescending(h => h.CheckedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<SiteHealthCheckResult>>(results);
        }
    }

    public Task ClearHealthHistoryAsync(string siteType, CancellationToken cancellationToken = default)
    {
        if (_healthHistory.TryGetValue(siteType, out var history))
        {
            lock (history)
            {
                history.Clear();
            }
        }

        if (_healthTrackers.TryGetValue(siteType, out var tracker))
        {
            tracker.ConsecutiveFailures = 0;
            tracker.LatencyHistory.Clear();
        }

        _logger.LogInformation("Cleared health history for site {SiteType}", siteType);
        return Task.CompletedTask;
    }

    public Task<bool> ReEnableSiteAsync(string siteType, CancellationToken cancellationToken = default)
    {
        if (!_adapterFactory.IsRegistered(siteType))
        {
            return Task.FromResult(false);
        }

        if (!_autoDisabledSites.Contains(siteType))
        {
            return Task.FromResult(false);
        }

        _autoDisabledSites.Remove(siteType);
        
        var factory = _adapterFactory as DdlSiteAdapterFactory;
        factory?.EnableSite(siteType);
        
        if (_healthTrackers.TryGetValue(siteType, out var tracker))
        {
            tracker.ConsecutiveFailures = 0;
            tracker.AutoDisabledAt = null;
        }

        _logger.LogInformation("Re-enabled auto-disabled site {SiteType}", siteType);
        return Task.FromResult(true);
    }

    public void RecordSuccess(string siteType)
    {
        if (_healthTrackers.TryGetValue(siteType, out var tracker))
        {
            tracker.LastSuccessTime = DateTime.UtcNow;
            tracker.ConsecutiveFailures = 0;
            tracker.TotalSuccesses++;
        }
    }

    public void RecordFailure(string siteType, string errorMessage)
    {
        if (_healthTrackers.TryGetValue(siteType, out var tracker))
        {
            tracker.LastFailureTime = DateTime.UtcNow;
            tracker.LastErrorMessage = errorMessage;
            tracker.ConsecutiveFailures++;
            tracker.TotalFailures++;
            
            CheckAutoDisable(siteType, tracker);
        }
    }

    public SiteHealthSettings GetSettings()
    {
        return _settings;
    }

    public void UpdateSettings(SiteHealthSettings settings)
    {
        _settings = settings;
        
        // Restart timer with new interval if running
        if (_checkTimer != null && _settings.Enabled)
        {
            _checkTimer.Change(
                TimeSpan.FromMinutes(_settings.CheckIntervalMinutes),
                TimeSpan.FromMinutes(_settings.CheckIntervalMinutes));
        }
    }

    #region IHostedService

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.Enabled)
        {
            _logger.LogInformation(
                "Starting site health monitoring. Check interval: {IntervalMinutes} minutes",
                _settings.CheckIntervalMinutes);

            _checkTimer = new Timer(
                DoHealthCheck,
                null,
                TimeSpan.FromMinutes(1), // Initial delay
                TimeSpan.FromMinutes(_settings.CheckIntervalMinutes));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping site health monitoring");
        _checkTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void DoHealthCheck(object? state)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Starting periodic health check for all sites");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await CheckAllSitesAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic health check");
        }
    }

    #endregion

    #region Private Methods

    private SiteHealthStatus BuildHealthStatus(string siteType, string displayName)
    {
        var tracker = _healthTrackers.GetOrAdd(siteType, _ => new SiteHealthTracker { SiteType = siteType });
        var factory = _adapterFactory as DdlSiteAdapterFactory;
        var isEnabled = factory?.IsSiteEnabled(siteType) ?? _adapterFactory.GetEnabledSites().Contains(siteType);
        var isAutoDisabled = _autoDisabledSites.Contains(siteType);

        var state = DetermineState(tracker, isAutoDisabled);
        var successRate = CalculateSuccessRate(siteType);
        var avgLatency = CalculateAverageLatency(tracker);

        var issues = new List<string>();
        if (tracker.ConsecutiveFailures >= _settings.UnhealthyThreshold)
        {
            issues.Add($"{tracker.ConsecutiveFailures} consecutive failures");
        }
        if (avgLatency > _settings.DegradedLatencyThresholdMs)
        {
            issues.Add($"High average latency ({avgLatency}ms)");
        }
        if (isAutoDisabled)
        {
            issues.Add("Auto-disabled due to repeated failures");
        }

        return new SiteHealthStatus
        {
            SiteType = siteType,
            DisplayName = displayName,
            State = state,
            IsEnabled = isEnabled,
            IsAutoDisabled = isAutoDisabled,
            ConsecutiveFailures = tracker.ConsecutiveFailures,
            LastErrorMessage = tracker.LastErrorMessage,
            LastCheckTime = tracker.LastCheckTime,
            LastSuccessTime = tracker.LastSuccessTime,
            LastFailureTime = tracker.LastFailureTime,
            AverageLatencyMs = avgLatency,
            SuccessRate = successRate,
            DetectedIssues = issues,
            AutoDisabledAt = tracker.AutoDisabledAt
        };
    }

    private SiteHealthState DetermineState(SiteHealthTracker tracker, bool isAutoDisabled)
    {
        if (isAutoDisabled)
        {
            return SiteHealthState.Disabled;
        }

        if (!tracker.LastCheckTime.HasValue)
        {
            return SiteHealthState.Unknown;
        }

        if (tracker.ConsecutiveFailures >= _settings.UnhealthyThreshold)
        {
            return SiteHealthState.Unhealthy;
        }

        if (tracker.ConsecutiveFailures > 0 || CalculateAverageLatency(tracker) > _settings.DegradedLatencyThresholdMs)
        {
            return SiteHealthState.Degraded;
        }

        return SiteHealthState.Healthy;
    }

    private void UpdateTracker(SiteHealthTracker tracker, SiteHealthCheckResult result)
    {
        tracker.LastCheckTime = result.CheckedAt;

        if (result.Success)
        {
            tracker.LastSuccessTime = result.CheckedAt;
            tracker.ConsecutiveFailures = 0;
            tracker.TotalSuccesses++;
            tracker.LatencyHistory.Enqueue(result.LatencyMs);
            
            // Keep only last 10 latency measurements
            while (tracker.LatencyHistory.Count > 10)
            {
                tracker.LatencyHistory.TryDequeue(out _);
            }
        }
        else
        {
            tracker.LastFailureTime = result.CheckedAt;
            tracker.LastErrorMessage = result.ErrorMessage;
            tracker.ConsecutiveFailures++;
            tracker.TotalFailures++;
        }
    }

    private void AddToHistory(string siteType, SiteHealthCheckResult result)
    {
        var history = _healthHistory.GetOrAdd(siteType, _ => new List<SiteHealthCheckResult>());
        
        lock (history)
        {
            history.Add(result);
            
            // Trim to max entries
            while (history.Count > _settings.MaxHistoryEntries)
            {
                history.RemoveAt(0);
            }
        }
    }

    private void CheckAutoDisable(string siteType, SiteHealthTracker tracker)
    {
        if (!_settings.AutoDisableEnabled)
        {
            return;
        }

        if (tracker.ConsecutiveFailures >= _settings.AutoDisableThreshold && !_autoDisabledSites.Contains(siteType))
        {
            _autoDisabledSites.Add(siteType);
            tracker.AutoDisabledAt = DateTime.UtcNow;
            
            var factory = _adapterFactory as DdlSiteAdapterFactory;
            factory?.DisableSite(siteType);
            
            _logger.LogWarning(
                "Auto-disabled site {SiteType} after {ConsecutiveFailures} consecutive failures. Last error: {LastError}",
                siteType, tracker.ConsecutiveFailures, tracker.LastErrorMessage);
        }
    }

    private double CalculateSuccessRate(string siteType)
    {
        if (!_healthHistory.TryGetValue(siteType, out var history))
        {
            return 0;
        }

        lock (history)
        {
            if (history.Count == 0)
            {
                return 0;
            }

            var recentChecks = history.TakeLast(20).ToList();
            var successCount = recentChecks.Count(h => h.Success);
            return Math.Round((double)successCount / recentChecks.Count * 100, 1);
        }
    }

    private int CalculateAverageLatency(SiteHealthTracker tracker)
    {
        if (tracker.LatencyHistory.IsEmpty)
        {
            return 0;
        }

        return (int)tracker.LatencyHistory.Average();
    }

    private HealthCheckFailureType ClassifyFailure(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            return HealthCheckFailureType.Unknown;
        }

        var lower = errorMessage.ToLowerInvariant();

        if (lower.Contains("timeout") || lower.Contains("timed out"))
        {
            return HealthCheckFailureType.Timeout;
        }
        if (lower.Contains("dns") || lower.Contains("name resolution"))
        {
            return HealthCheckFailureType.DnsError;
        }
        if (lower.Contains("ssl") || lower.Contains("certificate") || lower.Contains("tls"))
        {
            return HealthCheckFailureType.SslError;
        }
        if (lower.Contains("cloudflare") || lower.Contains("challenge") || lower.Contains("captcha"))
        {
            return HealthCheckFailureType.CloudflareChallenge;
        }
        if (lower.Contains("rate limit") || lower.Contains("too many requests") || lower.Contains("429"))
        {
            return HealthCheckFailureType.RateLimited;
        }
        if (lower.Contains("401") || lower.Contains("403") || lower.Contains("unauthorized") || lower.Contains("forbidden"))
        {
            return HealthCheckFailureType.AuthenticationFailed;
        }
        if (lower.Contains("404") || lower.Contains("500") || lower.Contains("502") || lower.Contains("503"))
        {
            return HealthCheckFailureType.HttpError;
        }
        if (lower.Contains("parse") || lower.Contains("parsing") || lower.Contains("format") || lower.Contains("invalid"))
        {
            return HealthCheckFailureType.ParseError;
        }
        if (lower.Contains("no results") || lower.Contains("empty"))
        {
            return HealthCheckFailureType.NoResults;
        }
        if (lower.Contains("network") || lower.Contains("connection"))
        {
            return HealthCheckFailureType.NetworkError;
        }

        return HealthCheckFailureType.Unknown;
    }

    private HealthCheckFailureType ClassifyHttpException(HttpRequestException ex)
    {
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("ssl") || message.Contains("certificate"))
        {
            return HealthCheckFailureType.SslError;
        }
        if (message.Contains("name resolution") || message.Contains("dns"))
        {
            return HealthCheckFailureType.DnsError;
        }
        if (message.Contains("connection refused") || message.Contains("network"))
        {
            return HealthCheckFailureType.NetworkError;
        }

        return HealthCheckFailureType.HttpError;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _checkTimer?.Dispose();
        }

        _disposed = true;
    }

    #endregion

    /// <summary>
    /// Internal tracker for a single site's health state.
    /// </summary>
    private class SiteHealthTracker
    {
        public required string SiteType { get; init; }
        public DateTime? LastCheckTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public string? LastErrorMessage { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int TotalSuccesses { get; set; }
        public int TotalFailures { get; set; }
        public DateTime? AutoDisabledAt { get; set; }
        public ConcurrentQueue<int> LatencyHistory { get; } = new();
    }
}
