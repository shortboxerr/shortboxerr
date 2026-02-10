using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Search;

/// <summary>
/// Implements Mylar3-style search result scoring.
/// Scores candidates based on quality, size, release group, match accuracy, and other factors.
/// </summary>
public class SearchResultScorer : ISearchResultScorer
{
    private readonly ISearchSettingsService _settingsService;
    private readonly ILogger<SearchResultScorer>? _logger;

    public SearchResultScorer(ISearchSettingsService settingsService, ILogger<SearchResultScorer>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public ScoredCandidate ScoreCandidate(Candidate candidate, SearchContext searchContext)
    {
        var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();
        return ScoreCandidateInternal(candidate, searchContext, settings);
    }

    public IReadOnlyList<ScoredCandidate> ScoreAndSort(IEnumerable<Candidate> candidates, SearchContext searchContext)
    {
        var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();
        
        var scored = candidates
            .Select(c => ScoreCandidateInternal(c, searchContext, settings))
            .OrderByDescending(s => s.TotalScore)
            .ThenBy(s => s.Candidate.SourcePriority)
            .ToList();

        _logger?.LogDebug("Scored {Count} candidates, best score: {BestScore}", 
            scored.Count, scored.FirstOrDefault()?.TotalScore ?? 0);

        return scored;
    }

    public ScoredCandidate? GetBestCandidate(IEnumerable<Candidate> candidates, SearchContext searchContext)
    {
        var sorted = ScoreAndSort(candidates, searchContext);
        return sorted.FirstOrDefault(s => s.MeetsThreshold);
    }

    private ScoredCandidate ScoreCandidateInternal(Candidate candidate, SearchContext context, SearchSettings settings)
    {
        var weights = settings.ScoringWeights;
        var breakdown = new ScoreBreakdown
        {
            Quality = ScoreQuality(candidate, settings, weights),
            Size = ScoreSize(candidate, settings, weights, context.SearchingForPack),
            ReleaseGroup = ScoreReleaseGroup(candidate, settings, weights),
            YearMatch = ScoreYearMatch(candidate, context, weights),
            IssueMatch = ScoreIssueMatch(candidate, context, weights),
            SeriesMatch = ScoreSeriesMatch(candidate, context, weights),
            Format = ScoreFormat(candidate, settings, weights),
            SourcePriority = ScoreSourcePriority(candidate, weights),
            Freshness = ScoreFreshness(candidate, weights),
            PreferredWords = ScorePreferredWords(candidate, settings, weights),
            BlacklistPenalty = ScoreBlacklistPenalty(candidate, settings, weights),
            MaxPossible = weights.MaxScore
        };

        var totalScore = breakdown.FinalScore;
        var normalizedScore = weights.MaxScore > 0 
            ? (double)totalScore / weights.MaxScore * 100 
            : 0;

        // Minimum threshold is 30% of max score
        var meetsThreshold = normalizedScore >= 30;

        return new ScoredCandidate
        {
            Candidate = candidate,
            TotalScore = totalScore,
            NormalizedScore = normalizedScore,
            Breakdown = breakdown,
            MeetsThreshold = meetsThreshold
        };
    }

    private static ScoreComponent ScoreQuality(Candidate candidate, SearchSettings settings, ScoringWeights weights)
    {
        var maxPoints = weights.QualityWeight;
        var releaseTitle = candidate.ReleaseTitle.ToLowerInvariant();
        var tags = candidate.Tags.Select(t => t.ToLowerInvariant()).ToList();

        // Detect quality from title and tags
        var detectedQuality = DetectQuality(releaseTitle, tags);
        
        // Score based on preference match
        int points;
        string reason;

        if (settings.PreferredQuality == PreferredQuality.Any)
        {
            points = maxPoints;
            reason = "Any quality accepted";
        }
        else if (detectedQuality == settings.PreferredQuality)
        {
            points = maxPoints;
            reason = $"Exact quality match: {detectedQuality}";
        }
        else
        {
            // Partial score for close quality tiers
            var qualityDiff = Math.Abs((int)detectedQuality - (int)settings.PreferredQuality);
            points = maxPoints - (qualityDiff * 25);
            points = Math.Max(0, points);
            reason = $"Quality: {detectedQuality} (preferred: {settings.PreferredQuality})";
        }

        return new ScoreComponent
        {
            Points = points,
            MaxPoints = maxPoints,
            Reason = reason
        };
    }

    private static PreferredQuality DetectQuality(string title, List<string> tags)
    {
        var combined = title + " " + string.Join(" ", tags);
        
        if (combined.Contains("digital") || combined.Contains("d-empire") || 
            combined.Contains("minutemen") || combined.Contains("dcp"))
        {
            return PreferredQuality.Digital;
        }
        
        if (combined.Contains("webrip") || combined.Contains("web-rip") || 
            combined.Contains("webdl") || combined.Contains("web-dl"))
        {
            return PreferredQuality.Webrip;
        }
        
        if (combined.Contains("scan") || combined.Contains("scanned") ||
            combined.Contains("c2c") || combined.Contains("noads"))
        {
            return PreferredQuality.Scan;
        }

        return PreferredQuality.Any;
    }

    private static ScoreComponent ScoreSize(Candidate candidate, SearchSettings settings, ScoringWeights weights, bool isPack)
    {
        var maxPoints = weights.SizeWeight;
        
        if (!candidate.Size.HasValue)
        {
            return new ScoreComponent
            {
                Points = maxPoints / 2,
                MaxPoints = maxPoints,
                Reason = "Size unknown, partial score"
            };
        }

        var sizeMb = candidate.Size.Value / (1024.0 * 1024.0);
        var ranges = settings.ExpectedSizeRanges;

        int minMb, maxMb, idealMb;
        if (isPack || candidate.IsCollection)
        {
            minMb = ranges.PackMinMb;
            maxMb = ranges.PackMaxMb;
            idealMb = (minMb + maxMb) / 2;
        }
        else
        {
            minMb = ranges.SingleIssueMinMb;
            maxMb = ranges.SingleIssueMaxMb;
            idealMb = ranges.SingleIssueIdealMb;
        }

        // Check hard limits from settings
        if (settings.MinSizeMb > 0 && sizeMb < settings.MinSizeMb)
        {
            return new ScoreComponent
            {
                Points = 0,
                MaxPoints = maxPoints,
                Reason = $"Below minimum size ({sizeMb:F1}MB < {settings.MinSizeMb}MB)"
            };
        }

        if (settings.MaxSizeMb > 0 && sizeMb > settings.MaxSizeMb)
        {
            return new ScoreComponent
            {
                Points = 0,
                MaxPoints = maxPoints,
                Reason = $"Above maximum size ({sizeMb:F1}MB > {settings.MaxSizeMb}MB)"
            };
        }

        // Score based on distance from ideal
        double score;
        string reason;

        if (sizeMb >= minMb && sizeMb <= maxMb)
        {
            // Within range, score based on proximity to ideal
            var deviation = Math.Abs(sizeMb - idealMb) / idealMb;
            score = maxPoints * (1 - Math.Min(deviation, 0.5)); // Max 50% reduction for being far from ideal
            reason = $"Size {sizeMb:F1}MB within range";
        }
        else if (sizeMb < minMb)
        {
            score = maxPoints * 0.5 * (sizeMb / minMb);
            reason = $"Size {sizeMb:F1}MB below expected range";
        }
        else
        {
            score = maxPoints * 0.5 * (maxMb / sizeMb);
            reason = $"Size {sizeMb:F1}MB above expected range";
        }

        return new ScoreComponent
        {
            Points = (int)Math.Max(0, score),
            MaxPoints = maxPoints,
            Reason = reason
        };
    }

    private static ScoreComponent ScoreReleaseGroup(Candidate candidate, SearchSettings settings, ScoringWeights weights)
    {
        var maxPoints = weights.ReleaseGroupWeight;
        var releaseTitle = candidate.ReleaseTitle;
        var trustedGroups = settings.TrustedReleaseGroups;

        // Extract release group from title (usually at the end after hyphen)
        var group = ExtractReleaseGroup(releaseTitle);

        if (string.IsNullOrEmpty(group))
        {
            return new ScoreComponent
            {
                Points = maxPoints / 4, // Small base score for unknown groups
                MaxPoints = maxPoints,
                Reason = "Release group not detected"
            };
        }

        var isTrusted = trustedGroups.StrictMatching
            ? trustedGroups.Groups.Any(g => g.Equals(group, StringComparison.OrdinalIgnoreCase))
            : trustedGroups.Groups.Any(g => group.Contains(g, StringComparison.OrdinalIgnoreCase) ||
                                             g.Contains(group, StringComparison.OrdinalIgnoreCase));

        if (isTrusted)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = $"Trusted release group: {group}"
            };
        }

        return new ScoreComponent
        {
            Points = maxPoints / 2,
            MaxPoints = maxPoints,
            Reason = $"Unknown release group: {group}"
        };
    }

    private static string ExtractReleaseGroup(string title)
    {
        // Common patterns:
        // "Title (2024) #001 (Digital) (Zone-Empire)" -> "Zone-Empire"
        // "Title 001 - Group" -> "Group"
        // "Title-GROUP" -> "GROUP"

        // Try parentheses pattern first
        var parenMatch = System.Text.RegularExpressions.Regex.Match(
            title, @"\(([A-Za-z][\w-]*(?:-[A-Za-z][\w-]*)?)\)\s*$");
        if (parenMatch.Success)
        {
            return parenMatch.Groups[1].Value;
        }

        // Try hyphen at end pattern
        var hyphenMatch = System.Text.RegularExpressions.Regex.Match(
            title, @"-([A-Za-z][\w-]*)\s*$");
        if (hyphenMatch.Success)
        {
            return hyphenMatch.Groups[1].Value;
        }

        return string.Empty;
    }

    private static ScoreComponent ScoreYearMatch(Candidate candidate, SearchContext context, ScoringWeights weights)
    {
        var maxPoints = weights.YearMatchWeight;

        if (!context.TargetYear.HasValue)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = "No target year specified"
            };
        }

        if (!candidate.Year.HasValue)
        {
            return new ScoreComponent
            {
                Points = maxPoints / 2,
                MaxPoints = maxPoints,
                Reason = "Year not detected in release"
            };
        }

        var diff = Math.Abs(candidate.Year.Value - context.TargetYear.Value);

        if (diff == 0)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = $"Exact year match: {candidate.Year}"
            };
        }

        if (diff <= 1)
        {
            return new ScoreComponent
            {
                Points = (int)(maxPoints * 0.75),
                MaxPoints = maxPoints,
                Reason = $"Year close: {candidate.Year} (target: {context.TargetYear})"
            };
        }

        // Penalize more for larger year differences
        var score = maxPoints * Math.Max(0, 1 - (diff * 0.2));
        return new ScoreComponent
        {
            Points = (int)score,
            MaxPoints = maxPoints,
            Reason = $"Year mismatch: {candidate.Year} (target: {context.TargetYear})"
        };
    }

    private static ScoreComponent ScoreIssueMatch(Candidate candidate, SearchContext context, ScoringWeights weights)
    {
        var maxPoints = weights.IssueMatchWeight;

        if (!context.TargetIssueNumber.HasValue)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = "No target issue specified"
            };
        }

        if (!candidate.IssueNumber.HasValue)
        {
            // If it's a collection, that's fine for packs
            if (candidate.IsCollection && context.SearchingForPack)
            {
                return new ScoreComponent
                {
                    Points = maxPoints,
                    MaxPoints = maxPoints,
                    Reason = "Collection (pack search)"
                };
            }

            return new ScoreComponent
            {
                Points = 0,
                MaxPoints = maxPoints,
                Reason = "Issue number not detected"
            };
        }

        if (candidate.IssueNumber == context.TargetIssueNumber)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = $"Exact issue match: #{candidate.IssueNumber}"
            };
        }

        // Wrong issue number is a critical mismatch
        return new ScoreComponent
        {
            Points = 0,
            MaxPoints = maxPoints,
            Reason = $"Issue mismatch: #{candidate.IssueNumber} (target: #{context.TargetIssueNumber})"
        };
    }

    private static ScoreComponent ScoreSeriesMatch(Candidate candidate, SearchContext context, ScoringWeights weights)
    {
        var maxPoints = weights.SeriesMatchWeight;

        if (string.IsNullOrEmpty(candidate.SeriesTitle))
        {
            return new ScoreComponent
            {
                Points = maxPoints / 4,
                MaxPoints = maxPoints,
                Reason = "Series title not parsed"
            };
        }

        var candidateTitle = NormalizeTitle(candidate.SeriesTitle);
        var targetTitle = NormalizeTitle(context.TargetSeriesTitle);

        // Exact match
        if (candidateTitle.Equals(targetTitle, StringComparison.OrdinalIgnoreCase))
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = $"Exact series match: {candidate.SeriesTitle}"
            };
        }

        // Contains match
        if (candidateTitle.Contains(targetTitle, StringComparison.OrdinalIgnoreCase) ||
            targetTitle.Contains(candidateTitle, StringComparison.OrdinalIgnoreCase))
        {
            return new ScoreComponent
            {
                Points = (int)(maxPoints * 0.75),
                MaxPoints = maxPoints,
                Reason = $"Partial series match: {candidate.SeriesTitle}"
            };
        }

        // Calculate similarity
        var similarity = CalculateSimilarity(candidateTitle, targetTitle);
        var score = (int)(maxPoints * similarity);

        return new ScoreComponent
        {
            Points = score,
            MaxPoints = maxPoints,
            Reason = $"Series similarity {similarity:P0}: {candidate.SeriesTitle}"
        };
    }

    private static string NormalizeTitle(string title)
    {
        // Remove common noise: "the", volume indicators, years, etc.
        var normalized = title.ToLowerInvariant()
            .Replace("the ", "")
            .Replace("a ", "")
            .Replace("an ", "");

        // Remove parenthetical content
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s*\([^)]*\)", "");
        
        // Remove special characters
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s]", "");
        
        // Collapse whitespace
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    private static double CalculateSimilarity(string a, string b)
    {
        // Simple Levenshtein-based similarity
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var maxLen = Math.Max(a.Length, b.Length);
        var distance = LevenshteinDistance(a, b);
        return 1.0 - ((double)distance / maxLen);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static ScoreComponent ScoreFormat(Candidate candidate, SearchSettings settings, ScoringWeights weights)
    {
        var maxPoints = weights.FormatWeight;
        var format = candidate.Format?.ToLowerInvariant() ?? "";

        if (string.IsNullOrEmpty(format))
        {
            return new ScoreComponent
            {
                Points = maxPoints / 2,
                MaxPoints = maxPoints,
                Reason = "Format unknown"
            };
        }

        // CBZ-only mode
        if (settings.CbzOnly && format != "cbz")
        {
            return new ScoreComponent
            {
                Points = 0,
                MaxPoints = maxPoints,
                Reason = $"Format {format} not allowed (CBZ only)"
            };
        }

        // Score based on preference order
        var preferenceIndex = settings.FormatPreference
            .FindIndex(f => f.Equals(format, StringComparison.OrdinalIgnoreCase));

        if (preferenceIndex == 0)
        {
            return new ScoreComponent
            {
                Points = maxPoints,
                MaxPoints = maxPoints,
                Reason = $"Preferred format: {format}"
            };
        }

        if (preferenceIndex > 0)
        {
            var score = maxPoints * (1 - (preferenceIndex * 0.25));
            return new ScoreComponent
            {
                Points = (int)Math.Max(0, score),
                MaxPoints = maxPoints,
                Reason = $"Format {format} (preference #{preferenceIndex + 1})"
            };
        }

        return new ScoreComponent
        {
            Points = maxPoints / 4,
            MaxPoints = maxPoints,
            Reason = $"Format {format} not in preferences"
        };
    }

    private static ScoreComponent ScoreSourcePriority(Candidate candidate, ScoringWeights weights)
    {
        var maxPoints = weights.SourcePriorityWeight;
        var priority = candidate.SourcePriority;

        // Priority 1 = full score, decreasing by 10% per priority level
        var score = maxPoints * Math.Max(0, 1 - ((priority - 1) * 0.1));

        return new ScoreComponent
        {
            Points = (int)score,
            MaxPoints = maxPoints,
            Reason = $"Source priority: {priority} ({candidate.Source})"
        };
    }

    private static ScoreComponent ScoreFreshness(Candidate candidate, ScoringWeights weights)
    {
        var maxPoints = weights.FreshnessWeight;
        var age = (DateTime.UtcNow - candidate.DiscoveredAt).TotalDays;

        // Fresh releases (< 7 days) get full points
        // Score decreases over time
        double score;
        string reason;

        if (age < 7)
        {
            score = maxPoints;
            reason = $"Fresh release ({age:F0} days old)";
        }
        else if (age < 30)
        {
            score = maxPoints * 0.75;
            reason = $"Recent release ({age:F0} days old)";
        }
        else if (age < 90)
        {
            score = maxPoints * 0.5;
            reason = $"Older release ({age:F0} days old)";
        }
        else
        {
            score = maxPoints * 0.25;
            reason = $"Old release ({age:F0} days old)";
        }

        return new ScoreComponent
        {
            Points = (int)score,
            MaxPoints = maxPoints,
            Reason = reason
        };
    }

    private static ScoreComponent ScorePreferredWords(Candidate candidate, SearchSettings settings, ScoringWeights weights)
    {
        var bonusPerWord = weights.PreferredWordBonus;
        var title = candidate.ReleaseTitle.ToLowerInvariant();
        var foundWords = settings.PreferredWords
            .Where(w => title.Contains(w.ToLowerInvariant()))
            .ToList();

        var totalBonus = foundWords.Count * bonusPerWord;

        return new ScoreComponent
        {
            Points = totalBonus,
            MaxPoints = settings.PreferredWords.Count * bonusPerWord,
            Reason = foundWords.Count > 0 
                ? $"Preferred words: {string.Join(", ", foundWords)}"
                : "No preferred words found"
        };
    }

    private static ScoreComponent ScoreBlacklistPenalty(Candidate candidate, SearchSettings settings, ScoringWeights weights)
    {
        var penaltyPerWord = weights.BlacklistWordPenalty;
        var title = candidate.ReleaseTitle.ToLowerInvariant();
        var foundWords = settings.BlacklistWords
            .Where(w => title.Contains(w.ToLowerInvariant()))
            .ToList();

        var totalPenalty = foundWords.Count * penaltyPerWord;

        return new ScoreComponent
        {
            Points = totalPenalty,
            MaxPoints = 0, // This is a penalty, not positive points
            Reason = foundWords.Count > 0
                ? $"Blacklist penalty: {string.Join(", ", foundWords)}"
                : "No blacklist words found"
        };
    }
}
