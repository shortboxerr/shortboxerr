using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl.Resolvers;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for premium file host resolvers (Rapidgator, Uploaded.net).
/// </summary>
public class PremiumHostResolverTests
{
    #region RapidgatorResolver Tests

    [Fact]
    public void RapidgatorResolver_HasCorrectHostId()
    {
        var resolver = new RapidgatorResolver();
        Assert.Equal("rapidgator", resolver.HostId);
    }

    [Fact]
    public void RapidgatorResolver_HasCorrectDisplayName()
    {
        var resolver = new RapidgatorResolver();
        Assert.Equal("Rapidgator", resolver.DisplayName);
    }

    [Fact]
    public void RapidgatorResolver_SupportsExpectedHosts()
    {
        var resolver = new RapidgatorResolver();

        Assert.Contains("rapidgator.net", resolver.SupportedHosts);
        Assert.Contains("rapidgator.asia", resolver.SupportedHosts);
        Assert.Contains("rg.to", resolver.SupportedHosts);
    }

    [Fact]
    public void RapidgatorResolver_IsAvailable()
    {
        var resolver = new RapidgatorResolver();
        Assert.True(resolver.IsAvailable);
    }

    [Fact]
    public void RapidgatorResolver_HasLowPriority()
    {
        var resolver = new RapidgatorResolver();
        Assert.Equal(15, resolver.Priority);
    }

    [Theory]
    [InlineData("https://rapidgator.net/file/abc123/filename.zip", true)]
    [InlineData("https://rapidgator.net/file/xyz789", true)]
    [InlineData("https://rg.to/file/abc123", true)]
    [InlineData("https://rapidgator.asia/file/test", true)]
    [InlineData("https://mediafire.com/file/abc123", false)]
    [InlineData("https://example.com/rapidgator", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RapidgatorResolver_CanResolve_ReturnsExpected(string? url, bool expected)
    {
        var resolver = new RapidgatorResolver();
        Assert.Equal(expected, resolver.CanResolve(url ?? ""));
    }

    [Theory]
    [InlineData("https://rapidgator.net/file/abc123/filename.zip", "abc123")]
    [InlineData("https://rapidgator.net/file/xyz789", "xyz789")]
    [InlineData("https://rg.to/file/test123", "test123")]
    [InlineData("https://rapidgator.asia/file/ABCDEF", "ABCDEF")]
    [InlineData("https://example.com/file/abc123", null)]
    public void RapidgatorResolver_ExtractFileId_ReturnsExpected(string url, string? expectedId)
    {
        var result = RapidgatorResolver.ExtractFileId(url);
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractSessionId_FromJsonResponse()
    {
        var json = @"{""response"":{""session_id"":""abc123token""}}";
        var result = RapidgatorResolver.ExtractSessionId(json);
        Assert.Equal("abc123token", result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractSessionId_FromTokenResponse()
    {
        var json = @"{""response"":{""token"":""xyz789token""}}";
        var result = RapidgatorResolver.ExtractSessionId(json);
        Assert.Equal("xyz789token", result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractSessionId_InvalidJson_ReturnsNull()
    {
        var result = RapidgatorResolver.ExtractSessionId("invalid json");
        Assert.Null(result);
    }

    [Fact]
    public void RapidgatorResolver_ParseFileInfo_ValidJson()
    {
        var json = @"{""response"":{""file"":{""name"":""test.cbz"",""size"":12345678}}}";
        var result = RapidgatorResolver.ParseFileInfo(json);

        Assert.NotNull(result);
        Assert.Equal("test.cbz", result.Value.Filename);
        Assert.Equal(12345678, result.Value.Size);
    }

    [Fact]
    public void RapidgatorResolver_ParseFileInfo_InvalidJson_ReturnsNull()
    {
        var result = RapidgatorResolver.ParseFileInfo("invalid");
        Assert.Null(result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractDownloadUrl_FromJson()
    {
        var json = @"{""response"":{""download_url"":""https://download.rapidgator.net/abc123""}}";
        var result = RapidgatorResolver.ExtractDownloadUrl(json);
        Assert.Equal("https://download.rapidgator.net/abc123", result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractDownloadUrl_AlternateFormat()
    {
        var json = @"{""response"":{""url"":""https://direct.rg.to/file.zip""}}";
        var result = RapidgatorResolver.ExtractDownloadUrl(json);
        Assert.Equal("https://direct.rg.to/file.zip", result);
    }

    [Fact]
    public void RapidgatorResolver_ExtractFilenameFromPage_ClassPattern()
    {
        var html = @"<div class=""file-name"">Comic Book.cbz</div>";
        var result = RapidgatorResolver.ExtractFilenameFromPage(html);
        Assert.Equal("Comic Book.cbz", result);
    }

    [Theory]
    [InlineData("File size: 100 MB", 104857600)]
    [InlineData("File size: 1.5 GB", 1610612736)]
    [InlineData("File size: 500 KB", 512000)]
    [InlineData("File size: 1024 B", 1024)]
    public void RapidgatorResolver_ExtractFileSizeFromPage_ParsesCorrectly(string html, long expectedBytes)
    {
        var result = RapidgatorResolver.ExtractFileSizeFromPage(html);
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task RapidgatorResolver_ResolveAsync_WithoutCredentials_ReturnsFailure()
    {
        var resolver = new RapidgatorResolver();
        var result = await resolver.ResolveAsync("https://rapidgator.net/file/invalid123");

        Assert.False(result.Success);
        // Without credentials, the resolver will either fail with AuthenticationRequired or 
        // with a network/file error depending on the URL and server response
        Assert.True(
            result.FailureReason == HostResolverFailureReason.AuthenticationRequired ||
            result.FailureReason == HostResolverFailureReason.FileNotFound ||
            result.FailureReason == HostResolverFailureReason.NetworkError,
            $"Expected auth required, file not found, or network error but got {result.FailureReason}"
        );
    }

    #endregion

    #region UploadedResolver Tests

    [Fact]
    public void UploadedResolver_HasCorrectHostId()
    {
        var resolver = new UploadedResolver();
        Assert.Equal("uploaded", resolver.HostId);
    }

    [Fact]
    public void UploadedResolver_HasCorrectDisplayName()
    {
        var resolver = new UploadedResolver();
        Assert.Equal("Uploaded.net", resolver.DisplayName);
    }

    [Fact]
    public void UploadedResolver_SupportsExpectedHosts()
    {
        var resolver = new UploadedResolver();

        Assert.Contains("uploaded.net", resolver.SupportedHosts);
        Assert.Contains("uploaded.to", resolver.SupportedHosts);
        Assert.Contains("ul.to", resolver.SupportedHosts);
    }

    [Fact]
    public void UploadedResolver_IsAvailable()
    {
        var resolver = new UploadedResolver();
        Assert.True(resolver.IsAvailable);
    }

    [Fact]
    public void UploadedResolver_HasLowPriority()
    {
        var resolver = new UploadedResolver();
        Assert.Equal(16, resolver.Priority);
    }

    [Theory]
    [InlineData("https://uploaded.net/file/abc123", true)]
    [InlineData("https://uploaded.to/file/xyz789/test.zip", true)]
    [InlineData("https://ul.to/abc123", true)]
    [InlineData("https://ul.to/xyz789", true)]
    [InlineData("https://mediafire.com/file/abc123", false)]
    [InlineData("https://example.com/uploaded", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UploadedResolver_CanResolve_ReturnsExpected(string? url, bool expected)
    {
        var resolver = new UploadedResolver();
        Assert.Equal(expected, resolver.CanResolve(url ?? ""));
    }

    [Theory]
    [InlineData("https://uploaded.net/file/abc123", "abc123")]
    [InlineData("https://uploaded.to/file/xyz789/filename.zip", "xyz789")]
    [InlineData("https://ul.to/test123", "test123")]
    [InlineData("https://ul.to/ABCDEF", "ABCDEF")]
    [InlineData("https://example.com/file/abc123", null)]
    public void UploadedResolver_ExtractFileId_ReturnsExpected(string url, string? expectedId)
    {
        var result = UploadedResolver.ExtractFileId(url);
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void UploadedResolver_ExtractAuthToken_FromJsonResponse()
    {
        var json = @"{""access_token"":""abc123token""}";
        var result = UploadedResolver.ExtractAuthToken(json);
        Assert.Equal("abc123token", result);
    }

    [Fact]
    public void UploadedResolver_ExtractAuthToken_FromCsvResponse()
    {
        var csv = "apikey,xyz789token";
        var result = UploadedResolver.ExtractAuthToken(csv);
        Assert.Equal("xyz789token", result);
    }

    [Fact]
    public void UploadedResolver_ExtractAuthToken_FromKeyValueResponse()
    {
        var response = "token:abc123";
        var result = UploadedResolver.ExtractAuthToken(response);
        Assert.Equal("abc123", result);
    }

    [Fact]
    public void UploadedResolver_ParseFileInfo_ValidJson()
    {
        var json = @"[{""name"":""test.cbz"",""size"":12345678}]";
        var result = UploadedResolver.ParseFileInfo(json);

        Assert.NotNull(result);
        Assert.Equal("test.cbz", result.Value.Filename);
        Assert.Equal(12345678, result.Value.Size);
    }

    [Fact]
    public void UploadedResolver_ParseFileInfo_CsvFormat()
    {
        var csv = "test.cbz,abc123,12345678,online";
        var result = UploadedResolver.ParseFileInfo(csv);

        Assert.NotNull(result);
        Assert.Equal("test.cbz", result.Value.Filename);
        Assert.Equal(12345678, result.Value.Size);
    }

    [Fact]
    public void UploadedResolver_ExtractDownloadUrl_FromJson()
    {
        var json = @"{""url"":""https://download.uploaded.net/abc123""}";
        var result = UploadedResolver.ExtractDownloadUrl(json);
        Assert.Equal("https://download.uploaded.net/abc123", result);
    }

    [Fact]
    public void UploadedResolver_ExtractDownloadUrl_PlainUrl()
    {
        var response = "https://direct.uploaded.net/file.zip";
        var result = UploadedResolver.ExtractDownloadUrl(response);
        Assert.Equal("https://direct.uploaded.net/file.zip", result);
    }

    [Fact]
    public void UploadedResolver_ExtractFilenameFromPage_ClassPattern()
    {
        var html = @"<div class=""file_name"">Comic Book.cbz</div>";
        var result = UploadedResolver.ExtractFilenameFromPage(html);
        Assert.Equal("Comic Book.cbz", result);
    }

    [Fact]
    public void UploadedResolver_ExtractFilenameFromPage_IdPattern()
    {
        var html = @"<span id=""filename"">Test File.zip</span>";
        var result = UploadedResolver.ExtractFilenameFromPage(html);
        Assert.Equal("Test File.zip", result);
    }

    [Fact]
    public void UploadedResolver_ExtractFilenameFromPage_TitlePattern()
    {
        var html = @"<title>Download MyComic.cbz - Uploaded.net</title>";
        var result = UploadedResolver.ExtractFilenameFromPage(html);
        Assert.Equal("MyComic.cbz", result);
    }

    [Theory]
    [InlineData("Size: 100 MB", 104857600)]
    [InlineData("Filesize: 1.5 GB", 1610612736)]
    [InlineData("File size: 500 KB", 512000)]
    public void UploadedResolver_ExtractFileSizeFromPage_ParsesCorrectly(string html, long expectedBytes)
    {
        var result = UploadedResolver.ExtractFileSizeFromPage(html);
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public async Task UploadedResolver_ResolveAsync_WithoutCredentials_ReturnsFailure()
    {
        var resolver = new UploadedResolver();
        var result = await resolver.ResolveAsync("https://uploaded.net/file/invalid123");

        Assert.False(result.Success);
        // Without credentials, the resolver will either fail with AuthenticationRequired or
        // with a network/file error depending on the URL and server response
        Assert.True(
            result.FailureReason == HostResolverFailureReason.AuthenticationRequired ||
            result.FailureReason == HostResolverFailureReason.FileNotFound ||
            result.FailureReason == HostResolverFailureReason.NetworkError ||
            result.FailureReason == HostResolverFailureReason.HostUnavailable,
            $"Expected auth required, file not found, network error, or host unavailable but got {result.FailureReason}"
        );
    }

    #endregion

    #region Factory Integration Tests

    [Fact]
    public void DownloadHostResolverFactory_RegistersRapidgatorResolver()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolverById("rapidgator");

        Assert.NotNull(resolver);
        Assert.IsType<RapidgatorResolver>(resolver);
    }

    [Fact]
    public void DownloadHostResolverFactory_RegistersUploadedResolver()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolverById("uploaded");

        Assert.NotNull(resolver);
        Assert.IsType<UploadedResolver>(resolver);
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_RapidgatorUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://rapidgator.net/file/abc123"));
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_UploadedUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://uploaded.net/file/abc123"));
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_UlToUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://ul.to/abc123"));
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_RgToUrl()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://rg.to/file/abc123"));
    }

    [Fact]
    public void DownloadHostResolverFactory_GetHostInfos_IncludesPremiumHosts()
    {
        var factory = new DownloadHostResolverFactory();
        var hostInfos = factory.GetHostInfos();

        Assert.Contains(hostInfos, h => h.HostId == "rapidgator");
        Assert.Contains(hostInfos, h => h.HostId == "uploaded");
    }

    #endregion

    #region HostCredentials Tests

    [Fact]
    public void HostCredentials_CanSetUsername()
    {
        var creds = new HostCredentials { Username = "testuser" };
        Assert.Equal("testuser", creds.Username);
    }

    [Fact]
    public void HostCredentials_CanSetPassword()
    {
        var creds = new HostCredentials { Password = "secret123" };
        Assert.Equal("secret123", creds.Password);
    }

    [Fact]
    public void HostCredentials_CanSetApiKey()
    {
        var creds = new HostCredentials { ApiKey = "api_key_abc123" };
        Assert.Equal("api_key_abc123", creds.ApiKey);
    }

    [Fact]
    public void HostResolverOptions_DefaultTimeoutIs30Seconds()
    {
        var options = new HostResolverOptions();
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void HostResolverOptions_FollowRedirectsEnabledByDefault()
    {
        var options = new HostResolverOptions();
        Assert.True(options.FollowRedirects);
    }

    [Fact]
    public void HostResolverOptions_CanSetCredentials()
    {
        var creds = new HostCredentials
        {
            Username = "user",
            Password = "pass",
            ApiKey = "key"
        };
        var options = new HostResolverOptions { Credentials = creds };

        Assert.NotNull(options.Credentials);
        Assert.Equal("user", options.Credentials.Username);
        Assert.Equal("pass", options.Credentials.Password);
        Assert.Equal("key", options.Credentials.ApiKey);
    }

    #endregion

    #region HostResolverResult Tests

    [Fact]
    public void HostResolverResult_Succeeded_SetsCorrectProperties()
    {
        var result = HostResolverResult.Succeeded(
            "https://direct.example.com/file.zip",
            "file.zip",
            1024000
        );

        Assert.True(result.Success);
        Assert.Equal("https://direct.example.com/file.zip", result.DirectUrl);
        Assert.Equal("file.zip", result.Filename);
        Assert.Equal(1024000, result.FileSize);
        Assert.Equal(HostResolverFailureReason.None, result.FailureReason);
    }

    [Fact]
    public void HostResolverResult_Failed_SetsCorrectProperties()
    {
        var result = HostResolverResult.Failed(
            HostResolverFailureReason.AuthenticationRequired,
            "Premium account required"
        );

        Assert.False(result.Success);
        Assert.Null(result.DirectUrl);
        Assert.Equal(HostResolverFailureReason.AuthenticationRequired, result.FailureReason);
        Assert.Equal("Premium account required", result.ErrorMessage);
    }

    [Fact]
    public void HostResolverResult_UrlExpiry_CanBeSet()
    {
        var result = new HostResolverResult
        {
            Success = true,
            DirectUrl = "https://example.com/file",
            UrlExpiry = TimeSpan.FromHours(24)
        };

        Assert.Equal(TimeSpan.FromHours(24), result.UrlExpiry);
    }

    #endregion
}
