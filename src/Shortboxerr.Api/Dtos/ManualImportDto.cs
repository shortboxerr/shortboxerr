using Shortboxerr.Core.Models;

namespace Shortboxerr.Api.Dtos;

public record StagedItemDto
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public long Size { get; init; }
    public required string Extension { get; init; }
    public DateTime LastModified { get; init; }
    public ParsedComicInfoDto? ParsedInfo { get; init; }
    public int ParseConfidence { get; init; }
    public int? SuggestedSeriesId { get; init; }
    public int? SuggestedEditionId { get; init; }
    public bool IsCollection { get; init; }
    public string? RejectionReason { get; init; }

    public static StagedItemDto FromModel(StagedItem item) => new()
    {
        Path = item.Path,
        FileName = item.FileName,
        Size = item.Size,
        Extension = item.Extension,
        LastModified = item.LastModified,
        ParsedInfo = item.ParsedInfo != null ? ParsedComicInfoDto.FromModel(item.ParsedInfo) : null,
        ParseConfidence = item.ParseConfidence,
        SuggestedSeriesId = item.SuggestedSeriesId,
        SuggestedEditionId = item.SuggestedEditionId,
        IsCollection = item.IsCollection,
        RejectionReason = item.RejectionReason
    };
}

public record ParsedComicInfoDto
{
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? VolumeNumber { get; init; }
    public int? Year { get; init; }
    public string? Publisher { get; init; }
    public string? EditionIndicator { get; init; }
    public string? IssueRange { get; init; }
    public List<string> Tags { get; init; } = new();

    public static ParsedComicInfoDto FromModel(ParsedComicInfo info) => new()
    {
        SeriesTitle = info.SeriesTitle,
        IssueNumber = info.IssueNumber,
        VolumeNumber = info.VolumeNumber,
        Year = info.Year,
        Publisher = info.Publisher,
        EditionIndicator = info.EditionIndicator,
        IssueRange = info.IssueRange,
        Tags = info.Tags
    };
}

public record ImportPreviewDto
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public required string NewFileName { get; init; }
    public bool WillRename { get; init; }
    public bool WillMove { get; init; }
    public int? SeriesId { get; init; }
    public string? SeriesTitle { get; init; }
    public int? IssueId { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? EditionId { get; init; }
    public string? EditionTitle { get; init; }
    public bool IsCollection { get; init; }
    public List<string> Warnings { get; init; } = new();
    public bool CanImport { get; init; }
    public string? BlockReason { get; init; }

    public static ImportPreviewDto FromModel(ImportPreview preview) => new()
    {
        SourcePath = preview.SourcePath,
        DestinationPath = preview.DestinationPath,
        NewFileName = preview.NewFileName,
        WillRename = preview.WillRename,
        WillMove = preview.WillMove,
        SeriesId = preview.SeriesId,
        SeriesTitle = preview.SeriesTitle,
        IssueId = preview.IssueId,
        IssueNumber = preview.IssueNumber,
        EditionId = preview.EditionId,
        EditionTitle = preview.EditionTitle,
        IsCollection = preview.IsCollection,
        Warnings = preview.Warnings,
        CanImport = preview.CanImport,
        BlockReason = preview.BlockReason
    };
}

public record ImportResultDto
{
    public bool Success { get; init; }
    public required string SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public string? ErrorMessage { get; init; }
    public int? FileAssetId { get; init; }
    public int? HistoryEventId { get; init; }

    public static ImportResultDto FromModel(ImportResult result) => new()
    {
        Success = result.Success,
        SourcePath = result.SourcePath,
        DestinationPath = result.DestinationPath,
        ErrorMessage = result.ErrorMessage,
        FileAssetId = result.FileAssetId,
        HistoryEventId = result.HistoryEventId
    };
}

public record ImportRequest
{
    public required string SourcePath { get; init; }
    public int? SeriesId { get; init; }
    public int? IssueId { get; init; }
    public int? EditionId { get; init; }
}



