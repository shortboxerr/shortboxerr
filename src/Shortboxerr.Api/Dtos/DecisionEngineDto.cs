using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Dtos;

public record CandidateDto
{
    public required string Id { get; init; }
    public required string ReleaseTitle { get; init; }
    public required string Source { get; init; }
    public int SourcePriority { get; init; }
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? VolumeNumber { get; init; }
    public int? Year { get; init; }
    public string? Format { get; init; }
    public long? Size { get; init; }
    public bool IsCollection { get; init; }
    public string? EditionType { get; init; }
    public string? DownloadUrl { get; init; }
    public DateTime DiscoveredAt { get; init; }
    public List<string> Tags { get; init; } = new();

    public Candidate ToModel() => new()
    {
        Id = Id,
        ReleaseTitle = ReleaseTitle,
        Source = Source,
        SourcePriority = SourcePriority,
        SeriesTitle = SeriesTitle,
        IssueNumber = IssueNumber,
        VolumeNumber = VolumeNumber,
        Year = Year,
        Format = Format,
        Size = Size,
        IsCollection = IsCollection,
        EditionType = EditionType,
        DownloadUrl = DownloadUrl,
        DiscoveredAt = DiscoveredAt,
        Tags = Tags
    };

    public static CandidateDto FromModel(Candidate model) => new()
    {
        Id = model.Id,
        ReleaseTitle = model.ReleaseTitle,
        Source = model.Source,
        SourcePriority = model.SourcePriority,
        SeriesTitle = model.SeriesTitle,
        IssueNumber = model.IssueNumber,
        VolumeNumber = model.VolumeNumber,
        Year = model.Year,
        Format = model.Format,
        Size = model.Size,
        IsCollection = model.IsCollection,
        EditionType = model.EditionType,
        DownloadUrl = model.DownloadUrl,
        DiscoveredAt = model.DiscoveredAt,
        Tags = model.Tags
    };
}

public record CandidateTargetDto
{
    public required string SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? VolumeNumber { get; init; }
    public int? Year { get; init; }
    public bool IsCollection { get; init; }
    public string? EditionTitle { get; init; }

    public CandidateTarget ToModel() => new()
    {
        SeriesTitle = SeriesTitle,
        IssueNumber = IssueNumber,
        VolumeNumber = VolumeNumber,
        Year = Year,
        IsCollection = IsCollection,
        EditionTitle = EditionTitle
    };
}

public record EvaluateCandidatesRequest
{
    public required List<CandidateDto> Candidates { get; init; }
    public required CandidateTargetDto Target { get; init; }
}

public record EvaluateSingleCandidateRequest
{
    public required CandidateDto Candidate { get; init; }
    public required CandidateTargetDto Target { get; init; }
}

public record CandidateEvaluationDto
{
    public required CandidateDto Candidate { get; init; }
    public bool Accepted { get; init; }
    public int Score { get; init; }
    public string? RejectionReason { get; init; }
    public required DecisionExplanationDto Explanation { get; init; }

    public static CandidateEvaluationDto FromModel(CandidateEvaluation model) => new()
    {
        Candidate = CandidateDto.FromModel(model.Candidate),
        Accepted = model.Accepted,
        Score = model.Score,
        RejectionReason = model.RejectionReason?.ToString(),
        Explanation = DecisionExplanationDto.FromModel(model.Explanation)
    };
}

public record DecisionExplanationDto
{
    public List<ScoringFactorDto> ScoringFactors { get; init; } = new();
    public List<CheckResultDto> Checks { get; init; } = new();
    public required string Summary { get; init; }
    public int BaseScore { get; init; }
    public int Penalties { get; init; }
    public int FinalScore { get; init; }

    public static DecisionExplanationDto FromModel(DecisionExplanation model) => new()
    {
        ScoringFactors = model.ScoringFactors.Select(ScoringFactorDto.FromModel).ToList(),
        Checks = model.Checks.Select(CheckResultDto.FromModel).ToList(),
        Summary = model.Summary,
        BaseScore = model.BaseScore,
        Penalties = model.Penalties,
        FinalScore = model.FinalScore
    };
}

public record ScoringFactorDto(string Name, int Points, string Reason)
{
    public static ScoringFactorDto FromModel(ScoringFactor model) => new(model.Name, model.Points, model.Reason);
}

public record CheckResultDto(string CheckName, bool Passed, string Details)
{
    public static CheckResultDto FromModel(CheckResult model) => new(model.CheckName, model.Passed, model.Details);
}

public record EvaluationResultDto
{
    public required List<CandidateEvaluationDto> RankedCandidates { get; init; }
    public CandidateEvaluationDto? BestCandidate { get; init; }
    public bool ShouldAutoGrab { get; init; }
    public required string AutoGrabReason { get; init; }
    public int TotalCandidates { get; init; }
    public int AcceptedCandidates { get; init; }
    public int RejectedCandidates { get; init; }
}

