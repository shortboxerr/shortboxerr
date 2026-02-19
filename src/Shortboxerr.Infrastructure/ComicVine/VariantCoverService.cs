using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Service for detecting and managing variant covers for comic issues.
/// </summary>
public class VariantCoverService : IVariantCoverService
{
    private readonly ShortboxerrDbContext _context;
    private readonly IComicVineClient _comicVineClient;
    private readonly ILogger<VariantCoverService> _logger;

    private static readonly Dictionary<string, (string Pattern, int Priority)> VariantPatterns = new()
    {
        { "variant cover", ("Variant", 55) },
        { "variant edition", ("Variant", 55) },
        { "cover variant", ("Variant", 55) },
        { "cover b", ("Variant B", 60) },
        { "cover c", ("Variant C", 60) },
        { "cover d", ("Variant D", 60) },
        { "cover e", ("Variant E", 60) },
        { "1:10", ("1:10 Incentive", 80) },
        { "1:25", ("1:25 Incentive", 85) },
        { "1:50", ("1:50 Incentive", 90) },
        { "1:100", ("1:100 Incentive", 95) },
        { "1:200", ("1:200 Incentive", 98) },
        { "incentive variant", ("Incentive", 75) },
        { "incentive cover", ("Incentive", 75) },
        { "virgin cover", ("Virgin", 70) },
        { "virgin variant", ("Virgin", 70) },
        { "sketch cover", ("Sketch", 65) },
        { "sketch variant", ("Sketch", 65) },
        { "blank cover", ("Blank", 60) },
        { "blank variant", ("Blank", 60) },
        { "exclusive cover", ("Exclusive", 70) },
        { "exclusive variant", ("Exclusive", 70) },
        { "foil cover", ("Foil", 75) },
        { "foil variant", ("Foil", 75) },
        { "glow in the dark", ("Glow in the Dark", 75) },
        { "chromium", ("Chromium", 75) },
        { "lenticular", ("Lenticular", 80) },
        { "wraparound", ("Wraparound", 65) },
        { "connecting cover", ("Connecting", 65) },
        { "connecting variant", ("Connecting", 65) },
        { "homage cover", ("Homage", 60) },
        { "homage variant", ("Homage", 60) },
        { "retailer exclusive", ("Retailer Exclusive", 75) },
        { "retailer variant", ("Retailer Exclusive", 75) },
        { "convention exclusive", ("Convention Exclusive", 80) },
        { "sdcc exclusive", ("SDCC Exclusive", 85) },
        { "sdcc variant", ("SDCC Exclusive", 85) },
        { "nycc exclusive", ("NYCC Exclusive", 85) },
        { "nycc variant", ("NYCC Exclusive", 85) },
        { "c2e2 exclusive", ("C2E2 Exclusive", 85) },
        { "wondercon exclusive", ("WonderCon Exclusive", 85) },
        { "second printing", ("Second Printing", 55) },
        { "third printing", ("Third Printing", 55) },
        { "2nd printing", ("Second Printing", 55) },
        { "3rd printing", ("Third Printing", 55) },
        { "ratio variant", ("Ratio Variant", 75) }
    };

    public VariantCoverService(
        ShortboxerrDbContext context,
        IComicVineClient comicVineClient,
        ILogger<VariantCoverService> logger)
    {
        _context = context;
        _comicVineClient = comicVineClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VariantCover>> GetVariantCoversAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.VariantCovers
            .Where(v => v.IssueId == issueId)
            .OrderByDescending(v => v.IsPrimaryCover)
            .ThenByDescending(v => v.IsPreferred)
            .ThenBy(v => v.VariantType)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<VariantCoverResult> FetchVariantCoversAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _context.Issues
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return VariantCoverResult.Failed(issueId, "Issue not found");
        }

        if (!issue.ComicVineId.HasValue)
        {
            return VariantCoverResult.Failed(issueId, "Issue has no ComicVine ID");
        }

        try
        {
            var cvResult = await _comicVineClient.GetIssueAsync(issue.ComicVineId.Value, cancellationToken);
            
            if (!cvResult.Success || cvResult.Data == null)
            {
                return VariantCoverResult.Failed(issueId, cvResult.Error ?? "Failed to fetch from ComicVine");
            }

            var cvIssue = cvResult.Data;
            var variants = new List<VariantCover>();
            var existingCovers = await _context.VariantCovers
                .Where(v => v.IssueId == issueId)
                .ToListAsync(cancellationToken);

            // Add main cover as primary
            if (cvIssue.Image?.OriginalUrl != null)
            {
                var mainCover = existingCovers.FirstOrDefault(c => c.IsPrimaryCover);
                if (mainCover == null)
                {
                    mainCover = new VariantCoverEntity
                    {
                        IssueId = issueId,
                        ComicVineImageId = 0, // Main cover doesn't have a separate ID
                        ImageUrl = cvIssue.Image.OriginalUrl,
                        Caption = "Main Cover",
                        IsPrimaryCover = true,
                        IsPreferred = true,
                        DetectedAt = DateTime.UtcNow
                    };
                    _context.VariantCovers.Add(mainCover);
                }

                variants.Add(MapToDto(mainCover));
            }

            // Process associated images for variants
            var totalImages = cvIssue.AssociatedImages.Count;
            foreach (var assocImage in cvIssue.AssociatedImages)
            {
                if (string.IsNullOrEmpty(assocImage.OriginalUrl))
                    continue;

                var detection = DetectVariant(assocImage.Caption, assocImage.ImageTags, null);
                
                var existingCover = existingCovers.FirstOrDefault(c => c.ComicVineImageId == assocImage.Id);
                if (existingCover == null)
                {
                    existingCover = new VariantCoverEntity
                    {
                        IssueId = issueId,
                        ComicVineImageId = assocImage.Id,
                        ImageUrl = assocImage.OriginalUrl,
                        Caption = assocImage.Caption,
                        ImageTags = assocImage.ImageTags,
                        VariantType = detection.VariantType,
                        IsPrimaryCover = false,
                        IsPreferred = false,
                        DetectedAt = DateTime.UtcNow
                    };
                    _context.VariantCovers.Add(existingCover);
                }
                else
                {
                    existingCover.ImageUrl = assocImage.OriginalUrl;
                    existingCover.Caption = assocImage.Caption;
                    existingCover.ImageTags = assocImage.ImageTags;
                    existingCover.VariantType = detection.VariantType;
                    existingCover.UpdatedAt = DateTime.UtcNow;
                }

                if (detection.IsVariant)
                {
                    variants.Add(MapToDto(existingCover));
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Fetched {Total} images, detected {Variants} variants for issue {IssueId}",
                totalImages, variants.Count(v => !v.IsPrimaryCover), issueId);

            return VariantCoverResult.Succeeded(issueId, totalImages + 1, variants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch variant covers for issue {IssueId}", issueId);
            return VariantCoverResult.Failed(issueId, ex.Message);
        }
    }

    public async Task<SeriesVariantCoverResult> FetchSeriesVariantCoversAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var issues = await _context.Issues
            .Where(i => i.SeriesId == seriesId && i.ComicVineId.HasValue)
            .OrderBy(i => i.IssueNumber)
            .ToListAsync(cancellationToken);

        if (issues.Count == 0)
        {
            return new SeriesVariantCoverResult
            {
                Success = false,
                SeriesId = seriesId,
                Error = "No issues with ComicVine IDs found"
            };
        }

        var issueResults = new List<VariantCoverResult>();
        var issuesWithVariants = 0;
        var totalVariants = 0;

        foreach (var issue in issues)
        {
            var result = await FetchVariantCoversAsync(issue.Id, cancellationToken);
            issueResults.Add(result);

            if (result.Success && result.VariantsDetected > 0)
            {
                issuesWithVariants++;
                totalVariants += result.VariantsDetected;
            }

            // Small delay to avoid rate limiting
            await Task.Delay(500, cancellationToken);
        }

        return new SeriesVariantCoverResult
        {
            Success = true,
            SeriesId = seriesId,
            IssuesProcessed = issues.Count,
            IssuesWithVariants = issuesWithVariants,
            TotalVariantsDetected = totalVariants,
            IssueResults = issueResults
        };
    }

    public VariantDetectionResult DetectVariant(string? caption, string? imageTags, string? filename)
    {
        var text = $"{caption ?? ""} {imageTags ?? ""} {filename ?? ""}".ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return new VariantDetectionResult
            {
                IsVariant = false,
                Confidence = 0
            };
        }

        var matchedPatterns = new List<string>();
        string? detectedType = null;
        var highestPriority = 0;

        foreach (var (pattern, (variantType, priority)) in VariantPatterns)
        {
            if (text.Contains(pattern))
            {
                matchedPatterns.Add(pattern);
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    detectedType = variantType;
                }
            }
        }

        return new VariantDetectionResult
        {
            IsVariant = matchedPatterns.Count > 0,
            VariantType = detectedType,
            Confidence = highestPriority,
            MatchedPatterns = matchedPatterns
        };
    }

    public async Task<IReadOnlyList<IssueWithVariants>> GetIssuesWithVariantsAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var issues = await _context.Issues
            .Where(i => i.SeriesId == seriesId)
            .Include(i => i.VariantCovers)
            .OrderBy(i => i.IssueNumber)
            .ToListAsync(cancellationToken);

        return issues
            .Where(i => i.VariantCovers.Any())
            .Select(i => new IssueWithVariants
            {
                IssueId = i.Id,
                IssueNumber = i.IssueNumber,
                Title = i.Title,
                MainCoverUrl = i.CoverImageUrl,
                VariantCount = i.VariantCovers.Count(v => !v.IsPrimaryCover),
                Variants = i.VariantCovers.Select(MapToDto).ToList(),
                PreferredVariant = i.VariantCovers.FirstOrDefault(v => v.IsPreferred && !v.IsPrimaryCover) is { } pref 
                    ? MapToDto(pref) 
                    : null
            })
            .ToList();
    }

    public async Task SetPreferredCoverAsync(int issueId, int? variantCoverId, CancellationToken cancellationToken = default)
    {
        var covers = await _context.VariantCovers
            .Where(v => v.IssueId == issueId)
            .ToListAsync(cancellationToken);

        foreach (var cover in covers)
        {
            cover.IsPreferred = variantCoverId.HasValue 
                ? cover.Id == variantCoverId.Value 
                : cover.IsPrimaryCover;
            cover.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Set preferred cover for issue {IssueId} to variant {VariantId}", 
            issueId, variantCoverId?.ToString() ?? "main");
    }

    public async Task<VariantCoverStats> GetSeriesStatsAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var issues = await _context.Issues
            .Where(i => i.SeriesId == seriesId)
            .Include(i => i.VariantCovers)
            .ToListAsync(cancellationToken);

        var issuesWithVariants = issues.Where(i => i.VariantCovers.Any(v => !v.IsPrimaryCover)).ToList();
        var allVariants = issues.SelectMany(i => i.VariantCovers.Where(v => !v.IsPrimaryCover)).ToList();

        var variantsByType = allVariants
            .Where(v => !string.IsNullOrEmpty(v.VariantType))
            .GroupBy(v => v.VariantType!)
            .ToDictionary(g => g.Key, g => g.Count());

        return new VariantCoverStats
        {
            SeriesId = seriesId,
            TotalIssues = issues.Count,
            IssuesWithVariants = issuesWithVariants.Count,
            TotalVariants = allVariants.Count,
            AverageVariantsPerIssue = issuesWithVariants.Count > 0 
                ? (double)allVariants.Count / issuesWithVariants.Count 
                : 0,
            VariantsByType = variantsByType,
            LastFetchedAt = allVariants.Any() 
                ? allVariants.Max(v => v.DetectedAt) 
                : null
        };
    }

    private static VariantCover MapToDto(VariantCoverEntity entity) => new()
    {
        Id = entity.Id,
        IssueId = entity.IssueId,
        ComicVineImageId = entity.ComicVineImageId,
        ImageUrl = entity.ImageUrl,
        Caption = entity.Caption,
        VariantType = entity.VariantType,
        IsPrimaryCover = entity.IsPrimaryCover,
        IsPreferred = entity.IsPreferred,
        DetectedAt = entity.DetectedAt
    };
}
