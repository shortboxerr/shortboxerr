using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Pixeldrain file hosting links.
/// Pixeldrain provides a simple API for direct downloads.
/// </summary>
public partial class PixeldrainResolver : BaseHostResolver
{
    private const string PixeldrainApiBase = "https://pixeldrain.com/api/file";
    private const string PixeldrainDownloadBase = "https://pixeldrain.com/api/file/{0}";

    public PixeldrainResolver(ILogger<PixeldrainResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "Pixeldrain";
    public override string DisplayName => "Pixeldrain";
    public override IReadOnlyList<string> SupportedHosts => new[] { "pixeldrain.com" };
    public override int Priority => 3;

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Pixeldrain URL: {Url}", url);

        try
        {
            // Extract file ID from URL
            var fileId = ExtractFileId(url);
            if (string.IsNullOrEmpty(fileId))
            {
                Logger?.LogWarning("Failed to extract Pixeldrain file ID from URL: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not extract file ID from Pixeldrain URL"
                );
            }

            Logger?.LogDebug("Extracted Pixeldrain file ID: {FileId}", fileId);

            // Get file info from API
            using var client = CreateHttpClient(options);
            var infoUrl = $"{PixeldrainApiBase}/{fileId}/info";
            
            using var response = await client.GetAsync(infoUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = ClassifyHttpStatus(response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return HostResolverResult.Failed(
                        HostResolverFailureReason.FileNotFound,
                        "File not found on Pixeldrain"
                    );
                }
                
                Logger?.LogWarning("Pixeldrain API request failed: HTTP {StatusCode}", (int)response.StatusCode);
                return HostResolverResult.Failed(reason, $"HTTP {(int)response.StatusCode}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var fileInfo = JsonSerializer.Deserialize<PixeldrainFileInfo>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (fileInfo == null)
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Failed to parse Pixeldrain API response"
                );
            }

            // Check availability
            if (fileInfo.Availability == "file_not_found")
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File not found on Pixeldrain"
                );
            }

            // Build direct download URL
            var directUrl = string.Format(PixeldrainDownloadBase, fileId);

            Logger?.LogDebug("Pixeldrain resolved: {Filename}, Size: {Size}", fileInfo.Name, fileInfo.Size);

            return new HostResolverResult
            {
                Success = true,
                DirectUrl = directUrl,
                Filename = fileInfo.Name,
                FileSize = fileInfo.Size,
                ContentType = fileInfo.MimeType,
                FailureReason = HostResolverFailureReason.None
            };
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (JsonException ex)
        {
            Logger?.LogWarning(ex, "Failed to parse Pixeldrain response");
            return HostResolverResult.Failed(HostResolverFailureReason.ParseError, "Invalid API response format");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Pixeldrain URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    public override async Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await ResolveAsync(url, new HostResolverOptions { TimeoutSeconds = 15 }, cancellationToken);
        
        return new HostVerifyResult
        {
            IsAvailable = result.Success,
            Filename = result.Filename,
            FileSize = result.FileSize,
            Message = result.ErrorMessage,
            FailureReason = result.FailureReason
        };
    }

    /// <summary>
    /// Extracts the file ID from various Pixeldrain URL formats.
    /// Supports:
    /// - https://pixeldrain.com/u/abc123
    /// - https://pixeldrain.com/u/abc123?download
    /// - https://pixeldrain.com/api/file/abc123
    /// </summary>
    internal static string? ExtractFileId(string url)
    {
        // Pattern 1: /u/fileId
        var match = FileIdFromUPattern().Match(url);
        if (match.Success)
        {
            return match.Groups["fileId"].Value;
        }

        // Pattern 2: /api/file/fileId
        match = FileIdFromApiPattern().Match(url);
        if (match.Success)
        {
            return match.Groups["fileId"].Value;
        }

        // Pattern 3: Just a file ID (8-12 alphanumeric chars)
        if (url.Length >= 8 && url.Length <= 12 && url.All(char.IsLetterOrDigit))
        {
            return url;
        }

        return null;
    }

    [GeneratedRegex(@"pixeldrain\.com/u/(?<fileId>[A-Za-z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdFromUPattern();

    [GeneratedRegex(@"pixeldrain\.com/api/file/(?<fileId>[A-Za-z0-9]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdFromApiPattern();

    /// <summary>
    /// Pixeldrain API file info response.
    /// </summary>
    private class PixeldrainFileInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public long Size { get; set; }
        public string? MimeType { get; set; }
        public string? Availability { get; set; }
        public int Downloads { get; set; }
        public bool CanEdit { get; set; }
    }
}
