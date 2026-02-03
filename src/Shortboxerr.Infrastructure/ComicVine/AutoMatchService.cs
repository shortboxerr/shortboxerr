using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of IAutoMatchService for automatic ComicVine matching.
/// </summary>
public class AutoMatchService : IAutoMatchService
{
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly IEditionMetadataService _editionMetadataService;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AutoMatchService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AutoMatchService(
        ISeriesMetadataService seriesMetadataService,
        IEditionMetadataService editionMetadataService,
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        ILogger<AutoMatchService> logger)
    {
        _seriesMetadataService = seriesMetadataService;
        _editionMetadataService = editionMetadataService;
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc />
    public async Task<AutoMatchResult> AutoMatchStagedItemAsync(
        StagedItem stagedItem,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var parsed = stagedItem.ParsedInfo;

        if (parsed == null || string.IsNullOrEmpty(parsed.SeriesTitle))
        {
            return new AutoMatchResult
            {
                Success = false,
                Error = "Unable to parse series information from filename",
                ParsedInfo = parsed
            };
        }

        try
        {
            // Determine if this is a collection (TPB/HC/etc.) or single issue
            bool isCollection = !string.IsNullOrEmpty(parsed.EditionIndicator) ||
                               !string.IsNullOrEmpty(parsed.IssueRange) ||
                               parsed.VolumeNumber.HasValue && !parsed.IssueNumber.HasValue;

            if (isCollection)
            {
                return await AutoMatchEditionAsync(parsed, settings, cancellationToken);
            }
            else
            {
                return await AutoMatchSeriesAsync(parsed, settings, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-matching staged item: {FileName}", stagedItem.FileName);
            return new AutoMatchResult
            {
                Success = false,
                Error = ex.Message,
                ParsedInfo = parsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<BulkAutoMatchResult> AutoMatchAllUnmatchedSeriesAsync(
        int? confidenceThreshold = null,
        bool matchImmediately = false,
        IProgress<BulkMatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var threshold = confidenceThreshold ?? settings.ConfidenceThreshold;

        var unmatchedSeries = await _dbContext.Series
            .Where(s => s.ComicVineId == null)
            .ToListAsync(cancellationToken);

        var result = new BulkAutoMatchResult
        {
            Success = true,
            TotalProcessed = unmatchedSeries.Count
        };

        var currentProgress = new BulkMatchProgress { Total = unmatchedSeries.Count };

        foreach (var series in unmatchedSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentProgress.Current++;
            currentProgress.CurrentItem = series.Title;
            progress?.Report(currentProgress);

            try
            {
                var searchResult = await _seriesMetadataService.SearchSeriesAsync(
                    series.Title,
                    series.Publisher,
                    series.StartYear,
                    series.StartYear,
                    cancellationToken: cancellationToken);

                if (!searchResult.Success || !searchResult.Results.Any())
                {
                    result.NoMatchFound++;
                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = series.Id,
                        ItemTitle = series.Title,
                        Success = false,
                        Error = "No matches found"
                    });
                    continue;
                }

                var topMatch = searchResult.Results.First();
                var requiresReview = topMatch.ConfidenceScore < threshold;

                if (!requiresReview && matchImmediately)
                {
                    // Auto-match immediately
                    var matchResult = await _seriesMetadataService.MatchSeriesAsync(
                        series.Id,
                        topMatch.ComicVineId,
                        syncMetadata: true,
                        createMissingIssues: settings.CreateMissingItems,
                        cancellationToken);

                    if (matchResult.Success)
                    {
                        result.AutoMatched++;
                        currentProgress.Matched++;
                    }
                    else
                    {
                        result.Errors++;
                        currentProgress.Failed++;
                    }

                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = series.Id,
                        ItemTitle = series.Title,
                        Success = matchResult.Success,
                        MatchedComicVineId = matchResult.Success ? topMatch.ComicVineId : null,
                        ConfidenceScore = topMatch.ConfidenceScore,
                        Error = matchResult.Error
                    });
                }
                else
                {
                    // Queue for review
                    await CreatePendingMatchAsync(
                        "Series",
                        series.Id,
                        series.Title,
                        searchResult.Results.Select(c => MapToAutoMatchCandidate(c)).ToList(),
                        cancellationToken);

                    result.QueuedForReview++;
                    currentProgress.RequiresReview++;

                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = series.Id,
                        ItemTitle = series.Title,
                        Success = true,
                        ConfidenceScore = topMatch.ConfidenceScore,
                        RequiresReview = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-matching series {SeriesId}: {Title}", series.Id, series.Title);
                result.Errors++;
                currentProgress.Failed++;
                result.Results.Add(new BulkMatchItemResult
                {
                    ItemId = series.Id,
                    ItemTitle = series.Title,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Bulk auto-match complete: {Total} processed, {Matched} matched, {Review} queued for review, {NoMatch} no match, {Errors} errors",
            result.TotalProcessed, result.AutoMatched, result.QueuedForReview, result.NoMatchFound, result.Errors);

        return result;
    }

    /// <inheritdoc />
    public async Task<BulkAutoMatchResult> AutoMatchAllUnmatchedEditionsAsync(
        int? confidenceThreshold = null,
        bool matchImmediately = false,
        IProgress<BulkMatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var threshold = confidenceThreshold ?? settings.ConfidenceThreshold;

        var unmatchedEditions = await _dbContext.EditionTitles
            .Where(e => e.ComicVineId == null)
            .ToListAsync(cancellationToken);

        var result = new BulkAutoMatchResult
        {
            Success = true,
            TotalProcessed = unmatchedEditions.Count
        };

        var currentProgress = new BulkMatchProgress { Total = unmatchedEditions.Count };

        foreach (var edition in unmatchedEditions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentProgress.Current++;
            currentProgress.CurrentItem = edition.Title;
            progress?.Report(currentProgress);

            try
            {
                var searchResult = await _editionMetadataService.SearchEditionsAsync(
                    edition.Title,
                    edition.Publisher,
                    edition.ReleaseDate?.Year,
                    cancellationToken: cancellationToken);

                if (!searchResult.Success || !searchResult.Results.Any())
                {
                    result.NoMatchFound++;
                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = edition.Id,
                        ItemTitle = edition.Title,
                        Success = false,
                        Error = "No matches found"
                    });
                    continue;
                }

                var topMatch = searchResult.Results.First();
                var requiresReview = topMatch.ConfidenceScore < threshold;

                if (!requiresReview && matchImmediately)
                {
                    // Auto-match immediately
                    var matchResult = await _editionMetadataService.MatchEditionAsync(
                        edition.Id,
                        topMatch.ComicVineId,
                        syncMetadata: true,
                        mapContents: true,
                        cancellationToken);

                    if (matchResult.Success)
                    {
                        result.AutoMatched++;
                        currentProgress.Matched++;
                    }
                    else
                    {
                        result.Errors++;
                        currentProgress.Failed++;
                    }

                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = edition.Id,
                        ItemTitle = edition.Title,
                        Success = matchResult.Success,
                        MatchedComicVineId = matchResult.Success ? topMatch.ComicVineId : null,
                        ConfidenceScore = topMatch.ConfidenceScore,
                        Error = matchResult.Error
                    });
                }
                else
                {
                    // Queue for review
                    await CreatePendingMatchAsync(
                        "Edition",
                        edition.Id,
                        edition.Title,
                        searchResult.Results.Select(c => MapEditionToAutoMatchCandidate(c)).ToList(),
                        cancellationToken);

                    result.QueuedForReview++;
                    currentProgress.RequiresReview++;

                    result.Results.Add(new BulkMatchItemResult
                    {
                        ItemId = edition.Id,
                        ItemTitle = edition.Title,
                        Success = true,
                        ConfidenceScore = topMatch.ConfidenceScore,
                        RequiresReview = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-matching edition {EditionId}: {Title}", edition.Id, edition.Title);
                result.Errors++;
                currentProgress.Failed++;
                result.Results.Add(new BulkMatchItemResult
                {
                    ItemId = edition.Id,
                    ItemTitle = edition.Title,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Core.ComicVine.PendingMatch>> GetPendingMatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.PendingMatches
            .Where(p => p.Status == PendingMatchStatus.Pending)
            .OrderByDescending(p => p.TopConfidenceScore)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return pending.Select(p => new Core.ComicVine.PendingMatch
        {
            Id = p.Id,
            ItemType = p.ItemType,
            ItemId = p.ItemId,
            ItemTitle = p.ItemTitle,
            CreatedAt = p.CreatedAt,
            Candidates = DeserializeCandidates(p.CandidatesJson)
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> AcceptPendingMatchAsync(
        int pendingMatchId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.PendingMatches.FindAsync(new object[] { pendingMatchId }, cancellationToken);
        if (pending == null || pending.Status != PendingMatchStatus.Pending)
        {
            return false;
        }

        var candidates = DeserializeCandidates(pending.CandidatesJson);
        if (!candidates.Any())
        {
            return false;
        }

        var topCandidate = candidates.First();

        bool success;
        if (pending.ItemType == "Series")
        {
            var result = await _seriesMetadataService.MatchSeriesAsync(
                pending.ItemId,
                topCandidate.ComicVineId,
                syncMetadata: true,
                createMissingIssues: true,
                cancellationToken);
            success = result.Success;
        }
        else if (pending.ItemType == "Edition")
        {
            var result = await _editionMetadataService.MatchEditionAsync(
                pending.ItemId,
                topCandidate.ComicVineId,
                syncMetadata: true,
                mapContents: true,
                cancellationToken);
            success = result.Success;
        }
        else
        {
            return false;
        }

        if (success)
        {
            pending.Status = PendingMatchStatus.Accepted;
            pending.SelectedComicVineId = topCandidate.ComicVineId;
            pending.ResolvedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return success;
    }

    /// <inheritdoc />
    public async Task<bool> RejectPendingMatchAsync(
        int pendingMatchId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.PendingMatches.FindAsync(new object[] { pendingMatchId }, cancellationToken);
        if (pending == null || pending.Status != PendingMatchStatus.Pending)
        {
            return false;
        }

        pending.Status = PendingMatchStatus.Rejected;
        pending.ResolvedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<AutoMatchSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var cvSettings = await _settingsService.GetAsync<ComicVineSettings>(
            "comicvine", new ComicVineSettings(), cancellationToken);

        return new AutoMatchSettings
        {
            ConfidenceThreshold = cvSettings?.AutoMatchThreshold ?? 85,
            AutoMatchOnImport = true,
            CreateMissingItems = true,
            MaxCandidatesForReview = 5
        };
    }

    #region Private Methods

    private async Task<AutoMatchResult> AutoMatchSeriesAsync(
        ParsedComicInfo parsed,
        AutoMatchSettings settings,
        CancellationToken cancellationToken)
    {
        // First check if we already have a matching local series
        var existingSeries = await FindExistingSeriesAsync(parsed, cancellationToken);
        if (existingSeries != null)
        {
            // Found existing local series
            Issue? matchedIssue = null;
            if (parsed.IssueNumber.HasValue)
            {
                matchedIssue = await _dbContext.Issues
                    .FirstOrDefaultAsync(i => 
                        i.SeriesId == existingSeries.Id && 
                        i.IssueNumber == parsed.IssueNumber.Value, 
                        cancellationToken);
            }

            return new AutoMatchResult
            {
                Success = true,
                AutoMatched = true,
                MatchedSeriesId = existingSeries.Id,
                MatchedIssueId = matchedIssue?.Id,
                ConfidenceScore = 100, // Exact local match
                ParsedInfo = parsed
            };
        }

        // Search ComicVine for matches
        var searchResult = await _seriesMetadataService.SearchSeriesAsync(
            parsed.SeriesTitle!,
            parsed.Publisher,
            parsed.Year,
            parsed.Year,
            cancellationToken: cancellationToken);

        if (!searchResult.Success || !searchResult.Results.Any())
        {
            return new AutoMatchResult
            {
                Success = true,
                AutoMatched = false,
                RequiresReview = false,
                ParsedInfo = parsed,
                Error = "No ComicVine matches found"
            };
        }

        var candidates = searchResult.Results
            .Take(settings.MaxCandidatesForReview)
            .Select(c => MapToAutoMatchCandidate(c))
            .ToList();

        // Check for existing local series that match these ComicVine IDs
        foreach (var candidate in candidates)
        {
            var existingByComicVine = await _dbContext.Series
                .FirstOrDefaultAsync(s => s.ComicVineId == candidate.ComicVineId, cancellationToken);
            if (existingByComicVine != null)
            {
                candidate.ExistingSeriesId = existingByComicVine.Id;
                candidate.ExistingSeriesTitle = existingByComicVine.Title;
            }
        }

        var topMatch = candidates.First();
        var requiresReview = topMatch.ConfidenceScore < settings.ConfidenceThreshold;

        return new AutoMatchResult
        {
            Success = true,
            AutoMatched = !requiresReview,
            MatchedSeriesId = topMatch.ExistingSeriesId,
            ConfidenceScore = topMatch.ConfidenceScore,
            RequiresReview = requiresReview,
            Candidates = candidates,
            ParsedInfo = parsed
        };
    }

    private async Task<AutoMatchResult> AutoMatchEditionAsync(
        ParsedComicInfo parsed,
        AutoMatchSettings settings,
        CancellationToken cancellationToken)
    {
        // Build edition search query
        var searchQuery = parsed.SeriesTitle!;
        if (parsed.VolumeNumber.HasValue)
        {
            searchQuery += $" Vol. {parsed.VolumeNumber}";
        }
        if (!string.IsNullOrEmpty(parsed.EditionIndicator))
        {
            searchQuery += $" {parsed.EditionIndicator}";
        }

        // Check for existing local edition
        var existingEdition = await FindExistingEditionAsync(parsed, cancellationToken);
        if (existingEdition != null)
        {
            return new AutoMatchResult
            {
                Success = true,
                AutoMatched = true,
                MatchedEditionId = existingEdition.Id,
                MatchedSeriesId = existingEdition.SeriesId,
                ConfidenceScore = 100,
                ParsedInfo = parsed
            };
        }

        // Search ComicVine
        var searchResult = await _editionMetadataService.SearchEditionsAsync(
            searchQuery,
            parsed.Publisher,
            parsed.Year,
            cancellationToken: cancellationToken);

        if (!searchResult.Success || !searchResult.Results.Any())
        {
            return new AutoMatchResult
            {
                Success = true,
                AutoMatched = false,
                RequiresReview = false,
                ParsedInfo = parsed,
                Error = "No ComicVine matches found"
            };
        }

        var candidates = searchResult.Results
            .Take(settings.MaxCandidatesForReview)
            .Select(c => MapEditionToAutoMatchCandidate(c))
            .ToList();

        var topMatch = candidates.First();
        var requiresReview = topMatch.ConfidenceScore < settings.ConfidenceThreshold;

        return new AutoMatchResult
        {
            Success = true,
            AutoMatched = !requiresReview,
            ConfidenceScore = topMatch.ConfidenceScore,
            RequiresReview = requiresReview,
            Candidates = candidates,
            ParsedInfo = parsed
        };
    }

    private async Task<Series?> FindExistingSeriesAsync(ParsedComicInfo parsed, CancellationToken cancellationToken)
    {
        var title = parsed.SeriesTitle?.ToLowerInvariant() ?? "";
        
        var query = _dbContext.Series.AsQueryable();

        // Try exact title match first
        var exactMatch = await query
            .Where(s => s.Title.ToLower() == title || 
                       (s.SortTitle != null && s.SortTitle.ToLower() == title))
            .FirstOrDefaultAsync(cancellationToken);

        if (exactMatch != null)
        {
            // If year is specified, verify it matches
            if (parsed.Year.HasValue && exactMatch.StartYear.HasValue &&
                exactMatch.StartYear.Value != parsed.Year.Value)
            {
                // Try to find with matching year
                var yearMatch = await query
                    .Where(s => (s.Title.ToLower() == title || 
                               (s.SortTitle != null && s.SortTitle.ToLower() == title)) &&
                               s.StartYear == parsed.Year.Value)
                    .FirstOrDefaultAsync(cancellationToken);
                
                return yearMatch ?? exactMatch;
            }
            return exactMatch;
        }

        return null;
    }

    private async Task<EditionTitle?> FindExistingEditionAsync(ParsedComicInfo parsed, CancellationToken cancellationToken)
    {
        var title = parsed.SeriesTitle?.ToLowerInvariant() ?? "";
        
        var query = _dbContext.EditionTitles.AsQueryable();

        // Try to find matching edition
        if (parsed.VolumeNumber.HasValue)
        {
            return await query
                .Where(e => e.Title.ToLower().Contains(title) && 
                           e.VolumeNumber == parsed.VolumeNumber.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await query
            .Where(e => e.Title.ToLower() == title)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task CreatePendingMatchAsync(
        string itemType,
        int itemId,
        string itemTitle,
        List<AutoMatchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        // Remove any existing pending match for this item
        var existing = await _dbContext.PendingMatches
            .Where(p => p.ItemType == itemType && p.ItemId == itemId && p.Status == PendingMatchStatus.Pending)
            .ToListAsync(cancellationToken);

        _dbContext.PendingMatches.RemoveRange(existing);

        // Create new pending match
        var pending = new Core.Entities.PendingMatch
        {
            ItemType = itemType,
            ItemId = itemId,
            ItemTitle = itemTitle,
            CandidatesJson = JsonSerializer.Serialize(candidates, _jsonOptions),
            TopConfidenceScore = candidates.FirstOrDefault()?.ConfidenceScore ?? 0
        };

        _dbContext.PendingMatches.Add(pending);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AutoMatchCandidate MapToAutoMatchCandidate(SeriesMatchCandidate c)
    {
        return new AutoMatchCandidate
        {
            ComicVineId = c.ComicVineId,
            Title = c.Title,
            Year = c.StartYear,
            Publisher = c.Publisher,
            IssueCount = c.IssueCount,
            CoverImageUrl = c.CoverImageUrl,
            ConfidenceScore = c.ConfidenceScore,
            ConfidenceReasons = c.ConfidenceReasons
        };
    }

    private static AutoMatchCandidate MapEditionToAutoMatchCandidate(EditionMatchCandidate c)
    {
        return new AutoMatchCandidate
        {
            ComicVineId = c.ComicVineId,
            Title = c.Title,
            Year = c.StartYear,
            Publisher = c.Publisher,
            IssueCount = c.IssueCount,
            CoverImageUrl = c.CoverImageUrl,
            ConfidenceScore = c.ConfidenceScore,
            ConfidenceReasons = c.ConfidenceReasons
        };
    }

    private List<AutoMatchCandidate> DeserializeCandidates(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<AutoMatchCandidate>>(json, _jsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    #endregion
}

