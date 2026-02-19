using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for download host blacklisting.
/// </summary>
public static class HostBlacklistEndpoints
{
    public static void MapHostBlacklistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddl/hosts/blacklist")
            .WithTags("Host Blacklist")
            .WithOpenApi();

        // GET all blacklisted hosts
        group.MapGet("/", (IHostBlacklistService blacklistService) =>
        {
            var entries = blacklistService.GetBlacklist();
            return Results.Ok(entries.Select(ToDto));
        })
        .WithName("GetBlacklist")
        .WithDescription("Gets all currently blacklisted download hosts.");

        // GET blacklist entry for specific host
        group.MapGet("/{hostId}", (string hostId, IHostBlacklistService blacklistService) =>
        {
            var entry = blacklistService.GetBlacklistEntry(hostId);
            if (entry == null)
            {
                return Results.NotFound(new { message = $"Host '{hostId}' is not blacklisted." });
            }
            return Results.Ok(ToDto(entry));
        })
        .WithName("GetBlacklistEntry")
        .WithDescription("Gets the blacklist entry for a specific host.");

        // POST blacklist a host manually
        group.MapPost("/{hostId}", (string hostId, BlacklistHostRequest request, IHostBlacklistService blacklistService) =>
        {
            TimeSpan? duration = request.DurationMinutes.HasValue 
                ? TimeSpan.FromMinutes(request.DurationMinutes.Value) 
                : null;
            
            blacklistService.Blacklist(hostId, request.Reason ?? "Manually blacklisted", duration);
            
            var entry = blacklistService.GetBlacklistEntry(hostId);
            return Results.Ok(entry != null ? ToDto(entry) : null);
        })
        .WithName("BlacklistHost")
        .WithDescription("Manually blacklist a download host.");

        // DELETE remove host from blacklist
        group.MapDelete("/{hostId}", (string hostId, IHostBlacklistService blacklistService) =>
        {
            var removed = blacklistService.RemoveFromBlacklist(hostId);
            if (!removed)
            {
                return Results.NotFound(new { message = $"Host '{hostId}' was not blacklisted." });
            }
            return Results.Ok(new { message = $"Host '{hostId}' removed from blacklist." });
        })
        .WithName("RemoveFromBlacklist")
        .WithDescription("Remove a host from the blacklist.");

        // GET failure statistics for all hosts
        group.MapGet("/stats", (IHostBlacklistService blacklistService) =>
        {
            var stats = blacklistService.GetFailureStatistics();
            return Results.Ok(stats.Select(ToStatsDto));
        })
        .WithName("GetHostFailureStats")
        .WithDescription("Gets failure statistics for all tracked hosts.");

        // GET failure statistics for specific host
        group.MapGet("/stats/{hostId}", (string hostId, IHostBlacklistService blacklistService) =>
        {
            var stats = blacklistService.GetHostFailureStats(hostId);
            if (stats == null)
            {
                return Results.NotFound(new { message = $"No statistics found for host '{hostId}'." });
            }
            return Results.Ok(ToStatsDto(stats));
        })
        .WithName("GetHostFailureStatsByHost")
        .WithDescription("Gets failure statistics for a specific host.");

        // DELETE clear all blacklist entries and stats
        group.MapDelete("/", (IHostBlacklistService blacklistService) =>
        {
            blacklistService.ClearAll();
            return Results.Ok(new { message = "All blacklist entries and statistics cleared." });
        })
        .WithName("ClearAllBlacklist")
        .WithDescription("Clears all blacklist entries and failure statistics.");

        // DELETE clear stats for specific host
        group.MapDelete("/stats/{hostId}", (string hostId, IHostBlacklistService blacklistService) =>
        {
            blacklistService.ClearHostStats(hostId);
            return Results.Ok(new { message = $"Statistics cleared for host '{hostId}'." });
        })
        .WithName("ClearHostStats")
        .WithDescription("Clears failure statistics for a specific host.");

        // GET blacklist settings
        group.MapGet("/settings", (IHostBlacklistService blacklistService) =>
        {
            var settings = blacklistService.GetSettings();
            return Results.Ok(ToSettingsDto(settings));
        })
        .WithName("GetBlacklistSettings")
        .WithDescription("Gets the current blacklist settings.");

        // PUT update blacklist settings
        group.MapPut("/settings", (HostBlacklistSettingsDto dto, IHostBlacklistService blacklistService) =>
        {
            var settings = FromSettingsDto(dto);
            blacklistService.UpdateSettings(settings);
            return Results.Ok(ToSettingsDto(blacklistService.GetSettings()));
        })
        .WithName("UpdateBlacklistSettings")
        .WithDescription("Updates the blacklist settings.");

        // POST purge expired entries
        group.MapPost("/purge", (IHostBlacklistService blacklistService) =>
        {
            var purged = blacklistService.PurgeExpiredEntries();
            return Results.Ok(new { purgedCount = purged });
        })
        .WithName("PurgeExpiredBlacklist")
        .WithDescription("Removes expired blacklist entries.");

        // GET check if a host is blacklisted
        group.MapGet("/check/{hostId}", (string hostId, IHostBlacklistService blacklistService) =>
        {
            var isBlacklisted = blacklistService.IsBlacklisted(hostId);
            var entry = blacklistService.GetBlacklistEntry(hostId);
            return Results.Ok(new BlacklistCheckResultDto
            {
                HostId = hostId,
                IsBlacklisted = isBlacklisted,
                Entry = entry != null ? ToDto(entry) : null
            });
        })
        .WithName("CheckHostBlacklisted")
        .WithDescription("Checks if a specific host is currently blacklisted.");
    }

    private static HostBlacklistEntryDto ToDto(HostBlacklistEntry entry) => new()
    {
        HostId = entry.HostId,
        DisplayName = entry.DisplayName,
        Reason = entry.Reason,
        BlacklistedAt = entry.BlacklistedAt,
        ExpiresAt = entry.ExpiresAt,
        IsAutomatic = entry.IsAutomatic,
        ConsecutiveFailures = entry.ConsecutiveFailures,
        TriggeringFailureReason = entry.TriggeringFailureReason?.ToString(),
        IsExpired = entry.IsExpired,
        TimeRemainingSeconds = entry.TimeRemaining?.TotalSeconds
    };

    private static HostFailureStatsDto ToStatsDto(HostFailureStats stats) => new()
    {
        HostId = stats.HostId,
        DisplayName = stats.DisplayName,
        SuccessCount = stats.SuccessCount,
        FailureCount = stats.FailureCount,
        ConsecutiveFailures = stats.ConsecutiveFailures,
        LastSuccessTime = stats.LastSuccessTime,
        LastFailureTime = stats.LastFailureTime,
        LastErrorMessage = stats.LastErrorMessage,
        LastFailureReason = stats.LastFailureReason?.ToString(),
        FailuresByReason = stats.FailuresByReason.ToDictionary(
            kvp => kvp.Key.ToString(), 
            kvp => kvp.Value),
        SuccessRate = stats.SuccessRate,
        IsBlacklisted = stats.IsBlacklisted,
        TimesBlacklisted = stats.TimesBlacklisted
    };

    private static HostBlacklistSettingsDto ToSettingsDto(HostBlacklistSettings settings) => new()
    {
        AutoBlacklistEnabled = settings.AutoBlacklistEnabled,
        ConsecutiveFailureThreshold = settings.ConsecutiveFailureThreshold,
        DefaultBlacklistDurationMinutes = (int)settings.DefaultBlacklistDuration.TotalMinutes,
        MaxBlacklistDurationMinutes = (int)settings.MaxBlacklistDuration.TotalMinutes,
        EscalateDuration = settings.EscalateDuration,
        EscalationMultiplier = settings.EscalationMultiplier,
        ImmediateBlacklistReasons = settings.ImmediateBlacklistReasons.Select(r => r.ToString()).ToList(),
        NonBlacklistableReasons = settings.NonBlacklistableReasons.Select(r => r.ToString()).ToList(),
        StatsRetentionDays = (int)settings.StatsRetentionPeriod.TotalDays
    };

    private static HostBlacklistSettings FromSettingsDto(HostBlacklistSettingsDto dto)
    {
        var settings = new HostBlacklistSettings
        {
            AutoBlacklistEnabled = dto.AutoBlacklistEnabled,
            ConsecutiveFailureThreshold = dto.ConsecutiveFailureThreshold,
            DefaultBlacklistDuration = TimeSpan.FromMinutes(dto.DefaultBlacklistDurationMinutes),
            MaxBlacklistDuration = TimeSpan.FromMinutes(dto.MaxBlacklistDurationMinutes),
            EscalateDuration = dto.EscalateDuration,
            EscalationMultiplier = dto.EscalationMultiplier,
            StatsRetentionPeriod = TimeSpan.FromDays(dto.StatsRetentionDays)
        };

        if (dto.ImmediateBlacklistReasons?.Any() == true)
        {
            settings.ImmediateBlacklistReasons = dto.ImmediateBlacklistReasons
                .Where(r => Enum.TryParse<HostResolverFailureReason>(r, out _))
                .Select(r => Enum.Parse<HostResolverFailureReason>(r))
                .ToHashSet();
        }

        if (dto.NonBlacklistableReasons?.Any() == true)
        {
            settings.NonBlacklistableReasons = dto.NonBlacklistableReasons
                .Where(r => Enum.TryParse<HostResolverFailureReason>(r, out _))
                .Select(r => Enum.Parse<HostResolverFailureReason>(r))
                .ToHashSet();
        }

        return settings;
    }
}

// DTOs
public class HostBlacklistEntryDto
{
    public required string HostId { get; init; }
    public required string DisplayName { get; init; }
    public required string Reason { get; init; }
    public DateTime BlacklistedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsAutomatic { get; init; }
    public int ConsecutiveFailures { get; init; }
    public string? TriggeringFailureReason { get; init; }
    public bool IsExpired { get; init; }
    public double? TimeRemainingSeconds { get; init; }
}

public class HostFailureStatsDto
{
    public required string HostId { get; init; }
    public required string DisplayName { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime? LastSuccessTime { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public string? LastErrorMessage { get; init; }
    public string? LastFailureReason { get; init; }
    public Dictionary<string, int> FailuresByReason { get; init; } = new();
    public double SuccessRate { get; init; }
    public bool IsBlacklisted { get; init; }
    public int TimesBlacklisted { get; init; }
}

public class HostBlacklistSettingsDto
{
    public bool AutoBlacklistEnabled { get; init; } = true;
    public int ConsecutiveFailureThreshold { get; init; } = 3;
    public int DefaultBlacklistDurationMinutes { get; init; } = 60;
    public int MaxBlacklistDurationMinutes { get; init; } = 1440;
    public bool EscalateDuration { get; init; } = true;
    public double EscalationMultiplier { get; init; } = 2.0;
    public List<string> ImmediateBlacklistReasons { get; init; } = new();
    public List<string> NonBlacklistableReasons { get; init; } = new();
    public int StatsRetentionDays { get; init; } = 7;
}

public class BlacklistHostRequest
{
    public string? Reason { get; init; }
    public int? DurationMinutes { get; init; }
}

public class BlacklistCheckResultDto
{
    public required string HostId { get; init; }
    public bool IsBlacklisted { get; init; }
    public HostBlacklistEntryDto? Entry { get; init; }
}
