using Microsoft.Extensions.Logging.Abstractions;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Infrastructure.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for NzbFilterService.
/// </summary>
public class NzbFilterServiceTests
{
    private readonly NzbReleaseParser _parser = new();
    private readonly NzbFilterService _filterService;

    public NzbFilterServiceTests()
    {
        _filterService = new NzbFilterService(NullLogger<NzbFilterService>.Instance, _parser);
    }

    private NzbCandidate CreateCandidate(
        string title = "Batman.001.2024.Digital.cbz-GROUP",
        long size = 50 * 1024 * 1024,
        DateTime? publishedDate = null,
        bool isPasswordProtected = false,
        List<int>? categories = null)
    {
        var parsedInfo = _parser.Parse(title);
        return new NzbCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = title,
            IndexerName = "TestIndexer",
            NzbUrl = "https://example.com/nzb",
            ParsedInfo = parsedInfo,
            Size = size,
            PublishedDate = publishedDate ?? DateTime.UtcNow.AddDays(-5),
            IsPasswordProtected = isPasswordProtected,
            Categories = categories ?? new List<int> { 7030 }
        };
    }

    #region Age Filtering

    [Fact]
    public void Filter_AgeTooNew_Rejects()
    {
        var candidate = CreateCandidate(publishedDate: DateTime.UtcNow); // 0 days old
        var settings = new NzbFilterSettings { MinAgeDays = 1 };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.TooNew, result.RejectionReason);
    }

    [Fact]
    public void Filter_AgeTooOld_Rejects()
    {
        var candidate = CreateCandidate(publishedDate: DateTime.UtcNow.AddDays(-30));
        var settings = new NzbFilterSettings { MaxAgeDays = 7 };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.TooOld, result.RejectionReason);
    }

    [Fact]
    public void Filter_AgeWithinRange_Accepts()
    {
        var candidate = CreateCandidate(publishedDate: DateTime.UtcNow.AddDays(-5));
        var settings = new NzbFilterSettings { MinAgeDays = 1, MaxAgeDays = 10 };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.True(result.Accepted);
    }

    #endregion

    #region Size Filtering

    [Fact]
    public void Filter_SizeTooSmall_Rejects()
    {
        var candidate = CreateCandidate(size: 1024 * 1024); // 1 MB
        var settings = new NzbFilterSettings { MinSizeBytes = 5 * 1024 * 1024 }; // 5 MB minimum
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.TooSmall, result.RejectionReason);
    }

    [Fact]
    public void Filter_SizeTooLarge_Rejects()
    {
        var candidate = CreateCandidate(size: 500 * 1024 * 1024); // 500 MB
        var settings = new NzbFilterSettings { MaxSizeBytes = 100 * 1024 * 1024 }; // 100 MB max
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.TooLarge, result.RejectionReason);
    }

    [Fact]
    public void Filter_SizeWithinRange_Accepts()
    {
        var candidate = CreateCandidate(size: 50 * 1024 * 1024); // 50 MB
        var settings = new NzbFilterSettings 
        { 
            MinSizeBytes = 1 * 1024 * 1024, 
            MaxSizeBytes = 100 * 1024 * 1024 
        };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.True(result.Accepted);
    }

    [Fact]
    public void Filter_SizeMB_ConvenienceProperties()
    {
        var settings = new NzbFilterSettings { MinSizeMB = 5, MaxSizeMB = 100 };
        
        Assert.Equal(5 * 1024 * 1024, settings.MinSizeBytes);
        Assert.Equal(100 * 1024 * 1024, settings.MaxSizeBytes);
    }

    #endregion

    #region Password Protection Filtering

    [Fact]
    public void Filter_PasswordProtected_RejectsWhenEnabled()
    {
        var candidate = CreateCandidate(isPasswordProtected: true);
        var settings = new NzbFilterSettings { RejectPasswordProtected = true };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.PasswordProtected, result.RejectionReason);
    }

    [Fact]
    public void Filter_PasswordProtected_AcceptsWhenDisabled()
    {
        var candidate = CreateCandidate(isPasswordProtected: true);
        var settings = new NzbFilterSettings { RejectPasswordProtected = false };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.True(result.Accepted);
    }

    #endregion

    #region Word Filtering

    [Fact]
    public void Filter_BannedWord_Rejects()
    {
        var candidate = CreateCandidate(title: "Batman.001.SAMPLE.2024.cbz-GROUP");
        var settings = new NzbFilterSettings { BannedWords = new List<string> { "sample" } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.BannedWordFound, result.RejectionReason);
    }

    [Fact]
    public void Filter_MissingRequiredWord_Rejects()
    {
        var candidate = CreateCandidate(title: "Batman.001.2024.cbr-GROUP");
        var settings = new NzbFilterSettings { RequiredWords = new List<string> { "digital" } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.MissingRequiredWord, result.RejectionReason);
    }

    [Fact]
    public void Filter_HasRequiredWord_Accepts()
    {
        var candidate = CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP");
        var settings = new NzbFilterSettings { RequiredWords = new List<string> { "digital" } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.True(result.Accepted);
    }

    [Fact]
    public void Filter_PreferredWord_IncreasesScore()
    {
        var candidate = CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP");
        var settingsWithPreferred = new NzbFilterSettings { PreferredWords = new List<string> { "digital" } };
        var settingsWithoutPreferred = new NzbFilterSettings { PreferredWords = new List<string>() };
        
        var resultWith = _filterService.Filter(candidate, settingsWithPreferred);
        var resultWithout = _filterService.Filter(candidate, settingsWithoutPreferred);
        
        Assert.True(resultWith.ScoreAdjustment > resultWithout.ScoreAdjustment);
    }

    #endregion

    #region Category Filtering

    [Fact]
    public void Filter_ExcludedCategory_Rejects()
    {
        var candidate = CreateCandidate(categories: new List<int> { 7030, 7000 });
        var settings = new NzbFilterSettings { ExcludeCategories = new List<int> { 7030 } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.CategoryExcluded, result.RejectionReason);
    }

    [Fact]
    public void Filter_NotInIncludedCategories_Rejects()
    {
        var candidate = CreateCandidate(categories: new List<int> { 7030 });
        var settings = new NzbFilterSettings { IncludeCategories = new List<int> { 1000, 2000 } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.CategoryNotIncluded, result.RejectionReason);
    }

    [Fact]
    public void Filter_InIncludedCategory_Accepts()
    {
        var candidate = CreateCandidate(categories: new List<int> { 7030 });
        var settings = new NzbFilterSettings { IncludeCategories = new List<int> { 7030, 7000 } };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.True(result.Accepted);
    }

    #endregion

    #region Confidence Filtering

    [Fact]
    public void Filter_LowConfidence_Rejects()
    {
        var candidate = CreateCandidate(title: "Batman"); // Low info = low confidence
        var settings = new NzbFilterSettings { MinParseConfidence = 50 };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.False(result.Accepted);
        Assert.Equal(NzbRejectionReason.LowConfidence, result.RejectionReason);
    }

    #endregion

    #region Format Preference

    [Fact]
    public void Filter_PreferredFormat_IncreasesScore()
    {
        var candidateCbz = CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP");
        var candidateCbr = CreateCandidate(title: "Batman.001.2024.Digital.cbr-GROUP");
        var settings = new NzbFilterSettings { PreferredFormats = new List<string> { "cbz", "cbr" } };
        
        var resultCbz = _filterService.Filter(candidateCbz, settings);
        var resultCbr = _filterService.Filter(candidateCbr, settings);
        
        Assert.True(resultCbz.ScoreAdjustment > resultCbr.ScoreAdjustment, 
            "CBZ should have higher score than CBR when CBZ is more preferred");
    }

    #endregion

    #region Release Modifiers

    [Fact]
    public void Filter_ProperRelease_IncreasesScore()
    {
        var properCandidate = CreateCandidate(title: "Batman.001.PROPER.2024.Digital.cbz-GROUP");
        var normalCandidate = CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP");
        var settings = new NzbFilterSettings { PreferProper = true };
        
        var properResult = _filterService.Filter(properCandidate, settings);
        var normalResult = _filterService.Filter(normalCandidate, settings);
        
        Assert.True(properResult.ScoreAdjustment > normalResult.ScoreAdjustment);
    }

    [Fact]
    public void Filter_RepackRelease_IncreasesScore()
    {
        var repackCandidate = CreateCandidate(title: "Batman.001.REPACK.2024.Digital.cbz-GROUP");
        var normalCandidate = CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP");
        var settings = new NzbFilterSettings { PreferRepack = true };
        
        var repackResult = _filterService.Filter(repackCandidate, settings);
        var normalResult = _filterService.Filter(normalCandidate, settings);
        
        Assert.True(repackResult.ScoreAdjustment > normalResult.ScoreAdjustment);
    }

    #endregion

    #region Preferred Indexer

    [Fact]
    public void Filter_PreferredIndexer_IncreasesScore()
    {
        var parsedInfo = _parser.Parse("Batman.001.2024.Digital.cbz-GROUP");
        var preferredCandidate = new NzbCandidate
        {
            Id = "1",
            ReleaseTitle = "Batman.001.2024.Digital.cbz-GROUP",
            IndexerName = "PreferredIndexer",
            IndexerId = "preferred-id",
            NzbUrl = "https://example.com/nzb",
            ParsedInfo = parsedInfo,
            Size = 50 * 1024 * 1024,
            PublishedDate = DateTime.UtcNow.AddDays(-5)
        };
        
        var settings = new NzbFilterSettings 
        { 
            PreferredIndexers = new List<string> { "preferred-id" } 
        };
        
        var result = _filterService.Filter(preferredCandidate, settings);
        
        Assert.True(result.ScoreAdjustment >= 20, "Preferred indexer should add +20 bonus");
    }

    #endregion

    #region FilterMany

    [Fact]
    public void FilterMany_FiltersOutRejectedCandidates()
    {
        var candidates = new List<NzbCandidate>
        {
            CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP"),
            CreateCandidate(title: "Batman.002.SAMPLE.2024.cbz-GROUP"), // Should be filtered
            CreateCandidate(title: "Batman.003.2024.Digital.cbz-GROUP"),
        };
        var settings = new NzbFilterSettings { BannedWords = new List<string> { "sample" } };
        
        var filtered = _filterService.FilterMany(candidates, settings).ToList();
        
        Assert.Equal(2, filtered.Count);
        Assert.DoesNotContain(filtered, c => c.ReleaseTitle.Contains("SAMPLE"));
    }

    [Fact]
    public void FilterMany_MarksFilteredCandidates()
    {
        var candidates = new List<NzbCandidate>
        {
            CreateCandidate(title: "Batman.001.SAMPLE.2024.cbz-GROUP"),
        };
        var settings = new NzbFilterSettings { BannedWords = new List<string> { "sample" } };
        
        // Process through FilterMany (it yields nothing, but candidate is modified)
        _ = _filterService.FilterMany(candidates, settings).ToList();
        
        Assert.True(candidates[0].IsFiltered);
        Assert.NotNull(candidates[0].FilterReason);
    }

    [Fact]
    public void FilterMany_SetsQualityScores()
    {
        var candidates = new List<NzbCandidate>
        {
            CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP"),
        };
        var settings = NzbFilterSettings.Default;
        
        var filtered = _filterService.FilterMany(candidates, settings).ToList();
        
        Assert.True(filtered[0].QualityScore > 0, "Quality score should be set");
    }

    #endregion

    #region FilterAndSort

    [Fact]
    public void FilterAndSort_SortsByQualityScoreDescending()
    {
        var candidates = new List<NzbCandidate>
        {
            CreateCandidate(title: "Batman.001.2024.cbr-GROUP"), // Lower quality (CBR)
            CreateCandidate(title: "Batman.001.2024.Digital.cbz-GROUP"), // Higher quality (Digital + CBZ)
            CreateCandidate(title: "Batman.001.2024.Scan.cbz-GROUP"), // Medium quality (Scan)
        };
        var settings = new NzbFilterSettings 
        { 
            PreferredFormats = new List<string> { "cbz", "cbr" },
            PreferredWords = new List<string> { "digital" }
        };
        
        var sorted = _filterService.FilterAndSort(candidates, settings);
        
        Assert.Equal(3, sorted.Count);
        Assert.True(sorted[0].QualityScore >= sorted[1].QualityScore);
        Assert.True(sorted[1].QualityScore >= sorted[2].QualityScore);
    }

    [Fact]
    public void FilterAndSort_PrefersNewerForSameQuality()
    {
        // Same title, different dates
        var newerCandidate = CreateCandidate(
            title: "Batman.001.2024.Digital.cbz-GROUP", 
            publishedDate: DateTime.UtcNow.AddDays(-1));
        var olderCandidate = CreateCandidate(
            title: "Batman.001.2024.Digital.cbz-GROUP",
            publishedDate: DateTime.UtcNow.AddDays(-10));
        
        var candidates = new List<NzbCandidate> { olderCandidate, newerCandidate };
        var settings = NzbFilterSettings.Default;
        
        var sorted = _filterService.FilterAndSort(candidates, settings);
        
        Assert.Equal(2, sorted.Count);
        // Both have same quality, so newer should be first
        Assert.True(sorted[0].Age < sorted[1].Age, "Newer release should come first");
    }

    #endregion

    #region Default Settings

    [Fact]
    public void GetDefaultSettings_ReturnsNonNull()
    {
        var settings = _filterService.GetDefaultSettings();
        
        Assert.NotNull(settings);
        Assert.Contains("sample", settings.BannedWords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("digital", settings.PreferredWords, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultSettings_HasReasonableDefaults()
    {
        var settings = NzbFilterSettings.Default;
        
        Assert.True(settings.RejectPasswordProtected);
        Assert.True(settings.PreferProper);
        Assert.True(settings.PreferRepack);
        Assert.NotEmpty(settings.PreferredFormats);
        Assert.NotEmpty(settings.BannedWords);
    }

    #endregion

    #region Check Details

    [Fact]
    public void Filter_ReturnsDetailedChecks()
    {
        var candidate = CreateCandidate(title: "Batman.001.PROPER.2024.Digital.cbz-GROUP");
        var settings = new NzbFilterSettings 
        { 
            PreferProper = true,
            PreferredWords = new List<string> { "digital" },
            BannedWords = new List<string> { "sample" }
        };
        
        var result = _filterService.Filter(candidate, settings);
        
        Assert.NotEmpty(result.Checks);
        Assert.Contains(result.Checks, c => c.Name == "BannedWords" && c.Passed);
        Assert.Contains(result.Checks, c => c.Name == "Proper" && c.Passed);
    }

    #endregion
}
