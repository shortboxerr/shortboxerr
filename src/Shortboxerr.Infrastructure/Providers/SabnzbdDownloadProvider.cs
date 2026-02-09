using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Nzb;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// SABnzbd download provider that wraps the SABnzbd client.
/// Implements IDownloadProvider to integrate with the unified provider system.
/// </summary>
public class SabnzbdDownloadProvider : IDownloadProvider
{
    private readonly ProviderDefinition _definition;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SabnzbdSettings _settings;

    public SabnzbdDownloadProvider(ProviderDefinition definition, IHttpClientFactory httpClientFactory)
    {
        _definition = definition;
        _httpClientFactory = httpClientFactory;
        _settings = ParseSettings(definition);
    }

    public int Id => _definition.Id;
    public string Name => _definition.Name;
    public ProviderType Type => ProviderType.Usenet;
    public bool IsEnabled { get => _definition.IsEnabled; set => _definition.IsEnabled = value; }
    public int Priority { get => _definition.Priority; set => _definition.Priority = value; }
    public IReadOnlyList<string> SupportedProtocols => new[] { "nzb", "usenet" };

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var result = await client.TestConnectionAsync(cancellationToken);
            
            if (result.Success)
            {
                return ProviderTestResult.Ok(
                    $"Connected to SABnzbd {result.Version}",
                    (int)result.ResponseTimeMs);
            }
            
            return ProviderTestResult.Fail(result.Message);
        }
        catch (Exception ex)
        {
            return ProviderTestResult.Fail($"Connection error: {ex.Message}");
        }
    }

    public Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderHealth
        {
            Status = _definition.IsEnabled ? HealthStatus.Healthy : HealthStatus.Unknown,
            Message = _definition.IsEnabled ? "Enabled" : "Disabled"
        });
    }

    public async Task<DownloadResult> DownloadAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            var nzbUrl = candidate.DownloadUrl;
            if (string.IsNullOrEmpty(nzbUrl))
            {
                return DownloadResult.Fail("No download URL provided");
            }

            var client = CreateClient();
            var options = new NzbDownloadOptions
            {
                Category = _settings.Category,
                Priority = _settings.DefaultPriority,
                Name = candidate.ReleaseTitle
            };

            var result = await client.AddNzbUrlAsync(nzbUrl, options, cancellationToken);
            
            if (result.Success && result.DownloadId != null)
            {
                return DownloadResult.Ok(result.DownloadId, candidate);
            }
            
            return DownloadResult.Fail(result.ErrorMessage ?? "Failed to add NZB to SABnzbd");
        }
        catch (Exception ex)
        {
            return DownloadResult.Fail($"Download error: {ex.Message}");
        }
    }

    public async Task<DownloadStatus> GetStatusAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var status = await client.GetDownloadStatusAsync(downloadId, cancellationToken);
            
            if (status != null)
            {
                return MapNzbStatus(status);
            }

            return new DownloadStatus
            {
                DownloadId = downloadId,
                State = DownloadState.Failed,
                Error = "Download not found in queue or history"
            };
        }
        catch (Exception ex)
        {
            return new DownloadStatus
            {
                DownloadId = downloadId,
                State = DownloadState.Failed,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> CancelAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            return await client.RemoveDownloadAsync(downloadId, false, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<DownloadStatus>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var queue = await client.GetQueueAsync(cancellationToken);
            
            return queue.Select(MapNzbStatus).ToList();
        }
        catch
        {
            return Array.Empty<DownloadStatus>();
        }
    }

    private SabnzbdClient CreateClient()
    {
        var httpClient = _httpClientFactory.CreateClient("SABnzbd");
        return new SabnzbdClient(httpClient, _settings);
    }

    private static SabnzbdSettings ParseSettings(ProviderDefinition definition)
    {
        // First try to parse from Settings JSON
        if (!string.IsNullOrEmpty(definition.Settings))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<SabnzbdSettingsJson>(definition.Settings);
                if (parsed != null)
                {
                    // Handle legacy format where Host might contain full URL or host:port
                    var host = parsed.Host ?? definition.BaseUrl ?? "";
                    int? port = parsed.Port;
                    var useSsl = parsed.UseSsl ?? false;
                    
                    // Parse host if it contains protocol or port (legacy format)
                    (host, port, useSsl) = ParseHostString(host, port, useSsl);
                    
                    return new SabnzbdSettings
                    {
                        Host = host,
                        Port = port,
                        ApiKey = parsed.ApiKey ?? definition.ApiKey ?? "",
                        Category = parsed.Category ?? "comics",
                        UseSsl = useSsl
                    };
                }
            }
            catch
            {
                // Fall through to defaults
            }
        }

        // Fall back to ProviderDefinition fields
        var (fallbackHost, fallbackPort, fallbackSsl) = ParseHostString(definition.BaseUrl ?? "", null, false);
        return new SabnzbdSettings
        {
            Host = fallbackHost,
            Port = fallbackPort,
            ApiKey = definition.ApiKey ?? "",
            Category = "comics",
            UseSsl = fallbackSsl
        };
    }

    /// <summary>
    /// Parses a host string that might contain protocol and/or port (legacy format).
    /// </summary>
    private static (string Host, int? Port, bool UseSsl) ParseHostString(string hostString, int? existingPort, bool existingSsl)
    {
        if (string.IsNullOrEmpty(hostString))
        {
            return ("", existingPort, existingSsl);
        }

        var host = hostString;
        var port = existingPort;
        var useSsl = existingSsl;

        // Check for protocol prefix
        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            useSsl = true;
            host = host[8..];
        }
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            useSsl = false;
            host = host[7..];
        }

        // Remove trailing slashes
        host = host.TrimEnd('/');

        // Check for port in host:port format
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < host.Length - 1)
        {
            var portStr = host[(colonIndex + 1)..];
            if (int.TryParse(portStr, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535)
            {
                port ??= parsedPort;
                host = host[..colonIndex];
            }
        }

        return (host, port, useSsl);
    }

    private static DownloadStatus MapNzbStatus(NzbDownloadStatus nzbStatus)
    {
        return new DownloadStatus
        {
            DownloadId = nzbStatus.Id,
            State = MapNzbState(nzbStatus.State),
            Progress = nzbStatus.ProgressPercent,
            TotalBytes = nzbStatus.TotalBytes,
            DownloadedBytes = nzbStatus.DownloadedBytes,
            SpeedBytesPerSecond = nzbStatus.SpeedBytesPerSecond,
            EstimatedTimeRemaining = nzbStatus.TimeRemaining,
            Error = nzbStatus.ErrorMessage,
            StartedAt = nzbStatus.AddedAt ?? DateTime.UtcNow,
            CompletedAt = nzbStatus.CompletedAt,
            OutputPath = nzbStatus.DownloadPath,
            CandidateTitle = nzbStatus.Name
        };
    }

    private static DownloadState MapNzbState(NzbDownloadState state)
    {
        return state switch
        {
            NzbDownloadState.Queued => DownloadState.Queued,
            NzbDownloadState.Downloading => DownloadState.Downloading,
            NzbDownloadState.Paused => DownloadState.Paused,
            NzbDownloadState.Verifying => DownloadState.Processing,
            NzbDownloadState.Repairing => DownloadState.Processing,
            NzbDownloadState.Extracting => DownloadState.Processing,
            NzbDownloadState.PostProcessing => DownloadState.Processing,
            NzbDownloadState.Completed => DownloadState.Completed,
            NzbDownloadState.Failed => DownloadState.Failed,
            NzbDownloadState.Deleted => DownloadState.Cancelled,
            _ => DownloadState.Queued
        };
    }

    /// <summary>
    /// JSON structure for SABnzbd settings stored in ProviderDefinition.Settings.
    /// </summary>
    private class SabnzbdSettingsJson
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? ApiKey { get; set; }
        public string? Category { get; set; }
        public bool? UseSsl { get; set; }
    }
}

/// <summary>
/// Factory for creating SabnzbdDownloadProvider instances.
/// </summary>
public class SabnzbdDownloadProviderFactory
{
    private readonly IServiceProvider _services;

    public SabnzbdDownloadProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public SabnzbdDownloadProvider Create(ProviderDefinition definition)
    {
        var httpClientFactory = _services.GetRequiredService<IHttpClientFactory>();
        return new SabnzbdDownloadProvider(definition, httpClientFactory);
    }
}
