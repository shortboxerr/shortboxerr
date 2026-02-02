namespace Shortboxerr.Core.Entities;

/// <summary>
/// Audit log for significant events (imports, downloads, renames, etc.).
/// </summary>
public class HistoryEvent
{
    public int Id { get; set; }
    
    /// <summary>
    /// Type of event.
    /// </summary>
    public HistoryEventType EventType { get; set; }
    
    /// <summary>
    /// Related series ID (if applicable).
    /// </summary>
    public int? SeriesId { get; set; }
    
    /// <summary>
    /// Related issue ID (if applicable).
    /// </summary>
    public int? IssueId { get; set; }
    
    /// <summary>
    /// Related edition ID (if applicable).
    /// </summary>
    public int? EditionTitleId { get; set; }
    
    /// <summary>
    /// Human-readable message describing the event.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// JSON data with additional details.
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Source path (for file operations).
    /// </summary>
    public string? SourcePath { get; set; }
    
    /// <summary>
    /// Destination path (for file operations).
    /// </summary>
    public string? DestinationPath { get; set; }
    
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Series? Series { get; set; }
    public Issue? Issue { get; set; }
    public EditionTitle? EditionTitle { get; set; }
}

public enum HistoryEventType
{
    Unknown = 0,
    
    // File operations
    FileImported = 10,
    FileRenamed = 11,
    FileDeleted = 12,
    FileMoved = 13,
    
    // Download operations
    DownloadGrabbed = 20,
    DownloadCompleted = 21,
    DownloadFailed = 22,
    
    // Series/Issue operations
    SeriesAdded = 30,
    SeriesDeleted = 31,
    IssueAdded = 32,
    IssueMonitoredChanged = 33,
    
    // Edition operations
    EditionAdded = 40,
    EditionDeleted = 41,
    EditionMonitoredChanged = 42,
    
    // System operations
    ApplicationStarted = 90,
    ApplicationUpdated = 91,
    BackupCreated = 92
}

