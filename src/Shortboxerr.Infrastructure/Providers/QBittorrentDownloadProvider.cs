using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Torrent;
using Shortboxerr.Infrastructure.Torrent;

namespace Shortboxerr.Infrastructure.Providers;

/// <summary>
/// qBittorrent download provider that wraps the qBittorrent client.
/// Implements IDownloadProvider to integrate with the unified provider system.
/// </summary>
public class QBittorrentDownloadProvider : IDownloadProvider
{
    private readonly ProviderDefinition _definition;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QBittorrentSettings _settings;

    public QBittorrentDownloadProvider(ProviderDefinition definition, IHttpClientFactory httpClientFactory)
    {
        _definition = definition;
        _httpClientFactory = httpClientFactory;
        _settings = ParseSettings(definition);
    }

    public int Id => _definition.Id;
    public string Name => _definition.Name;
    public ProviderType Type => ProviderType.Torrent;
    public bool IsEnabled { get => _definition.IsEnabled; set => _definition.IsEnabled = value; }
    public int Priority { get => _definition.Priority; set => _definition.Priority = value; }
    public IReadOnlyList<string> SupportedProtocols => new[] { "torrent", "magnet" };

    public async Task<ProviderTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = CreateClient();
            var result = await client.TestConnectionAsync(cancellationToken);

            if (result.Success)
            {
                return ProviderTestResult.Ok(
                    $"Connected to qBittorrent {result.Version}",
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
                // Also check disk space
                var diskSpace = await client.GetDiskSpaceAsync(cancellationToken);
                if (diskSpace?.IsLow == true)
                {
                    return new ProviderHealth
                    {
                        Status = HealthStatus.Degraded,
                        Message = $"Low disk space: {diskSpace.FreeBytes / (1024 * 1024 * 1024)}GB free"
                    };
                }

                return new ProviderHealth
                {
                    Status = HealthStatus.Healthy,
                    Message = $"qBittorrent {result.Version} connected"
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

            var downloadUrl = candidate.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                return DownloadResult.Fail("No download URL available");
            }

            var options = new TorrentAddOptions
            {
                Category = _settings.Category,
                AddPaused = _settings.AddPaused,
                SavePath = _settings.SavePath,
                RatioLimit = _settings.DefaultRatioLimit,
                SeedingTimeLimitMinutes = _settings.DefaultSeedingTimeLimit,
                SequentialDownload = _settings.SequentialDownload,
                FirstLastPiecePriority = _settings.FirstLastPiecePriority
            };

            TorrentAddResult result;

            // Determine if it's a magnet link or torrent URL
            if (downloadUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                result = await client.AddTorrentMagnetAsync(downloadUrl, options, cancellationToken);
            }
            else
            {
                result = await client.AddTorrentUrlAsync(downloadUrl, options, cancellationToken);
            }

            if (result.Success && !string.IsNullOrEmpty(result.Hash))
            {
                return DownloadResult.Ok(result.Hash, candidate);
            }

            return DownloadResult.Fail(result.ErrorMessage ?? "Failed to add torrent to qBittorrent");
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
            var status = await client.GetStatusAsync(downloadId, cancellationToken);

            if (status == null)
            {
                return new DownloadStatus
                {
                    DownloadId = downloadId,
                    State = DownloadState.Failed,
                    Error = "Torrent not found"
                };
            }

            return new DownloadStatus
            {
                DownloadId = downloadId,
                CandidateTitle = status.Name,
                State = MapState(status.State),
                Progress = status.Progress,
                TotalBytes = status.TotalBytes,
                DownloadedBytes = status.DownloadedBytes,
                SpeedBytesPerSecond = status.DownloadSpeedBps,
                EstimatedTimeRemaining = status.TimeRemaining,
                OutputPath = status.ContentPath ?? status.SavePath,
                Error = status.Error
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
            return await client.RemoveTorrentAsync(downloadId, deleteFiles: true, cancellationToken);
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
            var torrents = await client.GetAllTorrentsAsync(cancellationToken);

            return torrents
                .Where(t => t.State != TorrentState.Completed && t.State != TorrentState.Seeding)
                .Select(status => new DownloadStatus
                {
                    DownloadId = status.Hash,
                    CandidateTitle = status.Name,
                    State = MapState(status.State),
                    Progress = status.Progress,
                    TotalBytes = status.TotalBytes,
                    DownloadedBytes = status.DownloadedBytes,
                    SpeedBytesPerSecond = status.DownloadSpeedBps,
                    EstimatedTimeRemaining = status.TimeRemaining,
                    OutputPath = status.ContentPath ?? status.SavePath,
                    Error = status.Error
                }).ToList();
        }
        catch
        {
            return Array.Empty<DownloadStatus>();
        }
    }

    private QBittorrentClient CreateClient()
    {
        var httpClient = _httpClientFactory.CreateClient("qBittorrent");
        return new QBittorrentClient(httpClient, _settings);
    }

    private static QBittorrentSettings ParseSettings(ProviderDefinition definition)
    {
        // First try to parse from Settings JSON
        if (!string.IsNullOrEmpty(definition.Settings))
        {
            try
            {
                var settings = JsonSerializer.Deserialize<QBittorrentSettingsJson>(definition.Settings,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (settings != null && !string.IsNullOrEmpty(settings.Host))
                {
                    return new QBittorrentSettings
                    {
                        Host = settings.Host,
                        Port = settings.Port,
                        Username = settings.Username,
                        Password = settings.Password,
                        Category = settings.Category ?? "comics",
                        SavePath = settings.SavePath,
                        UseSsl = settings.UseSsl ?? false,
                        AddPaused = settings.AddPaused ?? false,
                        DefaultRatioLimit = settings.RatioLimit,
                        DefaultSeedingTimeLimit = settings.SeedingTimeLimit,
                        SequentialDownload = settings.SequentialDownload ?? false,
                        FirstLastPiecePriority = settings.FirstLastPiecePriority ?? false
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
        var port = 8080;
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

        return new QBittorrentSettings
        {
            Host = host,
            Port = port,
            Username = definition.Username,
            Password = definition.Password,
            Category = "comics",
            UseSsl = useSsl
        };
    }

    private static DownloadState MapState(TorrentState state)
    {
        return state switch
        {
            TorrentState.Queued => DownloadState.Queued,
            TorrentState.Downloading => DownloadState.Downloading,
            TorrentState.Paused => DownloadState.Paused,
            TorrentState.Checking => DownloadState.Processing,
            TorrentState.Seeding => DownloadState.Completed,
            TorrentState.Completed => DownloadState.Completed,
            TorrentState.Error => DownloadState.Failed,
            TorrentState.Stalled => DownloadState.Downloading,
            TorrentState.FetchingMetadata => DownloadState.Downloading,
            TorrentState.Moving => DownloadState.Processing,
            _ => DownloadState.Queued
        };
    }

    /// <summary>
    /// JSON structure for qBittorrent settings stored in ProviderDefinition.Settings.
    /// </summary>
    private class QBittorrentSettingsJson
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Category { get; set; }
        public string? SavePath { get; set; }
        public bool? UseSsl { get; set; }
        public bool? AddPaused { get; set; }
        public double? RatioLimit { get; set; }
        public int? SeedingTimeLimit { get; set; }
        public bool? SequentialDownload { get; set; }
        public bool? FirstLastPiecePriority { get; set; }
    }
}

/// <summary>
/// Factory for creating qBittorrent download providers.
/// </summary>
public class QBittorrentDownloadProviderFactory
{
    private readonly IServiceProvider _services;

    public QBittorrentDownloadProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IDownloadProvider Create(ProviderDefinition definition)
    {
        var httpClientFactory = _services.GetRequiredService<IHttpClientFactory>();
        return new QBittorrentDownloadProvider(definition, httpClientFactory);
    }
}
