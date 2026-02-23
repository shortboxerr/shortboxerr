using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Mega.nz file hosting links.
/// Handles Mega's encrypted file links by parsing the URL, extracting the file key,
/// and interacting with the Mega API to get file metadata and download URLs.
/// 
/// Note: Mega uses client-side encryption. Files are encrypted with AES-128-CTR.
/// The encryption key is embedded in the URL fragment (after #!) and never sent to Mega servers.
/// </summary>
public partial class MegaResolver : BaseHostResolver
{
    private const string MegaApiUrl = "https://g.api.mega.co.nz/cs";

    public MegaResolver(ILogger<MegaResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "mega";
    public override string DisplayName => "Mega.nz";
    public override IReadOnlyList<string> SupportedHosts => new[]
    {
        "mega.nz",
        "mega.co.nz"
    };
    public override int Priority => 1; // High priority - Mega is reliable and fast

    public override async Task<HostResolverResult> ResolveAsync(
        string url,
        HostResolverOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Mega.nz URL: {Url}", url);

        try
        {
            // Parse the URL to extract file ID and key
            var parsedLink = ParseMegaUrl(url);
            if (parsedLink == null)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not parse Mega.nz URL format"
                );
            }

            // Get file attributes from Mega API
            var fileInfo = await GetFileAttributesAsync(parsedLink.Value.FileId, options, cancellationToken);
            if (fileInfo == null)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File not found or link has expired"
                );
            }

            // Decrypt file attributes using the key from URL
            var attributes = DecryptFileAttributes(fileInfo.Value.EncryptedAttributes, parsedLink.Value.Key);
            if (attributes == null)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not decrypt file attributes"
                );
            }

            // Build the result with download URL and metadata
            // Note: The actual download requires decryption on the client side
            // We return the Mega download URL which streams encrypted content
            var downloadUrl = fileInfo.Value.DownloadUrl;

            Logger?.LogDebug("Mega.nz resolved: {Filename}, Size: {Size}", attributes.Value.Name, fileInfo.Value.Size);

            return new HostResolverResult
            {
                Success = true,
                DirectUrl = downloadUrl,
                Filename = attributes.Value.Name,
                FileSize = fileInfo.Value.Size,
                FailureReason = HostResolverFailureReason.None,
                RequiredHeaders = new Dictionary<string, string>
                {
                    ["X-Mega-Key"] = parsedLink.Value.Key // Store key for decryption
                }
            };
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("Too Many"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.RateLimited,
                "Mega.nz rate limit exceeded. Please try again later."
            );
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Mega.nz URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    public override async Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var parsedLink = ParseMegaUrl(url);
            if (parsedLink == null)
            {
                return new HostVerifyResult
                {
                    IsAvailable = false,
                    FailureReason = HostResolverFailureReason.ParseError,
                    Message = "Invalid Mega.nz URL format"
                };
            }

            var fileInfo = await GetFileAttributesAsync(parsedLink.Value.FileId, new HostResolverOptions(), cancellationToken);
            if (fileInfo == null)
            {
                return new HostVerifyResult
                {
                    IsAvailable = false,
                    FailureReason = HostResolverFailureReason.FileNotFound,
                    Message = "File not found or link has expired"
                };
            }

            var attributes = DecryptFileAttributes(fileInfo.Value.EncryptedAttributes, parsedLink.Value.Key);

            return new HostVerifyResult
            {
                IsAvailable = true,
                Filename = attributes?.Name,
                FileSize = fileInfo.Value.Size
            };
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to verify Mega.nz URL: {Url}", url);
            return new HostVerifyResult
            {
                IsAvailable = false,
                FailureReason = ClassifyException(ex),
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Parses a Mega.nz URL to extract the file ID and encryption key.
    /// Supports both old format (mega.nz/#!fileId!key) and new format (mega.nz/file/fileId#key).
    /// </summary>
    internal static (string FileId, string Key)? ParseMegaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // New format: https://mega.nz/file/fileId#key
        var newFormatMatch = NewFormatPattern().Match(url);
        if (newFormatMatch.Success)
        {
            return (newFormatMatch.Groups["id"].Value, newFormatMatch.Groups["key"].Value);
        }

        // Old format: https://mega.nz/#!fileId!key or https://mega.co.nz/#!fileId!key
        var oldFormatMatch = OldFormatPattern().Match(url);
        if (oldFormatMatch.Success)
        {
            return (oldFormatMatch.Groups["id"].Value, oldFormatMatch.Groups["key"].Value);
        }

        // Folder links (not fully supported yet, but parse them)
        var folderMatch = FolderPattern().Match(url);
        if (folderMatch.Success)
        {
            // For folders, we'd need additional handling
            return null; // Folder support deferred
        }

        return null;
    }

    /// <summary>
    /// Extracts just the file ID from a Mega URL.
    /// </summary>
    internal static string? ExtractFileId(string url)
    {
        var parsed = ParseMegaUrl(url);
        return parsed?.FileId;
    }

    /// <summary>
    /// Gets file attributes from the Mega API.
    /// </summary>
    private async Task<(string DownloadUrl, long Size, string EncryptedAttributes)?> GetFileAttributesAsync(
        string fileId,
        HostResolverOptions options,
        CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(options);

        // Mega API request for file info (g = get, p = public)
        var request = new[]
        {
            new
            {
                a = "g",  // action: get
                g = 1,    // include download URL
                p = fileId // public file handle
            }
        };

        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"{MegaApiUrl}?id=0", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Logger?.LogWarning("Mega API request failed: HTTP {StatusCode}", (int)response.StatusCode);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        // Parse response - it's an array with one element
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Check for error response (negative number)
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstElement = root[0];

                // Error codes are negative integers
                if (firstElement.ValueKind == JsonValueKind.Number)
                {
                    var errorCode = firstElement.GetInt32();
                    Logger?.LogDebug("Mega API returned error code: {ErrorCode}", errorCode);
                    return null; // File not found or other error
                }

                // Success response contains file info
                if (firstElement.ValueKind == JsonValueKind.Object)
                {
                    var downloadUrl = firstElement.TryGetProperty("g", out var g) ? g.GetString() : null;
                    var size = firstElement.TryGetProperty("s", out var s) ? s.GetInt64() : 0;
                    var encryptedAttr = firstElement.TryGetProperty("at", out var at) ? at.GetString() : null;

                    if (downloadUrl != null && encryptedAttr != null)
                    {
                        return (downloadUrl, size, encryptedAttr);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            Logger?.LogWarning(ex, "Failed to parse Mega API response");
        }

        return null;
    }

    /// <summary>
    /// Decrypts Mega file attributes using the key from the URL.
    /// Mega uses AES-CBC with a zero IV for attribute encryption.
    /// </summary>
    internal static (string Name, string? Fingerprint)? DecryptFileAttributes(string encryptedBase64, string keyBase64)
    {
        try
        {
            // Decode the key from URL-safe Base64
            var keyBytes = Base64UrlDecode(keyBase64);
            if (keyBytes == null || keyBytes.Length < 32)
                return null;

            // Mega file keys are 32 bytes (256 bits) but AES uses 16-byte key
            // XOR the two halves together to get the actual AES key
            var aesKey = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                aesKey[i] = (byte)(keyBytes[i] ^ keyBytes[i + 16]);
            }

            // Decode encrypted attributes
            var encryptedBytes = Base64UrlDecode(encryptedBase64);
            if (encryptedBytes == null)
                return null;

            // Decrypt using AES-CBC with zero IV
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = new byte[16]; // Zero IV
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            // Find the JSON content - it starts with "MEGA{" and ends with "}"
            var decryptedStr = Encoding.UTF8.GetString(decryptedBytes);
            var jsonStart = decryptedStr.IndexOf('{');
            var jsonEnd = decryptedStr.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
                return null;

            var jsonContent = decryptedStr.Substring(jsonStart, jsonEnd - jsonStart + 1);

            // Parse the JSON attributes
            using var doc = JsonDocument.Parse(jsonContent);
            var name = doc.RootElement.TryGetProperty("n", out var n) ? n.GetString() : null;
            var fingerprint = doc.RootElement.TryGetProperty("c", out var c) ? c.GetString() : null;

            if (name == null)
                return null;

            return (name, fingerprint);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes a URL-safe Base64 string (Mega uses custom encoding).
    /// </summary>
    internal static byte[]? Base64UrlDecode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        // Mega uses URL-safe Base64 without padding
        // Replace URL-safe characters with standard Base64
        var base64 = input
            .Replace("-", "+")
            .Replace("_", "/")
            .Replace(",", ""); // Mega sometimes uses comma as separator

        // Add padding if needed
        var padding = (4 - base64.Length % 4) % 4;
        base64 += new string('=', padding);

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Encodes bytes to URL-safe Base64 (Mega format).
    /// </summary>
    internal static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    // Regex patterns for Mega URL formats

    [GeneratedRegex(@"mega\.(?:nz|co\.nz)/file/(?<id>[a-zA-Z0-9_-]+)#(?<key>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NewFormatPattern();

    [GeneratedRegex(@"mega\.(?:nz|co\.nz)/#!(?<id>[a-zA-Z0-9_-]+)!(?<key>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex OldFormatPattern();

    [GeneratedRegex(@"mega\.(?:nz|co\.nz)/(?:#F!|folder/)(?<id>[a-zA-Z0-9_-]+)(?:[#!](?<key>[a-zA-Z0-9_-]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex FolderPattern();
}
