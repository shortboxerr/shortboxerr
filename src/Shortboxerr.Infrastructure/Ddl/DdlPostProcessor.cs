using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Post-processor for downloaded files.
/// Implements Mylar3's zip_zip extraction behavior and file organization.
/// </summary>
public class DdlPostProcessor : IDdlPostProcessor
{
    private readonly ILogger<DdlPostProcessor>? _logger;
    
    /// <summary>
    /// Comic file extensions to recognize.
    /// </summary>
    private static readonly string[] ComicExtensions = { ".cbz", ".cbr", ".cb7", ".pdf" };
    
    public DdlPostProcessor(ILogger<DdlPostProcessor>? logger = null)
    {
        _logger = logger;
    }
    
    public async Task<DdlPostProcessResult> ProcessAsync(string filePath, DdlPostProcessOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DdlPostProcessOptions();
        
        if (!File.Exists(filePath))
        {
            return DdlPostProcessResult.Failed($"File not found: {filePath}", filePath);
        }
        
        // Check if file is a zip that needs extraction (Mylar3's zip_zip behavior)
        if (options.ExtractZip && NeedsExtraction(filePath))
        {
            return await ExtractZipAsync(filePath, options, cancellationToken);
        }
        
        // No processing needed
        return DdlPostProcessResult.NoProcessingNeeded(filePath);
    }
    
    public bool NeedsExtraction(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Only extract regular .zip files, not comic archives
        return extension == ".zip" && !IsComicArchive(filePath);
    }
    
    /// <summary>
    /// Extract a zip file (Mylar3's zip_zip method).
    /// </summary>
    private Task<DdlPostProcessResult> ExtractZipAsync(string zipPath, DdlPostProcessOptions options, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var extractedFiles = new List<string>();
            string? outputPath = null;
            
            try
            {
                // Determine extraction destination
                var zipFileName = Path.GetFileNameWithoutExtension(zipPath);
                var zipDirectory = Path.GetDirectoryName(zipPath) ?? ".";
                var extractDir = options.ExtractDestination ?? Path.Combine(zipDirectory, zipFileName);
                
                _logger?.LogInformation("Zip file detected. Unzipping into: {Path}", extractDir);
                
                // Create extraction directory
                if (!Directory.Exists(extractDir))
                {
                    Directory.CreateDirectory(extractDir);
                }
                
                // Extract
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        
                        // Skip directories
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }
                        
                        // Filter by extension if specified
                        if (options.KeepExtensions != null)
                        {
                            var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                            if (!options.KeepExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }
                        
                        // Determine destination path
                        string destPath;
                        if (options.FlattenDirectories)
                        {
                            destPath = Path.Combine(extractDir, entry.Name);
                        }
                        else
                        {
                            destPath = Path.Combine(extractDir, entry.FullName);
                        }
                        
                        // Ensure destination directory exists
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        
                        // Extract file
                        entry.ExtractToFile(destPath, overwrite: true);
                        extractedFiles.Add(destPath);
                        
                        _logger?.LogDebug("Extracted: {File}", entry.FullName);
                    }
                }
                
                // Determine output path
                if (extractedFiles.Count == 1)
                {
                    outputPath = extractedFiles[0];
                    
                    // Rename if requested
                    if (!string.IsNullOrEmpty(options.RenameExtractedFile))
                    {
                        var renamedPath = Path.Combine(zipDirectory, options.RenameExtractedFile);
                        if (File.Exists(renamedPath))
                        {
                            File.Delete(renamedPath);
                        }
                        File.Move(outputPath, renamedPath);
                        extractedFiles[0] = renamedPath;
                        outputPath = renamedPath;
                    }
                }
                else if (extractedFiles.Count > 1)
                {
                    outputPath = extractDir;
                }
                
                // Delete original zip if requested
                var deleted = false;
                if (options.DeleteZipAfterExtract && extractedFiles.Count > 0)
                {
                    try
                    {
                        File.Delete(zipPath);
                        deleted = true;
                        _logger?.LogDebug("Deleted zip file after extraction: {Path}", zipPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Unable to delete zip file after extraction: {Path}", zipPath);
                    }
                }
                
                _logger?.LogInformation("Extracted {Count} files from zip", extractedFiles.Count);
                
                return DdlPostProcessResult.Succeeded(
                    outputPath ?? extractDir,
                    DdlPostProcessType.ZipExtracted,
                    extractedFiles,
                    zipPath,
                    deleted
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to extract zip: {Path}", zipPath);
                return DdlPostProcessResult.Failed($"Extraction failed: {ex.Message}", zipPath);
            }
        }, cancellationToken);
    }
    
    /// <summary>
    /// Check if a file is a comic archive (as opposed to a regular zip to extract).
    /// CBZ files are ZIP files but shouldn't be extracted.
    /// </summary>
    private static bool IsComicArchive(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return ComicExtensions.Contains(extension);
    }
}
