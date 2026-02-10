using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.GZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shortboxerr.Core.Services;

// Alias to disambiguate from SharpCompress.Common.ArchiveType
using CoreArchiveType = Shortboxerr.Core.Services.ArchiveType;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Archive extractor implementation using SharpCompress.
/// Supports ZIP, RAR, 7z, TAR, GZip, and BZip2 formats.
/// </summary>
public class ArchiveExtractor : IArchiveExtractor
{
    private readonly ILogger<ArchiveExtractor>? _logger;

    // Supported archive extensions
    private static readonly Dictionary<string, CoreArchiveType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".zip", CoreArchiveType.Zip },
        { ".cbz", CoreArchiveType.Zip },
        { ".rar", CoreArchiveType.Rar },
        { ".cbr", CoreArchiveType.Rar },
        { ".7z", CoreArchiveType.SevenZip },
        { ".tar", CoreArchiveType.Tar },
        { ".gz", CoreArchiveType.GZip },
        { ".tgz", CoreArchiveType.GZip },
        { ".bz2", CoreArchiveType.BZip2 },
    };

    // Magic bytes for format detection
    private static readonly byte[] ZipMagic = { 0x50, 0x4B }; // PK
    private static readonly byte[] RarMagic = { 0x52, 0x61, 0x72, 0x21 }; // Rar!
    private static readonly byte[] SevenZipMagic = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }; // 7z
    private static readonly byte[] GZipMagic = { 0x1F, 0x8B }; // GZip

    public ArchiveExtractor(ILogger<ArchiveExtractor>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            return ArchiveExtractionResult.Failed(archivePath, destinationDirectory, "Archive file not found");
        }

        var archiveType = GetArchiveType(archivePath);
        if (archiveType == CoreArchiveType.Unknown)
        {
            return ArchiveExtractionResult.Failed(archivePath, destinationDirectory, "Unsupported archive format");
        }

        var stopwatch = Stopwatch.StartNew();
        var extractedFiles = new List<string>();
        long totalSize = 0;

        try
        {
            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Use SharpCompress to extract based on archive type
            await Task.Run(() =>
            {
                using var archive = OpenArchive(archivePath, archiveType);
                
                if (archive == null)
                {
                    throw new InvalidOperationException($"Failed to open archive: {archivePath}");
                }

                // Check for password protection (RAR/7z can be encrypted)
                if (archive.Entries.Any(e => e.IsEncrypted))
                {
                    throw new PasswordProtectedArchiveException("Archive is password-protected");
                }

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entryPath = Path.Combine(destinationDirectory, SanitizePath(entry.Key ?? "unknown"));
                    var entryDir = Path.GetDirectoryName(entryPath);
                    
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    entry.WriteToFile(entryPath, new ExtractionOptions
                    {
                        ExtractFullPath = false,
                        Overwrite = true
                    });

                    extractedFiles.Add(entryPath);
                    totalSize += entry.Size;

                    _logger?.LogDebug("Extracted: {Entry} ({Size} bytes)", entry.Key, entry.Size);
                }
            }, cancellationToken);

            stopwatch.Stop();

            _logger?.LogInformation(
                "Extracted {Count} files ({Size:N0} bytes) from {Archive} in {Duration}ms",
                extractedFiles.Count,
                totalSize,
                Path.GetFileName(archivePath),
                stopwatch.ElapsedMilliseconds);

            return ArchiveExtractionResult.Succeeded(
                archivePath,
                destinationDirectory,
                archiveType,
                extractedFiles,
                totalSize,
                stopwatch.Elapsed);
        }
        catch (PasswordProtectedArchiveException)
        {
            _logger?.LogWarning("Archive is password-protected: {Archive}", archivePath);
            return ArchiveExtractionResult.Failed(archivePath, destinationDirectory, "Archive is password-protected", isPasswordProtected: true);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Archive extraction cancelled: {Archive}", archivePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting archive: {Archive}", archivePath);
            return ArchiveExtractionResult.Failed(archivePath, destinationDirectory, ex.Message);
        }
    }

    public async Task<ArchiveExtractionResult> ExtractToSiblingDirectoryAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var archiveDir = Path.GetDirectoryName(archivePath) ?? "";
        var archiveName = Path.GetFileNameWithoutExtension(archivePath);
        var destinationDirectory = Path.Combine(archiveDir, $"{archiveName}_extracted");

        return await ExtractAsync(archivePath, destinationDirectory, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            return Array.Empty<string>();
        }

        var archiveType = GetArchiveType(archivePath);
        if (archiveType == CoreArchiveType.Unknown)
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();

        await Task.Run(() =>
        {
            using var archive = OpenArchive(archivePath, archiveType);
            
            if (archive != null)
            {
                files.AddRange(archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Select(e => e.Key ?? "unknown"));
            }
        }, cancellationToken);

        return files;
    }

    public bool IsSupportedArchive(string filePath)
    {
        return GetArchiveType(filePath) != CoreArchiveType.Unknown;
    }

    public CoreArchiveType GetArchiveType(string filePath)
    {
        // First, try by extension
        var extension = Path.GetExtension(filePath);
        if (ExtensionMap.TryGetValue(extension, out var type))
        {
            return type;
        }

        // If extension doesn't match, try magic bytes
        if (File.Exists(filePath))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                return DetectByMagicBytes(stream);
            }
            catch
            {
                // Ignore file read errors
            }
        }

        return CoreArchiveType.Unknown;
    }

    private static CoreArchiveType DetectByMagicBytes(Stream stream)
    {
        var buffer = new byte[8];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0; // Reset for subsequent reads

        if (bytesRead < 2)
            return CoreArchiveType.Unknown;

        // Check ZIP (PK)
        if (buffer[0] == ZipMagic[0] && buffer[1] == ZipMagic[1])
            return CoreArchiveType.Zip;

        // Check RAR (Rar!)
        if (bytesRead >= 4 && 
            buffer[0] == RarMagic[0] && buffer[1] == RarMagic[1] && 
            buffer[2] == RarMagic[2] && buffer[3] == RarMagic[3])
            return CoreArchiveType.Rar;

        // Check 7z
        if (bytesRead >= 6 &&
            buffer[0] == SevenZipMagic[0] && buffer[1] == SevenZipMagic[1] &&
            buffer[2] == SevenZipMagic[2] && buffer[3] == SevenZipMagic[3] &&
            buffer[4] == SevenZipMagic[4] && buffer[5] == SevenZipMagic[5])
            return CoreArchiveType.SevenZip;

        // Check GZip
        if (buffer[0] == GZipMagic[0] && buffer[1] == GZipMagic[1])
            return CoreArchiveType.GZip;

        return CoreArchiveType.Unknown;
    }

    private static IArchive? OpenArchive(string archivePath, CoreArchiveType archiveType)
    {
        return archiveType switch
        {
            CoreArchiveType.Zip => ZipArchive.Open(archivePath),
            CoreArchiveType.Rar => RarArchive.Open(archivePath),
            CoreArchiveType.SevenZip => SevenZipArchive.Open(archivePath),
            CoreArchiveType.Tar => TarArchive.Open(archivePath),
            CoreArchiveType.GZip => GZipArchive.Open(archivePath),
            _ => ArchiveFactory.Open(archivePath) // Let SharpCompress detect
        };
    }

    private static string SanitizePath(string entryPath)
    {
        // Remove any leading slashes or drive letters
        var sanitized = entryPath.TrimStart('/', '\\');
        
        // Replace any invalid path characters
        foreach (var c in Path.GetInvalidPathChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }

        // Prevent directory traversal attacks
        sanitized = sanitized.Replace("..", "__");

        return sanitized;
    }
}

/// <summary>
/// Exception thrown when trying to extract a password-protected archive.
/// </summary>
public class PasswordProtectedArchiveException : Exception
{
    public PasswordProtectedArchiveException(string message) : base(message) { }
}
