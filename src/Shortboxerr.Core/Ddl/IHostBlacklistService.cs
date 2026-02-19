namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for temporarily blacklisting download hosts that consistently fail.
/// Hosts are automatically removed from blacklist after a configurable duration.
/// </summary>
public interface IHostBlacklistService
{
    /// <summary>
    /// Check if a host is currently blacklisted.
    /// </summary>
    bool IsBlacklisted(string hostId);
    
    /// <summary>
    /// Check if a URL's host is currently blacklisted.
    /// </summary>
    bool IsUrlBlacklisted(string url);
    
    /// <summary>
    /// Add a host to the blacklist with optional reason and duration.
    /// </summary>
    void Blacklist(string hostId, string reason, TimeSpan? duration = null);
    
    /// <summary>
    /// Remove a host from the blacklist manually.
    /// </summary>
    bool RemoveFromBlacklist(string hostId);
    
    /// <summary>
    /// Record a download failure for a host. May trigger automatic blacklisting.
    /// </summary>
    void RecordFailure(string hostId, HostResolverFailureReason failureReason, string? errorMessage = null);
    
    /// <summary>
    /// Record a successful download for a host. Resets failure counter.
    /// </summary>
    void RecordSuccess(string hostId);
    
    /// <summary>
    /// Get the current blacklist status for all hosts.
    /// </summary>
    IReadOnlyList<HostBlacklistEntry> GetBlacklist();
    
    /// <summary>
    /// Get the blacklist entry for a specific host.
    /// </summary>
    HostBlacklistEntry? GetBlacklistEntry(string hostId);
    
    /// <summary>
    /// Get failure statistics for all tracked hosts.
    /// </summary>
    IReadOnlyList<HostFailureStats> GetFailureStatistics();
    
    /// <summary>
    /// Get failure statistics for a specific host.
    /// </summary>
    HostFailureStats? GetHostFailureStats(string hostId);
    
    /// <summary>
    /// Clear all failure statistics and blacklist entries.
    /// </summary>
    void ClearAll();
    
    /// <summary>
    /// Clear failure statistics for a specific host.
    /// </summary>
    void ClearHostStats(string hostId);
    
    /// <summary>
    /// Get the current blacklist settings.
    /// </summary>
    HostBlacklistSettings GetSettings();
    
    /// <summary>
    /// Update blacklist settings.
    /// </summary>
    void UpdateSettings(HostBlacklistSettings settings);
    
    /// <summary>
    /// Remove expired blacklist entries.
    /// </summary>
    int PurgeExpiredEntries();
}

/// <summary>
/// An entry in the host blacklist.
/// </summary>
public record HostBlacklistEntry
{
    /// <summary>
    /// Host identifier (e.g., "mediafire", "mega", "pixeldrain").
    /// </summary>
    public required string HostId { get; init; }
    
    /// <summary>
    /// Display name of the host.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Reason for blacklisting.
    /// </summary>
    public required string Reason { get; init; }
    
    /// <summary>
    /// When the host was blacklisted.
    /// </summary>
    public DateTime BlacklistedAt { get; init; }
    
    /// <summary>
    /// When the blacklist entry expires (null = permanent until manual removal).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }
    
    /// <summary>
    /// Whether this was automatically blacklisted due to failures.
    /// </summary>
    public bool IsAutomatic { get; init; }
    
    /// <summary>
    /// Number of consecutive failures that triggered the blacklist.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
    
    /// <summary>
    /// The failure reason that triggered blacklisting.
    /// </summary>
    public HostResolverFailureReason? TriggeringFailureReason { get; init; }
    
    /// <summary>
    /// Whether this entry has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
    
    /// <summary>
    /// Time remaining until expiry (null if permanent or expired).
    /// </summary>
    public TimeSpan? TimeRemaining => ExpiresAt.HasValue && !IsExpired 
        ? ExpiresAt.Value - DateTime.UtcNow 
        : null;
}

/// <summary>
/// Failure statistics for a download host.
/// </summary>
public record HostFailureStats
{
    /// <summary>
    /// Host identifier.
    /// </summary>
    public required string HostId { get; init; }
    
    /// <summary>
    /// Display name of the host.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Total number of successful downloads.
    /// </summary>
    public int SuccessCount { get; init; }
    
    /// <summary>
    /// Total number of failed downloads.
    /// </summary>
    public int FailureCount { get; init; }
    
    /// <summary>
    /// Current consecutive failure count.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
    
    /// <summary>
    /// Last successful download time.
    /// </summary>
    public DateTime? LastSuccessTime { get; init; }
    
    /// <summary>
    /// Last failure time.
    /// </summary>
    public DateTime? LastFailureTime { get; init; }
    
    /// <summary>
    /// Last error message.
    /// </summary>
    public string? LastErrorMessage { get; init; }
    
    /// <summary>
    /// Most recent failure reason.
    /// </summary>
    public HostResolverFailureReason? LastFailureReason { get; init; }
    
    /// <summary>
    /// Breakdown of failures by reason.
    /// </summary>
    public IReadOnlyDictionary<HostResolverFailureReason, int> FailuresByReason { get; init; } 
        = new Dictionary<HostResolverFailureReason, int>();
    
    /// <summary>
    /// Success rate (0-100).
    /// </summary>
    public double SuccessRate => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;
    
    /// <summary>
    /// Whether this host is currently blacklisted.
    /// </summary>
    public bool IsBlacklisted { get; init; }
    
    /// <summary>
    /// Number of times this host has been blacklisted.
    /// </summary>
    public int TimesBlacklisted { get; init; }
}

/// <summary>
/// Settings for host blacklisting behavior.
/// </summary>
public class HostBlacklistSettings
{
    /// <summary>
    /// Whether automatic blacklisting is enabled.
    /// </summary>
    public bool AutoBlacklistEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of consecutive failures before auto-blacklisting.
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 3;
    
    /// <summary>
    /// Default blacklist duration for auto-blacklisted hosts.
    /// </summary>
    public TimeSpan DefaultBlacklistDuration { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Maximum blacklist duration (cap for escalating durations).
    /// </summary>
    public TimeSpan MaxBlacklistDuration { get; set; } = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Whether to escalate blacklist duration for repeat offenders.
    /// </summary>
    public bool EscalateDuration { get; set; } = true;
    
    /// <summary>
    /// Multiplier for escalating duration (e.g., 2.0 = double each time).
    /// </summary>
    public double EscalationMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Failure reasons that should trigger immediate blacklisting (not wait for threshold).
    /// </summary>
    public HashSet<HostResolverFailureReason> ImmediateBlacklistReasons { get; set; } = new()
    {
        HostResolverFailureReason.HostUnavailable,
        HostResolverFailureReason.AuthenticationRequired
    };
    
    /// <summary>
    /// Failure reasons that should never trigger blacklisting (transient issues).
    /// </summary>
    public HashSet<HostResolverFailureReason> NonBlacklistableReasons { get; set; } = new()
    {
        HostResolverFailureReason.Timeout,
        HostResolverFailureReason.NetworkError
    };
    
    /// <summary>
    /// How long to retain failure statistics after last activity.
    /// </summary>
    public TimeSpan StatsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
