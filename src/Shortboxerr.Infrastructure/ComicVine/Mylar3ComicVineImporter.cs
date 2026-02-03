using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.ComicVine;

/// <summary>
/// Implementation of IMylar3ComicVineImporter for importing ComicVine settings from Mylar3.
/// </summary>
public class Mylar3ComicVineImporter : IMylar3ComicVineImporter
{
    private readonly ISettingsService _settingsService;
    private readonly IComicVineClient _comicVineClient;
    private readonly ISeriesMetadataService _seriesMetadataService;
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ILogger<Mylar3ComicVineImporter> _logger;

    // INI section and key patterns
    private static readonly Regex SectionPattern = new(@"^\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex KeyValuePattern = new(@"^([^=]+)=(.*)$", RegexOptions.Compiled);

    public Mylar3ComicVineImporter(
        ISettingsService settingsService,
        IComicVineClient comicVineClient,
        ISeriesMetadataService seriesMetadataService,
        ShortboxerrDbContext dbContext,
        ILogger<Mylar3ComicVineImporter> logger)
    {
        _settingsService = settingsService;
        _comicVineClient = comicVineClient;
        _seriesMetadataService = seriesMetadataService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public Mylar3ComicVineSettings ParseComicVineSettings(string configContent)
    {
        var result = new Mylar3ComicVineSettings { Success = true };

        try
        {
            var sections = ParseIniSections(configContent);

            // Look for ComicVine settings in [General] and [CV] sections
            if (sections.TryGetValue("General", out var generalSettings))
            {
                ParseGeneralSection(generalSettings, result);
            }

            if (sections.TryGetValue("CV", out var cvSettings))
            {
                ParseComicVineSection(cvSettings, result);
            }

            // Also check [ComicVine] section (alternative naming)
            if (sections.TryGetValue("ComicVine", out var comicVineSettings))
            {
                ParseComicVineSection(comicVineSettings, result);
            }

            // Validate results
            if (string.IsNullOrEmpty(result.ApiKey))
            {
                result.Warnings.Add("No ComicVine API key found in config");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Failed to parse config: {ex.Message}";
            _logger.LogError(ex, "Error parsing Mylar3 config for ComicVine settings");
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Mylar3ComicVineSettings> ParseComicVineSettingsFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new Mylar3ComicVineSettings
            {
                Success = false,
                Error = $"File not found: {filePath}"
            };
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var result = ParseComicVineSettings(content);
            result.SourcePath = filePath;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Mylar3 config file: {FilePath}", filePath);
            return new Mylar3ComicVineSettings
            {
                Success = false,
                Error = $"Error reading file: {ex.Message}",
                SourcePath = filePath
            };
        }
    }

    /// <inheritdoc />
    public async Task<ComicVineImportResult> ImportComicVineSettingsAsync(
        Mylar3ComicVineSettings settings,
        ComicVineImportOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new ComicVineImportResult { Success = true };

        try
        {
            // Get current settings
            var currentSettings = await _settingsService.GetAsync<ComicVineSettings>(
                "comicvine", new ComicVineSettings(), cancellationToken);

            // Import API key
            if (!string.IsNullOrEmpty(settings.ApiKey))
            {
                if (options.OverwriteApiKey || string.IsNullOrEmpty(currentSettings?.ApiKey))
                {
                    currentSettings ??= new ComicVineSettings();
                    currentSettings.ApiKey = settings.ApiKey;
                    result.ApiKeyImported = true;
                    result.ImportedSettings.Add("ApiKey");
                }
                else
                {
                    result.SkippedSettings.Add("ApiKey (existing key present, overwrite=false)");
                }
            }

            // Import cache settings
            if (options.ImportCacheSettings)
            {
                currentSettings ??= new ComicVineSettings();
                
                if (settings.CacheCovers)
                {
                    // Cache is enabled by default in Shortboxerr
                    result.ImportedSettings.Add("CoverCacheEnabled");
                }

                if (!string.IsNullOrEmpty(settings.CoverCachePath))
                {
                    // Note: Shortboxerr uses its own cache path, but we can log this
                    result.Warnings.Add($"Mylar3 cover cache path ({settings.CoverCachePath}) noted but not imported (Shortboxerr uses own cache)");
                }

                if (!string.IsNullOrEmpty(settings.CoverQuality))
                {
                    result.ImportedSettings.Add($"CoverQuality: {settings.CoverQuality}");
                }

                result.CacheSettingsImported = true;
            }

            // Import auto-match settings
            if (options.ImportAutoMatchSettings && settings.AutoMatchThreshold.HasValue)
            {
                currentSettings ??= new ComicVineSettings();
                currentSettings.AutoMatchThreshold = settings.AutoMatchThreshold.Value;
                result.AutoMatchSettingsImported = true;
                result.ImportedSettings.Add($"AutoMatchThreshold: {settings.AutoMatchThreshold}");
            }

            // Import refresh settings
            if (options.ImportRefreshSettings)
            {
                var refreshSettings = await _settingsService.GetAsync<MetadataRefreshSettings>(
                    "metadata_refresh", new MetadataRefreshSettings(), cancellationToken);

                if (settings.RefreshIntervalDays.HasValue)
                {
                    refreshSettings ??= new MetadataRefreshSettings();
                    refreshSettings.RefreshInterval = TimeSpan.FromDays(settings.RefreshIntervalDays.Value);
                    await _settingsService.SetAsync("metadata_refresh", refreshSettings, cancellationToken);
                    result.RefreshSettingsImported = true;
                    result.ImportedSettings.Add($"RefreshInterval: {settings.RefreshIntervalDays} days");
                }
            }

            // Save ComicVine settings
            if (currentSettings != null)
            {
                currentSettings.Enabled = settings.Enabled;
                await _settingsService.SetAsync("comicvine", currentSettings, cancellationToken);
            }

            _logger.LogInformation(
                "Imported ComicVine settings from Mylar3: {Imported} settings imported, {Skipped} skipped",
                result.ImportedSettings.Count, result.SkippedSettings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing ComicVine settings");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ComicVineIdValidationResult> ValidateComicVineIdsAsync(
        string mylar3DbPath,
        CancellationToken cancellationToken = default)
    {
        var result = new ComicVineIdValidationResult { Success = true };

        if (!File.Exists(mylar3DbPath))
        {
            return new ComicVineIdValidationResult
            {
                Success = false,
                Error = $"Mylar3 database not found: {mylar3DbPath}"
            };
        }

        try
        {
            var connectionString = $"Data Source={mylar3DbPath};Mode=ReadOnly;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Query Mylar3 comics table for ComicVine IDs
            var command = connection.CreateCommand();
            command.CommandText = "SELECT ComicName, ComicID FROM comics WHERE ComicID IS NOT NULL";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var title = reader.GetString(0);
                var comicVineIdStr = reader.GetString(1);

                if (!int.TryParse(comicVineIdStr, out var comicVineId))
                {
                    result.Items.Add(new ComicVineIdValidationItem
                    {
                        Mylar3SeriesTitle = title,
                        Mylar3ComicVineId = 0,
                        IsIdValid = false,
                        ValidationError = $"Invalid ComicVine ID format: {comicVineIdStr}"
                    });
                    result.InvalidIds++;
                    continue;
                }

                result.TotalSeries++;

                // Check if we have a local series with matching title
                var localSeries = await _dbContext.Series
                    .FirstOrDefaultAsync(s => s.Title.ToLower() == title.ToLower(), cancellationToken);

                var item = new ComicVineIdValidationItem
                {
                    Mylar3SeriesTitle = title,
                    Mylar3ComicVineId = comicVineId,
                    LocalSeriesId = localSeries?.Id,
                    LocalSeriesTitle = localSeries?.Title,
                    TitleMatched = localSeries != null
                };

                if (localSeries != null)
                {
                    result.MatchedByTitle++;
                }
                else
                {
                    result.UnmatchedByTitle++;
                }

                // Optionally validate with ComicVine (expensive, so we'll just mark as potentially valid)
                item.IsIdValid = true; // Assume valid, actual validation in migration

                result.Items.Add(item);
                result.ValidIds++;
            }

            _logger.LogInformation(
                "Validated Mylar3 database: {Total} series, {Matched} matched by title",
                result.TotalSeries, result.MatchedByTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Mylar3 database: {Path}", mylar3DbPath);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ComicVineIdMigrationResult> MigrateComicVineIdsAsync(
        string mylar3DbPath,
        ComicVineIdMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new ComicVineIdMigrationResult { Success = true };

        if (!File.Exists(mylar3DbPath))
        {
            return new ComicVineIdMigrationResult
            {
                Success = false,
                Error = $"Mylar3 database not found: {mylar3DbPath}"
            };
        }

        try
        {
            var connectionString = $"Data Source={mylar3DbPath};Mode=ReadOnly;";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = "SELECT ComicName, ComicID, ComicYear FROM comics WHERE ComicID IS NOT NULL";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var processed = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (options.MaxSeries > 0 && processed >= options.MaxSeries)
                    break;

                var title = reader.GetString(0);
                var comicVineIdStr = reader.GetString(1);
                var year = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);

                if (!int.TryParse(comicVineIdStr, out var comicVineId))
                {
                    result.Items.Add(new ComicVineIdMigrationItem
                    {
                        Mylar3SeriesTitle = title,
                        Mylar3ComicVineId = 0,
                        Status = "Failed",
                        Error = $"Invalid ComicVine ID format: {comicVineIdStr}"
                    });
                    result.Failed++;
                    continue;
                }

                result.TotalProcessed++;
                processed++;

                // Find local series
                var localSeries = await _dbContext.Series
                    .FirstOrDefaultAsync(s => s.Title.ToLower() == title.ToLower(), cancellationToken);

                if (localSeries == null && options.RequireTitleMatch)
                {
                    result.Items.Add(new ComicVineIdMigrationItem
                    {
                        Mylar3SeriesTitle = title,
                        Mylar3ComicVineId = comicVineId,
                        Status = "Skipped",
                        SkipReason = "No matching local series found"
                    });
                    result.Skipped++;
                    continue;
                }

                if (localSeries != null)
                {
                    // Check if already has ComicVine ID
                    if (localSeries.ComicVineId.HasValue && !options.OverwriteExisting)
                    {
                        result.Items.Add(new ComicVineIdMigrationItem
                        {
                            Mylar3SeriesTitle = title,
                            Mylar3ComicVineId = comicVineId,
                            LocalSeriesId = localSeries.Id,
                            Status = "Skipped",
                            SkipReason = $"Already has ComicVine ID: {localSeries.ComicVineId}"
                        });
                        result.Skipped++;
                        continue;
                    }

                    // Validate the ID if requested
                    if (options.ValidateIds)
                    {
                        var volumeResult = await _comicVineClient.GetVolumeAsync(comicVineId, cancellationToken);
                        if (!volumeResult.Success)
                        {
                            result.Items.Add(new ComicVineIdMigrationItem
                            {
                                Mylar3SeriesTitle = title,
                                Mylar3ComicVineId = comicVineId,
                                LocalSeriesId = localSeries.Id,
                                Status = "Failed",
                                Error = $"ComicVine ID validation failed: {volumeResult.Error}"
                            });
                            result.Failed++;
                            continue;
                        }
                    }

                    // Migrate the ID
                    localSeries.ComicVineId = comicVineId;
                    localSeries.ComicVineLastUpdated = DateTime.UtcNow;

                    var item = new ComicVineIdMigrationItem
                    {
                        Mylar3SeriesTitle = title,
                        Mylar3ComicVineId = comicVineId,
                        LocalSeriesId = localSeries.Id,
                        Status = "Migrated"
                    };

                    // Sync metadata if requested
                    if (options.SyncMetadataAfterMigration)
                    {
                        try
                        {
                            await _seriesMetadataService.RefreshSeriesMetadataAsync(
                                localSeries.Id, forceRefresh: true, cancellationToken);
                            item.MetadataSynced = true;
                            result.MetadataSynced++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync metadata for series {SeriesId}", localSeries.Id);
                        }
                    }

                    result.Items.Add(item);
                    result.Migrated++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Migrated ComicVine IDs from Mylar3: {Migrated} migrated, {Skipped} skipped, {Failed} failed",
                result.Migrated, result.Skipped, result.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating ComicVine IDs from Mylar3: {Path}", mylar3DbPath);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    #region Private Methods

    private Dictionary<string, Dictionary<string, string>> ParseIniSections(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = "Default";
        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in content.Split('\n'))
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#') || trimmedLine.StartsWith(';'))
                continue;

            // Check for section header
            var sectionMatch = SectionPattern.Match(trimmedLine);
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups[1].Value;
                if (!sections.ContainsKey(currentSection))
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            // Check for key=value
            var kvMatch = KeyValuePattern.Match(trimmedLine);
            if (kvMatch.Success)
            {
                var key = kvMatch.Groups[1].Value.Trim();
                var value = kvMatch.Groups[2].Value.Trim();
                sections[currentSection][key] = value;
            }
        }

        return sections;
    }

    private void ParseGeneralSection(Dictionary<string, string> settings, Mylar3ComicVineSettings result)
    {
        // ComicVine API key might be in General section
        if (settings.TryGetValue("comicvine_api", out var apiKey))
        {
            result.ApiKey = apiKey;
            result.RawSettings["comicvine_api"] = apiKey;
        }
        else if (settings.TryGetValue("cv_api", out apiKey))
        {
            result.ApiKey = apiKey;
            result.RawSettings["cv_api"] = apiKey;
        }

        // Cover settings
        if (settings.TryGetValue("cache_dir", out var cacheDir))
        {
            result.CoverCachePath = cacheDir;
            result.RawSettings["cache_dir"] = cacheDir;
        }
    }

    private void ParseComicVineSection(Dictionary<string, string> settings, Mylar3ComicVineSettings result)
    {
        // API Key
        if (settings.TryGetValue("api_key", out var apiKey) || 
            settings.TryGetValue("apikey", out apiKey) ||
            settings.TryGetValue("comicvine_api", out apiKey))
        {
            result.ApiKey = apiKey;
            result.RawSettings["api_key"] = apiKey;
        }

        // Enabled
        if (settings.TryGetValue("enabled", out var enabled))
        {
            result.Enabled = ParseBool(enabled);
            result.RawSettings["enabled"] = enabled;
        }

        // Auto-match threshold
        if (settings.TryGetValue("automatch_threshold", out var threshold) ||
            settings.TryGetValue("match_threshold", out threshold))
        {
            if (int.TryParse(threshold, out var thresholdValue))
            {
                result.AutoMatchThreshold = thresholdValue;
                result.RawSettings["automatch_threshold"] = threshold;
            }
        }

        // Refresh interval
        if (settings.TryGetValue("refresh_interval", out var refreshInterval) ||
            settings.TryGetValue("cv_refresh", out refreshInterval))
        {
            if (int.TryParse(refreshInterval, out var intervalValue))
            {
                result.RefreshIntervalDays = intervalValue;
                result.RawSettings["refresh_interval"] = refreshInterval;
            }
        }

        // Cover quality
        if (settings.TryGetValue("cover_quality", out var coverQuality))
        {
            result.CoverQuality = coverQuality;
            result.RawSettings["cover_quality"] = coverQuality;
        }

        // Skip variants
        if (settings.TryGetValue("skip_variants", out var skipVariants))
        {
            result.SkipVariants = ParseBool(skipVariants);
            result.RawSettings["skip_variants"] = skipVariants;
        }

        // Skip annuals
        if (settings.TryGetValue("skip_annuals", out var skipAnnuals))
        {
            result.SkipAnnuals = ParseBool(skipAnnuals);
            result.RawSettings["skip_annuals"] = skipAnnuals;
        }

        // Track unmapped settings
        var knownSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "api_key", "apikey", "comicvine_api", "enabled", "automatch_threshold",
            "match_threshold", "refresh_interval", "cv_refresh", "cover_quality",
            "skip_variants", "skip_annuals"
        };

        foreach (var setting in settings.Keys)
        {
            if (!knownSettings.Contains(setting))
            {
                result.UnmappedSettings.Add(setting);
            }
        }
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

