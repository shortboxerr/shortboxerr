using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Evaluates and ranks release candidates for acquisition.
/// Implements Mylar3-compatible selection logic.
/// </summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Evaluate a single candidate against a target (series/issue/edition).
    /// </summary>
    CandidateEvaluation Evaluate(Candidate candidate, CandidateTarget target);
    
    /// <summary>
    /// Evaluate and rank multiple candidates, returning them in preference order.
    /// </summary>
    IReadOnlyList<CandidateEvaluation> EvaluateAndRank(
        IEnumerable<Candidate> candidates, 
        CandidateTarget target);
    
    /// <summary>
    /// Get the best candidate from a list, or null if none are acceptable.
    /// </summary>
    CandidateEvaluation? GetBestCandidate(
        IEnumerable<Candidate> candidates, 
        CandidateTarget target);
    
    /// <summary>
    /// Check if auto-grab should proceed for the best candidate.
    /// Returns false if score is below threshold or manual review is needed.
    /// </summary>
    (bool ShouldAutoGrab, string Reason) CheckAutoGrab(
        IReadOnlyList<CandidateEvaluation> rankedCandidates);
}

/// <summary>
/// The target we're trying to match candidates against.
/// </summary>
public class CandidateTarget
{
    /// <summary>
    /// Target series title.
    /// </summary>
    public required string SeriesTitle { get; init; }
    
    /// <summary>
    /// Target issue number (for singles).
    /// </summary>
    public decimal? IssueNumber { get; init; }
    
    /// <summary>
    /// Target volume number.
    /// </summary>
    public int? VolumeNumber { get; init; }
    
    /// <summary>
    /// Target year (for disambiguation).
    /// </summary>
    public int? Year { get; init; }
    
    /// <summary>
    /// Whether we're looking for a collection.
    /// </summary>
    public bool IsCollection { get; init; }
    
    /// <summary>
    /// Target edition title (for collections).
    /// </summary>
    public string? EditionTitle { get; init; }
}

