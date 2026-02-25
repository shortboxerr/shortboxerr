namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Service for post-processing downloaded files.
/// Handles zip extraction (Mylar3's zip_zip behavior), file renaming, and organization.
/// </summary>
public interface IDdlPostProcessor
{
    /// <summary>
    /// Process a downloaded file (extract if zip, organize, etc.)
    /// </summary>
    /// <param name="filePath">Path to downloaded file</param>
    /// <param name="options">Processing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of post-processing</returns>
    Task<DdlPostProcessResult> ProcessAsync(string filePath, DdlPostProcessOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a file needs extraction.
    /// </summary>
    bool NeedsExtraction(string filePath);
}

/// <summary>
/// Options for post-processing downloaded files.
/// </summary>
public class DdlPostProcessOptions
{
    /// <summary>
    /// Whether to extract zip files (Mylar3's AutoExtractZip behavior).
    /// </summary>
    public bool ExtractZip { get; set; } = true;
    
    /// <summary>
    /// Whether to delete the zip file after extraction.
    /// </summary>
    public bool DeleteZipAfterExtract { get; set; } = true;
    
    /// <summary>
    /// Target directory for extracted files (if different from zip location).
    /// </summary>
    public string? ExtractDestination { get; set; }
    
    /// <summary>
    /// Whether to flatten directory structure during extraction.
    /// </summary>
    public bool FlattenDirectories { get; set; } = false;
    
    /// <summary>
    /// File extensions to keep during extraction (null = keep all).
    /// </summary>
    public string[]? KeepExtensions { get; set; }
    
    /// <summary>
    /// Custom filename for single-file extraction result.
    /// </summary>
    public string? RenameExtractedFile { get; set; }
}

/// <summary>
/// Result of post-processing a downloaded file.
/// </summary>
public class DdlPostProcessResult
{
    /// <summary>
    /// Whether post-processing succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Path to the final processed file(s).
    /// </summary>
    public string? OutputPath { get; init; }
    
    /// <summary>
    /// List of extracted files (if extraction was performed).
    /// </summary>
    public IReadOnlyList<string> ExtractedFiles { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Type of processing that was performed.
    /// </summary>
    public DdlPostProcessType ProcessType { get; init; }
    
    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Original file path before processing.
    /// </summary>
    public string? OriginalPath { get; init; }
    
    /// <summary>
    /// Whether the original file was deleted.
    /// </summary>
    public bool OriginalDeleted { get; init; }
    
    public static DdlPostProcessResult Succeeded(string outputPath, DdlPostProcessType processType, IReadOnlyList<string>? extractedFiles = null, string? originalPath = null, bool originalDeleted = false)
    {
        return new DdlPostProcessResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessType = processType,
            ExtractedFiles = extractedFiles ?? Array.Empty<string>(),
            OriginalPath = originalPath,
            OriginalDeleted = originalDeleted
        };
    }
    
    public static DdlPostProcessResult Failed(string errorMessage, string? originalPath = null)
    {
        return new DdlPostProcessResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ProcessType = DdlPostProcessType.None,
            OriginalPath = originalPath
        };
    }
    
    public static DdlPostProcessResult NoProcessingNeeded(string filePath)
    {
        return new DdlPostProcessResult
        {
            Success = true,
            OutputPath = filePath,
            ProcessType = DdlPostProcessType.None,
            OriginalPath = filePath
        };
    }
}

/// <summary>
/// Type of post-processing performed.
/// </summary>
public enum DdlPostProcessType
{
    /// <summary>
    /// No processing performed.
    /// </summary>
    None,
    
    /// <summary>
    /// File was extracted from zip archive.
    /// </summary>
    ZipExtracted,
    
    /// <summary>
    /// File was renamed/moved.
    /// </summary>
    Renamed,
    
    /// <summary>
    /// Multiple processing steps performed.
    /// </summary>
    Multiple
}
