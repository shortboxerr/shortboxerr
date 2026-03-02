using Shortboxerr.Core.ComicVine;

namespace Shortboxerr.Tests;

public class ComicVineIdParserTests
{
    #region TryParse Tests

    [Theory]
    [InlineData("4050-12345", ComicVineResourceType.Volume, 12345, "4050-12345")]
    [InlineData("4050-1", ComicVineResourceType.Volume, 1, "4050-1")]
    [InlineData("4050-999999", ComicVineResourceType.Volume, 999999, "4050-999999")]
    [InlineData("4000-123456", ComicVineResourceType.Issue, 123456, "4000-123456")]
    [InlineData("4000-1", ComicVineResourceType.Issue, 1, "4000-1")]
    [InlineData("4045-98765", ComicVineResourceType.StoryArc, 98765, "4045-98765")]
    [InlineData("4005-54321", ComicVineResourceType.Character, 54321, "4005-54321")]
    [InlineData("4010-11111", ComicVineResourceType.Publisher, 11111, "4010-11111")]
    public void TryParse_PrefixedId_ParsesCorrectly(
        string input, ComicVineResourceType expectedType, int expectedId, string expectedFullId)
    {
        var result = ComicVineIdParser.TryParse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedId, result.NumericId);
        Assert.Equal(expectedFullId, result.FullId);
    }

    [Theory]
    [InlineData("12345", 12345)]
    [InlineData("1", 1)]
    [InlineData("999999", 999999)]
    public void TryParse_PlainNumeric_ReturnsUnknownType(string input, int expectedId)
    {
        var result = ComicVineIdParser.TryParse(input);

        Assert.NotNull(result);
        Assert.Equal(ComicVineResourceType.Unknown, result.Type);
        Assert.Equal(expectedId, result.NumericId);
        Assert.Equal(input, result.FullId);
    }

    [Theory]
    [InlineData("https://comicvine.gamespot.com/batman/4050-796/", ComicVineResourceType.Volume, 796)]
    [InlineData("https://comicvine.gamespot.com/batman-1/4000-6227/", ComicVineResourceType.Issue, 6227)]
    [InlineData("comicvine.gamespot.com/some-arc/4045-12345", ComicVineResourceType.StoryArc, 12345)]
    public void TryParse_ComicVineUrl_ExtractsId(string input, ComicVineResourceType expectedType, int expectedId)
    {
        var result = ComicVineIdParser.TryParse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedId, result.NumericId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Batman")]
    [InlineData("Spider-Man 2099")]
    [InlineData("The Amazing Spider-Man")]
    [InlineData("4050-")] // Missing ID
    [InlineData("-12345")] // Missing prefix
    [InlineData("4050-abc")] // Non-numeric ID
    [InlineData("9999-12345")] // Unknown prefix
    public void TryParse_InvalidInput_ReturnsNull(string? input)
    {
        var result = ComicVineIdParser.TryParse(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("  4050-12345  ", ComicVineResourceType.Volume, 12345)]
    [InlineData("\t4000-99999\t", ComicVineResourceType.Issue, 99999)]
    public void TryParse_WithWhitespace_TrimsAndParses(string input, ComicVineResourceType expectedType, int expectedId)
    {
        var result = ComicVineIdParser.TryParse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedId, result.NumericId);
    }

    #endregion

    #region TryParseAs Tests

    [Theory]
    [InlineData("4050-12345", ComicVineResourceType.Volume, true)]
    [InlineData("4050-12345", ComicVineResourceType.Issue, false)] // Wrong type
    [InlineData("4000-12345", ComicVineResourceType.Issue, true)]
    [InlineData("4000-12345", ComicVineResourceType.Volume, false)] // Wrong type
    public void TryParseAs_SpecificType_ValidatesType(string input, ComicVineResourceType expectedType, bool shouldSucceed)
    {
        var result = ComicVineIdParser.TryParseAs(input, expectedType);

        if (shouldSucceed)
        {
            Assert.NotNull(result);
            Assert.Equal(expectedType, result.Type);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Theory]
    [InlineData("12345", ComicVineResourceType.Volume, "4050-12345")]
    [InlineData("99999", ComicVineResourceType.Issue, "4000-99999")]
    [InlineData("54321", ComicVineResourceType.StoryArc, "4045-54321")]
    public void TryParseAs_PlainNumeric_AssumesExpectedType(string input, ComicVineResourceType expectedType, string expectedFullId)
    {
        var result = ComicVineIdParser.TryParseAs(input, expectedType);

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(expectedFullId, result.FullId);
    }

    #endregion

    #region IsComicVineId Tests

    [Theory]
    [InlineData("4050-12345", true)]
    [InlineData("4000-999999", true)]
    [InlineData("12345", true)] // Plain numeric is considered an ID
    [InlineData("Batman", false)]
    [InlineData("Spider-Man 2099", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsComicVineId_DetectsCorrectly(string? input, bool expected)
    {
        Assert.Equal(expected, ComicVineIdParser.IsComicVineId(input));
    }

    #endregion

    #region Specific Type Check Tests

    [Theory]
    [InlineData("4050-12345", true)]
    [InlineData("4000-12345", false)]
    [InlineData("12345", false)] // Plain numeric doesn't match specific type
    public void IsVolumeId_DetectsCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, ComicVineIdParser.IsVolumeId(input));
    }

    [Theory]
    [InlineData("4000-12345", true)]
    [InlineData("4050-12345", false)]
    [InlineData("12345", false)]
    public void IsIssueId_DetectsCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, ComicVineIdParser.IsIssueId(input));
    }

    [Theory]
    [InlineData("4045-12345", true)]
    [InlineData("4050-12345", false)]
    [InlineData("12345", false)]
    public void IsStoryArcId_DetectsCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, ComicVineIdParser.IsStoryArcId(input));
    }

    #endregion
}
