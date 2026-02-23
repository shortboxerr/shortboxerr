using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Uploaded.net (ul.to) file hosting links.
/// Supports premium account authentication.
/// Free tier access has significant limitations (wait times, speed limits, CAPTCHA).
/// </summary>
public partial class UploadedResolver : BaseHostResolver
{
    private const string ApiBaseUrl = "https://uploaded.net/api";

    public UploadedResolver(ILogger<UploadedResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "uploaded";
    public override string DisplayName => "Uploaded.net";
    public override IReadOnlyList<string> SupportedHosts => new[]
    {
        "uploaded.net",
        "uploaded.to",
        "ul.to"
    };
    public override int Priority => 16; // Lower priority due to premium requirement

    public override async Task<HostResolverResult> ResolveAsync(
        string url,
        HostResolverOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Uploaded.net URL: {Url}", url);

        try
        {
            // Extract file ID from URL
            var fileId = ExtractFileId(url);
            if (string.IsNullOrEmpty(fileId))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not extract file ID from Uploaded.net URL"
                );
            }

            // Try premium API if credentials provided
            if (options.Credentials != null &&
                (!string.IsNullOrEmpty(options.Credentials.Username) || !string.IsNullOrEmpty(options.Credentials.ApiKey)))
            {
                return await ResolveWithPremiumAsync(fileId, url, options, cancellationToken);
            }

            // Fall back to page scraping for file info (free users have limited access)
            return await ResolveFreeAsync(url, fileId, options, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Uploaded.net URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    private async Task<HostResolverResult> ResolveWithPremiumAsync(
        string fileId,
        string originalUrl,
        HostResolverOptions options,
        CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(options);

        // Step 1: Authenticate
        string? authToken = null;

        if (!string.IsNullOrEmpty(options.Credentials?.ApiKey))
        {
            authToken = options.Credentials.ApiKey;
        }
        else if (!string.IsNullOrEmpty(options.Credentials?.Username) &&
                 !string.IsNullOrEmpty(options.Credentials?.Password))
        {
            // Login with username/password
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", options.Credentials.Username),
                new KeyValuePair<string, string>("pw", options.Credentials.Password)
            });

            using var loginResponse = await client.PostAsync($"{ApiBaseUrl}/user", loginData, cancellationToken);

            if (!loginResponse.IsSuccessStatusCode)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "Uploaded.net login failed"
                );
            }

            var loginResult = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            authToken = ExtractAuthToken(loginResult);

            if (string.IsNullOrEmpty(authToken))
            {
                // Check for error messages
                if (loginResult.Contains("invalid") || loginResult.Contains("err"))
                {
                    return HostResolverResult.Failed(
                        HostResolverFailureReason.AuthenticationRequired,
                        "Invalid Uploaded.net credentials"
                    );
                }

                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "Could not obtain Uploaded.net authentication token"
                );
            }
        }

        if (string.IsNullOrEmpty(authToken))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.AuthenticationRequired,
                "Premium credentials required for Uploaded.net"
            );
        }

        // Step 2: Get file info
        var fileInfoData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("apikey", authToken),
            new KeyValuePair<string, string>("id_0", fileId)
        });

        using var fileInfoResponse = await client.PostAsync($"{ApiBaseUrl}/filemultiple", fileInfoData, cancellationToken);

        string? filename = null;
        long? fileSize = null;

        if (fileInfoResponse.IsSuccessStatusCode)
        {
            var fileInfoResult = await fileInfoResponse.Content.ReadAsStringAsync(cancellationToken);
            var info = ParseFileInfo(fileInfoResult);
            if (info != null)
            {
                filename = info.Value.Filename;
                fileSize = info.Value.Size;
            }
        }

        // Step 3: Get download link
        var downloadData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("apikey", authToken),
            new KeyValuePair<string, string>("file", fileId)
        });

        using var downloadResponse = await client.PostAsync($"{ApiBaseUrl}/download/retrieve", downloadData, cancellationToken);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            // Try alternate endpoint
            var directUrl = await TryAlternateDownloadAsync(fileId, authToken, client, cancellationToken);
            if (!string.IsNullOrEmpty(directUrl))
            {
                return new HostResolverResult
                {
                    Success = true,
                    DirectUrl = directUrl,
                    Filename = filename,
                    FileSize = fileSize,
                    UrlExpiry = TimeSpan.FromHours(12)
                };
            }

            var reason = ClassifyHttpStatus(downloadResponse.StatusCode);
            return HostResolverResult.Failed(reason, $"Failed to get download link: HTTP {(int)downloadResponse.StatusCode}");
        }

        var downloadResult = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
        var downloadUrl = ExtractDownloadUrl(downloadResult);

        if (string.IsNullOrEmpty(downloadUrl))
        {
            // Check for specific error conditions
            if (downloadResult.Contains("offline") || downloadResult.Contains("Offline"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File is offline on Uploaded.net"
                );
            }

            return HostResolverResult.Failed(
                HostResolverFailureReason.ParseError,
                "Could not extract download URL from Uploaded.net API response"
            );
        }

        Logger?.LogDebug("Uploaded.net resolved (premium): {Filename}, Size: {Size}", filename, fileSize);

        return new HostResolverResult
        {
            Success = true,
            DirectUrl = downloadUrl,
            Filename = filename,
            FileSize = fileSize,
            UrlExpiry = TimeSpan.FromHours(12) // Uploaded links expire relatively quickly
        };
    }

    private async Task<string?> TryAlternateDownloadAsync(
        string fileId,
        string authToken,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try direct link generation
            var linkData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("apikey", authToken),
                new KeyValuePair<string, string>("id", fileId)
            });

            using var response = await client.PostAsync($"{ApiBaseUrl}/link", linkData, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                return ExtractDownloadUrl(result);
            }
        }
        catch
        {
            // Ignore and return null
        }

        return null;
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
        if (html.Contains("File not found") || html.Contains("404") || html.Contains("does not exist"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File not found on Uploaded.net"
            );
        }

        if (html.Contains("was deleted") || html.Contains("has been removed"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File has been deleted from Uploaded.net"
            );
        }

        if (html.Contains("Maintenance") || html.Contains("maintenance"))
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.HostUnavailable,
                "Uploaded.net is under maintenance"
            );
        }

        // Extract file metadata
        var filename = ExtractFilenameFromPage(html);
        var fileSize = ExtractFileSizeFromPage(html);

        // For free users, we can only provide metadata
        // Free downloads require CAPTCHA and wait times
        return HostResolverResult.Failed(
            HostResolverFailureReason.AuthenticationRequired,
            $"Premium account required. File: {filename ?? fileId}, Size: {FormatSize(fileSize)}"
        );
    }

    internal static string? ExtractFileId(string url)
    {
        // Patterns:
        // https://uploaded.net/file/abc123
        // https://ul.to/abc123
        // https://uploaded.to/file/abc123/filename.zip
        var match = FileIdPattern().Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }

    internal static string? ExtractAuthToken(string response)
    {
        // Response formats:
        // "access_token":"xxx"
        // apikey,xxx
        // token:xxx

        // Try JSON format
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("access_token", out var token))
                return token.GetString();
            if (doc.RootElement.TryGetProperty("apikey", out var apikey))
                return apikey.GetString();
            if (doc.RootElement.TryGetProperty("token", out var t))
                return t.GetString();
        }
        catch { }

        // Try CSV format (apikey,value)
        if (response.Contains(',') && !response.Contains('{'))
        {
            var parts = response.Trim().Split(',');
            if (parts.Length >= 2)
                return parts[1].Trim();
        }

        // Try key:value format
        var match = TokenPattern().Match(response);
        if (match.Success)
            return match.Groups["token"].Value;

        return null;
    }

    internal static (string Filename, long Size)? ParseFileInfo(string response)
    {
        try
        {
            // Format: filename,id,size,status or JSON
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var file = doc.RootElement[0];
                var filename = file.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
                var size = file.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                return (filename, size);
            }
        }
        catch { }

        // Try CSV format
        var lines = response.Trim().Split('\n');
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var filename = parts[0].Trim();
                if (long.TryParse(parts[2].Trim(), out var size))
                    return (filename, size);
            }
        }

        return null;
    }

    internal static string? ExtractDownloadUrl(string response)
    {
        // Try JSON format
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("url", out var url))
                return url.GetString();
            if (doc.RootElement.TryGetProperty("download_url", out var dlUrl))
                return dlUrl.GetString();
            if (doc.RootElement.TryGetProperty("link", out var link))
                return link.GetString();
        }
        catch { }

        // Try plain URL format
        if (response.Trim().StartsWith("http"))
            return response.Trim().Split('\n')[0].Trim();

        // Try to find URL in response
        var match = DownloadUrlPattern().Match(response);
        if (match.Success)
            return match.Groups["url"].Value;

        return null;
    }

    internal static string? ExtractFilenameFromPage(string html)
    {
        // Pattern 1: class="file_name"
        var match = FilenameClassPattern().Match(html);
        if (match.Success)
            return HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim());

        // Pattern 2: id="filename"
        match = FilenameIdPattern().Match(html);
        if (match.Success)
            return HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim());

        // Pattern 3: <title>Download filename - Uploaded
        match = TitlePattern().Match(html);
        if (match.Success)
            return HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim());

        return null;
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

    [GeneratedRegex(@"(?:uploaded\.(?:net|to)|ul\.to)/(?:file/)?(?<id>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdPattern();

    [GeneratedRegex(@"(?:token|apikey|access_token)[""']?\s*[:=]\s*[""']?(?<token>[a-zA-Z0-9_-]+)[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"(?<url>https?://[^\s""'<>]+uploaded[^\s""'<>]*)", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadUrlPattern();

    [GeneratedRegex(@"class=[""'][^""']*file_?name[^""']*[""'][^>]*>(?<filename>[^<]+)<", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameClassPattern();

    [GeneratedRegex(@"id=[""']filename[""'][^>]*>(?<filename>[^<]+)<", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameIdPattern();

    [GeneratedRegex(@"<title>\s*Download\s+(?<filename>[^-<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"(?:Size|Filesize|File\s+size)[:\s]*(?<size>\d+(?:[.,]\d+)?)\s*(?<unit>B|KB|MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex FileSizePagePattern();
}
