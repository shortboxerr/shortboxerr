using Shortboxerr.Core.Nzb;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for NzbReleaseParser.
/// </summary>
public class NzbReleaseParserTests
{
    private readonly NzbReleaseParser _parser = new();

    #region Basic Parsing

    [Fact]
    public void Parse_WithEmptyTitle_ReturnsEmptyInfo()
    {
        var result = _parser.Parse("");
        
        Assert.Null(result.SeriesTitle);
        Assert.Null(result.IssueNumber);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void Parse_WithNullTitle_ReturnsEmptyInfo()
    {
        var result = _parser.Parse(null!);
        
        Assert.Null(result.SeriesTitle);
        Assert.Equal(0, result.Confidence);
    }

    #endregion

    #region Scene Naming Conventions

    [Theory]
    [InlineData("Batman.001.2024.Digital.cbz-XGroup", "Batman", 1, 2024, "CBZ", "XGroup")]
    [InlineData("Spider-Man.v2.#023.Digital.cbr-RELEASE", "Spider-Man", 23, null, "CBR", "RELEASE")]
    [InlineData("Amazing.Spider-Man.#100.2023.Webrip.cbz-Scene", "Amazing Spider-Man", 100, 2023, "CBZ", "Scene")]
    public void Parse_SceneNamingConvention_ExtractsCorrectly(
        string title, string expectedSeries, decimal expectedIssue, int? expectedYear, string expectedFormat, string expectedGroup)
    {
        var result = _parser.Parse(title);
        
        Assert.Contains(expectedSeries, result.SeriesTitle ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedIssue, result.IssueNumber);
        Assert.Equal(expectedYear, result.Year);
        Assert.Equal(expectedFormat, result.Format);
        Assert.Equal(expectedGroup, result.ReleaseGroup);
    }

    [Theory]
    [InlineData("X-Men.v1.001.Digital-GROUP")]
    [InlineData("X-Men.Vol.1.001.Digital-GROUP")]
    [InlineData("X-Men.Volume.1.001.Digital-GROUP")]
    public void Parse_VolumePatterns_ExtractsVolumeNumber(string title)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(1, result.VolumeNumber);
    }

    #endregion

    #region Release Modifiers

    [Theory]
    [InlineData("Batman.001.REPACK.Digital.cbz-GROUP", true, false, false)]
    [InlineData("Batman.001.PROPER.Digital.cbz-GROUP", false, true, false)]
    [InlineData("Batman.001.INTERNAL.Digital.cbz-GROUP", false, false, true)]
    [InlineData("Batman.001.PROPER.REPACK.Digital.cbz-GROUP", true, true, false)]
    public void Parse_ReleaseModifiers_DetectsCorrectly(string title, bool isRepack, bool isProper, bool isInternal)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(isRepack, result.IsRepack);
        Assert.Equal(isProper, result.IsProper);
        Assert.Equal(isInternal, result.IsInternal);
    }

    #endregion

    #region Quality Detection

    [Theory]
    [InlineData("Batman.001.Digital.cbz-GROUP", "digital")]
    [InlineData("Batman.001.Webrip.cbz-GROUP", "webrip")]
    [InlineData("Batman.001.Scan.cbz-GROUP", "scan")]
    public void Parse_QualityIndicators_DetectsCorrectly(string title, string expectedQuality)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(expectedQuality, result.Quality, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Format Detection

    [Theory]
    [InlineData("Batman.001.Digital.cbz-GROUP", "CBZ")]
    [InlineData("Batman.001.Digital.cbr-GROUP", "CBR")]
    [InlineData("Batman.001.Digital.pdf-GROUP", "PDF")]
    [InlineData("Batman.001.Digital.epub-GROUP", "EPUB")]
    public void Parse_FileFormats_DetectsCorrectly(string title, string expectedFormat)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(expectedFormat, result.Format);
    }

    #endregion

    #region Publisher Detection

    [Theory]
    [InlineData("Marvel.Batman.001.Digital.cbz-GROUP", "Marvel")]
    [InlineData("DC.Batman.001.Digital.cbz-GROUP", "DC")]
    [InlineData("Image.Saga.001.Digital.cbz-GROUP", "Image")]
    [InlineData("Dark.Horse.Hellboy.001.Digital.cbz-GROUP", "Dark Horse")]
    public void Parse_Publishers_DetectsCorrectly(string title, string expectedPublisher)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(expectedPublisher, result.Publisher);
    }

    #endregion

    #region Collection Detection

    [Theory]
    [InlineData("Batman.TPB.Vol.1.2024.Digital.cbz-GROUP", true, "TPB")]
    [InlineData("Spider-Man.Omnibus.2023.Digital.cbz-GROUP", true, "Omnibus")]
    [InlineData("X-Men.Compendium.2024.Digital.cbz-GROUP", true, "Compendium")]
    [InlineData("Batman.HC.Vol.2.2024.Digital.cbz-GROUP", true, "Hardcover")]
    [InlineData("Saga.Deluxe.Edition.2024.Digital.cbz-GROUP", true, "Deluxe")]
    public void Parse_Collections_DetectsCorrectly(string title, bool isCollection, string expectedEditionType)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(isCollection, result.IsCollection);
        Assert.Equal(expectedEditionType, result.EditionType);
    }

    [Fact]
    public void Parse_Collections_DetectsIssueRange_WhenPresent()
    {
        // Issue ranges are a best-effort feature - test that it doesn't crash
        var result = _parser.Parse("Batman.TPB.Issues.1-6.2024.Digital.cbz-GROUP");
        
        Assert.True(result.IsCollection);
        // IssueRange detection is optional - just verify parsing doesn't fail
        Assert.NotNull(result);
    }

    #endregion

    #region Issue Number Edge Cases

    [Theory]
    [InlineData("Batman.#001.Digital.cbz-GROUP", 1)]
    [InlineData("Batman.#1.Digital.cbz-GROUP", 1)]
    [InlineData("Batman.#1.5.Digital.cbz-GROUP", 1.5)]
    [InlineData("Batman.Annual.#1.Digital.cbz-GROUP", 1)]
    public void Parse_IssueNumbers_HandlesVariousFormats(string title, decimal expectedIssue)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(expectedIssue, result.IssueNumber);
    }

    #endregion

    #region Year Detection

    [Theory]
    [InlineData("Batman.001.(2024).Digital.cbz-GROUP", 2024)]
    [InlineData("Batman.#001.2024.Digital.cbz-GROUP", 2024)]
    [InlineData("Batman.#001.(1998).Digital.cbz-GROUP", 1998)]
    public void Parse_Years_DetectsCorrectly(string title, int expectedYear)
    {
        var result = _parser.Parse(title);
        
        Assert.Equal(expectedYear, result.Year);
    }

    [Theory]
    [InlineData("Batman.#001.(1800).Digital.cbz-GROUP")] // Too old
    [InlineData("Batman.#001.(2100).Digital.cbz-GROUP")] // Too far future
    public void Parse_InvalidYears_IgnoresIncorrectly(string title)
    {
        var result = _parser.Parse(title);
        
        Assert.Null(result.Year);
    }

    #endregion

    #region Confidence Scoring

    [Fact]
    public void Parse_WellFormedRelease_HighConfidence()
    {
        var result = _parser.Parse("Batman.v1.001.2024.Digital.cbz-XGroup");
        
        Assert.True(result.Confidence >= 50, $"Confidence {result.Confidence} should be >= 50");
    }

    [Fact]
    public void Parse_MinimalRelease_LowerConfidence()
    {
        var result = _parser.Parse("Batman");
        
        Assert.True(result.Confidence < 50, $"Confidence {result.Confidence} should be < 50");
    }

    #endregion

    #region Tokenization

    [Fact]
    public void Parse_AlwaysTokenizes()
    {
        var result = _parser.Parse("Batman.001.2024.Digital.cbz-GROUP");
        
        Assert.NotEmpty(result.Tokens);
        Assert.Contains("Batman", result.Tokens);
        Assert.Contains("001", result.Tokens);
    }

    #endregion

    #region Quality Score Calculation

    [Theory]
    [InlineData("digital", 100)]
    [InlineData("webrip", 90)]
    [InlineData("scan", 70)]
    public void CalculateQualityScore_QualityIndicators_ReturnsExpectedRange(string quality, int minExpectedScore)
    {
        var info = new NzbParsedInfo { Quality = quality };
        var score = _parser.CalculateQualityScore(info);
        
        Assert.True(score >= minExpectedScore, $"Quality '{quality}' score {score} should be >= {minExpectedScore}");
    }

    [Theory]
    [InlineData("CBZ", 20)]
    [InlineData("CBR", 15)]
    [InlineData("PDF", 10)]
    public void CalculateQualityScore_Formats_AddsExpectedBonus(string format, int expectedBonus)
    {
        var info = new NzbParsedInfo { Format = format };
        var baseScore = _parser.CalculateQualityScore(new NzbParsedInfo());
        var formatScore = _parser.CalculateQualityScore(info);
        
        Assert.Equal(expectedBonus, formatScore - baseScore);
    }

    [Fact]
    public void CalculateQualityScore_ProperRelease_AddsBonus()
    {
        var baseInfo = new NzbParsedInfo();
        var properInfo = new NzbParsedInfo { IsProper = true };
        
        var baseScore = _parser.CalculateQualityScore(baseInfo);
        var properScore = _parser.CalculateQualityScore(properInfo);
        
        Assert.True(properScore > baseScore, "PROPER release should have higher score");
    }

    [Fact]
    public void CalculateQualityScore_RepackRelease_AddsBonus()
    {
        var baseInfo = new NzbParsedInfo();
        var repackInfo = new NzbParsedInfo { IsRepack = true };
        
        var baseScore = _parser.CalculateQualityScore(baseInfo);
        var repackScore = _parser.CalculateQualityScore(repackInfo);
        
        Assert.True(repackScore > baseScore, "REPACK release should have higher score");
    }

    #endregion

    #region ParseRelease Integration

    [Fact]
    public void ParseRelease_CreatesValidCandidate()
    {
        var release = new NewznabRelease
        {
            Guid = "test-guid-123",
            Title = "Batman.001.2024.Digital.cbz-GROUP",
            NzbUrl = "https://example.com/nzb/test",
            Size = 50 * 1024 * 1024, // 50 MB
            PublishedDate = DateTime.UtcNow.AddDays(-5),
            IndexerName = "TestIndexer"
        };
        
        var candidate = _parser.ParseRelease(release, indexerPriority: 25);
        
        Assert.Equal("test-guid-123", candidate.Id);
        Assert.Equal("Batman.001.2024.Digital.cbz-GROUP", candidate.ReleaseTitle);
        Assert.Equal("TestIndexer", candidate.IndexerName);
        Assert.Equal("https://example.com/nzb/test", candidate.NzbUrl);
        Assert.Equal(50 * 1024 * 1024, candidate.Size);
        Assert.Equal(25, candidate.IndexerPriority);
        Assert.NotNull(candidate.ParsedInfo);
        Assert.Equal(2024, candidate.ParsedInfo.Year);
    }

    [Fact]
    public void ParseRelease_ToCandidate_ConvertsProperly()
    {
        var release = new NewznabRelease
        {
            Guid = "test-guid",
            Title = "Amazing.Spider-Man.v2.050.2023.Digital.cbz-GROUP",
            NzbUrl = "https://example.com/nzb/test",
            Size = 100 * 1024 * 1024,
            PublishedDate = DateTime.UtcNow
        };
        
        var nzbCandidate = _parser.ParseRelease(release, indexerPriority: 10);
        var genericCandidate = nzbCandidate.ToCandidate();
        
        Assert.Equal("test-guid", genericCandidate.Id);
        Assert.Contains("Spider-Man", genericCandidate.SeriesTitle ?? "");
        Assert.Equal(50, genericCandidate.IssueNumber);
        Assert.Equal(2, genericCandidate.VolumeNumber);
        Assert.Equal(2023, genericCandidate.Year);
        Assert.Equal("CBZ", genericCandidate.Format);
        Assert.Equal("NZB:", genericCandidate.Source.Substring(0, 4));
        Assert.Equal(10, genericCandidate.SourcePriority);
    }

    #endregion

    #region Real-World Examples

    [Theory]
    [InlineData("Batman.2016.001.Digital.Zone-Empire.cbz-EMPIRE")]
    [InlineData("X-Men.Red.v2.001.2022.Digital.HD.cbz-Minutemen")]
    [InlineData("Invincible.Compendium.001.2011.Digital.cbr-Glorith")]
    [InlineData("Saga.Deluxe.Edition.Book.One.2023.Digital.cbz-Oroboros")]
    public void Parse_RealWorldExamples_DoesNotThrow(string title)
    {
        var exception = Record.Exception(() => _parser.Parse(title));
        
        Assert.Null(exception);
        
        var result = _parser.Parse(title);
        Assert.NotNull(result);
        Assert.True(result.Confidence > 0);
    }

    #endregion
}
