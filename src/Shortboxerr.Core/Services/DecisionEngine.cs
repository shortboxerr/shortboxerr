using Microsoft.Extensions.Options;
using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Evaluates and ranks release candidates for acquisition.
/// Implements Mylar3-compatible selection logic with deterministic tie-breaking.
/// </summary>
public class DecisionEngine : IDecisionEngine
{
    private readonly DecisionEngineSettings _settings;

    public DecisionEngine(IOptions<DecisionEngineSettings> settings)
    {
        _settings = settings.Value;
    }

    public CandidateEvaluation Evaluate(Candidate candidate, CandidateTarget target)
    {
        var checks = new List<CheckResult>();
        var scoringFactors = new List<ScoringFactor>();
        var rejectionReason = RejectionReason.None;
        
        // === REJECTION CHECKS ===
        
        // Check banned words
        var bannedWordCheck = CheckBannedWords(candidate);
        checks.Add(bannedWordCheck);
        if (!bannedWordCheck.Passed)
        {
            rejectionReason = RejectionReason.BannedWordFound;
            return CreateRejection(candidate, rejectionReason, checks, scoringFactors, 
                $"Rejected: {bannedWordCheck.Details}");
        }
        
        // Check required words
        var requiredWordCheck = CheckRequiredWords(candidate);
        checks.Add(requiredWordCheck);
        if (!requiredWordCheck.Passed)
        {
            rejectionReason = RejectionReason.MissingRequiredWord;
            return CreateRejection(candidate, rejectionReason, checks, scoringFactors,
                $"Rejected: {requiredWordCheck.Details}");
        }
        
        // Check format
        var formatCheck = CheckFormat(candidate);
        checks.Add(formatCheck);
        if (!formatCheck.Passed)
        {
            rejectionReason = RejectionReason.UnsupportedFormat;
            return CreateRejection(candidate, rejectionReason, checks, scoringFactors,
                $"Rejected: {formatCheck.Details}");
        }
        
        // Check size
        var sizeCheck = CheckSize(candidate);
        checks.Add(sizeCheck);
        if (!sizeCheck.Passed)
        {
            rejectionReason = candidate.Size < GetMinSize(candidate.IsCollection) 
                ? RejectionReason.TooSmall 
                : RejectionReason.TooLarge;
            return CreateRejection(candidate, rejectionReason, checks, scoringFactors,
                $"Rejected: {sizeCheck.Details}");
        }
        
        // === SCORING ===
        
        int baseScore = 0;
        int penalties = 0;
        
        // Format scoring
        var formatScore = ScoreFormat(candidate);
        if (formatScore.Points != 0)
        {
            scoringFactors.Add(formatScore);
            baseScore += Math.Max(0, formatScore.Points);
            penalties += Math.Max(0, -formatScore.Points);
        }
        
        // Series match scoring
        var seriesScore = ScoreSeriesMatch(candidate, target);
        scoringFactors.Add(seriesScore);
        baseScore += Math.Max(0, seriesScore.Points);
        
        // Issue/Edition match scoring
        if (target.IsCollection)
        {
            var editionScore = ScoreEditionMatch(candidate, target);
            scoringFactors.Add(editionScore);
            baseScore += Math.Max(0, editionScore.Points);
        }
        else
        {
            var issueScore = ScoreIssueMatch(candidate, target);
            scoringFactors.Add(issueScore);
            baseScore += Math.Max(0, issueScore.Points);
        }
        
        // Year match scoring
        if (target.Year.HasValue && candidate.Year.HasValue)
        {
            var yearScore = ScoreYearMatch(candidate, target);
            scoringFactors.Add(yearScore);
            baseScore += Math.Max(0, yearScore.Points);
        }
        
        // Source priority scoring (always add even if 0 for transparency)
        var sourceScore = ScoreSourcePriority(candidate);
        scoringFactors.Add(sourceScore);
        if (sourceScore.Points < 0)
        {
            penalties += -sourceScore.Points;
        }
        
        int finalScore = baseScore - penalties;
        
        // Add pass check for all rejection checks
        checks.Add(new CheckResult("AllChecks", true, "All rejection checks passed"));
        
        var summary = $"Accepted with score {finalScore} (base: {baseScore}, penalties: {penalties})";
        
        return new CandidateEvaluation
        {
            Candidate = candidate,
            Accepted = true,
            Score = finalScore,
            RejectionReason = null,
            Explanation = new DecisionExplanation
            {
                ScoringFactors = scoringFactors,
                Checks = checks,
                Summary = summary,
                BaseScore = baseScore,
                Penalties = penalties,
                FinalScore = finalScore
            }
        };
    }

    public IReadOnlyList<CandidateEvaluation> EvaluateAndRank(
        IEnumerable<Candidate> candidates, 
        CandidateTarget target)
    {
        var evaluations = candidates
            .Select(c => Evaluate(c, target))
            .ToList();
        
        // Sort by: Accepted (true first), then Score (descending), then deterministic tie-break
        return evaluations
            .OrderByDescending(e => e.Accepted)
            .ThenByDescending(e => e.Score)
            .ThenBy(e => e.Candidate.Source) // Alphabetical source for tie-break
            .ThenBy(e => e.Candidate.ReleaseTitle) // Alphabetical title for final tie-break
            .ToList();
    }

    public CandidateEvaluation? GetBestCandidate(
        IEnumerable<Candidate> candidates, 
        CandidateTarget target)
    {
        var ranked = EvaluateAndRank(candidates, target);
        return ranked.FirstOrDefault(e => e.Accepted);
    }

    public (bool ShouldAutoGrab, string Reason) CheckAutoGrab(
        IReadOnlyList<CandidateEvaluation> rankedCandidates)
    {
        if (!_settings.AutoGrabEnabled)
        {
            return (false, "Auto-grab is disabled");
        }
        
        var accepted = rankedCandidates.Where(e => e.Accepted).ToList();
        
        if (accepted.Count == 0)
        {
            return (false, "No acceptable candidates");
        }
        
        var best = accepted[0];
        
        if (best.Score < _settings.AutoGrabThreshold)
        {
            return (false, $"Best score {best.Score} below threshold {_settings.AutoGrabThreshold}");
        }
        
        // Check if manual choice is needed (multiple candidates within margin)
        if (accepted.Count > 1)
        {
            var second = accepted[1];
            if (best.Score - second.Score <= _settings.ManualChoiceMargin)
            {
                return (false, $"Multiple candidates within {_settings.ManualChoiceMargin} point margin - manual selection required");
            }
        }
        
        return (true, $"Auto-grab approved with score {best.Score}");
    }

    // === CHECK METHODS ===
    
    private CheckResult CheckBannedWords(Candidate candidate)
    {
        var title = candidate.ReleaseTitle.ToLowerInvariant();
        foreach (var banned in _settings.BannedWords)
        {
            if (title.Contains(banned.ToLowerInvariant()))
            {
                return new CheckResult("BannedWords", false, $"Contains banned word: '{banned}'");
            }
        }
        return new CheckResult("BannedWords", true, "No banned words found");
    }

    private CheckResult CheckRequiredWords(Candidate candidate)
    {
        if (_settings.RequiredWords.Count == 0)
        {
            return new CheckResult("RequiredWords", true, "No required words configured");
        }
        
        var title = candidate.ReleaseTitle.ToLowerInvariant();
        foreach (var required in _settings.RequiredWords)
        {
            if (!title.Contains(required.ToLowerInvariant()))
            {
                return new CheckResult("RequiredWords", false, $"Missing required word: '{required}'");
            }
        }
        return new CheckResult("RequiredWords", true, "All required words present");
    }

    private CheckResult CheckFormat(Candidate candidate)
    {
        if (string.IsNullOrEmpty(candidate.Format))
        {
            return new CheckResult("Format", true, "No format specified - allowing");
        }
        
        var format = candidate.Format.ToLowerInvariant();
        var allowed = _settings.FormatPreferenceOrder.Select(f => f.ToLowerInvariant()).ToList();
        
        if (allowed.Count > 0 && !allowed.Contains(format))
        {
            return new CheckResult("Format", false, $"Format '{format}' not in allowed list");
        }
        
        return new CheckResult("Format", true, $"Format '{format}' is allowed");
    }

    private CheckResult CheckSize(Candidate candidate)
    {
        if (!candidate.Size.HasValue)
        {
            return new CheckResult("Size", true, "Size unknown - allowing");
        }
        
        var size = candidate.Size.Value;
        var minSize = GetMinSize(candidate.IsCollection);
        var maxSize = GetMaxSize(candidate.IsCollection);
        
        if (minSize > 0 && size < minSize)
        {
            return new CheckResult("Size", false, $"Size {FormatSize(size)} below minimum {FormatSize(minSize)}");
        }
        
        if (maxSize > 0 && size > maxSize)
        {
            return new CheckResult("Size", false, $"Size {FormatSize(size)} exceeds maximum {FormatSize(maxSize)}");
        }
        
        return new CheckResult("Size", true, $"Size {FormatSize(size)} within limits");
    }

    // === SCORING METHODS ===

    private ScoringFactor ScoreFormat(Candidate candidate)
    {
        if (string.IsNullOrEmpty(candidate.Format))
        {
            return new ScoringFactor("Format", 0, "No format specified");
        }
        
        var format = candidate.Format.ToLowerInvariant();
        var index = _settings.FormatPreferenceOrder
            .Select(f => f.ToLowerInvariant())
            .ToList()
            .IndexOf(format);
        
        if (index == 0)
        {
            return new ScoringFactor("Format", _settings.FormatMatchPoints, $"Preferred format: {format}");
        }
        else if (index > 0)
        {
            var penalty = index * 5; // 5 points per rank below preferred
            return new ScoringFactor("Format", _settings.FormatMatchPoints - penalty, 
                $"Format {format} (rank {index + 1})");
        }
        
        return new ScoringFactor("Format", 0, $"Format {format} not in preference list");
    }

    private ScoringFactor ScoreSeriesMatch(Candidate candidate, CandidateTarget target)
    {
        if (string.IsNullOrEmpty(candidate.SeriesTitle))
        {
            return new ScoringFactor("SeriesMatch", 0, "No series title parsed");
        }
        
        var candidateSeries = NormalizeTitle(candidate.SeriesTitle);
        var targetSeries = NormalizeTitle(target.SeriesTitle);
        
        if (candidateSeries == targetSeries)
        {
            return new ScoringFactor("SeriesMatch", _settings.ExactSeriesMatchPoints, "Exact series title match");
        }
        
        if (candidateSeries.Contains(targetSeries) || targetSeries.Contains(candidateSeries))
        {
            return new ScoringFactor("SeriesMatch", _settings.PartialSeriesMatchPoints, "Partial series title match");
        }
        
        return new ScoringFactor("SeriesMatch", 0, "Series title does not match");
    }

    private ScoringFactor ScoreIssueMatch(Candidate candidate, CandidateTarget target)
    {
        if (!target.IssueNumber.HasValue)
        {
            return new ScoringFactor("IssueMatch", 0, "No target issue number");
        }
        
        if (!candidate.IssueNumber.HasValue)
        {
            return new ScoringFactor("IssueMatch", 0, "No issue number parsed");
        }
        
        if (candidate.IssueNumber == target.IssueNumber)
        {
            return new ScoringFactor("IssueMatch", _settings.ExactIssueMatchPoints, 
                $"Exact issue match: #{target.IssueNumber}");
        }
        
        return new ScoringFactor("IssueMatch", 0, 
            $"Issue mismatch: candidate #{candidate.IssueNumber} vs target #{target.IssueNumber}");
    }

    private ScoringFactor ScoreEditionMatch(Candidate candidate, CandidateTarget target)
    {
        if (string.IsNullOrEmpty(target.EditionTitle))
        {
            // For collections without specific edition target, just verify it's a collection
            if (candidate.IsCollection)
            {
                return new ScoringFactor("EditionMatch", 15, "Is a collection");
            }
            return new ScoringFactor("EditionMatch", 0, "Not a collection");
        }
        
        var candidateTitle = NormalizeTitle(candidate.ReleaseTitle);
        var targetEdition = NormalizeTitle(target.EditionTitle);
        
        if (candidateTitle.Contains(targetEdition) || targetEdition.Contains(candidateTitle))
        {
            return new ScoringFactor("EditionMatch", _settings.ExactIssueMatchPoints, "Edition title match");
        }
        
        if (candidate.VolumeNumber.HasValue && target.VolumeNumber.HasValue &&
            candidate.VolumeNumber == target.VolumeNumber)
        {
            return new ScoringFactor("EditionMatch", 15, $"Volume {target.VolumeNumber} match");
        }
        
        return new ScoringFactor("EditionMatch", 0, "Edition does not match");
    }

    private ScoringFactor ScoreYearMatch(Candidate candidate, CandidateTarget target)
    {
        if (!target.Year.HasValue || !candidate.Year.HasValue)
        {
            return new ScoringFactor("YearMatch", 0, "Year not available for comparison");
        }
        
        if (candidate.Year == target.Year)
        {
            return new ScoringFactor("YearMatch", _settings.YearMatchPoints, $"Year match: {target.Year}");
        }
        
        var diff = Math.Abs(candidate.Year.Value - target.Year.Value);
        if (diff <= 1)
        {
            return new ScoringFactor("YearMatch", _settings.YearMatchPoints / 2, 
                $"Year close: {candidate.Year} vs {target.Year}");
        }
        
        return new ScoringFactor("YearMatch", 0, $"Year mismatch: {candidate.Year} vs {target.Year}");
    }

    private ScoringFactor ScoreSourcePriority(Candidate candidate)
    {
        if (_settings.SourcePriority.Count == 0)
        {
            return new ScoringFactor("SourcePriority", 0, "No source priority configured");
        }
        
        var index = _settings.SourcePriority
            .Select(s => s.ToLowerInvariant())
            .ToList()
            .IndexOf(candidate.Source.ToLowerInvariant());
        
        if (index < 0)
        {
            // Source not in priority list - apply max penalty
            var maxPenalty = _settings.SourcePriority.Count * _settings.SourcePriorityPenalty;
            return new ScoringFactor("SourcePriority", -maxPenalty, 
                $"Source '{candidate.Source}' not in priority list");
        }
        
        if (index == 0)
        {
            return new ScoringFactor("SourcePriority", 0, $"Top priority source: {candidate.Source}");
        }
        
        var penalty = index * _settings.SourcePriorityPenalty;
        return new ScoringFactor("SourcePriority", -penalty, 
            $"Source '{candidate.Source}' at priority {index + 1} (-{penalty} points)");
    }

    // === HELPERS ===

    private static CandidateEvaluation CreateRejection(
        Candidate candidate, 
        RejectionReason reason, 
        List<CheckResult> checks,
        List<ScoringFactor> factors,
        string summary)
    {
        return new CandidateEvaluation
        {
            Candidate = candidate,
            Accepted = false,
            Score = 0,
            RejectionReason = reason,
            Explanation = new DecisionExplanation
            {
                ScoringFactors = factors,
                Checks = checks,
                Summary = summary,
                BaseScore = 0,
                Penalties = 0,
                FinalScore = 0
            }
        };
    }

    private long GetMinSize(bool isCollection) => 
        isCollection ? _settings.MinSizeBytesCollections : _settings.MinSizeBytesSingles;

    private long GetMaxSize(bool isCollection) => 
        isCollection ? _settings.MaxSizeBytesCollections : _settings.MaxSizeBytesSingles;

    private static string NormalizeTitle(string title) => 
        title.ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Trim();

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1}GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1}MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F1}KB";
        return $"{bytes}B";
    }
}

