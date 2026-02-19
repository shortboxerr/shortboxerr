using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Service for temporarily blacklisting download hosts that consistently fail.
/// </summary>
public class HostBlacklistService : IHostBlacklistService
{
    private readonly ILogger<HostBlacklistService>? _logger;
    private readonly IDownloadHostResolverFactory? _resolverFactory;
    private readonly ConcurrentDictionary<string, BlacklistState> _blacklist = new();
    private readonly ConcurrentDictionary<string, FailureState> _failureStats = new();
    private HostBlacklistSettings _settings = new();
    private readonly object _lock = new();

    public HostBlacklistService(
        IDownloadHostResolverFactory? resolverFactory = null,
        ILogger<HostBlacklistService>? logger = null)
    {
        _resolverFactory = resolverFactory;
        _logger = logger;
    }

    public bool IsBlacklisted(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return false;

        var normalizedId = hostId.ToLowerInvariant();
        
        if (_blacklist.TryGetValue(normalizedId, out var state))
        {
            if (state.IsExpired)
            {
                _blacklist.TryRemove(normalizedId, out _);
                _logger?.LogInformation("Host {HostId} blacklist expired, now available", hostId);
                return false;
            }
            return true;
        }
        
        return false;
    }

    public bool IsUrlBlacklisted(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        var hostId = ExtractHostId(url);
        return hostId != null && IsBlacklisted(hostId);
    }

    public void Blacklist(string hostId, string reason, TimeSpan? duration = null)
    {
        if (string.IsNullOrEmpty(hostId))
            return;

        var normalizedId = hostId.ToLowerInvariant();
        var displayName = GetDisplayName(normalizedId);
        var effectiveDuration = duration ?? _settings.DefaultBlacklistDuration;
        
        // Check for escalation
        if (_settings.EscalateDuration && _failureStats.TryGetValue(normalizedId, out var stats))
        {
            var multiplier = Math.Pow(_settings.EscalationMultiplier, stats.TimesBlacklisted);
            var escalatedDuration = TimeSpan.FromTicks((long)(effectiveDuration.Ticks * multiplier));
            effectiveDuration = escalatedDuration > _settings.MaxBlacklistDuration 
                ? _settings.MaxBlacklistDuration 
                : escalatedDuration;
        }

        var state = new BlacklistState
        {
            HostId = normalizedId,
            DisplayName = displayName,
            Reason = reason,
            BlacklistedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + effectiveDuration,
            IsAutomatic = false,
            ConsecutiveFailures = _failureStats.TryGetValue(normalizedId, out var fs) ? fs.ConsecutiveFailures : 0
        };

        _blacklist[normalizedId] = state;
        
        // Update blacklist count in failure stats
        _failureStats.AddOrUpdate(
            normalizedId,
            _ => new FailureState { HostId = normalizedId, DisplayName = displayName, TimesBlacklisted = 1 },
            (_, existing) =>
            {
                existing.TimesBlacklisted++;
                return existing;
            });

        _logger?.LogWarning("Host {HostId} blacklisted: {Reason}. Expires at {ExpiresAt}", 
            hostId, reason, state.ExpiresAt);
    }

    public bool RemoveFromBlacklist(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return false;

        var normalizedId = hostId.ToLowerInvariant();
        var removed = _blacklist.TryRemove(normalizedId, out _);
        
        if (removed)
        {
            _logger?.LogInformation("Host {HostId} manually removed from blacklist", hostId);
        }
        
        return removed;
    }

    public void RecordFailure(string hostId, HostResolverFailureReason failureReason, string? errorMessage = null)
    {
        if (string.IsNullOrEmpty(hostId))
            return;

        var normalizedId = hostId.ToLowerInvariant();
        var displayName = GetDisplayName(normalizedId);

        var stats = _failureStats.AddOrUpdate(
            normalizedId,
            _ =>
            {
                var state = new FailureState
                {
                    HostId = normalizedId,
                    DisplayName = displayName,
                    FailureCount = 1,
                    ConsecutiveFailures = 1,
                    LastFailureTime = DateTime.UtcNow,
                    LastErrorMessage = errorMessage,
                    LastFailureReason = failureReason
                };
                state.FailuresByReason[failureReason] = 1;
                return state;
            },
            (_, existing) =>
            {
                existing.FailureCount++;
                existing.ConsecutiveFailures++;
                existing.LastFailureTime = DateTime.UtcNow;
                existing.LastErrorMessage = errorMessage;
                existing.LastFailureReason = failureReason;
                
                if (!existing.FailuresByReason.ContainsKey(failureReason))
                    existing.FailuresByReason[failureReason] = 0;
                existing.FailuresByReason[failureReason]++;
                
                return existing;
            });

        _logger?.LogDebug("Recorded failure for {HostId}: {Reason} ({ErrorMessage}). Consecutive: {Count}", 
            hostId, failureReason, errorMessage, stats.ConsecutiveFailures);

        // Check for automatic blacklisting
        if (_settings.AutoBlacklistEnabled && !IsBlacklisted(normalizedId))
        {
            // Skip non-blacklistable reasons
            if (_settings.NonBlacklistableReasons.Contains(failureReason))
            {
                _logger?.LogDebug("Failure reason {Reason} is non-blacklistable, skipping auto-blacklist check", failureReason);
                return;
            }

            // Immediate blacklist for certain failure types
            if (_settings.ImmediateBlacklistReasons.Contains(failureReason))
            {
                AutoBlacklist(normalizedId, stats, $"Immediate blacklist due to {failureReason}", failureReason);
                return;
            }

            // Threshold-based blacklisting
            if (stats.ConsecutiveFailures >= _settings.ConsecutiveFailureThreshold)
            {
                AutoBlacklist(normalizedId, stats, 
                    $"Exceeded failure threshold ({stats.ConsecutiveFailures} consecutive failures)", 
                    failureReason);
            }
        }
    }

    public void RecordSuccess(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return;

        var normalizedId = hostId.ToLowerInvariant();
        var displayName = GetDisplayName(normalizedId);

        _failureStats.AddOrUpdate(
            normalizedId,
            _ => new FailureState
            {
                HostId = normalizedId,
                DisplayName = displayName,
                SuccessCount = 1,
                LastSuccessTime = DateTime.UtcNow
            },
            (_, existing) =>
            {
                existing.SuccessCount++;
                existing.ConsecutiveFailures = 0; // Reset consecutive failures
                existing.LastSuccessTime = DateTime.UtcNow;
                return existing;
            });

        _logger?.LogDebug("Recorded success for {HostId}", hostId);
    }

    public IReadOnlyList<HostBlacklistEntry> GetBlacklist()
    {
        PurgeExpiredEntries();
        
        return _blacklist.Values
            .Select(s => ToEntry(s))
            .OrderBy(e => e.ExpiresAt)
            .ToList();
    }

    public HostBlacklistEntry? GetBlacklistEntry(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return null;

        var normalizedId = hostId.ToLowerInvariant();
        
        if (_blacklist.TryGetValue(normalizedId, out var state))
        {
            if (state.IsExpired)
            {
                _blacklist.TryRemove(normalizedId, out _);
                return null;
            }
            return ToEntry(state);
        }
        
        return null;
    }

    public IReadOnlyList<HostFailureStats> GetFailureStatistics()
    {
        return _failureStats.Values
            .Select(s => ToStats(s))
            .OrderByDescending(s => s.FailureCount)
            .ToList();
    }

    public HostFailureStats? GetHostFailureStats(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return null;

        var normalizedId = hostId.ToLowerInvariant();
        
        return _failureStats.TryGetValue(normalizedId, out var state) 
            ? ToStats(state) 
            : null;
    }

    public void ClearAll()
    {
        _blacklist.Clear();
        _failureStats.Clear();
        _logger?.LogInformation("Cleared all blacklist entries and failure statistics");
    }

    public void ClearHostStats(string hostId)
    {
        if (string.IsNullOrEmpty(hostId))
            return;

        var normalizedId = hostId.ToLowerInvariant();
        _blacklist.TryRemove(normalizedId, out _);
        _failureStats.TryRemove(normalizedId, out _);
        _logger?.LogInformation("Cleared stats for host {HostId}", hostId);
    }

    public HostBlacklistSettings GetSettings()
    {
        lock (_lock)
        {
            return new HostBlacklistSettings
            {
                AutoBlacklistEnabled = _settings.AutoBlacklistEnabled,
                ConsecutiveFailureThreshold = _settings.ConsecutiveFailureThreshold,
                DefaultBlacklistDuration = _settings.DefaultBlacklistDuration,
                MaxBlacklistDuration = _settings.MaxBlacklistDuration,
                EscalateDuration = _settings.EscalateDuration,
                EscalationMultiplier = _settings.EscalationMultiplier,
                ImmediateBlacklistReasons = new HashSet<HostResolverFailureReason>(_settings.ImmediateBlacklistReasons),
                NonBlacklistableReasons = new HashSet<HostResolverFailureReason>(_settings.NonBlacklistableReasons),
                StatsRetentionPeriod = _settings.StatsRetentionPeriod
            };
        }
    }

    public void UpdateSettings(HostBlacklistSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
        }
        _logger?.LogInformation("Updated host blacklist settings");
    }

    public int PurgeExpiredEntries()
    {
        var expiredKeys = _blacklist
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _blacklist.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger?.LogDebug("Purged {Count} expired blacklist entries", expiredKeys.Count);
        }

        return expiredKeys.Count;
    }

    private void AutoBlacklist(string normalizedId, FailureState stats, string reason, HostResolverFailureReason failureReason)
    {
        var duration = _settings.DefaultBlacklistDuration;
        
        // Escalate duration for repeat offenders
        if (_settings.EscalateDuration && stats.TimesBlacklisted > 0)
        {
            var multiplier = Math.Pow(_settings.EscalationMultiplier, stats.TimesBlacklisted);
            var escalatedDuration = TimeSpan.FromTicks((long)(duration.Ticks * multiplier));
            duration = escalatedDuration > _settings.MaxBlacklistDuration 
                ? _settings.MaxBlacklistDuration 
                : escalatedDuration;
        }

        var state = new BlacklistState
        {
            HostId = normalizedId,
            DisplayName = stats.DisplayName,
            Reason = reason,
            BlacklistedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + duration,
            IsAutomatic = true,
            ConsecutiveFailures = stats.ConsecutiveFailures,
            TriggeringFailureReason = failureReason
        };

        _blacklist[normalizedId] = state;
        stats.TimesBlacklisted++;

        _logger?.LogWarning("Auto-blacklisted host {HostId}: {Reason}. Duration: {Duration}. Times blacklisted: {Times}", 
            normalizedId, reason, duration, stats.TimesBlacklisted);
    }

    private string? ExtractHostId(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            // Remove common prefixes
            if (host.StartsWith("www."))
                host = host[4..];
            
            // Try to get resolver's host ID
            if (_resolverFactory != null)
            {
                var resolver = _resolverFactory.GetResolver(url);
                if (resolver != null)
                    return resolver.HostId.ToLowerInvariant();
            }
            
            // Fallback: use domain without TLD
            var parts = host.Split('.');
            return parts.Length >= 2 ? parts[0] : host;
        }
        catch
        {
            return null;
        }
    }

    private string GetDisplayName(string hostId)
    {
        if (_resolverFactory != null)
        {
            var resolvers = _resolverFactory.GetAllResolvers();
            var resolver = resolvers.FirstOrDefault(r => 
                r.HostId.Equals(hostId, StringComparison.OrdinalIgnoreCase));
            if (resolver != null)
                return resolver.DisplayName;
        }
        
        // Fallback: capitalize first letter
        return char.ToUpperInvariant(hostId[0]) + hostId[1..];
    }

    private HostBlacklistEntry ToEntry(BlacklistState state) => new()
    {
        HostId = state.HostId,
        DisplayName = state.DisplayName,
        Reason = state.Reason,
        BlacklistedAt = state.BlacklistedAt,
        ExpiresAt = state.ExpiresAt,
        IsAutomatic = state.IsAutomatic,
        ConsecutiveFailures = state.ConsecutiveFailures,
        TriggeringFailureReason = state.TriggeringFailureReason
    };

    private HostFailureStats ToStats(FailureState state) => new()
    {
        HostId = state.HostId,
        DisplayName = state.DisplayName,
        SuccessCount = state.SuccessCount,
        FailureCount = state.FailureCount,
        ConsecutiveFailures = state.ConsecutiveFailures,
        LastSuccessTime = state.LastSuccessTime,
        LastFailureTime = state.LastFailureTime,
        LastErrorMessage = state.LastErrorMessage,
        LastFailureReason = state.LastFailureReason,
        FailuresByReason = new Dictionary<HostResolverFailureReason, int>(state.FailuresByReason),
        IsBlacklisted = IsBlacklisted(state.HostId),
        TimesBlacklisted = state.TimesBlacklisted
    };

    private class BlacklistState
    {
        public required string HostId { get; init; }
        public required string DisplayName { get; init; }
        public required string Reason { get; init; }
        public DateTime BlacklistedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public bool IsAutomatic { get; init; }
        public int ConsecutiveFailures { get; init; }
        public HostResolverFailureReason? TriggeringFailureReason { get; init; }
        
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
    }

    private class FailureState
    {
        public required string HostId { get; init; }
        public required string DisplayName { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public string? LastErrorMessage { get; set; }
        public HostResolverFailureReason? LastFailureReason { get; set; }
        public Dictionary<HostResolverFailureReason, int> FailuresByReason { get; } = new();
        public int TimesBlacklisted { get; set; }
    }
}
