using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DDL candidate filtering.
/// Validates Mylar3-compatible filtering rules.
/// </summary>
public class DdlFilterTests
{
    private readonly IDdlFilter _filter = new DdlFilter();
    private readonly DdlFilterSettings _defaultSettings = new();

    #region Banned Words

    [Fact]
    public void CheckCandidate_WithBannedWord_Rejects()
    {
        var candidate = CreateCandidate("Batman #001 (sample).cbz");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.False(passes);
        Assert.Contains("sample", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_WithPreviewWord_Rejects()
    {
        var candidate = CreateCandidate("Batman #001 preview.cbz");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.False(passes);
        Assert.Contains("preview", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_WithoutBannedWords_Passes()
    {
        var candidate = CreateCandidate("Batman #001 (2023).cbz");
        
        var (passes, _) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.True(passes);
    }

    [Fact]
    public void CheckCandidate_CustomBannedWord_Rejects()
    {
        var settings = new DdlFilterSettings
        {
            BannedWords = new List<string> { "NUKED" }
        };
        var candidate = CreateCandidate("Batman #001 NUKED.cbz");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("NUKED", reason);
    }

    #endregion

    #region Required Words

    [Fact]
    public void CheckCandidate_MissingRequiredWord_Rejects()
    {
        var settings = new DdlFilterSettings
        {
            RequiredWords = new List<string> { "Digital" }
        };
        var candidate = CreateCandidate("Batman #001.cbz");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("Digital", reason);
    }

    [Fact]
    public void CheckCandidate_HasRequiredWord_Passes()
    {
        var settings = new DdlFilterSettings
        {
            RequiredWords = new List<string> { "Digital" }
        };
        var candidate = CreateCandidate("Batman #001 (Digital).cbz");
        
        var (passes, _) = _filter.CheckCandidate(candidate, settings);
        
        Assert.True(passes);
    }

    #endregion

    #region Format Filtering

    [Fact]
    public void CheckCandidate_BlockedFormat_Rejects()
    {
        var settings = new DdlFilterSettings
        {
            BlockedFormats = new List<string> { "pdf" }
        };
        var candidate = CreateCandidate("Batman #001.pdf", format: "pdf");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("pdf", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_PreferredFormat_Passes()
    {
        var candidate = CreateCandidate("Batman #001.cbz", format: "cbz");
        
        var (passes, _) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.True(passes);
    }

    [Fact]
    public void CheckCandidate_RequirePreferredFormat_RejectsUnknown()
    {
        var settings = new DdlFilterSettings
        {
            RequirePreferredFormat = true,
            PreferredFormats = new List<string> { "cbz", "cbr" }
        };
        var candidate = CreateCandidate("Batman #001.cb7", format: "cb7");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("not in preferred", reason?.ToLowerInvariant());
    }

    #endregion

    #region Size Filtering

    [Fact]
    public void CheckCandidate_TooSmallSingle_Rejects()
    {
        var candidate = CreateCandidate("Batman #001.cbz", size: 500_000); // 500KB
        
        var (passes, reason) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.False(passes);
        Assert.Contains("below minimum", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_TooLargeSingle_Rejects()
    {
        var candidate = CreateCandidate("Batman #001.cbz", size: 300_000_000); // 300MB
        
        var (passes, reason) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.False(passes);
        Assert.Contains("exceeds maximum", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_ValidSizeSingle_Passes()
    {
        var candidate = CreateCandidate("Batman #001.cbz", size: 15_000_000); // 15MB
        
        var (passes, _) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.True(passes);
    }

    [Fact]
    public void CheckCandidate_TooSmallCollection_Rejects()
    {
        var candidate = CreateCandidate("Batman TPB Vol 1.cbz", size: 1_000_000, isCollection: true); // 1MB
        
        var (passes, reason) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.False(passes);
        Assert.Contains("collection", reason?.ToLowerInvariant());
    }

    [Fact]
    public void CheckCandidate_ValidSizeCollection_Passes()
    {
        var candidate = CreateCandidate("Batman TPB Vol 1.cbz", size: 50_000_000, isCollection: true); // 50MB
        
        var (passes, _) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.True(passes);
    }

    [Fact]
    public void CheckCandidate_NoSize_Passes()
    {
        var candidate = CreateCandidate("Batman #001.cbz", size: null);
        
        var (passes, _) = _filter.CheckCandidate(candidate, _defaultSettings);
        
        Assert.True(passes);
    }

    #endregion

    #region Parse Confidence

    [Fact]
    public void CheckCandidate_LowConfidence_RejectsWhenRequired()
    {
        var settings = new DdlFilterSettings
        {
            MinParseConfidence = 50
        };
        var candidate = CreateCandidate("random_file.cbz", confidence: 20);
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("confidence", reason?.ToLowerInvariant());
    }

    #endregion

    #region Series Title Requirement

    [Fact]
    public void CheckCandidate_NoSeriesTitle_Rejects()
    {
        var settings = new DdlFilterSettings
        {
            RequireSeriesTitle = true
        };
        var candidate = CreateCandidate("#001.cbz", seriesTitle: null);
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("series", reason?.ToLowerInvariant());
    }

    #endregion

    #region Blocked Release Groups

    [Fact]
    public void CheckCandidate_BlockedGroup_Rejects()
    {
        var settings = new DdlFilterSettings
        {
            BlockedGroups = new List<string> { "BadGroup" }
        };
        var candidate = CreateCandidate("Batman #001.cbz", releaseGroup: "BadGroup");
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.False(passes);
        Assert.Contains("BadGroup", reason);
    }

    #endregion

    #region Batch Filtering

    [Fact]
    public void Filter_MarksCandidatesCorrectly()
    {
        var candidates = new[]
        {
            CreateCandidate("Batman #001.cbz"),
            CreateCandidate("Batman #001 (sample).cbz"),
            CreateCandidate("Batman #002.cbz")
        };
        
        var results = _filter.Filter(candidates, _defaultSettings);
        
        Assert.Equal(3, results.Count);
        Assert.False(results[0].IsFiltered);
        Assert.True(results[1].IsFiltered);
        Assert.False(results[2].IsFiltered);
    }

    #endregion

    #region Helpers

    private static DdlCandidate CreateCandidate(
        string title, 
        string format = "cbz",
        long? size = 15_000_000,
        bool isCollection = false,
        string? seriesTitle = "Batman",
        string? releaseGroup = null,
        int confidence = 50)
    {
        return new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = title,
            SourceSite = "TestSite",
            Size = size,
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = seriesTitle,
                Format = format,
                IsCollection = isCollection,
                ReleaseGroup = releaseGroup,
                Confidence = confidence
            }
        };
    }

    #endregion
}



