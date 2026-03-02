using System.Text.RegularExpressions;

namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Parses DDL release titles into structured information.
/// Implements Mylar3-compatible parsing rules for comic book releases.
/// </summary>
public partial class DdlReleaseParser : IDdlReleaseParser
{
    // Common edition type indicators
    // NOTE: "Absolute" alone is NOT included because it's commonly a series name prefix
    // (e.g., "Absolute Batman", "Absolute Martian Manhunter"). Only "Absolute Edition" 
    // at the END of a title indicates a collection.
    private static readonly string[] CollectionTypes = new[]
    {
        "TPB", "Trade Paperback", "Trade",
        "HC", "Hardcover", "Hard Cover",
        "Omnibus", "Omni",
        "Deluxe", "Deluxe Edition",
        "Absolute Edition", // Only "Absolute Edition", not standalone "Absolute"
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
        "Webrip", "Web-Rip", "Web Rip",
        "Scan", "Scanned", "c2c-Scan", "c2c",
        "HD", "HQ",
        "Proper", "Repack"
    };
    
    // Pack indicators (Mylar3's pack_receipts)
    private static readonly string[] PackIndicators = new[]
    {
        "+ TPBs", "+TPBs", "+ TPB", "+TPB",
        "+ Deluxe Books", "+ Deluxe",
        "+ Annuals", "+Annuals", "+ Annual",
        " & ", " and ", "+ ",
        "Weekly Pack", "Week Pack"
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
        "Zenescope",
        "Cartoon Books"
    };
    
    // Reboot/Revival indicators - used to disambiguate series runs
    private static readonly string[] RebootIndicators = new[]
    {
        "New 52", "New52",
        "Rebirth",
        "Dawn of X", "DawnOfX",
        "Infinite Frontier",
        "All-New", "AllNew",
        "All New",
        "Marvel NOW", "Marvel Now",
        "Fresh Start",
        "Heroes Reborn",
        "Ultimate Universe",
        "Black Label",
        "MAX", "Max Comics",
        "Knights",
        "Legacy"
    };
    
    // Series version indicators
    private static readonly string[] SeriesVersionIndicators = new[]
    {
        "Second Series", "2nd Series",
        "Third Series", "3rd Series",
        "Fourth Series", "4th Series",
        "Fifth Series", "5th Series",
        "Second Volume", "2nd Volume",
        "Third Volume", "3rd Volume",
        "Volume Two", "Volume 2",
        "Volume Three", "Volume 3"
    };
    
    // Release groups and their associated publishers (for publisher hints)
    private static readonly Dictionary<string, string> ReleaseGroupPublishers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DC-Empire", "DC Comics" },
        { "Empire-DC", "DC Comics" },
        { "DC-Minutemen", "DC Comics" },
        { "Minutemen-DC", "DC Comics" },
        { "Marvel-Empire", "Marvel" },
        { "Empire-Marvel", "Marvel" },
        { "Marvel-Minutemen", "Marvel" },
        { "Minutemen-Marvel", "Marvel" },
        { "Image-Empire", "Image Comics" },
        { "Empire-Image", "Image Comics" },
        { "DarkHorse", "Dark Horse Comics" },
        { "Dark-Horse", "Dark Horse Comics" }
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
        
        // Pre-process: normalize separators (underscores, periods) to spaces
        // This enables parsing of titles like "Wonder_Woman_001_(DC)_(2023)" and "Aquaman.001.2023.Digital"
        workingTitle = NormalizeSeparators(workingTitle);
        
        // Tokenize
        info.Tokens.AddRange(Tokenize(workingTitle));
        
        // Extract all parenthetical groups for later processing
        var parenGroups = ExtractAllParenGroups(workingTitle);
        
        // Extract year (commonly in parentheses or at end)
        var (year, titleAfterYear) = ExtractYear(workingTitle);
        if (year.HasValue)
        {
            info.Year = year;
            workingTitle = titleAfterYear;
            confidence += 10;
        }
        
        // Extract publisher from parenthetical groups (before other processing)
        // This handles cases like "Wolverine 0001 (Marvel) (2024).cbz" where publisher precedes year
        var (publisherFromParens, titleAfterPubParens) = ExtractPublisherFromParenGroups(workingTitle, parenGroups);
        if (!string.IsNullOrEmpty(publisherFromParens))
        {
            info.Publisher = publisherFromParens;
            workingTitle = titleAfterPubParens;
            confidence += 5;
        }
        
        // Extract quality from parenthetical groups
        // This handles cases like "Action Comics 1050 (2023) (Webrip).cbz"
        var (qualityFromParens, titleAfterQualParens) = ExtractQualityFromParenGroups(workingTitle, parenGroups);
        if (!string.IsNullOrEmpty(qualityFromParens))
        {
            info.Quality = qualityFromParens;
            workingTitle = titleAfterQualParens;
            confidence += 5;
        }
        
        // Extract quality (early, if not found in parens) for scene-style naming like "Aquaman 001 2023 Digital"
        // This must happen before issue extraction so "001" isn't hidden by trailing "Digital"
        if (string.IsNullOrEmpty(info.Quality))
        {
            var (qualityEarly, titleAfterQualEarly) = ExtractQuality(workingTitle);
            if (!string.IsNullOrEmpty(qualityEarly))
            {
                info.Quality = qualityEarly;
                workingTitle = titleAfterQualEarly;
                confidence += 5;
            }
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
        
        // Extract release group FIRST (at end after hyphen) - must be before publisher
        // extraction to prevent matching publishers embedded in release groups like "DC-Empire"
        var (releaseGroup, titleAfterGroup) = ExtractReleaseGroup(workingTitle);
        if (!string.IsNullOrEmpty(releaseGroup))
        {
            info.ReleaseGroup = releaseGroup;
            workingTitle = titleAfterGroup;
            confidence += 5;
            
            // Extract publisher hint from release group naming
            var publisherHint = ExtractPublisherHintFromGroup(releaseGroup);
            if (!string.IsNullOrEmpty(publisherHint))
            {
                info.PublisherHint = publisherHint;
                // If we don't have a publisher yet, use the hint
                if (string.IsNullOrEmpty(info.Publisher))
                {
                    info.Publisher = publisherHint;
                }
                confidence += 3;
            }
        }
        
        // Extract publisher (if not already found from parens or release group hint)
        if (string.IsNullOrEmpty(info.Publisher))
        {
            var (publisher, titleAfterPublisher) = ExtractPublisher(workingTitle);
            if (!string.IsNullOrEmpty(publisher))
            {
                info.Publisher = publisher;
                workingTitle = titleAfterPublisher;
                confidence += 5;
            }
        }
        
        // Note: Quality extraction moved earlier in the pipeline (before issue extraction)
        // for proper handling of scene-style naming
        
        // Detect pack (multiple issues in one download) - Mylar3's pack_check
        var (isPack, packIndicator, includesAnnuals, titleAfterPack) = DetectPack(workingTitle);
        if (isPack)
        {
            info.IsPack = true;
            info.PackIndicator = packIndicator;
            info.IncludesAnnuals = includesAnnuals;
            workingTitle = titleAfterPack;
            confidence += 5;
        }
        
        // Extract reboot/revival indicators (New 52, Rebirth, etc.)
        var (rebootIndicator, titleAfterReboot) = ExtractRebootIndicator(workingTitle);
        if (!string.IsNullOrEmpty(rebootIndicator))
        {
            info.RebootIndicator = rebootIndicator;
            workingTitle = titleAfterReboot;
            confidence += 5;
        }
        
        // Extract series version indicators (Second Series, Third Volume, etc.)
        var (seriesVersion, titleAfterVersion) = ExtractSeriesVersion(workingTitle);
        if (!string.IsNullOrEmpty(seriesVersion))
        {
            info.SeriesVersion = seriesVersion;
            workingTitle = titleAfterVersion;
            confidence += 5;
        }
        
        // Detect disambiguation year from title (year in parens that's part of series name)
        // E.g., "Batman (2016)" - the year is for disambiguation, not publication
        info.DisambiguationYear = DetectDisambiguationYear(info.Year, info.SeriesTitle ?? workingTitle);
        
        // Handle hyphen-separated subtitles: "Star Wars - Darth Vader" -> preserve full title
        workingTitle = HandleHyphenSubtitle(workingTitle);
        
        // What remains is the series title
        info.SeriesTitle = CleanSeriesTitle(workingTitle);
        if (!string.IsNullOrWhiteSpace(info.SeriesTitle))
        {
            confidence += 20;
        }
        
        info.Confidence = Math.Min(100, confidence);
        
        return info;
    }

    /// <summary>
    /// Normalize separators (underscores, periods) to spaces.
    /// Preserves periods in file extensions and decimal issue numbers.
    /// </summary>
    private static string NormalizeSeparators(string title)
    {
        // Replace underscores with spaces
        var normalized = title.Replace('_', ' ');
        
        // Replace periods with spaces for scene-style naming like "Aquaman.001.2023.Digital"
        // but preserve periods in:
        // 1. Decimal issue numbers like "1.5" or "#1.5" (small decimal, not year-like)
        // 2. "Vol." patterns
        
        // First, protect "Vol." patterns
        normalized = Regex.Replace(normalized, @"\bVol\.", "Vol<DOT>", RegexOptions.IgnoreCase);
        
        // Protect decimal issue numbers - only small decimals like "1.5" not "001.2023"
        // Pattern: digit(s).single_digit where total < 4 digits before decimal
        // This protects 1.5, 01.5, 001.5 but NOT 001.2023 or 2023.01
        normalized = Regex.Replace(normalized, @"(\d{1,3})\.(\d)(?!\d{2})", "$1<DECIMAL>$2");
        
        // Now replace all remaining periods with spaces
        normalized = normalized.Replace('.', ' ');
        
        // Restore protected patterns
        normalized = normalized.Replace("Vol<DOT>", "Vol.");
        normalized = normalized.Replace("<DECIMAL>", ".");
        
        // Clean up multiple spaces
        normalized = MultipleSpacesRegex().Replace(normalized, " ");
        
        return normalized.Trim();
    }

    /// <summary>
    /// Handle hyphen-separated subtitles like "Star Wars - Darth Vader".
    /// Preserves the full title including subtitle.
    /// </summary>
    private static string HandleHyphenSubtitle(string title)
    {
        // If there's a " - " pattern followed by text, it's likely a subtitle
        // We want to keep this as part of the series title
        // Just clean up extra spaces around hyphens
        var cleaned = Regex.Replace(title, @"\s+-\s+", " - ");
        return cleaned;
    }

    /// <summary>
    /// Extract all parenthetical groups from the title for processing.
    /// </summary>
    private static List<string> ExtractAllParenGroups(string title)
    {
        var groups = new List<string>();
        var matches = AllParenGroupsRegex().Matches(title);
        foreach (Match match in matches)
        {
            groups.Add(match.Groups[1].Value);
        }
        return groups;
    }

    /// <summary>
    /// Extract publisher from parenthetical groups.
    /// Handles cases like "Wolverine 0001 (Marvel) (2024).cbz"
    /// </summary>
    private static (string? publisher, string remainingTitle) ExtractPublisherFromParenGroups(string title, List<string> parenGroups)
    {
        foreach (var group in parenGroups)
        {
            // Check if this group is a known publisher
            var matchedPublisher = KnownPublishers
                .FirstOrDefault(p => p.Equals(group, StringComparison.OrdinalIgnoreCase));
            
            if (matchedPublisher != null)
            {
                // Remove this parenthetical group from the title
                var pattern = $@"\({Regex.Escape(group)}\)";
                var remaining = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (matchedPublisher, remaining);
            }
        }
        
        return (null, title);
    }

    /// <summary>
    /// Extract quality from parenthetical groups.
    /// Handles cases like "Action Comics 1050 (2023) (Webrip).cbz"
    /// </summary>
    private static (string? quality, string remainingTitle) ExtractQualityFromParenGroups(string title, List<string> parenGroups)
    {
        foreach (var group in parenGroups)
        {
            // Check if this group is a known quality tag
            var matchedQuality = QualityTags
                .FirstOrDefault(q => q.Equals(group, StringComparison.OrdinalIgnoreCase));
            
            if (matchedQuality != null)
            {
                // Remove this parenthetical group from the title
                var pattern = $@"\({Regex.Escape(group)}\)";
                var remaining = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (matchedQuality, remaining);
            }
            
            // Also check for quality tags within compound groups like "Digital-Empire"
            foreach (var qualityTag in QualityTags)
            {
                if (group.StartsWith(qualityTag, StringComparison.OrdinalIgnoreCase) ||
                    group.EndsWith(qualityTag, StringComparison.OrdinalIgnoreCase) ||
                    group.Contains($"-{qualityTag}", StringComparison.OrdinalIgnoreCase) ||
                    group.Contains($"{qualityTag}-", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't remove the group, just extract the quality
                    return (qualityTag, title);
                }
            }
        }
        
        return (null, title);
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
        // Match year in parentheses: (2023) or (1999) - most common format
        var parenMatch = YearInParensRegex().Match(title);
        if (parenMatch.Success && int.TryParse(parenMatch.Groups[1].Value, out var parenYear))
        {
            if (parenYear >= 1900 && parenYear <= DateTime.Now.Year + 1)
            {
                var remaining = title.Replace(parenMatch.Value, "").Trim();
                return (parenYear, remaining);
            }
        }
        
        // Match year in brackets: [2023] - alternative format
        var bracketMatch = YearInBracketsRegex().Match(title);
        if (bracketMatch.Success && int.TryParse(bracketMatch.Groups[1].Value, out var bracketYear))
        {
            if (bracketYear >= 1900 && bracketYear <= DateTime.Now.Year + 1)
            {
                var remaining = title.Replace(bracketMatch.Value, "").Trim();
                return (bracketYear, remaining);
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
        
        // Match standalone year anywhere in title (for scene-style naming like "Aquaman 001 2023 Digital")
        var anywhereMatch = YearAnywhereRegex().Match(title);
        if (anywhereMatch.Success && int.TryParse(anywhereMatch.Groups[1].Value, out var anyYear))
        {
            if (anyYear >= 1900 && anyYear <= DateTime.Now.Year + 1)
            {
                var remaining = title.Remove(anywhereMatch.Index, anywhereMatch.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (anyYear, remaining);
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
            "ABSOLUTE EDITION" => "Absolute", // Only "Absolute Edition", not standalone "Absolute"
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
        
        // Match "v1" or "v01" format (standalone)
        var vMatch = VShortRegex().Match(title);
        if (vMatch.Success && int.TryParse(vMatch.Groups[1].Value, out var vNum))
        {
            var remaining = title.Replace(vMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (vNum, remaining);
        }
        
        // Match "(v1)" or "(v2)" in parentheses
        var vParenMatch = VolumeInParensRegex().Match(title);
        if (vParenMatch.Success && int.TryParse(vParenMatch.Groups[1].Value, out var vParenNum))
        {
            var remaining = title.Replace(vParenMatch.Value, "").Trim();
            remaining = MultipleSpacesRegex().Replace(remaining, " ");
            return (vParenNum, remaining);
        }
        
        // Match "Vol. One", "Vol. Two", etc.
        var volWordMatch = VolumeWordRegex().Match(title);
        if (volWordMatch.Success)
        {
            var wordNum = ParseOrdinalWord(volWordMatch.Groups[1].Value);
            if (wordNum.HasValue)
            {
                var remaining = title.Replace(volWordMatch.Value, "").Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (wordNum, remaining);
            }
        }
        
        return (null, title);
    }
    
    /// <summary>
    /// Parse ordinal words like "One", "Two", "First", "Second" to numbers.
    /// </summary>
    private static int? ParseOrdinalWord(string word)
    {
        return word.ToLowerInvariant() switch
        {
            "one" or "first" or "1st" => 1,
            "two" or "second" or "2nd" => 2,
            "three" or "third" or "3rd" => 3,
            "four" or "fourth" or "4th" => 4,
            "five" or "fifth" or "5th" => 5,
            "six" or "sixth" or "6th" => 6,
            "seven" or "seventh" or "7th" => 7,
            "eight" or "eighth" or "8th" => 8,
            "nine" or "ninth" or "9th" => 9,
            "ten" or "tenth" or "10th" => 10,
            _ => null
        };
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
        
        // Match standalone number at end (common pattern): "Batman 1" or "Aquaman 001"
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
        
        // Match standalone 3-digit number before parentheses: "Batman 001 (2023)"
        var threeDigitMatch = ThreeDigitNumberRegex().Match(title);
        if (threeDigitMatch.Success && decimal.TryParse(threeDigitMatch.Groups[1].Value, out var threeDigitNum))
        {
            if (threeDigitNum > 0 && threeDigitNum < 2000)
            {
                var remaining = title[..threeDigitMatch.Index].Trim();
                return (threeDigitNum, remaining);
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
    
    /// <summary>
    /// Detect if title indicates a pack (multiple issues/volumes).
    /// Based on Mylar3's pack_receipts and check_for_pack logic.
    /// </summary>
    private static (bool isPack, string? indicator, bool includesAnnuals, string remainingTitle) DetectPack(string title)
    {
        foreach (var indicator in PackIndicators)
        {
            var index = title.IndexOf(indicator, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var includesAnnuals = indicator.Contains("Annual", StringComparison.OrdinalIgnoreCase);
                
                // Remove pack indicator from title
                var remaining = title.Remove(index, indicator.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                
                return (true, indicator.Trim(), includesAnnuals, remaining);
            }
        }
        
        // Check for issue range pattern which indicates pack: "1 - 12" or "#1-12"
        var rangeMatch = IssueRangeRegex().Match(title);
        if (rangeMatch.Success)
        {
            // Has issue range = is a pack
            return (true, null, false, title);
        }
        
        // Check for year range which can indicate pack: "2020-2024"
        var yearRangeMatch = YearRangeRegex().Match(title);
        if (yearRangeMatch.Success)
        {
            return (true, null, false, title);
        }
        
        return (false, null, false, title);
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
        // Check for quality in parentheses first: (Digital)
        foreach (var quality in QualityTags)
        {
            var parenPattern = $@"\({Regex.Escape(quality)}\)";
            var parenMatch = Regex.Match(title, parenPattern, RegexOptions.IgnoreCase);
            if (parenMatch.Success)
            {
                var remaining = title.Remove(parenMatch.Index, parenMatch.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (quality, remaining);
            }
        }
        
        // Then check standalone words
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
        // Match release group in parentheses at end: (GroupName) or (Group-Name)
        // Must contain hyphen or special character to be a group, not a simple word
        var parenGroupMatch = ReleaseGroupParensRegex().Match(title);
        if (parenGroupMatch.Success)
        {
            var group = parenGroupMatch.Groups[1].Value.Trim();
            // Don't extract if it looks like a year, quality indicator, or publisher
            if (!IsYear(group) && !IsQuality(group) && !IsPublisher(group))
            {
                // Prefer groups that have hyphen (scene naming style)
                if (group.Contains('-'))
                {
                    var remaining = title[..parenGroupMatch.Index].Trim();
                    return (group, remaining);
                }
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

    /// <summary>
    /// Extract publisher hint from release group naming conventions.
    /// E.g., "DC-Empire" suggests DC Comics publisher.
    /// </summary>
    private static string? ExtractPublisherHintFromGroup(string releaseGroup)
    {
        // Check exact matches first
        if (ReleaseGroupPublishers.TryGetValue(releaseGroup, out var exactMatch))
        {
            return exactMatch;
        }
        
        // Check if release group contains publisher prefix/suffix
        foreach (var (pattern, publisher) in ReleaseGroupPublishers)
        {
            if (releaseGroup.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return publisher;
            }
        }
        
        // Check for publisher name embedded in group
        foreach (var publisher in KnownPublishers)
        {
            if (releaseGroup.StartsWith(publisher + "-", StringComparison.OrdinalIgnoreCase) ||
                releaseGroup.EndsWith("-" + publisher, StringComparison.OrdinalIgnoreCase) ||
                releaseGroup.Contains("-" + publisher + "-", StringComparison.OrdinalIgnoreCase))
            {
                return publisher;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Extract reboot/revival indicators from title.
    /// E.g., "New 52", "Rebirth", "Dawn of X", etc.
    /// </summary>
    private static (string? indicator, string remainingTitle) ExtractRebootIndicator(string title)
    {
        // Check parenthetical groups first (most reliable)
        foreach (var indicator in RebootIndicators)
        {
            var parenPattern = $@"\({Regex.Escape(indicator)}\)";
            var parenMatch = Regex.Match(title, parenPattern, RegexOptions.IgnoreCase);
            if (parenMatch.Success)
            {
                var remaining = title.Remove(parenMatch.Index, parenMatch.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (indicator, remaining);
            }
        }
        
        // Check as standalone word boundary matches
        foreach (var indicator in RebootIndicators.OrderByDescending(i => i.Length))
        {
            var pattern = $@"\b{Regex.Escape(indicator)}\b";
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var remaining = title.Remove(match.Index, match.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (indicator, remaining);
            }
        }
        
        return (null, title);
    }

    /// <summary>
    /// Extract series version indicators from title.
    /// E.g., "Second Series", "Third Volume", "2nd Series", etc.
    /// </summary>
    private static (string? version, string remainingTitle) ExtractSeriesVersion(string title)
    {
        // Check parenthetical groups first
        foreach (var version in SeriesVersionIndicators)
        {
            var parenPattern = $@"\({Regex.Escape(version)}\)";
            var parenMatch = Regex.Match(title, parenPattern, RegexOptions.IgnoreCase);
            if (parenMatch.Success)
            {
                var remaining = title.Remove(parenMatch.Index, parenMatch.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (version, remaining);
            }
        }
        
        // Check as standalone matches
        foreach (var version in SeriesVersionIndicators.OrderByDescending(v => v.Length))
        {
            var pattern = $@"\b{Regex.Escape(version)}\b";
            var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var remaining = title.Remove(match.Index, match.Length).Trim();
                remaining = MultipleSpacesRegex().Replace(remaining, " ");
                return (version, remaining);
            }
        }
        
        return (null, title);
    }

    /// <summary>
    /// Detect if the extracted year is used for series disambiguation rather than publication date.
    /// Returns the year if it appears to be a disambiguation year (attached to series name).
    /// </summary>
    private static int? DetectDisambiguationYear(int? extractedYear, string seriesTitle)
    {
        if (!extractedYear.HasValue)
            return null;
        
        // If the series title ends with the year in parens, it's likely a disambiguation year
        // E.g., "Batman (2016)" - the year disambiguates which Batman series
        var yearPattern = $@"\({extractedYear}\)\s*$";
        if (Regex.IsMatch(seriesTitle, yearPattern))
        {
            return extractedYear;
        }
        
        // Also detect years that commonly indicate series runs (2011+)
        // Most modern series disambiguation uses 2011+ years
        if (extractedYear >= 2011)
        {
            return extractedYear;
        }
        
        return null;
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
    
    [GeneratedRegex(@"\b(19\d{2}|20\d{2})\b")]
    private static partial Regex YearAnywhereRegex();
    
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
    
    [GeneratedRegex(@"\b(19\d{2}|20\d{2})\s*-\s*(19\d{2}|20\d{2})\b")]
    private static partial Regex YearRangeRegex();
    
    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex PublisherInParensRegex();
    
    [GeneratedRegex(@"\s-\s*([A-Za-z][\w-]+)\s*$")]
    private static partial Regex ReleaseGroupRegex();
    
    [GeneratedRegex(@"\(([A-Za-z][\w-]+)\)\s*$")]
    private static partial Regex ReleaseGroupParensRegex();
    
    [GeneratedRegex(@"^[-_\.\s]+")]
    private static partial Regex LeadingPunctuationRegex();
    
    [GeneratedRegex(@"[-_\.\s]+$")]
    private static partial Regex TrailingPunctuationRegex();
    
    [GeneratedRegex(@"[\s\-_\.]+")]
    private static partial Regex TokenizeRegex();
    
    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex AllParenGroupsRegex();
    
    // New regex patterns for enhanced volume parsing
    [GeneratedRegex(@"\(v(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeInParensRegex();
    
    [GeneratedRegex(@"\bVol(?:ume)?\.?\s*(One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|First|Second|Third|Fourth|Fifth|Sixth|Seventh|Eighth|Ninth|Tenth|\d+(?:st|nd|rd|th))", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeWordRegex();
    
    // Year in brackets: [2023]
    [GeneratedRegex(@"\[(\d{4})\]")]
    private static partial Regex YearInBracketsRegex();
}
