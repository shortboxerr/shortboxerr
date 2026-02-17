using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Service for managing issue metadata via ComicVine.
/// </summary>
public class IssueMetadataService : IIssueMetadataService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly IComicVineClient _comicVineClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IssueMetadataService> _logger;

    // Patterns for detecting special issues
    private static readonly Regex AnnualPattern = new(
        @"(?:^|\s)Annual(?:\s+#?(\d+))?(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex SpecialPattern = new(
        @"(?:^|\s)(Special|One[- ]?Shot|Giant[- ]?Size|King[- ]?Size|80[- ]?Page Giant|100[- ]?Page|Preview|Prologue|Epilogue|Finale|Zero Hour|Infinity|Secret Files|Sourcebook|Handbook|Who'?s Who|Directory|Index)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IssueMetadataService(
        ShortboxerrDbContext dbContext,
        IComicVineClient comicVineClient,
        ISettingsService settingsService,
        ILogger<IssueMetadataService> logger)
    {
        _dbContext = dbContext;
        _comicVineClient = comicVineClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueByComicVineIdAsync(
        int comicVineIssueId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _comicVineClient.GetIssueAsync(comicVineIssueId, cancellationToken);
            
            if (!result.Success || result.Data == null)
            {
                return new IssueDetailResult
                {
                    Success = false,
                    Error = result.Error ?? "Issue not found"
                };
            }

            var cvIssue = result.Data;
            var issueDetail = MapToIssueDetail(cvIssue);

            return new IssueDetailResult
            {
                Success = true,
                Issue = issueDetail
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ComicVine issue {IssueId}", comicVineIssueId);
            return new IssueDetailResult
            {
                Success = false,
                Error = $"Failed to get issue: {ex.Message}"
            };
        }
    }

    public async Task<IssueRefreshResult> RefreshIssueMetadataAsync(
        int issueId, 
        bool force = false, 
        CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.StoryArcs)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return new IssueRefreshResult
            {
                Success = false,
                Error = $"Issue {issueId} not found",
                IssueId = issueId
            };
        }

        if (issue.ComicVineId == null)
        {
            return new IssueRefreshResult
            {
                Success = false,
                Error = "Issue is not linked to ComicVine",
                IssueId = issueId
            };
        }

        // Check if refresh is needed
        var settings = await _settingsService.GetAsync<ComicVineSettings>("comicvine", new ComicVineSettings(), cancellationToken) 
            ?? new ComicVineSettings();
        if (!force && issue.MetadataLastRefreshed.HasValue)
        {
            var daysSinceRefresh = (DateTime.UtcNow - issue.MetadataLastRefreshed.Value).TotalDays;
            if (daysSinceRefresh < settings.RefreshIntervalDays)
            {
                return new IssueRefreshResult
                {
                    Success = true,
                    IssueId = issueId,
                    ComicVineId = issue.ComicVineId,
                    WasUpdated = false
                };
            }
        }

        try
        {
            var result = await _comicVineClient.GetIssueAsync(issue.ComicVineId.Value, cancellationToken);
            
            if (!result.Success || result.Data == null)
            {
                return new IssueRefreshResult
                {
                    Success = false,
                    Error = result.Error ?? "Failed to fetch issue from ComicVine",
                    IssueId = issueId,
                    ComicVineId = issue.ComicVineId
                };
            }

            var cvIssue = result.Data;
            var updatedFields = new List<string>();

            // Update issue fields
            if (cvIssue.Name != issue.Title)
            {
                issue.Title = cvIssue.Name;
                updatedFields.Add("Title");
            }

            if (cvIssue.Description != issue.Overview)
            {
                issue.Overview = cvIssue.Description;
                updatedFields.Add("Overview");
            }

            if (cvIssue.CoverDate != issue.CoverDate)
            {
                issue.CoverDate = cvIssue.CoverDate;
                updatedFields.Add("CoverDate");
            }

            if (cvIssue.StoreDate != issue.StoreDate)
            {
                issue.StoreDate = cvIssue.StoreDate;
                // Also update ReleaseDate if StoreDate is available
                issue.ReleaseDate = cvIssue.StoreDate ?? cvIssue.CoverDate;
                updatedFields.Add("StoreDate");
            }

            var newCoverUrl = cvIssue.Image?.MediumUrl ?? cvIssue.Image?.SmallUrl;
            if (newCoverUrl != issue.CoverImageUrl)
            {
                issue.CoverImageUrl = newCoverUrl;
                updatedFields.Add("CoverImageUrl");
            }

            // Detect special issue types
            var (isAnnual, isSpecial, specialType) = DetectSpecialIssueType(cvIssue.IssueNumber, cvIssue.Name);
            
            // Also check if the series is an annual series (e.g., "Batman Annual", "Absolute Batman Annual")
            if (!isAnnual && issue.Series != null && AnnualPattern.IsMatch(issue.Series.Title ?? ""))
            {
                isAnnual = true;
            }
            
            if (isAnnual != issue.IsAnnual)
            {
                issue.IsAnnual = isAnnual;
                updatedFields.Add("IsAnnual");
            }
            if (isSpecial != issue.IsSpecial)
            {
                issue.IsSpecial = isSpecial;
                updatedFields.Add("IsSpecial");
            }
            if (specialType != issue.SpecialType)
            {
                issue.SpecialType = specialType;
                updatedFields.Add("SpecialType");
            }

            issue.MetadataLastRefreshed = DateTime.UtcNow;
            issue.UpdatedAt = DateTime.UtcNow;

            // Sync story arcs
            var (arcsAdded, arcsRemoved) = await SyncStoryArcsInternalAsync(issue, cvIssue.StoryArcs, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Refreshed issue {IssueId} ({IssueNumber}) metadata from ComicVine. Updated: {Fields}",
                issueId, issue.IssueNumberText ?? issue.IssueNumber.ToString(), string.Join(", ", updatedFields));

            return new IssueRefreshResult
            {
                Success = true,
                IssueId = issueId,
                ComicVineId = issue.ComicVineId,
                WasUpdated = updatedFields.Count > 0 || arcsAdded > 0 || arcsRemoved > 0,
                UpdatedFields = updatedFields,
                StoryArcsAdded = arcsAdded,
                StoryArcsRemoved = arcsRemoved
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh issue {IssueId} metadata", issueId);
            return new IssueRefreshResult
            {
                Success = false,
                Error = $"Failed to refresh: {ex.Message}",
                IssueId = issueId,
                ComicVineId = issue.ComicVineId
            };
        }
    }

    public async Task<IssuesBulkRefreshResult> RefreshSeriesIssuesMetadataAsync(
        int seriesId, 
        bool force = false, 
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .ThenInclude(i => i.StoryArcs)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new IssuesBulkRefreshResult
            {
                Success = false,
                Error = $"Series {seriesId} not found",
                SeriesId = seriesId
            };
        }

        var results = new List<IssueRefreshResult>();
        var issuesRefreshed = 0;
        var issuesFailed = 0;
        var issuesSkipped = 0;

        foreach (var issue in series.Issues.Where(i => i.ComicVineId.HasValue))
        {
            try
            {
                var result = await RefreshIssueMetadataAsync(issue.Id, force, cancellationToken);
                results.Add(result);

                if (result.Success)
                {
                    if (result.WasUpdated)
                        issuesRefreshed++;
                    else
                        issuesSkipped++;
                }
                else
                {
                    issuesFailed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh issue {IssueId} in series {SeriesId}", issue.Id, seriesId);
                issuesFailed++;
                results.Add(new IssueRefreshResult
                {
                    Success = false,
                    Error = ex.Message,
                    IssueId = issue.Id,
                    ComicVineId = issue.ComicVineId
                });
            }
        }

        return new IssuesBulkRefreshResult
        {
            Success = issuesFailed == 0,
            SeriesId = seriesId,
            TotalIssues = series.Issues.Count(i => i.ComicVineId.HasValue),
            IssuesRefreshed = issuesRefreshed,
            IssuesFailed = issuesFailed,
            IssuesSkipped = issuesSkipped,
            Results = results
        };
    }

    public async Task<IssueStoryArcSyncResult> SyncIssueStoryArcsAsync(
        int issueId, 
        CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.StoryArcs)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return new IssueStoryArcSyncResult
            {
                Success = false,
                Error = $"Issue {issueId} not found",
                IssueId = issueId
            };
        }

        if (issue.ComicVineId == null)
        {
            return new IssueStoryArcSyncResult
            {
                Success = false,
                Error = "Issue is not linked to ComicVine",
                IssueId = issueId
            };
        }

        try
        {
            var result = await _comicVineClient.GetIssueAsync(issue.ComicVineId.Value, cancellationToken);
            
            if (!result.Success || result.Data == null)
            {
                return new IssueStoryArcSyncResult
                {
                    Success = false,
                    Error = result.Error ?? "Failed to fetch issue from ComicVine",
                    IssueId = issueId
                };
            }

            var (added, removed) = await SyncStoryArcsInternalAsync(issue, result.Data.StoryArcs, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new IssueStoryArcSyncResult
            {
                Success = true,
                IssueId = issueId,
                StoryArcsAdded = added,
                StoryArcsRemoved = removed,
                StoryArcNames = issue.StoryArcs.Select(sa => sa.Name).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync story arcs for issue {IssueId}", issueId);
            return new IssueStoryArcSyncResult
            {
                Success = false,
                Error = $"Failed to sync: {ex.Message}",
                IssueId = issueId
            };
        }
    }

    public async Task<SpecialIssueDetectionResult> DetectSpecialIssuesAsync(
        int seriesId, 
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new SpecialIssueDetectionResult
            {
                Success = false,
                Error = $"Series {seriesId} not found",
                SeriesId = seriesId
            };
        }

        var specialIssues = new List<SpecialIssueInfo>();
        var annualsDetected = 0;
        var specialsDetected = 0;

        // Check if the series itself is an annual series (e.g., "Batman Annual", "Absolute Batman Annual")
        var seriesIsAnnual = AnnualPattern.IsMatch(series.Title ?? "");

        foreach (var issue in series.Issues)
        {
            var issueNumberText = issue.IssueNumberText ?? issue.IssueNumber.ToString();
            var (isAnnual, isSpecial, specialType) = DetectSpecialIssueType(issueNumberText, issue.Title);
            
            // If the series is an annual series, mark all issues as annuals
            if (seriesIsAnnual && !isAnnual)
            {
                isAnnual = true;
            }

            var changed = false;
            if (isAnnual != issue.IsAnnual)
            {
                issue.IsAnnual = isAnnual;
                changed = true;
            }
            if (isSpecial != issue.IsSpecial)
            {
                issue.IsSpecial = isSpecial;
                changed = true;
            }
            if (specialType != issue.SpecialType)
            {
                issue.SpecialType = specialType;
                changed = true;
            }

            if (changed)
            {
                issue.UpdatedAt = DateTime.UtcNow;
            }

            if (isAnnual || isSpecial)
            {
                specialIssues.Add(new SpecialIssueInfo
                {
                    IssueId = issue.Id,
                    IssueNumber = issueNumberText,
                    IsAnnual = isAnnual,
                    IsSpecial = isSpecial,
                    SpecialType = specialType
                });

                if (isAnnual) annualsDetected++;
                if (isSpecial) specialsDetected++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detected {Annuals} annuals and {Specials} specials in series {SeriesId}",
            annualsDetected, specialsDetected, seriesId);

        return new SpecialIssueDetectionResult
        {
            Success = true,
            SeriesId = seriesId,
            AnnualsDetected = annualsDetected,
            SpecialsDetected = specialsDetected,
            SpecialIssues = specialIssues
        };
    }

    #region Private Methods

    private ComicVineIssueDetail MapToIssueDetail(ComicVineIssue cvIssue)
    {
        var (isAnnual, isSpecial, specialType) = DetectSpecialIssueType(cvIssue.IssueNumber, cvIssue.Name);

        return new ComicVineIssueDetail
        {
            ComicVineId = cvIssue.Id,
            Name = cvIssue.Name,
            IssueNumber = cvIssue.IssueNumber,
            Description = cvIssue.Description,
            CoverDate = cvIssue.CoverDate,
            StoreDate = cvIssue.StoreDate,
            CoverImageUrl = cvIssue.Image?.MediumUrl ?? cvIssue.Image?.SmallUrl,
            ComicVineUrl = cvIssue.SiteDetailUrl,
            VolumeId = cvIssue.Volume?.Id,
            VolumeName = cvIssue.Volume?.Name,
            StoryArcs = cvIssue.StoryArcs.Select(sa => new StoryArcInfo
            {
                ComicVineId = sa.Id,
                Name = sa.Name,
                ComicVineUrl = sa.ApiDetailUrl?.Replace("/api/", "/")
            }).ToList(),
            IsAnnual = isAnnual,
            IsSpecial = isSpecial,
            SpecialType = specialType
        };
    }

    private Task<(int Added, int Removed)> SyncStoryArcsInternalAsync(
        Issue issue,
        List<ComicVineStoryArcRef> cvStoryArcs,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken; // Unused but kept for consistency
        var existingArcs = issue.StoryArcs.ToList();
        var existingArcIds = existingArcs.Select(a => a.ComicVineStoryArcId).ToHashSet();
        var newArcIds = cvStoryArcs.Select(a => a.Id).ToHashSet();

        // Remove arcs that no longer exist
        var arcsToRemove = existingArcs.Where(a => !newArcIds.Contains(a.ComicVineStoryArcId)).ToList();
        foreach (var arc in arcsToRemove)
        {
            _dbContext.IssueStoryArcs.Remove(arc);
        }

        // Add new arcs
        var arcsToAdd = cvStoryArcs.Where(a => !existingArcIds.Contains(a.Id)).ToList();
        foreach (var cvArc in arcsToAdd)
        {
            var newArc = new IssueStoryArc
            {
                IssueId = issue.Id,
                ComicVineStoryArcId = cvArc.Id,
                Name = cvArc.Name,
                ComicVineUrl = cvArc.ApiDetailUrl?.Replace("/api/", "/")
            };
            _dbContext.IssueStoryArcs.Add(newArc);
            issue.StoryArcs.Add(newArc);
        }

        return Task.FromResult((arcsToAdd.Count, arcsToRemove.Count));
    }

    private static (bool IsAnnual, bool IsSpecial, string? SpecialType) DetectSpecialIssueType(
        string? issueNumber, 
        string? title)
    {
        var textToCheck = $"{issueNumber} {title}".Trim();
        
        // Check for annual
        var annualMatch = AnnualPattern.Match(textToCheck);
        if (annualMatch.Success)
        {
            return (true, false, null);
        }

        // Check for other special types
        var specialMatch = SpecialPattern.Match(textToCheck);
        if (specialMatch.Success)
        {
            var specialType = specialMatch.Groups[1].Value.Trim();
            return (false, true, NormalizeSpecialType(specialType));
        }

        // Check for negative issue numbers (often specials)
        if (decimal.TryParse(issueNumber, out var num) && num < 0)
        {
            return (false, true, "Preview");
        }

        // Check for fractional issues (like .1, .2 point issues)
        if (issueNumber?.Contains('.') == true && decimal.TryParse(issueNumber, out var fracNum))
        {
            // .1 issues are typically not specials, just continuation issues
            // Only mark as special if it's a weird fraction
            var fraction = fracNum - Math.Floor(fracNum);
            if (fraction != 0.1m && fraction != 0.2m && fraction != 0.5m)
            {
                return (false, true, "Special");
            }
        }

        return (false, false, null);
    }

    private static string NormalizeSpecialType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "special" => "Special",
            "one-shot" or "oneshot" => "One-Shot",
            "giant-size" or "giantsize" or "giant size" => "Giant-Size",
            "king-size" or "kingsize" or "king size" => "King-Size",
            "80-page giant" or "80 page giant" => "80-Page Giant",
            "100-page" or "100 page" => "100-Page",
            "preview" => "Preview",
            "prologue" => "Prologue",
            "epilogue" => "Epilogue",
            "finale" => "Finale",
            "zero hour" => "Zero Hour",
            "infinity" => "Infinity",
            "secret files" => "Secret Files",
            "sourcebook" => "Sourcebook",
            "handbook" => "Handbook",
            "who's who" or "whos who" => "Who's Who",
            "directory" => "Directory",
            "index" => "Index",
            _ => type
        };
    }

    #endregion
}

