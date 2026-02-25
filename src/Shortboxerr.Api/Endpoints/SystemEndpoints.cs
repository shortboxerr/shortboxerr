using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Metron;
using Shortboxerr.Core.PullList;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Api.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/system")
            .WithTags("System");

        group.MapGet("/info", GetSystemInfo)
            .WithName("GetSystemInfo")
            .WithOpenApi()
            .Produces<SystemInfoResponse>(200);

        group.MapGet("/status", GetSystemStatus)
            .WithName("GetSystemStatusV2")
            .WithOpenApi()
            .Produces<SystemStatusResponse>(200);

        group.MapGet("/logs", GetLogFiles)
            .WithName("GetLogFiles")
            .WithOpenApi()
            .Produces<LogFilesResponse>(200);

        group.MapGet("/logs/{filename}", GetLogFileContent)
            .WithName("GetLogFileContent")
            .WithOpenApi()
            .Produces<LogContentResponse>(200)
            .Produces(404);

        group.MapGet("/logs/recent", GetRecentLogs)
            .WithName("GetRecentLogs")
            .WithOpenApi()
            .Produces<LogContentResponse>(200);

        group.MapDelete("/logs/{filename}", DeleteLogFile)
            .WithName("DeleteLogFile")
            .WithOpenApi()
            .Produces(204)
            .Produces(404);

        group.MapGet("/metron/rate-limits", GetMetronRateLimits)
            .WithName("GetMetronRateLimits")
            .WithOpenApi()
            .Produces<MetronRateLimitStats>(200);
    }

    private static IResult GetMetronRateLimits(IMetronClient metronClient)
    {
        var stats = metronClient.GetRateLimitStats();
        return Results.Ok(stats);
    }

    private static IResult GetLogFileContent(string filename, int? lines = 500, string? level = null, string? search = null)
    {
        var logDirectory = GetLogDirectory();
        var filePath = Path.Combine(logDirectory, filename);

        // Security: ensure the file is within the log directory
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(Path.GetFullPath(logDirectory)))
        {
            return Results.NotFound("Log file not found");
        }

        if (!File.Exists(fullPath))
        {
            return Results.NotFound("Log file not found");
        }

        var allLines = File.ReadAllLines(fullPath);
        var filteredLines = FilterLogLines(allLines, level, search);
        
        // Take last N lines
        var resultLines = filteredLines.TakeLast(lines ?? 500).ToList();

        return Results.Ok(new LogContentResponse
        {
            FileName = filename,
            TotalLines = allLines.Length,
            FilteredLines = filteredLines.Count,
            ReturnedLines = resultLines.Count,
            Lines = resultLines.Select(ParseLogLine).ToList()
        });
    }

    private static IResult GetRecentLogs(int? lines = 100, string? level = null, string? search = null)
    {
        var logDirectory = GetLogDirectory();
        
        if (!Directory.Exists(logDirectory))
        {
            return Results.Ok(new LogContentResponse
            {
                FileName = "recent",
                TotalLines = 0,
                FilteredLines = 0,
                ReturnedLines = 0,
                Lines = new List<LogLine>()
            });
        }

        // Get the most recent log file
        var recentFile = Directory.GetFiles(logDirectory, "*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (recentFile == null)
        {
            return Results.Ok(new LogContentResponse
            {
                FileName = "recent",
                TotalLines = 0,
                FilteredLines = 0,
                ReturnedLines = 0,
                Lines = new List<LogLine>()
            });
        }

        return GetLogFileContent(recentFile.Name, lines, level, search);
    }

    private static IResult DeleteLogFile(string filename)
    {
        var logDirectory = GetLogDirectory();
        var filePath = Path.Combine(logDirectory, filename);

        // Security: ensure the file is within the log directory
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(Path.GetFullPath(logDirectory)))
        {
            return Results.NotFound("Log file not found");
        }

        if (!File.Exists(fullPath))
        {
            return Results.NotFound("Log file not found");
        }

        File.Delete(fullPath);
        return Results.NoContent();
    }

    private static string GetLogDirectory()
    {
        return Shortboxerr.Infrastructure.Logging.SerilogConfiguration.GetLogDirectory();
    }

    private static List<string> FilterLogLines(string[] lines, string? level, string? search)
    {
        IEnumerable<string> filtered = lines;

        if (!string.IsNullOrEmpty(level))
        {
            var levels = GetLevelsAtOrAbove(level);
            filtered = filtered.Where(line => levels.Any(l => line.Contains($"[{l}]", StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(line => line.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    private static List<string> GetLevelsAtOrAbove(string level)
    {
        var allLevels = new[] { "VRB", "DBG", "INF", "WRN", "ERR", "FTL" };
        var levelIndex = Array.FindIndex(allLevels, l => l.Equals(level, StringComparison.OrdinalIgnoreCase));
        
        if (levelIndex < 0)
        {
            // Try full names
            var fullLevels = new[] { "VERBOSE", "DEBUG", "INFORMATION", "WARNING", "ERROR", "FATAL" };
            levelIndex = Array.FindIndex(fullLevels, l => l.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        return levelIndex >= 0 ? allLevels.Skip(levelIndex).ToList() : allLevels.ToList();
    }

    private static LogLine ParseLogLine(string line)
    {
        // Parse format: [2026-02-04 21:57:42.630] [INF] [Category] Message
        var logLine = new LogLine { Raw = line };

        try
        {
            // Extract timestamp
            var timestampMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]");
            if (timestampMatch.Success)
            {
                logLine.Timestamp = DateTime.TryParse(timestampMatch.Groups[1].Value, out var ts) ? ts : null;
            }

            // Extract level
            var levelMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(VRB|DBG|INF|WRN|ERR|FTL)\]");
            if (levelMatch.Success)
            {
                logLine.Level = levelMatch.Groups[1].Value;
            }

            // Extract category
            var categoryMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[(?:VRB|DBG|INF|WRN|ERR|FTL)\] \[([^\]]+)\]");
            if (categoryMatch.Success)
            {
                logLine.Category = categoryMatch.Groups[1].Value;
            }

            // Extract message (everything after the category or level)
            var messageStart = line.LastIndexOf(']');
            if (messageStart >= 0 && messageStart < line.Length - 1)
            {
                logLine.Message = line[(messageStart + 1)..].Trim();
            }
        }
        catch
        {
            // If parsing fails, just use the raw line
            logLine.Message = line;
        }

        return logLine;
    }

    private static IResult GetSystemInfo(
        ShortboxerrDbContext dbContext,
        IConfiguration configuration)
    {
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";

        // Get directories using centralized container-first logic
        var dataDirectory = Shortboxerr.Infrastructure.Logging.SerilogConfiguration.GetDataDirectory();
        var logDirectory = Shortboxerr.Infrastructure.Logging.SerilogConfiguration.GetLogDirectory();

        // Get database info
        var dbPath = dbContext.Database.GetDbConnection().ConnectionString;
        var dbProvider = dbContext.Database.ProviderName ?? "Unknown";

        // Get disk space for data directory
        DiskSpaceInfo? diskSpace = null;
        try
        {
            if (Directory.Exists(dataDirectory))
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(dataDirectory) ?? "/");
                diskSpace = new DiskSpaceInfo
                {
                    Path = dataDirectory,
                    TotalBytes = driveInfo.TotalSize,
                    FreeBytes = driveInfo.AvailableFreeSpace,
                    UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace
                };
            }
        }
        catch
        {
            // Disk info may not be available on all platforms
        }

        var response = new SystemInfoResponse
        {
            AppName = "Shortboxerr",
            Version = version,
            Branch = "main",
            BuildTime = assembly.GetCustomAttribute<AssemblyMetadataAttribute>()?.Value,
            
            // Runtime info
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            
            // OS info
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            
            // Database info
            DatabaseProvider = dbProvider,
            DatabasePath = dbPath,
            
            // Directories
            DataDirectory = dataDirectory,
            LogDirectory = logDirectory,
            
            // Memory
            WorkingSetBytes = process.WorkingSet64,
            PrivateMemoryBytes = process.PrivateMemorySize64,
            GcTotalMemoryBytes = GC.GetTotalMemory(false),
            
            // Uptime
            StartTime = StartTime,
            Uptime = DateTime.UtcNow - StartTime,
            
            // Disk space
            DiskSpace = diskSpace
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> GetSystemStatus(
        ShortboxerrDbContext dbContext,
        [FromServices] Shortboxerr.Core.Providers.IProviderManager providerManager,
        [FromServices] Shortboxerr.Core.Ddl.IDdlSiteAdapterFactory ddlFactory,
        [FromServices] Shortboxerr.Core.Nzb.INzbIndexerProvider nzbIndexerProvider,
        [FromServices] IPullListService pullListService)
    {
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";

        // Check if series-annual integration is enabled (defaults to true)
        var settings = await pullListService.GetSettingsAsync();
        var hideLinkedAnnuals = settings.EnableSeriesAnnualIntegration ?? true;

        // Query actual statistics from database
        // If series-annual integration is enabled, exclude linked annual series
        var seriesCount = hideLinkedAnnuals
            ? await dbContext.Series.Where(s => !s.ParentSeriesId.HasValue).CountAsync()
            : await dbContext.Series.CountAsync();
        var issuesCount = await dbContext.Issues.CountAsync();
        var collectionsCount = await dbContext.EditionTitles.CountAsync();
        var filesCount = await dbContext.FileAssets.CountAsync();
        
        // Get NZB indexer count from NzbIndexerProvider (Newznab indexers like NZBgeek)
        int nzbIndexers = 0;
        try
        {
            var indexers = await nzbIndexerProvider.GetIndexersAsync();
            nzbIndexers = indexers.Count(i => i.Enabled);
        }
        catch
        {
            // Ignore errors
        }
        
        // Get DDL site count (also count as indexers)
        int ddlSites = 0;
        try
        {
            var enabledSites = ddlFactory.GetEnabledSites();
            ddlSites = enabledSites.Count;
        }
        catch
        {
            // Ignore errors
        }
        
        var enabledIndexers = nzbIndexers + ddlSites;
        var indexerStatus = enabledIndexers > 0 ? "healthy" : "warning";

        return Results.Ok(new SystemStatusResponse
        {
            AppName = "Shortboxerr",
            Version = version,
            StartTime = StartTime,
            Uptime = DateTime.UtcNow - StartTime,
            IsHealthy = true,
            WorkingSetMb = process.WorkingSet64 / (1024.0 * 1024.0),
            
            // Statistics
            SeriesCount = seriesCount,
            IssuesCount = issuesCount,
            CollectionsCount = collectionsCount,
            FilesCount = filesCount,
            EnabledIndexers = enabledIndexers,
            IndexerStatus = indexerStatus,
            DatabaseStatus = "Connected",
            QueuedDownloads = 0  // TODO: connect to actual queue when implemented
        });
    }

    private static IResult GetLogFiles()
    {
        var logDirectory = GetLogDirectory();

        var files = new List<LogFileInfo>();

        if (Directory.Exists(logDirectory))
        {
            foreach (var file in Directory.GetFiles(logDirectory, "*.log")
                .Concat(Directory.GetFiles(logDirectory, "*.txt")))
            {
                var fileInfo = new FileInfo(file);
                files.Add(new LogFileInfo
                {
                    FileName = fileInfo.Name,
                    FilePath = fileInfo.FullName,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    Created = fileInfo.CreationTimeUtc
                });
            }
        }

        return Results.Ok(new LogFilesResponse
        {
            LogDirectory = logDirectory,
            Files = files.OrderByDescending(f => f.LastModified).ToList()
        });
    }
}

#region Response DTOs

public class SystemInfoResponse
{
    // Application
    public required string AppName { get; set; }
    public required string Version { get; set; }
    public string? Branch { get; set; }
    public string? BuildTime { get; set; }

    // Runtime
    public required string RuntimeVersion { get; set; }
    public required string RuntimeIdentifier { get; set; }

    // Operating System
    public required string OsDescription { get; set; }
    public required string OsArchitecture { get; set; }
    public required string ProcessArchitecture { get; set; }

    // Database
    public required string DatabaseProvider { get; set; }
    public string? DatabasePath { get; set; }

    // Directories
    public required string DataDirectory { get; set; }
    public required string LogDirectory { get; set; }

    // Memory (in bytes)
    public long WorkingSetBytes { get; set; }
    public long PrivateMemoryBytes { get; set; }
    public long GcTotalMemoryBytes { get; set; }

    // Uptime
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }

    // Disk
    public DiskSpaceInfo? DiskSpace { get; set; }
}

public class DiskSpaceInfo
{
    public required string Path { get; set; }
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    
    public double UsedPercent => TotalBytes > 0 ? (UsedBytes * 100.0 / TotalBytes) : 0;
    public string TotalFormatted => FormatBytes(TotalBytes);
    public string FreeFormatted => FormatBytes(FreeBytes);
    public string UsedFormatted => FormatBytes(UsedBytes);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class SystemStatusResponse
{
    public required string AppName { get; set; }
    public required string Version { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
    public bool IsHealthy { get; set; }
    public double WorkingSetMb { get; set; }
    
    // Statistics
    public int SeriesCount { get; set; }
    public int IssuesCount { get; set; }
    public int CollectionsCount { get; set; }
    public int FilesCount { get; set; }
    public int EnabledIndexers { get; set; }
    public string IndexerStatus { get; set; } = "healthy";
    public string DatabaseStatus { get; set; } = "Connected";
    public int QueuedDownloads { get; set; }
}

public class LogFilesResponse
{
    public required string LogDirectory { get; set; }
    public List<LogFileInfo> Files { get; set; } = new();
}

public class LogFileInfo
{
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    
    public string SizeFormatted
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = SizeBytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

public class LogContentResponse
{
    public required string FileName { get; set; }
    public int TotalLines { get; set; }
    public int FilteredLines { get; set; }
    public int ReturnedLines { get; set; }
    public List<LogLine> Lines { get; set; } = new();
}

public class LogLine
{
    public required string Raw { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string? Message { get; set; }
}

#endregion
