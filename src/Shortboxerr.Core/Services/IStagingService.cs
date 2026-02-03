using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Manages the staging folder for manual imports.
/// </summary>
public interface IStagingService
{
    /// <summary>
    /// Scan the staging folder and return all importable items.
    /// </summary>
    Task<IReadOnlyList<StagedItem>> ScanStagingFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a preview of what will happen when importing a staged item.
    /// </summary>
    Task<ImportPreview> GetImportPreviewAsync(string sourcePath, int? seriesId, int? issueId, int? editionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute an import operation.
    /// </summary>
    Task<ImportResult> ImportAsync(string sourcePath, int? seriesId, int? issueId, int? editionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move a file to the failed folder.
    /// </summary>
    Task<bool> MoveToFailedAsync(string sourcePath, string reason, CancellationToken cancellationToken = default);
}



