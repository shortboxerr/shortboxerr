using System.Text.Json;
using Microsoft.Extensions.Options;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden tests that run against fixture files to verify Mylar3 parity.
/// These tests ensure consistent behavior across versions.
/// </summary>
public class DecisionEngineGoldenTests
{
    private readonly string _fixturesPath;
    
    public DecisionEngineGoldenTests()
    {
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        
        // Copy fixtures to output if they don't exist (for test discovery)
        var sourceFixtures = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Fixtures");
        if (Directory.Exists(sourceFixtures) && !Directory.Exists(_fixturesPath))
        {
            Directory.CreateDirectory(_fixturesPath);
            foreach (var file in Directory.GetFiles(sourceFixtures, "*.json"))
            {
                File.Copy(file, Path.Combine(_fixturesPath, Path.GetFileName(file)), true);
            }
        }
    }

    [Fact]
    public void GoldenTests_AllFixturesPass()
    {
        var fixturePath = Path.Combine(_fixturesPath, "decision_engine_golden.json");
        if (!File.Exists(fixturePath))
        {
            // Try alternative path during test execution
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Fixtures", "decision_engine_golden.json");
        }
        
        Assert.True(File.Exists(fixturePath), $"Golden fixture file not found at {fixturePath}");
        
        var json = File.ReadAllText(fixturePath);
        var fixture = JsonSerializer.Deserialize<GoldenFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(fixture);
        Assert.NotNull(fixture.TestCases);
        Assert.NotEmpty(fixture.TestCases);
        
        var settings = MapSettings(fixture.Settings);
        var engine = new DecisionEngine(Options.Create(settings));
        
        var failures = new List<string>();
        
        foreach (var testCase in fixture.TestCases)
        {
            try
            {
                RunTestCase(engine, testCase);
            }
            catch (Exception ex)
            {
                failures.Add($"[{testCase.Id}] {testCase.Description}: {ex.Message}");
            }
        }
        
        if (failures.Count > 0)
        {
            Assert.Fail($"Golden test failures:\n{string.Join("\n", failures)}");
        }
    }

    [Fact]
    public void GoldenTests_SingleExactMatch()
    {
        RunSingleGoldenTest("single-exact-match");
    }

    [Fact]
    public void GoldenTests_FormatPreference()
    {
        RunSingleGoldenTest("format-preference");
    }

    [Fact]
    public void GoldenTests_BannedWordRejection()
    {
        RunSingleGoldenTest("banned-word-rejection");
    }

    [Fact]
    public void GoldenTests_SizeTooSmall()
    {
        RunSingleGoldenTest("size-too-small");
    }

    [Fact]
    public void GoldenTests_SourcePriority()
    {
        RunSingleGoldenTest("source-priority");
    }

    [Fact]
    public void GoldenTests_CollectionTpb()
    {
        RunSingleGoldenTest("collection-tpb");
    }

    [Fact]
    public void GoldenTests_DeterministicTiebreak()
    {
        RunSingleGoldenTest("deterministic-tiebreak");
    }

    [Fact]
    public void GoldenTests_ManualReviewMargin()
    {
        RunSingleGoldenTest("manual-review-margin");
    }

    private void RunSingleGoldenTest(string testCaseId)
    {
        var (fixture, testCase) = LoadTestCase(testCaseId);
        var settings = MapSettings(fixture.Settings);
        var engine = new DecisionEngine(Options.Create(settings));
        
        RunTestCase(engine, testCase);
    }

    private (GoldenFixture fixture, GoldenTestCase testCase) LoadTestCase(string id)
    {
        var fixturePath = Path.Combine(_fixturesPath, "decision_engine_golden.json");
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Fixtures", "decision_engine_golden.json");
        }
        
        var json = File.ReadAllText(fixturePath);
        var fixture = JsonSerializer.Deserialize<GoldenFixture>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
        
        var testCase = fixture.TestCases.FirstOrDefault(t => t.Id == id);
        Assert.NotNull(testCase);
        
        return (fixture, testCase);
    }

    private void RunTestCase(IDecisionEngine engine, GoldenTestCase testCase)
    {
        var target = MapTarget(testCase.Target);
        var candidates = testCase.Candidates.Select((c, i) => MapCandidate(c, i)).ToList();
        
        var ranked = engine.EvaluateAndRank(candidates, target);
        
        // Check acceptance
        if (testCase.ExpectedAccepted.HasValue && testCase.Candidates.Count == 1)
        {
            var result = ranked.First();
            Assert.Equal(testCase.ExpectedAccepted.Value, result.Accepted);
            
            if (testCase.ExpectedRejectionReason != null && !result.Accepted)
            {
                Assert.Equal(testCase.ExpectedRejectionReason, result.RejectionReason?.ToString());
            }
        }
        
        // Check best candidate
        if (testCase.ExpectedBestId.HasValue)
        {
            var best = ranked.FirstOrDefault(r => r.Accepted);
            Assert.NotNull(best);
            
            var expectedCandidate = testCase.Candidates[testCase.ExpectedBestId.Value];
            Assert.Equal(expectedCandidate.ReleaseTitle, best.Candidate.ReleaseTitle);
            Assert.Equal(expectedCandidate.Source, best.Candidate.Source);
        }
        
        // Check auto-grab
        if (testCase.ShouldAutoGrab.HasValue)
        {
            var (shouldGrab, _) = engine.CheckAutoGrab(ranked);
            Assert.Equal(testCase.ShouldAutoGrab.Value, shouldGrab);
        }
    }

    private static DecisionEngineSettings MapSettings(GoldenSettings? settings)
    {
        if (settings == null)
        {
            return new DecisionEngineSettings();
        }
        
        return new DecisionEngineSettings
        {
            AutoGrabEnabled = settings.AutoGrabEnabled,
            AutoGrabThreshold = settings.AutoGrabThreshold,
            ManualChoiceMargin = settings.ManualChoiceMargin,
            FormatPreferenceOrder = settings.FormatPreferenceOrder ?? new List<string> { "cbz", "cbr" },
            BannedWords = settings.BannedWords ?? new List<string> { "sample", "preview" },
            RequiredWords = settings.RequiredWords ?? new List<string>(),
            MinSizeBytesSingles = settings.MinSizeBytesSingles,
            MaxSizeBytesSingles = settings.MaxSizeBytesSingles,
            MinSizeBytesCollections = settings.MinSizeBytesCollections,
            MaxSizeBytesCollections = settings.MaxSizeBytesCollections,
            SourcePriority = settings.SourcePriority ?? new List<string>()
        };
    }

    private static CandidateTarget MapTarget(GoldenTarget target) => new()
    {
        SeriesTitle = target.SeriesTitle,
        IssueNumber = target.IssueNumber,
        VolumeNumber = target.VolumeNumber,
        Year = target.Year,
        IsCollection = target.IsCollection,
        EditionTitle = target.EditionTitle
    };

    private static Candidate MapCandidate(GoldenCandidate c, int index) => new()
    {
        Id = $"candidate-{index}",
        ReleaseTitle = c.ReleaseTitle,
        Source = c.Source,
        SeriesTitle = c.SeriesTitle,
        IssueNumber = c.IssueNumber,
        VolumeNumber = c.VolumeNumber,
        Year = c.Year,
        Format = c.Format,
        Size = c.Size,
        IsCollection = c.IsCollection,
        EditionType = c.EditionType
    };

    // DTO classes for JSON deserialization
    private class GoldenFixture
    {
        public string? Description { get; set; }
        public string? Version { get; set; }
        public List<GoldenTestCase> TestCases { get; set; } = new();
        public GoldenSettings? Settings { get; set; }
    }

    private class GoldenTestCase
    {
        public required string Id { get; set; }
        public required string Description { get; set; }
        public required GoldenTarget Target { get; set; }
        public List<GoldenCandidate> Candidates { get; set; } = new();
        public int? ExpectedBestId { get; set; }
        public bool? ExpectedAccepted { get; set; }
        public bool? ShouldAutoGrab { get; set; }
        public string? ExpectedRejectionReason { get; set; }
        public string? Note { get; set; }
    }

    private class GoldenTarget
    {
        public required string SeriesTitle { get; set; }
        public decimal? IssueNumber { get; set; }
        public int? VolumeNumber { get; set; }
        public int? Year { get; set; }
        public bool IsCollection { get; set; }
        public string? EditionTitle { get; set; }
    }

    private class GoldenCandidate
    {
        public required string ReleaseTitle { get; set; }
        public required string Source { get; set; }
        public string? SeriesTitle { get; set; }
        public decimal? IssueNumber { get; set; }
        public int? VolumeNumber { get; set; }
        public int? Year { get; set; }
        public string? Format { get; set; }
        public long? Size { get; set; }
        public bool IsCollection { get; set; }
        public string? EditionType { get; set; }
    }

    private class GoldenSettings
    {
        public bool AutoGrabEnabled { get; set; } = true;
        public int AutoGrabThreshold { get; set; } = 80;
        public int ManualChoiceMargin { get; set; } = 10;
        public List<string>? FormatPreferenceOrder { get; set; }
        public List<string>? BannedWords { get; set; }
        public List<string>? RequiredWords { get; set; }
        public long MinSizeBytesSingles { get; set; } = 1_000_000;
        public long MaxSizeBytesSingles { get; set; } = 200_000_000;
        public long MinSizeBytesCollections { get; set; } = 5_000_000;
        public long MaxSizeBytesCollections { get; set; } = 2_000_000_000;
        public List<string>? SourcePriority { get; set; }
    }
}

