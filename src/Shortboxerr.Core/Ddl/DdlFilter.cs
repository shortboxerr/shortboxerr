namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Filters DDL candidates based on configurable rules.
/// Implements Mylar3-compatible filtering logic.
/// </summary>
public class DdlFilter : IDdlFilter
{
    public IReadOnlyList<DdlCandidate> Filter(IEnumerable<DdlCandidate> candidates, DdlFilterSettings settings)
    {
        var results = new List<DdlCandidate>();
        
        foreach (var candidate in candidates)
        {
            var (passes, reason) = CheckCandidate(candidate, settings);
            
            if (!passes)
            {
                candidate.IsFiltered = true;
                candidate.FilterReason = reason;
            }
            
            results.Add(candidate);
        }
        
        return results;
    }

    public (bool Passes, string? Reason) CheckCandidate(DdlCandidate candidate, DdlFilterSettings settings)
    {
        // Check banned words
        var bannedCheck = CheckBannedWords(candidate.ReleaseTitle, settings.BannedWords);
        if (!bannedCheck.Passes)
        {
            return bannedCheck;
        }
        
        // Check required words
        var requiredCheck = CheckRequiredWords(candidate.ReleaseTitle, settings.RequiredWords);
        if (!requiredCheck.Passes)
        {
            return requiredCheck;
        }
        
        // Check format
        var formatCheck = CheckFormat(candidate.ParsedInfo.Format, settings);
        if (!formatCheck.Passes)
        {
            return formatCheck;
        }
        
        // Check size
        if (candidate.Size.HasValue)
        {
            var sizeCheck = CheckSize(candidate.Size.Value, candidate.ParsedInfo.IsCollection, settings);
            if (!sizeCheck.Passes)
            {
                return sizeCheck;
            }
        }
        
        // Check parse confidence
        if (settings.MinParseConfidence > 0 && candidate.ParsedInfo.Confidence < settings.MinParseConfidence)
        {
            return (false, $"Parse confidence {candidate.ParsedInfo.Confidence} below minimum {settings.MinParseConfidence}");
        }
        
        // Check series title requirement
        if (settings.RequireSeriesTitle && string.IsNullOrWhiteSpace(candidate.ParsedInfo.SeriesTitle))
        {
            return (false, "No series title could be parsed");
        }
        
        // Check year requirement
        if (settings.RequireYear && !candidate.ParsedInfo.Year.HasValue)
        {
            return (false, "No year information found");
        }
        
        // Check blocked groups
        if (!string.IsNullOrEmpty(candidate.ParsedInfo.ReleaseGroup))
        {
            var groupCheck = CheckReleaseGroup(candidate.ParsedInfo.ReleaseGroup, settings.BlockedGroups);
            if (!groupCheck.Passes)
            {
                return groupCheck;
            }
        }
        
        return (true, null);
    }

    private static (bool Passes, string? Reason) CheckBannedWords(string title, IEnumerable<string> bannedWords)
    {
        var lowerTitle = title.ToLowerInvariant();
        
        foreach (var word in bannedWords)
        {
            if (lowerTitle.Contains(word.ToLowerInvariant()))
            {
                return (false, $"Contains banned word: '{word}'");
            }
        }
        
        return (true, null);
    }

    private static (bool Passes, string? Reason) CheckRequiredWords(string title, IEnumerable<string> requiredWords)
    {
        var words = requiredWords.ToList();
        if (words.Count == 0)
        {
            return (true, null);
        }
        
        var lowerTitle = title.ToLowerInvariant();
        
        foreach (var word in words)
        {
            if (!lowerTitle.Contains(word.ToLowerInvariant()))
            {
                return (false, $"Missing required word: '{word}'");
            }
        }
        
        return (true, null);
    }

    private static (bool Passes, string? Reason) CheckFormat(string? format, DdlFilterSettings settings)
    {
        if (string.IsNullOrEmpty(format))
        {
            // No format detected - allow if not required
            return settings.RequirePreferredFormat 
                ? (false, "No file format detected") 
                : (true, null);
        }
        
        var lowerFormat = format.ToLowerInvariant();
        
        // Check blocked formats
        if (settings.BlockedFormats.Any(f => f.ToLowerInvariant() == lowerFormat))
        {
            return (false, $"Blocked format: '{format}'");
        }
        
        // Check if format is preferred (if required)
        if (settings.RequirePreferredFormat)
        {
            if (!settings.PreferredFormats.Any(f => f.ToLowerInvariant() == lowerFormat))
            {
                return (false, $"Format '{format}' not in preferred list");
            }
        }
        
        return (true, null);
    }

    private static (bool Passes, string? Reason) CheckSize(long sizeBytes, bool isCollection, DdlFilterSettings settings)
    {
        var minSize = isCollection ? settings.MinSizeBytesCollections : settings.MinSizeBytesSingles;
        var maxSize = isCollection ? settings.MaxSizeBytesCollections : settings.MaxSizeBytesSingles;
        var type = isCollection ? "collection" : "single";
        
        if (minSize > 0 && sizeBytes < minSize)
        {
            return (false, $"Size {FormatSize(sizeBytes)} below minimum {FormatSize(minSize)} for {type}");
        }
        
        if (maxSize > 0 && sizeBytes > maxSize)
        {
            return (false, $"Size {FormatSize(sizeBytes)} exceeds maximum {FormatSize(maxSize)} for {type}");
        }
        
        return (true, null);
    }

    private static (bool Passes, string? Reason) CheckReleaseGroup(string group, IEnumerable<string> blockedGroups)
    {
        var lowerGroup = group.ToLowerInvariant();
        
        foreach (var blocked in blockedGroups)
        {
            if (lowerGroup.Equals(blocked.ToLowerInvariant()))
            {
                return (false, $"Blocked release group: '{group}'");
            }
        }
        
        return (true, null);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1}GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1}MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F1}KB";
        return $"{bytes}B";
    }
}



