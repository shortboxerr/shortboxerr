using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Dropbox file sharing links.
/// Converts share URLs to direct download URLs by modifying the dl parameter.
/// </summary>
public partial class DropboxResolver : BaseHostResolver
{
    public DropboxResolver(ILogger<DropboxResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "Dropbox";
    public override string DisplayName => "Dropbox";
    public override IReadOnlyList<string> SupportedHosts => new[] { "dropbox.com", "www.dropbox.com", "dl.dropbox.com", "dl.dropboxusercontent.com" };
    public override int Priority => 5;

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Dropbox URL: {Url}", url);

        try
        {
            // Convert share URL to direct download URL
            var directUrl = ConvertToDirectDownload(url);
            if (string.IsNullOrEmpty(directUrl))
            {
                Logger?.LogWarning("Failed to convert Dropbox URL to direct download: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not convert Dropbox share link to direct download URL"
                );
            }

            // Extract filename from URL
            var filename = ExtractFilename(url);

            // Verify the link is accessible with HEAD request
            using var client = CreateHttpClient(options);
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, directUrl);
            using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!headResponse.IsSuccessStatusCode)
            {
                var reason = ClassifyHttpStatus(headResponse.StatusCode);

                // Handle common Dropbox errors
                if (headResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return HostResolverResult.Failed(
                        HostResolverFailureReason.FileNotFound,
                        "File not found or share link has expired"
                    );
                }

                if (headResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return HostResolverResult.Failed(
                        HostResolverFailureReason.AuthenticationRequired,
                        "File requires authentication or is not publicly shared"
                    );
                }

                Logger?.LogWarning("Dropbox HEAD request failed: HTTP {StatusCode}", (int)headResponse.StatusCode);
                return HostResolverResult.Failed(reason, $"HTTP {(int)headResponse.StatusCode}");
            }

            // Get metadata from response
            var fileSize = headResponse.Content.Headers.ContentLength;
            var resolvedFilename = ExtractFilenameFromContentDisposition(headResponse) ?? filename;
            var contentType = headResponse.Content.Headers.ContentType?.MediaType;

            Logger?.LogDebug("Dropbox resolved: {Filename}, Size: {Size}", resolvedFilename, fileSize);

            return new HostResolverResult
            {
                Success = true,
                DirectUrl = directUrl,
                Filename = resolvedFilename,
                FileSize = fileSize,
                ContentType = contentType,
                FailureReason = HostResolverFailureReason.None
            };
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Dropbox URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    /// <summary>
    /// Converts a Dropbox share URL to a direct download URL.
    /// </summary>
    /// <remarks>
    /// Dropbox share URLs can be converted to direct downloads by:
    /// 1. Changing www.dropbox.com to dl.dropboxusercontent.com
    /// 2. Or setting dl=1 query parameter
    /// </remarks>
    internal static string? ConvertToDirectDownload(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);

            // Check if it's already a direct download URL
            if (uri.Host.Equals("dl.dropboxusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            // For standard share links, set dl=1 to force download
            // This works for: www.dropbox.com/s/{id}/{filename}?dl=0
            // And for: www.dropbox.com/scl/fi/{id}/{filename}?rlkey={key}&dl=0
            query["dl"] = "1";

            var uriBuilder = new UriBuilder(uri)
            {
                Query = query.ToString()
            };

            return uriBuilder.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts filename from Dropbox URL path.
    /// </summary>
    internal static string? ExtractFilename(string url)
    {
        try
        {
            // Pattern 1: /s/{id}/{filename}
            var match = ShareLinkFilenamePattern().Match(url);
            if (match.Success)
            {
                var filename = match.Groups["filename"].Value;
                // Remove query string if present
                var queryIndex = filename.IndexOf('?');
                if (queryIndex > 0)
                {
                    filename = filename[..queryIndex];
                }
                return Uri.UnescapeDataString(filename);
            }

            // Pattern 2: /scl/fi/{id}/{filename}
            match = SclLinkFilenamePattern().Match(url);
            if (match.Success)
            {
                var filename = match.Groups["filename"].Value;
                var queryIndex = filename.IndexOf('?');
                if (queryIndex > 0)
                {
                    filename = filename[..queryIndex];
                }
                return Uri.UnescapeDataString(filename);
            }

            // Fallback: try to get filename from URL path
            var uri = new Uri(url);
            var pathFilename = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(pathFilename) ? null : Uri.UnescapeDataString(pathFilename);
        }
        catch
        {
            return null;
        }
    }

    // /s/{id}/{filename}
    [GeneratedRegex(@"/s/[^/]+/(?<filename>[^/?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ShareLinkFilenamePattern();

    // /scl/fi/{id}/{filename}
    [GeneratedRegex(@"/scl/fi/[^/]+/(?<filename>[^/?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SclLinkFilenamePattern();
}
