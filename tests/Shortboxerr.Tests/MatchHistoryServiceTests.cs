using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Services;

namespace Shortboxerr.Tests;

public class MatchHistoryServiceTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;
    private readonly MatchHistoryService _service;
    private readonly Mock<ILogger<MatchHistoryService>> _loggerMock;

    public MatchHistoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ShortboxerrDbContext(options);
        _loggerMock = new Mock<ILogger<MatchHistoryService>>();
        _service = new MatchHistoryService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task LogMatchAsync_CreatesRecordWithAllFields()
    {
        var candidate = new DdlCandidate
        {
            Id = "test-match-1",
            ReleaseTitle = "Batman 001 (2020).cbz",
            SourceSite = "TestSite",
            SourceUrl = "https://example.com/download/123",
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = "Batman",
                IssueNumber = 1,
                Year = 2020,
                Publisher = "DC Comics",
                Format = "cbz"
            }
        };

        var series = new Series { Id = 1, Title = "Batman", StartYear = 2020 };
        var issue = new Issue { Id = 1, IssueNumber = 1, SeriesId = 1 };

        var result = new DdlMatchResult
        {
            MatchFound = true,
            Confidence = 95,
            Series = series,
            Issue = issue,
            Explanation = "Matched to issue: Batman #1",
            RequiresManualReview = false,
            IsFirstIssueForSeries = true,
            IsLowConfidence = false,
            ReviewReason = null,
            ConfidenceReductions = new List<string> { "Test reduction (-5)" }
        };

        var record = await _service.LogMatchAsync(candidate, result, MatchOutcome.AutoImported);

        Assert.NotNull(record);
        Assert.Equal("test-match-1", record.MatchId);
        Assert.Equal("Batman 001 (2020).cbz", record.ReleaseTitle);
        Assert.Equal("TestSite", record.SourceSite);
        Assert.Equal("Batman", record.ParsedSeriesTitle);
        Assert.Equal("1", record.ParsedIssueNumber);
        Assert.Equal(2020, record.ParsedYear);
        Assert.Equal("DC Comics", record.ParsedPublisher);
        Assert.Equal(MatchOutcome.AutoImported, record.Outcome);
        Assert.True(record.MatchFound);
        Assert.Equal(95, record.ConfidenceScore);
        Assert.Equal(1, record.MatchedSeriesId);
        Assert.Equal("Batman", record.MatchedSeriesTitle);
        Assert.True(record.WasFirstIssue);
        Assert.False(record.RequiredManualReview);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsRecordsWithFiltering()
    {
        // Seed some records
        _context.MatchHistories.AddRange(
            new MatchHistory
            {
                MatchId = "match-1",
                ReleaseTitle = "Batman 001.cbz",
                MatchedSeriesId = 1,
                MatchedSeriesTitle = "Batman",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 95,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            },
            new MatchHistory
            {
                MatchId = "match-2",
                ReleaseTitle = "Superman 001.cbz",
                MatchedSeriesId = 2,
                MatchedSeriesTitle = "Superman",
                Outcome = MatchOutcome.PendingReview,
                MatchFound = true,
                ConfidenceScore = 75,
                RequiredManualReview = true,
                Timestamp = DateTime.UtcNow.AddHours(-2)
            },
            new MatchHistory
            {
                MatchId = "match-3",
                ReleaseTitle = "Spider-Man 001.cbz",
                Outcome = MatchOutcome.NoMatch,
                MatchFound = false,
                ConfidenceScore = 0,
                Timestamp = DateTime.UtcNow.AddHours(-3)
            }
        );
        await _context.SaveChangesAsync();

        // Test filtering by outcome
        var autoImportedResult = await _service.GetHistoryAsync(new MatchHistoryQuery
        {
            Outcome = MatchOutcome.AutoImported
        });
        Assert.Single(autoImportedResult.Records);
        Assert.Equal("match-1", autoImportedResult.Records[0].MatchId);

        // Test filtering by required review
        var reviewResult = await _service.GetHistoryAsync(new MatchHistoryQuery
        {
            RequiredReview = true
        });
        Assert.Single(reviewResult.Records);
        Assert.Equal("match-2", reviewResult.Records[0].MatchId);

        // Test pagination
        var pagedResult = await _service.GetHistoryAsync(new MatchHistoryQuery
        {
            Page = 1,
            PageSize = 2
        });
        Assert.Equal(2, pagedResult.Records.Count);
        Assert.Equal(3, pagedResult.TotalCount);
        Assert.Equal(2, pagedResult.TotalPages);
    }

    [Fact]
    public async Task GetAccuracyStatsAsync_CalculatesCorrectStatistics()
    {
        // Seed records with various outcomes
        _context.MatchHistories.AddRange(
            new MatchHistory
            {
                MatchId = "s1",
                ReleaseTitle = "Release 1",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 95,
                UserVerified = true,
                Timestamp = DateTime.UtcNow
            },
            new MatchHistory
            {
                MatchId = "s2",
                ReleaseTitle = "Release 2",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 90,
                UserVerified = true,
                Timestamp = DateTime.UtcNow
            },
            new MatchHistory
            {
                MatchId = "s3",
                ReleaseTitle = "Release 3",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 85,
                UserVerified = false, // Incorrect
                Timestamp = DateTime.UtcNow
            },
            new MatchHistory
            {
                MatchId = "s4",
                ReleaseTitle = "Release 4",
                Outcome = MatchOutcome.PendingReview,
                MatchFound = true,
                ConfidenceScore = 70,
                Timestamp = DateTime.UtcNow
            },
            new MatchHistory
            {
                MatchId = "s5",
                ReleaseTitle = "Release 5",
                Outcome = MatchOutcome.NoMatch,
                MatchFound = false,
                ConfidenceScore = 0,
                Timestamp = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        var stats = await _service.GetAccuracyStatsAsync();

        Assert.Equal(5, stats.TotalMatches);
        Assert.Equal(3, stats.AutoImported);
        Assert.Equal(1, stats.PendingReview);
        Assert.Equal(1, stats.NoMatchFound);
        Assert.Equal(2, stats.VerifiedCorrect);
        Assert.Equal(1, stats.VerifiedIncorrect);
        Assert.Equal(2, stats.Unverified);
        // Accuracy: 2 correct / (2 correct + 1 incorrect) = 66.67%
        Assert.True(stats.AccuracyRate > 66 && stats.AccuracyRate < 67);
    }

    [Fact]
    public async Task VerifyMatchAsync_UpdatesRecord()
    {
        var record = new MatchHistory
        {
            MatchId = "verify-test",
            ReleaseTitle = "Test Release",
            Outcome = MatchOutcome.AutoImported,
            MatchFound = true,
            ConfidenceScore = 80,
            MatchedSeriesId = 1,
            Timestamp = DateTime.UtcNow
        };
        _context.MatchHistories.Add(record);
        await _context.SaveChangesAsync();

        var updated = await _service.VerifyMatchAsync(record.Id, isCorrect: false, correctedSeriesId: 2);

        Assert.NotNull(updated);
        Assert.False(updated.UserVerified);
        Assert.Equal(2, updated.CorrectedSeriesId);
        Assert.Equal(MatchOutcome.ManuallyCorrected, updated.Outcome);
        Assert.NotNull(updated.VerifiedAt);
    }

    [Fact]
    public async Task GetProblematicSeriesAsync_ReturnsSeriesToReview()
    {
        // Seed records with mismatches for one series
        _context.MatchHistories.AddRange(
            new MatchHistory
            {
                MatchId = "p1",
                ReleaseTitle = "Batman 001",
                MatchedSeriesId = 1,
                MatchedSeriesTitle = "Batman",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 90,
                UserVerified = false, // Mismatch
                Timestamp = DateTime.UtcNow
            },
            new MatchHistory
            {
                MatchId = "p2",
                ReleaseTitle = "Batman 002",
                MatchedSeriesId = 1,
                MatchedSeriesTitle = "Batman",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 85,
                UserVerified = false, // Mismatch
                Timestamp = DateTime.UtcNow.AddHours(-1)
            },
            new MatchHistory
            {
                MatchId = "p3",
                ReleaseTitle = "Superman 001",
                MatchedSeriesId = 2,
                MatchedSeriesTitle = "Superman",
                Outcome = MatchOutcome.AutoImported,
                MatchFound = true,
                ConfidenceScore = 95,
                UserVerified = true, // Correct
                Timestamp = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        var problematic = await _service.GetProblematicSeriesAsync(minMismatches: 2);

        Assert.Single(problematic);
        Assert.Equal(1, problematic[0].SeriesId);
        Assert.Equal("Batman", problematic[0].SeriesTitle);
        Assert.Equal(2, problematic[0].Mismatches);
    }
}
