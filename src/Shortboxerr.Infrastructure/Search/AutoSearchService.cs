using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Search;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Search;

/// <summary>
/// Service for automatic searching of wanted issues.
/// Integrates with DDL providers to find and queue downloads.
/// </summary>
public class AutoSearchService : IAutoSearchService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly IDdlSearchService _ddlSearchService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AutoSearchService> _logger;
    
    private static readonly List<AutoSearchHistoryEntry> _searchHistory = new();
    private static readonly object _historyLock = new();
    private static DateTime? _lastRunAt;
    private static bool _isRunning;
    private static int _todaySearchCount;
    private static int _todayFoundCount;
    private static DateTime _todayDate = DateTime.UtcNow.Date;
    
    public AutoSearchService(
        ShortboxerrDbContext dbContext,
        IDdlSearchService ddlSearchService,
        ISettingsService settingsService,
        ILogger<AutoSearchService> logger)
    {
        _dbContext = dbContext;
        _ddlSearchService = ddlSearchService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<AutoSearchResult> SearchIssueAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var issue = await _dbContext.Issues
            .Include(i => i.Series)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);
        
        if (issue == null)
        {
            return AutoSearchResult.Failed(issueId, "Unknown", "?", "Issue not found", stopwatch.Elapsed);
        }
        
        var seriesTitle = issue.Series?.Title ?? "Unknown Series";
        var issueNumber = issue.IssueNumberText ?? issue.IssueNumber.ToString();
        
        _logger.LogInformation("Searching for {Series} #{Issue}", seriesTitle, issueNumber);
        
        try
        {
            var query = new DdlSearchQuery
            {
                SeriesTitle = seriesTitle,
                IssueNumber = issue.IssueNumber,
                Year = issue.ReleaseDate?.Year ?? issue.CoverDate?.Year
            };
            
            var searchResult = await _ddlSearchService.SearchAllAsync(query, cancellationToken);
            
            // Update search tracking
            issue.LastSearchedAt = DateTime.UtcNow;
            issue.SearchAttempts++;
            
            if (searchResult.AllCandidates.Count > 0)
            {
                // Get the best candidate (first one after deduplication/sorting)
                var bestCandidate = searchResult.AllCandidates.First();
                
                _logger.LogInformation("Found {Count} candidates for {Series} #{Issue}, best: {Title}",
                    searchResult.AllCandidates.Count, seriesTitle, issueNumber, bestCandidate.ReleaseTitle);
                
                issue.LastSearchError = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                stopwatch.Stop();
                
                var result = AutoSearchResult.Found(
                    issueId, seriesTitle, issueNumber,
                    searchResult.AllCandidates.Count,
                    bestCandidate.ReleaseTitle,
                    null, // Download not initiated automatically - requires user confirmation or download client
                    stopwatch.Elapsed);
                
                AddToHistory(result);
                IncrementTodayStats(found: true);
                
                return result;
            }
            else
            {
                _logger.LogDebug("No candidates found for {Series} #{Issue}", seriesTitle, issueNumber);
                
                issue.LastSearchError = "No candidates found";
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                stopwatch.Stop();
                
                var result = AutoSearchResult.NotFound(issueId, seriesTitle, issueNumber, stopwatch.Elapsed);
                AddToHistory(result);
                IncrementTodayStats(found: false);
                
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for {Series} #{Issue}", seriesTitle, issueNumber);
            
            issue.LastSearchedAt = DateTime.UtcNow;
            issue.SearchAttempts++;
            issue.LastSearchError = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            stopwatch.Stop();
            
            var result = AutoSearchResult.Failed(issueId, seriesTitle, issueNumber, ex.Message, stopwatch.Elapsed);
            AddToHistory(result);
            IncrementTodayStats(found: false);
            
            return result;
        }
    }

    public async Task<AutoSearchBatchResult> SearchSeriesWantedAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var wantedIssues = await GetWantedIssuesForSeries(seriesId, cancellationToken);
        
        if (wantedIssues.Count == 0)
        {
            return AutoSearchBatchResult.Empty;
        }
        
        var results = new List<AutoSearchResult>();
        var settings = await _settingsService.GetAsync<SearchSettings>(SearchSettings.SettingsKey, new(), cancellationToken);
        
        foreach (var issue in wantedIssues)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var result = await SearchIssueAsync(issue.IssueId, cancellationToken);
            results.Add(result);
            
            // Apply search delay
            if (settings.SearchDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.SearchDelaySeconds), cancellationToken);
            }
        }
        
        stopwatch.Stop();
        
        return new AutoSearchBatchResult
        {
            TotalSearched = results.Count,
            SuccessCount = results.Count(r => r.Success && r.CandidatesFound > 0),
            FailedCount = results.Count(r => !r.Success && r.Error != null),
            NotFoundCount = results.Count(r => !r.Success && r.Error == null),
            Results = results,
            TotalDuration = stopwatch.Elapsed
        };
    }

    public async Task<AutoSearchBatchResult> SearchAllWantedAsync(int? maxIssues = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (_isRunning)
        {
            _logger.LogWarning("Auto-search is already running, skipping");
            return new AutoSearchBatchResult
            {
                TotalSearched = 0,
                SuccessCount = 0,
                FailedCount = 0,
                NotFoundCount = 0,
                Results = Array.Empty<AutoSearchResult>(),
                TotalDuration = TimeSpan.Zero,
                Error = "Auto-search is already running"
            };
        }
        
        try
        {
            _isRunning = true;
            _lastRunAt = DateTime.UtcNow;
            
            var searchableIssues = await GetSearchableIssuesAsync(maxIssues, cancellationToken);
            
            if (searchableIssues.Count == 0)
            {
                _logger.LogDebug("No searchable issues found");
                return AutoSearchBatchResult.Empty;
            }
            
            _logger.LogInformation("Starting auto-search for {Count} issues", searchableIssues.Count);
            
            var results = new List<AutoSearchResult>();
            var settings = await _settingsService.GetAsync<SearchSettings>(SearchSettings.SettingsKey, new(), cancellationToken);
            
            foreach (var issue in searchableIssues)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                var result = await SearchIssueAsync(issue.IssueId, cancellationToken);
                results.Add(result);
                
                // Apply search delay
                if (settings.SearchDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(settings.SearchDelaySeconds), cancellationToken);
                }
            }
            
            stopwatch.Stop();
            
            var batchResult = new AutoSearchBatchResult
            {
                TotalSearched = results.Count,
                SuccessCount = results.Count(r => r.Success && r.CandidatesFound > 0),
                FailedCount = results.Count(r => !r.Success && r.Error != null),
                NotFoundCount = results.Count(r => !r.Success && r.Error == null),
                Results = results,
                TotalDuration = stopwatch.Elapsed
            };
            
            _logger.LogInformation("Auto-search completed: {Searched} searched, {Found} found, {Failed} failed in {Duration}",
                batchResult.TotalSearched, batchResult.SuccessCount, batchResult.FailedCount, batchResult.TotalDuration);
            
            return batchResult;
        }
        finally
        {
            _isRunning = false;
        }
    }

    public async Task<IReadOnlyList<WantedIssueInfo>> GetSearchableIssuesAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync<SearchSettings>(SearchSettings.SettingsKey, new(), cancellationToken);
        
        // Calculate the threshold for re-searching stale issues
        var staleThreshold = settings.StaleSearchThresholdDays > 0
            ? DateTime.UtcNow.AddDays(-settings.StaleSearchThresholdDays)
            : (DateTime?)null;
        
        var query = _dbContext.Issues
            .Include(i => i.Series)
            .Where(i => i.Status == IssueStatus.Wanted)
            .Where(i => i.Series != null && i.Series.Monitored)
            .Where(i => 
                // Never searched
                i.LastSearchedAt == null ||
                // Stale search (if threshold is configured)
                (staleThreshold != null && i.LastSearchedAt < staleThreshold))
            .OrderBy(i => i.LastSearchedAt ?? DateTime.MinValue) // Prioritize never-searched
            .ThenByDescending(i => i.ReleaseDate) // Then by release date (newer first)
            .Select(i => new WantedIssueInfo
            {
                IssueId = i.Id,
                SeriesId = i.SeriesId,
                SeriesTitle = i.Series!.Title,
                IssueNumber = i.IssueNumberText ?? i.IssueNumber.ToString(),
                IssueTitle = i.Title,
                ReleaseDate = i.ReleaseDate ?? i.StoreDate,
                LastSearchedAt = i.LastSearchedAt,
                SearchAttempts = i.SearchAttempts
            });
        
        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }
        
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<AutoSearchStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync<SearchSettings>(SearchSettings.SettingsKey, new(), cancellationToken);
        
        var wantedCount = await _dbContext.Issues
            .CountAsync(i => i.Status == IssueStatus.Wanted, cancellationToken);
        
        var searchableCount = await _dbContext.Issues
            .Include(i => i.Series)
            .CountAsync(i => 
                i.Status == IssueStatus.Wanted && 
                i.Series != null && 
                i.Series.Monitored &&
                (i.LastSearchedAt == null || 
                 (settings.StaleSearchThresholdDays > 0 && 
                  i.LastSearchedAt < DateTime.UtcNow.AddDays(-settings.StaleSearchThresholdDays))),
                cancellationToken);
        
        // Reset today's stats if it's a new day
        if (_todayDate != DateTime.UtcNow.Date)
        {
            _todayDate = DateTime.UtcNow.Date;
            _todaySearchCount = 0;
            _todayFoundCount = 0;
        }
        
        // Calculate next run time
        DateTime? nextRunAt = null;
        if (settings.AutoSearchEnabled && _lastRunAt.HasValue)
        {
            nextRunAt = _lastRunAt.Value.AddHours(settings.AutoSearchIntervalHours);
        }
        
        return new AutoSearchStatus
        {
            Enabled = settings.AutoSearchEnabled,
            IsRunning = _isRunning,
            WantedIssuesCount = wantedCount,
            SearchableCount = searchableCount,
            LastRunAt = _lastRunAt,
            NextRunAt = nextRunAt,
            TodaySearchCount = _todaySearchCount,
            TodayFoundCount = _todayFoundCount
        };
    }

    public Task<IReadOnlyList<AutoSearchHistoryEntry>> GetHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var history = _searchHistory
                .OrderByDescending(h => h.SearchedAt)
                .Take(limit)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<AutoSearchHistoryEntry>>(history);
        }
    }

    private async Task<List<WantedIssueInfo>> GetWantedIssuesForSeries(int seriesId, CancellationToken cancellationToken)
    {
        return await _dbContext.Issues
            .Include(i => i.Series)
            .Where(i => i.SeriesId == seriesId && i.Status == IssueStatus.Wanted)
            .Select(i => new WantedIssueInfo
            {
                IssueId = i.Id,
                SeriesId = i.SeriesId,
                SeriesTitle = i.Series!.Title,
                IssueNumber = i.IssueNumberText ?? i.IssueNumber.ToString(),
                IssueTitle = i.Title,
                ReleaseDate = i.ReleaseDate ?? i.StoreDate,
                LastSearchedAt = i.LastSearchedAt,
                SearchAttempts = i.SearchAttempts
            })
            .ToListAsync(cancellationToken);
    }

    private void AddToHistory(AutoSearchResult result)
    {
        lock (_historyLock)
        {
            _searchHistory.Add(new AutoSearchHistoryEntry
            {
                IssueId = result.IssueId,
                SeriesTitle = result.SeriesTitle,
                IssueNumber = result.IssueNumber,
                SearchedAt = DateTime.UtcNow,
                Found = result.Success && result.CandidatesFound > 0,
                CandidatesFound = result.CandidatesFound,
                SelectedCandidate = result.SelectedCandidateTitle,
                Error = result.Error
            });
            
            // Keep only last 1000 entries
            while (_searchHistory.Count > 1000)
            {
                _searchHistory.RemoveAt(0);
            }
        }
    }

    private static void IncrementTodayStats(bool found)
    {
        if (_todayDate != DateTime.UtcNow.Date)
        {
            _todayDate = DateTime.UtcNow.Date;
            _todaySearchCount = 0;
            _todayFoundCount = 0;
        }
        
        _todaySearchCount++;
        if (found) _todayFoundCount++;
    }
}
