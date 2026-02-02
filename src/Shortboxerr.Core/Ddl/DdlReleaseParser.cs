using System.Text.RegularExpressions;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Parses DDL release titles into structured information.
/// Implements Mylar3-compatible parsing rules for comic book releases.
/// </summary>
public partial class DdlReleaseParser : IDdlReleaseParser
{
    // Common edition type indicators
    private static readonly string[] CollectionTypes = new[]
    {
        "TPB", "Trade Paperback", "Trade",
        "HC", "Hardcover", "Hard Cover",
        "Omnibus", "Omni",
        "Deluxe", "Deluxe Edition",
        "Absolute", "Absolute Edition",
        "Compendium",
        "Complete Collection", "Complete",
        "Library Edition",
        "Ultimate Collection",
        "Book"
    };
    
    // Common quality indicators
    private static readonly string[] QualityTags = new[]
    {
        "Digital", "Digital Edition",
        "Webrip", "Web-Rip",
        "Scan", "Scanned",
        "HD", "HQ",
        "Proper", "Repack"
    };
    
    // Publishers (for extraction)
    private static readonly string[] KnownPublishers = new[]
    {
        "Marvel", "DC", "DC Comics",
        "Image", "Image Comics",
        "Dark Horse", "Dark Horse Comics",
        "IDW", "IDW Publishing",
        "Boom", "Boom Studios", "Boom! Studios",
        "Dynamite", "Dynamite Entertainment",
        "Valiant", "Valiant Comics",
        "Oni", "Oni Press",
        "Archie", "Archie Comics",
        "Vertigo",
        "Aftershock",
        "AWA", "AWA Studios",
        "Titan", "Titan Comics",
        "Zenescope"
    };

    public DdlParsedInfo Parse(string releaseTitle)
    {
        if (string.IsNullOrWhiteSpace(releaseTitle))
        {
            return new DdlParsedInfo { Confidence = 0 };
        }
        
        var info = new DdlParsedInfo();
        var workingTitle = releaseTitle.Trim();
        var confidence = 0;
        
        // Extract format first (and remove from working title)
        info.Format = ExtractFormat(workingTitle);
        if (info.Format != null)
        {
            workingTitle = RemoveExtension(workingTitle);
            confidence += 10;
        }
        
        // Tokenize
        info.Tokens.AddRange(Tokenize(workingTitle));
        
        // Extract year (commonly in parentheses or at end)
        var (year, titleAfterYear) = ExtractYear(workingTitle);
        if (year.HasValue)
        {
            info.Year = year;
            workingTitle = titleAfterYear;
            confidence += 10;
        }
        
        // Check for collection types
        var (isCollection, editionType, titleAfterEdition) = ExtractEditionType(workingTitle);
        info.IsCollection = isCollection;
        info.EditionType = editionType;
        if (isCollection)
        {
            workingTitle = titleAfterEdition;
            confidence += 10;
        }
        
        // Extract volume number
        var (volumeNumber, titleAfterVolume) = ExtractVolumeNumber(workingTitle);
        if (volumeNumber.HasValue)
        {
            info.VolumeNumber = volumeNumber;
            workingTitle = titleAfterVolume;
            confidence += 10;
        }
        
        // Extract issue number (for singles)
        if (!info.IsCollection)
        {
            var (issueNumber, titleAfterIssue) = ExtractIssueNumber(workingTitle);
            if (issueNumber.HasValue)
            {
                info.IssueNumber = issueNumber;
                workingTitle = titleAfterIssue;
                confidence += 15;
            }
        }
        else
        {
            // Extract issue range for collections
            var (issueRange, titleAfterRange) = ExtractIssueRange(workingTitle);
            if (!string.IsNullOrEmpty(issueRange))
            {
                info.IssueRange = issueRange;
                workingTitle = titleAfterRange;
                confidence += 10;
            }
        }
        
        // Extract publisher
        var (publisher, titleAfterPublisher) = ExtractPublisher(workingTitle);
        if (!string.IsNullOrEmpty(publisher))
        {
            info.Publisher = publisher;
            workingTitle = titleAfterPublisher;
            confidence += 5;
        }
        
        // Extract quality
        var (quality, titleAfterQuality) = ExtractQuality(workingTitle);
        if (!string.IsNullOrEmpty(quality))
        {
            info.Quality = quality;
            workingTitle = titleAfterQuality;
            confidence += 5;
        }
        
        // Extract release group (typically at end after hyphen)
        var (releaseGroup, titleAfterGroup) = ExtractReleaseGroup(workingTitle);
        if (!string.IsNullOrEmpty(releaseGroup))
        {
            info.ReleaseGroup = releaseGroup;
            workingTitle = titleAfterGroup;
            confidence += 5;
        }
        
        // What remains is the series title
        info.SeriesTitle = CleanSeriesTitle(workingTitle);
        if (!string.IsNullOrWhiteSpace(info.SeriesTitle))
        {
            confidence += 20;
        }
        
        info.Confidence = Math.Min(100, confidence);
        
        return info;
    }

    public string? ExtractFormat(string title)
    {
        var lower = title.ToLowerInvariant();
        
        if (lower.EndsWith(".cbz") || lower.Contains(".cbz"))
            return "cbz";
        if (lower.EndsWith(".cbr") || lower.Contains(".cbr"))
            return "cbr";
        if (lower.EndsWith(".pdf") || lower.Contains(".pdf"))
            return "pdf";
        if (lower.EndsWith(".cb7") || lower.Contains(".cb7"))
            return "cb7";
        if (lower.EndsWith(".epub") || lower.Contains(".epub"))
            return "epub";
            
        return null;
    }

    public string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;
            
        // Remove common noise
        var normalized = title
            .ToLowerInvariant()
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Replace("'", "")
            .Replace(":", " ")
            .Replace("!", "")
            .Replace("?", "")
            .Trim();
        
        // Remove multiple spaces
        normalized = MultipleSpacesRegex().Replace(normalized, " ");
        
        // Remove articles at start
        if (normalized.StartsWith("the "))
            normalized = normalized[4..];
        if (normalized.StartsWith("a "))
            normalized = normalized[2..];
        if (normalized.StartsWith("an "))
            normalized = normalized[3..];
            
        return normalized.Trim();
    }

    private static string RemoveExtension(string title)
    {
        return ExtensionRegex().Replace(title, "").Trim();
    }

    private static (int? year, string remainingTitle) ExtractYear(string title)
    {
        // Match year in parentheses: (2023) or (1999)
        var parenMatch = YearInParensRegex().Match(title);
        if (parenMatch.Success && int.TryParse(parenMatch.Groups[1].Value, out var parenYear))
        {
            if (parenYear >= 1900 && parenYear <= DateTime.Now.Year + 1)
            {
                var remaining = title.Replace(parenMatch.Value, "").Trim();
                return (parenYear, remaining);
            }
        }
        
        // Match year at end or standalone: 2023
        var endMatch = YearAtEndRegex().Match(title);
        if (endMatch.Success && int.TryParse(endMatch.Groups[1].Value, out var endYear))
        {
            if (endYear >= 1900 && endYear <= DateTime.Now.Year + 1)
            {
                var remaining = title[..endMatch.Index].Trim();
                return (endYear, remaining);
            }
        }
        
        return (null, title);
    }

    private static (bool isCollection, string? editionType, string remainingTitle) ExtractEditionType(string title)
    {
        var upperTitle = title.ToUpperInvariant();
        
        foreach (var collectionType in CollectionTypes)
        {
            var pattern = $@"\b{Regex.Escape(collectionType)}\b";
            var match = Regex.Match(upperTitle, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Get the actual matched text from the original title
                var actualMatch = title.Substring(match.Index, match.Length);
                var remaining = title.Remove(match.Index, match.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                
                return (true, NormalizeEditionType(collectionType), remaining);
            }
        }
        
        return (false, null, title);
    }

    private static string NormalizeEditionType(string type)
    {
        return type.ToUpperInvariant() switch
        {
            "TPB" or "TRADE PAPERBACK" or "TRADE" => "TPB",
            "HC" or "HARDCOVER" or "HARD COVER" => "HC",
            "OMNIBUS" or "OMNI" => "Omnibus",
            "DELUXE" or "DELUXE EDITION" => "Deluxe",
            "ABSOLUTE" or "ABSOLUTE EDITION" => "Absolute",
            "COMPENDIUM" => "Compendium",
            "COMPLETE COLLECTION" or "COMPLETE" => "Complete",
            "LIBRARY EDITION" => "Library",
            "ULTIMATE COLLECTION" => "Ultimate",
            "BOOK" => "Book",
            _ => type
        };
    }

    private static (int? volumeNumber, string remainingTitle) ExtractVolumeNumber(string title)
    {
        // Match Vol/Volume followed by number
        var volMatch = VolumeRegex().Match(title);
        if (volMatch.Success && int.TryParse(volMatch.Groups[1].Value, out var volNum))
        {
            var remaining = title.Replace(volMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (volNum, remaining);
        }
        
        // Match "v1" or "v01" format
        var vMatch = VShortRegex().Match(title);
        if (vMatch.Success && int.TryParse(vMatch.Groups[1].Value, out var vNum))
        {
            var remaining = title.Replace(vMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (vNum, remaining);
        }
        
        return (null, title);
    }

    private static (decimal? issueNumber, string remainingTitle) ExtractIssueNumber(string title)
    {
        // Match #001 or #1 or #1.5
        var hashMatch = IssueHashRegex().Match(title);
        if (hashMatch.Success && decimal.TryParse(hashMatch.Groups[1].Value, out var hashNum))
        {
            var remaining = title.Replace(hashMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (hashNum, remaining);
        }
        
        // Match "Issue 001" or "Issue 1"
        var issueMatch = IssueWordRegex().Match(title);
        if (issueMatch.Success && decimal.TryParse(issueMatch.Groups[1].Value, out var issueNum))
        {
            var remaining = title.Replace(issueMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (issueNum, remaining);
        }
        
        // Match standalone 3-digit number (common pattern): "Batman 001" or "Amazing Spider-Man 001"
        var threeDigitMatch = ThreeDigitNumberRegex().Match(title);
        if (threeDigitMatch.Success && decimal.TryParse(threeDigitMatch.Groups[1].Value, out var threeDigitNum))
        {
            if (threeDigitNum > 0 && threeDigitNum < 2000)
            {
                var remaining = title[..threeDigitMatch.Index].Trim();
                return (threeDigitNum, remaining);
            }
        }
        
        // Match standalone number at end (common pattern): "Batman 1"
        var endNumMatch = NumberAtEndRegex().Match(title);
        if (endNumMatch.Success && decimal.TryParse(endNumMatch.Groups[1].Value, out var endNum))
        {
            // Only accept if number is reasonable for issue count
            if (endNum > 0 && endNum < 2000)
            {
                var remaining = title[..endNumMatch.Index].Trim();
                return (endNum, remaining);
            }
        }
        
        return (null, title);
    }

    private static (string? issueRange, string remainingTitle) ExtractIssueRange(string title)
    {
        // Match issue range patterns: #1-6, Issues 1-12, etc.
        var rangeMatch = IssueRangeRegex().Match(title);
        if (rangeMatch.Success)
        {
            var range = $"{rangeMatch.Groups[1].Value}-{rangeMatch.Groups[2].Value}";
            var remaining = title.Replace(rangeMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (range, remaining);
        }
        
        return (null, title);
    }

    private static (string? publisher, string remainingTitle) ExtractPublisher(string title)
    {
        foreach (var publisher in KnownPublishers.OrderByDescending(p => p.Length))
        {
            var pattern = $@"\b{Regex.Escape(publisher)}\b";
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var remaining = title.Remove(match.Index, match.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (publisher, remaining);
            }
        }
        
        // Check for publisher in parentheses
        var parenMatch = PublisherInParensRegex().Match(title);
        if (parenMatch.Success)
        {
            var pubName = parenMatch.Groups[1].Value;
            if (KnownPublishers.Any(p => p.Equals(pubName, StringComparison.OrdinalIgnoreCase)))
            {
                var remaining = title.Replace(parenMatch.Value, "").Trim();
                return (pubName, remaining);
            }
        }
        
        return (null, title);
    }

    private static (string? quality, string remainingTitle) ExtractQuality(string title)
    {
        foreach (var quality in QualityTags)
        {
            var pattern = $@"\b{Regex.Escape(quality)}\b";
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var remaining = title.Remove(match.Index, match.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (quality, remaining);
            }
        }
        
        return (null, title);
    }

    private static (string? group, string remainingTitle) ExtractReleaseGroup(string title)
    {
        // Match release group in parentheses at end: (GroupName)
        var parenGroupMatch = ReleaseGroupParensRegex().Match(title);
        if (parenGroupMatch.Success)
        {
            var group = parenGroupMatch.Groups[1].Value.Trim();
            // Don't extract if it looks like a year, quality indicator, or publisher
            if (!IsYear(group) && !IsQuality(group) && !IsPublisher(group))
            {
                var remaining = title[..parenGroupMatch.Index].Trim();
                return (group, remaining);
            }
        }
        
        // Match release group after hyphen at end (but protect hyphenated names)
        // Only match if hyphen is preceded by a space
        var groupMatch = ReleaseGroupRegex().Match(title);
        if (groupMatch.Success)
        {
            var group = groupMatch.Groups[1].Value.Trim();
            // Don't extract short groups that might be part of the title
            if (group.Length > 3 && !group.Contains(' '))
            {
                var remaining = title[..groupMatch.Index].Trim();
                return (group, remaining);
            }
        }
        
        return (null, title);
    }

    private static bool IsYear(string text)
    {
        return int.TryParse(text, out var year) && year >= 1900 && year <= DateTime.Now.Year + 1;
    }

    private static bool IsQuality(string text)
    {
        return QualityTags.Any(q => q.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPublisher(string text)
    {
        return KnownPublishers.Any(p => p.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanSeriesTitle(string title)
    {
        // Remove common noise patterns
        var cleaned = title
            .Replace("()", "")
            .Replace("[]", "")
            .Replace("- -", "-")
            .Trim();
        
        // Remove trailing/leading punctuation
        cleaned = LeadingPunctuationRegex().Replace(cleaned, "");
        cleaned = TrailingPunctuationRegex().Replace(cleaned, "");
        
        // Normalize spaces
        cleaned = MultipleSpacesRegex().Replace(cleaned, " ");
        
        return cleaned.Trim();
    }

    private static List<string> Tokenize(string title)
    {
        // Split on common delimiters
        return TokenizeRegex()
            .Split(title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToList();
    }

    // Compiled regex patterns for performance
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();
    
    [GeneratedRegex(@"\.(cbz|cbr|pdf|cb7|epub)$", RegexOptions.IgnoreCase)]
    private static partial Regex ExtensionRegex();
    
    [GeneratedRegex(@"\((\d{4})\)")]
    private static partial Regex YearInParensRegex();
    
    [GeneratedRegex(@"\b(\d{4})\s*$")]
    private static partial Regex YearAtEndRegex();
    
    [GeneratedRegex(@"\bVol(?:ume)?\.?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeRegex();
    
    [GeneratedRegex(@"\bv(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VShortRegex();
    
    [GeneratedRegex(@"#(\d+(?:\.\d+)?)")]
    private static partial Regex IssueHashRegex();
    
    [GeneratedRegex(@"\bIssue\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex IssueWordRegex();
    
    [GeneratedRegex(@"\s+(\d{1,4}(?:\.\d+)?)\s*$")]
    private static partial Regex NumberAtEndRegex();
    
    [GeneratedRegex(@"\s+(\d{3,4})\s*(?:\(|$)")]
    private static partial Regex ThreeDigitNumberRegex();
    
    [GeneratedRegex(@"#?(\d+)\s*-\s*(\d+)")]
    private static partial Regex IssueRangeRegex();
    
    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex PublisherInParensRegex();
    
    [GeneratedRegex(@"\s-\s*([^-]+?)\s*$")]
    private static partial Regex ReleaseGroupRegex();
    
    [GeneratedRegex(@"\(([A-Za-z][\w-]+)\)\s*$")]
    private static partial Regex ReleaseGroupParensRegex();
    
    [GeneratedRegex(@"^[-_\.\s]+")]
    private static partial Regex LeadingPunctuationRegex();
    
    [GeneratedRegex(@"[-_\.\s]+$")]
    private static partial Regex TrailingPunctuationRegex();
    
    [GeneratedRegex(@"[\s\-_\.]+")]
    private static partial Regex TokenizeRegex();
}

