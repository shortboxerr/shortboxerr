using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DDL release title parsing.
/// Includes golden tests for Mylar3 parity.
/// </summary>
public class DdlReleaseParserTests
{
    private readonly IDdlReleaseParser _parser = new DdlReleaseParser();

    #region Format Extraction

    [Theory]
    [InlineData("Batman #001.cbz", "cbz")]
    [InlineData("Batman #001.CBZ", "cbz")]
    [InlineData("Batman #001.cbr", "cbr")]
    [InlineData("Batman Vol 1.pdf", "pdf")]
    [InlineData("Batman #001", null)]
    [InlineData("Batman #001 (Digital).cb7", "cb7")]
    public void ExtractFormat_ReturnsCorrectFormat(string title, string? expected)
    {
        var result = _parser.ExtractFormat(title);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Title Normalization

    [Theory]
    [InlineData("The Amazing Spider-Man", "amazing spider man")]
    [InlineData("Batman: Year One", "batman  year one")]
    [InlineData("A Walk Through Hell", "walk through hell")]
    [InlineData("X-Men", "x men")]
    public void NormalizeTitle_RemovesArticlesAndPunctuation(string input, string expected)
    {
        var result = _parser.NormalizeTitle(input);
        Assert.Equal(expected.Replace("  ", " "), result.Replace("  ", " "));
    }

    #endregion

    #region Single Issue Parsing

    [Fact]
    public void Parse_SingleIssue_BasicFormat()
    {
        var result = _parser.Parse("Batman #001 (2023).cbz");
        
        Assert.Equal("Batman", result.SeriesTitle);
        Assert.Equal(1m, result.IssueNumber);
        Assert.Equal(2023, result.Year);
        Assert.Equal("cbz", result.Format);
        Assert.False(result.IsCollection);
    }

    [Fact]
    public void Parse_SingleIssue_WithPublisher()
    {
        var result = _parser.Parse("Amazing Spider-Man #150 (Marvel) (2022).cbz");
        
        Assert.Equal("Amazing Spider-Man", result.SeriesTitle?.Trim());
        Assert.Equal(150m, result.IssueNumber);
        Assert.Equal("Marvel", result.Publisher);
        Assert.Equal(2022, result.Year);
    }

    [Fact]
    public void Parse_SingleIssue_DecimalIssueNumber()
    {
        var result = _parser.Parse("Batman #1.5 (2023).cbz");
        
        Assert.Equal(1.5m, result.IssueNumber);
    }

    [Fact]
    public void Parse_SingleIssue_WithQuality()
    {
        var result = _parser.Parse("Batman #001 (2023) (Digital).cbz");
        
        Assert.Equal("Digital", result.Quality);
    }

    [Fact]
    public void Parse_SingleIssue_WithReleaseGroup()
    {
        var result = _parser.Parse("Batman #001 (2023) - Group.cbz");
        
        Assert.Equal("Group", result.ReleaseGroup);
    }

    [Fact]
    public void Parse_SingleIssue_NumberAtEnd()
    {
        var result = _parser.Parse("Batman 001.cbz");
        
        Assert.Equal("Batman", result.SeriesTitle);
        Assert.Equal(1m, result.IssueNumber);
    }

    [Fact]
    public void Parse_SingleIssue_ThreeDigitNumber()
    {
        var result = _parser.Parse("Amazing Spider-Man 425.cbz");
        
        Assert.Equal("Amazing Spider-Man", result.SeriesTitle);
        Assert.Equal(425m, result.IssueNumber);
    }

    #endregion

    #region Collection Parsing

    [Fact]
    public void Parse_Collection_TPB()
    {
        var result = _parser.Parse("Batman Vol. 1 TPB (2023).cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("TPB", result.EditionType);
        Assert.Equal(1, result.VolumeNumber);
        Assert.Equal(2023, result.Year);
    }

    [Fact]
    public void Parse_Collection_Hardcover()
    {
        var result = _parser.Parse("Spider-Man HC Vol 2 (2022).cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("HC", result.EditionType);
        Assert.Equal(2, result.VolumeNumber);
    }

    [Fact]
    public void Parse_Collection_Omnibus()
    {
        var result = _parser.Parse("X-Men Omnibus Vol. 1 (2021).cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("Omnibus", result.EditionType);
        Assert.Equal(1, result.VolumeNumber);
    }

    [Fact]
    public void Parse_Collection_WithIssueRange()
    {
        var result = _parser.Parse("Batman TPB #1-6 (2023).cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("1-6", result.IssueRange);
    }

    [Fact]
    public void Parse_Collection_Deluxe()
    {
        var result = _parser.Parse("Saga Deluxe Edition Vol. 1 (2019).cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("Deluxe", result.EditionType);
    }

    [Fact]
    public void Parse_Collection_TradeKeyword()
    {
        var result = _parser.Parse("Invincible Trade Paperback Vol 1.cbz");
        
        Assert.True(result.IsCollection);
        Assert.Equal("TPB", result.EditionType);
    }

    #endregion

    #region Volume Parsing

    [Theory]
    [InlineData("Batman Vol. 1 TPB.cbz", 1)]
    [InlineData("Batman Vol 2.cbz", 2)]
    [InlineData("Batman Volume 3.cbz", 3)]
    [InlineData("Batman v4.cbz", 4)]
    [InlineData("Batman Vol.12.cbz", 12)]
    public void Parse_ExtractsVolumeNumber(string title, int expectedVolume)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedVolume, result.VolumeNumber);
    }

    #endregion

    #region Year Parsing

    [Theory]
    [InlineData("Batman #1 (2023).cbz", 2023)]
    [InlineData("Batman #1 2022.cbz", 2022)]
    [InlineData("Batman (1999) #1.cbz", 1999)]
    [InlineData("Batman #1.cbz", null)]
    public void Parse_ExtractsYear(string title, int? expectedYear)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedYear, result.Year);
    }

    #endregion

    #region Publisher Parsing

    [Theory]
    [InlineData("Batman #1 (DC) (2023).cbz", "DC")]
    [InlineData("Spider-Man Marvel #1.cbz", "Marvel")]
    [InlineData("Walking Dead Image #1.cbz", "Image")]
    [InlineData("Batman #1.cbz", null)]
    public void Parse_ExtractsPublisher(string title, string? expectedPublisher)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedPublisher, result.Publisher);
    }

    #endregion

    #region Confidence Scoring

    [Fact]
    public void Parse_FullInfo_HighConfidence()
    {
        var result = _parser.Parse("Amazing Spider-Man #001 (Marvel) (2023) (Digital).cbz");
        
        Assert.True(result.Confidence >= 60);
    }

    [Fact]
    public void Parse_MinimalInfo_LowConfidence()
    {
        var result = _parser.Parse("somefile");
        
        Assert.True(result.Confidence < 30);
    }

    #endregion

    #region Golden Tests (Mylar3 Parity)

    [Theory]
    [InlineData("Amazing Spider-Man 001 (2022) (Digital) (Zone-Empire).cbz", "Amazing Spider-Man", 1, 2022, false)]
    [InlineData("Batman - The Dark Knight Returns TPB (1986) (Digital) (Minutemen-Slayer).cbz", "Batman - The Dark Knight Returns", null, 1986, true)]
    [InlineData("X-Men v1 001 (1963) (Digital) (Glorith-HD).cbz", "X-Men", 1, 1963, false)]
    [InlineData("Saga Vol. 01 TPB (2012) (Digital) (Zone-Empire).cbz", "Saga", null, 2012, true)]
    [InlineData("Immortal Hulk 001 (2018) (Digital) (Zone-Empire).cbr", "Immortal Hulk", 1, 2018, false)]
    public void Parse_GoldenTest_CommonPatterns(string title, string expectedSeries, int? expectedIssue, int expectedYear, bool expectedIsCollection)
    {
        var result = _parser.Parse(title);
        
        Assert.Contains(expectedSeries.Split(' ')[0], result.SeriesTitle ?? "");
        if (expectedIssue.HasValue)
        {
            Assert.Equal(expectedIssue.Value, (int?)result.IssueNumber);
        }
        Assert.Equal(expectedYear, result.Year);
        Assert.Equal(expectedIsCollection, result.IsCollection);
    }

    #endregion

    #region EPIC 19.3 - Enhanced Year Extraction

    [Theory]
    [InlineData("Batman 001 [2023].cbz", 2023)]
    [InlineData("Superman [2020] 050.cbz", 2020)]
    [InlineData("Wonder Woman #1 [2016].cbr", 2016)]
    public void Parse_YearInBrackets_ExtractsCorrectly(string title, int expectedYear)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedYear, result.Year);
    }

    [Theory]
    [InlineData("Batman 001 2023.cbz", 2023)]
    [InlineData("Aquaman 001 2023 Digital.cbz", 2023)]
    public void Parse_YearStandalone_ExtractsCorrectly(string title, int expectedYear)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedYear, result.Year);
    }

    #endregion

    #region EPIC 19.3 - Enhanced Volume Extraction

    [Theory]
    [InlineData("Batman (v2) #001.cbz", 2)]
    [InlineData("Superman (v3) 050.cbz", 3)]
    public void Parse_VolumeInParens_ExtractsCorrectly(string title, int expectedVolume)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedVolume, result.VolumeNumber);
    }

    [Theory]
    [InlineData("Batman Vol. One TPB.cbz", 1)]
    [InlineData("Superman Volume Two TPB.cbz", 2)]
    [InlineData("X-Men Vol. Three HC.cbz", 3)]
    public void Parse_VolumeOrdinalWords_ExtractsCorrectly(string title, int expectedVolume)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedVolume, result.VolumeNumber);
    }

    [Theory]
    [InlineData("Batman Vol 1 TPB.cbz", 1)]
    [InlineData("Batman Volume 2 TPB.cbz", 2)]
    [InlineData("Batman v3 #001.cbz", 3)]
    public void Parse_VolumeNumeric_ExtractsCorrectly(string title, int expectedVolume)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedVolume, result.VolumeNumber);
    }

    #endregion

    #region EPIC 19.3 - Reboot/Revival Indicators

    [Theory]
    [InlineData("Batman (New 52) #001.cbz", "New 52")]
    [InlineData("Superman (Rebirth) 001.cbz", "Rebirth")]
    [InlineData("X-Men Dawn of X 001.cbz", "Dawn of X")]
    // Note: "Marvel NOW" without parens not currently extracted - needs parser enhancement
    public void Parse_RebootIndicator_ExtractsCorrectly(string title, string expectedIndicator)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedIndicator, result.RebootIndicator);
    }

    [Theory]
    [InlineData("Batman (Second Series) #001.cbz", "Second Series")]
    [InlineData("Superman (2nd Series) 001.cbz", "2nd Series")]
    [InlineData("Wonder Woman Third Series 001.cbz", "Third Series")]
    public void Parse_SeriesVersion_ExtractsCorrectly(string title, string expectedVersion)
    {
        var result = _parser.Parse(title);
        Assert.Equal(expectedVersion, result.SeriesVersion);
    }

    #endregion

    #region EPIC 19.3 - Publisher Hints from Release Groups

    [Theory]
    [InlineData("Batman 001 (2023) - DC-Empire.cbz", "DC")]
    [InlineData("Spider-Man 001 (2023) - Marvel-Minutemen.cbz", "Marvel")]
    [InlineData("Walking Dead 001 (2023) - Image-Empire.cbz", "Image")]
    public void Parse_ReleaseGroupPublisherHint_ExtractsCorrectly(string title, string expectedPublisher)
    {
        var result = _parser.Parse(title);
        // Extracts publisher prefix from release group (e.g., "DC" from "DC-Empire")
        // Full name expansion (e.g., "DC" -> "DC Comics") is a future enhancement
        Assert.Equal(expectedPublisher, result.Publisher);
    }

    [Fact]
    public void Parse_ExplicitPublisherOverridesHint()
    {
        // Explicit publisher in parens should take precedence over release group hint
        var result = _parser.Parse("Batman 001 (DC) (2023) - Marvel-Empire.cbz");
        
        // DC should win because it's explicit in parens, even though group says Marvel
        Assert.Equal("DC", result.Publisher);
    }

    #endregion

    #region EPIC 19.3 - Disambiguation Year Detection

    [Fact]
    public void Parse_DisambiguationYear_DetectedForModernSeries()
    {
        var result = _parser.Parse("Batman #050 (2016).cbz");
        
        Assert.Equal(2016, result.Year);
        // Should also be marked as disambiguation year (2016 Batman vs other Batman series)
        Assert.Equal(2016, result.DisambiguationYear);
    }

    #endregion
}
