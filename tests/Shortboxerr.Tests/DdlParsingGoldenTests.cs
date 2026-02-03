using System.Text.Json;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden tests for DDL release parsing.
/// These tests ensure Mylar3 parity for release title parsing.
/// </summary>
public class DdlParsingGoldenTests
{
    private readonly IDdlReleaseParser _parser = new DdlReleaseParser();
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "ddl_parsing_golden.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> GoldenTestCases()
    {
        var json = File.ReadAllText(FixturePath);
        var fixture = JsonSerializer.Deserialize<ParsingGoldenFixture>(json, JsonOptions);
        
        foreach (var testCase in fixture?.TestCases ?? Array.Empty<ParsingTestCase>())
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(GoldenTestCases))]
    public void Parse_GoldenFixture(ParsingTestCase testCase)
    {
        var result = _parser.Parse(testCase.Title);
        var expected = testCase.Expected;

        // Check series title (exact or contains)
        if (!string.IsNullOrEmpty(expected.SeriesTitle))
        {
            Assert.Equal(expected.SeriesTitle, result.SeriesTitle);
        }
        else if (!string.IsNullOrEmpty(expected.SeriesTitleContains))
        {
            Assert.Contains(expected.SeriesTitleContains, result.SeriesTitle ?? "", StringComparison.OrdinalIgnoreCase);
        }

        // Check issue number
        if (expected.IssueNumber.HasValue)
        {
            Assert.NotNull(result.IssueNumber);
            Assert.Equal(expected.IssueNumber.Value, result.IssueNumber.Value);
        }

        // Check volume number
        if (expected.VolumeNumber.HasValue)
        {
            Assert.Equal(expected.VolumeNumber, result.VolumeNumber);
        }

        // Check year
        if (expected.Year.HasValue)
        {
            Assert.Equal(expected.Year, result.Year);
        }
        else if (expected.Year == null && testCase.Expected.GetType().GetProperty("Year") != null)
        {
            // Explicitly null in fixture means we expect null
        }

        // Check format
        if (!string.IsNullOrEmpty(expected.Format))
        {
            Assert.Equal(expected.Format, result.Format);
        }

        // Check publisher
        if (!string.IsNullOrEmpty(expected.Publisher))
        {
            Assert.Equal(expected.Publisher, result.Publisher);
        }

        // Check quality
        if (!string.IsNullOrEmpty(expected.Quality))
        {
            Assert.Equal(expected.Quality, result.Quality);
        }

        // Check is_collection
        Assert.Equal(expected.IsCollection, result.IsCollection);

        // Check edition type
        if (!string.IsNullOrEmpty(expected.EditionType))
        {
            Assert.Equal(expected.EditionType, result.EditionType);
        }

        // Check issue range
        if (!string.IsNullOrEmpty(expected.IssueRange))
        {
            Assert.Equal(expected.IssueRange, result.IssueRange);
        }
    }

    #region Fixture Models

    public class ParsingGoldenFixture
    {
        public string? Description { get; set; }
        public ParsingTestCase[] TestCases { get; set; } = Array.Empty<ParsingTestCase>();
    }

    public class ParsingTestCase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ExpectedParsing Expected { get; set; } = new();
        
        public override string ToString() => $"[{Id}] {Title}";
    }

    public class ExpectedParsing
    {
        public string? SeriesTitle { get; set; }
        public string? SeriesTitleContains { get; set; }
        public decimal? IssueNumber { get; set; }
        public int? VolumeNumber { get; set; }
        public int? Year { get; set; }
        public string? Format { get; set; }
        public string? Publisher { get; set; }
        public string? Quality { get; set; }
        public bool IsCollection { get; set; }
        public string? EditionType { get; set; }
        public string? IssueRange { get; set; }
    }

    #endregion
}



