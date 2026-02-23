using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Rapidgator file hosting links.
/// Supports premium account authentication via API.
/// Free tier access has significant limitations (wait times, speed limits).
/// </summary>
public partial class RapidgatorResolver : BaseHostResolver
{
    private const string ApiBaseUrl = "https://rapidgator.net/api/v2";

    public RapidgatorResolver(ILogger<RapidgatorResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "rapidgator";
    public override string DisplayName => "Rapidgator";
    public override IReadOnlyList<string> SupportedHosts => new[]
    {
        "rapidgator.net",
        "rapidgator.asia",
        "rg.to"
    };
    public override int Priority => 15; // Lower priority due to premium requirement

    public override async Task<HostResolverResult> ResolveAsync(
        string url,
        HostResolverOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Rapidgator URL: {Url}", url);

        try
        {
            // Extract file ID from URL
            var fileId = ExtractFileId(url);
            if (string.IsNullOrEmpty(fileId))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not extract file ID from Rapidgator URL"
                );
            }

            // Try premium API if credentials provided
            if (options.Credentials != null &&
                (!string.IsNullOrEmpty(options.Credentials.Username) || !string.IsNullOrEmpty(options.Credentials.ApiKey)))
            {
                return await ResolveWithPremiumAsync(fileId, options, cancellationToken);
            }

            // Fall back to page scraping for file info (free users can't download)
            return await ResolveFreeAsync(url, fileId, options, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Rapidgator URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    private async Task<HostResolverResult> ResolveWithPremiumAsync(
        string fileId,
        HostResolverOptions options,
        CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(options);

        // Step 1: Login and get session token
        string? sessionId = null;

        if (!string.IsNullOrEmpty(options.Credentials?.ApiKey))
        {
            // Use API key directly
            sessionId = options.Credentials.ApiKey;
        }
        else if (!string.IsNullOrEmpty(options.Credentials?.Username) &&
                 !string.IsNullOrEmpty(options.Credentials?.Password))
        {
            // Login with username/password
            var loginUrl = $"{ApiBaseUrl}/user/login?login={Uri.EscapeDataString(options.Credentials.Username)}&password={Uri.EscapeDataString(options.Credentials.Password)}";

            using var loginResponse = await client.GetAsync(loginUrl, cancellationToken);
            if (!loginResponse.IsSuccessStatusCode)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "Rapidgator login failed"
                );
            }

            var loginJson = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            sessionId = ExtractSessionId(loginJson);

            if (string.IsNullOrEmpty(sessionId))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "Could not obtain Rapidgator session token"
                );
            }
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.AuthenticationRequired,
                "Premium credentials required for Rapidgator"
            );
        }

        // Step 2: Get file info
        var infoUrl = $"{ApiBaseUrl}/file/info?file_id={fileId}&token={sessionId}";
        using var infoResponse = await client.GetAsync(infoUrl, cancellationToken);

        if (!infoResponse.IsSuccessStatusCode)
        {
            var reason = ClassifyHttpStatus(infoResponse.StatusCode);
            return HostResolverResult.Failed(reason, $"Failed to get file info: HTTP {(int)infoResponse.StatusCode}");
        }

        var infoJson = await infoResponse.Content.ReadAsStringAsync(cancellationToken);
        var fileInfo = ParseFileInfo(infoJson);

        if (fileInfo == null)
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File not found on Rapidgator"
            );
        }

        // Step 3: Get download link
        var downloadUrl = $"{ApiBaseUrl}/file/download?file_id={fileId}&token={sessionId}";
        using var downloadResponse = await client.GetAsync(downloadUrl, cancellationToken);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            var reason = ClassifyHttpStatus(downloadResponse.StatusCode);
            return HostResolverResult.Failed(reason, $"Failed to get download link: HTTP {(int)downloadResponse.StatusCode}");
        }

        var downloadJson = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
        var directUrl = ExtractDownloadUrl(downloadJson);

        if (string.IsNullOrEmpty(directUrl))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.ParseError,
                "Could not extract download URL from Rapidgator API response"
            );
        }

        Logger?.LogDebug("Rapidgator resolved (premium): {Filename}, Size: {Size}",
            fileInfo.Value.Filename, fileInfo.Value.Size);

        return new HostResolverResult
        {
            Success = true,
            DirectUrl = directUrl,
            Filename = fileInfo.Value.Filename,
            FileSize = fileInfo.Value.Size,
            UrlExpiry = TimeSpan.FromHours(24) // Rapidgator links typically expire after 24h
        };
    }

    private async Task<HostResolverResult> ResolveFreeAsync(
        string url,
        string fileId,
        HostResolverOptions options,
        CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(options);

        // Fetch the file page to get metadata
        using var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var reason = ClassifyHttpStatus(response.StatusCode);
            return HostResolverResult.Failed(reason, $"HTTP {(int)response.StatusCode}");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        // Check for error states
        if (html.Contains("File not found") || html.Contains("404"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File not found on Rapidgator"
            );
        }

        if (html.Contains("File has been deleted"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File has been deleted from Rapidgator"
            );
        }

        // Extract file metadata
        var filename = ExtractFilenameFromPage(html);
        var fileSize = ExtractFileSizeFromPage(html);

        // For free users, we can only provide metadata, not a direct download link
        return HostResolverResult.Failed(
            HostResolverFailureReason.AuthenticationRequired,
            $"Premium account required. File: {filename}, Size: {FormatSize(fileSize)}"
        );
    }

    internal static string? ExtractFileId(string url)
    {
        // Patterns:
        // https://rapidgator.net/file/abc123/filename.zip
        // https://rg.to/file/abc123
        var match = FileIdPattern().Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }

    internal static string? ExtractSessionId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("response", out var response) &&
                response.TryGetProperty("session_id", out var sessionId))
            {
                return sessionId.GetString();
            }
            // Also try token format
            if (doc.RootElement.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("token", out var token))
            {
                return token.GetString();
            }
        }
        catch { }
        return null;
    }

    internal static (string Filename, long Size)? ParseFileInfo(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("response", out var response) &&
                response.TryGetProperty("file", out var file))
            {
                var filename = file.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
                var size = file.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                return (filename, size);
            }
        }
        catch { }
        return null;
    }

    internal static string? ExtractDownloadUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("response", out var response) &&
                response.TryGetProperty("download_url", out var url))
            {
                return url.GetString();
            }
            // Alternative path
            if (doc.RootElement.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("url", out var directUrl))
            {
                return directUrl.GetString();
            }
        }
        catch { }
        return null;
    }

    internal static string? ExtractFilenameFromPage(string html)
    {
        var match = FilenamePattern().Match(html);
        return match.Success ? HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim()) : null;
    }

    internal static long? ExtractFileSizeFromPage(string html)
    {
        var match = FileSizePagePattern().Match(html);
        if (match.Success && decimal.TryParse(match.Groups["size"].Value, out var size))
        {
            var unit = match.Groups["unit"].Value.ToUpperInvariant();
            return unit switch
            {
                "B" => (long)size,
                "KB" => (long)(size * 1024),
                "MB" => (long)(size * 1024 * 1024),
                "GB" => (long)(size * 1024 * 1024 * 1024),
                _ => null
            };
        }
        return null;
    }

    private static string FormatSize(long? bytes)
    {
        if (!bytes.HasValue) return "unknown size";
        var size = bytes.Value;
        string[] units = { "B", "KB", "MB", "GB" };
        var unitIndex = 0;
        double displaySize = size;
        while (displaySize >= 1024 && unitIndex < units.Length - 1)
        {
            displaySize /= 1024;
            unitIndex++;
        }
        return $"{displaySize:F2} {units[unitIndex]}";
    }

    [GeneratedRegex(@"(?:rapidgator\.net|rapidgator\.asia|rg\.to)/file/(?<id>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdPattern();

    [GeneratedRegex(@"class=[""']file-name[""'][^>]*>(?<filename>[^<]+)<", RegexOptions.IgnoreCase)]
    private static partial Regex FilenamePattern();

    [GeneratedRegex(@"File\s+size:\s*(?<size>\d+(?:\.\d+)?)\s*(?<unit>B|KB|MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex FileSizePagePattern();
}
