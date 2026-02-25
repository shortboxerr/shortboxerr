using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Entities;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Activity;

/// <summary>
/// Service for persisting and managing download history in the database.
/// </summary>
public class DownloadHistoryService : IDownloadHistoryService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ILogger<DownloadHistoryService>? _logger;

    public DownloadHistoryService(ShortboxerrDbContext dbContext, ILogger<DownloadHistoryService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task AddAsync(DownloadHistory entry, CancellationToken cancellationToken = default)
    {
        _dbContext.DownloadHistories.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger?.LogDebug("Added download history entry for {Title}", entry.Title);
    }

    public async Task<IReadOnlyList<DownloadHistory>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadHistories
            .OrderByDescending(h => h.CompletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadHistory>> GetBySourceTypeAsync(DownloadSourceType sourceType, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadHistories
            .Where(h => h.SourceType == sourceType)
            .OrderByDescending(h => h.CompletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<DownloadHistory?> GetByDownloadIdAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadHistories
            .FirstOrDefaultAsync(h => h.DownloadId == downloadId, cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.DownloadHistories.CountAsync(cancellationToken);
        if (count > 0)
        {
            await _dbContext.DownloadHistories.ExecuteDeleteAsync(cancellationToken);
            _logger?.LogInformation("Cleared {Count} download history entries", count);
        }
        return count;
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.DownloadHistories
            .Where(h => h.State == DownloadHistoryState.Completed)
            .ExecuteDeleteAsync(cancellationToken);
        
        if (count > 0)
        {
            _logger?.LogInformation("Cleared {Count} completed download history entries", count);
        }
        return count;
    }

    public async Task<int> ClearBySourceTypeAsync(DownloadSourceType sourceType, CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.DownloadHistories
            .Where(h => h.SourceType == sourceType)
            .ExecuteDeleteAsync(cancellationToken);
        
        if (count > 0)
        {
            _logger?.LogInformation("Cleared {Count} {SourceType} download history entries", count, sourceType);
        }
        return count;
    }

    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.DownloadHistories.FindAsync(new object[] { id }, cancellationToken);
        if (entry == null)
            return false;

        _dbContext.DownloadHistories.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger?.LogDebug("Removed download history entry {Id}", id);
        return true;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DownloadHistories.CountAsync(cancellationToken);
    }
}
