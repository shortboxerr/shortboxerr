using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
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
    }

    private static IResult GetSystemInfo(
        ShortboxerrDbContext dbContext,
        IConfiguration configuration)
    {
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";

        // Get data directory
        var dataDirectory = configuration.GetValue<string>("DataDirectory")
            ?? Environment.GetEnvironmentVariable("SHORTBOXERR_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "shortboxerr");

        // Get log directory
        var logDirectory = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_DIR")
            ?? Path.Combine(dataDirectory, "logs");

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

    private static IResult GetSystemStatus()
    {
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";

        return Results.Ok(new SystemStatusResponse
        {
            AppName = "Shortboxerr",
            Version = version,
            StartTime = StartTime,
            Uptime = DateTime.UtcNow - StartTime,
            IsHealthy = true,
            WorkingSetMb = process.WorkingSet64 / (1024.0 * 1024.0)
        });
    }

    private static IResult GetLogFiles()
    {
        var logDirectory = Environment.GetEnvironmentVariable("SHORTBOXERR_LOG_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "shortboxerr",
                "logs");

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

#endregion
