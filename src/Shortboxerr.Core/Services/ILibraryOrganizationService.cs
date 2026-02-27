namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for reorganizing library files to match current naming format settings.
/// Mimics Sonarr/Radarr "Organize &amp; Rename" functionality.
/// </summary>
public interface ILibraryOrganizationService
{
    /// <summary>
    /// Gets a preview of how series folders would be renamed for the given series IDs.
    /// </summary>
    /// <param name="seriesIds">Series IDs to preview. If empty, previews all series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of series rename previews.</returns>
    Task<IReadOnlyList<SeriesRenamePreview>> GetSeriesRenamePreviewsAsync(
        int[] seriesIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preview of how a single series folder would be renamed.
    /// </summary>
    /// <param name="seriesId">Series ID to preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Series rename preview, or null if series not found.</returns>
    Task<SeriesRenamePreview?> GetSeriesRenamePreviewAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the rename operation for the given series IDs.
    /// </summary>
    /// <param name="seriesIds">Series IDs to rename. Must not be empty.</param>
    /// <param name="dryRun">If true, simulates the operation without actually modifying files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of execution results.</returns>
    Task<IReadOnlyList<SeriesRenameResult>> ExecuteSeriesRenameAsync(
        int[] seriesIds,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the rename operation for a single series.
    /// </summary>
    /// <param name="seriesId">Series ID to rename.</param>
    /// <param name="dryRun">If true, simulates the operation without actually modifying files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result for the series.</returns>
    Task<SeriesRenameResult> ExecuteSeriesRenameAsync(
        int seriesId,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets path mismatch status for multiple series.
    /// This is a lightweight check that doesn't include file details.
    /// </summary>
    /// <param name="seriesIds">Series IDs to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping series ID to path mismatch info.</returns>
    Task<IReadOnlyDictionary<int, PathMismatchInfo>> GetPathMismatchStatusAsync(
        int[] seriesIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight path mismatch info for series list display.
/// </summary>
public class PathMismatchInfo
{
    /// <summary>
    /// Whether the current path differs from the expected path.
    /// </summary>
    public bool HasMismatch { get; set; }

    /// <summary>
    /// Current folder path (may be null if series has no files).
    /// </summary>
    public string? CurrentPath { get; set; }

    /// <summary>
    /// Expected folder path based on current format settings.
    /// </summary>
    public string ExpectedPath { get; set; } = string.Empty;
}

/// <summary>
/// Preview of how a series folder would be renamed.
/// </summary>
public class SeriesRenamePreview
{
    /// <summary>
    /// Series ID.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// Series title for display.
    /// </summary>
    public string SeriesTitle { get; set; } = string.Empty;

    /// <summary>
    /// Current folder path (may be null if series has no files yet).
    /// </summary>
    public string? CurrentPath { get; set; }

    /// <summary>
    /// Computed new folder path based on current format settings.
    /// </summary>
    public string NewPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the folder will be moved (current path differs from new path).
    /// </summary>
    public bool WillMove => !string.IsNullOrEmpty(CurrentPath) 
        && !string.Equals(CurrentPath, NewPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a new folder will be created (series has no current path).
    /// </summary>
    public bool WillCreate => string.IsNullOrEmpty(CurrentPath);

    /// <summary>
    /// Number of files that will be affected by this rename.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Total size of files that will be affected (in bytes).
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Individual file rename previews within this series.
    /// </summary>
    public List<FileRenamePreview> Files { get; set; } = new();

    /// <summary>
    /// Any errors or conflicts detected during preview.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Any warnings (non-blocking issues).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Whether this series can be safely renamed (no blocking errors).
    /// </summary>
    public bool CanRename => Errors.Count == 0;
}

/// <summary>
/// Preview of how an individual file would be renamed.
/// </summary>
public class FileRenamePreview
{
    /// <summary>
    /// File asset ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// Current file name (without directory).
    /// </summary>
    public string CurrentFileName { get; set; } = string.Empty;

    /// <summary>
    /// Computed new file name based on format settings.
    /// </summary>
    public string NewFileName { get; set; } = string.Empty;

    /// <summary>
    /// Current full path.
    /// </summary>
    public string CurrentPath { get; set; } = string.Empty;

    /// <summary>
    /// Computed new full path.
    /// </summary>
    public string NewPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file name will change.
    /// </summary>
    public bool WillRename => !string.Equals(CurrentFileName, NewFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the file will be moved to a different directory.
    /// </summary>
    public bool WillMove => !string.Equals(
        Path.GetDirectoryName(CurrentPath),
        Path.GetDirectoryName(NewPath),
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Whether this is a collection file (vs single issue).
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Issue number (for single issues).
    /// </summary>
    public decimal? IssueNumber { get; set; }

    /// <summary>
    /// Any error specific to this file.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Result of executing a series rename operation.
/// </summary>
public class SeriesRenameResult
{
    /// <summary>
    /// Series ID.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// Series title.
    /// </summary>
    public string SeriesTitle { get; set; } = string.Empty;

    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Previous folder path.
    /// </summary>
    public string? PreviousPath { get; set; }

    /// <summary>
    /// New folder path after rename.
    /// </summary>
    public string? NewPath { get; set; }

    /// <summary>
    /// Number of files that were moved/renamed.
    /// </summary>
    public int FilesRenamed { get; set; }

    /// <summary>
    /// Number of files that failed to rename.
    /// </summary>
    public int FilesFailed { get; set; }

    /// <summary>
    /// Individual file results.
    /// </summary>
    public List<FileRenameResult> FileResults { get; set; } = new();

    /// <summary>
    /// Whether this result is from a dry-run (simulation, no actual changes made).
    /// </summary>
    public bool IsDryRun { get; set; }
}

/// <summary>
/// Result of renaming an individual file.
/// </summary>
public class FileRenameResult
{
    /// <summary>
    /// File asset ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// Whether the file was successfully renamed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if rename failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Previous path.
    /// </summary>
    public string? PreviousPath { get; set; }

    /// <summary>
    /// New path after rename.
    /// </summary>
    public string? NewPath { get; set; }

    /// <summary>
    /// Whether this result is from a dry-run (simulation, no actual changes made).
    /// </summary>
    public bool IsDryRun { get; set; }
}
