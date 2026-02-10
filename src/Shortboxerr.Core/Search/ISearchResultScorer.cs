using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Search;

/// <summary>
/// Service for scoring and ranking search results.
/// Implements Mylar3-style search result ordering based on configurable weights.
/// </summary>
public interface ISearchResultScorer
{
    /// <summary>
    /// Scores a single candidate based on the current search settings.
    /// </summary>
    /// <param name="candidate">The candidate to score.</param>
    /// <param name="searchContext">Context for the search (target series, issue, etc.).</param>
    /// <returns>Scored result with breakdown of factors.</returns>
    ScoredCandidate ScoreCandidate(Candidate candidate, SearchContext searchContext);

    /// <summary>
    /// Scores and sorts a list of candidates.
    /// </summary>
    /// <param name="candidates">Candidates to score and sort.</param>
    /// <param name="searchContext">Context for the search.</param>
    /// <returns>Scored candidates sorted by score (highest first).</returns>
    IReadOnlyList<ScoredCandidate> ScoreAndSort(IEnumerable<Candidate> candidates, SearchContext searchContext);

    /// <summary>
    /// Gets the best candidate from a list.
    /// </summary>
    /// <param name="candidates">Candidates to evaluate.</param>
    /// <param name="searchContext">Context for the search.</param>
    /// <returns>The highest-scoring candidate, or null if none are acceptable.</returns>
    ScoredCandidate? GetBestCandidate(IEnumerable<Candidate> candidates, SearchContext searchContext);
}

/// <summary>
/// Context information for scoring search results.
/// </summary>
public class SearchContext
{
    /// <summary>
    /// Target series title to match.
    /// </summary>
    public required string TargetSeriesTitle { get; init; }

    /// <summary>
    /// Target issue number (if searching for specific issue).
    /// </summary>
    public decimal? TargetIssueNumber { get; init; }

    /// <summary>
    /// Target year (publication year).
    /// </summary>
    public int? TargetYear { get; init; }

    /// <summary>
    /// Target volume number.
    /// </summary>
    public int? TargetVolume { get; init; }

    /// <summary>
    /// Whether searching for a pack/collection.
    /// </summary>
    public bool SearchingForPack { get; init; }

    /// <summary>
    /// ComicVine series ID (for precise matching).
    /// </summary>
    public int? ComicVineSeriesId { get; init; }

    /// <summary>
    /// ComicVine issue ID (for precise matching).
    /// </summary>
    public int? ComicVineIssueId { get; init; }
}

/// <summary>
/// A candidate with its computed score and breakdown.
/// </summary>
public class ScoredCandidate
{
    /// <summary>
    /// The original candidate.
    /// </summary>
    public required Candidate Candidate { get; init; }

    /// <summary>
    /// Final computed score.
    /// </summary>
    public int TotalScore { get; init; }

    /// <summary>
    /// Normalized score as percentage (0-100).
    /// </summary>
    public double NormalizedScore { get; init; }

    /// <summary>
    /// Detailed breakdown of how the score was calculated.
    /// </summary>
    public required ScoreBreakdown Breakdown { get; init; }

    /// <summary>
    /// Whether this candidate meets the minimum quality threshold.
    /// </summary>
    public bool MeetsThreshold { get; init; }

    /// <summary>
    /// Grade based on score (A, B, C, D, F).
    /// </summary>
    public string Grade => NormalizedScore switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _ => "F"
    };
}

/// <summary>
/// Detailed breakdown of how a score was calculated.
/// </summary>
public class ScoreBreakdown
{
    /// <summary>
    /// Points from quality tier matching.
    /// </summary>
    public ScoreComponent Quality { get; init; } = new();

    /// <summary>
    /// Points from file size evaluation.
    /// </summary>
    public ScoreComponent Size { get; init; } = new();

    /// <summary>
    /// Points from release group reputation.
    /// </summary>
    public ScoreComponent ReleaseGroup { get; init; } = new();

    /// <summary>
    /// Points from year match accuracy.
    /// </summary>
    public ScoreComponent YearMatch { get; init; } = new();

    /// <summary>
    /// Points from issue number match.
    /// </summary>
    public ScoreComponent IssueMatch { get; init; } = new();

    /// <summary>
    /// Points from series title match.
    /// </summary>
    public ScoreComponent SeriesMatch { get; init; } = new();

    /// <summary>
    /// Points from format preference.
    /// </summary>
    public ScoreComponent Format { get; init; } = new();

    /// <summary>
    /// Points from source priority.
    /// </summary>
    public ScoreComponent SourcePriority { get; init; } = new();

    /// <summary>
    /// Points from release freshness.
    /// </summary>
    public ScoreComponent Freshness { get; init; } = new();

    /// <summary>
    /// Bonus from preferred words.
    /// </summary>
    public ScoreComponent PreferredWords { get; init; } = new();

    /// <summary>
    /// Penalty from blacklisted words.
    /// </summary>
    public ScoreComponent BlacklistPenalty { get; init; } = new();

    /// <summary>
    /// Total of all positive scores.
    /// </summary>
    public int TotalPositive => Quality.Points + Size.Points + ReleaseGroup.Points + 
                                 YearMatch.Points + IssueMatch.Points + SeriesMatch.Points +
                                 Format.Points + SourcePriority.Points + Freshness.Points +
                                 PreferredWords.Points;

    /// <summary>
    /// Total penalties (negative).
    /// </summary>
    public int TotalPenalties => BlacklistPenalty.Points;

    /// <summary>
    /// Final score after penalties.
    /// </summary>
    public int FinalScore => Math.Max(0, TotalPositive - TotalPenalties);

    /// <summary>
    /// Maximum possible score.
    /// </summary>
    public int MaxPossible { get; init; }
}

/// <summary>
/// A single scoring component with points and explanation.
/// </summary>
public class ScoreComponent
{
    /// <summary>
    /// Points awarded (or deducted if penalty).
    /// </summary>
    public int Points { get; init; }

    /// <summary>
    /// Maximum points possible for this component.
    /// </summary>
    public int MaxPoints { get; init; }

    /// <summary>
    /// Human-readable reason for the score.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Percentage of max points earned.
    /// </summary>
    public double Percentage => MaxPoints > 0 ? (double)Points / MaxPoints * 100 : 0;
}
