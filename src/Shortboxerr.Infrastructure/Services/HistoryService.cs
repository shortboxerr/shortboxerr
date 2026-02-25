using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Service for recording history events when library content is added, removed, or modified.
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly ShortboxerrDbContext _db;
    private readonly ILogger<HistoryService> _logger;

    public HistoryService(ShortboxerrDbContext db, ILogger<HistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordFileImportedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? additionalData,
        CancellationToken cancellationToken)
    {
        var filename = Path.GetFileName(sourcePath);
        await RecordEventAsync(
            eventType: HistoryEventType.FileImported,
            message: $"Imported {filename}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: editionId,
            sourcePath: sourcePath,
            destinationPath: destinationPath,
            success: true,
            errorMessage: null,
            data: additionalData,
            cancellationToken: cancellationToken);
    }

    public async Task RecordFileDeletedAsync(
        string filePath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var filename = Path.GetFileName(filePath);
        var message = string.IsNullOrEmpty(reason)
            ? $"Deleted {filename}"
            : $"Deleted {filename}: {reason}";

        await RecordEventAsync(
            eventType: HistoryEventType.FileDeleted,
            message: message,
            seriesId: seriesId,
            issueId: issueId,
            editionId: editionId,
            sourcePath: filePath,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordFileRenamedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        CancellationToken cancellationToken)
    {
        var oldName = Path.GetFileName(sourcePath);
        var newName = Path.GetFileName(destinationPath);
        
        await RecordEventAsync(
            eventType: HistoryEventType.FileRenamed,
            message: $"Renamed {oldName} → {newName}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: editionId,
            sourcePath: sourcePath,
            destinationPath: destinationPath,
            success: true,
            errorMessage: null,
            data: null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordFileMovedAsync(
        string sourcePath,
        string destinationPath,
        int? seriesId,
        int? issueId,
        int? editionId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var filename = Path.GetFileName(sourcePath);
        var message = string.IsNullOrEmpty(reason)
            ? $"Moved {filename}"
            : $"Moved {filename}: {reason}";

        await RecordEventAsync(
            eventType: HistoryEventType.FileMoved,
            message: message,
            seriesId: seriesId,
            issueId: issueId,
            editionId: editionId,
            sourcePath: sourcePath,
            destinationPath: destinationPath,
            success: true,
            errorMessage: null,
            data: null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordDownloadGrabbedAsync(
        string title,
        string source,
        int? seriesId,
        int? issueId,
        string? additionalData,
        CancellationToken cancellationToken)
    {
        await RecordEventAsync(
            eventType: HistoryEventType.DownloadGrabbed,
            message: $"Grabbed {title} from {source}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: additionalData,
            cancellationToken: cancellationToken);
    }

    public async Task RecordDownloadCompletedAsync(
        string title,
        string filePath,
        int? seriesId,
        int? issueId,
        long fileSize,
        string? source,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(new { FileSize = fileSize, Source = source });
        
        await RecordEventAsync(
            eventType: HistoryEventType.DownloadCompleted,
            message: $"Downloaded {title}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: null,
            sourcePath: null,
            destinationPath: filePath,
            success: true,
            errorMessage: null,
            data: data,
            cancellationToken: cancellationToken);
    }

    public async Task RecordDownloadFailedAsync(
        string title,
        string errorMessage,
        int? seriesId,
        int? issueId,
        string? source,
        CancellationToken cancellationToken)
    {
        await RecordEventAsync(
            eventType: HistoryEventType.DownloadFailed,
            message: $"Download failed: {title}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: false,
            errorMessage: errorMessage,
            data: source != null ? JsonSerializer.Serialize(new { Source = source }) : null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordSeriesAddedAsync(
        int seriesId,
        string seriesTitle,
        string? source,
        CancellationToken cancellationToken)
    {
        var message = string.IsNullOrEmpty(source)
            ? $"Added series: {seriesTitle}"
            : $"Added series: {seriesTitle} (from {source})";

        await RecordEventAsync(
            eventType: HistoryEventType.SeriesAdded,
            message: message,
            seriesId: seriesId,
            issueId: null,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: source != null ? JsonSerializer.Serialize(new { Source = source }) : null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordSeriesDeletedAsync(
        int? seriesId,
        string seriesTitle,
        bool deleteFiles,
        CancellationToken cancellationToken)
    {
        var message = deleteFiles
            ? $"Deleted series and files: {seriesTitle}"
            : $"Deleted series: {seriesTitle}";

        await RecordEventAsync(
            eventType: HistoryEventType.SeriesDeleted,
            message: message,
            seriesId: seriesId,
            issueId: null,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: JsonSerializer.Serialize(new { DeletedFiles = deleteFiles }),
            cancellationToken: cancellationToken);
    }

    public async Task RecordIssueAddedAsync(
        int issueId,
        int seriesId,
        decimal issueNumber,
        string seriesTitle,
        CancellationToken cancellationToken)
    {
        await RecordEventAsync(
            eventType: HistoryEventType.IssueAdded,
            message: $"Added issue #{issueNumber} to {seriesTitle}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordIssueMonitoringChangedAsync(
        int issueId,
        int seriesId,
        decimal issueNumber,
        bool monitored,
        CancellationToken cancellationToken)
    {
        var action = monitored ? "enabled" : "disabled";
        await RecordEventAsync(
            eventType: HistoryEventType.IssueMonitoredChanged,
            message: $"Monitoring {action} for issue #{issueNumber}",
            seriesId: seriesId,
            issueId: issueId,
            editionId: null,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: JsonSerializer.Serialize(new { Monitored = monitored }),
            cancellationToken: cancellationToken);
    }

    public async Task RecordEditionAddedAsync(
        int editionId,
        int seriesId,
        string editionTitle,
        string seriesTitle,
        CancellationToken cancellationToken)
    {
        await RecordEventAsync(
            eventType: HistoryEventType.EditionAdded,
            message: $"Added edition: {editionTitle} ({seriesTitle})",
            seriesId: seriesId,
            issueId: null,
            editionId: editionId,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: null,
            cancellationToken: cancellationToken);
    }

    public async Task RecordEditionDeletedAsync(
        int? editionId,
        int? seriesId,
        string editionTitle,
        string seriesTitle,
        bool deleteFiles,
        CancellationToken cancellationToken)
    {
        var message = deleteFiles
            ? $"Deleted edition and files: {editionTitle} ({seriesTitle})"
            : $"Deleted edition: {editionTitle} ({seriesTitle})";

        await RecordEventAsync(
            eventType: HistoryEventType.EditionDeleted,
            message: message,
            seriesId: seriesId,
            issueId: null,
            editionId: editionId,
            sourcePath: null,
            destinationPath: null,
            success: true,
            errorMessage: null,
            data: JsonSerializer.Serialize(new { DeletedFiles = deleteFiles }),
            cancellationToken: cancellationToken);
    }

    public async Task RecordEventAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var historyEvent = new HistoryEvent
            {
                EventType = eventType,
                Message = message,
                SeriesId = seriesId,
                IssueId = issueId,
                EditionTitleId = editionId,
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                Success = success,
                ErrorMessage = errorMessage,
                Data = data,
                Timestamp = DateTime.UtcNow
            };

            _db.HistoryEvents.Add(historyEvent);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Recorded history event: {EventType} - {Message}", eventType, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record history event: {EventType} - {Message}", eventType, message);
        }
    }
}
