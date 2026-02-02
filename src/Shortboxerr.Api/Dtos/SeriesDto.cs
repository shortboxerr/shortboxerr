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
    public string? Path { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool Monitored { get; init; }
    public int IssueCount { get; init; }
    public int IssueFileCount { get; init; }
    public int EditionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static SeriesDto FromEntity(Series series) => new()
    {
        Id = series.Id,
        Title = series.Title,
        SortTitle = series.SortTitle,
        Publisher = series.Publisher,
        StartYear = series.StartYear,
        EndYear = series.EndYear,
        Status = series.Status,
        Path = series.Path,
        ExternalId = series.ExternalId,
        ExternalSource = series.ExternalSource,
        Overview = series.Overview,
        Monitored = series.Monitored,
        IssueCount = series.Issues?.Count ?? 0,
        IssueFileCount = series.Issues?.Count(i => i.HasFile) ?? 0,
        EditionCount = series.Editions?.Count ?? 0,
        CreatedAt = series.CreatedAt,
        UpdatedAt = series.UpdatedAt
    };
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

