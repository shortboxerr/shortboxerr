using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Services;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for ArchiveExtractor.
/// Tests archive type detection, extraction, and file listing.
/// </summary>
public class ArchiveExtractorTests : IDisposable
{
    private readonly ArchiveExtractor _extractor;
    private readonly string _tempDir;

    public ArchiveExtractorTests()
    {
        _extractor = new ArchiveExtractor(NullLogger<ArchiveExtractor>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"archive_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Archive Type Detection by Extension

    [Theory]
    [InlineData(".zip", ArchiveType.Zip)]
    [InlineData(".cbz", ArchiveType.Zip)]
    [InlineData(".rar", ArchiveType.Rar)]
    [InlineData(".cbr", ArchiveType.Rar)]
    [InlineData(".7z", ArchiveType.SevenZip)]
    [InlineData(".tar", ArchiveType.Tar)]
    [InlineData(".gz", ArchiveType.GZip)]
    [InlineData(".tgz", ArchiveType.GZip)]
    public void GetArchiveType_ByExtension_ReturnsCorrectType(string extension, ArchiveType expectedType)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, $"test{extension}");

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".exe")]
    [InlineData("")]
    public void GetArchiveType_UnsupportedExtension_ReturnsUnknown(string extension)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, $"test{extension}");

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(ArchiveType.Unknown, result);
    }

    #endregion

    #region IsSupportedArchive Tests

    [Theory]
    [InlineData("test.zip", true)]
    [InlineData("test.cbz", true)]
    [InlineData("test.rar", true)]
    [InlineData("test.cbr", true)]
    [InlineData("test.7z", true)]
    [InlineData("test.tar", true)]
    [InlineData("test.gz", true)]
    [InlineData("test.txt", false)]
    [InlineData("test.pdf", false)]
    [InlineData("test", false)]
    public void IsSupportedArchive_ReturnsCorrectResult(string filename, bool expected)
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, filename);

        // Act
        var result = _extractor.IsSupportedArchive(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region ZIP Archive Extraction Tests

    [Fact]
    public async Task ExtractAsync_ValidZipArchive_ExtractsAllFiles()
    {
        // Arrange
        var zipPath = CreateTestZipArchive("test.zip", new[]
        {
            ("file1.txt", "Hello World"),
            ("file2.cbz", "Comic Book Content"),
            ("subdir/file3.txt", "Nested File")
        });
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(zipPath, destDir);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ArchiveType.Zip, result.ArchiveType);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(zipPath, result.ArchivePath);
        Assert.Equal(destDir, result.DestinationDirectory);
        Assert.True(result.Duration.TotalMilliseconds > 0);
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "file2.cbz")));
        Assert.True(File.Exists(Path.Combine(destDir, "subdir", "file3.txt")));
    }

    [Fact]
    public async Task ExtractAsync_EmptyZipArchive_ReturnsSuccessWithNoFiles()
    {
        // Arrange
        var zipPath = CreateTestZipArchive("empty.zip", Array.Empty<(string, string)>());
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(zipPath, destDir);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.FileCount);
    }

    [Fact]
    public async Task ExtractToSiblingDirectoryAsync_ExtractsToCorrectLocation()
    {
        // Arrange
        var zipPath = CreateTestZipArchive("comic.zip", new[]
        {
            ("page1.jpg", "Image Data 1"),
            ("page2.jpg", "Image Data 2")
        });

        // Act
        var result = await _extractor.ExtractToSiblingDirectoryAsync(zipPath);

        // Assert
        Assert.True(result.Success);
        Assert.EndsWith("comic_extracted", result.DestinationDirectory);
        Assert.Equal(2, result.FileCount);
    }

    #endregion

    #region CBZ Archive Extraction Tests

    [Fact]
    public async Task ExtractAsync_CbzArchive_ExtractsAsZip()
    {
        // Arrange - CBZ is just a renamed ZIP
        var zipPath = CreateTestZipArchive("comic.cbz", new[]
        {
            ("page01.jpg", "Image 1"),
            ("page02.jpg", "Image 2"),
            ("ComicInfo.xml", "<ComicInfo><Title>Test</Title></ComicInfo>")
        });
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(zipPath, destDir);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ArchiveType.Zip, result.ArchiveType);
        Assert.Equal(3, result.FileCount);
    }

    #endregion

    #region List Files Tests

    [Fact]
    public async Task ListFilesAsync_ValidZipArchive_ReturnsFileList()
    {
        // Arrange
        var zipPath = CreateTestZipArchive("test.zip", new[]
        {
            ("file1.txt", "Content 1"),
            ("dir/file2.txt", "Content 2")
        });

        // Act
        var files = await _extractor.ListFilesAsync(zipPath);

        // Assert
        Assert.Equal(2, files.Count);
        Assert.Contains("file1.txt", files);
        Assert.Contains("dir/file2.txt", files);
    }

    [Fact]
    public async Task ListFilesAsync_NonExistentArchive_ReturnsEmptyList()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "nonexistent.zip");

        // Act
        var files = await _extractor.ListFilesAsync(path);

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public async Task ListFilesAsync_UnsupportedFormat_ReturnsEmptyList()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(path, "Not an archive");

        // Act
        var files = await _extractor.ListFilesAsync(path);

        // Assert
        Assert.Empty(files);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExtractAsync_NonExistentArchive_ReturnsFailure()
    {
        // Arrange
        var archivePath = Path.Combine(_tempDir, "nonexistent.zip");
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(archivePath, destDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_UnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "Not an archive");
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(filePath, destDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unsupported", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_CorruptedArchive_ReturnsFailure()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "corrupted.zip");
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x50, 0x4B, 0x00, 0x00 }); // Invalid ZIP
        var destDir = Path.Combine(_tempDir, "extracted");

        // Act
        var result = await _extractor.ExtractAsync(filePath, destDir);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var zipPath = CreateTestZipArchive("test.zip", new[]
        {
            ("file1.txt", "Content")
        });
        var destDir = Path.Combine(_tempDir, "extracted");
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _extractor.ExtractAsync(zipPath, destDir, cts.Token));
    }

    #endregion

    #region Magic Bytes Detection Tests

    [Fact]
    public void GetArchiveType_ZipMagicBytes_DetectsAsZip()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.unknown");
        File.WriteAllBytes(filePath, new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // ZIP magic bytes

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(ArchiveType.Zip, result);
    }

    [Fact]
    public void GetArchiveType_RarMagicBytes_DetectsAsRar()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.unknown");
        File.WriteAllBytes(filePath, new byte[] { 0x52, 0x61, 0x72, 0x21 }); // RAR magic bytes

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(ArchiveType.Rar, result);
    }

    [Fact]
    public void GetArchiveType_SevenZipMagicBytes_DetectsAs7z()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.unknown");
        File.WriteAllBytes(filePath, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }); // 7z magic bytes

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(ArchiveType.SevenZip, result);
    }

    [Fact]
    public void GetArchiveType_GZipMagicBytes_DetectsAsGZip()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.unknown");
        File.WriteAllBytes(filePath, new byte[] { 0x1F, 0x8B, 0x08 }); // GZip magic bytes

        // Act
        var result = _extractor.GetArchiveType(filePath);

        // Assert
        Assert.Equal(ArchiveType.GZip, result);
    }

    #endregion

    #region Helper Methods

    private string CreateTestZipArchive(string fileName, IEnumerable<(string path, string content)> files)
    {
        var zipPath = Path.Combine(_tempDir, fileName);

        using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        return zipPath;
    }

    #endregion
}
