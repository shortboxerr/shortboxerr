using System.Text.Json;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden tests for DDL candidate filtering.
/// These tests ensure Mylar3 parity for filtering rules.
/// </summary>
public class DdlFilteringGoldenTests
{
    private readonly IDdlFilter _filter = new DdlFilter();
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "ddl_filtering_golden.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static FilteringGoldenFixture LoadFixture()
    {
        var json = File.ReadAllText(FixturePath);
        return JsonSerializer.Deserialize<FilteringGoldenFixture>(json, JsonOptions) 
            ?? throw new InvalidOperationException("Failed to load fixture");
    }

    public static IEnumerable<object[]> GoldenTestCases()
    {
        var fixture = LoadFixture();
        
        foreach (var testCase in fixture.TestCases ?? Array.Empty<FilteringTestCase>())
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(GoldenTestCases))]
    public void Filter_GoldenFixture(FilteringTestCase testCase)
    {
        var fixture = LoadFixture();
        var settings = BuildSettings(fixture.DefaultSettings);
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = testCase.Title,
            SourceSite = "GoldenTest",
            Size = testCase.Size,
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman", // Default
                Format = testCase.Format,
                IsCollection = testCase.IsCollection,
                Confidence = 50
            }
        };
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.Equal(testCase.ExpectedPasses, passes);
        
        if (!testCase.ExpectedPasses && !string.IsNullOrEmpty(testCase.ExpectedReasonContains))
        {
            Assert.NotNull(reason);
            Assert.Contains(testCase.ExpectedReasonContains, reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static IEnumerable<object[]> RequiredWordsTestCases()
    {
        var fixture = LoadFixture();
        
        foreach (var testCase in fixture.RequiredWordsTestCases ?? Array.Empty<RequiredWordsTestCase>())
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(RequiredWordsTestCases))]
    public void Filter_RequiredWords_GoldenFixture(RequiredWordsTestCase testCase)
    {
        var fixture = LoadFixture();
        var settings = BuildSettings(fixture.DefaultSettings);
        settings.RequiredWords = testCase.RequiredWords?.ToList() ?? new List<string>();
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = testCase.Title,
            SourceSite = "GoldenTest",
            Size = testCase.Size,
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                Format = testCase.Format,
                IsCollection = testCase.IsCollection,
                Confidence = 50
            }
        };
        
        var (passes, reason) = _filter.CheckCandidate(candidate, settings);
        
        Assert.Equal(testCase.ExpectedPasses, passes);
        
        if (!testCase.ExpectedPasses && !string.IsNullOrEmpty(testCase.ExpectedReasonContains))
        {
            Assert.NotNull(reason);
            Assert.Contains(testCase.ExpectedReasonContains, reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DdlFilterSettings BuildSettings(DefaultSettings? defaults)
    {
        if (defaults == null)
        {
            return new DdlFilterSettings();
        }
        
        return new DdlFilterSettings
        {
            BannedWords = defaults.BannedWords?.ToList() ?? new List<string> { "sample", "preview" },
            MinSizeBytesSingles = defaults.MinSizeSingles,
            MaxSizeBytesSingles = defaults.MaxSizeSingles,
            MinSizeBytesCollections = defaults.MinSizeCollections,
            MaxSizeBytesCollections = defaults.MaxSizeCollections,
            BlockedFormats = defaults.BlockedFormats?.ToList() ?? new List<string> { "pdf" }
        };
    }

    #region Fixture Models

    public class FilteringGoldenFixture
    {
        public string? Description { get; set; }
        public DefaultSettings? DefaultSettings { get; set; }
        public FilteringTestCase[] TestCases { get; set; } = Array.Empty<FilteringTestCase>();
        public RequiredWordsTestCase[] RequiredWordsTestCases { get; set; } = Array.Empty<RequiredWordsTestCase>();
    }

    public class DefaultSettings
    {
        public string[]? BannedWords { get; set; }
        public long MinSizeSingles { get; set; } = 1_000_000;
        public long MaxSizeSingles { get; set; } = 200_000_000;
        public long MinSizeCollections { get; set; } = 5_000_000;
        public long MaxSizeCollections { get; set; } = 2_000_000_000;
        public string[]? BlockedFormats { get; set; }
    }

    public class FilteringTestCase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long? Size { get; set; }
        public bool IsCollection { get; set; }
        public string Format { get; set; } = "cbz";
        public bool ExpectedPasses { get; set; }
        public string? ExpectedReasonContains { get; set; }
        
        public override string ToString() => $"[{Id}] {Title}";
    }

    public class RequiredWordsTestCase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long? Size { get; set; } = 15_000_000;
        public bool IsCollection { get; set; }
        public string Format { get; set; } = "cbz";
        public string[]? RequiredWords { get; set; }
        public bool ExpectedPasses { get; set; }
        public string? ExpectedReasonContains { get; set; }
        
        public override string ToString() => $"[{Id}] {Title}";
    }

    #endregion
}

