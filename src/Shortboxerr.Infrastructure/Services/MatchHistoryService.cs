using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Service for logging and querying auto-match history.
/// </summary>
public class MatchHistoryService : IMatchHistoryService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ILogger<MatchHistoryService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public MatchHistoryService(ShortboxerrDbContext dbContext, ILogger<MatchHistoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MatchHistory> LogMatchAsync(
        DdlCandidate candidate,
        DdlMatchResult result,
        MatchOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        var record = new MatchHistory
        {
            MatchId = candidate.Id,
            ReleaseTitle = candidate.ReleaseTitle,
            SourceSite = candidate.SourceSite,
            ParsedSeriesTitle = candidate.ParsedInfo?.SeriesTitle,
            ParsedIssueNumber = candidate.ParsedInfo?.IssueNumber?.ToString(),
            ParsedYear = candidate.ParsedInfo?.Year,
            ParsedPublisher = candidate.ParsedInfo?.Publisher,
            Outcome = outcome,
            MatchFound = result.MatchFound,
            ConfidenceScore = result.Confidence,
            MatchedSeriesId = result.Series?.Id,
            MatchedSeriesTitle = result.Series?.Title,
            MatchedIssueId = result.Issue?.Id,
            MatchedIssueNumber = result.Issue?.IssueNumber.ToString(),
            WasFirstIssue = result.IsFirstIssueForSeries,
            RequiredManualReview = result.RequiresManualReview,
            ReviewReason = result.ReviewReason,
            Explanation = result.Explanation,
            Timestamp = DateTime.UtcNow
        };

        // Serialize score breakdown if available
        if (result.ScoreBreakdown != null)
        {
            record.ScoreBreakdownJson = JsonSerializer.Serialize(new
            {
                result.ScoreBreakdown.TitleScore,
                result.ScoreBreakdown.YearAdjustment,
                result.ScoreBreakdown.PublisherAdjustment,
                result.ScoreBreakdown.AmbiguityPenalty,
                result.ScoreBreakdown.FinalScore,
                result.ScoreBreakdown.YearMatchStatus,
                result.ScoreBreakdown.PublisherMatchStatus,
                result.ScoreBreakdown.IsAmbiguousSeries,
                Explanations = result.ScoreBreakdown.ScoreExplanations
            }, JsonOptions);
        }

        // Serialize confidence reductions if available
        if (result.ConfidenceReductions.Count > 0)
        {
            record.ConfidenceReductionsJson = JsonSerializer.Serialize(result.ConfidenceReductions, JsonOptions);
        }

        _dbContext.MatchHistories.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Match logged: {ReleaseTitle} -> {Outcome} (confidence: {Confidence}%, series: {SeriesTitle})",
            candidate.ReleaseTitle,
            outcome,
            result.Confidence,
            result.Series?.Title ?? "none");

        return record;
    }

    public async Task<MatchHistory?> VerifyMatchAsync(
        int matchHistoryId,
        bool isCorrect,
        int? correctedSeriesId = null,
        int? correctedIssueId = null,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.MatchHistories.FindAsync(new object[] { matchHistoryId }, cancellationToken);
        if (record == null)
            return null;

        record.UserVerified = isCorrect;
        record.VerifiedAt = DateTime.UtcNow;

        if (!isCorrect)
        {
            record.CorrectedSeriesId = correctedSeriesId;
            record.CorrectedIssueId = correctedIssueId;
            record.Outcome = correctedSeriesId.HasValue ? MatchOutcome.ManuallyCorrected : MatchOutcome.ManuallyRejected;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Match verified: {MatchId} -> {IsCorrect} (corrected to series: {CorrectedSeriesId})",
            record.MatchId,
            isCorrect,
            correctedSeriesId);

        return record;
    }

    public async Task<MatchHistoryQueryResult> GetHistoryAsync(
        MatchHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.MatchHistories.AsQueryable();

        // Apply filters
        if (query.SeriesId.HasValue)
            baseQuery = baseQuery.Where(m => m.MatchedSeriesId == query.SeriesId);

        if (query.Outcome.HasValue)
            baseQuery = baseQuery.Where(m => m.Outcome == query.Outcome);

        if (query.RequiredReview.HasValue)
            baseQuery = baseQuery.Where(m => m.RequiredManualReview == query.RequiredReview);

        if (query.UserVerified.HasValue)
            baseQuery = baseQuery.Where(m => m.UserVerified == query.UserVerified);

        if (query.Since.HasValue)
            baseQuery = baseQuery.Where(m => m.Timestamp >= query.Since);

        if (query.Until.HasValue)
            baseQuery = baseQuery.Where(m => m.Timestamp <= query.Until);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.ToLower();
            baseQuery = baseQuery.Where(m => 
                m.ReleaseTitle.ToLower().Contains(term) ||
                (m.MatchedSeriesTitle != null && m.MatchedSeriesTitle.ToLower().Contains(term)) ||
                (m.ParsedSeriesTitle != null && m.ParsedSeriesTitle.ToLower().Contains(term)));
        }

        // Get total count before pagination
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // Apply sorting
        baseQuery = query.SortBy switch
        {
            MatchHistorySortBy.ConfidenceScore => query.SortDescending
                ? baseQuery.OrderByDescending(m => m.ConfidenceScore)
                : baseQuery.OrderBy(m => m.ConfidenceScore),
            MatchHistorySortBy.SeriesTitle => query.SortDescending
                ? baseQuery.OrderByDescending(m => m.MatchedSeriesTitle)
                : baseQuery.OrderBy(m => m.MatchedSeriesTitle),
            MatchHistorySortBy.Outcome => query.SortDescending
                ? baseQuery.OrderByDescending(m => m.Outcome)
                : baseQuery.OrderBy(m => m.Outcome),
            _ => query.SortDescending
                ? baseQuery.OrderByDescending(m => m.Timestamp)
                : baseQuery.OrderBy(m => m.Timestamp)
        };

        // Apply pagination
        var records = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new MatchHistoryQueryResult
        {
            Records = records,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<MatchAccuracyStats> GetAccuracyStatsAsync(
        int? seriesId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MatchHistories.AsQueryable();

        if (seriesId.HasValue)
            query = query.Where(m => m.MatchedSeriesId == seriesId);

        if (since.HasValue)
            query = query.Where(m => m.Timestamp >= since);

        // Use database aggregation instead of loading all records
        var totalCount = await query.CountAsync(cancellationToken);
        
        if (totalCount == 0)
        {
            return new MatchAccuracyStats();
        }

        // Aggregate outcome counts in a single query using GroupBy
        var outcomeCounts = await query
            .GroupBy(m => m.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var outcomeDict = outcomeCounts.ToDictionary(x => x.Outcome, x => x.Count);

        // Aggregate verification counts
        var verifiedCorrect = await query.CountAsync(r => r.UserVerified == true, cancellationToken);
        var verifiedIncorrect = await query.CountAsync(r => r.UserVerified == false, cancellationToken);
        var unverified = await query.CountAsync(r => !r.UserVerified.HasValue, cancellationToken);

        // Auto-import accuracy calculation
        var autoImportedQuery = query.Where(r => r.Outcome == MatchOutcome.AutoImported);
        var autoImportedVerifiedCount = await autoImportedQuery.CountAsync(r => r.UserVerified.HasValue, cancellationToken);
        var autoImportedCorrectCount = await autoImportedQuery.CountAsync(r => r.UserVerified == true, cancellationToken);
        var autoImportAccuracy = autoImportedVerifiedCount > 0
            ? (double)autoImportedCorrectCount / autoImportedVerifiedCount * 100
            : 0;

        // Aggregate statistics
        var avgConfidence = await query.AverageAsync(r => r.ConfidenceScore, cancellationToken);
        var oldestRecord = await query.MinAsync(r => r.Timestamp, cancellationToken);
        var newestRecord = await query.MaxAsync(r => r.Timestamp, cancellationToken);

        return new MatchAccuracyStats
        {
            TotalMatches = totalCount,
            AutoImported = outcomeDict.GetValueOrDefault(MatchOutcome.AutoImported, 0),
            PendingReview = outcomeDict.GetValueOrDefault(MatchOutcome.PendingReview, 0),
            ManuallyApproved = outcomeDict.GetValueOrDefault(MatchOutcome.ManuallyApproved, 0),
            ManuallyRejected = outcomeDict.GetValueOrDefault(MatchOutcome.ManuallyRejected, 0),
            ManuallyCorrected = outcomeDict.GetValueOrDefault(MatchOutcome.ManuallyCorrected, 0),
            NoMatchFound = outcomeDict.GetValueOrDefault(MatchOutcome.NoMatch, 0),
            VerifiedCorrect = verifiedCorrect,
            VerifiedIncorrect = verifiedIncorrect,
            Unverified = unverified,
            AutoImportAccuracy = autoImportAccuracy,
            AverageConfidence = avgConfidence,
            OldestRecord = oldestRecord,
            NewestRecord = newestRecord
        };
    }

    public async Task<IReadOnlyList<SeriesMismatchSummary>> GetProblematicSeriesAsync(
        int minMismatches = 2,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MatchHistories
            .Where(m => m.MatchedSeriesId.HasValue);

        if (since.HasValue)
            query = query.Where(m => m.Timestamp >= since);

        var grouped = await query
            .GroupBy(m => new { m.MatchedSeriesId, m.MatchedSeriesTitle })
            .Select(g => new
            {
                SeriesId = g.Key.MatchedSeriesId!.Value,
                SeriesTitle = g.Key.MatchedSeriesTitle ?? "Unknown",
                TotalMatches = g.Count(),
                Mismatches = g.Count(m => m.UserVerified == false),
                LastMismatch = g.Where(m => m.UserVerified == false)
                    .Max(m => (DateTime?)m.Timestamp) ?? DateTime.MinValue
            })
            .Where(g => g.Mismatches >= minMismatches)
            .OrderByDescending(g => g.Mismatches)
            .ToListAsync(cancellationToken);

        return grouped.Select(g => new SeriesMismatchSummary
        {
            SeriesId = g.SeriesId,
            SeriesTitle = g.SeriesTitle,
            TotalMatches = g.TotalMatches,
            Mismatches = g.Mismatches,
            LastMismatch = g.LastMismatch
        }).ToList();
    }
}
