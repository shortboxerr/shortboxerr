using Shortboxerr.Core.Entities;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for recording history events when library content is added, removed, or modified.
/// </summary>
public interface IHistoryService
{
    // File operations
    Task RecordFileImportedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? additionalData = null,
        CancellationToken cancellationToken = default);

    Task RecordFileDeletedAsync(
        string filePath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task RecordFileRenamedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        CancellationToken cancellationToken = default);

    Task RecordFileMovedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    // Download operations
    Task RecordDownloadGrabbedAsync(
        string title,
        string source,
        int? seriesId,
        int? issueId,
        string? additionalData = null,
        CancellationToken cancellationToken = default);

    Task RecordDownloadCompletedAsync(
        string title,
        string filePath,
        int? seriesId,
        int? issueId,
        long fileSize,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task RecordDownloadFailedAsync(
        string title,
        string errorMessage,
        int? seriesId,
        int? issueId,
        string? source = null,
        CancellationToken cancellationToken = default);

    // Series operations
    Task RecordSeriesAddedAsync(
        int seriesId,
        string seriesTitle,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task RecordSeriesDeletedAsync(
        int? seriesId,
        string seriesTitle,
        bool deleteFiles,
        CancellationToken cancellationToken = default);

    // Issue operations
    Task RecordIssueAddedAsync(
        int issueId,
        int seriesId,
        decimal issueNumber,
        string seriesTitle,
        CancellationToken cancellationToken = default);

    Task RecordIssueMonitoringChangedAsync(
        int issueId,
        int seriesId,
        decimal issueNumber,
        bool monitored,
        CancellationToken cancellationToken = default);

    // Edition operations
    Task RecordEditionAddedAsync(
        int editionId,
        int seriesId,
        string editionTitle,
        string seriesTitle,
        CancellationToken cancellationToken = default);

    Task RecordEditionDeletedAsync(
        int? editionId,
        int? seriesId,
        string editionTitle,
        string seriesTitle,
        bool deleteFiles,
        CancellationToken cancellationToken = default);

    // Generic event recording
    Task RecordEventAsync(
        HistoryEventType eventType,
        string message,
        int? seriesId = null,
        int? issueId = null,
        int? editionId = null,
        string? sourcePath = null,
        string? destinationPath = null,
        bool success = true,
        string? errorMessage = null,
        string? data = null,
        CancellationToken cancellationToken = default);
}
