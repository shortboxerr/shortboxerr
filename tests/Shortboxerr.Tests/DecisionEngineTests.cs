using Microsoft.Extensions.Options;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Tests;

/// <summary>
/// Golden test harness for DecisionEngine.
/// Tests verify Mylar3-compatible candidate selection logic.
/// </summary>
public class DecisionEngineTests
{
    private readonly IDecisionEngine _engine;
    private readonly DecisionEngineSettings _settings;

    public DecisionEngineTests()
    {
        _settings = new DecisionEngineSettings
        {
            AutoGrabEnabled = true,
            AutoGrabThreshold = 80,
            ManualChoiceMargin = 10,
            FormatPreferenceOrder = new List<string> { "cbz", "cbr" },
            BannedWords = new List<string> { "sample", "preview" },
            RequiredWords = new List<string>(),
            MinSizeBytesSingles = 1_000_000,
            MaxSizeBytesSingles = 200_000_000,
            MinSizeBytesCollections = 5_000_000,
            MaxSizeBytesCollections = 2_000_000_000,
            SourcePriority = new List<string> { "preferred-source", "secondary-source" }
        };
        
        _engine = new DecisionEngine(Options.Create(_settings));
    }

    #region Rejection Tests

    [Fact]
    public void Evaluate_WithBannedWord_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001 (sample).cbz");
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.BannedWordFound, result.RejectionReason);
        Assert.Contains("sample", result.Explanation.Summary.ToLowerInvariant());
    }

    [Fact]
    public void Evaluate_WithPreviewWord_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001 preview.cbz");
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.BannedWordFound, result.RejectionReason);
    }

    [Fact]
    public void Evaluate_MissingRequiredWord_RejectsCandidate()
    {
        // Arrange - Configure required word
        var settings = CreateSettings();
        settings.RequiredWords = new List<string> { "Digital" };
        var engine = new DecisionEngine(Options.Create(settings));

        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz");
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.MissingRequiredWord, result.RejectionReason);
    }

    [Fact]
    public void Evaluate_WithRequiredWord_AcceptsCandidate()
    {
        // Arrange
        var settings = CreateSettings();
        settings.RequiredWords = new List<string> { "Digital" };
        var engine = new DecisionEngine(Options.Create(settings));

        var candidate = CreateCandidate("Amazing Spider-Man #001 (Digital).cbz");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = engine.Evaluate(candidate, target);

        // Assert
        Assert.True(result.Accepted);
    }

    [Fact]
    public void Evaluate_FileTooSmall_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz");
        candidate.Size = 500_000; // 500KB - below 1MB minimum
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.TooSmall, result.RejectionReason);
    }

    [Fact]
    public void Evaluate_FileTooLarge_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz");
        candidate.Size = 300_000_000; // 300MB - above 200MB max
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.TooLarge, result.RejectionReason);
    }

    [Fact]
    public void Evaluate_CollectionTooSmall_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Spider-Man Vol. 1 TPB.cbz");
        candidate.IsCollection = true;
        candidate.Size = 1_000_000; // 1MB - below 5MB minimum for collections
        var target = CreateTarget("Spider-Man", null, isCollection: true);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.TooSmall, result.RejectionReason);
    }

    [Fact]
    public void Evaluate_UnsupportedFormat_RejectsCandidate()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.pdf");
        candidate.Format = "pdf";
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.UnsupportedFormat, result.RejectionReason);
    }

    #endregion

    #region Scoring Tests

    [Fact]
    public void Evaluate_PreferredFormat_GetsMaxFormatPoints()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Format = "cbz";
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.True(result.Accepted);
        var formatFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "Format");
        Assert.NotNull(formatFactor);
        Assert.Equal(_settings.FormatMatchPoints, formatFactor.Points);
    }

    [Fact]
    public void Evaluate_SecondaryFormat_GetsPenalizedPoints()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbr");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Format = "cbr";
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.True(result.Accepted);
        var formatFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "Format");
        Assert.NotNull(formatFactor);
        Assert.True(formatFactor.Points < _settings.FormatMatchPoints);
    }

    [Fact]
    public void Evaluate_ExactSeriesMatch_GetsMaxSeriesPoints()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var seriesFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "SeriesMatch");
        Assert.NotNull(seriesFactor);
        Assert.Equal(_settings.ExactSeriesMatchPoints, seriesFactor.Points);
    }

    [Fact]
    public void Evaluate_PartialSeriesMatch_GetsPartialPoints()
    {
        // Arrange
        var candidate = CreateCandidate("Spider-Man #001.cbz");
        candidate.SeriesTitle = "Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1); // Target contains candidate

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var seriesFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "SeriesMatch");
        Assert.NotNull(seriesFactor);
        Assert.Equal(_settings.PartialSeriesMatchPoints, seriesFactor.Points);
    }

    [Fact]
    public void Evaluate_ExactIssueMatch_GetsMaxIssuePoints()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #042.cbz");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 42;
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 42);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var issueFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "IssueMatch");
        Assert.NotNull(issueFactor);
        Assert.Equal(_settings.ExactIssueMatchPoints, issueFactor.Points);
    }

    [Fact]
    public void Evaluate_YearMatch_GetsYearPoints()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001 (2023).cbz");
        candidate.SeriesTitle = "Amazing Spider-Man";
        candidate.IssueNumber = 1;
        candidate.Year = 2023;
        candidate.Size = 10_000_000;
        var target = CreateTarget("Amazing Spider-Man", 1, year: 2023);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var yearFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "YearMatch");
        Assert.NotNull(yearFactor);
        Assert.Equal(_settings.YearMatchPoints, yearFactor.Points);
    }

    [Fact]
    public void Evaluate_TopPrioritySource_NoPenalty()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz", 10_000_000, "Amazing Spider-Man", 1, "cbz", "preferred-source");
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var sourceFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "SourcePriority");
        Assert.NotNull(sourceFactor);
        Assert.Equal(0, sourceFactor.Points); // No penalty for top priority
    }

    [Fact]
    public void Evaluate_LowerPrioritySource_GetsPenalty()
    {
        // Arrange
        var candidate = CreateCandidate("Amazing Spider-Man #001.cbz", 10_000_000, "Amazing Spider-Man", 1, "cbz", "secondary-source");
        var target = CreateTarget("Amazing Spider-Man", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        var sourceFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "SourcePriority");
        Assert.NotNull(sourceFactor);
        Assert.True(sourceFactor.Points < 0); // Penalty for lower priority
    }

    #endregion

    #region Ranking Tests

    [Fact]
    public void EvaluateAndRank_SortsAcceptedFirst()
    {
        // Arrange
        var candidates = new[]
        {
            CreateCandidate("Good.cbz", 10_000_000, "Test", 1),
            CreateCandidate("Bad (sample).cbz", 10_000_000, "Test", 1) // Will be rejected
        };
        var target = CreateTarget("Test", 1);

        // Act
        var ranked = _engine.EvaluateAndRank(candidates, target);

        // Assert
        Assert.True(ranked[0].Accepted);
        Assert.False(ranked[1].Accepted);
    }

    [Fact]
    public void EvaluateAndRank_SortsByScoreDescending()
    {
        // Arrange - Create two candidates with different formats
        var candidates = new[]
        {
            CreateCandidate("Test #001.cbr", 10_000_000, "Test", 1, "cbr"), // Lower score
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz")  // Higher score
        };
        var target = CreateTarget("Test", 1);

        // Act
        var ranked = _engine.EvaluateAndRank(candidates, target);

        // Assert
        Assert.True(ranked[0].Score > ranked[1].Score);
        Assert.Equal("cbz", ranked[0].Candidate.Format);
    }

    [Fact]
    public void EvaluateAndRank_DeterministicTieBreak()
    {
        // Arrange - Create identical candidates with different sources
        var candidates = new[]
        {
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz", "zzz-source"),
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz", "aaa-source")
        };
        var target = CreateTarget("Test", 1);

        // Act
        var ranked = _engine.EvaluateAndRank(candidates, target);

        // Assert - Should sort alphabetically by source for deterministic tie-break
        Assert.Equal("aaa-source", ranked[0].Candidate.Source);
        Assert.Equal("zzz-source", ranked[1].Candidate.Source);
    }

    [Fact]
    public void GetBestCandidate_ReturnsTopAccepted()
    {
        // Arrange
        var candidates = new[]
        {
            CreateCandidate("Test #001.cbr", 10_000_000, "Test", 1, "cbr"),
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz")
        };
        var target = CreateTarget("Test", 1);

        // Act
        var best = _engine.GetBestCandidate(candidates, target);

        // Assert
        Assert.NotNull(best);
        Assert.Equal("cbz", best.Candidate.Format);
    }

    [Fact]
    public void GetBestCandidate_ReturnsNullWhenNoneAccepted()
    {
        // Arrange
        var candidates = new[]
        {
            CreateCandidate("Test (sample).cbz", 10_000_000, "Test", 1),
            CreateCandidate("Test (preview).cbz", 10_000_000, "Test", 1)
        };
        var target = CreateTarget("Test", 1);

        // Act
        var best = _engine.GetBestCandidate(candidates, target);

        // Assert
        Assert.Null(best);
    }

    #endregion

    #region Auto-Grab Tests

    [Fact]
    public void CheckAutoGrab_Disabled_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();
        settings.AutoGrabEnabled = false;
        var engine = new DecisionEngine(Options.Create(settings));

        var candidates = new[] { CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1) };
        var target = CreateTarget("Test", 1);
        var ranked = engine.EvaluateAndRank(candidates, target);

        // Act
        var (shouldGrab, reason) = engine.CheckAutoGrab(ranked);

        // Assert
        Assert.False(shouldGrab);
        Assert.Contains("disabled", reason.ToLowerInvariant());
    }

    [Fact]
    public void CheckAutoGrab_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        var settings = CreateSettings();
        settings.AutoGrabThreshold = 1000; // Very high threshold
        var engine = new DecisionEngine(Options.Create(settings));

        var candidates = new[] { CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1) };
        var target = CreateTarget("Test", 1);
        var ranked = engine.EvaluateAndRank(candidates, target);

        // Act
        var (shouldGrab, reason) = engine.CheckAutoGrab(ranked);

        // Assert
        Assert.False(shouldGrab);
        Assert.Contains("threshold", reason.ToLowerInvariant());
    }

    [Fact]
    public void CheckAutoGrab_MultipleWithinMargin_ReturnsFalse()
    {
        // Arrange - Two candidates with same format/score (both high scoring)
        // Use a settings with a lower threshold so candidates pass
        var settings = CreateSettings();
        settings.AutoGrabThreshold = 50; // Lower threshold so candidates pass
        var engine = new DecisionEngine(Options.Create(settings));
        
        var candidates = new[]
        {
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz", "source-a"),
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz", "source-b")
        };
        var target = CreateTarget("Test", 1);
        var ranked = engine.EvaluateAndRank(candidates, target);

        // Act
        var (shouldGrab, reason) = engine.CheckAutoGrab(ranked);

        // Assert
        Assert.False(shouldGrab);
        Assert.Contains("margin", reason.ToLowerInvariant());
    }

    [Fact]
    public void CheckAutoGrab_ClearWinner_ReturnsTrue()
    {
        // Arrange - Use lower threshold and one clear winner
        var settings = CreateSettings();
        settings.AutoGrabThreshold = 50; // Lower threshold
        var engine = new DecisionEngine(Options.Create(settings));
        
        var candidates = new[]
        {
            CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1, "cbz"),
            CreateCandidate("Test #001.cbr", 10_000_000, "Different Series", 1, "cbr")
        };
        var target = CreateTarget("Test", 1);
        var ranked = engine.EvaluateAndRank(candidates, target);

        // Act
        var (shouldGrab, reason) = engine.CheckAutoGrab(ranked);

        // Assert
        Assert.True(shouldGrab);
        Assert.Contains("approved", reason.ToLowerInvariant());
    }

    #endregion

    #region Explanation Tests

    [Fact]
    public void Evaluate_ExplanationContainsAllChecks()
    {
        // Arrange
        var candidate = CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1);
        var target = CreateTarget("Test", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.NotEmpty(result.Explanation.Checks);
        Assert.All(result.Explanation.Checks, c => Assert.NotEmpty(c.CheckName));
        Assert.All(result.Explanation.Checks, c => Assert.NotEmpty(c.Details));
    }

    [Fact]
    public void Evaluate_ExplanationHasSummary()
    {
        // Arrange
        var candidate = CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1);
        var target = CreateTarget("Test", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.NotEmpty(result.Explanation.Summary);
    }

    [Fact]
    public void Evaluate_ExplanationTracksScoring()
    {
        // Arrange
        var candidate = CreateCandidate("Test #001.cbz", 10_000_000, "Test", 1);
        var target = CreateTarget("Test", 1);

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.True(result.Accepted);
        Assert.True(result.Explanation.FinalScore > 0);
        Assert.Equal(
            result.Explanation.BaseScore - result.Explanation.Penalties,
            result.Explanation.FinalScore);
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void Evaluate_Collection_ScoredCorrectly()
    {
        // Arrange
        var candidate = CreateCandidate("Spider-Man Vol. 1 TPB.cbz");
        candidate.IsCollection = true;
        candidate.EditionType = "TPB";
        candidate.VolumeNumber = 1;
        candidate.SeriesTitle = "Spider-Man";
        candidate.Size = 50_000_000;
        var target = CreateTarget("Spider-Man", null, isCollection: true, editionTitle: "Vol. 1");

        // Act
        var result = _engine.Evaluate(candidate, target);

        // Assert
        Assert.True(result.Accepted);
        var editionFactor = result.Explanation.ScoringFactors
            .FirstOrDefault(f => f.Name == "EditionMatch");
        Assert.NotNull(editionFactor);
        Assert.True(editionFactor.Points > 0);
    }

    #endregion

    #region Helpers

    private static DecisionEngineSettings CreateSettings() => new()
    {
        AutoGrabEnabled = true,
        AutoGrabThreshold = 80,
        ManualChoiceMargin = 10,
        FormatPreferenceOrder = new List<string> { "cbz", "cbr" },
        BannedWords = new List<string> { "sample", "preview" },
        RequiredWords = new List<string>(),
        MinSizeBytesSingles = 1_000_000,
        MaxSizeBytesSingles = 200_000_000,
        MinSizeBytesCollections = 5_000_000,
        MaxSizeBytesCollections = 2_000_000_000,
        SourcePriority = new List<string> { "preferred-source", "secondary-source" }
    };

    private static Candidate CreateCandidate(string title, long size = 0, string? series = null, decimal? issue = null, string format = "cbz", string source = "test-source")
    {
        return new Candidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = title,
            Source = source,
            SeriesTitle = series,
            IssueNumber = issue,
            Format = format,
            Size = size > 0 ? size : null
        };
    }

    private static CandidateTarget CreateTarget(string series, decimal? issue, bool isCollection = false, string? editionTitle = null, int? year = null)
    {
        return new CandidateTarget
        {
            SeriesTitle = series,
            IssueNumber = issue,
            IsCollection = isCollection,
            EditionTitle = editionTitle,
            Year = year
        };
    }

    #endregion
}

