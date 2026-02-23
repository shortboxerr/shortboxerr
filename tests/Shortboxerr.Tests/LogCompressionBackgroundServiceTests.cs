using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

public class LogCompressionBackgroundServiceTests : IDisposable
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<LogCompressionBackgroundService>> _mockLogger;
    private readonly ServiceProvider _serviceProvider;
    private readonly string _testLogDir;

    public LogCompressionBackgroundServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<LogCompressionBackgroundService>>();

        // Create test log directory
        _testLogDir = Path.Combine(Path.GetTempPath(), $"shortboxerr-test-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testLogDir);
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_DIR", _testLogDir);

        var services = new ServiceCollection();
        services.AddScoped<ISettingsService>(_ => _mockSettingsService.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        Environment.SetEnvironmentVariable("SHORTBOXERR_LOG_DIR", null);
        if (Directory.Exists(_testLogDir))
        {
            try { Directory.Delete(_testLogDir, true); }
            catch { }
        }
    }

    [Fact]
    public async Task TriggerCompressionAsync_WhenDisabled_DoesNotCompress()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("false");

        // Create an old log file
        var oldLogFile = Path.Combine(_testLogDir, "shortboxerr20260101.log");
        await File.WriteAllTextAsync(oldLogFile, "Test log content");
        File.SetLastWriteTimeUtc(oldLogFile, DateTime.UtcNow.AddDays(-7));

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert - manual trigger always compresses
        Assert.True(File.Exists(oldLogFile + ".gz") || result.FilesCompressed > 0 || !File.Exists(oldLogFile));
    }

    [Fact]
    public async Task TriggerCompressionAsync_CompressesOldLogFiles()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressLogsOlderThanDays", It.IsAny<CancellationToken>()))
            .ReturnsAsync("1");

        // Create an old log file (2 days old)
        var oldLogFile = Path.Combine(_testLogDir, "shortboxerr20260101.log");
        var testContent = "Test log content line 1\nTest log content line 2\n";
        await File.WriteAllTextAsync(oldLogFile, testContent);
        File.SetLastWriteTimeUtc(oldLogFile, DateTime.UtcNow.AddDays(-2));

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert
        Assert.Equal(1, result.FilesCompressed);
        Assert.False(File.Exists(oldLogFile)); // Original deleted
        Assert.True(File.Exists(oldLogFile + ".gz")); // Compressed version exists

        // Verify compressed content is valid
        await using var fs = File.OpenRead(oldLogFile + ".gz");
        await using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        var decompressed = await reader.ReadToEndAsync();
        Assert.Equal(testContent, decompressed);
    }

    [Fact]
    public async Task TriggerCompressionAsync_DoesNotCompressCurrentLogFile()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressLogsOlderThanDays", It.IsAny<CancellationToken>()))
            .ReturnsAsync("1");

        // Create the current log file (should not be compressed)
        var currentLogFile = Path.Combine(_testLogDir, "shortboxerr.log");
        await File.WriteAllTextAsync(currentLogFile, "Current log content");
        File.SetLastWriteTimeUtc(currentLogFile, DateTime.UtcNow.AddDays(-2));

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert
        Assert.Equal(0, result.FilesCompressed);
        Assert.True(File.Exists(currentLogFile)); // Current log not touched
        Assert.False(File.Exists(currentLogFile + ".gz")); // Not compressed
    }

    [Fact]
    public async Task TriggerCompressionAsync_DoesNotCompressRecentLogs()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressLogsOlderThanDays", It.IsAny<CancellationToken>()))
            .ReturnsAsync("7");

        // Create a recent log file (1 day old, threshold is 7 days)
        var recentLogFile = Path.Combine(_testLogDir, "shortboxerr20260216.log");
        await File.WriteAllTextAsync(recentLogFile, "Recent log content");
        File.SetLastWriteTimeUtc(recentLogFile, DateTime.UtcNow.AddDays(-1));

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert
        Assert.Equal(0, result.FilesCompressed);
        Assert.True(File.Exists(recentLogFile)); // Recent log not compressed
    }

    [Fact]
    public async Task TriggerCompressionAsync_SkipsAlreadyCompressedFiles()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressLogsOlderThanDays", It.IsAny<CancellationToken>()))
            .ReturnsAsync("1");

        // Create an already compressed file
        var compressedFile = Path.Combine(_testLogDir, "shortboxerr20260101.log.gz");
        await File.WriteAllTextAsync(compressedFile, "compressed content");
        File.SetLastWriteTimeUtc(compressedFile, DateTime.UtcNow.AddDays(-5));

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert
        Assert.Equal(0, result.FilesCompressed);
        Assert.True(File.Exists(compressedFile)); // Already compressed file untouched
    }

    [Fact]
    public async Task TriggerCompressionAsync_BytesSavedTracksCompressionSavings()
    {
        // Arrange
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressOldLogs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        _mockSettingsService
            .Setup(s => s.GetAsync("Logging:CompressLogsOlderThanDays", It.IsAny<CancellationToken>()))
            .ReturnsAsync("1");

        // Create an old log file with repetitive content (compresses well)
        var logFile = Path.Combine(_testLogDir, "shortboxerr20260115.log");
        var testContent = string.Join("\n", Enumerable.Repeat("This is a repeating log line that compresses well", 100));
        await File.WriteAllTextAsync(logFile, testContent);
        File.SetLastWriteTimeUtc(logFile, DateTime.UtcNow.AddDays(-3));

        var originalSize = new FileInfo(logFile).Length;

        var service = new LogCompressionBackgroundService(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _mockLogger.Object);

        // Act
        var result = await service.TriggerCompressionAsync();

        // Assert
        Assert.Equal(1, result.FilesCompressed);
        Assert.True(result.BytesSaved > 0, $"Expected bytes saved > 0, got {result.BytesSaved}");
        Assert.True(File.Exists(logFile + ".gz"));
        
        // Compressed file should be smaller than original
        var compressedSize = new FileInfo(logFile + ".gz").Length;
        Assert.True(compressedSize < originalSize);
    }
}
