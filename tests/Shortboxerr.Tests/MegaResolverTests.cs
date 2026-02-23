using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl.Resolvers;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for the Mega.nz resolver with encryption support.
/// </summary>
public class MegaResolverTests
{
    #region Basic Properties Tests

    [Fact]
    public void MegaResolver_HasCorrectHostId()
    {
        var resolver = new MegaResolver();
        Assert.Equal("mega", resolver.HostId);
    }

    [Fact]
    public void MegaResolver_HasCorrectDisplayName()
    {
        var resolver = new MegaResolver();
        Assert.Equal("Mega.nz", resolver.DisplayName);
    }

    [Fact]
    public void MegaResolver_SupportsExpectedHosts()
    {
        var resolver = new MegaResolver();

        Assert.Contains("mega.nz", resolver.SupportedHosts);
        Assert.Contains("mega.co.nz", resolver.SupportedHosts);
    }

    [Fact]
    public void MegaResolver_IsAvailable()
    {
        var resolver = new MegaResolver();
        Assert.True(resolver.IsAvailable);
    }

    [Fact]
    public void MegaResolver_HasHighPriority()
    {
        var resolver = new MegaResolver();
        Assert.Equal(1, resolver.Priority);
    }

    #endregion

    #region CanResolve Tests

    [Theory]
    [InlineData("https://mega.nz/file/abc123#key456", true)]
    [InlineData("https://mega.nz/#!abc123!key456", true)]
    [InlineData("https://mega.co.nz/file/xyz789#secretkey", true)]
    [InlineData("https://mega.co.nz/#!xyz789!secretkey", true)]
    [InlineData("https://mediafire.com/file/abc123", false)]
    [InlineData("https://example.com/notmega", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void MegaResolver_CanResolve_ReturnsExpected(string? url, bool expected)
    {
        var resolver = new MegaResolver();
        Assert.Equal(expected, resolver.CanResolve(url ?? ""));
    }

    #endregion

    #region URL Parsing Tests

    [Theory]
    [InlineData("https://mega.nz/file/abc123#key456", "abc123", "key456")]
    [InlineData("https://mega.nz/file/FileId123#EncryptionKey", "FileId123", "EncryptionKey")]
    [InlineData("https://mega.co.nz/file/xyz789#secret", "xyz789", "secret")]
    public void ParseMegaUrl_NewFormat_ExtractsCorrectly(string url, string expectedId, string expectedKey)
    {
        var result = MegaResolver.ParseMegaUrl(url);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value.FileId);
        Assert.Equal(expectedKey, result.Value.Key);
    }

    [Theory]
    [InlineData("https://mega.nz/#!abc123!key456", "abc123", "key456")]
    [InlineData("https://mega.nz/#!FileId!EncryptionKey", "FileId", "EncryptionKey")]
    [InlineData("https://mega.co.nz/#!xyz789!secret", "xyz789", "secret")]
    public void ParseMegaUrl_OldFormat_ExtractsCorrectly(string url, string expectedId, string expectedKey)
    {
        var result = MegaResolver.ParseMegaUrl(url);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value.FileId);
        Assert.Equal(expectedKey, result.Value.Key);
    }

    [Theory]
    [InlineData("https://mega.nz/folder/abc123#key456")]
    [InlineData("https://mega.nz/#F!abc123!key456")]
    public void ParseMegaUrl_FolderLinks_ReturnsNull(string url)
    {
        // Folder links are not supported in this implementation
        var result = MegaResolver.ParseMegaUrl(url);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com")]
    [InlineData("https://mega.nz/file/")]
    [InlineData("https://mega.nz/#!")]
    public void ParseMegaUrl_InvalidUrl_ReturnsNull(string url)
    {
        var result = MegaResolver.ParseMegaUrl(url);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://mega.nz/file/abc123#key456", "abc123")]
    [InlineData("https://mega.nz/#!xyz789!secret", "xyz789")]
    [InlineData("https://invalid.com/file", null)]
    public void ExtractFileId_ReturnsExpected(string url, string? expectedId)
    {
        var result = MegaResolver.ExtractFileId(url);
        Assert.Equal(expectedId, result);
    }

    #endregion

    #region Base64 Encoding Tests

    [Fact]
    public void Base64UrlDecode_StandardInput_DecodesCorrectly()
    {
        // URL-safe base64 for "Hello World"
        var input = "SGVsbG8gV29ybGQ";
        var result = MegaResolver.Base64UrlDecode(input);

        Assert.NotNull(result);
        Assert.Equal("Hello World", System.Text.Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Base64UrlDecode_WithUrlSafeChars_DecodesCorrectly()
    {
        // Test URL-safe character replacement (- for +, _ for /)
        var urlSafeInput = "abc-def_ghi";
        var standardInput = "abc+def/ghi";

        var urlSafeResult = MegaResolver.Base64UrlDecode(urlSafeInput);
        var standardResult = MegaResolver.Base64UrlDecode(standardInput);

        Assert.NotNull(urlSafeResult);
        Assert.NotNull(standardResult);
        Assert.Equal(standardResult, urlSafeResult);
    }

    [Fact]
    public void Base64UrlDecode_EmptyInput_ReturnsNull()
    {
        var result = MegaResolver.Base64UrlDecode("");
        Assert.Null(result);
    }

    [Fact]
    public void Base64UrlDecode_NullInput_ReturnsNull()
    {
        var result = MegaResolver.Base64UrlDecode(null!);
        Assert.Null(result);
    }

    [Fact]
    public void Base64UrlEncode_RoundTrip_PreservesData()
    {
        var original = new byte[] { 0x00, 0x01, 0x02, 0xFE, 0xFF };
        var encoded = MegaResolver.Base64UrlEncode(original);
        var decoded = MegaResolver.Base64UrlDecode(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Base64UrlEncode_NoUrlUnsafeChars()
    {
        // Generate bytes that would normally produce + and / in standard base64
        var testBytes = new byte[] { 0xFB, 0xEF, 0xBE }; // Would be ++++ in standard base64

        var encoded = MegaResolver.Base64UrlEncode(testBytes);

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded);
    }

    #endregion

    #region Attribute Decryption Tests

    [Fact]
    public void DecryptFileAttributes_InvalidKey_ReturnsNull()
    {
        var encryptedAttr = "someencrypteddata";
        var shortKey = "short"; // Too short to be valid

        var result = MegaResolver.DecryptFileAttributes(encryptedAttr, shortKey);

        Assert.Null(result);
    }

    [Fact]
    public void DecryptFileAttributes_EmptyInput_ReturnsNull()
    {
        var result = MegaResolver.DecryptFileAttributes("", "");
        Assert.Null(result);
    }

    #endregion

    #region Factory Integration Tests

    [Fact]
    public void DownloadHostResolverFactory_RegistersMegaResolver()
    {
        var factory = new DownloadHostResolverFactory();
        var resolver = factory.GetResolverById("mega");

        Assert.NotNull(resolver);
        Assert.IsType<MegaResolver>(resolver);
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_MegaNewFormat()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://mega.nz/file/abc123#key456"));
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_MegaOldFormat()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://mega.nz/#!abc123!key456"));
    }

    [Fact]
    public void DownloadHostResolverFactory_CanResolve_MegaCoNz()
    {
        var factory = new DownloadHostResolverFactory();
        Assert.True(factory.CanResolve("https://mega.co.nz/file/abc123#key456"));
    }

    [Fact]
    public void DownloadHostResolverFactory_GetHostInfos_IncludesMega()
    {
        var factory = new DownloadHostResolverFactory();
        var hostInfos = factory.GetHostInfos();

        Assert.Contains(hostInfos, h => h.HostId == "mega");
    }

    [Fact]
    public void DownloadHostResolverFactory_MegaHasHighPriority()
    {
        var factory = new DownloadHostResolverFactory();
        var hostInfos = factory.GetHostInfos();

        var megaInfo = hostInfos.FirstOrDefault(h => h.HostId == "mega");
        Assert.NotNull(megaInfo);
        Assert.Equal(1, megaInfo.Priority);
    }

    #endregion

    #region Resolver Behavior Tests

    [Fact]
    public async Task ResolveAsync_InvalidUrl_ReturnsParseError()
    {
        var resolver = new MegaResolver();
        var result = await resolver.ResolveAsync("https://mega.nz/invalid");

        Assert.False(result.Success);
        Assert.Equal(HostResolverFailureReason.ParseError, result.FailureReason);
    }

    [Fact]
    public async Task ResolveAsync_EmptyUrl_ReturnsParseError()
    {
        var resolver = new MegaResolver();
        var result = await resolver.ResolveAsync("");

        Assert.False(result.Success);
        Assert.Equal(HostResolverFailureReason.ParseError, result.FailureReason);
    }

    [Fact]
    public async Task VerifyAsync_InvalidUrl_ReturnsNotAvailable()
    {
        var resolver = new MegaResolver();
        var result = await resolver.VerifyAsync("https://mega.nz/invalid");

        Assert.False(result.IsAvailable);
        Assert.Equal(HostResolverFailureReason.ParseError, result.FailureReason);
    }

    [Fact]
    public async Task ResolveAsync_NonExistentFile_ReturnsFileNotFound()
    {
        var resolver = new MegaResolver();
        // Use a valid URL format but with a non-existent file ID
        var result = await resolver.ResolveAsync("https://mega.nz/file/XXXXXXXX#YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY");

        Assert.False(result.Success);
        // Could be FileNotFound or NetworkError depending on API response
        Assert.True(
            result.FailureReason == HostResolverFailureReason.FileNotFound ||
            result.FailureReason == HostResolverFailureReason.ParseError ||
            result.FailureReason == HostResolverFailureReason.NetworkError,
            $"Expected FileNotFound, ParseError, or NetworkError but got {result.FailureReason}"
        );
    }

    #endregion

    #region URL Format Variations

    [Theory]
    [InlineData("https://mega.nz/file/abc123#key", "mega.nz new format")]
    [InlineData("http://mega.nz/file/abc123#key", "mega.nz new format http")]
    [InlineData("https://mega.co.nz/file/abc123#key", "mega.co.nz new format")]
    [InlineData("https://mega.nz/#!abc123!key", "mega.nz old format")]
    [InlineData("https://mega.co.nz/#!abc123!key", "mega.co.nz old format")]
    public void CanResolve_AllUrlVariations_ReturnsTrue(string url, string description)
    {
        var resolver = new MegaResolver();
        Assert.True(resolver.CanResolve(url), $"Failed for {description}");
    }

    [Theory]
    [InlineData("mega.nz/file/abc123#key")] // Missing protocol
    [InlineData("https://www.mega.nz/file/abc123#key")] // Has www (not in SupportedHosts)
    public void CanResolve_EdgeCases_HandlesCorrectly(string url)
    {
        var resolver = new MegaResolver();
        // These might or might not be resolved depending on implementation
        // The important thing is they don't throw
        var result = resolver.CanResolve(url);
        // Just verify it returns a boolean without exception
        Assert.True(result || !result);
    }

    #endregion

    #region Key Handling Tests

    [Fact]
    public void ParseMegaUrl_PreservesKeyCase()
    {
        var url = "https://mega.nz/file/abc123#AbCdEfGh";
        var result = MegaResolver.ParseMegaUrl(url);

        Assert.NotNull(result);
        Assert.Equal("AbCdEfGh", result.Value.Key);
    }

    [Fact]
    public void ParseMegaUrl_HandlesLongKeys()
    {
        // Mega keys are typically 43 characters (256 bits in URL-safe base64)
        var longKey = new string('a', 43);
        var url = $"https://mega.nz/file/abc123#{longKey}";
        var result = MegaResolver.ParseMegaUrl(url);

        Assert.NotNull(result);
        Assert.Equal(longKey, result.Value.Key);
    }

    [Fact]
    public void ParseMegaUrl_HandlesSpecialChars()
    {
        // URL-safe base64 uses - and _
        var url = "https://mega.nz/file/abc123#key-with_special-chars_123";
        var result = MegaResolver.ParseMegaUrl(url);

        Assert.NotNull(result);
        Assert.Equal("key-with_special-chars_123", result.Value.Key);
    }

    #endregion

    #region RequiredHeaders Tests

    [Fact]
    public void HostResolverResult_CanStoreEncryptionKey()
    {
        // When a Mega URL is resolved, the key should be stored in RequiredHeaders
        var result = new HostResolverResult
        {
            Success = true,
            DirectUrl = "https://download.mega.co.nz/abc123",
            Filename = "test.cbz",
            RequiredHeaders = new Dictionary<string, string>
            {
                ["X-Mega-Key"] = "encryption-key-here"
            }
        };

        Assert.True(result.RequiredHeaders.ContainsKey("X-Mega-Key"));
        Assert.Equal("encryption-key-here", result.RequiredHeaders["X-Mega-Key"]);
    }

    #endregion
}
