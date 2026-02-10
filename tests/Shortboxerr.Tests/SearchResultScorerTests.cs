using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Search;
using Shortboxerr.Infrastructure.Search;
using Xunit;

namespace Shortboxerr.Tests;

public class SearchResultScorerTests
{
    private readonly Mock<ISearchSettingsService> _settingsServiceMock;
    private readonly SearchSettings _defaultSettings;
    private readonly SearchResultScorer _scorer;
    private readonly SearchContext _defaultContext;

    public SearchResultScorerTests()
    {
        _settingsServiceMock = new Mock<ISearchSettingsService>();
        _defaultSettings = new SearchSettings();
        _settingsServiceMock.Setup(s => s.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_defaultSettings);

        _scorer = new SearchResultScorer(_settingsServiceMock.Object, null);

        _defaultContext = new SearchContext
        {
            TargetSeriesTitle = "Batman",
            TargetIssueNumber = 1,
            TargetYear = 2024
        };
    }

    private Candidate CreateCandidate(
        string title = "Batman #001 (2024) (Digital) (Zone-Empire)",
        string? seriesTitle = "Batman",
        decimal? issueNumber = 1,
        int? year = 2024,
        string? format = "cbz",
        long? size = 50 * 1024 * 1024, // 50 MB
        int sourcePriority = 1)
    {
        return new Candidate
        {
            Id = Guid.NewGuid().ToString(),
            ReleaseTitle = title,
            Source = "GetComics",
            SourcePriority = sourcePriority,
            SeriesTitle = seriesTitle,
            IssueNumber = issueNumber,
            Year = year,
            Format = format,
            Size = size
        };
    }

    #region Quality Scoring Tests

    [Fact]
    public void ScoreQuality_WithDigitalRelease_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Digital) (Zone-Empire)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.QualityWeight, result.Breakdown.Quality.Points);
        Assert.Contains("Digital", result.Breakdown.Quality.Reason);
    }

    [Fact]
    public void ScoreQuality_WithWebripRelease_ReturnsPartialScore()
    {
        // Arrange
        _defaultSettings.PreferredQuality = PreferredQuality.Digital;
        var candidate = CreateCandidate(title: "Batman #001 (Webrip)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.Quality.Points < _defaultSettings.ScoringWeights.QualityWeight);
        Assert.True(result.Breakdown.Quality.Points > 0);
    }

    [Fact]
    public void ScoreQuality_WithAnyQualityPreference_ReturnsMaxScore()
    {
        // Arrange
        _defaultSettings.PreferredQuality = PreferredQuality.Any;
        var candidate = CreateCandidate(title: "Batman #001 (Scan)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.QualityWeight, result.Breakdown.Quality.Points);
    }

    [Fact]
    public void ScoreQuality_DetectsMinutemenAsDigital()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Minutemen)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Contains("Digital", result.Breakdown.Quality.Reason);
    }

    [Fact]
    public void ScoreQuality_DetectsC2cAsScan()
    {
        // Arrange
        _defaultSettings.PreferredQuality = PreferredQuality.Scan;
        var candidate = CreateCandidate(title: "Batman #001 (c2c)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.QualityWeight, result.Breakdown.Quality.Points);
    }

    #endregion

    #region Size Scoring Tests

    [Fact]
    public void ScoreSize_WithinExpectedRange_ReturnsHighScore()
    {
        // Arrange
        var candidate = CreateCandidate(size: 75 * 1024 * 1024); // 75 MB ideal

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.Size.Points >= _defaultSettings.ScoringWeights.SizeWeight * 0.8);
    }

    [Fact]
    public void ScoreSize_BelowMinimum_ReturnsZero()
    {
        // Arrange
        _defaultSettings.MinSizeMb = 10;
        var candidate = CreateCandidate(size: 5 * 1024 * 1024); // 5 MB

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(0, result.Breakdown.Size.Points);
        Assert.Contains("Below minimum", result.Breakdown.Size.Reason);
    }

    [Fact]
    public void ScoreSize_AboveMaximum_ReturnsZero()
    {
        // Arrange
        _defaultSettings.MaxSizeMb = 100;
        var candidate = CreateCandidate(size: 200 * 1024 * 1024); // 200 MB

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(0, result.Breakdown.Size.Points);
        Assert.Contains("Above maximum", result.Breakdown.Size.Reason);
    }

    [Fact]
    public void ScoreSize_UnknownSize_ReturnsPartialScore()
    {
        // Arrange
        var candidate = CreateCandidate(size: null);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.SizeWeight / 2, result.Breakdown.Size.Points);
        Assert.Contains("unknown", result.Breakdown.Size.Reason);
    }

    #endregion

    #region Release Group Scoring Tests

    [Fact]
    public void ScoreReleaseGroup_TrustedGroup_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Digital) (Minutemen)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.ReleaseGroupWeight, result.Breakdown.ReleaseGroup.Points);
        Assert.Contains("Trusted", result.Breakdown.ReleaseGroup.Reason);
    }

    [Fact]
    public void ScoreReleaseGroup_UnknownGroup_ReturnsPartialScore()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Digital) (RandomGroup)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.ReleaseGroupWeight / 2, result.Breakdown.ReleaseGroup.Points);
        Assert.Contains("Unknown", result.Breakdown.ReleaseGroup.Reason);
    }

    [Fact]
    public void ScoreReleaseGroup_NoGroup_ReturnsMinimalScore()
    {
        // Arrange - title without any group-like content
        var candidate = CreateCandidate(title: "Batman #001 2024");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.ReleaseGroupWeight / 4, result.Breakdown.ReleaseGroup.Points);
        Assert.Contains("not detected", result.Breakdown.ReleaseGroup.Reason);
    }

    [Theory]
    [InlineData("Title (Zone-Empire)", "Zone-Empire")]
    [InlineData("Title (DCP)", "DCP")]
    [InlineData("Title-Empire", "Empire")]
    [InlineData("Title (2024) (Digital) (Nem)", "Nem")]
    public void ScoreReleaseGroup_ExtractsGroupCorrectly(string title, string expectedGroup)
    {
        // Arrange
        var candidate = CreateCandidate(title: title);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Contains(expectedGroup, result.Breakdown.ReleaseGroup.Reason, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Year Matching Tests

    [Fact]
    public void ScoreYearMatch_ExactMatch_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(year: 2024);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.YearMatchWeight, result.Breakdown.YearMatch.Points);
    }

    [Fact]
    public void ScoreYearMatch_OneYearOff_ReturnsHighScore()
    {
        // Arrange
        var candidate = CreateCandidate(year: 2023);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.YearMatch.Points >= _defaultSettings.ScoringWeights.YearMatchWeight * 0.7);
    }

    [Fact]
    public void ScoreYearMatch_NoYearInCandidate_ReturnsPartialScore()
    {
        // Arrange
        var candidate = CreateCandidate(year: null);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.YearMatchWeight / 2, result.Breakdown.YearMatch.Points);
    }

    [Fact]
    public void ScoreYearMatch_NoTargetYear_ReturnsMaxScore()
    {
        // Arrange
        var context = new SearchContext
        {
            TargetSeriesTitle = "Batman",
            TargetIssueNumber = 1,
            TargetYear = null
        };
        var candidate = CreateCandidate(year: 2020);

        // Act
        var result = _scorer.ScoreCandidate(candidate, context);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.YearMatchWeight, result.Breakdown.YearMatch.Points);
    }

    #endregion

    #region Issue Matching Tests

    [Fact]
    public void ScoreIssueMatch_ExactMatch_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(issueNumber: 1);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.IssueMatchWeight, result.Breakdown.IssueMatch.Points);
    }

    [Fact]
    public void ScoreIssueMatch_WrongIssue_ReturnsZero()
    {
        // Arrange
        var candidate = CreateCandidate(issueNumber: 5);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(0, result.Breakdown.IssueMatch.Points);
        Assert.Contains("mismatch", result.Breakdown.IssueMatch.Reason);
    }

    [Fact]
    public void ScoreIssueMatch_NoIssueNumber_ReturnsZeroForSingleSearch()
    {
        // Arrange
        var candidate = CreateCandidate(issueNumber: null);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(0, result.Breakdown.IssueMatch.Points);
    }

    [Fact]
    public void ScoreIssueMatch_CollectionForPackSearch_ReturnsMaxScore()
    {
        // Arrange
        var context = new SearchContext
        {
            TargetSeriesTitle = "Batman",
            TargetIssueNumber = 1,
            SearchingForPack = true
        };
        var candidate = CreateCandidate(issueNumber: null);
        candidate.IsCollection = true;

        // Act
        var result = _scorer.ScoreCandidate(candidate, context);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.IssueMatchWeight, result.Breakdown.IssueMatch.Points);
    }

    #endregion

    #region Series Matching Tests

    [Fact]
    public void ScoreSeriesMatch_ExactMatch_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(seriesTitle: "Batman");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.SeriesMatchWeight, result.Breakdown.SeriesMatch.Points);
    }

    [Fact]
    public void ScoreSeriesMatch_PartialMatch_ReturnsHighScore()
    {
        // Arrange
        var candidate = CreateCandidate(seriesTitle: "Batman: The Dark Knight");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.SeriesMatch.Points >= _defaultSettings.ScoringWeights.SeriesMatchWeight * 0.7);
    }

    [Fact]
    public void ScoreSeriesMatch_NoSeriesTitle_ReturnsMinimalScore()
    {
        // Arrange
        var candidate = CreateCandidate(seriesTitle: null);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.SeriesMatchWeight / 4, result.Breakdown.SeriesMatch.Points);
    }

    [Fact]
    public void ScoreSeriesMatch_IgnoresArticles()
    {
        // Arrange
        var context = new SearchContext
        {
            TargetSeriesTitle = "The Batman",
            TargetIssueNumber = 1
        };
        var candidate = CreateCandidate(seriesTitle: "Batman");

        // Act
        var result = _scorer.ScoreCandidate(candidate, context);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.SeriesMatchWeight, result.Breakdown.SeriesMatch.Points);
    }

    #endregion

    #region Format Scoring Tests

    [Fact]
    public void ScoreFormat_PreferredFormat_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(format: "cbz");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.FormatWeight, result.Breakdown.Format.Points);
    }

    [Fact]
    public void ScoreFormat_SecondPreference_ReturnsLessScore()
    {
        // Arrange
        var candidate = CreateCandidate(format: "cbr");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.Format.Points < _defaultSettings.ScoringWeights.FormatWeight);
        Assert.True(result.Breakdown.Format.Points > 0);
    }

    [Fact]
    public void ScoreFormat_CbzOnlyMode_RejectsOtherFormats()
    {
        // Arrange
        _defaultSettings.CbzOnly = true;
        var candidate = CreateCandidate(format: "cbr");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(0, result.Breakdown.Format.Points);
        Assert.Contains("not allowed", result.Breakdown.Format.Reason);
    }

    [Fact]
    public void ScoreFormat_UnknownFormat_ReturnsPartialScore()
    {
        // Arrange
        var candidate = CreateCandidate(format: null);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.FormatWeight / 2, result.Breakdown.Format.Points);
    }

    #endregion

    #region Source Priority Tests

    [Fact]
    public void ScoreSourcePriority_PriorityOne_ReturnsMaxScore()
    {
        // Arrange
        var candidate = CreateCandidate(sourcePriority: 1);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.SourcePriorityWeight, result.Breakdown.SourcePriority.Points);
    }

    [Fact]
    public void ScoreSourcePriority_LowerPriority_ReturnsLessScore()
    {
        // Arrange
        var candidate = CreateCandidate(sourcePriority: 5);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.SourcePriority.Points < _defaultSettings.ScoringWeights.SourcePriorityWeight);
    }

    #endregion

    #region Preferred/Blacklist Word Tests

    [Fact]
    public void ScorePreferredWords_WithPreferredWord_AddsBonus()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Digital) (HD)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.PreferredWords.Points > 0);
        Assert.Contains("digital", result.Breakdown.PreferredWords.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoreBlacklistWords_WithBlacklistedWord_AddsPenalty()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Sample)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.Breakdown.BlacklistPenalty.Points > 0);
        Assert.Contains("sample", result.Breakdown.BlacklistPenalty.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoreBlacklistWords_MultipleBlacklistedWords_AddsCumulativePenalty()
    {
        // Arrange
        var candidate = CreateCandidate(title: "Batman #001 (Sample) (Watermark)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(2 * _defaultSettings.ScoringWeights.BlacklistWordPenalty, 
            result.Breakdown.BlacklistPenalty.Points);
    }

    #endregion

    #region Integration/Sorting Tests

    [Fact]
    public void ScoreAndSort_ReturnsSortedByScore()
    {
        // Arrange
        var candidates = new[]
        {
            CreateCandidate(title: "Batman #001 (Digital) (Minutemen)", size: 75 * 1024 * 1024),
            CreateCandidate(title: "Batman #001 (Scan)", size: 10 * 1024 * 1024),
            CreateCandidate(title: "Batman #002 (Digital)", issueNumber: 2)
        };

        // Act
        var results = _scorer.ScoreAndSort(candidates, _defaultContext);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(results[0].TotalScore >= results[1].TotalScore);
        Assert.True(results[1].TotalScore >= results[2].TotalScore);
    }

    [Fact]
    public void GetBestCandidate_ReturnsHighestScoring()
    {
        // Arrange
        var candidates = new[]
        {
            CreateCandidate(title: "Batman #001 (Sample)", size: 10 * 1024 * 1024),
            CreateCandidate(title: "Batman #001 (Digital) (Minutemen)", size: 75 * 1024 * 1024),
        };

        // Act
        var best = _scorer.GetBestCandidate(candidates, _defaultContext);

        // Assert
        Assert.NotNull(best);
        Assert.Contains("Minutemen", best.Candidate.ReleaseTitle);
    }

    [Fact]
    public void ScoreBreakdown_CalculatesCorrectTotals()
    {
        // Arrange
        var candidate = CreateCandidate();

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(result.Breakdown.FinalScore, result.TotalScore);
        Assert.True(result.Breakdown.MaxPossible > 0);
        Assert.True(result.NormalizedScore >= 0 && result.NormalizedScore <= 100);
    }

    [Fact]
    public void MeetsThreshold_HighScore_ReturnsTrue()
    {
        // Arrange - perfect match
        var candidate = CreateCandidate(
            title: "Batman #001 (2024) (Digital) (Minutemen)",
            seriesTitle: "Batman",
            issueNumber: 1,
            year: 2024,
            format: "cbz",
            size: 75 * 1024 * 1024);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.True(result.MeetsThreshold);
    }

    [Fact]
    public void MeetsThreshold_VeryLowScore_ReturnsFalse()
    {
        // Arrange - terrible match with penalties
        var candidate = CreateCandidate(
            title: "Superman #099 (2010) (Sample) (Watermark) (Preview)",
            seriesTitle: "Superman",
            issueNumber: 99,
            year: 2010,
            format: "pdf",
            size: 1 * 1024 * 1024);

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.False(result.MeetsThreshold);
    }

    [Theory]
    [InlineData(90, "A")]
    [InlineData(85, "B")]
    [InlineData(75, "C")]
    [InlineData(65, "D")]
    [InlineData(50, "F")]
    public void Grade_ReturnsCorrectGrade(double normalizedScore, string expectedGrade)
    {
        // Arrange
        var candidate = CreateCandidate();
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);
        
        // Create a scored candidate with specific normalized score for testing
        var testScored = new ScoredCandidate
        {
            Candidate = candidate,
            TotalScore = 100,
            NormalizedScore = normalizedScore,
            Breakdown = result.Breakdown,
            MeetsThreshold = true
        };

        // Assert
        Assert.Equal(expectedGrade, testScored.Grade);
    }

    #endregion

    #region Custom Weight Tests

    [Fact]
    public void CustomWeights_AffectsScoring()
    {
        // Arrange
        _defaultSettings.ScoringWeights.QualityWeight = 500; // Very high weight
        _defaultSettings.ScoringWeights.SizeWeight = 1; // Very low weight
        
        var digitalCandidate = CreateCandidate(title: "Batman #001 (Digital)", size: 10 * 1024 * 1024);
        var scanCandidate = CreateCandidate(title: "Batman #001 (Scan)", size: 75 * 1024 * 1024);

        // Act
        var results = _scorer.ScoreAndSort(new[] { digitalCandidate, scanCandidate }, _defaultContext);

        // Assert - digital should win despite worse size due to quality weight
        Assert.Contains("Digital", results[0].Candidate.ReleaseTitle);
    }

    [Fact]
    public void TrustedReleaseGroups_CustomList_Works()
    {
        // Arrange
        _defaultSettings.TrustedReleaseGroups.Groups = new List<string> { "MyTrustedGroup" };
        var candidate = CreateCandidate(title: "Batman #001 (MyTrustedGroup)");

        // Act
        var result = _scorer.ScoreCandidate(candidate, _defaultContext);

        // Assert
        Assert.Equal(_defaultSettings.ScoringWeights.ReleaseGroupWeight, result.Breakdown.ReleaseGroup.Points);
    }

    #endregion
}

public class ScoringWeightsTests
{
    [Fact]
    public void Default_CreatesValidWeights()
    {
        // Act
        var weights = ScoringWeights.Default;

        // Assert
        Assert.True(weights.QualityWeight > 0);
        Assert.True(weights.MaxScore > 0);
    }

    [Fact]
    public void MaxScore_CalculatesCorrectly()
    {
        // Arrange
        var weights = new ScoringWeights
        {
            QualityWeight = 100,
            SizeWeight = 50,
            ReleaseGroupWeight = 75,
            YearMatchWeight = 50,
            IssueMatchWeight = 100,
            SeriesMatchWeight = 100,
            FormatWeight = 25,
            SourcePriorityWeight = 30,
            FreshnessWeight = 20
        };

        // Act
        var maxScore = weights.MaxScore;

        // Assert
        Assert.Equal(550, maxScore);
    }
}

public class TrustedReleaseGroupsTests
{
    [Fact]
    public void Default_IncludesCommonGroups()
    {
        // Act
        var groups = TrustedReleaseGroups.Default;

        // Assert
        Assert.Contains("Minutemen", groups.Groups);
        Assert.Contains("DCP", groups.Groups);
        Assert.Contains("Empire", groups.Groups);
    }

    [Fact]
    public void StrictMatching_DefaultsFalse()
    {
        // Act
        var groups = TrustedReleaseGroups.Default;

        // Assert
        Assert.False(groups.StrictMatching);
    }
}

public class ExpectedSizeRangesTests
{
    [Fact]
    public void Default_HasReasonableValues()
    {
        // Act
        var ranges = ExpectedSizeRanges.Default;

        // Assert
        Assert.True(ranges.SingleIssueMinMb < ranges.SingleIssueIdealMb);
        Assert.True(ranges.SingleIssueIdealMb < ranges.SingleIssueMaxMb);
        Assert.True(ranges.PackMinMb < ranges.PackMaxMb);
    }
}

public class ScoreComponentTests
{
    [Fact]
    public void Percentage_CalculatesCorrectly()
    {
        // Arrange
        var component = new ScoreComponent
        {
            Points = 75,
            MaxPoints = 100,
            Reason = "Test"
        };

        // Assert
        Assert.Equal(75.0, component.Percentage);
    }

    [Fact]
    public void Percentage_ZeroMaxPoints_ReturnsZero()
    {
        // Arrange
        var component = new ScoreComponent
        {
            Points = 50,
            MaxPoints = 0,
            Reason = "Test"
        };

        // Assert
        Assert.Equal(0.0, component.Percentage);
    }
}

public class ScoreBreakdownTests
{
    [Fact]
    public void TotalPositive_SumsAllPositiveComponents()
    {
        // Arrange
        var breakdown = new ScoreBreakdown
        {
            Quality = new ScoreComponent { Points = 100 },
            Size = new ScoreComponent { Points = 50 },
            ReleaseGroup = new ScoreComponent { Points = 75 },
            YearMatch = new ScoreComponent { Points = 50 },
            IssueMatch = new ScoreComponent { Points = 100 },
            SeriesMatch = new ScoreComponent { Points = 100 },
            Format = new ScoreComponent { Points = 25 },
            SourcePriority = new ScoreComponent { Points = 30 },
            Freshness = new ScoreComponent { Points = 20 },
            PreferredWords = new ScoreComponent { Points = 10 }
        };

        // Assert
        Assert.Equal(560, breakdown.TotalPositive);
    }

    [Fact]
    public void FinalScore_SubtractsPenalties()
    {
        // Arrange
        var breakdown = new ScoreBreakdown
        {
            Quality = new ScoreComponent { Points = 100 },
            BlacklistPenalty = new ScoreComponent { Points = 50 }
        };

        // Assert
        Assert.Equal(50, breakdown.FinalScore);
    }

    [Fact]
    public void FinalScore_MinimumIsZero()
    {
        // Arrange
        var breakdown = new ScoreBreakdown
        {
            Quality = new ScoreComponent { Points = 10 },
            BlacklistPenalty = new ScoreComponent { Points = 100 }
        };

        // Assert
        Assert.Equal(0, breakdown.FinalScore);
    }
}
