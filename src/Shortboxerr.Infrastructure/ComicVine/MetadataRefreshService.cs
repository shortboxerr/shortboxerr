using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of IMetadataRefreshService for refreshing metadata from ComicVine.
/// </summary>
public class MetadataRefreshService : IMetadataRefreshService
{
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly IEditionMetadataService _editionMetadataService;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MetadataRefreshService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MetadataRefreshService(
        ISeriesMetadataService seriesMetadataService,
        IEditionMetadataService editionMetadataService,
        ShortboxerrDbContext dbContext,
        ISettingsService settingsService,
        ILogger<MetadataRefreshService> logger)
    {
        _seriesMetadataService = seriesMetadataService;
        _editionMetadataService = editionMetadataService;
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc />
    public async Task<RefreshResult> RefreshSeriesAsync(
        int seriesId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series.FindAsync(new object[] { seriesId }, cancellationToken);
        if (series == null)
        {
            return new RefreshResult
            {
                Success = false,
                Error = $"Series {seriesId} not found",
                ItemId = seriesId,
                ItemType = "Series",
                ItemTitle = "Unknown"
            };
        }

        if (!series.ComicVineId.HasValue)
        {
            return new RefreshResult
            {
                Success = false,
                Error = "Series is not matched to ComicVine",
                ItemId = seriesId,
                ItemType = "Series",
                ItemTitle = series.Title
            };
        }

        // Check if refresh is needed (unless forced)
        var settings = await GetSettingsAsync(cancellationToken);
        if (!force && series.ComicVineLastUpdated.HasValue)
        {
            var timeSinceLastRefresh = DateTime.UtcNow - series.ComicVineLastUpdated.Value;
            if (timeSinceLastRefresh < settings.RefreshInterval)
            {
                return new RefreshResult
                {
                    Success = true,
                    ItemId = seriesId,
                    ItemType = "Series",
                    ItemTitle = series.Title,
                    MetadataChanged = false
                };
            }
        }

        try
        {
            // Refresh metadata
            var result = await _seriesMetadataService.RefreshSeriesMetadataAsync(
                seriesId, force, cancellationToken);

            // Refresh issues
            var issueResult = await RefreshSeriesIssuesAsync(seriesId, force, cancellationToken);

            var refreshResult = new RefreshResult
            {
                Success = result.Success,
                Error = result.Error,
                ItemId = seriesId,
                ItemType = "Series",
                ItemTitle = series.Title,
                MetadataChanged = result.MetadataChanged,
                UpdatedFields = new List<string>()
            };

            // Log the refresh event
            await LogRefreshEventAsync(refreshResult, "Manual", issueResult.NewIssuesDiscovered, cancellationToken);

            _logger.LogInformation(
                "Refreshed series {SeriesId} ({Title}), {NewIssues} new issues discovered",
                seriesId, series.Title, issueResult.NewIssuesDiscovered);

            return refreshResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing series {SeriesId}", seriesId);
            
            var errorResult = new RefreshResult
            {
                Success = false,
                Error = ex.Message,
                ItemId = seriesId,
                ItemType = "Series",
                ItemTitle = series.Title
            };
            
            await LogRefreshEventAsync(errorResult, "Manual", 0, cancellationToken);
            return errorResult;
        }
    }

    /// <inheritdoc />
    public async Task<BulkRefreshResult> RefreshAllSeriesAsync(
        bool force = false,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var matchedSeries = await _dbContext.Series
            .Where(s => s.ComicVineId != null)
            .ToListAsync(cancellationToken);

        var result = new BulkRefreshResult
        {
            Success = true,
            TotalProcessed = matchedSeries.Count
        };

        var currentProgress = new RefreshProgress { Total = matchedSeries.Count };

        foreach (var series in matchedSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentProgress.Current++;
            currentProgress.CurrentItem = series.Title;
            progress?.Report(currentProgress);

            var refreshResult = await RefreshSeriesAsync(series.Id, force, cancellationToken);
            
            if (refreshResult.Success)
            {
                result.Refreshed++;
                currentProgress.Refreshed++;
            }
            else
            {
                result.Errors++;
                currentProgress.Errors++;
            }

            result.Results.Add(refreshResult);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        _logger.LogInformation(
            "Bulk refresh completed: {Refreshed}/{Total} series in {Duration}",
            result.Refreshed, result.TotalProcessed, result.Duration);

        return result;
    }

    /// <inheritdoc />
    public async Task<BulkRefreshResult> RefreshStaleSeriesAsync(
        TimeSpan maxAge,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var cutoffDate = DateTime.UtcNow - maxAge;

        var staleSeries = await _dbContext.Series
            .Where(s => s.ComicVineId != null &&
                       (s.ComicVineLastUpdated == null || s.ComicVineLastUpdated < cutoffDate))
            .ToListAsync(cancellationToken);

        var settings = await GetSettingsAsync(cancellationToken);
        var seriesToProcess = staleSeries.Take(settings.MaxSeriesPerRun).ToList();

        var result = new BulkRefreshResult
        {
            Success = true,
            TotalProcessed = seriesToProcess.Count,
            Skipped = staleSeries.Count - seriesToProcess.Count
        };

        var currentProgress = new RefreshProgress { Total = seriesToProcess.Count };

        foreach (var series in seriesToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentProgress.Current++;
            currentProgress.CurrentItem = series.Title;
            progress?.Report(currentProgress);

            var refreshResult = await RefreshSeriesAsync(series.Id, force: false, cancellationToken);
            
            // Log as scheduled refresh
            await LogRefreshEventAsync(refreshResult, "Scheduled", 0, cancellationToken);

            if (refreshResult.Success)
            {
                result.Refreshed++;
                currentProgress.Refreshed++;
            }
            else
            {
                result.Errors++;
                currentProgress.Errors++;
            }

            result.Results.Add(refreshResult);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        _logger.LogInformation(
            "Stale series refresh completed: {Refreshed}/{Total} series, {Skipped} skipped (limit), in {Duration}",
            result.Refreshed, result.TotalProcessed, result.Skipped, result.Duration);

        return result;
    }

    /// <inheritdoc />
    public async Task<SeriesIssueRefreshResult> RefreshSeriesIssuesAsync(
        int seriesId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var series = await _dbContext.Series
            .Include(s => s.Issues)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new SeriesIssueRefreshResult
            {
                Success = false,
                Error = $"Series {seriesId} not found",
                SeriesId = seriesId
            };
        }

        if (!series.ComicVineId.HasValue)
        {
            return new SeriesIssueRefreshResult
            {
                Success = false,
                Error = "Series is not matched to ComicVine",
                SeriesId = seriesId
            };
        }

        try
        {
            // Sync issues from ComicVine
            var syncResult = await _seriesMetadataService.SyncIssuesFromComicVineAsync(seriesId, cancellationToken);

            return new SeriesIssueRefreshResult
            {
                Success = syncResult.Success,
                Error = syncResult.Error,
                SeriesId = seriesId,
                TotalIssues = syncResult.TotalIssues,
                NewIssuesDiscovered = syncResult.IssuesAdded,
                IssuesUpdated = syncResult.IssuesUpdated,
                NewIssueIds = new List<int>() // IDs not tracked in IssueSyncResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing issues for series {SeriesId}", seriesId);
            return new SeriesIssueRefreshResult
            {
                Success = false,
                Error = ex.Message,
                SeriesId = seriesId
            };
        }
    }

    /// <inheritdoc />
    public async Task<RefreshResult> RefreshEditionAsync(
        int editionId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var edition = await _dbContext.EditionTitles.FindAsync(new object[] { editionId }, cancellationToken);
        if (edition == null)
        {
            return new RefreshResult
            {
                Success = false,
                Error = $"Edition {editionId} not found",
                ItemId = editionId,
                ItemType = "Edition",
                ItemTitle = "Unknown"
            };
        }

        if (!edition.ComicVineId.HasValue)
        {
            return new RefreshResult
            {
                Success = false,
                Error = "Edition is not matched to ComicVine",
                ItemId = editionId,
                ItemType = "Edition",
                ItemTitle = edition.Title
            };
        }

        try
        {
            var result = await _editionMetadataService.RefreshEditionMetadataAsync(
                editionId, force, cancellationToken);

            var refreshResult = new RefreshResult
            {
                Success = result.Success,
                Error = result.Error,
                ItemId = editionId,
                ItemType = "Edition",
                ItemTitle = edition.Title,
                MetadataChanged = result.MetadataSynced
            };

            await LogRefreshEventAsync(refreshResult, "Manual", 0, cancellationToken);
            return refreshResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing edition {EditionId}", editionId);
            
            var errorResult = new RefreshResult
            {
                Success = false,
                Error = ex.Message,
                ItemId = editionId,
                ItemType = "Edition",
                ItemTitle = edition.Title
            };
            
            await LogRefreshEventAsync(errorResult, "Manual", 0, cancellationToken);
            return errorResult;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Core.ComicVine.MetadataRefreshEvent>> GetSeriesRefreshHistoryAsync(
        int seriesId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var events = await _dbContext.MetadataRefreshEvents
            .Where(e => e.ItemType == "Series" && e.ItemId == seriesId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return events.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Core.ComicVine.MetadataRefreshEvent>> GetRecentRefreshEventsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var events = await _dbContext.MetadataRefreshEvents
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return events.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MetadataRefreshSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync<MetadataRefreshSettings>(
            "metadata_refresh", new MetadataRefreshSettings(), cancellationToken);
        
        return settings ?? new MetadataRefreshSettings();
    }

    /// <inheritdoc />
    public async Task<int> GetStaleSeriesCountAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var cutoffDate = DateTime.UtcNow - settings.RefreshInterval;

        return await _dbContext.Series
            .Where(s => s.ComicVineId != null &&
                       (s.ComicVineLastUpdated == null || s.ComicVineLastUpdated < cutoffDate))
            .CountAsync(cancellationToken);
    }

    #region Private Methods

    private async Task LogRefreshEventAsync(
        RefreshResult result,
        string source,
        int newIssuesDiscovered,
        CancellationToken cancellationToken)
    {
        var eventEntity = new Core.Entities.MetadataRefreshEvent
        {
            ItemType = result.ItemType,
            ItemId = result.ItemId,
            ItemTitle = result.ItemTitle,
            Success = result.Success,
            Error = result.Error,
            MetadataChanged = result.MetadataChanged,
            UpdatedFieldsJson = result.UpdatedFields.Any() 
                ? JsonSerializer.Serialize(result.UpdatedFields, _jsonOptions) 
                : null,
            NewIssuesDiscovered = newIssuesDiscovered,
            Source = source
        };

        _dbContext.MetadataRefreshEvents.Add(eventEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Core.ComicVine.MetadataRefreshEvent MapToDto(Core.Entities.MetadataRefreshEvent entity)
    {
        return new Core.ComicVine.MetadataRefreshEvent
        {
            Id = entity.Id,
            ItemType = entity.ItemType,
            ItemId = entity.ItemId,
            ItemTitle = entity.ItemTitle,
            Success = entity.Success,
            Error = entity.Error,
            MetadataChanged = entity.MetadataChanged,
            UpdatedFieldsJson = entity.UpdatedFieldsJson,
            NewIssuesDiscovered = entity.NewIssuesDiscovered,
            CreatedAt = entity.CreatedAt,
            Source = entity.Source
        };
    }

    #endregion
}

