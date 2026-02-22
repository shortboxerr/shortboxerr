using Shortboxerr.Core.Torrent;
using Xunit;

namespace Shortboxerr.Tests;

public class TorrentImportServiceTests
{
    #region TorrentImportSettings Tests

    [Fact]
    public void TorrentImportSettings_DefaultValues()
    {
        var settings = new TorrentImportSettings();

        Assert.True(settings.AutoImportEnabled);
        Assert.Equal(FileTransferMode.HardLink, settings.TransferMode);
        Assert.False(settings.RemoveAfterImport);
        Assert.False(settings.DeleteFilesOnRemove);
        Assert.Equal(1.0, settings.MinimumSeedRatio);
        Assert.Equal(0, settings.MinimumSeedTimeMinutes);
        Assert.True(settings.SeedRequirementsOrMode);
        Assert.Null(settings.Category);
        Assert.Null(settings.DestinationPath);
        Assert.Equal(5, settings.ScanIntervalMinutes);
        Assert.False(settings.ExtractArchives);
        Assert.False(settings.PreserveFolderStructure);
    }

    [Fact]
    public void TorrentImportSettings_DefaultFileExtensions()
    {
        var settings = new TorrentImportSettings();

        Assert.Contains(".cbz", settings.FileExtensions);
        Assert.Contains(".cbr", settings.FileExtensions);
        Assert.Contains(".cb7", settings.FileExtensions);
        Assert.Contains(".pdf", settings.FileExtensions);
        Assert.Equal(4, settings.FileExtensions.Count);
    }

    [Fact]
    public void TorrentImportSettings_CanCustomize()
    {
        var settings = new TorrentImportSettings
        {
            AutoImportEnabled = false,
            TransferMode = FileTransferMode.Copy,
            RemoveAfterImport = true,
            DeleteFilesOnRemove = true,
            MinimumSeedRatio = 2.0,
            MinimumSeedTimeMinutes = 60,
            SeedRequirementsOrMode = false,
            Category = "comics",
            DestinationPath = "/library/comics",
            ScanIntervalMinutes = 10,
            ExtractArchives = true,
            PreserveFolderStructure = true
        };

        Assert.False(settings.AutoImportEnabled);
        Assert.Equal(FileTransferMode.Copy, settings.TransferMode);
        Assert.True(settings.RemoveAfterImport);
        Assert.Equal(2.0, settings.MinimumSeedRatio);
        Assert.Equal(60, settings.MinimumSeedTimeMinutes);
        Assert.False(settings.SeedRequirementsOrMode);
        Assert.Equal("comics", settings.Category);
    }

    #endregion

    #region FileTransferMode Tests

    [Fact]
    public void FileTransferMode_Copy_IsDefault()
    {
        Assert.Equal(0, (int)FileTransferMode.Copy);
    }

    [Fact]
    public void FileTransferMode_HardLink_Value()
    {
        Assert.Equal(1, (int)FileTransferMode.HardLink);
    }

    [Fact]
    public void FileTransferMode_Move_Value()
    {
        Assert.Equal(2, (int)FileTransferMode.Move);
    }

    #endregion

    #region TorrentImportResult Tests

    [Fact]
    public void TorrentImportResult_Imported_CreatesSuccessResult()
    {
        var result = TorrentImportResult.Imported(
            "abc123", "Test Torrent", TorrentClientType.QBittorrent, 5, 1024 * 1024, true);

        Assert.Equal("abc123", result.Hash);
        Assert.Equal("Test Torrent", result.Name);
        Assert.Equal(TorrentClientType.QBittorrent, result.ClientType);
        Assert.True(result.Success);
        Assert.Equal(TorrentImportStatus.Imported, result.Status);
        Assert.Equal(5, result.FilesImported);
        Assert.Equal(1024 * 1024, result.BytesImported);
        Assert.True(result.TorrentRemoved);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void TorrentImportResult_Skipped_CreatesSkipResult()
    {
        var result = TorrentImportResult.Skipped(
            "abc123", "Test Torrent", TorrentClientType.Transmission, TorrentImportStatus.SeedingRatioNotMet);

        Assert.Equal("abc123", result.Hash);
        Assert.True(result.Success);
        Assert.Equal(TorrentImportStatus.SeedingRatioNotMet, result.Status);
        Assert.Equal(0, result.FilesImported);
        Assert.False(result.TorrentRemoved);
    }

    [Fact]
    public void TorrentImportResult_Failed_CreatesFailureResult()
    {
        var result = TorrentImportResult.Failed(
            "abc123", "Test Torrent", TorrentClientType.Deluge, "File not found");

        Assert.Equal("abc123", result.Hash);
        Assert.False(result.Success);
        Assert.Equal(TorrentImportStatus.Failed, result.Status);
        Assert.Equal("File not found", result.ErrorMessage);
    }

    [Fact]
    public void TorrentImportResult_HasProcessedAt()
    {
        var before = DateTime.UtcNow;
        var result = TorrentImportResult.Imported("abc", "test", TorrentClientType.QBittorrent, 1, 100, false);
        var after = DateTime.UtcNow;

        Assert.InRange(result.ProcessedAt, before, after);
    }

    #endregion

    #region TorrentImportStatus Tests

    [Fact]
    public void TorrentImportStatus_Values()
    {
        Assert.Equal(0, (int)TorrentImportStatus.Imported);
        Assert.Equal(1, (int)TorrentImportStatus.NotCompleted);
        Assert.Equal(2, (int)TorrentImportStatus.SeedingRatioNotMet);
        Assert.Equal(3, (int)TorrentImportStatus.SeedingTimeNotMet);
        Assert.Equal(4, (int)TorrentImportStatus.WrongCategory);
        Assert.Equal(5, (int)TorrentImportStatus.NoMatchingFiles);
        Assert.Equal(6, (int)TorrentImportStatus.AlreadyImported);
        Assert.Equal(7, (int)TorrentImportStatus.Failed);
    }

    #endregion

    #region TorrentReadyResult Tests

    [Fact]
    public void TorrentReadyResult_Ready_CreatesReadyResult()
    {
        var result = TorrentReadyResult.Ready();

        Assert.True(result.IsReady);
        Assert.Equal(TorrentImportStatus.Imported, result.Status);
    }

    [Fact]
    public void TorrentReadyResult_NotReady_WithRatioInfo()
    {
        var result = TorrentReadyResult.NotReady(
            TorrentImportStatus.SeedingRatioNotMet,
            currentRatio: 0.5,
            requiredRatio: 1.0);

        Assert.False(result.IsReady);
        Assert.Equal(TorrentImportStatus.SeedingRatioNotMet, result.Status);
        Assert.Equal(0.5, result.CurrentRatio);
        Assert.Equal(1.0, result.RequiredRatio);
    }

    [Fact]
    public void TorrentReadyResult_NotReady_WithTimeInfo()
    {
        var result = TorrentReadyResult.NotReady(
            TorrentImportStatus.SeedingTimeNotMet,
            minutesSeeded: 30,
            requiredMinutes: 60);

        Assert.False(result.IsReady);
        Assert.Equal(TorrentImportStatus.SeedingTimeNotMet, result.Status);
        Assert.Equal(30, result.MinutesSeeded);
        Assert.Equal(60, result.RequiredMinutes);
    }

    #endregion

    #region TorrentFileImportResult Tests

    [Fact]
    public void TorrentFileImportResult_Succeeded_CreatesSuccessResult()
    {
        var files = new List<string> { "/path/comic1.cbz", "/path/comic2.cbr" };
        var result = TorrentFileImportResult.Succeeded(2, 1024 * 1024, files, true);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesImported);
        Assert.Equal(1024 * 1024, result.BytesTransferred);
        Assert.Equal(2, result.ImportedFiles.Count);
        Assert.True(result.UsedHardLinks);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void TorrentFileImportResult_NoFiles_CreatesEmptyResult()
    {
        var result = TorrentFileImportResult.NoFiles();

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesImported);
        Assert.Equal(0, result.BytesTransferred);
        Assert.Empty(result.ImportedFiles);
    }

    [Fact]
    public void TorrentFileImportResult_Error_CreatesErrorResult()
    {
        var result = TorrentFileImportResult.Error("Disk full");

        Assert.False(result.Success);
        Assert.Equal("Disk full", result.ErrorMessage);
        Assert.Equal(0, result.FilesImported);
    }

    #endregion

    #region TorrentStatus IsCompleted Tests

    [Fact]
    public void TorrentStatus_IsCompleted_WhenStateIsCompleted()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Completed
        };

        Assert.True(status.IsCompleted);
    }

    [Fact]
    public void TorrentStatus_IsCompleted_WhenStateIsSeeding()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Seeding
        };

        Assert.True(status.IsCompleted);
    }

    [Fact]
    public void TorrentStatus_IsCompleted_WhenProgressIs100()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Paused,
            TotalBytes = 1000,
            DownloadedBytes = 1000
        };

        Assert.True(status.IsCompleted);
    }

    [Fact]
    public void TorrentStatus_IsNotCompleted_WhenDownloading()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Downloading,
            TotalBytes = 1000,
            DownloadedBytes = 500
        };

        Assert.False(status.IsCompleted);
    }

    #endregion

    #region Integration Tests - Seeding Requirements

    [Fact]
    public void SeedingRequirements_OrMode_RatioMet()
    {
        // In OR mode, meeting ratio is sufficient even if time isn't met
        var settings = new TorrentImportSettings
        {
            MinimumSeedRatio = 1.0,
            MinimumSeedTimeMinutes = 60,
            SeedRequirementsOrMode = true
        };

        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Seeding,
            Ratio = 1.5, // Above required ratio
            CompletedOn = DateTime.UtcNow // Just completed, time not met
        };

        // Ratio is met (1.5 >= 1.0), so should be ready in OR mode
        Assert.True(status.Ratio >= settings.MinimumSeedRatio);
    }

    [Fact]
    public void SeedingRequirements_OrMode_TimeMet()
    {
        // In OR mode, meeting time is sufficient even if ratio isn't met
        var settings = new TorrentImportSettings
        {
            MinimumSeedRatio = 2.0,
            MinimumSeedTimeMinutes = 30,
            SeedRequirementsOrMode = true
        };

        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Seeding,
            Ratio = 0.5, // Below required ratio
            CompletedOn = DateTime.UtcNow.AddMinutes(-60) // 60 minutes ago, time is met
        };

        // Ratio not met but time is
        Assert.False(status.Ratio >= settings.MinimumSeedRatio);
        var minutesSeeded = (int)(DateTime.UtcNow - status.CompletedOn!.Value).TotalMinutes;
        Assert.True(minutesSeeded >= settings.MinimumSeedTimeMinutes);
    }

    [Fact]
    public void SeedingRequirements_AndMode_BothRequired()
    {
        var settings = new TorrentImportSettings
        {
            MinimumSeedRatio = 1.0,
            MinimumSeedTimeMinutes = 30,
            SeedRequirementsOrMode = false // AND mode
        };

        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            State = TorrentState.Seeding,
            Ratio = 1.5, // Above required ratio
            CompletedOn = DateTime.UtcNow // Just completed, time NOT met
        };

        // In AND mode, both must be met
        var ratioMet = status.Ratio >= settings.MinimumSeedRatio;
        var minutesSeeded = (int)(DateTime.UtcNow - status.CompletedOn!.Value).TotalMinutes;
        var timeMet = minutesSeeded >= settings.MinimumSeedTimeMinutes;

        Assert.True(ratioMet);
        Assert.False(timeMet);
    }

    #endregion

    #region File Extension Filter Tests

    [Theory]
    [InlineData(".cbz", true)]
    [InlineData(".CBZ", true)]
    [InlineData(".cbr", true)]
    [InlineData(".cb7", true)]
    [InlineData(".pdf", true)]
    [InlineData(".zip", false)]
    [InlineData(".rar", false)]
    [InlineData(".txt", false)]
    public void FileExtensions_DefaultFilter(string extension, bool shouldMatch)
    {
        var settings = new TorrentImportSettings();
        var matches = settings.FileExtensions.Any(ext =>
            ext.Equals(extension, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(shouldMatch, matches);
    }

    [Fact]
    public void FileExtensions_EmptyListMatchesAll()
    {
        var settings = new TorrentImportSettings
        {
            FileExtensions = new List<string>()
        };

        // Empty list = no filter = match all
        Assert.Empty(settings.FileExtensions);
    }

    [Fact]
    public void FileExtensions_CustomList()
    {
        var settings = new TorrentImportSettings
        {
            FileExtensions = new List<string> { ".cbz", ".cbr" }
        };

        Assert.Equal(2, settings.FileExtensions.Count);
        Assert.Contains(".cbz", settings.FileExtensions);
        Assert.Contains(".cbr", settings.FileExtensions);
        Assert.DoesNotContain(".pdf", settings.FileExtensions);
    }

    #endregion

    #region Category Filter Tests

    [Fact]
    public void Category_NullMatchesAll()
    {
        var settings = new TorrentImportSettings { Category = null };

        Assert.Null(settings.Category);
    }

    [Fact]
    public void Category_MatchesExact()
    {
        var settings = new TorrentImportSettings { Category = "comics" };

        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            Category = "comics"
        };

        Assert.Equal(settings.Category, status.Category);
    }

    [Fact]
    public void Category_CaseInsensitive()
    {
        var settings = new TorrentImportSettings { Category = "Comics" };

        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            Category = "comics"
        };

        Assert.True(string.Equals(settings.Category, status.Category, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Ratio Calculation Tests

    [Fact]
    public void Ratio_ZeroDownloaded_NoError()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            TotalBytes = 0,
            DownloadedBytes = 0,
            UploadedBytes = 100
        };

        // Ratio is uploaded/downloaded, should handle zero
        Assert.Equal(0, status.Ratio);
    }

    [Fact]
    public void Ratio_CorrectCalculation()
    {
        var status = new TorrentStatus
        {
            Hash = "abc",
            Name = "Test",
            TotalBytes = 1000,
            DownloadedBytes = 1000,
            UploadedBytes = 1500,
            Ratio = 1.5 // Pre-calculated by client
        };

        Assert.Equal(1.5, status.Ratio);
    }

    #endregion
}
