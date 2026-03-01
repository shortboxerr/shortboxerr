using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Service for reorganizing library files to match current naming format settings.
/// </summary>
public class LibraryOrganizationService : ILibraryOrganizationService
{
    private readonly ShortboxerrDbContext _db;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LibraryOrganizationService> _logger;
    private readonly string[] _libraryRoots;

    public LibraryOrganizationService(
        ShortboxerrDbContext db,
        ISettingsService settingsService,
        IConfiguration configuration,
        ILogger<LibraryOrganizationService> logger)
    {
        _db = db;
        _settingsService = settingsService;
        _logger = logger;
        
        _libraryRoots = configuration.GetSection("MediaManagement:RootFolders").Get<string[]>()
            ?? new[] { Environment.GetEnvironmentVariable("SHORTBOXERR_LIBRARY_ROOT") ?? "/data/library" };
    }

    public async Task<IReadOnlyList<SeriesRenamePreview>> GetSeriesRenamePreviewsAsync(
        int[] seriesIds,
        CancellationToken cancellationToken = default)
    {
        var previews = new List<SeriesRenamePreview>();
        
        var query = _db.Series
            .Include(s => s.Issues)
            .Include(s => s.Editions)
            .AsSplitQuery()
            .Where(s => s.ParentSeriesId == null);
        
        if (seriesIds.Length > 0)
        {
            query = query.Where(s => seriesIds.Contains(s.Id));
        }

        var seriesList = await query.ToListAsync(cancellationToken);
        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        
        foreach (var series in seriesList)
        {
            var preview = await BuildSeriesPreviewAsync(series, settings, cancellationToken);
            previews.Add(preview);
        }

        return previews;
    }

    public async Task<SeriesRenamePreview?> GetSeriesRenamePreviewAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var series = await _db.Series
            .Include(s => s.Issues)
            .Include(s => s.Editions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return null;
        }

        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        return await BuildSeriesPreviewAsync(series, settings, cancellationToken);
    }

    public async Task<IReadOnlyList<SeriesRenameResult>> ExecuteSeriesRenameAsync(
        int[] seriesIds,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (seriesIds.Length == 0)
        {
            throw new ArgumentException("Must provide at least one series ID", nameof(seriesIds));
        }

        if (dryRun)
        {
            _logger.LogInformation("Executing organization in DRY RUN mode for {Count} series", seriesIds.Length);
        }

        var results = new List<SeriesRenameResult>();
        
        foreach (var seriesId in seriesIds)
        {
            var result = await ExecuteSeriesRenameAsync(seriesId, dryRun, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<SeriesRenameResult> ExecuteSeriesRenameAsync(
        int seriesId,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var series = await _db.Series
            .Include(s => s.Issues)
            .Include(s => s.Editions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series == null)
        {
            return new SeriesRenameResult
            {
                SeriesId = seriesId,
                SeriesTitle = "Unknown",
                Success = false,
                Error = "Series not found"
            };
        }

        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        var preview = await BuildSeriesPreviewAsync(series, settings, cancellationToken);

        if (!preview.CanRename)
        {
            return new SeriesRenameResult
            {
                SeriesId = seriesId,
                SeriesTitle = series.Title,
                Success = false,
                Error = string.Join("; ", preview.Errors),
                PreviousPath = preview.CurrentPath,
                NewPath = preview.NewPath
            };
        }

        var result = new SeriesRenameResult
        {
            SeriesId = seriesId,
            SeriesTitle = series.Title,
            PreviousPath = preview.CurrentPath,
            NewPath = preview.NewPath
        };

        try
        {
            // DRY RUN MODE: Simulate the operation without making changes
            if (dryRun)
            {
                foreach (var filePreview in preview.Files)
                {
                    var fileResult = new FileRenameResult
                    {
                        PreviousPath = filePreview.CurrentPath,
                        NewPath = filePreview.NewPath,
                        Success = true,
                        IsDryRun = true
                    };
                    result.FileResults.Add(fileResult);
                    result.FilesRenamed++;
                }
                
                result.Success = true;
                result.IsDryRun = true;
                
                _logger.LogInformation(
                    "[DRY RUN] Would organize series {SeriesTitle}: {FilesRenamed} files would be renamed",
                    series.Title, result.FilesRenamed);
                
                return result;
            }
            
            // ACTUAL EXECUTION: Make real changes
            // Create the new directory structure
            var newDir = preview.NewPath;
            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
                _logger.LogInformation("Created directory: {Directory}", newDir);
            }

            // Track source directories for cleanup
            var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Move/rename files
            foreach (var filePreview in preview.Files)
            {
                // Track source directory before moving
                var sourceDir = Path.GetDirectoryName(filePreview.CurrentPath);
                if (!string.IsNullOrEmpty(sourceDir))
                {
                    sourceDirectories.Add(sourceDir);
                }

                var fileResult = await MoveFileAsync(filePreview, cancellationToken);
                result.FileResults.Add(fileResult);
                
                if (fileResult.Success)
                {
                    result.FilesRenamed++;
                }
                else
                {
                    result.FilesFailed++;
                }
            }

            // Update series path in database
            var previousPath = series.Path;
            series.Path = preview.NewPath;
            series.UpdatedAt = DateTime.UtcNow;
            
            await _db.SaveChangesAsync(cancellationToken);
            
            // Clean up all source directories that are now empty
            foreach (var sourceDir in sourceDirectories)
            {
                if (!string.Equals(sourceDir, preview.NewPath, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(sourceDir))
                {
                    TryRemoveEmptyDirectory(sourceDir);
                }
            }
            
            // Also try to remove the old series.Path if different
            if (!string.IsNullOrEmpty(previousPath) && 
                !sourceDirectories.Contains(previousPath) &&
                !string.Equals(previousPath, preview.NewPath, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(previousPath))
            {
                TryRemoveEmptyDirectory(previousPath);
            }

            // Also scan library root for any empty directories (orphan cleanup)
            CleanupOrphanedEmptyDirectories();

            result.Success = result.FilesFailed == 0;
            if (result.FilesFailed > 0)
            {
                result.Error = $"{result.FilesFailed} file(s) failed to rename";
            }
            
            _logger.LogInformation(
                "Organized series {SeriesTitle}: {FilesRenamed} files renamed, {FilesFailed} failed",
                series.Title, result.FilesRenamed, result.FilesFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error organizing series {SeriesId} {SeriesTitle}", seriesId, series.Title);
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<int, PathMismatchInfo>> GetPathMismatchStatusAsync(
        int[] seriesIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, PathMismatchInfo>();
        
        var query = _db.Series.Where(s => s.ParentSeriesId == null);
        
        if (seriesIds.Length > 0)
        {
            query = query.Where(s => seriesIds.Contains(s.Id));
        }

        var seriesList = await query
            .Select(s => new { s.Id, s.Title, s.StartYear, s.Publisher, s.Status, s.Path })
            .ToListAsync(cancellationToken);
        
        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        var libraryRoot = _libraryRoots.FirstOrDefault() ?? "/data/library";
        
        foreach (var series in seriesList)
        {
            var mockSeries = new Series
            {
                Id = series.Id,
                Title = series.Title,
                StartYear = series.StartYear,
                Publisher = series.Publisher,
                Status = series.Status
            };
            
            var expectedFolder = ExpandSeriesFolderFormat(settings.SeriesFolderFormat, mockSeries);
            var expectedPath = Path.Combine(libraryRoot, expectedFolder);
            
            var hasMismatch = !string.IsNullOrEmpty(series.Path) && 
                !string.Equals(series.Path, expectedPath, StringComparison.OrdinalIgnoreCase);
            
            result[series.Id] = new PathMismatchInfo
            {
                HasMismatch = hasMismatch,
                CurrentPath = series.Path,
                ExpectedPath = expectedPath
            };
        }
        
        return result;
    }

    private async Task<SeriesRenamePreview> BuildSeriesPreviewAsync(
        Series series,
        GeneralSettings settings,
        CancellationToken cancellationToken)
    {
        var preview = new SeriesRenamePreview
        {
            SeriesId = series.Id,
            SeriesTitle = series.Title,
            CurrentPath = series.Path
        };

        // Calculate the new path based on format settings
        var libraryRoot = _libraryRoots.FirstOrDefault() ?? "/data/library";
        var folderName = ExpandSeriesFolderFormat(settings.SeriesFolderFormat, series);
        preview.NewPath = Path.Combine(libraryRoot, folderName);

        // Get all issue IDs and edition IDs for this series
        var issueIds = series.Issues.Select(i => i.Id).ToList();
        var editionIds = series.Editions.Select(e => e.Id).ToList();

        // Query all file assets for these issues and editions
        var fileAssets = await _db.FileAssets
            .Include(f => f.Issue)
            .Include(f => f.EditionTitle)
            .Where(f => 
                (f.IssueId.HasValue && issueIds.Contains(f.IssueId.Value)) ||
                (f.EditionTitleId.HasValue && editionIds.Contains(f.EditionTitleId.Value)))
            .ToListAsync(cancellationToken);

        preview.FileCount = fileAssets.Count;
        preview.TotalSize = fileAssets.Sum(f => f.Size);

        // Build file previews
        foreach (var file in fileAssets)
        {
            var filePreview = BuildFilePreview(file, series, settings, preview.NewPath);
            preview.Files.Add(filePreview);

            if (!string.IsNullOrEmpty(filePreview.Error))
            {
                preview.Errors.Add($"File {file.Id}: {filePreview.Error}");
            }
        }

        // Check for destination conflicts between files
        var duplicateDestinations = preview.Files
            .Where(f => !string.IsNullOrEmpty(f.NewPath))
            .GroupBy(f => f.NewPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicateDestinations)
        {
            preview.Errors.Add($"Multiple files would have the same destination: {dup.Key}");
        }

        // Check if current path exists but is different
        if (!string.IsNullOrEmpty(series.Path) && 
            Directory.Exists(series.Path) &&
            !string.Equals(series.Path, preview.NewPath, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(preview.NewPath))
            {
                preview.Warnings.Add($"Destination folder already exists: {preview.NewPath}");
            }
        }

        return preview;
    }

    private FileRenamePreview BuildFilePreview(
        FileAsset file,
        Series series,
        GeneralSettings settings,
        string newSeriesFolder)
    {
        var preview = new FileRenamePreview
        {
            FileId = file.Id,
            CurrentFileName = Path.GetFileName(file.Path),
            CurrentPath = file.Path,
            Size = file.Size,
            IsCollection = file.EditionTitleId.HasValue
        };

        try
        {
            string newFileName;
            var extension = Path.GetExtension(file.Path);

            if (file.IssueId.HasValue && file.Issue != null)
            {
                preview.IssueNumber = file.Issue.IssueNumber;
                newFileName = ExpandIssueFileFormat(settings.IssueFileFormat, series, file.Issue, extension);
            }
            else if (file.EditionTitleId.HasValue && file.EditionTitle != null)
            {
                preview.IsCollection = true;
                newFileName = ExpandCollectionFileFormat(settings.CollectionFileFormat, series, file.EditionTitle, extension);
            }
            else
            {
                newFileName = preview.CurrentFileName;
                preview.Error = "File not linked to issue or edition";
            }

            preview.NewFileName = newFileName;
            preview.NewPath = Path.Combine(newSeriesFolder, newFileName);

            if (!File.Exists(file.Path))
            {
                preview.Error = "Source file not found";
            }
        }
        catch (Exception ex)
        {
            preview.Error = ex.Message;
        }

        return preview;
    }

    private async Task<FileRenameResult> MoveFileAsync(
        FileRenamePreview preview,
        CancellationToken cancellationToken)
    {
        var result = new FileRenameResult
        {
            FileId = preview.FileId,
            PreviousPath = preview.CurrentPath,
            NewPath = preview.NewPath
        };

        if (!string.IsNullOrEmpty(preview.Error))
        {
            result.Success = false;
            result.Error = preview.Error;
            return result;
        }

        if (string.Equals(preview.CurrentPath, preview.NewPath, StringComparison.OrdinalIgnoreCase))
        {
            result.Success = true;
            return result;
        }

        try
        {
            var destDir = Path.GetDirectoryName(preview.NewPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (File.Exists(preview.CurrentPath))
            {
                if (File.Exists(preview.NewPath))
                {
                    result.Success = false;
                    result.Error = "Destination file already exists";
                    return result;
                }

                File.Move(preview.CurrentPath, preview.NewPath);
                
                var fileAsset = await _db.FileAssets.FindAsync(new object[] { preview.FileId }, cancellationToken);
                if (fileAsset != null)
                {
                    fileAsset.Path = preview.NewPath;
                    fileAsset.RelativePath = GetRelativePath(preview.NewPath);
                    fileAsset.LastModified = DateTime.UtcNow;
                }

                result.Success = true;
                _logger.LogDebug("Moved file {FileId}: {OldPath} -> {NewPath}", 
                    preview.FileId, preview.CurrentPath, preview.NewPath);
            }
            else
            {
                result.Success = false;
                result.Error = "Source file not found";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Error moving file {FileId}: {OldPath} -> {NewPath}",
                preview.FileId, preview.CurrentPath, preview.NewPath);
        }

        return result;
    }

    private void CleanupOrphanedEmptyDirectories()
    {
        foreach (var libraryRoot in _libraryRoots)
        {
            if (!Directory.Exists(libraryRoot))
                continue;

            _logger.LogInformation("Scanning for orphaned empty directories in: {Root}", libraryRoot);
            
            try
            {
                // Get all directories recursively, deepest first
                var allDirs = Directory.GetDirectories(libraryRoot, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length) // Process deepest directories first
                    .ToList();

                foreach (var dir in allDirs)
                {
                    try
                    {
                        if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                        {
                            Directory.Delete(dir);
                            _logger.LogInformation("Removed orphaned empty directory: {Directory}", dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not remove directory: {Directory}", dir);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning for orphaned directories in: {Root}", libraryRoot);
            }
        }
    }

    private void TryRemoveEmptyDirectory(string path)
    {
        try
        {
            var dir = path;
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                if (_libraryRoots.Any(r => string.Equals(dir, r, StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }

                if (Directory.GetFileSystemEntries(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    _logger.LogDebug("Removed empty directory: {Directory}", dir);
                    dir = Path.GetDirectoryName(dir);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove empty directory: {Directory}", path);
        }
    }

    private string GetRelativePath(string fullPath)
    {
        foreach (var root in _libraryRoots)
        {
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath[root.Length..].TrimStart(Path.DirectorySeparatorChar);
            }
        }
        return fullPath;
    }

    private static string ExpandSeriesFolderFormat(string format, Series series)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return SanitizePath(series.Title);
        }

        var result = format;
        
        result = Regex.Replace(result, @"\{Series Title\}", series.Title ?? "Unknown Series", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Series Year\}", series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Year\}", series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Publisher\}", series.Publisher ?? "Unknown Publisher", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Status\}", series.Status.ToString(), RegexOptions.IgnoreCase);
        
        result = Regex.Replace(result, @"\s*\(\s*\)", "");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        
        var parts = result.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sanitizedParts = parts.Select(SanitizePath).ToArray();
        
        return Path.Combine(sanitizedParts);
    }

    private static string ExpandIssueFileFormat(string format, Series series, Issue issue, string extension)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return GenerateIssueFilename(series.Title, issue.IssueNumber, extension);
        }

        var result = format;
        
        var issueStr = issue.IssueNumber % 1 == 0 
            ? ((int)issue.IssueNumber).ToString("D3") 
            : issue.IssueNumber.ToString("000.0");
        
        result = Regex.Replace(result, @"\{Series Title\}", series.Title ?? "Unknown Series", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Issue\}", issueStr, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Issue Title\}", issue.Title ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Year\}", issue.StoreDate?.Year.ToString() ?? series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Publisher\}", series.Publisher ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Quality\}", "", RegexOptions.IgnoreCase);
        
        result = Regex.Replace(result, @"\s*\(\s*\)", "");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        
        return SanitizePath(result) + extension;
    }

    private static string ExpandCollectionFileFormat(string format, Series series, EditionTitle edition, string extension)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return GenerateEditionFilename(edition.Title, edition.VolumeNumber, extension);
        }

        var result = format;
        
        result = Regex.Replace(result, @"\{Series Title\}", series.Title ?? "Unknown Series", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Edition Type\}", GetEditionTypeLabel(edition.EditionType), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Volume\}", edition.VolumeNumber?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Year\}", edition.ReleaseDate?.Year.ToString() ?? series.StartYear?.ToString() ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Publisher\}", series.Publisher ?? "", RegexOptions.IgnoreCase);
        
        result = Regex.Replace(result, @"\s*Vol\.\s*$", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s*\(\s*\)", "");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        
        return SanitizePath(result) + extension;
    }

    private static string GetEditionTypeLabel(EditionType editionType) => editionType switch
    {
        EditionType.TradesPaperback => "TPB",
        EditionType.Hardcover => "HC",
        EditionType.Omnibus => "Omnibus",
        EditionType.Compendium => "Compendium",
        EditionType.AbsoluteEdition => "Absolute",
        EditionType.DeluxeEdition => "Deluxe",
        _ => "Collection"
    };

    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GenerateIssueFilename(string series, decimal issueNumber, string extension)
    {
        var issueStr = issueNumber % 1 == 0 
            ? ((int)issueNumber).ToString("D3") 
            : issueNumber.ToString("000.0");
        return $"{SanitizePath(series)} #{issueStr}{extension}";
    }

    private static string GenerateEditionFilename(string title, int? volumeNumber, string extension)
    {
        var name = volumeNumber.HasValue 
            ? $"{SanitizePath(title)} Vol. {volumeNumber}" 
            : SanitizePath(title);
        return $"{name}{extension}";
    }
}
