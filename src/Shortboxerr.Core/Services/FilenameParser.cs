using System.Text.RegularExpressions;
using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Parses comic book filenames to extract metadata.
/// Handles common naming conventions for singles and collections.
/// </summary>
public partial class FilenameParser : IFilenameParser
{
    // Collection indicators
    private static readonly string[] CollectionIndicators = 
    {
        "tpb", "trade paperback", "tp",
        "hc", "hardcover", "hard cover",
        "omnibus", "omni",
        "compendium",
        "absolute",
        "deluxe",
        "complete collection",
        "complete edition",
        "book one", "book two", "book three", "book 1", "book 2", "book 3",
        "vol.", "volume"
    };

    // Common publishers
    private static readonly string[] Publishers =
    {
        "Marvel", "DC", "DC Comics", "Image", "Dark Horse", "IDW", 
        "Boom", "Boom Studios", "Dynamite", "Valiant", "Vertigo",
        "Icon", "Wildstorm", "Top Cow", "Aftershock", "Oni Press"
    };

    public (ParsedComicInfo Info, int Confidence, bool IsCollection) Parse(string filename)
    {
        var info = new ParsedComicInfo();
        var confidence = 0;
        
        // Remove extension
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
        var working = nameWithoutExt;
        
        // Check if it's a collection
        var isCollection = IsCollectionFilename(working);
        
        // Extract year (commonly in parentheses or at end)
        var yearMatch = YearPattern().Match(working);
        if (yearMatch.Success)
        {
            var yearGroup = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
            if (int.TryParse(yearGroup, out var year))
            {
                info.Year = year;
                working = working.Replace(yearMatch.Value, " ").Trim();
                confidence += 10;
            }
        }

        // Extract publisher
        foreach (var pub in Publishers)
        {
            if (working.Contains(pub, StringComparison.OrdinalIgnoreCase))
            {
                info.Publisher = pub;
                working = Regex.Replace(working, Regex.Escape(pub), " ", RegexOptions.IgnoreCase).Trim();
                confidence += 5;
                break;
            }
        }

        // Extract tags in brackets/parentheses
        var tagMatches = TagPattern().Matches(working);
        foreach (Match tag in tagMatches)
        {
            var tagValue = tag.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tagValue) && !int.TryParse(tagValue, out _))
            {
                info.Tags.Add(tagValue);
            }
            working = working.Replace(tag.Value, " ");
        }

        if (isCollection)
        {
            // Parse as collection
            ParseCollection(working, info, ref confidence);
        }
        else
        {
            // Parse as single issue
            ParseSingleIssue(working, info, ref confidence);
        }

        // Clean up series title
        if (!string.IsNullOrEmpty(info.SeriesTitle))
        {
            info.SeriesTitle = CleanTitle(info.SeriesTitle);
            confidence += 20;
        }

        // Normalize confidence to 0-100
        confidence = Math.Min(100, Math.Max(0, confidence));

        return (info, confidence, isCollection);
    }

    private static bool IsCollectionFilename(string filename)
    {
        var lower = filename.ToLowerInvariant();
        
        // Check if it's DC's Absolute series line (not a collection)
        var isAbsoluteSeriesLine = Regex.IsMatch(lower, @"^absolute\s+(batman|wonder\s*woman|superman|flash|green\s*lantern|martian\s*manhunter|aquaman|cyborg|power\s*girl)");
        if (isAbsoluteSeriesLine)
            return false;
        
        return CollectionIndicators.Any(ind => lower.Contains(ind));
    }

    private void ParseCollection(string working, ParsedComicInfo info, ref int confidence)
    {
        // Try to extract volume number
        var volMatch = VolumePattern().Match(working);
        if (volMatch.Success)
        {
            info.VolumeNumber = int.Parse(volMatch.Groups[1].Value);
            info.EditionIndicator = "Vol.";
            working = working.Replace(volMatch.Value, " ");
            confidence += 15;
        }

        // Try to extract issue range
        var rangeMatch = IssueRangePattern().Match(working);
        if (rangeMatch.Success)
        {
            info.IssueRange = rangeMatch.Groups[1].Value;
            working = working.Replace(rangeMatch.Value, " ");
            confidence += 10;
        }

        // Detect edition type
        var lower = working.ToLowerInvariant();
        
        // Check if "Absolute" is part of DC's Absolute line (series name, not edition)
        // DC's Absolute line: Absolute Batman, Absolute Wonder Woman, Absolute Superman, etc.
        var isAbsoluteSeriesLine = Regex.IsMatch(lower, @"^absolute\s+(batman|wonder\s*woman|superman|flash|green\s*lantern|martian\s*manhunter|aquaman|cyborg|power\s*girl)");
        
        if (lower.Contains("omnibus") || lower.Contains("omni"))
            info.EditionIndicator = "Omnibus";
        else if (lower.Contains("hardcover") || lower.Contains(" hc"))
            info.EditionIndicator = "Hardcover";
        else if (lower.Contains("tpb") || lower.Contains("trade"))
            info.EditionIndicator = "TPB";
        else if (lower.Contains("compendium"))
            info.EditionIndicator = "Compendium";
        else if (lower.Contains("absolute") && !isAbsoluteSeriesLine)
            info.EditionIndicator = "Absolute";
        else if (lower.Contains("deluxe"))
            info.EditionIndicator = "Deluxe";

        if (info.EditionIndicator != null)
            confidence += 10;

        // What remains is likely the series title
        // Remove edition indicators (but not "absolute" if it's part of DC's Absolute series line)
        foreach (var ind in CollectionIndicators)
        {
            // Skip "absolute" if it's part of the DC Absolute series line
            if (ind == "absolute" && isAbsoluteSeriesLine)
                continue;
                
            working = Regex.Replace(working, $@"\b{Regex.Escape(ind)}\b", " ", RegexOptions.IgnoreCase);
        }

        info.SeriesTitle = working.Trim();
    }

    private void ParseSingleIssue(string working, ParsedComicInfo info, ref int confidence)
    {
        // Common patterns: "Series Name #123", "Series Name 123", "Series Name - 123"
        
        // Handle "Issue #X" pattern (common from ReadComicOnline)
        // e.g., "Absolute Martian Manhunter Issue #9" -> series="Absolute Martian Manhunter", issue=9
        var issueWordMatch = IssueWordPattern().Match(working);
        if (issueWordMatch.Success)
        {
            info.IssueNumber = decimal.Parse(issueWordMatch.Groups[1].Value);
            info.SeriesTitle = working[..issueWordMatch.Index].Trim();
            confidence += 25;
            return;
        }
        
        // Try hash pattern: "Title #123" or "Title #123.1"
        var hashMatch = IssueHashPattern().Match(working);
        if (hashMatch.Success)
        {
            info.IssueNumber = decimal.Parse(hashMatch.Groups[1].Value);
            info.SeriesTitle = working[..hashMatch.Index].Trim();
            confidence += 25;
            return;
        }

        // Try "v01" or "v1" volume pattern followed by issue
        var volIssueMatch = VolumeIssuePattern().Match(working);
        if (volIssueMatch.Success)
        {
            info.VolumeNumber = int.Parse(volIssueMatch.Groups[1].Value);
            if (volIssueMatch.Groups[2].Success)
            {
                info.IssueNumber = decimal.Parse(volIssueMatch.Groups[2].Value);
            }
            info.SeriesTitle = working[..volIssueMatch.Index].Trim();
            confidence += 20;
            return;
        }

        // Try trailing number pattern: "Title 001" or "Title - 001"
        var trailingMatch = TrailingIssuePattern().Match(working);
        if (trailingMatch.Success)
        {
            info.IssueNumber = decimal.Parse(trailingMatch.Groups[1].Value);
            info.SeriesTitle = working[..trailingMatch.Index].Trim();
            confidence += 15;
            return;
        }

        // No issue number found - just use as title
        info.SeriesTitle = working.Trim();
    }

    private static string CleanTitle(string title)
    {
        // Remove common separators and extra whitespace
        title = title.Replace(" - ", " ").Replace("_", " ");
        title = Regex.Replace(title, @"\s+", " ");
        title = title.Trim(' ', '-', '_', '.');
        return title;
    }

    // Regex patterns
    [GeneratedRegex(@"\((\d{4})\)|\b(19|20)\d{2}\b")]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"[\[\(]([^\]\)]+)[\]\)]")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(?:vol(?:ume)?\.?\s*|v)(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumePattern();

    [GeneratedRegex(@"#\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex IssueHashPattern();

    [GeneratedRegex(@"\bIssue\s*#?\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex IssueWordPattern();

    [GeneratedRegex(@"v(\d+)\s*(?:#?\s*(\d+(?:\.\d+)?))?", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeIssuePattern();

    [GeneratedRegex(@"[-\s](\d{1,4}(?:\.\d+)?)\s*$")]
    private static partial Regex TrailingIssuePattern();

    [GeneratedRegex(@"#?\s*(\d+)\s*-\s*(\d+)")]
    private static partial Regex IssueRangePattern();
}

