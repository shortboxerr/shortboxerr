using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Token-bucket based rate limiter for DDL site requests.
/// Implements per-site rate limiting with configurable limits and backoff.
/// </summary>
public class DdlRateLimiter : IDdlRateLimiter
{
    private readonly ConcurrentDictionary<string, SiteRateLimitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<DdlRateLimiter>? _logger;

    // Default settings
    private const int DefaultRequestsPerMinute = 10;
    private const int DefaultMinDelayMs = 100;
    private const int DefaultBackoffSeconds = 60;
    private const int MaxBackoffSeconds = 300; // 5 minutes max

    public DdlRateLimiter(ILogger<DdlRateLimiter>? logger = null)
    {
        _logger = logger;
    }

    public async Task<IDisposable> AcquireAsync(string siteType, CancellationToken cancellationToken = default)
    {
        var state = GetOrCreateState(siteType);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if in backoff
            if (state.IsInBackoff)
            {
                var backoffRemaining = state.BackoffUntil!.Value - DateTime.UtcNow;
                if (backoffRemaining > TimeSpan.Zero)
                {
                    _logger?.LogDebug("Site {Site} is in backoff for {Remaining:F1}s", siteType, backoffRemaining.TotalSeconds);
                    await Task.Delay(backoffRemaining, cancellationToken);
                }
                state.ClearBackoff();
            }

            // Check minimum delay since last request
            if (state.LastRequestTime.HasValue && state.MinDelayMs > 0)
            {
                var timeSinceLastRequest = DateTime.UtcNow - state.LastRequestTime.Value;
                var minDelay = TimeSpan.FromMilliseconds(state.MinDelayMs);
                if (timeSinceLastRequest < minDelay)
                {
                    var waitTime = minDelay - timeSinceLastRequest;
                    _logger?.LogTrace("Waiting {Ms}ms before next request to {Site}", waitTime.TotalMilliseconds, siteType);
                    await Task.Delay(waitTime, cancellationToken);
                }
            }

            // Try to acquire a slot
            if (state.TryAcquireSlot())
            {
                _logger?.LogTrace("Acquired rate limit slot for {Site} ({Used}/{Limit})", 
                    siteType, state.RequestsInWindow, state.RequestsPerMinute);
                return new RateLimitToken(state);
            }

            // Window is full, wait until it resets
            var waitUntilReset = state.WindowResetTime - DateTime.UtcNow;
            if (waitUntilReset > TimeSpan.Zero)
            {
                _logger?.LogDebug("Rate limit reached for {Site}, waiting {Seconds:F1}s until window reset", 
                    siteType, waitUntilReset.TotalSeconds);
                await Task.Delay(waitUntilReset, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException(cancellationToken);
    }

    public bool TryAcquire(string siteType, out IDisposable? token)
    {
        var state = GetOrCreateState(siteType);
        
        // Check if in backoff
        if (state.IsInBackoff)
        {
            token = null;
            return false;
        }

        // Check minimum delay
        if (state.LastRequestTime.HasValue && state.MinDelayMs > 0)
        {
            var timeSinceLastRequest = DateTime.UtcNow - state.LastRequestTime.Value;
            if (timeSinceLastRequest < TimeSpan.FromMilliseconds(state.MinDelayMs))
            {
                token = null;
                return false;
            }
        }

        // Try to acquire
        if (state.TryAcquireSlot())
        {
            token = new RateLimitToken(state);
            return true;
        }

        token = null;
        return false;
    }

    public RateLimitStatus GetStatus(string siteType)
    {
        var state = GetOrCreateState(siteType);
        state.RefreshWindow(); // Ensure window is current
        
        return new RateLimitStatus
        {
            SiteType = siteType,
            RequestsPerMinute = state.RequestsPerMinute,
            MinDelayMs = state.MinDelayMs,
            RequestsInWindow = state.RequestsInWindow,
            RequestsRemaining = Math.Max(0, state.RequestsPerMinute - state.RequestsInWindow),
            WindowResetTime = state.WindowResetTime,
            IsInBackoff = state.IsInBackoff,
            BackoffUntil = state.BackoffUntil,
            LastRequestTime = state.LastRequestTime,
            TotalRequests = state.TotalRequests,
            RateLimitViolations = state.RateLimitViolations
        };
    }

    public IReadOnlyDictionary<string, RateLimitStatus> GetAllStatuses()
    {
        return _states.ToDictionary(
            kvp => kvp.Key,
            kvp => GetStatus(kvp.Key),
            StringComparer.OrdinalIgnoreCase
        );
    }

    public void Configure(string siteType, int requestsPerMinute, int minDelayMs = 0)
    {
        var state = GetOrCreateState(siteType);
        state.RequestsPerMinute = Math.Max(1, requestsPerMinute);
        state.MinDelayMs = Math.Max(0, minDelayMs);
        
        _logger?.LogInformation("Configured rate limit for {Site}: {Rpm} req/min, {DelayMs}ms min delay", 
            siteType, state.RequestsPerMinute, state.MinDelayMs);
    }

    public void ReportRateLimited(string siteType, TimeSpan? retryAfter = null)
    {
        var state = GetOrCreateState(siteType);
        state.RecordRateLimitViolation();

        // Calculate backoff duration
        var backoffDuration = retryAfter ?? TimeSpan.FromSeconds(
            Math.Min(DefaultBackoffSeconds * Math.Pow(2, Math.Min(state.ConsecutiveViolations - 1, 3)), MaxBackoffSeconds));

        state.SetBackoff(backoffDuration);
        
        _logger?.LogWarning("Rate limited by {Site}, backing off for {Seconds}s (violation #{Count})", 
            siteType, backoffDuration.TotalSeconds, state.RateLimitViolations);
    }

    public void Reset(string siteType)
    {
        if (_states.TryRemove(siteType, out _))
        {
            _logger?.LogDebug("Reset rate limiter state for {Site}", siteType);
        }
    }

    public void ResetAll()
    {
        _states.Clear();
        _logger?.LogDebug("Reset all rate limiter states");
    }

    private SiteRateLimitState GetOrCreateState(string siteType)
    {
        return _states.GetOrAdd(siteType, _ => new SiteRateLimitState
        {
            SiteType = siteType,
            RequestsPerMinute = DefaultRequestsPerMinute,
            MinDelayMs = DefaultMinDelayMs
        });
    }

    /// <summary>
    /// Internal state tracking for a single site.
    /// </summary>
    private class SiteRateLimitState
    {
        private readonly object _lock = new();
        private DateTime _windowStart = DateTime.UtcNow;
        private int _requestsInWindow;

        public required string SiteType { get; init; }
        public int RequestsPerMinute { get; set; }
        public int MinDelayMs { get; set; }
        public DateTime? LastRequestTime { get; private set; }
        public DateTime? BackoffUntil { get; private set; }
        public long TotalRequests { get; private set; }
        public int RateLimitViolations { get; private set; }
        public int ConsecutiveViolations { get; private set; }

        public int RequestsInWindow
        {
            get
            {
                lock (_lock)
                {
                    RefreshWindow();
                    return _requestsInWindow;
                }
            }
        }

        public DateTime WindowResetTime
        {
            get
            {
                lock (_lock)
                {
                    return _windowStart.AddMinutes(1);
                }
            }
        }

        public bool IsInBackoff => BackoffUntil.HasValue && BackoffUntil.Value > DateTime.UtcNow;

        public bool TryAcquireSlot()
        {
            lock (_lock)
            {
                RefreshWindow();
                
                if (_requestsInWindow >= RequestsPerMinute)
                {
                    return false;
                }

                _requestsInWindow++;
                LastRequestTime = DateTime.UtcNow;
                TotalRequests++;
                ConsecutiveViolations = 0; // Reset on successful request
                return true;
            }
        }

        public void RefreshWindow()
        {
            var now = DateTime.UtcNow;
            if (now >= _windowStart.AddMinutes(1))
            {
                _windowStart = now;
                _requestsInWindow = 0;
            }
        }

        public void RecordRateLimitViolation()
        {
            lock (_lock)
            {
                RateLimitViolations++;
                ConsecutiveViolations++;
            }
        }

        public void SetBackoff(TimeSpan duration)
        {
            lock (_lock)
            {
                BackoffUntil = DateTime.UtcNow.Add(duration);
            }
        }

        public void ClearBackoff()
        {
            lock (_lock)
            {
                if (BackoffUntil.HasValue && BackoffUntil.Value <= DateTime.UtcNow)
                {
                    BackoffUntil = null;
                }
            }
        }
    }

    /// <summary>
    /// Token returned when rate limit slot is acquired.
    /// </summary>
    private class RateLimitToken : IDisposable
    {
        private readonly SiteRateLimitState _state;
        private bool _disposed;

        public RateLimitToken(SiteRateLimitState state)
        {
            _state = state;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Could track request completion time here if needed
            }
        }
    }
}
