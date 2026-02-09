using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Nzb;

namespace Shortboxerr.Infrastructure.Nzb;

/// <summary>
/// Implementation of NZB candidate filtering.
/// </summary>
public class NzbFilterService : INzbFilterService
{
    private readonly ILogger<NzbFilterService> _logger;
    private readonly INzbReleaseParser _parser;
    
    public NzbFilterService(ILogger<NzbFilterService> logger, INzbReleaseParser parser)
    {
        _logger = logger;
        _parser = parser;
    }
    
    /// <inheritdoc />
    public NzbFilterResult Filter(NzbCandidate candidate, NzbFilterSettings? settings = null)
    {
        settings ??= NzbFilterSettings.Default;
        var checks = new List<NzbFilterCheck>();
        var scoreAdjustment = 0;
        
        // Age checks
        if (settings.MinAgeDays > 0)
        {
            var passed = candidate.Age >= settings.MinAgeDays;
            checks.Add(new NzbFilterCheck("MinAge", passed, $"Age {candidate.Age} days >= {settings.MinAgeDays}"));
            if (!passed)
            {
                return NzbFilterResult.Reject(NzbRejectionReason.TooNew, 
                    $"Release too new: {candidate.Age} days < minimum {settings.MinAgeDays} days", checks);
            }
        }
        
        if (settings.MaxAgeDays > 0)
        {
            var passed = candidate.Age <= settings.MaxAgeDays;
            checks.Add(new NzbFilterCheck("MaxAge", passed, $"Age {candidate.Age} days <= {settings.MaxAgeDays}"));
            if (!passed)
            {
                return NzbFilterResult.Reject(NzbRejectionReason.TooOld, 
                    $"Release too old: {candidate.Age} days > maximum {settings.MaxAgeDays} days", checks);
            }
        }
        
        // Size checks
        if (settings.MinSizeBytes > 0)
        {
            var passed = candidate.Size >= settings.MinSizeBytes;
            checks.Add(new NzbFilterCheck("MinSize", passed, $"Size {FormatSize(candidate.Size)} >= {FormatSize(settings.MinSizeBytes)}"));
            if (!passed)
            {
                return NzbFilterResult.Reject(NzbRejectionReason.TooSmall, 
                    $"Release too small: {FormatSize(candidate.Size)} < minimum {FormatSize(settings.MinSizeBytes)}", checks);
            }
        }
        
        if (settings.MaxSizeBytes > 0)
        {
            var passed = candidate.Size <= settings.MaxSizeBytes;
            checks.Add(new NzbFilterCheck("MaxSize", passed, $"Size {FormatSize(candidate.Size)} <= {FormatSize(settings.MaxSizeBytes)}"));
            if (!passed)
            {
                return NzbFilterResult.Reject(NzbRejectionReason.TooLarge, 
                    $"Release too large: {FormatSize(candidate.Size)} > maximum {FormatSize(settings.MaxSizeBytes)}", checks);
            }
        }
        
        // Password protection check
        if (settings.RejectPasswordProtected && candidate.IsPasswordProtected)
        {
            checks.Add(new NzbFilterCheck("PasswordProtected", false, "Release is password protected"));
            return NzbFilterResult.Reject(NzbRejectionReason.PasswordProtected, 
                "Release is password protected", checks);
        }
        checks.Add(new NzbFilterCheck("PasswordProtected", true, "Not password protected"));
        
        // Banned words check
        foreach (var bannedWord in settings.BannedWords)
        {
            if (candidate.ReleaseTitle.Contains(bannedWord, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(new NzbFilterCheck("BannedWord", false, $"Contains banned word: {bannedWord}"));
                return NzbFilterResult.Reject(NzbRejectionReason.BannedWordFound, 
                    $"Release contains banned word: {bannedWord}", checks);
            }
        }
        checks.Add(new NzbFilterCheck("BannedWords", true, $"No banned words found ({settings.BannedWords.Count} checked)"));
        
        // Required words check
        foreach (var requiredWord in settings.RequiredWords)
        {
            if (!candidate.ReleaseTitle.Contains(requiredWord, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(new NzbFilterCheck("RequiredWord", false, $"Missing required word: {requiredWord}"));
                return NzbFilterResult.Reject(NzbRejectionReason.MissingRequiredWord, 
                    $"Release missing required word: {requiredWord}", checks);
            }
        }
        if (settings.RequiredWords.Count > 0)
        {
            checks.Add(new NzbFilterCheck("RequiredWords", true, $"All required words found ({settings.RequiredWords.Count})"));
        }
        
        // Category checks
        if (settings.ExcludeCategories.Count > 0)
        {
            var excludedCategory = candidate.Categories.FirstOrDefault(c => settings.ExcludeCategories.Contains(c));
            if (excludedCategory != 0)
            {
                checks.Add(new NzbFilterCheck("ExcludeCategory", false, $"In excluded category: {excludedCategory}"));
                return NzbFilterResult.Reject(NzbRejectionReason.CategoryExcluded, 
                    $"Release in excluded category: {excludedCategory}", checks);
            }
        }
        
        if (settings.IncludeCategories.Count > 0)
        {
            var inIncludedCategory = candidate.Categories.Any(c => settings.IncludeCategories.Contains(c));
            if (!inIncludedCategory)
            {
                checks.Add(new NzbFilterCheck("IncludeCategory", false, "Not in any included category"));
                return NzbFilterResult.Reject(NzbRejectionReason.CategoryNotIncluded, 
                    "Release not in any included category", checks);
            }
            checks.Add(new NzbFilterCheck("IncludeCategory", true, "In included category"));
        }
        
        // Parse confidence check
        if (settings.MinParseConfidence > 0)
        {
            var passed = candidate.ParsedInfo.Confidence >= settings.MinParseConfidence;
            checks.Add(new NzbFilterCheck("ParseConfidence", passed, 
                $"Confidence {candidate.ParsedInfo.Confidence}% >= {settings.MinParseConfidence}%"));
            if (!passed)
            {
                return NzbFilterResult.Reject(NzbRejectionReason.LowConfidence, 
                    $"Parse confidence too low: {candidate.ParsedInfo.Confidence}% < {settings.MinParseConfidence}%", checks);
            }
        }
        
        // Quality/format checks (soft - affects score but doesn't reject)
        // Preferred words bonus
        foreach (var preferredWord in settings.PreferredWords)
        {
            if (candidate.ReleaseTitle.Contains(preferredWord, StringComparison.OrdinalIgnoreCase))
            {
                scoreAdjustment += 10;
                checks.Add(new NzbFilterCheck("PreferredWord", true, $"Contains preferred word: {preferredWord} (+10)"));
            }
        }
        
        // Format preference bonus
        if (!string.IsNullOrEmpty(candidate.ParsedInfo.Format))
        {
            var formatIndex = settings.PreferredFormats.FindIndex(f => 
                f.Equals(candidate.ParsedInfo.Format, StringComparison.OrdinalIgnoreCase));
            if (formatIndex >= 0)
            {
                var formatBonus = (settings.PreferredFormats.Count - formatIndex) * 5;
                scoreAdjustment += formatBonus;
                checks.Add(new NzbFilterCheck("PreferredFormat", true, 
                    $"Format {candidate.ParsedInfo.Format} at preference #{formatIndex + 1} (+{formatBonus})"));
            }
        }
        
        // PROPER/REPACK bonus
        if (settings.PreferProper && candidate.ParsedInfo.IsProper)
        {
            scoreAdjustment += 15;
            checks.Add(new NzbFilterCheck("Proper", true, "PROPER release (+15)"));
        }
        
        if (settings.PreferRepack && candidate.ParsedInfo.IsRepack)
        {
            scoreAdjustment += 10;
            checks.Add(new NzbFilterCheck("Repack", true, "REPACK release (+10)"));
        }
        
        // Preferred indexer bonus
        if (settings.PreferredIndexers.Count > 0 && !string.IsNullOrEmpty(candidate.IndexerId))
        {
            if (settings.PreferredIndexers.Contains(candidate.IndexerId, StringComparer.OrdinalIgnoreCase))
            {
                scoreAdjustment += 20;
                checks.Add(new NzbFilterCheck("PreferredIndexer", true, $"From preferred indexer: {candidate.IndexerName} (+20)"));
            }
        }
        
        _logger.LogDebug("Candidate {Title} passed filtering with score adjustment {Adjustment}", 
            candidate.ReleaseTitle, scoreAdjustment);
        
        return NzbFilterResult.Accept(scoreAdjustment, checks);
    }
    
    /// <inheritdoc />
    public IEnumerable<NzbCandidate> FilterMany(IEnumerable<NzbCandidate> candidates, NzbFilterSettings? settings = null)
    {
        settings ??= NzbFilterSettings.Default;
        
        foreach (var candidate in candidates)
        {
            var result = Filter(candidate, settings);
            
            if (result.Accepted)
            {
                // Update quality score with adjustment
                candidate.QualityScore = _parser.CalculateQualityScore(candidate.ParsedInfo) + result.ScoreAdjustment;
                yield return candidate;
            }
            else
            {
                candidate.IsFiltered = true;
                candidate.FilterReason = result.RejectionMessage;
                _logger.LogDebug("Filtered out candidate {Title}: {Reason}", 
                    candidate.ReleaseTitle, result.RejectionMessage);
            }
        }
    }
    
    /// <inheritdoc />
    public IReadOnlyList<NzbCandidate> FilterAndSort(IEnumerable<NzbCandidate> candidates, NzbFilterSettings? settings = null)
    {
        var filtered = FilterMany(candidates, settings).ToList();
        
        // Sort by quality score (descending), then by age (ascending - newer is better)
        filtered.Sort((a, b) =>
        {
            var scoreCompare = b.QualityScore.CompareTo(a.QualityScore);
            if (scoreCompare != 0) return scoreCompare;
            
            // Prefer newer releases for same quality
            return a.Age.CompareTo(b.Age);
        });
        
        _logger.LogDebug("Filtered and sorted {Total} candidates to {Accepted} accepted", 
            candidates.Count(), filtered.Count);
        
        return filtered;
    }
    
    /// <inheritdoc />
    public NzbFilterSettings GetDefaultSettings() => NzbFilterSettings.Default;
    
    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}
