namespace Shortboxerr.Core.Models;

/// <summary>
/// Preview of an import operation before execution.
/// </summary>
public class ImportPreview
{
    /// <summary>
    /// Source file path.
    /// </summary>
    public required string SourcePath { get; init; }
    
    /// <summary>
    /// Destination path after import.
    /// </summary>
    public required string DestinationPath { get; init; }
    
    /// <summary>
    /// New filename after rename (if different).
    /// </summary>
    public required string NewFileName { get; init; }
    
    /// <summary>
    /// Whether file will be renamed.
    /// </summary>
    public bool WillRename { get; init; }
    
    /// <summary>
    /// Whether file will be moved.
    /// </summary>
    public bool WillMove { get; init; }
    
    /// <summary>
    /// Target series ID.
    /// </summary>
    public int? SeriesId { get; init; }
    
    /// <summary>
    /// Target series title.
    /// </summary>
    public string? SeriesTitle { get; init; }
    
    /// <summary>
    /// Target issue ID (for singles).
    /// </summary>
    public int? IssueId { get; init; }
    
    /// <summary>
    /// Target issue number (for singles).
    /// </summary>
    public decimal? IssueNumber { get; init; }
    
    /// <summary>
    /// Target edition ID (for collections).
    /// </summary>
    public int? EditionId { get; init; }
    
    /// <summary>
    /// Target edition title (for collections).
    /// </summary>
    public string? EditionTitle { get; init; }
    
    /// <summary>
    /// Whether this is a collection import.
    /// </summary>
    public bool IsCollection { get; init; }
    
    /// <summary>
    /// Any warnings about the import.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// Whether the import can proceed.
    /// </summary>
    public bool CanImport { get; init; }
    
    /// <summary>
    /// Reason if import cannot proceed.
    /// </summary>
    public string? BlockReason { get; init; }
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportResult
{
    public bool Success { get; init; }
    public required string SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public string? ErrorMessage { get; init; }
    public int? FileAssetId { get; init; }
    public int? HistoryEventId { get; init; }
}

