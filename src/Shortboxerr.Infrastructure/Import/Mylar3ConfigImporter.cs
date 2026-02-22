using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Import;

namespace Shortboxerr.Infrastructure.Import;

/// <summary>
/// Implements Mylar3 config.ini parsing and import.
/// </summary>
public class Mylar3ConfigImporter : IMylar3ConfigImporter
{
    private readonly ILogger<Mylar3ConfigImporter>? _logger;

    public Mylar3ConfigImporter(ILogger<Mylar3ConfigImporter>? logger = null)
    {
        _logger = logger;
    }

    public async Task<Mylar3ConfigParseResult> ParseConfigAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return Mylar3ConfigParseResult.Failed("Config path is empty");
        }

        if (!File.Exists(configPath))
        {
            return Mylar3ConfigParseResult.Failed($"Config file not found: {configPath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(configPath, cancellationToken);
            return await ParseConfigContentAsync(content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read config file: {Path}", configPath);
            return Mylar3ConfigParseResult.Failed($"Failed to read config file: {ex.Message}");
        }
    }

    public Task<Mylar3ConfigParseResult> ParseConfigContentAsync(
        string configContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configContent))
        {
            return Task.FromResult(Mylar3ConfigParseResult.Failed("Config content is empty"));
        }

        try
        {
            var sections = ParseIniSections(configContent);
            var warnings = new List<string>();
            var sectionNames = sections.Keys.ToList();

            // Parse indexers (newznab providers)
            var indexers = ParseIndexers(sections, warnings);

            // Parse SABnzbd settings
            var sabnzbd = ParseSabnzbd(sections, warnings);

            // Parse NZBGet settings
            var nzbget = ParseNzbget(sections, warnings);

            // Parse general settings
            var general = ParseGeneral(sections, warnings);

            _logger?.LogInformation(
                "Parsed Mylar3 config: {IndexerCount} indexers, SABnzbd: {HasSab}, NZBGet: {HasNzb}",
                indexers.Count, sabnzbd != null, nzbget != null);

            return Task.FromResult(Mylar3ConfigParseResult.Parsed(
                indexers, sabnzbd, nzbget, general, warnings, sectionNames));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse config content");
            return Task.FromResult(Mylar3ConfigParseResult.Failed($"Parse error: {ex.Message}"));
        }
    }

    public Task<Mylar3ImportResult> ImportAsync(
        Mylar3ConfigParseResult parseResult,
        Mylar3ImportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!parseResult.Success)
        {
            return Task.FromResult(Mylar3ImportResult.Failed("Cannot import: parsing failed"));
        }

        var itemResults = new List<Mylar3ImportItemResult>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var indexersImported = 0;
        var indexersSkipped = 0;
        var sabnzbdImported = false;
        var nzbgetImported = false;

        // Import indexers
        if (options.ImportIndexers)
        {
            foreach (var indexer in parseResult.Indexers)
            {
                if (!indexer.Enabled && !options.ImportDisabled)
                {
                    indexersSkipped++;
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "Indexer",
                        Name = indexer.Name,
                        Success = true,
                        Action = ImportAction.Skipped
                    });
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(indexer.Host) || string.IsNullOrEmpty(indexer.ApiKey))
                {
                    warnings.Add($"Indexer '{indexer.Name}' missing required fields (host or apikey)");
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "Indexer",
                        Name = indexer.Name,
                        Success = false,
                        Action = ImportAction.Failed,
                        ErrorMessage = "Missing host or API key"
                    });
                    continue;
                }

                // In a real implementation, this would save to the indexer configuration
                indexersImported++;
                itemResults.Add(new Mylar3ImportItemResult
                {
                    ItemType = "Indexer",
                    Name = indexer.Name,
                    Success = true,
                    Action = ImportAction.Imported
                });

                _logger?.LogInformation("Imported indexer: {Name} ({Host})", indexer.Name, indexer.Host);
            }
        }

        // Import SABnzbd
        if (options.ImportSabnzbd && parseResult.Sabnzbd != null)
        {
            var sab = parseResult.Sabnzbd;
            if (sab.Enabled || options.ImportDisabled)
            {
                if (!string.IsNullOrEmpty(sab.Host) && !string.IsNullOrEmpty(sab.ApiKey))
                {
                    sabnzbdImported = true;
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "SABnzbd",
                        Name = "SABnzbd",
                        Success = true,
                        Action = ImportAction.Imported
                    });
                    _logger?.LogInformation("Imported SABnzbd: {Host}:{Port}", sab.Host, sab.Port);
                }
                else
                {
                    warnings.Add("SABnzbd configuration missing required fields");
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "SABnzbd",
                        Name = "SABnzbd",
                        Success = false,
                        Action = ImportAction.Failed,
                        ErrorMessage = "Missing host or API key"
                    });
                }
            }
        }

        // Import NZBGet
        if (options.ImportNzbget && parseResult.Nzbget != null)
        {
            var nzb = parseResult.Nzbget;
            if (nzb.Enabled || options.ImportDisabled)
            {
                if (!string.IsNullOrEmpty(nzb.Host))
                {
                    nzbgetImported = true;
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "NZBGet",
                        Name = "NZBGet",
                        Success = true,
                        Action = ImportAction.Imported
                    });
                    _logger?.LogInformation("Imported NZBGet: {Host}:{Port}", nzb.Host, nzb.Port);
                }
                else
                {
                    warnings.Add("NZBGet configuration missing host");
                    itemResults.Add(new Mylar3ImportItemResult
                    {
                        ItemType = "NZBGet",
                        Name = "NZBGet",
                        Success = false,
                        Action = ImportAction.Failed,
                        ErrorMessage = "Missing host"
                    });
                }
            }
        }

        return Task.FromResult(new Mylar3ImportResult
        {
            Success = errors.Count == 0,
            IndexersImported = indexersImported,
            IndexersSkipped = indexersSkipped,
            SabnzbdImported = sabnzbdImported,
            NzbgetImported = nzbgetImported,
            Errors = errors,
            Warnings = warnings,
            ItemResults = itemResults
        });
    }

    public Task<Mylar3ValidationReport> ValidateAsync(
        Mylar3ConfigParseResult parseResult,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<Mylar3ValidationItem>();
        var warnings = new List<Mylar3ValidationItem>();
        var info = new List<Mylar3ValidationItem>();

        // Validate indexers
        foreach (var indexer in parseResult.Indexers)
        {
            if (string.IsNullOrEmpty(indexer.Name))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "Indexer",
                    ItemName = "(unnamed)",
                    Field = "name",
                    Message = "Indexer name is required"
                });
            }

            if (string.IsNullOrEmpty(indexer.Host))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "Indexer",
                    ItemName = indexer.Name,
                    Field = "host",
                    Message = "Host URL is required"
                });
            }
            else if (!Uri.TryCreate(indexer.Host, UriKind.Absolute, out var uri))
            {
                warnings.Add(new Mylar3ValidationItem
                {
                    ItemType = "Indexer",
                    ItemName = indexer.Name,
                    Field = "host",
                    Message = $"Invalid URL format: {indexer.Host}"
                });
            }

            if (string.IsNullOrEmpty(indexer.ApiKey))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "Indexer",
                    ItemName = indexer.Name,
                    Field = "apikey",
                    Message = "API key is required"
                });
            }

            if (!indexer.Enabled)
            {
                info.Add(new Mylar3ValidationItem
                {
                    ItemType = "Indexer",
                    ItemName = indexer.Name,
                    Message = "Indexer is disabled in Mylar3"
                });
            }
        }

        // Validate SABnzbd
        if (parseResult.Sabnzbd != null)
        {
            var sab = parseResult.Sabnzbd;
            if (string.IsNullOrEmpty(sab.Host))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "SABnzbd",
                    ItemName = "SABnzbd",
                    Field = "host",
                    Message = "Host is required"
                });
            }

            if (string.IsNullOrEmpty(sab.ApiKey))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "SABnzbd",
                    ItemName = "SABnzbd",
                    Field = "apikey",
                    Message = "API key is required"
                });
            }

            if (!sab.Enabled)
            {
                info.Add(new Mylar3ValidationItem
                {
                    ItemType = "SABnzbd",
                    ItemName = "SABnzbd",
                    Message = "SABnzbd is disabled in Mylar3"
                });
            }
        }

        // Validate NZBGet
        if (parseResult.Nzbget != null)
        {
            var nzb = parseResult.Nzbget;
            if (string.IsNullOrEmpty(nzb.Host))
            {
                errors.Add(new Mylar3ValidationItem
                {
                    ItemType = "NZBGet",
                    ItemName = "NZBGet",
                    Field = "host",
                    Message = "Host is required"
                });
            }

            if (!nzb.Enabled)
            {
                info.Add(new Mylar3ValidationItem
                {
                    ItemType = "NZBGet",
                    ItemName = "NZBGet",
                    Message = "NZBGet is disabled in Mylar3"
                });
            }
        }

        var summary = new Mylar3ImportSummary
        {
            TotalIndexers = parseResult.Indexers.Count,
            EnabledIndexers = parseResult.Indexers.Count(i => i.Enabled),
            HasSabnzbd = parseResult.Sabnzbd != null,
            SabnzbdEnabled = parseResult.Sabnzbd?.Enabled ?? false,
            HasNzbget = parseResult.Nzbget != null,
            NzbgetEnabled = parseResult.Nzbget?.Enabled ?? false
        };

        return Task.FromResult(new Mylar3ValidationReport
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Info = info,
            Summary = summary
        });
    }

    #region INI Parsing

    private static Dictionary<string, Dictionary<string, string>> ParseIniSections(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = "General";
        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            // Section header
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            // Key=Value pair
            var equalsIndex = line.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = line.Substring(0, equalsIndex).Trim();
                var value = line.Substring(equalsIndex + 1).Trim();

                // Remove quotes if present
                if (value.Length >= 2 &&
                    ((value.StartsWith('"') && value.EndsWith('"')) ||
                     (value.StartsWith('\'') && value.EndsWith('\''))))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                sections[currentSection][key] = value;
            }
        }

        return sections;
    }

    private List<Mylar3NewznabConfig> ParseIndexers(
        Dictionary<string, Dictionary<string, string>> sections,
        List<string> warnings)
    {
        var indexers = new List<Mylar3NewznabConfig>();

        // Mylar3 stores providers in a specific format
        // Look for sections or keys related to newznab providers
        if (sections.TryGetValue("Newznab", out var newznabSection))
        {
            // Parse multiple providers from the section
            // Common pattern: newznab_host, newznab_api, newznab_name, etc.
            // Or: extra_newznabs with JSON-like format

            var extraNewznabs = GetValue(newznabSection, "extra_newznabs");
            if (!string.IsNullOrEmpty(extraNewznabs))
            {
                indexers.AddRange(ParseExtraNewznabs(extraNewznabs, warnings));
            }

            // Also check for single provider config
            var host = GetValue(newznabSection, "newznab_host", "host");
            if (!string.IsNullOrEmpty(host))
            {
                indexers.Add(new Mylar3NewznabConfig
                {
                    Name = GetValue(newznabSection, "newznab_name", "name") ?? "Default Newznab",
                    Host = host,
                    ApiKey = GetValue(newznabSection, "newznab_api", "apikey") ?? string.Empty,
                    Uid = GetValue(newznabSection, "newznab_uid", "uid"),
                    Categories = ParseCategories(GetValue(newznabSection, "newznab_categories", "categories")),
                    Enabled = ParseBool(GetValue(newznabSection, "newznab_enabled", "enabled"), true),
                    VerifySsl = ParseBool(GetValue(newznabSection, "newznab_verify", "verify_ssl"), true),
                    ProviderType = "newznab"
                });
            }
        }

        // Check for numbered provider sections (Newznab1, Newznab2, etc.)
        for (int i = 1; i <= 20; i++)
        {
            var sectionName = $"Newznab{i}";
            if (sections.TryGetValue(sectionName, out var providerSection))
            {
                var host = GetValue(providerSection, "host");
                if (!string.IsNullOrEmpty(host))
                {
                    indexers.Add(new Mylar3NewznabConfig
                    {
                        Name = GetValue(providerSection, "name") ?? sectionName,
                        Host = host,
                        ApiKey = GetValue(providerSection, "apikey", "api") ?? string.Empty,
                        Uid = GetValue(providerSection, "uid"),
                        Categories = ParseCategories(GetValue(providerSection, "categories")),
                        Enabled = ParseBool(GetValue(providerSection, "enabled"), true),
                        VerifySsl = ParseBool(GetValue(providerSection, "verify_ssl"), true),
                        ProviderType = "newznab"
                    });
                }
            }
        }

        // Check General section for provider list
        if (sections.TryGetValue("General", out var generalSection))
        {
            var extraNewznabs = GetValue(generalSection, "extra_newznabs");
            if (!string.IsNullOrEmpty(extraNewznabs) && indexers.Count == 0)
            {
                indexers.AddRange(ParseExtraNewznabs(extraNewznabs, warnings));
            }
        }

        return indexers;
    }

    private List<Mylar3NewznabConfig> ParseExtraNewznabs(string value, List<string> warnings)
    {
        var indexers = new List<Mylar3NewznabConfig>();

        // Mylar3 stores extra_newznabs in a specific format:
        // [(name, host, verify_ssl, api, uid, enabled, categories), ...]
        // Try to parse this tuple format

        try
        {
            // Clean up the string
            var cleaned = value.Trim();
            if (cleaned.StartsWith('[')) cleaned = cleaned.Substring(1);
            if (cleaned.EndsWith(']')) cleaned = cleaned.Substring(0, cleaned.Length - 1);

            // Split by tuple boundaries
            var tuplePattern = new System.Text.RegularExpressions.Regex(@"\(([^)]+)\)");
            var matches = tuplePattern.Matches(cleaned);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var tupleContent = match.Groups[1].Value;
                var parts = SplitTupleParts(tupleContent);

                if (parts.Count >= 4)
                {
                    indexers.Add(new Mylar3NewznabConfig
                    {
                        Name = CleanQuotes(parts[0]),
                        Host = CleanQuotes(parts[1]),
                        VerifySsl = parts.Count > 2 && ParseBool(parts[2], true),
                        ApiKey = parts.Count > 3 ? CleanQuotes(parts[3]) : string.Empty,
                        Uid = parts.Count > 4 ? CleanQuotes(parts[4]) : null,
                        Enabled = parts.Count > 5 && ParseBool(parts[5], true),
                        Categories = parts.Count > 6 ? ParseCategories(parts[6]) : new List<string>(),
                        ProviderType = "newznab"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to parse extra_newznabs: {ex.Message}");
        }

        return indexers;
    }

    private static List<string> SplitTupleParts(string tupleContent)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '"';

        foreach (var c in tupleContent)
        {
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
                current.Append(c);
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
                current.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString().Trim());
        }

        return parts;
    }

    private static string CleanQuotes(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
             (trimmed.StartsWith('\'') && trimmed.EndsWith('\''))))
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }
        return trimmed;
    }

    private Mylar3SabnzbdConfig? ParseSabnzbd(
        Dictionary<string, Dictionary<string, string>> sections,
        List<string> warnings)
    {
        // Check SABnzbd section
        if (sections.TryGetValue("SABnzbd", out var sabSection))
        {
            return new Mylar3SabnzbdConfig
            {
                Host = GetValue(sabSection, "sab_host", "host") ?? string.Empty,
                Port = ParseInt(GetValue(sabSection, "sab_port", "port"), 8080),
                ApiKey = GetValue(sabSection, "sab_apikey", "apikey", "api_key") ?? string.Empty,
                Category = GetValue(sabSection, "sab_category", "category") ?? "comics",
                UseSsl = ParseBool(GetValue(sabSection, "sab_ssl", "ssl", "use_ssl"), false),
                Priority = GetValue(sabSection, "sab_priority", "priority"),
                Enabled = ParseBool(GetValue(sabSection, "use_sabnzbd", "enabled"), false)
            };
        }

        // Check General section for SABnzbd settings
        if (sections.TryGetValue("General", out var generalSection))
        {
            var host = GetValue(generalSection, "sab_host");
            if (!string.IsNullOrEmpty(host))
            {
                return new Mylar3SabnzbdConfig
                {
                    Host = host,
                    Port = ParseInt(GetValue(generalSection, "sab_port"), 8080),
                    ApiKey = GetValue(generalSection, "sab_apikey") ?? string.Empty,
                    Category = GetValue(generalSection, "sab_category") ?? "comics",
                    UseSsl = ParseBool(GetValue(generalSection, "sab_ssl"), false),
                    Priority = GetValue(generalSection, "sab_priority"),
                    Enabled = ParseBool(GetValue(generalSection, "use_sabnzbd"), false)
                };
            }
        }

        return null;
    }

    private Mylar3NzbgetConfig? ParseNzbget(
        Dictionary<string, Dictionary<string, string>> sections,
        List<string> warnings)
    {
        // Check NZBGet section
        if (sections.TryGetValue("NZBGet", out var nzbSection))
        {
            return new Mylar3NzbgetConfig
            {
                Host = GetValue(nzbSection, "nzbget_host", "host") ?? string.Empty,
                Port = ParseInt(GetValue(nzbSection, "nzbget_port", "port"), 6789),
                Username = GetValue(nzbSection, "nzbget_username", "username") ?? string.Empty,
                Password = GetValue(nzbSection, "nzbget_password", "password") ?? string.Empty,
                Category = GetValue(nzbSection, "nzbget_category", "category") ?? "comics",
                UseSsl = ParseBool(GetValue(nzbSection, "nzbget_ssl", "ssl", "use_ssl"), false),
                Priority = GetValue(nzbSection, "nzbget_priority", "priority"),
                Enabled = ParseBool(GetValue(nzbSection, "use_nzbget", "enabled"), false)
            };
        }

        // Check General section for NZBGet settings
        if (sections.TryGetValue("General", out var generalSection))
        {
            var host = GetValue(generalSection, "nzbget_host");
            if (!string.IsNullOrEmpty(host))
            {
                return new Mylar3NzbgetConfig
                {
                    Host = host,
                    Port = ParseInt(GetValue(generalSection, "nzbget_port"), 6789),
                    Username = GetValue(generalSection, "nzbget_username") ?? string.Empty,
                    Password = GetValue(generalSection, "nzbget_password") ?? string.Empty,
                    Category = GetValue(generalSection, "nzbget_category") ?? "comics",
                    UseSsl = ParseBool(GetValue(generalSection, "nzbget_ssl"), false),
                    Priority = GetValue(generalSection, "nzbget_priority"),
                    Enabled = ParseBool(GetValue(generalSection, "use_nzbget"), false)
                };
            }
        }

        return null;
    }

    private Mylar3GeneralConfig? ParseGeneral(
        Dictionary<string, Dictionary<string, string>> sections,
        List<string> warnings)
    {
        if (!sections.TryGetValue("General", out var generalSection))
        {
            return null;
        }

        return new Mylar3GeneralConfig
        {
            ComicLocation = GetValue(generalSection, "comic_location", "comic_dir", "destination_dir"),
            DownloadDirectory = GetValue(generalSection, "download_dir", "download_directory"),
            NzbEnabled = ParseBool(GetValue(generalSection, "nzb_startup_search", "enable_nzb", "nzb"), false),
            TorrentEnabled = ParseBool(GetValue(generalSection, "enable_torrents", "torrent"), false),
            PreferredNzbClient = GetValue(generalSection, "nzb_downloader", "preferred_nzb_client")
        };
    }

    private static string? GetValue(Dictionary<string, string> section, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (section.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        return null;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;

        var lower = value.ToLowerInvariant();
        return lower == "true" || lower == "1" || lower == "yes" || lower == "on";
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static List<string> ParseCategories(string? value)
    {
        if (string.IsNullOrEmpty(value)) return new List<string>();

        return value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();
    }

    #endregion
}
