using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Mylar3Migration;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Mylar3Migration;

/// <summary>
/// Service for migrating data from Mylar3 to Shortboxerr.
/// </summary>
public class Mylar3MigrationService : IMylar3MigrationService
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly ILogger<Mylar3MigrationService> _logger;

    public Mylar3MigrationService(
        ShortboxerrDbContext dbContext,
        ISeriesMetadataService seriesMetadataService,
        ILogger<Mylar3MigrationService> logger)
    {
        _dbContext = dbContext;
        _seriesMetadataService = seriesMetadataService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Mylar3Snapshot> AnalyzeDatabaseAsync(
        string dbPath,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new Mylar3Snapshot
        {
            Success = true,
            SourcePath = dbPath,
            AnalyzedAt = DateTime.UtcNow
        };

        if (!File.Exists(dbPath))
        {
            return new Mylar3Snapshot
            {
                Success = false,
                Error = $"Database file not found: {dbPath}"
            };
        }

        try
        {
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Discover tables
            snapshot.TablesFound = await GetTablesAsync(connection, cancellationToken);

            // Read series
            if (snapshot.TablesFound.Contains("comics"))
            {
                snapshot.Series = await ReadSeriesAsync(connection, cancellationToken);
                snapshot.Stats.TotalSeries = snapshot.Series.Count;
                snapshot.Stats.SeriesWithComicVineId = snapshot.Series.Count(s => s.ComicVineId.HasValue);
            }
            else
            {
                snapshot.Warnings.Add("Table 'comics' not found in Mylar3 database");
            }

            // Read issues
            if (snapshot.TablesFound.Contains("issues"))
            {
                snapshot.Issues = await ReadIssuesAsync(connection, cancellationToken);
                snapshot.Stats.TotalIssues = snapshot.Issues.Count;
                snapshot.Stats.IssuesWithComicVineId = snapshot.Issues.Count(i => i.ComicVineId.HasValue);
                snapshot.Stats.WantedIssues = snapshot.Issues.Count(i => 
                    i.Status?.Equals("Wanted", StringComparison.OrdinalIgnoreCase) == true);
                snapshot.Stats.DownloadedIssues = snapshot.Issues.Count(i => 
                    i.Status?.Equals("Downloaded", StringComparison.OrdinalIgnoreCase) == true ||
                    !string.IsNullOrEmpty(i.Location));
            }
            else
            {
                snapshot.Warnings.Add("Table 'issues' not found in Mylar3 database");
            }

            // Read file associations (from issues with locations or separate files table)
            snapshot.Files = await ReadFilesAsync(connection, snapshot.Issues, cancellationToken);
            snapshot.Stats.TotalFiles = snapshot.Files.Count;

            _logger.LogInformation(
                "Analyzed Mylar3 database: {Series} series, {Issues} issues, {Files} files",
                snapshot.Stats.TotalSeries,
                snapshot.Stats.TotalIssues,
                snapshot.Stats.TotalFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing Mylar3 database: {Path}", dbPath);
            snapshot.Success = false;
            snapshot.Error = ex.Message;
        }

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<string> ExportSnapshotAsync(
        Mylar3Snapshot snapshot,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(snapshot, options);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        _logger.LogInformation("Exported Mylar3 snapshot to: {Path}", outputPath);
        return outputPath;
    }

    /// <inheritdoc />
    public async Task<Mylar3MigrationResult> ImportAsync(
        Mylar3Snapshot snapshot,
        Mylar3MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new Mylar3MigrationResult
        {
            Success = true,
            StartedAt = DateTime.UtcNow,
            WasDryRun = options.DryRun
        };

        try
        {
            // Map Mylar3 series IDs to Shortboxerr series IDs
            var seriesIdMap = new Dictionary<string, int>(); // Mylar3 ComicId -> Shortboxerr SeriesId

            if (options.ImportSeries)
            {
                await ImportSeriesAsync(snapshot, options, result, seriesIdMap, cancellationToken);
            }

            if (options.ImportIssues)
            {
                await ImportIssuesAsync(snapshot, options, result, seriesIdMap, cancellationToken);
            }

            if (!options.DryRun)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Migration {Status}: {SeriesImported}/{SeriesProcessed} series, {IssuesImported}/{IssuesProcessed} issues",
                options.DryRun ? "dry-run complete" : "complete",
                result.SeriesImported, result.SeriesProcessed,
                result.IssuesImported, result.IssuesProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    /// <inheritdoc />
    public async Task<Mylar3MigrationResult> MigrateAsync(
        string dbPath,
        Mylar3MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await AnalyzeDatabaseAsync(dbPath, cancellationToken);
        
        if (!snapshot.Success)
        {
            return new Mylar3MigrationResult
            {
                Success = false,
                Error = snapshot.Error,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
        }

        return await ImportAsync(snapshot, options, cancellationToken);
    }

    #region Private Methods

    private async Task<List<string>> GetTablesAsync(SqliteConnection connection, CancellationToken ct)
    {
        var tables = new List<string>();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private async Task<List<Mylar3Series>> ReadSeriesAsync(SqliteConnection connection, CancellationToken ct)
    {
        var series = new List<Mylar3Series>();
        var command = connection.CreateCommand();
        
        // Common columns in Mylar3's comics table
        // Note: Some Mylar3 versions have different column names for monitoring
        command.CommandText = @"
            SELECT 
                ComicID,
                ComicName,
                ComicYear,
                ComicPublisher,
                ComicImage,
                Status,
                Total,
                Have,
                ComicLocation,
                Ignored,
                DateAdded,
                LastUpdated
            FROM comics";

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var comicId = GetString(reader, 0);
                var status = GetString(reader, 5);
                var item = new Mylar3Series
                {
                    ComicId = comicId,
                    ComicName = GetString(reader, 1),
                    ComicYear = GetInt(reader, 2),
                    ComicPublisher = GetString(reader, 3),
                    ComicImageUrl = GetString(reader, 4),
                    Status = status,
                    TotalIssues = GetInt(reader, 6),
                    HaveIssues = GetInt(reader, 7),
                    ComicLocation = GetString(reader, 8),
                    IsIgnored = GetInt(reader, 9) == 1,
                    DateAdded = GetDateTime(reader, 10),
                    LastUpdated = GetDateTime(reader, 11),
                    // Derive monitoring mode from status/ignored
                    Monitor = DeriveMonitoringMode(status, GetInt(reader, 9) == 1),
                    IsComplete = status?.Equals("Ended", StringComparison.OrdinalIgnoreCase) == true ||
                                 status?.Equals("Complete", StringComparison.OrdinalIgnoreCase) == true
                };

                // Try to parse ComicVine ID from ComicID
                if (!string.IsNullOrEmpty(comicId) && int.TryParse(comicId, out var cvId))
                {
                    item.ComicVineId = cvId;
                }

                series.Add(item);
            }
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Error reading series - some columns may not exist");
            // Try simpler query
            series = await ReadSeriesSimpleAsync(connection, ct);
        }

        // Try to read monitoring info from annuals table or separate monitoring config
        await EnrichWithMonitoringInfoAsync(connection, series, ct);

        return series;
    }

    /// <summary>
    /// Derive monitoring mode from Mylar3 status and ignored flag.
    /// </summary>
    private static string? DeriveMonitoringMode(string? status, bool isIgnored)
    {
        if (isIgnored)
            return "none";

        // In Mylar3, if not ignored and status is Active/Continuing, it's usually "all"
        // If Paused, it's "manual"
        // If Ended, it's typically "all" (to catch any remaining issues)
        return status?.ToLowerInvariant() switch
        {
            "paused" => "manual",
            "ended" or "complete" => "all",  // Complete series - want all missing
            "loading" => "future",            // Still loading - monitor future
            _ => "all"                        // Default to all for active series
        };
    }

    /// <summary>
    /// Try to enrich series with additional monitoring info from other tables.
    /// </summary>
    private async Task EnrichWithMonitoringInfoAsync(
        SqliteConnection connection, 
        List<Mylar3Series> series, 
        CancellationToken ct)
    {
        try
        {
            // Check if there's a monitor column we can query
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT Monitor FROM comics LIMIT 1";
            
            try
            {
                await using var reader = await checkCommand.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    // Monitor column exists - re-read with it
                    var monitorCommand = connection.CreateCommand();
                    monitorCommand.CommandText = "SELECT ComicID, Monitor FROM comics";
                    
                    await using var monitorReader = await monitorCommand.ExecuteReaderAsync(ct);
                    while (await monitorReader.ReadAsync(ct))
                    {
                        var comicId = GetString(monitorReader, 0);
                        var monitor = GetString(monitorReader, 1);
                        
                        var matching = series.FirstOrDefault(s => s.ComicId == comicId);
                        if (matching != null && !string.IsNullOrEmpty(monitor))
                        {
                            matching.Monitor = monitor;
                        }
                    }
                }
            }
            catch (SqliteException)
            {
                // Monitor column doesn't exist - that's fine, use derived values
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not enrich with monitoring info - using derived values");
        }
    }

    private async Task<List<Mylar3Series>> ReadSeriesSimpleAsync(SqliteConnection connection, CancellationToken ct)
    {
        var series = new List<Mylar3Series>();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ComicID, ComicName, ComicYear FROM comics";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var comicId = GetString(reader, 0);
            var item = new Mylar3Series
            {
                ComicId = comicId,
                ComicName = GetString(reader, 1),
                ComicYear = GetInt(reader, 2)
            };

            if (!string.IsNullOrEmpty(comicId) && int.TryParse(comicId, out var cvId))
            {
                item.ComicVineId = cvId;
            }

            series.Add(item);
        }

        return series;
    }

    private async Task<List<Mylar3Issue>> ReadIssuesAsync(SqliteConnection connection, CancellationToken ct)
    {
        var issues = new List<Mylar3Issue>();
        var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT 
                IssueID,
                ComicID,
                Issue_Number,
                IssueName,
                ReleaseDate,
                DigitalDate,
                Status,
                Location,
                ImageURL
            FROM issues";

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var issueId = GetString(reader, 0);
                var item = new Mylar3Issue
                {
                    IssueId = issueId,
                    ComicId = GetString(reader, 1),
                    IssueNumber = GetString(reader, 2),
                    IssueName = GetString(reader, 3),
                    ReleaseDate = GetDateTime(reader, 4),
                    StoreDate = GetDateTime(reader, 5),
                    Status = GetString(reader, 6),
                    Location = GetString(reader, 7),
                    ImageUrl = GetString(reader, 8)
                };

                if (!string.IsNullOrEmpty(issueId) && int.TryParse(issueId, out var cvId))
                {
                    item.ComicVineId = cvId;
                }

                issues.Add(item);
            }
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Error reading issues - some columns may not exist");
            issues = await ReadIssuesSimpleAsync(connection, ct);
        }

        return issues;
    }

    private async Task<List<Mylar3Issue>> ReadIssuesSimpleAsync(SqliteConnection connection, CancellationToken ct)
    {
        var issues = new List<Mylar3Issue>();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT IssueID, ComicID, Issue_Number, IssueName FROM issues";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var issueId = GetString(reader, 0);
            var item = new Mylar3Issue
            {
                IssueId = issueId,
                ComicId = GetString(reader, 1),
                IssueNumber = GetString(reader, 2),
                IssueName = GetString(reader, 3)
            };

            if (!string.IsNullOrEmpty(issueId) && int.TryParse(issueId, out var cvId))
            {
                item.ComicVineId = cvId;
            }

            issues.Add(item);
        }

        return issues;
    }

    private Task<List<Mylar3File>> ReadFilesAsync(SqliteConnection connection, List<Mylar3Issue> issues, CancellationToken ct)
    {
        // Extract file info from issues with location
        var files = issues
            .Where(i => !string.IsNullOrEmpty(i.Location))
            .Select(i => new Mylar3File
            {
                IssueId = i.IssueId,
                ComicId = i.ComicId,
                FilePath = i.Location,
                FileName = Path.GetFileName(i.Location)
            })
            .ToList();

        return Task.FromResult(files);
    }

    private async Task ImportSeriesAsync(
        Mylar3Snapshot snapshot,
        Mylar3MigrationOptions options,
        Mylar3MigrationResult result,
        Dictionary<string, int> seriesIdMap,
        CancellationToken ct)
    {
        var seriesToProcess = snapshot.Series
            .Where(s => !options.SkipIgnoredSeries || !s.IsIgnored)
            .Take(options.MaxSeries > 0 ? options.MaxSeries : int.MaxValue)
            .ToList();

        foreach (var mylarSeries in seriesToProcess)
        {
            result.SeriesProcessed++;

            if (string.IsNullOrEmpty(mylarSeries.ComicName))
            {
                result.SeriesFailed++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Series",
                    Mylar3Id = mylarSeries.ComicId,
                    Status = "Failed",
                    Error = "Series name is empty"
                });
                continue;
            }

            // Check if series already exists
            var existingSeries = await _dbContext.Series
                .FirstOrDefaultAsync(s => s.Title.ToLower() == mylarSeries.ComicName.ToLower(), ct);

            if (existingSeries != null)
            {
                if (options.SkipExistingSeries && !options.UpdateExistingSeries)
                {
                    result.SeriesSkipped++;
                    if (!string.IsNullOrEmpty(mylarSeries.ComicId))
                    {
                        seriesIdMap[mylarSeries.ComicId] = existingSeries.Id;
                    }
                    result.Items.Add(new Mylar3MigrationItem
                    {
                        EntityType = "Series",
                        Mylar3Id = mylarSeries.ComicId,
                        Mylar3Name = mylarSeries.ComicName,
                        ShortboxerrId = existingSeries.Id,
                        Status = "Skipped",
                        Reason = "Series already exists"
                    });
                    continue;
                }

                if (options.UpdateExistingSeries && !options.DryRun)
                {
                    // Update existing series
                    if (mylarSeries.ComicVineId.HasValue && !existingSeries.ComicVineId.HasValue)
                    {
                        existingSeries.ComicVineId = mylarSeries.ComicVineId;
                    }
                    if (mylarSeries.ComicYear.HasValue && !existingSeries.StartYear.HasValue)
                    {
                        existingSeries.StartYear = mylarSeries.ComicYear;
                    }
                    if (!string.IsNullOrEmpty(mylarSeries.ComicPublisher) && string.IsNullOrEmpty(existingSeries.Publisher))
                    {
                        existingSeries.Publisher = mylarSeries.ComicPublisher;
                    }
                    if (!string.IsNullOrEmpty(mylarSeries.ComicLocation) && string.IsNullOrEmpty(existingSeries.Path))
                    {
                        existingSeries.Path = mylarSeries.ComicLocation;
                    }
                    
                    // Import monitoring mode from Mylar3
                    if (options.ImportMonitoringModes && !string.IsNullOrEmpty(mylarSeries.Monitor))
                    {
                        existingSeries.MonitoringMode = MapMonitoringMode(mylarSeries.Monitor);
                        existingSeries.Monitored = mylarSeries.Monitor != "none" && !mylarSeries.IsIgnored;
                    }
                    
                    existingSeries.UpdatedAt = DateTime.UtcNow;

                    result.SeriesUpdated++;
                    if (!string.IsNullOrEmpty(mylarSeries.ComicId))
                    {
                        seriesIdMap[mylarSeries.ComicId] = existingSeries.Id;
                    }
                    result.Items.Add(new Mylar3MigrationItem
                    {
                        EntityType = "Series",
                        Mylar3Id = mylarSeries.ComicId,
                        Mylar3Name = mylarSeries.ComicName,
                        ShortboxerrId = existingSeries.Id,
                        Status = "Updated"
                    });
                    continue;
                }

                // Skip existing without update
                result.SeriesSkipped++;
                if (!string.IsNullOrEmpty(mylarSeries.ComicId))
                {
                    seriesIdMap[mylarSeries.ComicId] = existingSeries.Id;
                }
                continue;
            }

            // Create new series
            if (!options.DryRun)
            {
                var monitoringMode = options.ImportMonitoringModes && !string.IsNullOrEmpty(mylarSeries.Monitor)
                    ? MapMonitoringMode(mylarSeries.Monitor)
                    : SeriesMonitoringMode.AllIssues;
                    
                var newSeries = new Series
                {
                    Title = mylarSeries.ComicName,
                    StartYear = mylarSeries.ComicYear,
                    Publisher = mylarSeries.ComicPublisher,
                    ComicVineId = mylarSeries.ComicVineId,
                    CoverImageUrl = mylarSeries.ComicImageUrl,
                    Path = mylarSeries.ComicLocation,
                    Monitored = mylarSeries.Monitor != "none" && !mylarSeries.IsIgnored,
                    MonitoringMode = monitoringMode,
                    Status = MapSeriesStatus(mylarSeries.Status),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Series.Add(newSeries);
                await _dbContext.SaveChangesAsync(ct); // Save to get ID

                if (!string.IsNullOrEmpty(mylarSeries.ComicId))
                {
                    seriesIdMap[mylarSeries.ComicId] = newSeries.Id;
                }

                // Sync metadata if requested and has ComicVine ID
                if (options.SyncMetadataAfterImport && mylarSeries.ComicVineId.HasValue)
                {
                    try
                    {
                        await _seriesMetadataService.RefreshSeriesMetadataAsync(newSeries.Id, true, ct);
                        result.MetadataSynced++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync metadata for series {SeriesId}", newSeries.Id);
                        result.Warnings.Add($"Failed to sync metadata for {mylarSeries.ComicName}: {ex.Message}");
                    }
                }

                result.SeriesImported++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Series",
                    Mylar3Id = mylarSeries.ComicId,
                    Mylar3Name = mylarSeries.ComicName,
                    ShortboxerrId = newSeries.Id,
                    Status = "Imported"
                });
            }
            else
            {
                result.SeriesImported++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Series",
                    Mylar3Id = mylarSeries.ComicId,
                    Mylar3Name = mylarSeries.ComicName,
                    Status = "Imported",
                    Reason = "Dry run"
                });
            }
        }
    }

    private async Task ImportIssuesAsync(
        Mylar3Snapshot snapshot,
        Mylar3MigrationOptions options,
        Mylar3MigrationResult result,
        Dictionary<string, int> seriesIdMap,
        CancellationToken ct)
    {
        foreach (var mylarIssue in snapshot.Issues)
        {
            result.IssuesProcessed++;

            // Find parent series
            if (string.IsNullOrEmpty(mylarIssue.ComicId) || !seriesIdMap.TryGetValue(mylarIssue.ComicId, out var seriesId))
            {
                result.IssuesSkipped++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Issue",
                    Mylar3Id = mylarIssue.IssueId,
                    Mylar3Name = $"{mylarIssue.IssueNumber}: {mylarIssue.IssueName}",
                    Status = "Skipped",
                    Reason = "Parent series not found in migration"
                });
                continue;
            }

            // Parse issue number
            if (!decimal.TryParse(mylarIssue.IssueNumber, out var issueNumber))
            {
                // Try to extract number from string like "1.0" or "Annual 1"
                issueNumber = 0;
            }

            // Check if issue already exists
            var existingIssue = await _dbContext.Issues
                .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.IssueNumber == issueNumber, ct);

            if (existingIssue != null)
            {
                result.IssuesSkipped++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Issue",
                    Mylar3Id = mylarIssue.IssueId,
                    Mylar3Name = $"#{mylarIssue.IssueNumber}: {mylarIssue.IssueName}",
                    ShortboxerrId = existingIssue.Id,
                    Status = "Skipped",
                    Reason = "Issue already exists"
                });
                continue;
            }

            if (!options.DryRun)
            {
                var newIssue = new Issue
                {
                    SeriesId = seriesId,
                    IssueNumber = issueNumber,
                    Title = mylarIssue.IssueName ?? $"Issue #{mylarIssue.IssueNumber}",
                    ComicVineId = mylarIssue.ComicVineId,
                    StoreDate = mylarIssue.StoreDate ?? mylarIssue.ReleaseDate,
                    CoverImageUrl = mylarIssue.ImageUrl,
                    Status = MapIssueStatus(mylarIssue.Status, mylarIssue.Location, options.ImportWantedStatus),
                    Monitored = options.ImportWantedStatus && mylarIssue.Status?.Equals("Wanted", StringComparison.OrdinalIgnoreCase) == true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Issues.Add(newIssue);

                result.IssuesImported++;
                result.Items.Add(new Mylar3MigrationItem
                {
                    EntityType = "Issue",
                    Mylar3Id = mylarIssue.IssueId,
                    Mylar3Name = $"#{mylarIssue.IssueNumber}: {mylarIssue.IssueName}",
                    Status = "Imported"
                });
            }
            else
            {
                result.IssuesImported++;
            }
        }
    }

    private static SeriesStatus MapSeriesStatus(string? mylarStatus)
    {
        return mylarStatus?.ToLowerInvariant() switch
        {
            "continuing" => SeriesStatus.Continuing,
            "ended" => SeriesStatus.Ended,
            "hiatus" => SeriesStatus.Continuing, // Map hiatus to continuing
            _ => SeriesStatus.Continuing
        };
    }

    /// <summary>
    /// Maps Mylar3 monitoring mode to Shortboxerr's SeriesMonitoringMode.
    /// </summary>
    private static SeriesMonitoringMode MapMonitoringMode(string? mylarMonitor)
    {
        return mylarMonitor?.ToLowerInvariant() switch
        {
            "all" or "all_issues" => SeriesMonitoringMode.AllIssues,
            "future" or "future_issues" => SeriesMonitoringMode.FutureIssues,
            "manual" => SeriesMonitoringMode.Manual,
            "first" or "first_issue" => SeriesMonitoringMode.FirstIssue,
            "none" => SeriesMonitoringMode.None,
            _ => SeriesMonitoringMode.AllIssues // Default to all for active series
        };
    }

    private static IssueStatus MapIssueStatus(string? mylarStatus, string? location, bool importWanted)
    {
        if (!string.IsNullOrEmpty(location))
        {
            return IssueStatus.Owned;
        }

        if (!importWanted)
        {
            return IssueStatus.Missing;
        }

        return mylarStatus?.ToLowerInvariant() switch
        {
            "wanted" => IssueStatus.Wanted,
            "downloaded" => IssueStatus.Owned,
            "snatched" => IssueStatus.Wanted,
            "skipped" => IssueStatus.Skipped,
            "archived" => IssueStatus.Owned,
            _ => IssueStatus.Missing
        };
    }

    private static string? GetString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? GetInt(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        
        try
        {
            return reader.GetInt32(ordinal);
        }
        catch
        {
            // Try parsing as string
            var strValue = reader.GetString(ordinal);
            return int.TryParse(strValue, out var result) ? result : null;
        }
    }

    private static DateTime? GetDateTime(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        
        try
        {
            return reader.GetDateTime(ordinal);
        }
        catch
        {
            // Try parsing as string (Mylar3 stores dates as strings)
            var strValue = reader.GetString(ordinal);
            return DateTime.TryParse(strValue, out var result) ? result : null;
        }
    }

    #endregion
}
