using Shortboxerr.Core.Entities;

namespace Shortboxerr.Api.Dtos;

public record SeriesDto
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public string? SortTitle { get; init; }
    public string? Publisher { get; init; }
    public int? StartYear { get; init; }
    public int? EndYear { get; init; }
    public SeriesStatus Status { get; init; }
    public StatusSource StatusSource { get; init; }
    public string? Path { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool Monitored { get; init; }
    public int IssueCount { get; init; }
    public int UpcomingIssueCount { get; init; }
    public int IssueFileCount { get; init; }
    public int EditionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    
    // ComicVine metadata
    public int? ComicVineId { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? ComicVineUrl { get; init; }
    public int? TotalIssueCount { get; init; }
    public DateTime? MetadataLastRefreshed { get; init; }
    
    // Series-Annual Integration (Mylar3 parity)
    /// <summary>
    /// Type of series (Regular, Annual, Special, etc.).
    /// </summary>
    public SeriesType SeriesType { get; init; }
    
    /// <summary>
    /// If this is an annual series, the ID of the parent series.
    /// </summary>
    public int? ParentSeriesId { get; init; }
    
    /// <summary>
    /// For parent series, the list of linked annual series.
    /// </summary>
    public List<LinkedAnnualSeriesDto> LinkedAnnualSeries { get; init; } = new();

    public static SeriesDto FromEntity(Series series, int upcomingIssueCount = 0) => new()
    {
        Id = series.Id,
        Title = series.Title,
        SortTitle = series.SortTitle,
        Publisher = series.Publisher,
        StartYear = series.StartYear,
        EndYear = series.EndYear,
        Status = series.Status,
        StatusSource = series.StatusSource,
        Path = series.Path,
        ExternalId = series.ExternalId,
        ExternalSource = series.ExternalSource,
        Overview = series.Overview,
        Monitored = series.Monitored,
        IssueCount = series.Issues?.Count ?? 0,
        UpcomingIssueCount = upcomingIssueCount,
        IssueFileCount = series.Issues?.Count(i => i.HasFile) ?? 0,
        EditionCount = series.Editions?.Count ?? 0,
        CreatedAt = series.CreatedAt,
        UpdatedAt = series.UpdatedAt,
        // ComicVine fields
        ComicVineId = series.ComicVineId,
        CoverImageUrl = series.CoverImageUrl,
        ComicVineUrl = series.ComicVineUrl,
        TotalIssueCount = series.TotalIssueCount,
        MetadataLastRefreshed = series.MetadataLastRefreshed,
        // Series-Annual Integration
        SeriesType = series.SeriesType,
        ParentSeriesId = series.ParentSeriesId,
        LinkedAnnualSeries = series.LinkedAnnualSeries?
            .Select(a => new LinkedAnnualSeriesDto
            {
                Id = a.Id,
                Title = a.Title,
                StartYear = a.StartYear,
                IssueCount = a.Issues?.Count ?? 0,
                CoverImageUrl = a.CoverImageUrl
            })
            .ToList() ?? new()
    };
}

/// <summary>
/// Summary information about a linked annual series.
/// </summary>
public record LinkedAnnualSeriesDto
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public int? StartYear { get; init; }
    public int IssueCount { get; init; }
    public string? CoverImageUrl { get; init; }
}

public record CreateSeriesRequest
{
    public required string Title { get; init; }
    public string? SortTitle { get; init; }
    public string? Publisher { get; init; }
    public int? StartYear { get; init; }
    public int? EndYear { get; init; }
    public SeriesStatus Status { get; init; } = SeriesStatus.Continuing;
    public string? Path { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool Monitored { get; init; } = true;

    public Series ToEntity() => new()
    {
        Title = Title,
        SortTitle = SortTitle ?? Title,
        Publisher = Publisher,
        StartYear = StartYear,
        EndYear = EndYear,
        Status = Status,
        Path = Path,
        ExternalId = ExternalId,
        ExternalSource = ExternalSource,
        Overview = Overview,
        Monitored = Monitored
    };
}

public record UpdateSeriesRequest
{
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? Publisher { get; init; }
    public int? StartYear { get; init; }
    public int? EndYear { get; init; }
    public SeriesStatus? Status { get; init; }
    public string? Path { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool? Monitored { get; init; }
}

/// <summary>
/// Preview of what will be deleted when deleting a series.
/// </summary>
public record SeriesDeletePreviewDto
{
    public int SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public int IssueCount { get; init; }
    public int EditionCount { get; init; }
    public List<LinkedSeriesDto> LinkedAnnualSeries { get; init; } = new();
    public int TotalSeriesToDelete { get; init; }
}

/// <summary>
/// Summary of a linked series for deletion preview.
/// </summary>
public record LinkedSeriesDto
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public int IssueCount { get; init; }
}

/// <summary>
/// Result of a series deletion operation.
/// </summary>
public record SeriesDeleteResultDto
{
    public bool Success { get; init; }
    public required string SeriesDeleted { get; init; }
    public List<string> LinkedAnnualsDeleted { get; init; } = new();
    public int TotalDeleted { get; init; }
}

