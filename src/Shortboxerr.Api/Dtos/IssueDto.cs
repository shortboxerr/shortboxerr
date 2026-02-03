using Shortboxerr.Core.Entities;

namespace Shortboxerr.Api.Dtos;

public record IssueDto
{
    public int Id { get; init; }
    public int SeriesId { get; init; }
    public decimal IssueNumber { get; init; }
    public string? IssueNumberText { get; init; }
    public string? Title { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? StoreDate { get; init; }
    public DateTime? CoverDate { get; init; }
    public string? Overview { get; init; }
    public bool Monitored { get; init; }
    public bool HasFile { get; init; }
    public bool SatisfiedByEdition { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    
    // ComicVine metadata
    public int? ComicVineId { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? ComicVineUrl { get; init; }
    public DateTime? MetadataLastRefreshed { get; init; }
    
    // Computed display properties
    public string DisplayNumber => IssueNumberText ?? $"#{IssueNumber:0.##}";
    
    public static IssueDto FromEntity(Issue issue) => new()
    {
        Id = issue.Id,
        SeriesId = issue.SeriesId,
        IssueNumber = issue.IssueNumber,
        IssueNumberText = issue.IssueNumberText,
        Title = issue.Title,
        ReleaseDate = issue.ReleaseDate,
        StoreDate = issue.StoreDate,
        CoverDate = issue.CoverDate,
        Overview = issue.Overview,
        Monitored = issue.Monitored,
        HasFile = issue.HasFile,
        SatisfiedByEdition = issue.SatisfiedByEdition,
        CreatedAt = issue.CreatedAt,
        UpdatedAt = issue.UpdatedAt,
        // ComicVine fields
        ComicVineId = issue.ComicVineId,
        CoverImageUrl = issue.CoverImageUrl,
        ComicVineUrl = issue.ComicVineUrl,
        MetadataLastRefreshed = issue.MetadataLastRefreshed
    };
}

