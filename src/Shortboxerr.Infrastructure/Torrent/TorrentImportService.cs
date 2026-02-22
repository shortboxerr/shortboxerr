using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.Torrent;

namespace Shortboxerr.Infrastructure.Torrent;

/// <summary>
/// Service for handling completed torrent imports.
/// </summary>
public class TorrentImportService : ITorrentImportService
{
    private readonly ILogger<TorrentImportService>? _logger;
    private readonly ISettingsService _settingsService;
    private readonly Func<TorrentClientType, ITorrentClient?> _clientFactory;

    private const string SettingsKey = "torrent_import";

    public TorrentImportService(
        ISettingsService settingsService,
        Func<TorrentClientType, ITorrentClient?> clientFactory,
        ILogger<TorrentImportService>? logger = null)
    {
        _settingsService = settingsService;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TorrentImportResult>> ProcessCompletedTorrentsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.AutoImportEnabled)
        {
            _logger?.LogDebug("Auto-import is disabled");
            return Array.Empty<TorrentImportResult>();
        }

        var results = new List<TorrentImportResult>();

        foreach (var clientType in Enum.GetValues<TorrentClientType>())
        {
            var client = _clientFactory(clientType);
            if (client == null) continue;

            try
            {
                var torrents = await client.GetAllTorrentsAsync(cancellationToken);
                foreach (var torrent in torrents.Where(t => t.IsCompleted))
                {
                    var result = await ProcessTorrentInternalAsync(torrent, client, settings, cancellationToken);
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to process torrents from {ClientType}", clientType);
            }
        }

        return results;
    }

    public async Task<TorrentImportResult> ProcessTorrentAsync(
        string hash,
        TorrentClientType clientType,
        CancellationToken cancellationToken = default)
    {
        var client = _clientFactory(clientType);
        if (client == null)
        {
            return TorrentImportResult.Failed(hash, hash, clientType, $"Client {clientType} not configured");
        }

        var status = await client.GetStatusAsync(hash, cancellationToken);
        if (status == null)
        {
            return TorrentImportResult.Failed(hash, hash, clientType, "Torrent not found");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        return await ProcessTorrentInternalAsync(status, client, settings, cancellationToken);
    }

    public Task<TorrentReadyResult> CheckTorrentReadyAsync(
        TorrentStatus status,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default)
    {
        // Check if download is complete
        if (!status.IsCompleted)
        {
            return Task.FromResult(TorrentReadyResult.NotReady(TorrentImportStatus.NotCompleted));
        }

        // Check category filter
        if (!string.IsNullOrEmpty(settings.Category) &&
            !string.Equals(status.Category, settings.Category, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(TorrentReadyResult.NotReady(TorrentImportStatus.WrongCategory));
        }

        // Check seeding requirements
        var ratioMet = settings.MinimumSeedRatio <= 0 || status.Ratio >= settings.MinimumSeedRatio;
        var timeMet = settings.MinimumSeedTimeMinutes <= 0 || GetMinutesSeeded(status) >= settings.MinimumSeedTimeMinutes;

        if (settings.SeedRequirementsOrMode)
        {
            // OR mode: either requirement is sufficient
            if (!ratioMet && !timeMet)
            {
                if (settings.MinimumSeedRatio > 0)
                {
                    return Task.FromResult(TorrentReadyResult.NotReady(
                        TorrentImportStatus.SeedingRatioNotMet,
                        status.Ratio,
                        settings.MinimumSeedRatio,
                        GetMinutesSeeded(status),
                        settings.MinimumSeedTimeMinutes));
                }
                return Task.FromResult(TorrentReadyResult.NotReady(
                    TorrentImportStatus.SeedingTimeNotMet,
                    status.Ratio,
                    settings.MinimumSeedRatio,
                    GetMinutesSeeded(status),
                    settings.MinimumSeedTimeMinutes));
            }
        }
        else
        {
            // AND mode: both requirements must be met
            if (!ratioMet)
            {
                return Task.FromResult(TorrentReadyResult.NotReady(
                    TorrentImportStatus.SeedingRatioNotMet,
                    status.Ratio,
                    settings.MinimumSeedRatio));
            }
            if (!timeMet)
            {
                return Task.FromResult(TorrentReadyResult.NotReady(
                    TorrentImportStatus.SeedingTimeNotMet,
                    minutesSeeded: GetMinutesSeeded(status),
                    requiredMinutes: settings.MinimumSeedTimeMinutes));
            }
        }

        return Task.FromResult(TorrentReadyResult.Ready());
    }

    public async Task<TorrentFileImportResult> ImportFilesAsync(
        TorrentStatus status,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default)
    {
        var sourcePath = status.ContentPath ?? status.SavePath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            return TorrentFileImportResult.Error("No content path available");
        }

        var destinationPath = settings.DestinationPath;
        if (string.IsNullOrEmpty(destinationPath))
        {
            return TorrentFileImportResult.Error("No destination path configured");
        }

        try
        {
            var files = GetFilesToImport(sourcePath, settings);
            if (files.Count == 0)
            {
                _logger?.LogDebug("No matching files found in {Path}", sourcePath);
                return TorrentFileImportResult.NoFiles();
            }

            var importedFiles = new List<string>();
            long totalBytes = 0;
            var usedHardLinks = settings.TransferMode == FileTransferMode.HardLink;

            foreach (var sourceFile in files)
            {
                var relativePath = settings.PreserveFolderStructure
                    ? GetRelativePath(sourcePath, sourceFile)
                    : Path.GetFileName(sourceFile);

                var destFile = Path.Combine(destinationPath, relativePath);
                var destDir = Path.GetDirectoryName(destFile);

                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                var fileInfo = new FileInfo(sourceFile);
                var transferred = await TransferFileAsync(sourceFile, destFile, settings.TransferMode, cancellationToken);

                if (transferred)
                {
                    importedFiles.Add(destFile);
                    totalBytes += fileInfo.Length;
                    _logger?.LogDebug("Imported: {Source} -> {Dest}", sourceFile, destFile);
                }
                else if (settings.TransferMode == FileTransferMode.HardLink)
                {
                    // Fallback to copy if hard link fails
                    usedHardLinks = false;
                    transferred = await TransferFileAsync(sourceFile, destFile, FileTransferMode.Copy, cancellationToken);
                    if (transferred)
                    {
                        importedFiles.Add(destFile);
                        totalBytes += fileInfo.Length;
                        _logger?.LogDebug("Imported (copy fallback): {Source} -> {Dest}", sourceFile, destFile);
                    }
                }
            }

            _logger?.LogInformation("Imported {Count} files ({Bytes} bytes) from {Name}",
                importedFiles.Count, totalBytes, status.Name);

            return TorrentFileImportResult.Succeeded(importedFiles.Count, totalBytes, importedFiles, usedHardLinks);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import files from {Path}", sourcePath);
            return TorrentFileImportResult.Error(ex.Message);
        }
    }

    public async Task<bool> CleanupTorrentAsync(
        string hash,
        TorrentClientType clientType,
        TorrentImportSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.RemoveAfterImport)
        {
            return true;
        }

        var client = _clientFactory(clientType);
        if (client == null)
        {
            _logger?.LogWarning("Cannot cleanup torrent {Hash}: client {ClientType} not available", hash, clientType);
            return false;
        }

        try
        {
            var removed = await client.RemoveTorrentAsync(hash, settings.DeleteFilesOnRemove, cancellationToken);
            if (removed)
            {
                _logger?.LogInformation("Removed torrent {Hash} after import (deleteFiles: {DeleteFiles})",
                    hash, settings.DeleteFilesOnRemove);
            }
            return removed;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove torrent {Hash}", hash);
            return false;
        }
    }

    public async Task<TorrentImportSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsService.GetAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrEmpty(json))
        {
            return new TorrentImportSettings();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TorrentImportSettings>(json)
                   ?? new TorrentImportSettings();
        }
        catch
        {
            return new TorrentImportSettings();
        }
    }

    public async Task SaveSettingsAsync(TorrentImportSettings settings, CancellationToken cancellationToken = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        await _settingsService.SetAsync(SettingsKey, json, cancellationToken);
    }

    #region Private Methods

    private async Task<TorrentImportResult> ProcessTorrentInternalAsync(
        TorrentStatus status,
        ITorrentClient client,
        TorrentImportSettings settings,
        CancellationToken cancellationToken)
    {
        var clientType = client.ClientType;

        // Check if ready for import
        var readyResult = await CheckTorrentReadyAsync(status, settings, cancellationToken);
        if (!readyResult.IsReady)
        {
            _logger?.LogDebug("Torrent {Name} not ready: {Status}", status.Name, readyResult.Status);
            return TorrentImportResult.Skipped(status.Hash, status.Name, clientType, readyResult.Status);
        }

        // Import files
        var importResult = await ImportFilesAsync(status, settings, cancellationToken);
        if (!importResult.Success)
        {
            return TorrentImportResult.Failed(status.Hash, status.Name, clientType,
                importResult.ErrorMessage ?? "Import failed");
        }

        if (importResult.FilesImported == 0)
        {
            return TorrentImportResult.Skipped(status.Hash, status.Name, clientType,
                TorrentImportStatus.NoMatchingFiles);
        }

        // Cleanup torrent if configured
        var removed = await CleanupTorrentAsync(status.Hash, clientType, settings, cancellationToken);

        return TorrentImportResult.Imported(
            status.Hash,
            status.Name,
            clientType,
            importResult.FilesImported,
            importResult.BytesTransferred,
            removed);
    }

    private static int GetMinutesSeeded(TorrentStatus status)
    {
        if (status.CompletedOn == null) return 0;
        return (int)(DateTime.UtcNow - status.CompletedOn.Value).TotalMinutes;
    }

    private List<string> GetFilesToImport(string path, TorrentImportSettings settings)
    {
        var files = new List<string>();

        if (File.Exists(path))
        {
            // Single file torrent
            if (ShouldImportFile(path, settings))
            {
                files.Add(path);
            }
        }
        else if (Directory.Exists(path))
        {
            // Multi-file torrent
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (ShouldImportFile(file, settings))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    private static bool ShouldImportFile(string filePath, TorrentImportSettings settings)
    {
        if (settings.FileExtensions.Count == 0)
        {
            return true;
        }

        var extension = Path.GetExtension(filePath);
        return settings.FileExtensions.Any(ext =>
            ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (File.Exists(basePath))
        {
            return Path.GetFileName(fullPath);
        }

        var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        var relativeUri = baseUri.MakeRelativeUri(fullUri);
        return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    private async Task<bool> TransferFileAsync(
        string source,
        string destination,
        FileTransferMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(destination))
            {
                _logger?.LogDebug("Destination already exists: {Path}", destination);
                return true;
            }

            switch (mode)
            {
                case FileTransferMode.Copy:
                    await CopyFileAsync(source, destination, cancellationToken);
                    return true;

                case FileTransferMode.HardLink:
                    return TryCreateHardLink(source, destination);

                case FileTransferMode.Move:
                    File.Move(source, destination);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to transfer {Source} to {Dest} using {Mode}",
                source, destination, mode);
            return false;
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920; // 80KB buffer
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        await using var destStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, true);
        await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
    }

    private bool TryCreateHardLink(string source, string destination)
    {
        try
        {
            // Use File.CreateHardLink on Windows, or ln on Unix
            if (OperatingSystem.IsWindows())
            {
                return CreateHardLinkWindows(destination, source);
            }
            else
            {
                return CreateHardLinkUnix(source, destination);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Hard link creation failed for {Source}", source);
            return false;
        }
    }

    private static bool CreateHardLinkWindows(string destination, string source)
    {
        // P/Invoke would be needed for actual implementation
        // For now, return false to trigger copy fallback
        return false;
    }

    private static bool CreateHardLinkUnix(string source, string destination)
    {
        try
        {
            // On .NET 7+, we could use File.CreateHardLink
            // For now, try using Mono.Unix or shell command
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"\"{source}\" \"{destination}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
