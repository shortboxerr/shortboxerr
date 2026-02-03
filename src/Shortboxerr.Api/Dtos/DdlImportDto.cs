using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Api.Dtos;

/// <summary>
/// Request to process a completed DDL download.
/// </summary>
public record ProcessDownloadRequest(
    string FilePath,
    DdlCandidateDto Candidate,
    DdlImportOptionsDto? Options = null
);

/// <summary>
/// Request to verify a downloaded file.
/// </summary>
public record VerifyFileRequest(
    string FilePath,
    DdlCandidateDto? Candidate = null
);

/// <summary>
/// Request to move a file to staging.
/// </summary>
public record MoveToStagingRequest(
    string SourcePath,
    DdlCandidateDto Candidate
);

/// <summary>
/// Request to auto-match a candidate.
/// </summary>
public record AutoMatchRequest(
    DdlCandidateDto Candidate
);

/// <summary>
/// Request to execute import for a staged file.
/// </summary>
public record ExecuteImportRequest(
    string StagedFilePath,
    DdlCandidateDto Candidate,
    int? SeriesId = null,
    int? IssueId = null
);

/// <summary>
/// Request to approve a pending import.
/// </summary>
public record ApprovePendingImportRequest(
    string PendingImportId,
    int? SeriesId = null,
    int? IssueId = null
);

/// <summary>
/// Request to reject a pending import.
/// </summary>
public record RejectPendingImportRequest(
    string PendingImportId,
    string Reason,
    bool DeleteFile = false
);

/// <summary>
/// DTO for DDL candidate.
/// </summary>
public record DdlCandidateDto
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string ReleaseTitle { get; init; }
    public required string SourceSite { get; init; }
    public string? SourceUrl { get; init; }
    public DdlParsedInfoDto? ParsedInfo { get; init; }
    public List<DdlDownloadLinkDto> DownloadLinks { get; init; } = new();
    public long? Size { get; init; }
    public DateTime DateFound { get; init; } = DateTime.UtcNow;
    public int QualityScore { get; init; }
    public List<string> Tags { get; init; } = new();
    
    /// <summary>
    /// Convert to domain model.
    /// </summary>
    public DdlCandidate ToDomain() => new()
    {
        Id = Id,
        ReleaseTitle = ReleaseTitle,
        SourceSite = SourceSite,
        SourceUrl = SourceUrl,
        ParsedInfo = ParsedInfo?.ToDomain() ?? new DdlParsedInfo(),
        DownloadLinks = DownloadLinks.Select(l => l.ToDomain()).ToList(),
        Size = Size,
        DateFound = DateFound,
        QualityScore = QualityScore,
        Tags = Tags
    };
    
    /// <summary>
    /// Create from domain model.
    /// </summary>
    public static DdlCandidateDto FromDomain(DdlCandidate candidate) => new()
    {
        Id = candidate.Id,
        ReleaseTitle = candidate.ReleaseTitle,
        SourceSite = candidate.SourceSite,
        SourceUrl = candidate.SourceUrl,
        ParsedInfo = DdlParsedInfoDto.FromDomain(candidate.ParsedInfo),
        DownloadLinks = candidate.DownloadLinks.Select(DdlDownloadLinkDto.FromDomain).ToList(),
        Size = candidate.Size,
        DateFound = candidate.DateFound,
        QualityScore = candidate.QualityScore,
        Tags = candidate.Tags
    };
}

/// <summary>
/// DTO for DDL parsed info.
/// </summary>
public record DdlParsedInfoDto
{
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? VolumeNumber { get; init; }
    public int? Year { get; init; }
    public string? Publisher { get; init; }
    public string? Format { get; init; }
    public bool IsCollection { get; init; }
    public string? EditionType { get; init; }
    public string? IssueRange { get; init; }
    public string? ReleaseGroup { get; init; }
    public string? Quality { get; init; }
    public int Confidence { get; init; }
    
    public DdlParsedInfo ToDomain() => new()
    {
        SeriesTitle = SeriesTitle,
        IssueNumber = IssueNumber,
        VolumeNumber = VolumeNumber,
        Year = Year,
        Publisher = Publisher,
        Format = Format,
        IsCollection = IsCollection,
        EditionType = EditionType,
        IssueRange = IssueRange,
        ReleaseGroup = ReleaseGroup,
        Quality = Quality,
        Confidence = Confidence
    };
    
    public static DdlParsedInfoDto FromDomain(DdlParsedInfo info) => new()
    {
        SeriesTitle = info.SeriesTitle,
        IssueNumber = info.IssueNumber,
        VolumeNumber = info.VolumeNumber,
        Year = info.Year,
        Publisher = info.Publisher,
        Format = info.Format,
        IsCollection = info.IsCollection,
        EditionType = info.EditionType,
        IssueRange = info.IssueRange,
        ReleaseGroup = info.ReleaseGroup,
        Quality = info.Quality,
        Confidence = info.Confidence
    };
}

/// <summary>
/// DTO for DDL download link.
/// </summary>
public record DdlDownloadLinkDto
{
    public required string Url { get; init; }
    public string LinkType { get; init; } = "Direct";
    public string? HostName { get; init; }
    public bool IsVerified { get; init; }
    public int Priority { get; init; }
    public int? PartNumber { get; init; }
    public int? TotalParts { get; init; }
    
    public DdlDownloadLink ToDomain() => new()
    {
        Url = Url,
        LinkType = Enum.TryParse<DdlLinkType>(LinkType, out var lt) ? lt : DdlLinkType.Direct,
        HostName = HostName,
        IsVerified = IsVerified,
        Priority = Priority,
        PartNumber = PartNumber,
        TotalParts = TotalParts
    };
    
    public static DdlDownloadLinkDto FromDomain(DdlDownloadLink link) => new()
    {
        Url = link.Url,
        LinkType = link.LinkType.ToString(),
        HostName = link.HostName,
        IsVerified = link.IsVerified,
        Priority = link.Priority,
        PartNumber = link.PartNumber,
        TotalParts = link.TotalParts
    };
}

/// <summary>
/// DTO for DDL import options.
/// </summary>
public record DdlImportOptionsDto
{
    public bool AutoImportEnabled { get; init; } = true;
    public int AutoImportMinConfidence { get; init; } = 80;
    public bool RequireSeriesMatch { get; init; } = true;
    public bool RequireIssueMatch { get; init; } = true;
    public string? StagingFolderPath { get; init; }
    public bool DeleteSourceOnSuccess { get; init; } = true;
    public bool CreateHistoryEvents { get; init; } = true;
    
    public DdlImportOptions ToDomain() => new()
    {
        AutoImportEnabled = AutoImportEnabled,
        AutoImportMinConfidence = AutoImportMinConfidence,
        RequireSeriesMatch = RequireSeriesMatch,
        RequireIssueMatch = RequireIssueMatch,
        StagingFolderPath = StagingFolderPath,
        DeleteSourceOnSuccess = DeleteSourceOnSuccess,
        CreateHistoryEvents = CreateHistoryEvents
    };
}

/// <summary>
/// Response for DDL import result.
/// </summary>
public record DdlImportResultDto
{
    public required string ImportId { get; init; }
    public bool Success { get; init; }
    public string State { get; init; } = "Pending";
    public string? SourcePath { get; init; }
    public string? StagingPath { get; init; }
    public string? LibraryPath { get; init; }
    public int? SeriesId { get; init; }
    public string? SeriesTitle { get; init; }
    public int? IssueId { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? EditionId { get; init; }
    public int? FileAssetId { get; init; }
    public int? HistoryEventId { get; init; }
    public int MatchConfidence { get; init; }
    public string? ErrorMessage { get; init; }
    public bool PendingManualReview { get; init; }
    public string? PendingImportId { get; init; }
    public DateTime ProcessedAt { get; init; }
    
    public static DdlImportResultDto FromDomain(DdlImportResult result) => new()
    {
        ImportId = result.ImportId,
        Success = result.Success,
        State = result.State.ToString(),
        SourcePath = result.SourcePath,
        StagingPath = result.StagingPath,
        LibraryPath = result.LibraryPath,
        SeriesId = result.SeriesId,
        SeriesTitle = result.SeriesTitle,
        IssueId = result.IssueId,
        IssueNumber = result.IssueNumber,
        EditionId = result.EditionId,
        FileAssetId = result.FileAssetId,
        HistoryEventId = result.HistoryEventId,
        MatchConfidence = result.MatchConfidence,
        ErrorMessage = result.ErrorMessage,
        PendingManualReview = result.PendingManualReview,
        PendingImportId = result.PendingImportId,
        ProcessedAt = result.ProcessedAt
    };
}

/// <summary>
/// Response for DDL verification result.
/// </summary>
public record DdlVerificationResultDto
{
    public bool IsValid { get; init; }
    public required string FilePath { get; init; }
    public long FileSize { get; init; }
    public string? DetectedFormat { get; init; }
    public bool FormatSupported { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    
    public static DdlVerificationResultDto FromDomain(DdlVerificationResult result) => new()
    {
        IsValid = result.IsValid,
        FilePath = result.FilePath,
        FileSize = result.FileSize,
        DetectedFormat = result.DetectedFormat,
        FormatSupported = result.FormatSupported,
        ErrorMessage = result.ErrorMessage,
        Warnings = result.Warnings
    };
}

/// <summary>
/// Response for DDL staging result.
/// </summary>
public record DdlStagingResultDto
{
    public bool Success { get; init; }
    public required string SourcePath { get; init; }
    public string? StagingPath { get; init; }
    public string? StagingFilename { get; init; }
    public string? ErrorMessage { get; init; }
    
    public static DdlStagingResultDto FromDomain(DdlStagingResult result) => new()
    {
        Success = result.Success,
        SourcePath = result.SourcePath,
        StagingPath = result.StagingPath,
        StagingFilename = result.StagingFilename,
        ErrorMessage = result.ErrorMessage
    };
}

/// <summary>
/// Response for DDL match result.
/// </summary>
public record DdlMatchResultDto
{
    public bool MatchFound { get; init; }
    public int Confidence { get; init; }
    public int? SeriesId { get; init; }
    public string? SeriesTitle { get; init; }
    public int? IssueId { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? EditionId { get; init; }
    public string? EditionTitle { get; init; }
    public bool IsCollection { get; init; }
    public string? Explanation { get; init; }
    public IReadOnlyList<string> ConfidenceReductions { get; init; } = Array.Empty<string>();
    
    public static DdlMatchResultDto FromDomain(DdlMatchResult result) => new()
    {
        MatchFound = result.MatchFound,
        Confidence = result.Confidence,
        SeriesId = result.Series?.Id,
        SeriesTitle = result.Series?.Title,
        IssueId = result.Issue?.Id,
        IssueNumber = result.Issue?.IssueNumber,
        EditionId = result.Edition?.Id,
        EditionTitle = result.Edition?.Title,
        IsCollection = result.IsCollection,
        Explanation = result.Explanation,
        ConfidenceReductions = result.ConfidenceReductions
    };
}

/// <summary>
/// Response for pending import.
/// </summary>
public record DdlPendingImportDto
{
    public required string Id { get; init; }
    public required string StagingPath { get; init; }
    public required string Filename { get; init; }
    public long FileSize { get; init; }
    public DdlCandidateDto? Candidate { get; init; }
    public DdlMatchResultDto? BestMatch { get; init; }
    public int? SuggestedSeriesId { get; init; }
    public string? SuggestedSeriesTitle { get; init; }
    public decimal? SuggestedIssueNumber { get; init; }
    public bool IsCollection { get; init; }
    public DateTime StagedAt { get; init; }
    public string? ReviewReason { get; init; }
    
    public static DdlPendingImportDto FromDomain(DdlPendingImport pending) => new()
    {
        Id = pending.Id,
        StagingPath = pending.StagingPath,
        Filename = pending.Filename,
        FileSize = pending.FileSize,
        Candidate = pending.Candidate != null ? DdlCandidateDto.FromDomain(pending.Candidate) : null,
        BestMatch = pending.BestMatch != null ? DdlMatchResultDto.FromDomain(pending.BestMatch) : null,
        SuggestedSeriesId = pending.SuggestedSeriesId,
        SuggestedSeriesTitle = pending.SuggestedSeriesTitle,
        SuggestedIssueNumber = pending.SuggestedIssueNumber,
        IsCollection = pending.IsCollection,
        StagedAt = pending.StagedAt,
        ReviewReason = pending.ReviewReason
    };
}



