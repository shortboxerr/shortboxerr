namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for extracting files from various archive formats.
/// Supports ZIP, RAR, 7z, and other common archive types.
/// </summary>
public interface IArchiveExtractor
{
    /// <summary>
    /// Extracts all files from an archive to the specified directory.
    /// </summary>
    /// <param name="archivePath">Path to the archive file.</param>
    /// <param name="destinationDirectory">Directory to extract files to. Created if it doesn't exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the extraction operation.</returns>
    Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath, 
        string destinationDirectory, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts all files from an archive to a sibling directory named "{archiveName}_extracted".
    /// </summary>
    /// <param name="archivePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the extraction operation.</returns>
    Task<ArchiveExtractionResult> ExtractToSiblingDirectoryAsync(
        string archivePath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in an archive without extracting them.
    /// </summary>
    /// <param name="archivePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file paths within the archive.</returns>
    Task<IReadOnlyList<string>> ListFilesAsync(
        string archivePath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file is a supported archive format.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>True if the file is a supported archive format.</returns>
    bool IsSupportedArchive(string filePath);

    /// <summary>
    /// Gets the archive type based on file extension or magic bytes.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The detected archive type, or Unknown if not an archive.</returns>
    ArchiveType GetArchiveType(string filePath);
}

/// <summary>
/// Result of an archive extraction operation.
/// </summary>
public class ArchiveExtractionResult
{
    /// <summary>Whether the extraction was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Path to the archive that was extracted.</summary>
    public required string ArchivePath { get; init; }

    /// <summary>Directory where files were extracted to.</summary>
    public required string DestinationDirectory { get; init; }

    /// <summary>Detected archive type.</summary>
    public ArchiveType ArchiveType { get; init; }

    /// <summary>List of extracted file paths.</summary>
    public List<string> ExtractedFiles { get; init; } = new();

    /// <summary>Number of files extracted.</summary>
    public int FileCount => ExtractedFiles.Count;

    /// <summary>Total size of extracted files in bytes.</summary>
    public long TotalExtractedSize { get; init; }

    /// <summary>Error message if extraction failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether the archive was password-protected.</summary>
    public bool IsPasswordProtected { get; init; }

    /// <summary>Duration of the extraction operation.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Creates a success result.</summary>
    public static ArchiveExtractionResult Succeeded(
        string archivePath, 
        string destinationDirectory, 
        ArchiveType archiveType,
        List<string> extractedFiles, 
        long totalSize,
        TimeSpan duration) => new()
    {
        Success = true,
        ArchivePath = archivePath,
        DestinationDirectory = destinationDirectory,
        ArchiveType = archiveType,
        ExtractedFiles = extractedFiles,
        TotalExtractedSize = totalSize,
        Duration = duration
    };

    /// <summary>Creates a failure result.</summary>
    public static ArchiveExtractionResult Failed(
        string archivePath, 
        string destinationDirectory, 
        string errorMessage, 
        bool isPasswordProtected = false) => new()
    {
        Success = false,
        ArchivePath = archivePath,
        DestinationDirectory = destinationDirectory,
        ErrorMessage = errorMessage,
        IsPasswordProtected = isPasswordProtected
    };
}

/// <summary>
/// Types of supported archive formats.
/// </summary>
public enum ArchiveType
{
    /// <summary>Not an archive or unknown format.</summary>
    Unknown = 0,

    /// <summary>ZIP archive (.zip, .cbz).</summary>
    Zip = 1,

    /// <summary>RAR archive (.rar, .cbr).</summary>
    Rar = 2,

    /// <summary>7-Zip archive (.7z).</summary>
    SevenZip = 3,

    /// <summary>TAR archive (.tar).</summary>
    Tar = 4,

    /// <summary>GZip compressed (.gz, .tar.gz, .tgz).</summary>
    GZip = 5,

    /// <summary>BZip2 compressed (.bz2, .tar.bz2).</summary>
    BZip2 = 6
}
