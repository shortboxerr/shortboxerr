using Shortboxerr.Core.Entities;

namespace Shortboxerr.Api.Dtos;

public record EditionDto
{
    public int Id { get; init; }
    public int? SeriesId { get; init; }
    public string? SeriesTitle { get; init; }
    public required string Title { get; init; }
    public string? SortTitle { get; init; }
    public EditionType EditionType { get; init; }
    public int? VolumeNumber { get; init; }
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public int? PageCount { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public string? CoverImageUrl { get; init; }
    public int? ComicVineId { get; init; }
    public string? ComicVineUrl { get; init; }
    public bool Monitored { get; init; }
    public bool HasFile { get; init; }
    public int ContentCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static EditionDto FromEntity(EditionTitle edition) => new()
    {
        Id = edition.Id,
        SeriesId = edition.SeriesId,
        SeriesTitle = edition.Series?.Title,
        Title = edition.Title,
        SortTitle = edition.SortTitle,
        EditionType = edition.EditionType,
        VolumeNumber = edition.VolumeNumber,
        Isbn = edition.Isbn,
        Publisher = edition.Publisher,
        ReleaseDate = edition.ReleaseDate,
        PageCount = edition.PageCount,
        ExternalId = edition.ExternalId,
        ExternalSource = edition.ExternalSource,
        Overview = edition.Overview,
        CoverImageUrl = edition.CoverImageUrl,
        ComicVineId = edition.ComicVineId,
        ComicVineUrl = edition.ComicVineUrl,
        Monitored = edition.Monitored,
        HasFile = edition.HasFile,
        ContentCount = edition.Contents?.Count ?? 0,
        CreatedAt = edition.CreatedAt,
        UpdatedAt = edition.UpdatedAt
    };
}

/// <summary>
/// Detailed edition information including full contents list.
/// </summary>
public record EditionDetailDto : EditionDto
{
    public List<EditionContentDto> Contents { get; init; } = new();
    
    public static new EditionDetailDto FromEntity(EditionTitle edition) => new()
    {
        Id = edition.Id,
        SeriesId = edition.SeriesId,
        SeriesTitle = edition.Series?.Title,
        Title = edition.Title,
        SortTitle = edition.SortTitle,
        EditionType = edition.EditionType,
        VolumeNumber = edition.VolumeNumber,
        Isbn = edition.Isbn,
        Publisher = edition.Publisher,
        ReleaseDate = edition.ReleaseDate,
        PageCount = edition.PageCount,
        ExternalId = edition.ExternalId,
        ExternalSource = edition.ExternalSource,
        Overview = edition.Overview,
        CoverImageUrl = edition.CoverImageUrl,
        ComicVineId = edition.ComicVineId,
        ComicVineUrl = edition.ComicVineUrl,
        Monitored = edition.Monitored,
        HasFile = edition.HasFile,
        ContentCount = edition.Contents?.Count ?? 0,
        CreatedAt = edition.CreatedAt,
        UpdatedAt = edition.UpdatedAt,
        Contents = edition.Contents?
            .OrderBy(c => c.SortOrder)
            .Select(EditionContentDto.FromEntity)
            .ToList() ?? new()
    };
}

/// <summary>
/// Represents a single issue contained in an edition.
/// </summary>
public record EditionContentDto
{
    public int Id { get; init; }
    public int EditionTitleId { get; init; }
    public int? IssueId { get; init; }
    public int? SeriesId { get; init; }
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public string? IssueNumberText { get; init; }
    public string? IssueTitle { get; init; }
    public string? IssueCoverImageUrl { get; init; }
    public bool IssueHasFile { get; init; }
    public int SortOrder { get; init; }
    
    public static EditionContentDto FromEntity(EditionContent content) => new()
    {
        Id = content.Id,
        EditionTitleId = content.EditionTitleId,
        IssueId = content.IssueId,
        SeriesId = content.SeriesId ?? content.Issue?.SeriesId,
        SeriesTitle = content.Series?.Title ?? content.Issue?.Series?.Title,
        IssueNumber = content.IssueNumber ?? content.Issue?.IssueNumber,
        IssueNumberText = content.Issue?.IssueNumberText,
        IssueTitle = content.Issue?.Title,
        IssueCoverImageUrl = content.Issue?.CoverImageUrl,
        IssueHasFile = content.Issue?.HasFile ?? false,
        SortOrder = content.SortOrder
    };
}

public record CreateEditionRequest
{
    public int? SeriesId { get; init; }
    public required string Title { get; init; }
    public string? SortTitle { get; init; }
    public EditionType EditionType { get; init; } = EditionType.TradesPaperback;
    public int? VolumeNumber { get; init; }
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public int? PageCount { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool Monitored { get; init; }

    public EditionTitle ToEntity() => new()
    {
        SeriesId = SeriesId,
        Title = Title,
        SortTitle = SortTitle ?? Title,
        EditionType = EditionType,
        VolumeNumber = VolumeNumber,
        Isbn = Isbn,
        Publisher = Publisher,
        ReleaseDate = ReleaseDate,
        PageCount = PageCount,
        ExternalId = ExternalId,
        ExternalSource = ExternalSource,
        Overview = Overview,
        Monitored = Monitored
    };
}

public record UpdateEditionRequest
{
    public int? SeriesId { get; init; }
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public EditionType? EditionType { get; init; }
    public int? VolumeNumber { get; init; }
    public string? Isbn { get; init; }
    public string? Publisher { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public int? PageCount { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
    public string? Overview { get; init; }
    public bool? Monitored { get; init; }
}

