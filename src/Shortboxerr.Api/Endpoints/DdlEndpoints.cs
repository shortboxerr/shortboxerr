using Microsoft.AspNetCore.Mvc;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for DDL search and download operations.
/// </summary>
public static class DdlEndpoints
{
    public static void MapDdlEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddl")
            .WithTags("DDL")
            .WithOpenApi();

        // Search DDL sites
        group.MapPost("/search", SearchDdl)
            .WithName("SearchDdl")
            .WithSummary("Search DDL sites for releases")
            .WithDescription("Searches all enabled DDL sites for comic releases matching the query. Returns full candidate data including download links.")
            .Produces<DdlSearchResponseDto>();

        // Quick search by series/issue
        group.MapGet("/search", QuickSearchDdl)
            .WithName("QuickSearchDdl")
            .WithSummary("Quick search DDL sites")
            .WithDescription("Searches DDL sites using query parameters.")
            .Produces<DdlSearchResponseDto>();

        // Grab/download a candidate
        group.MapPost("/grab", GrabDdl)
            .WithName("GrabDdl")
            .WithSummary("Grab/download a DDL candidate")
            .WithDescription("Initiates a download for a DDL candidate. Returns the download ID for tracking.")
            .Produces<DdlGrabResponseDto>();

        // Get active downloads
        group.MapGet("/downloads/active", GetActiveDownloads)
            .WithName("GetActiveDdlDownloads")
            .WithSummary("Get active DDL downloads")
            .WithDescription("Returns all currently active DDL downloads.")
            .Produces<List<DdlDownloadStatusDto>>();

        // Get download status
        group.MapGet("/downloads/{downloadId}", GetDownloadStatus)
            .WithName("GetDdlDownloadStatus")
            .WithSummary("Get DDL download status")
            .WithDescription("Returns the status of a specific download.")
            .Produces<DdlDownloadStatusDto>();

        // Cancel a download
        group.MapPost("/downloads/{downloadId}/cancel", CancelDownload)
            .WithName("CancelDdlDownload")
            .WithSummary("Cancel a DDL download")
            .WithDescription("Cancels an active download.")
            .Produces<object>();

        // Retry a stalled/failed download
        group.MapPost("/downloads/{downloadId}/retry", RetryDownload)
            .WithName("RetryDdlDownload")
            .WithSummary("Retry a stalled or failed DDL download")
            .WithDescription("Retries a stalled or failed download, resuming from where it left off if possible.")
            .Produces<DdlGrabResponseDto>();

        // Get download history
        group.MapGet("/downloads/history", GetDownloadHistory)
            .WithName("GetDdlDownloadHistory")
            .WithSummary("Get DDL download history")
            .WithDescription("Returns recent download history.")
            .Produces<List<DdlDownloadHistoryDto>>();

        // Get DDL activity feed (for UI activity page)
        group.MapGet("/activity-feed", GetDdlActivity)
            .WithName("GetDdlActivity")
            .WithSummary("Get DDL activity feed")
            .WithDescription("Returns recent DDL activity including active downloads, completed downloads, and failures.")
            .Produces<List<DdlActivityEventDto>>();

        // Clear DDL activity history
        group.MapDelete("/activity-feed", ClearDdlActivity)
            .WithName("ClearDdlActivity")
            .WithSummary("Clear DDL activity history")
            .WithDescription("Clears the DDL activity history (completed, failed, and cancelled downloads).")
            .Produces<object>();

        // Get latest releases from all sites
        group.MapGet("/latest", GetLatestReleases)
            .WithName("GetLatestDdlReleases")
            .WithSummary("Get latest DDL releases")
            .WithDescription("Returns the latest releases from all enabled DDL sites.")
            .Produces<DdlSearchResponseDto>();

        // Extract download links from a page
        group.MapPost("/extract-links", ExtractLinks)
            .WithName("ExtractDdlLinks")
            .WithSummary("Extract download links from a DDL page")
            .WithDescription("Extracts actual download links from a DDL source page URL.")
            .Produces<DdlLinkExtractionResponseDto>();
    }

    private static async Task<IResult> SearchDdl(
        [FromBody] DdlSearchRequestDto request,
        [FromServices] IDdlSearchService searchService,
        CancellationToken cancellationToken)
    {
        var query = new DdlSearchQuery
        {
            SeriesTitle = request.SeriesTitle,
            IssueNumber = request.IssueNumber,
            Year = request.Year,
            VolumeNumber = request.VolumeNumber,
            CollectionsOnly = request.CollectionsOnly,
            Limit = request.MaxResults ?? 50
        };

        var result = await searchService.SearchAllAsync(query, cancellationToken);
        return Results.Ok(MapToSearchResponse(result));
    }

    private static async Task<IResult> QuickSearchDdl(
        [FromQuery] string? series,
        [FromQuery] decimal? issue,
        [FromQuery] int? year,
        [FromQuery] int? volume,
        [FromQuery] int? limit,
        [FromServices] IDdlSearchService searchService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(series))
        {
            return Results.BadRequest(new { error = "Series title is required" });
        }

        var query = new DdlSearchQuery
        {
            SeriesTitle = series,
            IssueNumber = issue,
            Year = year,
            VolumeNumber = volume,
            Limit = limit ?? 50
        };

        var result = await searchService.SearchAllAsync(query, cancellationToken);
        return Results.Ok(MapToSearchResponse(result));
    }

    private static async Task<IResult> GrabDdl(
        [FromBody] DdlGrabRequestDto request,
        [FromServices] IDdlDownloadService downloadService,
        [FromServices] ISettingsService settingsService,
        [FromServices] IHistoryService historyService,
        CancellationToken cancellationToken)
    {
        // Reconstruct the candidate from the request
        var candidate = new DdlCandidate
        {
            Id = request.CandidateId,
            ReleaseTitle = request.ReleaseTitle,
            SourceSite = request.SourceSite,
            SourceUrl = request.SourceUrl,
            ParsedInfo = new DdlParsedInfo
            {
                SeriesTitle = request.SeriesTitle,
                IssueNumber = request.IssueNumber,
                Year = request.Year
            },
            DownloadLinks = request.DownloadLinks.Select(l => new DdlDownloadLink
            {
                Url = l.Url,
                LinkType = Enum.TryParse<DdlLinkType>(l.LinkType, true, out var linkType) ? linkType : DdlLinkType.Direct,
                HostName = l.HostName,
                Priority = l.Priority
            }).ToList()
        };

        if (candidate.DownloadLinks.Count == 0)
        {
            return Results.BadRequest(new { error = "No download links provided" });
        }

        // Get download folder from settings, fall back to temp if not configured
        var generalSettings = await settingsService.GetGeneralSettingsAsync(cancellationToken);
        var defaultFolder = !string.IsNullOrEmpty(generalSettings.DownloadFolder) 
            ? generalSettings.DownloadFolder 
            : Path.GetTempPath();
        var downloadFolder = request.DestinationFolder ?? defaultFolder;

        // Ensure the download folder exists
        if (!Directory.Exists(downloadFolder))
        {
            try
            {
                Directory.CreateDirectory(downloadFolder);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Cannot create download folder: {ex.Message}" });
            }
        }

        var options = new DdlDownloadOptions
        {
            DestinationFolder = downloadFolder,
            CustomFilename = request.CustomFilename,
            VerifyDownload = true
        };

        var result = await downloadService.DownloadAsync(candidate, options, cancellationToken);

        // Record "grabbed" history event when download is initiated
        var title = !string.IsNullOrEmpty(request.SeriesTitle) && request.IssueNumber.HasValue
            ? $"{request.SeriesTitle} #{request.IssueNumber}"
            : request.ReleaseTitle;
        
        await historyService.RecordDownloadGrabbedAsync(
            title: title,
            source: request.SourceSite,
            seriesId: null,
            issueId: null,
            additionalData: null,
            cancellationToken: cancellationToken);

        return Results.Ok(new DdlGrabResponseDto
        {
            Success = result.Success,
            DownloadId = result.DownloadId,
            FilePath = result.FilePath,
            FileName = result.FileName,
            FileSize = result.FileSize,
            DurationMs = (int)result.Duration.TotalMilliseconds,
            ErrorMessage = result.ErrorMessage,
            FailureReason = result.Success ? null : result.FailureReason.ToString()
        });
    }

    private static IResult GetActiveDownloads(
        [FromServices] IDdlDownloadService downloadService)
    {
        var downloads = downloadService.GetActiveDownloads();
        return Results.Ok(downloads.Select(MapToStatusDto).ToList());
    }

    private static IResult GetDownloadStatus(
        string downloadId,
        [FromServices] IDdlDownloadService downloadService)
    {
        var status = downloadService.GetDownloadStatus(downloadId);
        if (status == null)
        {
            return Results.NotFound(new { error = "Download not found" });
        }

        return Results.Ok(MapToStatusDto(status));
    }

    private static IResult CancelDownload(
        string downloadId,
        [FromServices] IDdlDownloadService downloadService)
    {
        var cancelled = downloadService.CancelDownload(downloadId);
        if (!cancelled)
        {
            return Results.NotFound(new { error = "Download not found or already completed" });
        }

        return Results.Ok(new { message = "Download cancelled", downloadId });
    }

    private static async Task<IResult> RetryDownload(
        string downloadId,
        [FromServices] IDdlDownloadService downloadService,
        CancellationToken cancellationToken)
    {
        var result = await downloadService.RetryDownloadAsync(downloadId, cancellationToken);
        if (result == null)
        {
            return Results.NotFound(new { error = "Download not found or cannot be retried" });
        }

        return Results.Ok(new DdlGrabResponseDto
        {
            Success = result.Success,
            DownloadId = result.DownloadId,
            FilePath = result.FilePath,
            FileName = result.FileName,
            FileSize = result.FileSize,
            DurationMs = (int)result.Duration.TotalMilliseconds,
            ErrorMessage = result.ErrorMessage,
            FailureReason = result.Success ? null : result.FailureReason.ToString()
        });
    }

    private static IResult GetDownloadHistory(
        [FromQuery] int? limit,
        [FromServices] IDdlDownloadService downloadService)
    {
        var history = downloadService.GetDownloadHistory(limit ?? 50);
        return Results.Ok(history.Select(h => new DdlDownloadHistoryDto
        {
            Id = h.Id,
            DownloadId = h.DownloadId,
            SourceUrl = h.SourceUrl,
            SourceSite = h.SourceSite,
            ReleaseTitle = h.ReleaseTitle,
            DestinationPath = h.DestinationPath,
            FileSize = h.FileSize,
            Success = h.Success,
            FailureReason = h.FailureReason?.ToString(),
            ErrorMessage = h.ErrorMessage,
            RetryAttempts = h.RetryAttempts,
            DurationMs = (int)h.Duration.TotalMilliseconds,
            StartedAt = h.StartedAt,
            CompletedAt = h.CompletedAt
        }).ToList());
    }

    private static IResult GetDdlActivity(
        [FromQuery] string? filter,
        [FromQuery] int? limit,
        [FromServices] IDdlDownloadService downloadService)
    {
        var events = new List<DdlActivityEventDto>();
        var maxEvents = limit ?? 50;
        
        // Get active downloads
        var activeDownloads = downloadService.GetActiveDownloads();
        foreach (var download in activeDownloads)
        {
            var eventType = download.State switch
            {
                DdlDownloadState.Downloading => "download_started",
                DdlDownloadState.Stalled => "download_started",
                DdlDownloadState.Retrying => "download_started",
                _ => "download_started"
            };
            
            var status = download.State switch
            {
                DdlDownloadState.Downloading => "info",
                DdlDownloadState.Stalled => "warning",
                DdlDownloadState.Retrying => "warning",
                _ => "info"
            };
            
            var details = download.State == DdlDownloadState.Downloading
                ? $"{download.ProgressPercent:F0}% - {FormatBytes(download.BytesPerSecond)}/s"
                : download.State.ToString();
            
            events.Add(new DdlActivityEventDto
            {
                Id = download.DownloadId,
                Type = eventType,
                Title = Path.GetFileNameWithoutExtension(download.DestinationPath) ?? "Unknown",
                Provider = "GetComics",
                Timestamp = FormatRelativeTime(download.StartedAt),
                Details = details,
                Status = status
            });
        }
        
        // Get download history
        var history = downloadService.GetDownloadHistory(maxEvents);
        foreach (var entry in history)
        {
            // Apply filter
            if (filter == "download" && entry.Success) continue;
            if (filter == "failed" && entry.Success) continue;
            
            var eventType = entry.Success ? "download_complete" : "download_failed";
            var status = entry.Success ? "success" : "error";
            
            var details = entry.Success
                ? $"Downloaded {FormatBytes(entry.FileSize)}"
                : entry.ErrorMessage ?? entry.FailureReason?.ToString() ?? "Unknown error";
            
            events.Add(new DdlActivityEventDto
            {
                Id = entry.Id,
                Type = eventType,
                Title = entry.ReleaseTitle ?? Path.GetFileNameWithoutExtension(entry.DestinationPath) ?? "Unknown",
                Provider = entry.SourceSite ?? "GetComics",
                Timestamp = FormatRelativeTime(entry.CompletedAt),
                Details = details,
                Status = status
            });
        }
        
        // Sort by most recent first (active downloads first, then by timestamp)
        var sorted = events
            .OrderByDescending(e => e.Type == "download_started")
            .ThenByDescending(e => e.Timestamp)
            .Take(maxEvents)
            .ToList();
        
        return Results.Ok(sorted);
    }

    private static async Task<IResult> ClearDdlActivity(
        [FromServices] IDownloadHistoryService? historyService,
        [FromServices] IDdlDownloadService downloadService,
        CancellationToken cancellationToken)
    {
        var count = 0;
        
        // Clear persisted DDL history
        if (historyService != null)
        {
            count = await historyService.ClearBySourceTypeAsync(DownloadSourceType.Ddl, cancellationToken);
        }
        
        // Also clear the in-memory history in DdlDownloadService
        downloadService.ClearHistory();
        
        return Results.Ok(new { success = true, removedCount = count });
    }
    
    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024:F1} KB";
        return $"{bytes:F0} B";
    }
    
    private static string FormatBytes(long bytes)
    {
        return FormatBytes((double)bytes);
    }
    
    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToString("MMM d");
    }

    private static async Task<IResult> GetLatestReleases(
        [FromQuery] int? limit,
        [FromServices] IDdlSearchService searchService,
        CancellationToken cancellationToken)
    {
        var result = await searchService.GetLatestFromAllAsync(limit ?? 20, cancellationToken);
        return Results.Ok(MapToSearchResponse(result));
    }

    private static async Task<IResult> ExtractLinks(
        [FromBody] DdlExtractLinksRequestDto request,
        [FromServices] IDdlSearchService searchService,
        CancellationToken cancellationToken)
    {
        var result = await searchService.ExtractLinksAsync(request.SiteType, request.PageUrl, cancellationToken);
        return Results.Ok(new DdlLinkExtractionResponseDto
        {
            Success = result.Success,
            SourceUrl = result.SourceUrl,
            Links = result.Links.Select(l => new DdlDownloadLinkDto
            {
                Url = l.Url,
                LinkType = l.LinkType.ToString(),
                HostName = l.HostName,
                IsVerified = l.IsVerified,
                Priority = l.Priority
            }).ToList(),
            DeadLinks = result.DeadLinks.ToList(),
            ErrorMessage = result.ErrorMessage
        });
    }

    private static DdlSearchResponseDto MapToSearchResponse(DdlAggregatedSearchResult result)
    {
        return new DdlSearchResponseDto
        {
            Success = result.Success,
            TotalCandidates = result.AllCandidates.Count,
            TotalRawCandidates = result.TotalRawCandidates,
            DuplicatesRemoved = result.DuplicatesRemoved,
            SuccessfulSites = result.SuccessfulSites.ToList(),
            FailedSites = result.FailedSites.ToList(),
            Warnings = result.Warnings.ToList(),
            DurationMs = (int)result.TotalDuration.TotalMilliseconds,
            Candidates = result.AllCandidates.Select(c => new DdlCandidateDto
            {
                Id = c.Id,
                ReleaseTitle = c.ReleaseTitle,
                SourceSite = c.SourceSite,
                SourceUrl = c.SourceUrl,
                Size = c.Size,
                DateFound = c.DateFound,
                QualityScore = c.QualityScore,
                Tags = c.Tags,
                ParsedInfo = new DdlParsedInfoDto
                {
                    SeriesTitle = c.ParsedInfo.SeriesTitle,
                    IssueNumber = c.ParsedInfo.IssueNumber,
                    VolumeNumber = c.ParsedInfo.VolumeNumber,
                    Year = c.ParsedInfo.Year,
                    Publisher = c.ParsedInfo.Publisher,
                    Format = c.ParsedInfo.Format,
                    IsCollection = c.ParsedInfo.IsCollection,
                    EditionType = c.ParsedInfo.EditionType,
                    Quality = c.ParsedInfo.Quality,
                    Confidence = c.ParsedInfo.Confidence
                },
                DownloadLinks = c.DownloadLinks.Select(l => new DdlDownloadLinkDto
                {
                    Url = l.Url,
                    LinkType = l.LinkType.ToString(),
                    HostName = l.HostName,
                    IsVerified = l.IsVerified,
                    Priority = l.Priority
                }).ToList()
            }).ToList()
        };
    }

    private static DdlDownloadStatusDto MapToStatusDto(DdlDownloadStatus status)
    {
        return new DdlDownloadStatusDto
        {
            DownloadId = status.DownloadId,
            SourceUrl = status.SourceUrl,
            DestinationPath = status.DestinationPath,
            State = status.State.ToString(),
            TotalBytes = status.TotalBytes,
            BytesDownloaded = status.BytesDownloaded,
            ProgressPercent = status.ProgressPercent,
            BytesPerSecond = status.BytesPerSecond,
            StartedAt = status.StartedAt,
            CurrentRetry = status.CurrentRetry,
            LastError = status.LastError
        };
    }
}

// Request/Response DTOs

public record DdlSearchRequestDto
{
    public required string SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? Year { get; init; }
    public int? VolumeNumber { get; init; }
    public bool CollectionsOnly { get; init; }
    public int? MaxResults { get; init; }
}

public record DdlSearchResponseDto
{
    public bool Success { get; init; }
    public int TotalCandidates { get; init; }
    public int TotalRawCandidates { get; init; }
    public int DuplicatesRemoved { get; init; }
    public List<string> SuccessfulSites { get; init; } = new();
    public List<string> FailedSites { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public int DurationMs { get; init; }
    public List<DdlCandidateDto> Candidates { get; init; } = new();
}

public record DdlCandidateDto
{
    public required string Id { get; init; }
    public required string ReleaseTitle { get; init; }
    public required string SourceSite { get; init; }
    public string? SourceUrl { get; init; }
    public long? Size { get; init; }
    public DateTime DateFound { get; init; }
    public int QualityScore { get; init; }
    public List<string> Tags { get; init; } = new();
    public required DdlParsedInfoDto ParsedInfo { get; init; }
    public List<DdlDownloadLinkDto> DownloadLinks { get; init; } = new();
}

public record DdlParsedInfoDto
{
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? VolumeNumber { get; init; }
    public int? Year { get; init; }
    public string? Publisher { get; init; }
    public string? Format { get; init; }
    public bool IsCollection { get; init; }
    public string? EditionType { get; init; }
    public string? Quality { get; init; }
    public int Confidence { get; init; }
}

public record DdlDownloadLinkDto
{
    public required string Url { get; init; }
    public required string LinkType { get; init; }
    public string? HostName { get; init; }
    public bool IsVerified { get; init; }
    public int Priority { get; init; }
}

public record DdlGrabRequestDto
{
    public required string CandidateId { get; init; }
    public required string ReleaseTitle { get; init; }
    public required string SourceSite { get; init; }
    public string? SourceUrl { get; init; }
    public string? SeriesTitle { get; init; }
    public decimal? IssueNumber { get; init; }
    public int? Year { get; init; }
    public required List<DdlDownloadLinkDto> DownloadLinks { get; init; }
    public string? DestinationFolder { get; init; }
    public string? CustomFilename { get; init; }
}

public record DdlGrabResponseDto
{
    public bool Success { get; init; }
    public required string DownloadId { get; init; }
    public string? FilePath { get; init; }
    public string? FileName { get; init; }
    public long FileSize { get; init; }
    public int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FailureReason { get; init; }
}

public record DdlDownloadStatusDto
{
    public required string DownloadId { get; init; }
    public required string SourceUrl { get; init; }
    public required string DestinationPath { get; init; }
    public required string State { get; init; }
    public long? TotalBytes { get; init; }
    public long BytesDownloaded { get; init; }
    public double ProgressPercent { get; init; }
    public double BytesPerSecond { get; init; }
    public DateTime StartedAt { get; init; }
    public int CurrentRetry { get; init; }
    public string? LastError { get; init; }
}

public record DdlDownloadHistoryDto
{
    public required string Id { get; init; }
    public required string DownloadId { get; init; }
    public required string SourceUrl { get; init; }
    public string? SourceSite { get; init; }
    public string? ReleaseTitle { get; init; }
    public string? DestinationPath { get; init; }
    public long FileSize { get; init; }
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryAttempts { get; init; }
    public int DurationMs { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
}

public record DdlExtractLinksRequestDto
{
    public required string SiteType { get; init; }
    public required string PageUrl { get; init; }
}

public record DdlLinkExtractionResponseDto
{
    public bool Success { get; init; }
    public required string SourceUrl { get; init; }
    public List<DdlDownloadLinkDto> Links { get; init; } = new();
    public List<string> DeadLinks { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

public record DdlActivityEventDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Provider { get; init; }
    public required string Timestamp { get; init; }
    public string? Details { get; init; }
    public required string Status { get; init; }
}
