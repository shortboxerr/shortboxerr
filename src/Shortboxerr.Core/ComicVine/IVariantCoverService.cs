namespace Shortboxerr.Core.ComicVine;

/// <summary>
/// Service for detecting and managing variant covers for comic issues.
/// </summary>
public interface IVariantCoverService
{
    /// <summary>
    /// Get all variant covers for an issue.
    /// </summary>
    Task<IReadOnlyList<VariantCover>> GetVariantCoversAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch variant covers from ComicVine for an issue.
    /// </summary>
    Task<VariantCoverResult> FetchVariantCoversAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch variant covers for all issues in a series.
    /// </summary>
    Task<SeriesVariantCoverResult> FetchSeriesVariantCoversAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect if an image URL appears to be a variant cover based on naming patterns.
    /// </summary>
    VariantDetectionResult DetectVariant(string? caption, string? imageTags, string? filename);

    /// <summary>
    /// Get issues with variant covers in a series.
    /// </summary>
    Task<IReadOnlyList<IssueWithVariants>> GetIssuesWithVariantsAsync(int seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the preferred cover for an issue (main or variant).
    /// </summary>
    Task SetPreferredCoverAsync(int issueId, int? variantCoverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get variant cover statistics for a series.
    /// </summary>
    Task<VariantCoverStats> GetSeriesStatsAsync(int seriesId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a variant cover for a comic issue.
/// </summary>
public record VariantCover
{
    /// <summary>
    /// Internal database ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Parent issue ID.
    /// </summary>
    public int IssueId { get; init; }

    /// <summary>
    /// ComicVine image ID.
    /// </summary>
    public int ComicVineImageId { get; init; }

    /// <summary>
    /// Original image URL.
    /// </summary>
    public required string ImageUrl { get; init; }

    /// <summary>
    /// Caption/description from ComicVine.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Detected variant type (e.g., "Variant", "1:25 Incentive", "SDCC Exclusive").
    /// </summary>
    public string? VariantType { get; init; }

    /// <summary>
    /// Whether this is the main/primary cover (not a variant).
    /// </summary>
    public bool IsPrimaryCover { get; init; }

    /// <summary>
    /// Whether this is the user's preferred cover for display.
    /// </summary>
    public bool IsPreferred { get; init; }

    /// <summary>
    /// When this variant cover was detected/added.
    /// </summary>
    public DateTime DetectedAt { get; init; }
}

/// <summary>
/// Result of fetching variant covers for an issue.
/// </summary>
public record VariantCoverResult
{
    /// <summary>
    /// Whether the fetch was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Issue ID.
    /// </summary>
    public int IssueId { get; init; }

    /// <summary>
    /// Total images found.
    /// </summary>
    public int TotalImagesFound { get; init; }

    /// <summary>
    /// Number of variant covers detected.
    /// </summary>
    public int VariantsDetected { get; init; }

    /// <summary>
    /// The detected variant covers.
    /// </summary>
    public IReadOnlyList<VariantCover> Variants { get; init; } = Array.Empty<VariantCover>();

    public static VariantCoverResult Succeeded(int issueId, int totalImages, IReadOnlyList<VariantCover> variants)
        => new()
        {
            Success = true,
            IssueId = issueId,
            TotalImagesFound = totalImages,
            VariantsDetected = variants.Count(v => !v.IsPrimaryCover),
            Variants = variants
        };

    public static VariantCoverResult Failed(int issueId, string error)
        => new()
        {
            Success = false,
            IssueId = issueId,
            Error = error
        };
}

/// <summary>
/// Result of fetching variant covers for a series.
/// </summary>
public record SeriesVariantCoverResult
{
    /// <summary>
    /// Whether the fetch was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Series ID.
    /// </summary>
    public int SeriesId { get; init; }

    /// <summary>
    /// Number of issues processed.
    /// </summary>
    public int IssuesProcessed { get; init; }

    /// <summary>
    /// Number of issues with variants found.
    /// </summary>
    public int IssuesWithVariants { get; init; }

    /// <summary>
    /// Total variant covers detected across all issues.
    /// </summary>
    public int TotalVariantsDetected { get; init; }

    /// <summary>
    /// Results per issue.
    /// </summary>
    public IReadOnlyList<VariantCoverResult> IssueResults { get; init; } = Array.Empty<VariantCoverResult>();
}

/// <summary>
/// Result of variant detection for a single image.
/// </summary>
public record VariantDetectionResult
{
    /// <summary>
    /// Whether this appears to be a variant cover.
    /// </summary>
    public bool IsVariant { get; init; }

    /// <summary>
    /// Detected variant type.
    /// </summary>
    public string? VariantType { get; init; }

    /// <summary>
    /// Confidence score (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Matched patterns that led to this detection.
    /// </summary>
    public IReadOnlyList<string> MatchedPatterns { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Issue with its variant cover information.
/// </summary>
public record IssueWithVariants
{
    /// <summary>
    /// Issue ID.
    /// </summary>
    public int IssueId { get; init; }

    /// <summary>
    /// Issue number.
    /// </summary>
    public decimal IssueNumber { get; init; }

    /// <summary>
    /// Issue title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Main cover URL.
    /// </summary>
    public string? MainCoverUrl { get; init; }

    /// <summary>
    /// Number of variant covers.
    /// </summary>
    public int VariantCount { get; init; }

    /// <summary>
    /// Variant covers for this issue.
    /// </summary>
    public IReadOnlyList<VariantCover> Variants { get; init; } = Array.Empty<VariantCover>();

    /// <summary>
    /// Currently preferred cover (null = main cover).
    /// </summary>
    public VariantCover? PreferredVariant { get; init; }
}

/// <summary>
/// Variant cover statistics for a series.
/// </summary>
public record VariantCoverStats
{
    /// <summary>
    /// Series ID.
    /// </summary>
    public int SeriesId { get; init; }

    /// <summary>
    /// Total number of issues in the series.
    /// </summary>
    public int TotalIssues { get; init; }

    /// <summary>
    /// Number of issues with variant covers.
    /// </summary>
    public int IssuesWithVariants { get; init; }

    /// <summary>
    /// Total number of variant covers detected.
    /// </summary>
    public int TotalVariants { get; init; }

    /// <summary>
    /// Average variants per issue (for issues that have variants).
    /// </summary>
    public double AverageVariantsPerIssue { get; init; }

    /// <summary>
    /// Breakdown by variant type.
    /// </summary>
    public IReadOnlyDictionary<string, int> VariantsByType { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// When variant data was last fetched.
    /// </summary>
    public DateTime? LastFetchedAt { get; init; }
}
