using System.Text.RegularExpressions;

namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Parses NZB/Usenet release names to extract metadata.
/// Handles scene naming conventions common in Usenet releases.
/// </summary>
public partial class NzbReleaseParser : INzbReleaseParser
{
    // Collection indicators
    private static readonly string[] CollectionIndicators = 
    {
        "tpb", "trade.paperback", "tp",
        "hc", "hardcover", "hard.cover",
        "omnibus", "omni",
        "compendium",
        "absolute",
        "deluxe",
        "complete.collection",
        "complete.edition",
        "book.one", "book.two", "book.three", "book.1", "book.2", "book.3",
        "vol", "volume"
    };
    
    // Quality indicators (Digital is preferred)
    private static readonly Dictionary<string, int> QualityScores = new(StringComparer.OrdinalIgnoreCase)
    {
        { "digital", 100 },
        { "digital-hd", 100 },
        { "webrip", 90 },
        { "web", 85 },
        { "scan", 70 },
        { "hq.scan", 75 },
        { "lq.scan", 50 }
    };
    
    // Format indicators
    private static readonly string[] Formats = { "cbz", "cbr", "pdf", "epub", "azw3", "mobi" };
    
    // Common publishers
    private static readonly string[] Publishers =
    {
        "Marvel", "DC", "DC.Comics", "Image", "Dark.Horse", "IDW", 
        "Boom", "Boom.Studios", "Dynamite", "Valiant", "Vertigo",
        "Icon", "Wildstorm", "Top.Cow", "Aftershock", "Oni.Press",
        "Zenescope", "Titan", "Avatar", "Archie", "Fantagraphics"
    };
    
    /// <summary>
    /// Parses an NZB release title and extracts structured metadata.
    /// </summary>
    /// <param name="releaseTitle">The release title from the NZB indexer</param>
    /// <returns>Parsed information with confidence score</returns>
    public NzbParsedInfo Parse(string releaseTitle)
    {
        var info = new NzbParsedInfo();
        var confidence = 0;
        
        if (string.IsNullOrWhiteSpace(releaseTitle))
        {
            return info;
        }
        
        // Normalize the title (scene releases use dots as separators)
        var working = releaseTitle.Trim();
        var tokens = new List<string>();
        
        // Extract release group (typically at the end after a dash)
        var groupMatch = ReleaseGroupPattern().Match(working);
        if (groupMatch.Success)
        {
            info.ReleaseGroup = groupMatch.Groups[1].Value;
            working = working[..groupMatch.Index].TrimEnd('.', '-', ' ');
            confidence += 5;
        }
        
        // Check for release modifiers
        if (RepackPattern().IsMatch(working))
        {
            info.IsRepack = true;
            working = RepackPattern().Replace(working, "").Trim('.', '-', ' ');
            info.Tags.Add("REPACK");
        }
        
        if (ProperPattern().IsMatch(working))
        {
            info.IsProper = true;
            working = ProperPattern().Replace(working, "").Trim('.', '-', ' ');
            info.Tags.Add("PROPER");
        }
        
        if (InternalPattern().IsMatch(working))
        {
            info.IsInternal = true;
            working = InternalPattern().Replace(working, "").Trim('.', '-', ' ');
            info.Tags.Add("INTERNAL");
        }
        
        // Extract format
        foreach (var format in Formats)
        {
            var formatPattern = new Regex($@"[.\-_\s]{format}(?:[.\-_\s]|$)", RegexOptions.IgnoreCase);
            if (formatPattern.IsMatch(working))
            {
                info.Format = format.ToUpperInvariant();
                working = formatPattern.Replace(working, ".");
                confidence += 10;
                break;
            }
        }
        
        // Extract quality
        foreach (var quality in QualityScores.Keys)
        {
            var qualityPattern = new Regex($@"[.\-_\s]{Regex.Escape(quality)}(?:[.\-_\s]|$)", RegexOptions.IgnoreCase);
            if (qualityPattern.IsMatch(working))
            {
                info.Quality = quality;
                working = qualityPattern.Replace(working, ".");
                confidence += 5;
                break;
            }
        }
        
        // Check if it's a collection
        var isCollection = IsCollectionTitle(working);
        info.IsCollection = isCollection;
        
        // Extract year (commonly in parentheses or after series)
        var yearMatch = YearPattern().Match(working);
        if (yearMatch.Success)
        {
            var yearValue = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
            if (int.TryParse(yearValue, out var year) && year >= 1930 && year <= DateTime.Now.Year + 2)
            {
                info.Year = year;
                working = working.Replace(yearMatch.Value, ".").Trim('.', '-', ' ');
                confidence += 10;
            }
        }
        
        // Extract publisher
        foreach (var pub in Publishers)
        {
            var pubPattern = new Regex($@"(?:^|[.\-_\s]){Regex.Escape(pub)}(?:[.\-_\s]|$)", RegexOptions.IgnoreCase);
            if (pubPattern.IsMatch(working))
            {
                info.Publisher = pub.Replace(".", " ");
                working = pubPattern.Replace(working, ".");
                confidence += 5;
                break;
            }
        }
        
        // Extract volume number
        var volMatch = VolumePattern().Match(working);
        if (volMatch.Success)
        {
            if (int.TryParse(volMatch.Groups[1].Value, out var vol))
            {
                info.VolumeNumber = vol;
                working = working.Remove(volMatch.Index, volMatch.Length).Insert(volMatch.Index, ".");
                confidence += 15;
            }
        }
        
        // Extract issue number (various patterns)
        if (!isCollection)
        {
            var issueMatch = IssuePattern().Match(working);
            if (issueMatch.Success)
            {
                if (decimal.TryParse(issueMatch.Groups[1].Value, out var issueNum))
                {
                    info.IssueNumber = issueNum;
                    working = working.Remove(issueMatch.Index, issueMatch.Length).Insert(issueMatch.Index, ".");
                    confidence += 25;
                }
            }
            else
            {
                // Try trailing number pattern
                var trailingMatch = TrailingNumberPattern().Match(working);
                if (trailingMatch.Success)
                {
                    if (decimal.TryParse(trailingMatch.Groups[1].Value, out var issueNum))
                    {
                        info.IssueNumber = issueNum;
                        working = working[..trailingMatch.Index].TrimEnd('.', '-', ' ');
                        confidence += 15;
                    }
                }
            }
        }
        else
        {
            // For collections, try to extract issue range
            var rangeMatch = IssueRangePattern().Match(working);
            if (rangeMatch.Success)
            {
                info.IssueRange = $"{rangeMatch.Groups[1].Value}-{rangeMatch.Groups[2].Value}";
                working = working.Remove(rangeMatch.Index, rangeMatch.Length).Insert(rangeMatch.Index, ".");
                confidence += 10;
            }
            
            // Detect edition type
            info.EditionType = DetectEditionType(releaseTitle);
            if (info.EditionType != null)
            {
                confidence += 10;
            }
        }
        
        // Clean up and extract series title
        working = CleanupTitle(working);
        
        if (!string.IsNullOrWhiteSpace(working))
        {
            info.SeriesTitle = working;
            confidence += 20;
        }
        
        // Tokenize original title for advanced matching
        info.Tokens.AddRange(releaseTitle.Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        
        // Cap confidence at 100
        info.Confidence = Math.Min(100, Math.Max(0, confidence));
        
        return info;
    }
    
    /// <summary>
    /// Parses a NewznabRelease and creates an NzbCandidate.
    /// </summary>
    public NzbCandidate ParseRelease(NewznabRelease release, int indexerPriority = 50)
    {
        var parsedInfo = Parse(release.Title);
        return NzbCandidate.FromNewznabRelease(release, parsedInfo, indexerPriority);
    }
    
    /// <summary>
    /// Calculates a quality score for the release based on parsed info.
    /// </summary>
    public int CalculateQualityScore(NzbParsedInfo info)
    {
        var score = 0;
        
        // Quality indicators
        if (!string.IsNullOrEmpty(info.Quality) && QualityScores.TryGetValue(info.Quality, out var qualityScore))
        {
            score += qualityScore;
        }
        else
        {
            score += 50; // Unknown quality gets mid score
        }
        
        // Format preference (CBZ > CBR > PDF)
        score += info.Format?.ToUpperInvariant() switch
        {
            "CBZ" => 20,
            "CBR" => 15,
            "PDF" => 10,
            "EPUB" => 5,
            _ => 0
        };
        
        // PROPER releases get a bonus
        if (info.IsProper)
        {
            score += 10;
        }
        
        // REPACK might fix issues
        if (info.IsRepack)
        {
            score += 5;
        }
        
        // Higher confidence = better quality match
        score += info.Confidence / 10;
        
        return score;
    }
    
    private static bool IsCollectionTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        return CollectionIndicators.Any(ind => lower.Contains(ind));
    }
    
    private static string? DetectEditionType(string title)
    {
        var lower = title.ToLowerInvariant();
        
        if (lower.Contains("omnibus") || lower.Contains("omni"))
            return "Omnibus";
        if (lower.Contains("hardcover") || Regex.IsMatch(lower, @"[\.\-_\s]hc[\.\-_\s]"))
            return "Hardcover";
        if (lower.Contains("tpb") || lower.Contains("trade"))
            return "TPB";
        if (lower.Contains("compendium"))
            return "Compendium";
        if (lower.Contains("absolute"))
            return "Absolute";
        if (lower.Contains("deluxe"))
            return "Deluxe";
        if (lower.Contains("complete"))
            return "Complete";
            
        return null;
    }
    
    private static string CleanupTitle(string title)
    {
        // Replace dots and underscores with spaces
        var cleaned = title.Replace('.', ' ').Replace('_', ' ');
        
        // Remove collection indicators
        foreach (var ind in CollectionIndicators)
        {
            cleaned = Regex.Replace(cleaned, $@"\b{Regex.Escape(ind.Replace(".", " "))}\b", " ", RegexOptions.IgnoreCase);
        }
        
        // Remove multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        
        // Trim and clean edges
        cleaned = cleaned.Trim(' ', '-', '_', '.');
        
        return cleaned;
    }
    
    // Regex patterns using source generators for performance
    
    [GeneratedRegex(@"-([A-Za-z0-9]+)$")]
    private static partial Regex ReleaseGroupPattern();
    
    [GeneratedRegex(@"[\.\-_\s]REPACK[\.\-_\s]?", RegexOptions.IgnoreCase)]
    private static partial Regex RepackPattern();
    
    [GeneratedRegex(@"[\.\-_\s]PROPER[\.\-_\s]?", RegexOptions.IgnoreCase)]
    private static partial Regex ProperPattern();
    
    [GeneratedRegex(@"[\.\-_\s]INTERNAL[\.\-_\s]?", RegexOptions.IgnoreCase)]
    private static partial Regex InternalPattern();
    
    [GeneratedRegex(@"\((\d{4})\)|[\.\-_\s](19|20\d{2})[\.\-_\s]")]
    private static partial Regex YearPattern();
    
    [GeneratedRegex(@"(?:vol(?:ume)?\.?\s*|v)(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumePattern();
    
    [GeneratedRegex(@"#(\d+(?:\.\d+)?)|[\.\-_\s](\d{1,4}(?:\.\d+)?)[\.\-_\s](?:of[\.\-_\s]\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex IssuePattern();
    
    [GeneratedRegex(@"[\.\-_\s](\d{1,4}(?:\.\d+)?)\s*$")]
    private static partial Regex TrailingNumberPattern();
    
    [GeneratedRegex(@"[\.\-_\s](\d+)[\.\-_\s]?-[\.\-_\s]?(\d+)[\.\-_\s]")]
    private static partial Regex IssueRangePattern();
}
