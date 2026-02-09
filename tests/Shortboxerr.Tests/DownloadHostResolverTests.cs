using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Ddl.Resolvers;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for Download Host Resolvers.
/// </summary>
public class DownloadHostResolverTests
{
    #region DirectDownloadResolver Tests

    [Fact]
    public void DirectDownloadResolver_CanResolve_CbzUrl()
    {
        var resolver = new DirectDownloadResolver();
        Assert.True(resolver.CanResolve("https://example.com/file.cbz"));
    }

    [Fact]
    public void DirectDownloadResolver_CanResolve_CbrUrl()
    {
        var resolver = new DirectDownloadResolver();
        Assert.True(resolver.CanResolve("https://example.com/comic.cbr"));
    }

    [Fact]
    public void DirectDownloadResolver_CanResolve_ZipUrl()
    {
        var resolver = new DirectDownloadResolver();
        Assert.True(resolver.CanResolve("https://example.com/archive.zip"));
    }

    [Fact]
    public void DirectDownloadResolver_CanResolve_UrlWithQueryString()
    {
        var resolver = new DirectDownloadResolver();
        Assert.True(resolver.CanResolve("https://example.com/file.cbz?token=abc123"));
    }

    [Fact]
    public void DirectDownloadResolver_CanResolve_DownloadPath()
    {
        var resolver = new DirectDownloadResolver();
        Assert.True(resolver.CanResolve("https://example.com/download/file123"));
    }

    [Fact]
    public void DirectDownloadResolver_CannotResolve_HtmlPage()
    {
        var resolver = new DirectDownloadResolver();
        Assert.False(resolver.CanResolve("https://example.com/page.html"));
    }

    [Fact]
    public void DirectDownloadResolver_CannotResolve_MediaFireUrl()
    {
        var resolver = new DirectDownloadResolver();
        // MediaFire share pages don't have direct file extensions
        Assert.False(resolver.CanResolve("https://www.mediafire.com/file/abc123/SomeFile"));
    }

    [Fact]
    public void DirectDownloadResolver_HasHighestPriority()
    {
        var resolver = new DirectDownloadResolver();
        Assert.Equal(0, resolver.Priority);
    }

    #endregion

    #region PixeldrainResolver Tests

    [Fact]
    public void PixeldrainResolver_CanResolve_StandardUrl()
    {
        var resolver = new PixeldrainResolver();
        Assert.True(resolver.CanResolve("https://pixeldrain.com/u/abc12345"));
    }

    [Fact]
    public void PixeldrainResolver_CanResolve_ApiUrl()
    {
        var resolver = new PixeldrainResolver();
        Assert.True(resolver.CanResolve("https://pixeldrain.com/api/file/abc12345"));
    }

    [Fact]
    public void PixeldrainResolver_CanResolve_UrlWithDownloadQuery()
    {
        var resolver = new PixeldrainResolver();
        Assert.True(resolver.CanResolve("https://pixeldrain.com/u/abc12345?download"));
    }

    [Fact]
    public void PixeldrainResolver_CannotResolve_OtherHost()
    {
        var resolver = new PixeldrainResolver();
        Assert.False(resolver.CanResolve("https://mediafire.com/file/abc123"));
    }

    [Fact]
    public void PixeldrainResolver_ExtractFileId_StandardFormat()
    {
        var fileId = PixeldrainResolver.ExtractFileId("https://pixeldrain.com/u/abc12345");
        Assert.Equal("abc12345", fileId);
    }

    [Fact]
    public void PixeldrainResolver_ExtractFileId_ApiFormat()
    {
        var fileId = PixeldrainResolver.ExtractFileId("https://pixeldrain.com/api/file/xyz98765");
        Assert.Equal("xyz98765", fileId);
    }

    [Fact]
    public void PixeldrainResolver_ExtractFileId_WithQueryString()
    {
        var fileId = PixeldrainResolver.ExtractFileId("https://pixeldrain.com/u/file123?download=true");
        Assert.Equal("file123", fileId);
    }

    [Fact]
    public void PixeldrainResolver_ExtractFileId_InvalidUrl_ReturnsNull()
    {
        var fileId = PixeldrainResolver.ExtractFileId("https://other.com/u/abc");
        Assert.Null(fileId);
    }

    #endregion

    #region MediaFireResolver Tests

    [Fact]
    public void MediaFireResolver_CanResolve_StandardUrl()
    {
        var resolver = new MediaFireResolver();
        Assert.True(resolver.CanResolve("https://www.mediafire.com/file/abc123/SomeFile/file"));
    }

    [Fact]
    public void MediaFireResolver_CanResolve_WithoutWww()
    {
        var resolver = new MediaFireResolver();
        Assert.True(resolver.CanResolve("https://mediafire.com/file/abc123/SomeFile"));
    }

    [Fact]
    public void MediaFireResolver_CannotResolve_OtherHost()
    {
        var resolver = new MediaFireResolver();
        Assert.False(resolver.CanResolve("https://pixeldrain.com/u/abc123"));
    }

    [Fact]
    public void MediaFireResolver_ExtractDownloadUrl_AriaLabel()
    {
        var html = """
            <html>
            <body>
                <a aria-label="Download file" href="https://download123.mediafire.com/abc/file.cbz">Download</a>
            </body>
            </html>
            """;

        var url = MediaFireResolver.ExtractDownloadUrl(html);
        Assert.Equal("https://download123.mediafire.com/abc/file.cbz", url);
    }

    [Fact]
    public void MediaFireResolver_ExtractDownloadUrl_DownloadButtonId()
    {
        var html = """
            <html>
            <body>
                <a id="downloadButton" href="https://download456.mediafire.com/xyz/comic.cbz">Download</a>
            </body>
            </html>
            """;

        var url = MediaFireResolver.ExtractDownloadUrl(html);
        Assert.Equal("https://download456.mediafire.com/xyz/comic.cbz", url);
    }

    [Fact]
    public void MediaFireResolver_ExtractFilename_FilenameClass()
    {
        var html = """
            <html>
            <body>
                <div class="filename">Amazing Spider-Man 001 (2024).cbz</div>
            </body>
            </html>
            """;

        var filename = MediaFireResolver.ExtractFilename(html);
        Assert.Equal("Amazing Spider-Man 001 (2024).cbz", filename);
    }

    [Fact]
    public void MediaFireResolver_ExtractFilename_OgTitle()
    {
        var html = """
            <html>
            <head>
                <meta property="og:title" content="Batman 150 (2024).cbr" />
            </head>
            </html>
            """;

        var filename = MediaFireResolver.ExtractFilename(html);
        Assert.Equal("Batman 150 (2024).cbr", filename);
    }

    [Fact]
    public void MediaFireResolver_ExtractFileSize_MB()
    {
        var html = """
            <html>
            <body>
                <span class="details">Size: 45.5 MB</span>
            </body>
            </html>
            """;

        var size = MediaFireResolver.ExtractFileSize(html);
        Assert.NotNull(size);
        // 45.5 MB = 45.5 * 1024 * 1024 = 47,710,208 bytes
        Assert.True(size > 47_000_000 && size < 48_000_000);
    }

    [Fact]
    public void MediaFireResolver_ExtractFileSize_GB()
    {
        var html = """
            <html>
            <body>
                <span>File size: 1.2 GB</span>
            </body>
            </html>
            """;

        var size = MediaFireResolver.ExtractFileSize(html);
        Assert.NotNull(size);
        Assert.True(size > 1_200_000_000); // > 1.2 GB
    }

    #endregion

    #region DownloadHostResolverFactory Tests

    [Fact]
    public void Factory_RegistersBuiltInResolvers()
    {
        var factory = new DownloadHostResolverFactory();
        var resolvers = factory.GetAllResolvers();

        Assert.True(resolvers.Count >= 3); // Direct, MediaFire, Pixeldrain
    }

    [Fact]
    public void Factory_GetResolver_ReturnsDirectForCbzUrl()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://example.com/file.cbz");

        Assert.NotNull(resolver);
        Assert.Equal("Direct", resolver.HostId);
    }

    [Fact]
    public void Factory_GetResolver_ReturnsPixeldrainForPixeldrainUrl()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://pixeldrain.com/u/abc123");

        Assert.NotNull(resolver);
        Assert.Equal("Pixeldrain", resolver.HostId);
    }

    [Fact]
    public void Factory_GetResolver_ReturnsMediaFireForMediaFireUrl()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://www.mediafire.com/file/abc123");

        Assert.NotNull(resolver);
        Assert.Equal("MediaFire", resolver.HostId);
    }

    [Fact]
    public void Factory_GetResolver_ReturnsNullForUnknownHost()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://unknown-host.com/file");

        Assert.Null(resolver);
    }

    [Fact]
    public void Factory_CanResolve_ReturnsTrueForSupportedUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://pixeldrain.com/u/abc123"));
    }

    [Fact]
    public void Factory_CanResolve_ReturnsFalseForUnsupportedUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.False(factory.CanResolve("https://mega.nz/file/abc123")); // Mega not implemented yet
    }

    [Fact]
    public void Factory_GetResolvers_ReturnsSortedByPriority()
    {
        var factory = new DownloadHostResolverFactory();
        var resolvers = factory.GetAllResolvers();

        // Should be sorted by priority (ascending)
        for (int i = 1; i < resolvers.Count; i++)
        {
            Assert.True(resolvers[i - 1].Priority <= resolvers[i].Priority);
        }
    }

    [Fact]
    public void Factory_GetHostInfos_ReturnsAllHostInfo()
    {
        var factory = new DownloadHostResolverFactory();
        var infos = factory.GetHostInfos();

        Assert.Contains(infos, h => h.HostId == "Direct");
        Assert.Contains(infos, h => h.HostId == "MediaFire");
        Assert.Contains(infos, h => h.HostId == "Pixeldrain");
    }

    [Fact]
    public void Factory_RegisterResolver_AddsCustomResolver()
    {
        var factory = new DownloadHostResolverFactory();
        var customResolver = new TestResolver();
        factory.RegisterResolver(customResolver);

        var resolver = factory.GetResolver("https://testhost.com/file");
        Assert.NotNull(resolver);
        Assert.Equal("TestHost", resolver.HostId);
    }

    #endregion

    /// <summary>
    /// Test resolver for unit testing factory registration.
    /// </summary>
    private class TestResolver : IDownloadHostResolver
    {
        public string HostId => "TestHost";
        public string DisplayName => "Test Host";
        public IReadOnlyList<string> SupportedHosts => new[] { "testhost.com" };
        public int Priority => 100;
        public bool IsAvailable => true;

        public bool CanResolve(string url) => url.Contains("testhost.com");

        public Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HostResolverResult.Succeeded(url, "test.cbz"));
        }

        public Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostVerifyResult { IsAvailable = true });
        }
    }

    #region DropboxResolver Tests

    [Fact]
    public void DropboxResolver_CanResolve_StandardShareUrl()
    {
        var resolver = new DropboxResolver();
        Assert.True(resolver.CanResolve("https://www.dropbox.com/s/abc123/file.cbz?dl=0"));
    }

    [Fact]
    public void DropboxResolver_CanResolve_SclShareUrl()
    {
        var resolver = new DropboxResolver();
        Assert.True(resolver.CanResolve("https://www.dropbox.com/scl/fi/abc123/file.cbz?rlkey=xyz&dl=0"));
    }

    [Fact]
    public void DropboxResolver_CanResolve_DirectDownloadUrl()
    {
        var resolver = new DropboxResolver();
        Assert.True(resolver.CanResolve("https://dl.dropboxusercontent.com/s/abc123/file.cbz"));
    }

    [Fact]
    public void DropboxResolver_CannotResolve_OtherHost()
    {
        var resolver = new DropboxResolver();
        Assert.False(resolver.CanResolve("https://mediafire.com/file/abc123"));
    }

    [Fact]
    public void DropboxResolver_ConvertToDirectDownload_SetsDl1()
    {
        var directUrl = DropboxResolver.ConvertToDirectDownload("https://www.dropbox.com/s/abc123/file.cbz?dl=0");
        Assert.NotNull(directUrl);
        Assert.Contains("dl=1", directUrl);
    }

    [Fact]
    public void DropboxResolver_ConvertToDirectDownload_AlreadyDirect()
    {
        var url = "https://dl.dropboxusercontent.com/s/abc123/file.cbz";
        var directUrl = DropboxResolver.ConvertToDirectDownload(url);
        Assert.Equal(url, directUrl);
    }

    [Fact]
    public void DropboxResolver_ExtractFilename_StandardPath()
    {
        var filename = DropboxResolver.ExtractFilename("https://www.dropbox.com/s/abc123/Batman%20001.cbz?dl=0");
        Assert.Equal("Batman 001.cbz", filename);
    }

    [Fact]
    public void DropboxResolver_ExtractFilename_SclPath()
    {
        var filename = DropboxResolver.ExtractFilename("https://www.dropbox.com/scl/fi/abc123/Superman%20150.cbr?rlkey=xyz&dl=0");
        Assert.Equal("Superman 150.cbr", filename);
    }

    #endregion

    #region GoogleDriveResolver Tests

    [Fact]
    public void GoogleDriveResolver_CanResolve_FileViewUrl()
    {
        var resolver = new GoogleDriveResolver();
        Assert.True(resolver.CanResolve("https://drive.google.com/file/d/1abc123xyz/view"));
    }

    [Fact]
    public void GoogleDriveResolver_CanResolve_OpenUrl()
    {
        var resolver = new GoogleDriveResolver();
        Assert.True(resolver.CanResolve("https://drive.google.com/open?id=1abc123xyz"));
    }

    [Fact]
    public void GoogleDriveResolver_CanResolve_UcUrl()
    {
        var resolver = new GoogleDriveResolver();
        Assert.True(resolver.CanResolve("https://drive.google.com/uc?id=1abc123xyz&export=download"));
    }

    [Fact]
    public void GoogleDriveResolver_CannotResolve_OtherHost()
    {
        var resolver = new GoogleDriveResolver();
        Assert.False(resolver.CanResolve("https://dropbox.com/s/abc123/file.cbz"));
    }

    [Fact]
    public void GoogleDriveResolver_ExtractFileId_FromFilePath()
    {
        var fileId = GoogleDriveResolver.ExtractFileId("https://drive.google.com/file/d/1abc123XYZ-_/view?usp=sharing");
        Assert.Equal("1abc123XYZ-_", fileId);
    }

    [Fact]
    public void GoogleDriveResolver_ExtractFileId_FromOpenUrl()
    {
        var fileId = GoogleDriveResolver.ExtractFileId("https://drive.google.com/open?id=1def456ABC");
        Assert.Equal("1def456ABC", fileId);
    }

    [Fact]
    public void GoogleDriveResolver_ExtractFileId_FromUcUrl()
    {
        var fileId = GoogleDriveResolver.ExtractFileId("https://drive.google.com/uc?id=1ghi789DEF&export=download");
        Assert.Equal("1ghi789DEF", fileId);
    }

    [Fact]
    public void GoogleDriveResolver_ExtractFileId_InvalidUrl_ReturnsNull()
    {
        var fileId = GoogleDriveResolver.ExtractFileId("https://drive.google.com/drive/folders");
        Assert.Null(fileId);
    }

    [Fact]
    public void GoogleDriveResolver_IsFolderLink_True()
    {
        Assert.True(GoogleDriveResolver.IsFolderLink("https://drive.google.com/drive/folders/1abc123xyz?usp=sharing"));
    }

    [Fact]
    public void GoogleDriveResolver_IsFolderLink_False()
    {
        Assert.False(GoogleDriveResolver.IsFolderLink("https://drive.google.com/file/d/1abc123xyz/view"));
    }

    #endregion

    #region OneFichierResolver Tests

    [Fact]
    public void OneFichierResolver_CanResolve_StandardUrl()
    {
        var resolver = new OneFichierResolver();
        Assert.True(resolver.CanResolve("https://1fichier.com/?abc123xyz"));
    }

    [Fact]
    public void OneFichierResolver_CanResolve_FrDomain()
    {
        var resolver = new OneFichierResolver();
        Assert.True(resolver.CanResolve("https://1fichier.fr/?abc123xyz"));
    }

    [Fact]
    public void OneFichierResolver_CanResolve_InfoDomain()
    {
        var resolver = new OneFichierResolver();
        Assert.True(resolver.CanResolve("https://1fichier.info/?abc123xyz"));
    }

    [Fact]
    public void OneFichierResolver_CannotResolve_OtherHost()
    {
        var resolver = new OneFichierResolver();
        Assert.False(resolver.CanResolve("https://mediafire.com/file/abc123"));
    }

    [Fact]
    public void OneFichierResolver_ExtractWaitTime_StandardFormat()
    {
        var html = """
            <html>
            <body>
                <p>Please wait <span>60</span> seconds before downloading</p>
            </body>
            </html>
            """;

        var waitTime = OneFichierResolver.ExtractWaitTime(html);
        Assert.Equal(60, waitTime);
    }

    [Fact]
    public void OneFichierResolver_ExtractWaitTime_CounterVariable()
    {
        var html = """
            <script>
            var count = 45;
            </script>
            """;

        var waitTime = OneFichierResolver.ExtractWaitTime(html);
        Assert.Equal(45, waitTime);
    }

    [Fact]
    public void OneFichierResolver_ExtractWaitTime_NoWaitRequired()
    {
        var html = """
            <html>
            <body>
                <a href="https://cdn.1fichier.com/download/abc123">Download</a>
            </body>
            </html>
            """;

        var waitTime = OneFichierResolver.ExtractWaitTime(html);
        Assert.Equal(0, waitTime);
    }

    [Fact]
    public void OneFichierResolver_ExtractDirectDownloadUrl_CdnLink()
    {
        var html = """
            <html>
            <body>
                <a href="https://cdn1.1fichier.com/download/abc123/file.cbz">Download</a>
            </body>
            </html>
            """;

        var url = OneFichierResolver.ExtractDirectDownloadUrl(html);
        Assert.Equal("https://cdn1.1fichier.com/download/abc123/file.cbz", url);
    }

    [Fact]
    public void OneFichierResolver_ExtractDirectDownloadUrl_CzDomain()
    {
        var html = """
            <html>
            <body>
                <a href="https://cz1.1fichier.com/abc123/comic.cbz">Download</a>
            </body>
            </html>
            """;

        var url = OneFichierResolver.ExtractDirectDownloadUrl(html);
        Assert.Equal("https://cz1.1fichier.com/abc123/comic.cbz", url);
    }

    [Fact]
    public void OneFichierResolver_ExtractDirectDownloadUrl_FrDomain()
    {
        var html = """
            <html>
            <body>
                <a href="https://fr5.1fichier.com/abc123/batman.cbz">Click to download</a>
            </body>
            </html>
            """;

        var url = OneFichierResolver.ExtractDirectDownloadUrl(html);
        Assert.Equal("https://fr5.1fichier.com/abc123/batman.cbz", url);
    }

    [Fact]
    public void OneFichierResolver_ExtractFilename_FromClass()
    {
        var html = """
            <html>
            <body>
                <div class="file-name">Amazing Spider-Man 001 (2024).cbz</div>
            </body>
            </html>
            """;

        var filename = OneFichierResolver.ExtractFilename(html);
        Assert.Equal("Amazing Spider-Man 001 (2024).cbz", filename);
    }

    [Fact]
    public void OneFichierResolver_ExtractFilename_FromTitle()
    {
        var html = """
            <html>
            <head>
                <title>Batman 150 (2024).cbr - Download</title>
            </head>
            </html>
            """;

        var filename = OneFichierResolver.ExtractFilename(html);
        Assert.Equal("Batman 150 (2024).cbr", filename);
    }

    [Fact]
    public void OneFichierResolver_ExtractFilename_FromOgTitle()
    {
        var html = """
            <html>
            <head>
                <meta property="og:title" content="Superman 001.cbz" />
            </head>
            </html>
            """;

        var filename = OneFichierResolver.ExtractFilename(html);
        Assert.Equal("Superman 001.cbz", filename);
    }

    [Fact]
    public void OneFichierResolver_ExtractFileSize_MB()
    {
        var html = """
            <html>
            <body>
                <span class="size">Size: 45.5 MB</span>
            </body>
            </html>
            """;

        var size = OneFichierResolver.ExtractFileSize(html);
        Assert.NotNull(size);
        Assert.True(size > 47_000_000 && size < 48_000_000); // ~47.7 MB
    }

    [Fact]
    public void OneFichierResolver_ExtractFileSize_MO_FrenchUnit()
    {
        var html = """
            <html>
            <body>
                <span>Taille: 100 MO</span>
            </body>
            </html>
            """;

        var size = OneFichierResolver.ExtractFileSize(html);
        Assert.NotNull(size);
        Assert.True(size > 104_000_000); // > 100 MB
    }

    [Fact]
    public void OneFichierResolver_ExtractFileSize_GB()
    {
        var html = """
            <html>
            <body>
                <span>File size: 1.5 GB</span>
            </body>
            </html>
            """;

        var size = OneFichierResolver.ExtractFileSize(html);
        Assert.NotNull(size);
        Assert.True(size > 1_500_000_000); // > 1.5 GB
    }

    [Fact]
    public void OneFichierResolver_HasCorrectPriority()
    {
        var resolver = new OneFichierResolver();
        Assert.Equal(6, resolver.Priority);
    }

    [Fact]
    public void OneFichierResolver_IsAvailable()
    {
        var resolver = new OneFichierResolver();
        Assert.True(resolver.IsAvailable);
    }

    #endregion

    #region ZippyshareResolver Tests

    [Fact]
    public void ZippyshareResolver_CanResolve_StandardUrl()
    {
        var resolver = new ZippyshareResolver();
        Assert.True(resolver.CanResolve("https://www.zippyshare.com/v/abc123/file.html"));
    }

    [Fact]
    public void ZippyshareResolver_CanResolve_NumberedServer()
    {
        var resolver = new ZippyshareResolver();
        Assert.True(resolver.CanResolve("https://www15.zippyshare.com/v/abc123/file.html"));
    }

    [Fact]
    public void ZippyshareResolver_CanResolve_AllKnownServers()
    {
        var resolver = new ZippyshareResolver();
        
        // Test a sample of known server numbers
        Assert.True(resolver.CanResolve("https://www1.zippyshare.com/v/abc/file.html"));
        Assert.True(resolver.CanResolve("https://www5.zippyshare.com/v/abc/file.html"));
        Assert.True(resolver.CanResolve("https://www10.zippyshare.com/v/abc/file.html"));
        Assert.True(resolver.CanResolve("https://www20.zippyshare.com/v/abc/file.html"));
    }

    [Fact]
    public void ZippyshareResolver_CannotResolve_OtherHost()
    {
        var resolver = new ZippyshareResolver();
        Assert.False(resolver.CanResolve("https://mediafire.com/file/abc123"));
    }

    [Fact]
    public void ZippyshareResolver_IsNotAvailable()
    {
        var resolver = new ZippyshareResolver();
        Assert.False(resolver.IsAvailable);
    }

    [Fact]
    public void ZippyshareResolver_HasLowPriority()
    {
        var resolver = new ZippyshareResolver();
        Assert.Equal(99, resolver.Priority); // Low priority since defunct
    }

    [Fact]
    public async Task ZippyshareResolver_ResolveAsync_ReturnsHostUnavailable()
    {
        var resolver = new ZippyshareResolver();
        
        var result = await resolver.ResolveAsync("https://www15.zippyshare.com/v/abc123/file.html");
        
        Assert.False(result.Success);
        Assert.Equal(HostResolverFailureReason.HostUnavailable, result.FailureReason);
        Assert.Contains("shut down", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ZippyshareResolver_VerifyAsync_ReturnsUnavailable()
    {
        var resolver = new ZippyshareResolver();
        
        var result = await resolver.VerifyAsync("https://www15.zippyshare.com/v/abc123/file.html");
        
        Assert.False(result.IsAvailable);
        Assert.Equal(HostResolverFailureReason.HostUnavailable, result.FailureReason);
        Assert.Contains("shut down", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZippyshareResolver_ExtractServerNumber_ValidServer()
    {
        var serverNum = ZippyshareResolver.ExtractServerNumber("https://www15.zippyshare.com/v/abc123/file.html");
        Assert.Equal(15, serverNum);
    }

    [Fact]
    public void ZippyshareResolver_ExtractServerNumber_SingleDigit()
    {
        var serverNum = ZippyshareResolver.ExtractServerNumber("https://www3.zippyshare.com/v/abc123/file.html");
        Assert.Equal(3, serverNum);
    }

    [Fact]
    public void ZippyshareResolver_ExtractServerNumber_MainSite()
    {
        var serverNum = ZippyshareResolver.ExtractServerNumber("https://www.zippyshare.com/v/abc123/file.html");
        Assert.Null(serverNum); // No number in "www"
    }

    [Fact]
    public void ZippyshareResolver_ExtractFileKey_ValidPath()
    {
        var fileKey = ZippyshareResolver.ExtractFileKey("https://www15.zippyshare.com/v/abc123xyz/file.html");
        Assert.Equal("abc123xyz", fileKey);
    }

    [Fact]
    public void ZippyshareResolver_ExtractFileKey_InvalidPath()
    {
        var fileKey = ZippyshareResolver.ExtractFileKey("https://www15.zippyshare.com/invalid/path");
        Assert.Null(fileKey);
    }

    [Fact]
    public void ZippyshareResolver_IsZippyshareUrl_True()
    {
        Assert.True(ZippyshareResolver.IsZippyshareUrl("https://www15.zippyshare.com/v/abc123/file.html"));
        Assert.True(ZippyshareResolver.IsZippyshareUrl("https://zippyshare.com/v/abc123/file.html"));
    }

    [Fact]
    public void ZippyshareResolver_IsZippyshareUrl_False()
    {
        Assert.False(ZippyshareResolver.IsZippyshareUrl("https://mediafire.com/file/abc123"));
        Assert.False(ZippyshareResolver.IsZippyshareUrl("https://google.com"));
        Assert.False(ZippyshareResolver.IsZippyshareUrl(""));
        Assert.False(ZippyshareResolver.IsZippyshareUrl(null!));
    }

    [Fact]
    public void ZippyshareResolver_ShutdownDate_IsCorrect()
    {
        // Zippyshare shut down on March 19, 2023
        Assert.Equal(new DateTime(2023, 3, 19, 0, 0, 0, DateTimeKind.Utc), ZippyshareResolver.ShutdownDate);
    }

    #endregion

    #region Factory with New Resolvers Tests

    [Fact]
    public void Factory_GetResolver_ReturnsOneFichierForOneFichierUrl()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://1fichier.com/?abc123");

        Assert.NotNull(resolver);
        Assert.Equal("1fichier", resolver.HostId);
    }

    [Fact]
    public void Factory_GetResolver_DoesNotReturnZippyshareByDefault()
    {
        var factory = new DownloadHostResolverFactory();
        // Zippyshare is registered but IsAvailable = false, so GetResolver won't return it
        var resolver = factory.GetResolver("https://www15.zippyshare.com/v/abc123/file.html");

        // Should return null because Zippyshare is not available
        Assert.Null(resolver);
    }

    [Fact]
    public void Factory_GetAllResolvers_IncludesZippyshare()
    {
        var factory = new DownloadHostResolverFactory();
        var resolvers = factory.GetAllResolvers();

        Assert.Contains(resolvers, r => r.HostId == "Zippyshare");
    }

    [Fact]
    public void Factory_GetAvailableResolvers_ExcludesZippyshare()
    {
        var factory = new DownloadHostResolverFactory();
        var resolvers = factory.GetAvailableResolvers();

        Assert.DoesNotContain(resolvers, r => r.HostId == "Zippyshare");
    }

    [Fact]
    public void Factory_GetHostInfos_IncludesOneFichier()
    {
        var factory = new DownloadHostResolverFactory();
        var infos = factory.GetHostInfos();

        var oneFichier = infos.FirstOrDefault(h => h.HostId == "1fichier");
        Assert.NotNull(oneFichier);
        Assert.Equal("1fichier", oneFichier.DisplayName);
        Assert.True(oneFichier.IsAvailable);
    }

    [Fact]
    public void Factory_GetHostInfos_ZippyshareShowsNotAvailable()
    {
        var factory = new DownloadHostResolverFactory();
        var infos = factory.GetHostInfos();

        var zippyshare = infos.FirstOrDefault(h => h.HostId == "Zippyshare");
        Assert.NotNull(zippyshare);
        Assert.Contains("Defunct", zippyshare.DisplayName);
        Assert.False(zippyshare.IsAvailable);
    }

    [Fact]
    public void Factory_GetResolver_ReturnsDropboxForDropboxUrl()
    {
        var factory = new DownloadHostResolverFactory();
        // Use a URL without file extension to avoid DirectDownloadResolver matching first
        var resolver = factory.GetResolver("https://www.dropbox.com/s/abc123/file?dl=0");

        Assert.NotNull(resolver);
        Assert.Equal("Dropbox", resolver.HostId);
    }

    [Fact]
    public void Factory_GetResolver_ReturnsGoogleDriveForDriveUrl()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolver("https://drive.google.com/file/d/1abc123/view");

        Assert.NotNull(resolver);
        Assert.Equal("GoogleDrive", resolver.HostId);
    }

    [Fact]
    public void Factory_HasAllBuiltInResolvers()
    {
        var factory = new DownloadHostResolverFactory();
        var infos = factory.GetHostInfos();

        Assert.Contains(infos, h => h.HostId == "Direct");
        Assert.Contains(infos, h => h.HostId == "MediaFire");
        Assert.Contains(infos, h => h.HostId == "Pixeldrain");
        Assert.Contains(infos, h => h.HostId == "GoogleDrive");
        Assert.Contains(infos, h => h.HostId == "Dropbox");
        Assert.Contains(infos, h => h.HostId == "1fichier");
        Assert.Contains(infos, h => h.HostId == "Zippyshare");
    }

    #endregion

    #region DdlDownloadService Integration Tests

    [Fact]
    public async Task DdlDownloadService_UsesResolverFactory_ForHostedLinks()
    {
        // Arrange
        var factory = new DownloadHostResolverFactory();
        var service = new DdlDownloadService(factory);
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            SourceSite = "getcomics",
            ReleaseTitle = "Test Comic #1",
            ParsedInfo = new DdlParsedInfo { SeriesTitle = "Test Comic", IssueNumber = 1 },
            DownloadLinks = new List<DdlDownloadLink>
            {
                new() 
                { 
                    Url = "https://pixeldrain.com/u/abc123",
                    LinkType = DdlLinkType.Hoster,
                    HostName = "Pixeldrain",
                    Priority = 3
                }
            }
        };
        
        // Act - This will fail the actual download (no real file), but should attempt resolution
        var result = await service.DownloadAsync(candidate);
        
        // Assert - Should have attempted to resolve (will fail at actual download stage)
        // The key test is that it doesn't crash and handles the resolver properly
        Assert.False(result.Success); // Expected - no real file to download
    }

    [Fact]
    public async Task DdlDownloadService_FallsBackToAlternateLinks_OnFailure()
    {
        // Arrange
        var factory = new DownloadHostResolverFactory();
        var service = new DdlDownloadService(factory);
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            SourceSite = "getcomics",
            ReleaseTitle = "Test Comic #1",
            ParsedInfo = new DdlParsedInfo { SeriesTitle = "Test Comic", IssueNumber = 1 },
            DownloadLinks = new List<DdlDownloadLink>
            {
                new() 
                { 
                    Url = "https://invalid-host.example.com/file.cbz",
                    LinkType = DdlLinkType.Direct,
                    Priority = 1
                },
                new() 
                { 
                    Url = "https://also-invalid.example.com/file.cbz",
                    LinkType = DdlLinkType.Direct,
                    Priority = 2
                }
            }
        };
        
        // Act
        var result = await service.DownloadAsync(candidate);
        
        // Assert - Both links will fail, but service should try both
        Assert.False(result.Success);
    }

    [Fact]
    public void DdlDownloadService_WorksWithoutResolverFactory()
    {
        // Arrange - Service should work without resolver (for backward compatibility)
        var service = new DdlDownloadService(resolverFactory: null, logger: null);
        
        // Assert - Service should be instantiated successfully
        Assert.NotNull(service);
    }

    [Fact]
    public async Task DdlDownloadService_HandlesCandidateWithNoLinks()
    {
        // Arrange
        var factory = new DownloadHostResolverFactory();
        var service = new DdlDownloadService(factory);
        
        var candidate = new DdlCandidate
        {
            Id = Guid.NewGuid().ToString(),
            SourceSite = "getcomics",
            ReleaseTitle = "Test Comic #1",
            ParsedInfo = new DdlParsedInfo { SeriesTitle = "Test Comic", IssueNumber = 1 },
            DownloadLinks = new List<DdlDownloadLink>() // Empty
        };
        
        // Act
        var result = await service.DownloadAsync(candidate);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(DdlDownloadFailureReason.NoValidLinks, result.FailureReason);
    }

    #endregion
}
