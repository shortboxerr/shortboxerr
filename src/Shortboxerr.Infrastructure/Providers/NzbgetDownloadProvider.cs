using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.Providers;
using Shortboxerr.Infrastructure.Nzb;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// NZBGet download provider that wraps the NZBGet client.
/// Implements IDownloadProvider to integrate with the unified provider system.
/// </summary>
public class NzbgetDownloadProvider : IDownloadProvider
{
    private readonly ProviderDefinition _definition;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NzbgetSettings _settings;

    public NzbgetDownloadProvider(ProviderDefinition definition, IHttpClientFactory httpClientFactory)
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
                    $"Connected to NZBGet {result.Version}",
                    (int)result.ResponseTimeMs);
            }
            
            return ProviderTestResult.Fail(result.Message);
        }
        catch (Exception ex)
        {
            return ProviderTestResult.Fail($"Connection error: {ex.Message}");
        }
    }

    public async Task<ProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var result = await client.TestConnectionAsync(cancellationToken);

            if (result.Success)
            {
                // Also check the status
                var status = await client.GetStatusAsync(cancellationToken);
                if (status != null && status.FreeDiskSpaceMB < 1024)
                {
                    return new ProviderHealth
                    {
                        Status = HealthStatus.Unhealthy,
                        Message = $"Low disk space: {status.FreeDiskSpaceMB}MB free"
                    };
                }

                return new ProviderHealth
                {
                    Status = HealthStatus.Healthy,
                    Message = $"NZBGet {result.Version} connected"
                };
            }

            return new ProviderHealth
            {
                Status = HealthStatus.Unhealthy,
                Message = result.Message
            };
        }
        catch (Exception ex)
        {
            return new ProviderHealth
            {
                Status = HealthStatus.Unhealthy,
                Message = ex.Message
            };
        }
    }

    public async Task<DownloadResult> DownloadAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            
            // Prefer NZB URL if available
            var nzbUrl = candidate.DownloadUrl;
            if (string.IsNullOrEmpty(nzbUrl))
            {
                return DownloadResult.Fail("No NZB URL available");
            }

            var options = new NzbDownloadOptions
            {
                Category = _settings.Category,
                Priority = MapPriority(_settings.DefaultPriority),
                Name = candidate.ReleaseTitle
            };

            var result = await client.AddNzbUrlAsync(nzbUrl, options, cancellationToken);
            
            if (result.Success && !string.IsNullOrEmpty(result.DownloadId))
            {
                return DownloadResult.Ok(result.DownloadId, candidate);
            }
            
            return DownloadResult.Fail(result.ErrorMessage ?? "Failed to add NZB to NZBGet");
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

            if (status == null)
            {
                return new DownloadStatus
                {
                    DownloadId = downloadId,
                    State = DownloadState.Failed,
                    Error = "Download not found"
                };
            }

            return new DownloadStatus
            {
                DownloadId = downloadId,
                CandidateTitle = status.Name,
                State = MapState(status.State),
                Progress = status.ProgressPercent,
                TotalBytes = status.TotalBytes,
                DownloadedBytes = status.DownloadedBytes,
                SpeedBytesPerSecond = status.SpeedBytesPerSecond,
                EstimatedTimeRemaining = status.TimeRemaining,
                OutputPath = status.DownloadPath,
                Error = status.ErrorMessage
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
            return await client.RemoveDownloadAsync(downloadId, deleteFiles: true, cancellationToken);
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

            return queue.Select(status => new DownloadStatus
            {
                DownloadId = status.Id,
                CandidateTitle = status.Name,
                State = MapState(status.State),
                Progress = status.ProgressPercent,
                TotalBytes = status.TotalBytes,
                DownloadedBytes = status.DownloadedBytes,
                SpeedBytesPerSecond = status.SpeedBytesPerSecond,
                EstimatedTimeRemaining = status.TimeRemaining,
                OutputPath = status.DownloadPath,
                Error = status.ErrorMessage
            }).ToList();
        }
        catch
        {
            return Array.Empty<DownloadStatus>();
        }
    }

    private NzbgetClient CreateClient()
    {
        var httpClient = _httpClientFactory.CreateClient("NZBGet");
        return new NzbgetClient(httpClient, _settings);
    }

    private static NzbgetSettings ParseSettings(ProviderDefinition definition)
    {
        // First try to parse from Settings JSON
        if (!string.IsNullOrEmpty(definition.Settings))
        {
            try
            {
                var settings = JsonSerializer.Deserialize<NzbgetSettingsJson>(definition.Settings,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (settings != null && !string.IsNullOrEmpty(settings.Host))
                {
                    return new NzbgetSettings
                    {
                        Host = settings.Host,
                        Port = settings.Port,
                        Username = settings.Username ?? "nzbget",
                        Password = settings.Password ?? "tegbzn6789",
                        Category = settings.Category ?? "comics",
                        UseSsl = settings.UseSsl ?? false,
                        DefaultPriority = settings.Priority ?? NzbgetPriority.Normal,
                        AddPaused = settings.AddPaused ?? false
                    };
                }
            }
            catch
            {
                // Fall through to BaseUrl parsing
            }
        }

        // Try to parse from BaseUrl (legacy format)
        var baseUrl = definition.BaseUrl ?? "";
        var host = "localhost";
        var port = 6789;
        var useSsl = false;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            useSsl = baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            
            var hostPart = baseUrl
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            var colonIndex = hostPart.IndexOf(':');
            if (colonIndex > 0)
            {
                host = hostPart[..colonIndex];
                if (int.TryParse(hostPart[(colonIndex + 1)..], out var parsedPort))
                {
                    port = parsedPort;
                }
            }
            else
            {
                host = hostPart;
            }
        }

        return new NzbgetSettings
        {
            Host = host,
            Port = port,
            Username = definition.Username ?? "nzbget",
            Password = definition.Password ?? "tegbzn6789",
            Category = "comics",
            UseSsl = useSsl,
            DefaultPriority = NzbgetPriority.Normal
        };
    }

    private static NzbPriority MapPriority(NzbgetPriority priority)
    {
        return priority switch
        {
            NzbgetPriority.VeryLow => NzbPriority.Low,
            NzbgetPriority.Low => NzbPriority.Low,
            NzbgetPriority.Normal => NzbPriority.Normal,
            NzbgetPriority.High => NzbPriority.High,
            NzbgetPriority.VeryHigh => NzbPriority.High,
            NzbgetPriority.Force => NzbPriority.Force,
            _ => NzbPriority.Normal
        };
    }

    private static DownloadState MapState(NzbDownloadState state)
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
    /// JSON structure for NZBGet settings stored in ProviderDefinition.Settings.
    /// </summary>
    private class NzbgetSettingsJson
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Category { get; set; }
        public bool? UseSsl { get; set; }
        public NzbgetPriority? Priority { get; set; }
        public bool? AddPaused { get; set; }
    }
}

/// <summary>
/// Factory for creating NZBGet download providers.
/// </summary>
public class NzbgetDownloadProviderFactory
{
    private readonly IServiceProvider _services;

    public NzbgetDownloadProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IDownloadProvider Create(ProviderDefinition definition)
    {
        var httpClientFactory = _services.GetRequiredService<IHttpClientFactory>();
        return new NzbgetDownloadProvider(definition, httpClientFactory);
    }
}
