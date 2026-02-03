using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of IEditionMetadataService using ComicVine.
/// </summary>
public class EditionMetadataService : IEditionMetadataService
{
    private readonly IComicVineClient _comicVineClient;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<EditionMetadataService> _logger;

    // Patterns for detecting edition types from titles
    private static readonly Regex OmnibusPattern = new(@"\b(omnibus|omni)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HardcoverPattern = new(@"\b(hardcover|hc|deluxe)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AbsolutePattern = new(@"\b(absolute)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CompendiumPattern = new(@"\b(compendium)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TpbPattern = new(@"\b(tpb|trade|paperback|vol\.?\s*\d+|volume\s*\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public EditionMetadataService(
        IComicVineClient comicVineClient,
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        ILogger<EditionMetadataService> logger)
    {
        _comicVineClient = comicVineClient;
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EditionSearchResult> SearchEditionsAsync(
        string query,
        string? publisher = null,
        int? year = null,
        int page = 1,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cvSettings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
            if (string.IsNullOrEmpty(cvSettings?.ApiKey))
            {
                return new EditionSearchResult
                {
                    Success = false,
                    Error = "ComicVine API key not configured"
                };
            }

            var searchResult = await _comicVineClient.SearchVolumesAsync(query, page, limit, cancellationToken);
            if (!searchResult.Success)
            {
                return new EditionSearchResult
                {
                    Success = false,
                    Error = searchResult.Error
                };
            }

            var candidates = searchResult.Results
                .Select(v => MapVolumeToCandidate(v, query, publisher, year))
                .ToList();

            // Apply additional filtering
            if (!string.IsNullOrEmpty(publisher))
            {
                candidates = candidates
                    .Where(c => c.Publisher?.Contains(publisher, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            if (year.HasValue)
            {
                candidates = candidates
                    .Where(c => c.StartYear == year.Value || 
                               (c.StartYear.HasValue && Math.Abs(c.StartYear.Value - year.Value) <= 2))
                    .ToList();
            }

            // Sort by confidence score
            candidates = candidates.OrderByDescending(c => c.ConfidenceScore).ToList();

            return new EditionSearchResult
            {
                Success = true,
                Results = candidates,
                TotalResults = searchResult.TotalResults,
                Page = page,
                Limit = limit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search ComicVine for editions: {Query}", query);
            return new EditionSearchResult
            {
                Success = false,
                Error = $"Search failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<EditionMatchCandidate?> GetEditionByComicVineIdAsync(
        int volumeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _comicVineClient.GetVolumeAsync(volumeId, cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return null;
            }

            return MapVolumeToCandidate(result.Data, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get edition from ComicVine: {VolumeId}", volumeId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<EditionMatchResult> MatchEditionAsync(
        int editionId,
        int comicVineVolumeId,
        bool syncMetadata = true,
        bool mapContents = true,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles
            .Include(e => e.Contents)
            .FirstOrDefaultAsync(e => e.Id == editionId, cancellationToken);

        if (edition == null)
        {
            return new EditionMatchResult
            {
                Success = false,
                Error = $"Edition with ID {editionId} not found"
            };
        }

        try
        {
            var volumeResult = await _comicVineClient.GetVolumeAsync(comicVineVolumeId, cancellationToken);
            if (!volumeResult.Success || volumeResult.Data == null)
            {
                return new EditionMatchResult
                {
                    Success = false,
                    Error = $"ComicVine volume {comicVineVolumeId} not found"
                };
            }

            var volume = volumeResult.Data;

            // Set the ComicVine ID
            edition.ComicVineId = comicVineVolumeId;
            edition.ComicVineUrl = volume.SiteDetailUrl;
            edition.UpdatedAt = DateTime.UtcNow;

            // Sync metadata if requested
            if (syncMetadata)
            {
                ApplyVolumeMetadataToEdition(edition, volume);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Map contents if requested
            int issuesMapped = 0;
            if (mapContents)
            {
                var contentResult = await SyncEditionContentsAsync(editionId, cancellationToken);
                issuesMapped = contentResult.IssuesMapped;
            }

            _logger.LogInformation(
                "Matched edition {EditionId} ({Title}) to ComicVine volume {VolumeId}",
                editionId, edition.Title, comicVineVolumeId);

            return new EditionMatchResult
            {
                Success = true,
                EditionId = editionId,
                ComicVineId = comicVineVolumeId,
                MetadataSynced = syncMetadata,
                ContentsMapped = mapContents,
                IssuesMapped = issuesMapped
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to match edition {EditionId} to ComicVine volume {VolumeId}",
                editionId, comicVineVolumeId);
            return new EditionMatchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<EditionAutoMatchResult> AutoMatchEditionAsync(
        int editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles.FindAsync(new object[] { editionId }, cancellationToken);
        if (edition == null)
        {
            return new EditionAutoMatchResult
            {
                Success = false,
                Error = $"Edition with ID {editionId} not found"
            };
        }

        // Get auto-match threshold from settings
        var cvSettings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken);
        var threshold = cvSettings?.AutoMatchThreshold ?? 85;

        // Search for matches
        var searchResult = await SearchEditionsAsync(
            edition.Title,
            edition.Publisher,
            edition.ReleaseDate?.Year,
            cancellationToken: cancellationToken);

        if (!searchResult.Success || !searchResult.Results.Any())
        {
            return new EditionAutoMatchResult
            {
                Success = false,
                Error = searchResult.Error ?? "No matches found",
                EditionId = editionId,
                Candidates = searchResult.Results
            };
        }

        var topMatch = searchResult.Results.First();
        var requiresReview = topMatch.ConfidenceScore < threshold;

        // Auto-match if above threshold
        if (!requiresReview)
        {
            var matchResult = await MatchEditionAsync(
                editionId,
                topMatch.ComicVineId,
                syncMetadata: true,
                mapContents: true,
                cancellationToken);

            if (!matchResult.Success)
            {
                return new EditionAutoMatchResult
                {
                    Success = false,
                    Error = matchResult.Error,
                    EditionId = editionId,
                    Candidates = searchResult.Results
                };
            }
        }

        return new EditionAutoMatchResult
        {
            Success = true,
            EditionId = editionId,
            MatchedComicVineId = requiresReview ? null : topMatch.ComicVineId,
            ConfidenceScore = topMatch.ConfidenceScore,
            RequiresManualReview = requiresReview,
            Candidates = searchResult.Results
        };
    }

    /// <inheritdoc />
    public async Task<bool> UnmatchEditionAsync(
        int editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles.FindAsync(new object[] { editionId }, cancellationToken);
        if (edition == null)
        {
            return false;
        }

        edition.ComicVineId = null;
        edition.ComicVineUrl = null;
        edition.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unmatched edition {EditionId} ({Title}) from ComicVine", editionId, edition.Title);
        return true;
    }

    /// <inheritdoc />
    public async Task<EditionMatchResult> RefreshEditionMetadataAsync(
        int editionId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles.FindAsync(new object[] { editionId }, cancellationToken);
        if (edition == null)
        {
            return new EditionMatchResult
            {
                Success = false,
                Error = $"Edition with ID {editionId} not found"
            };
        }

        if (!edition.ComicVineId.HasValue)
        {
            return new EditionMatchResult
            {
                Success = false,
                Error = "Edition is not matched to ComicVine"
            };
        }

        return await MatchEditionAsync(editionId, edition.ComicVineId.Value, 
            syncMetadata: true, mapContents: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditionContentSyncResult> SyncEditionContentsAsync(
        int editionId,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles
            .Include(e => e.Contents)
            .Include(e => e.Series)
            .FirstOrDefaultAsync(e => e.Id == editionId, cancellationToken);

        if (edition == null)
        {
            return new EditionContentSyncResult
            {
                Success = false,
                Error = $"Edition with ID {editionId} not found"
            };
        }

        if (!edition.ComicVineId.HasValue)
        {
            return new EditionContentSyncResult
            {
                Success = false,
                Error = "Edition is not matched to ComicVine"
            };
        }

        try
        {
            // Fetch issues from ComicVine
            var issuesResult = await _comicVineClient.GetVolumeIssuesAsync(
                edition.ComicVineId.Value, 1, 100, cancellationToken);

            if (!issuesResult.Success)
            {
                return new EditionContentSyncResult
                {
                    Success = false,
                    Error = issuesResult.Error
                };
            }

            var mappings = new List<EditionContentMapping>();
            var issuesMapped = 0;
            var issuesCreated = 0;

            // Clear existing contents
            _dbContext.EditionContents.RemoveRange(edition.Contents);

            var sortOrder = 0;
            foreach (var cvIssue in issuesResult.Results.OrderBy(i => ParseIssueNumber(i.IssueNumber)))
            {
                sortOrder++;

                // Try to find matching local issue
                Issue? localIssue = null;
                Series? localSeries = null;

                if (edition.SeriesId.HasValue)
                {
                    // Look in the parent series first
                    var issueNum = ParseIssueNumber(cvIssue.IssueNumber);
                    localIssue = await _dbContext.Issues
                        .Include(i => i.Series)
                        .FirstOrDefaultAsync(i => 
                            i.SeriesId == edition.SeriesId.Value && 
                            i.IssueNumber == issueNum, cancellationToken);
                    localSeries = localIssue?.Series;
                }

                if (localIssue == null && cvIssue.Volume != null)
                {
                    // Look for issue by ComicVine ID
                    localIssue = await _dbContext.Issues
                        .Include(i => i.Series)
                        .FirstOrDefaultAsync(i => i.ComicVineId == cvIssue.Id, cancellationToken);
                    localSeries = localIssue?.Series;
                }

                // Create the content mapping
                var content = new EditionContent
                {
                    EditionTitleId = editionId,
                    IssueId = localIssue?.Id,
                    SeriesId = localSeries?.Id ?? edition.SeriesId,
                    IssueNumber = ParseIssueNumber(cvIssue.IssueNumber),
                    SortOrder = sortOrder
                };

                _dbContext.EditionContents.Add(content);

                if (localIssue != null)
                {
                    issuesMapped++;
                }

                mappings.Add(new EditionContentMapping
                {
                    ComicVineIssueId = cvIssue.Id,
                    IssueNumber = cvIssue.IssueNumber,
                    IssueTitle = cvIssue.Name,
                    LocalIssueId = localIssue?.Id,
                    LocalSeriesId = localSeries?.Id,
                    LocalSeriesTitle = localSeries?.Title,
                    WasCreated = false
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Synced {IssueCount} issues for edition {EditionId} ({Title}), {Mapped} mapped to local issues",
                issuesResult.Results.Count, editionId, edition.Title, issuesMapped);

            return new EditionContentSyncResult
            {
                Success = true,
                EditionId = editionId,
                IssuesFound = issuesResult.Results.Count,
                IssuesMapped = issuesMapped,
                IssuesCreated = issuesCreated,
                Mappings = mappings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync contents for edition {EditionId}", editionId);
            return new EditionContentSyncResult
            {
                Success = false,
                Error = ex.Message,
                EditionId = editionId
            };
        }
    }

    #region Private Methods

    private EditionMatchCandidate MapVolumeToCandidate(
        ComicVineVolume volume,
        string? searchQuery,
        string? filterPublisher,
        int? filterYear)
    {
        var confidence = CalculateConfidenceScore(volume, searchQuery, filterPublisher, filterYear, out var reasons);

        return new EditionMatchCandidate
        {
            ComicVineId = volume.Id,
            Title = volume.Name,
            StartYear = volume.StartYear,
            Publisher = volume.Publisher?.Name,
            Description = volume.Deck ?? volume.Description,
            IssueCount = volume.IssueCount,
            CoverImageUrl = volume.Image?.MediumUrl ?? volume.Image?.SmallUrl,
            ComicVineUrl = volume.SiteDetailUrl,
            ConfidenceScore = confidence,
            ConfidenceReasons = reasons,
            DetectedEditionType = DetectEditionType(volume.Name)
        };
    }

    private int CalculateConfidenceScore(
        ComicVineVolume volume,
        string? searchQuery,
        string? filterPublisher,
        int? filterYear,
        out List<string> reasons)
    {
        reasons = new List<string>();
        var score = 50; // Base score

        if (string.IsNullOrEmpty(searchQuery))
        {
            reasons.Add("No search query for comparison");
            return score;
        }

        var normalizedQuery = NormalizeTitle(searchQuery);
        var normalizedTitle = NormalizeTitle(volume.Name);

        // Exact title match
        if (normalizedTitle.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
            reasons.Add("Exact title match (+40)");
        }
        // Title starts with query
        else if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
            reasons.Add("Title starts with query (+25)");
        }
        // Title contains query
        else if (normalizedTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
            reasons.Add("Title contains query (+15)");
        }
        // Check aliases
        else if (volume.Aliases.Any(a => NormalizeTitle(a).Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 35;
            reasons.Add("Alias exact match (+35)");
        }

        // Publisher match
        if (!string.IsNullOrEmpty(filterPublisher) && 
            volume.Publisher?.Name?.Contains(filterPublisher, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 10;
            reasons.Add("Publisher match (+10)");
        }

        // Year match
        if (filterYear.HasValue && volume.StartYear == filterYear)
        {
            score += 10;
            reasons.Add("Year exact match (+10)");
        }
        else if (filterYear.HasValue && volume.StartYear.HasValue &&
                 Math.Abs(volume.StartYear.Value - filterYear.Value) <= 2)
        {
            score += 5;
            reasons.Add("Year close match (+5)");
        }

        // Edition type detection bonus
        var detectedType = DetectEditionType(volume.Name);
        if (detectedType.HasValue)
        {
            score += 5;
            reasons.Add($"Edition type detected: {detectedType.Value} (+5)");
        }

        // Cap at 100
        return Math.Min(100, score);
    }

    private static EditionType? DetectEditionType(string title)
    {
        if (AbsolutePattern.IsMatch(title)) return EditionType.AbsoluteEdition;
        if (OmnibusPattern.IsMatch(title)) return EditionType.Omnibus;
        if (CompendiumPattern.IsMatch(title)) return EditionType.Compendium;
        if (HardcoverPattern.IsMatch(title)) return EditionType.Hardcover;
        if (TpbPattern.IsMatch(title)) return EditionType.TradesPaperback;
        return null;
    }

    private static string NormalizeTitle(string title)
    {
        // Remove common prefixes/suffixes
        var normalized = title
            .Replace(":", " ")
            .Replace("-", " ")
            .Replace("  ", " ")
            .Trim();

        // Remove "The " prefix for matching
        if (normalized.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private void ApplyVolumeMetadataToEdition(EditionTitle edition, ComicVineVolume volume)
    {
        // Only update if we have new data
        if (!string.IsNullOrEmpty(volume.Description) && string.IsNullOrEmpty(edition.Overview))
        {
            edition.Overview = volume.Description;
        }

        if (!string.IsNullOrEmpty(volume.Publisher?.Name) && string.IsNullOrEmpty(edition.Publisher))
        {
            edition.Publisher = volume.Publisher.Name;
        }

        if (volume.Image?.MediumUrl != null)
        {
            edition.CoverImageUrl = volume.Image.OriginalUrl ?? volume.Image.MediumUrl;
        }

        // Detect and set edition type if not already set
        if (edition.EditionType == EditionType.TradesPaperback)
        {
            var detectedType = DetectEditionType(volume.Name);
            if (detectedType.HasValue)
            {
                edition.EditionType = detectedType.Value;
            }
        }

        edition.UpdatedAt = DateTime.UtcNow;
    }

    private static decimal ParseIssueNumber(string issueNumber)
    {
        if (string.IsNullOrEmpty(issueNumber))
            return 0;

        // Handle common formats
        var cleaned = issueNumber.Trim().TrimStart('#');

        if (decimal.TryParse(cleaned, out var number))
            return number;

        // Handle special cases like "½"
        if (cleaned == "½" || cleaned.Equals("1/2", StringComparison.OrdinalIgnoreCase))
            return 0.5m;

        return 0;
    }

    #endregion
}

