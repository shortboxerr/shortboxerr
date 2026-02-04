using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Implementation of Mylar3 configuration file importer.
/// Parses Mylar3 config.ini format and extracts DDL provider configurations.
/// </summary>
public partial class Mylar3ConfigImporter : IMylar3ConfigImporter
{
    private readonly ShortboxerrDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<Mylar3ConfigImporter>? _logger;
    
    // Known DDL sections in Mylar3 config
    private static readonly HashSet<string> KnownDdlSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "DDL", "DDL-1", "DDL-2", "DDL-3", "DDL-4", "DDL-5",
        "GettyComics", "ReadComicOnline", "GetComics", "Libgen"
    };
    
    // Mapping of Mylar3 site types to our site types
    private static readonly Dictionary<string, string> SiteTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "gettycomics", "GettyComics" },
        { "getty", "GettyComics" },
        { "readcomiconline", "ReadComicOnline" },
        { "rco", "ReadComicOnline" },
        { "getcomics", "GetComics" },
        { "libgen", "Libgen" },
        { "generic", "Generic" }
    };

    // Mapping of Mylar3 monitoring modes to Shortboxerr
    private static readonly Dictionary<string, SeriesMonitoringMode> MonitoringModeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "all", SeriesMonitoringMode.AllIssues },
        { "all_issues", SeriesMonitoringMode.AllIssues },
        { "future", SeriesMonitoringMode.FutureIssues },
        { "future_issues", SeriesMonitoringMode.FutureIssues },
        { "manual", SeriesMonitoringMode.Manual },
        { "none", SeriesMonitoringMode.None },
        { "first", SeriesMonitoringMode.FirstIssue },
        { "first_issue", SeriesMonitoringMode.FirstIssue }
    };

    public Mylar3ConfigImporter(
        ShortboxerrDbContext dbContext, 
        ISettingsService settingsService,
        ILogger<Mylar3ConfigImporter>? logger = null)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    public Mylar3ImportResult ParseConfig(string configContent)
    {
        var result = new Mylar3ImportResult { Success = true };
        var warnings = new List<string>();
        var unmappedSections = new List<string>();
        var unmappedSettings = new Dictionary<string, List<string>>();
        var providers = new List<Mylar3DdlProvider>();
        Mylar3GeneralSettings? generalSettings = null;
        Mylar3PullListSettings? pullListSettings = null;
        
        try
        {
            var sections = ParseIniSections(configContent);
            var priority = 1;
            
            foreach (var (sectionName, settings) in sections)
            {
                // Handle General section
                if (sectionName.Equals("General", StringComparison.OrdinalIgnoreCase))
                {
                    generalSettings = ParseGeneralSettings(settings, warnings);
                    // Also extract pull list settings from General section
                    pullListSettings = ParsePullListSettings(settings, warnings);
                    continue;
                }
                
                // Handle WeeklyPull / PullList section (separate section in some configs)
                if (sectionName.Equals("WeeklyPull", StringComparison.OrdinalIgnoreCase) ||
                    sectionName.Equals("PullList", StringComparison.OrdinalIgnoreCase))
                {
                    pullListSettings = ParsePullListSettings(settings, warnings);
                    continue;
                }
                
                // Handle DDL sections
                if (IsDdlSection(sectionName))
                {
                    var provider = ParseDdlProvider(sectionName, settings, priority++, warnings, unmappedSettings);
                    if (provider != null)
                    {
                        providers.Add(provider);
                    }
                    continue;
                }
                
                // Track unmapped sections
                if (!IsKnownNonDdlSection(sectionName))
                {
                    unmappedSections.Add(sectionName);
                }
            }
            
            return result with
            {
                DdlProviders = providers,
                GeneralSettings = generalSettings,
                PullListSettings = pullListSettings,
                UnmappedSections = unmappedSections,
                UnmappedSettings = unmappedSettings,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse Mylar3 config");
            return result with
            {
                Success = false,
                ErrorMessage = $"Failed to parse config: {ex.Message}"
            };
        }
    }

    public async Task<Mylar3ImportResult> ParseConfigFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new Mylar3ImportResult
            {
                Success = false,
                ErrorMessage = $"Config file not found: {filePath}"
            };
        }
        
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var result = ParseConfig(content);
        return result with { SourcePath = filePath };
    }

    public async Task<Mylar3ValidationReport> ValidateImportAsync(Mylar3ImportResult importResult, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var providersToCreate = new List<string>();
        var existingProviders = new List<string>();
        var settingDeviations = new List<string>();
        
        if (!importResult.Success)
        {
            return new Mylar3ValidationReport
            {
                IsValid = false,
                Errors = new List<string> { importResult.ErrorMessage ?? "Import result indicates failure" }
            };
        }
        
        // Check for duplicate names
        var existingNames = await _dbContext.Providers
            .Select(p => p.Name.ToLower())
            .ToListAsync(cancellationToken);
        
        foreach (var provider in importResult.DdlProviders)
        {
            if (existingNames.Contains(provider.Name.ToLower()))
            {
                existingProviders.Add(provider.Name);
                warnings.Add($"Provider '{provider.Name}' already exists");
            }
            else
            {
                providersToCreate.Add(provider.Name);
            }
            
            // Check for setting deviations from Mylar3 defaults
            if (provider.Settings != null)
            {
                var defaults = DdlProviderSettings.CreateMylar3Default(provider.SiteType);
                
                if (provider.Settings.RateLimitPerMinute != defaults.RateLimitPerMinute)
                    settingDeviations.Add($"{provider.Name}: Custom rate limit {provider.Settings.RateLimitPerMinute}/min (default: {defaults.RateLimitPerMinute})");
                
                if (provider.Settings.MaxRetries != defaults.MaxRetries)
                    settingDeviations.Add($"{provider.Name}: Custom max retries {provider.Settings.MaxRetries} (default: {defaults.MaxRetries})");
                
                if (provider.Settings.TimeoutSeconds != defaults.TimeoutSeconds)
                    settingDeviations.Add($"{provider.Name}: Custom timeout {provider.Settings.TimeoutSeconds}s (default: {defaults.TimeoutSeconds}s)");
            }
        }
        
        // Add import warnings
        warnings.AddRange(importResult.Warnings);
        
        if (importResult.UnmappedSections.Count > 0)
        {
            warnings.Add($"Unmapped sections: {string.Join(", ", importResult.UnmappedSections)}");
        }
        
        return new Mylar3ValidationReport
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            ProvidersToCreate = providersToCreate,
            ExistingProviders = existingProviders,
            SettingDeviations = settingDeviations
        };
    }

    public async Task<Mylar3ExecutionResult> ExecuteImportAsync(Mylar3ImportResult importResult, Mylar3ImportOptions options, CancellationToken cancellationToken = default)
    {
        if (!importResult.Success)
        {
            return new Mylar3ExecutionResult
            {
                Success = false,
                ErrorMessage = importResult.ErrorMessage ?? "Import result indicates failure"
            };
        }
        
        if (options.ValidateFirst)
        {
            var validation = await ValidateImportAsync(importResult, cancellationToken);
            if (!validation.IsValid)
            {
                return new Mylar3ExecutionResult
                {
                    Success = false,
                    ErrorMessage = string.Join("; ", validation.Errors)
                };
            }
        }
        
        var createdIds = new List<int>();
        var details = new List<ProviderImportDetail>();
        var created = 0;
        var updated = 0;
        var skipped = 0;
        
        foreach (var provider in importResult.DdlProviders)
        {
            try
            {
                // Skip disabled providers if option is set
                if (!provider.IsEnabled && !options.ImportDisabled)
                {
                    details.Add(new ProviderImportDetail
                    {
                        Name = provider.Name,
                        Action = ProviderImportAction.Skipped
                    });
                    skipped++;
                    continue;
                }
                
                var name = options.NamePrefix != null ? $"{options.NamePrefix}{provider.Name}" : provider.Name;
                
                // Check for existing
                var existing = await _dbContext.Providers
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), cancellationToken);
                
                if (existing != null)
                {
                    if (options.OverwriteExisting)
                    {
                        // Update existing
                        UpdateProviderFromMylar3(existing, provider, options);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        
                        details.Add(new ProviderImportDetail
                        {
                            Name = name,
                            Action = ProviderImportAction.Updated,
                            ProviderId = existing.Id
                        });
                        updated++;
                    }
                    else
                    {
                        details.Add(new ProviderImportDetail
                        {
                            Name = name,
                            Action = ProviderImportAction.Skipped
                        });
                        skipped++;
                    }
                    continue;
                }
                
                // Create new
                var newProvider = CreateProviderFromMylar3(provider, name, options);
                _dbContext.Providers.Add(newProvider);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                createdIds.Add(newProvider.Id);
                details.Add(new ProviderImportDetail
                {
                    Name = name,
                    Action = ProviderImportAction.Created,
                    ProviderId = newProvider.Id
                });
                created++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import provider {Name}", provider.Name);
                details.Add(new ProviderImportDetail
                {
                    Name = provider.Name,
                    Action = ProviderImportAction.Failed,
                    ErrorMessage = ex.Message
                });
            }
        }
        
        return new Mylar3ExecutionResult
        {
            Success = true,
            ProvidersCreated = created,
            ProvidersUpdated = updated,
            ProvidersSkipped = skipped,
            CreatedProviderIds = createdIds,
            Details = details
        };
    }

    private static Dictionary<string, Dictionary<string, string>> ParseIniSections(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = "Default";
        var currentSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                continue;
            
            // Section header
            var sectionMatch = SectionRegex().Match(trimmed);
            if (sectionMatch.Success)
            {
                // Save previous section
                if (currentSettings.Count > 0)
                {
                    sections[currentSection] = currentSettings;
                }
                
                currentSection = sectionMatch.Groups[1].Value;
                currentSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            
            // Key-value pair
            var kvMatch = KeyValueRegex().Match(trimmed);
            if (kvMatch.Success)
            {
                var key = kvMatch.Groups[1].Value.Trim();
                var value = kvMatch.Groups[2].Value.Trim();
                currentSettings[key] = value;
            }
        }
        
        // Save last section
        if (currentSettings.Count > 0)
        {
            sections[currentSection] = currentSettings;
        }
        
        return sections;
    }

    private static Mylar3GeneralSettings ParseGeneralSettings(Dictionary<string, string> settings, List<string> warnings)
    {
        return new Mylar3GeneralSettings
        {
            ComicLocation = settings.GetValueOrDefault("comic_location") ?? settings.GetValueOrDefault("comic_dir"),
            QualityProfile = settings.GetValueOrDefault("quality_profile") ?? settings.GetValueOrDefault("quality"),
            AutoGrab = ParseBool(settings.GetValueOrDefault("auto_grab"), true),
            PreferredFormat = settings.GetValueOrDefault("preferred_format") ?? settings.GetValueOrDefault("file_format"),
            StagingFolder = settings.GetValueOrDefault("staging_folder") ?? settings.GetValueOrDefault("staging_dir"),
            DatabasePath = settings.GetValueOrDefault("database") ?? settings.GetValueOrDefault("db_path")
        };
    }

    private Mylar3DdlProvider? ParseDdlProvider(string sectionName, Dictionary<string, string> settings, int priority, List<string> warnings, Dictionary<string, List<string>> unmapped)
    {
        // Determine site type
        var siteType = DetermineSiteType(sectionName, settings);
        if (siteType == null)
        {
            warnings.Add($"Could not determine site type for section [{sectionName}]");
            return null;
        }
        
        // Get name
        var name = settings.GetValueOrDefault("name") ?? 
                   settings.GetValueOrDefault("provider_name") ?? 
                   sectionName;
        
        // Parse enabled state
        var enabled = ParseBool(settings.GetValueOrDefault("enabled") ?? settings.GetValueOrDefault("active"), true);
        
        // Parse URL
        var baseUrl = settings.GetValueOrDefault("url") ?? 
                      settings.GetValueOrDefault("base_url") ?? 
                      settings.GetValueOrDefault("host");
        
        // Parse credentials
        var username = settings.GetValueOrDefault("username") ?? settings.GetValueOrDefault("user");
        var password = settings.GetValueOrDefault("password") ?? settings.GetValueOrDefault("pass");
        var apiKey = settings.GetValueOrDefault("api_key") ?? settings.GetValueOrDefault("apikey");
        
        // Parse DDL-specific settings
        var ddlSettings = DdlProviderSettings.CreateMylar3Default(siteType);
        
        if (settings.TryGetValue("rate_limit", out var rateLimit) || settings.TryGetValue("ratelimit", out rateLimit))
            ddlSettings.RateLimitPerMinute = int.TryParse(rateLimit, out var rl) ? rl : ddlSettings.RateLimitPerMinute;
        
        if (settings.TryGetValue("timeout", out var timeout))
            ddlSettings.TimeoutSeconds = int.TryParse(timeout, out var to) ? to : ddlSettings.TimeoutSeconds;
        
        if (settings.TryGetValue("download_timeout", out var dlTimeout))
            ddlSettings.DownloadTimeoutSeconds = int.TryParse(dlTimeout, out var dto) ? dto : ddlSettings.DownloadTimeoutSeconds;
        
        if (settings.TryGetValue("max_retries", out var retries) || settings.TryGetValue("retries", out retries))
            ddlSettings.MaxRetries = int.TryParse(retries, out var mr) ? mr : ddlSettings.MaxRetries;
        
        if (settings.TryGetValue("user_agent", out var userAgent) || settings.TryGetValue("useragent", out userAgent))
            ddlSettings.UserAgent = userAgent;
        
        // Track unmapped settings
        var mappedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name", "provider_name", "enabled", "active", "url", "base_url", "host",
            "username", "user", "password", "pass", "api_key", "apikey",
            "rate_limit", "ratelimit", "timeout", "download_timeout", "max_retries", "retries",
            "user_agent", "useragent", "site_type", "type"
        };
        
        var unmappedKeys = settings.Keys.Where(k => !mappedKeys.Contains(k)).ToList();
        if (unmappedKeys.Count > 0)
        {
            unmapped[sectionName] = unmappedKeys;
        }
        
        return new Mylar3DdlProvider
        {
            Name = name,
            SiteType = siteType,
            BaseUrl = baseUrl,
            IsEnabled = enabled,
            Priority = priority,
            Username = username,
            Password = password,
            ApiKey = apiKey,
            Settings = ddlSettings,
            OriginalSection = sectionName,
            RawSettings = new Dictionary<string, string>(settings)
        };
    }

    private static string? DetermineSiteType(string sectionName, Dictionary<string, string> settings)
    {
        // Check for explicit site_type setting
        if (settings.TryGetValue("site_type", out var siteType) || settings.TryGetValue("type", out siteType))
        {
            if (SiteTypeMapping.TryGetValue(siteType, out var mapped))
                return mapped;
            return siteType;
        }
        
        // Try to determine from section name
        foreach (var (key, value) in SiteTypeMapping)
        {
            if (sectionName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        
        // Check URL for clues
        if (settings.TryGetValue("url", out var url) || settings.TryGetValue("base_url", out url))
        {
            foreach (var (key, value) in SiteTypeMapping)
            {
                if (url.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        
        // Default to generic DDL if it's a DDL section
        if (sectionName.StartsWith("DDL", StringComparison.OrdinalIgnoreCase))
            return "Generic";
        
        return null;
    }

    private static bool IsDdlSection(string sectionName)
    {
        return sectionName.StartsWith("DDL", StringComparison.OrdinalIgnoreCase) ||
               KnownDdlSections.Contains(sectionName) ||
               SiteTypeMapping.Keys.Any(k => sectionName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKnownNonDdlSection(string sectionName)
    {
        var knownSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "General", "Newznab", "Torznab", "SABnzbd", "NZBGet", "Transmission",
            "qBittorrent", "Deluge", "rTorrent", "ComicVine", "Metadata", "Notifications",
            "Email", "Pushover", "Slack", "Discord", "Telegram", "Prowl",
            "WeeklyPull", "PullList"  // Pull list sections
        };
        return knownSections.Contains(sectionName);
    }

    private static Mylar3PullListSettings ParsePullListSettings(Dictionary<string, string> settings, List<string> warnings)
    {
        var rawSettings = new Dictionary<string, string>(settings);
        var unmapped = new List<string>();

        // Common key variants in Mylar3 config
        var weeklyPullFolder = settings.GetValueOrDefault("weeklypull_folder") ??
                               settings.GetValueOrDefault("weekly_pull_folder") ??
                               settings.GetValueOrDefault("pull_folder");

        var weeklyPullFormat = settings.GetValueOrDefault("weeklypull_format") ??
                               settings.GetValueOrDefault("weekly_pull_format") ??
                               settings.GetValueOrDefault("pull_format");

        var weeklyPullEnabled = settings.GetValueOrDefault("weeklypull_enable") ??
                                settings.GetValueOrDefault("weekly_pull_enabled") ??
                                settings.GetValueOrDefault("enable_weeklypull");

        var defaultMonitoring = settings.GetValueOrDefault("default_monitoring") ??
                                settings.GetValueOrDefault("series_monitoring") ??
                                settings.GetValueOrDefault("monitoring_mode");

        var autoAdd = settings.GetValueOrDefault("auto_add") ??
                      settings.GetValueOrDefault("auto_add_wanted") ??
                      settings.GetValueOrDefault("add_new_issues");

        var includeAnnuals = settings.GetValueOrDefault("include_annuals") ??
                             settings.GetValueOrDefault("annuals");

        var includeSpecials = settings.GetValueOrDefault("include_specials") ??
                              settings.GetValueOrDefault("specials");

        var skipVariants = settings.GetValueOrDefault("skip_variants") ??
                           settings.GetValueOrDefault("ignore_variants");

        var searchDelay = settings.GetValueOrDefault("search_delay") ??
                          settings.GetValueOrDefault("search_delay_hours") ??
                          settings.GetValueOrDefault("delay_hours");

        var weekStart = settings.GetValueOrDefault("week_start") ??
                        settings.GetValueOrDefault("week_start_day");

        // Track which settings were mapped
        var mappedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "weeklypull_folder", "weekly_pull_folder", "pull_folder",
            "weeklypull_format", "weekly_pull_format", "pull_format",
            "weeklypull_enable", "weekly_pull_enabled", "enable_weeklypull",
            "default_monitoring", "series_monitoring", "monitoring_mode",
            "auto_add", "auto_add_wanted", "add_new_issues",
            "include_annuals", "annuals", "include_specials", "specials",
            "skip_variants", "ignore_variants",
            "search_delay", "search_delay_hours", "delay_hours",
            "week_start", "week_start_day"
        };

        // Find unmapped pull list related settings
        foreach (var key in settings.Keys)
        {
            if (!mappedKeys.Contains(key) && 
                (key.Contains("pull", StringComparison.OrdinalIgnoreCase) ||
                 key.Contains("weekly", StringComparison.OrdinalIgnoreCase) ||
                 key.Contains("monitor", StringComparison.OrdinalIgnoreCase)))
            {
                unmapped.Add(key);
            }
        }

        return new Mylar3PullListSettings
        {
            WeeklyPullFolder = weeklyPullFolder,
            WeeklyPullFormat = weeklyPullFormat,
            WeeklyPullEnabled = !string.IsNullOrEmpty(weeklyPullEnabled) ? ParseBool(weeklyPullEnabled, false) : null,
            DefaultMonitoringMode = defaultMonitoring,
            AutoAddToWanted = !string.IsNullOrEmpty(autoAdd) ? ParseBool(autoAdd, true) : null,
            IncludeAnnuals = !string.IsNullOrEmpty(includeAnnuals) ? ParseBool(includeAnnuals, true) : null,
            IncludeSpecials = !string.IsNullOrEmpty(includeSpecials) ? ParseBool(includeSpecials, false) : null,
            SkipVariants = !string.IsNullOrEmpty(skipVariants) ? ParseBool(skipVariants, true) : null,
            SearchDelayHours = int.TryParse(searchDelay, out var delay) ? delay : null,
            WeekStartDay = int.TryParse(weekStart, out var day) ? day : null,
            RawSettings = rawSettings,
            UnmappedSettings = unmapped
        };
    }

    public async Task<Mylar3PullListImportResult> ImportPullListSettingsAsync(
        Mylar3PullListSettings settings,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        var result = new Mylar3PullListImportResult { Success = true };

        try
        {
            // Get current pull list settings
            var currentSettings = await _settingsService.GetAsync<PullListSettings>("pulllist", new PullListSettings(), cancellationToken)
                ?? new PullListSettings();

            // Import weekly export folder
            if (!string.IsNullOrEmpty(settings.WeeklyPullFolder))
            {
                if (string.IsNullOrEmpty(currentSettings.WeeklyExportDirectory) || overwriteExisting)
                {
                    currentSettings.WeeklyExportDirectory = settings.WeeklyPullFolder;
                    result.ImportedSettings.Add($"WeeklyExportDirectory: {settings.WeeklyPullFolder}");
                }
                else
                {
                    result.SkippedSettings.Add("WeeklyExportDirectory (already set)");
                }
            }

            // Import weekly export format
            if (!string.IsNullOrEmpty(settings.WeeklyPullFormat))
            {
                var format = MapWeeklyPullFormat(settings.WeeklyPullFormat);
                if (currentSettings.WeeklyExportFormat == default || overwriteExisting)
                {
                    currentSettings.WeeklyExportFormat = format;
                    result.ImportedSettings.Add($"WeeklyExportFormat: {format}");
                }
                else
                {
                    result.SkippedSettings.Add("WeeklyExportFormat (already set)");
                }
            }

            // Import weekly export enabled
            if (settings.WeeklyPullEnabled.HasValue)
            {
                if (!currentSettings.ExportWeeklyPullList || overwriteExisting)
                {
                    currentSettings.ExportWeeklyPullList = settings.WeeklyPullEnabled.Value;
                    result.ImportedSettings.Add($"ExportWeeklyPullList: {settings.WeeklyPullEnabled.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("ExportWeeklyPullList (already set)");
                }
            }

            // Import default monitoring mode
            if (!string.IsNullOrEmpty(settings.DefaultMonitoringMode))
            {
                if (MonitoringModeMapping.TryGetValue(settings.DefaultMonitoringMode, out var mode))
                {
                    if (currentSettings.DefaultMonitoringMode == SeriesMonitoringMode.FutureIssues || overwriteExisting)
                    {
                        currentSettings.DefaultMonitoringMode = mode;
                        result.ImportedSettings.Add($"DefaultMonitoringMode: {mode}");
                    }
                    else
                    {
                        result.SkippedSettings.Add("DefaultMonitoringMode (already set)");
                    }
                }
                else
                {
                    result.UnmappedSettings.Add($"DefaultMonitoringMode: {settings.DefaultMonitoringMode}");
                    result.Warnings.Add($"Unknown monitoring mode '{settings.DefaultMonitoringMode}'");
                }
            }

            // Import auto-add to wanted
            if (settings.AutoAddToWanted.HasValue)
            {
                if (overwriteExisting || currentSettings.AutoAddToWanted)
                {
                    currentSettings.AutoAddToWanted = settings.AutoAddToWanted.Value;
                    result.ImportedSettings.Add($"AutoAddToWanted: {settings.AutoAddToWanted.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("AutoAddToWanted (already set)");
                }
            }

            // Import include annuals
            if (settings.IncludeAnnuals.HasValue)
            {
                if (overwriteExisting || currentSettings.IncludeAnnualsInAutoAdd)
                {
                    currentSettings.IncludeAnnualsInAutoAdd = settings.IncludeAnnuals.Value;
                    result.ImportedSettings.Add($"IncludeAnnualsInAutoAdd: {settings.IncludeAnnuals.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("IncludeAnnualsInAutoAdd (already set)");
                }
            }

            // Import include specials
            if (settings.IncludeSpecials.HasValue)
            {
                if (overwriteExisting || !currentSettings.IncludeSpecialsInAutoAdd)
                {
                    currentSettings.IncludeSpecialsInAutoAdd = settings.IncludeSpecials.Value;
                    result.ImportedSettings.Add($"IncludeSpecialsInAutoAdd: {settings.IncludeSpecials.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("IncludeSpecialsInAutoAdd (already set)");
                }
            }

            // Import skip variants
            if (settings.SkipVariants.HasValue)
            {
                if (overwriteExisting || currentSettings.SkipVariantCovers)
                {
                    currentSettings.SkipVariantCovers = settings.SkipVariants.Value;
                    result.ImportedSettings.Add($"SkipVariantCovers: {settings.SkipVariants.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("SkipVariantCovers (already set)");
                }
            }

            // Import search delay
            if (settings.SearchDelayHours.HasValue)
            {
                if (currentSettings.SearchDelayHours == 6 || overwriteExisting) // 6 is default
                {
                    currentSettings.SearchDelayHours = settings.SearchDelayHours.Value;
                    result.ImportedSettings.Add($"SearchDelayHours: {settings.SearchDelayHours.Value}");
                }
                else
                {
                    result.SkippedSettings.Add("SearchDelayHours (already set)");
                }
            }

            // Import week start day
            if (settings.WeekStartDay.HasValue && settings.WeekStartDay.Value >= 0 && settings.WeekStartDay.Value <= 6)
            {
                var dayOfWeek = (DayOfWeek)settings.WeekStartDay.Value;
                if (currentSettings.WeekStartDay == DayOfWeek.Sunday || overwriteExisting)
                {
                    currentSettings.WeekStartDay = dayOfWeek;
                    result.ImportedSettings.Add($"WeekStartDay: {dayOfWeek}");
                }
                else
                {
                    result.SkippedSettings.Add("WeekStartDay (already set)");
                }
            }

            // Add unmapped settings from source
            result.UnmappedSettings.AddRange(settings.UnmappedSettings);

            // Save settings
            await _settingsService.SetAsync("pulllist", currentSettings, cancellationToken);

            _logger?.LogInformation(
                "Imported Mylar3 pull list settings: {Imported} imported, {Skipped} skipped, {Unmapped} unmapped",
                result.ImportedSettings.Count, result.SkippedSettings.Count, result.UnmappedSettings.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import pull list settings");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static WeeklyExportFormat MapWeeklyPullFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => WeeklyExportFormat.Json,
            "text" or "txt" => WeeklyExportFormat.Text,
            "csv" => WeeklyExportFormat.Csv,
            _ => WeeklyExportFormat.Json
        };
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderDefinition CreateProviderFromMylar3(Mylar3DdlProvider provider, string name, Mylar3ImportOptions options)
    {
        return new ProviderDefinition
        {
            Name = name,
            Implementation = "DdlProvider",
            Category = ProviderCategory.Indexer,
            Type = ProviderType.Ddl,
            IsEnabled = provider.IsEnabled,
            Priority = provider.Priority,
            BaseUrl = provider.BaseUrl,
            Username = options.ImportCredentials ? provider.Username : null,
            Password = options.ImportCredentials ? provider.Password : null,
            ApiKey = options.ImportCredentials ? provider.ApiKey : null,
            Settings = provider.Settings?.ToJson()
        };
    }

    private static void UpdateProviderFromMylar3(ProviderDefinition existing, Mylar3DdlProvider provider, Mylar3ImportOptions options)
    {
        existing.IsEnabled = provider.IsEnabled;
        existing.Priority = provider.Priority;
        existing.BaseUrl = provider.BaseUrl;
        
        if (options.ImportCredentials)
        {
            existing.Username = provider.Username;
            existing.Password = provider.Password;
            existing.ApiKey = provider.ApiKey;
        }
        
        existing.Settings = provider.Settings?.ToJson();
        existing.UpdatedAt = DateTime.UtcNow;
    }

    [GeneratedRegex(@"^\[([^\]]+)\]$")]
    private static partial Regex SectionRegex();
    
    [GeneratedRegex(@"^([^=]+)=(.*)$")]
    private static partial Regex KeyValueRegex();
}



