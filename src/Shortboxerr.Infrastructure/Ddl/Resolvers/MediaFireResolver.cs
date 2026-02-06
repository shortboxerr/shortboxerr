using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for MediaFire file hosting links.
/// Parses MediaFire share pages to extract direct download URLs.
/// </summary>
public partial class MediaFireResolver : BaseHostResolver
{
    public MediaFireResolver(ILogger<MediaFireResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "MediaFire";
    public override string DisplayName => "MediaFire";
    public override IReadOnlyList<string> SupportedHosts => new[] { "mediafire.com", "www.mediafire.com" };
    public override int Priority => 2;

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving MediaFire URL: {Url}", url);

        try
        {
            using var client = CreateHttpClient(options);

            // Fetch the share page
            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = ClassifyHttpStatus(response.StatusCode);
                Logger?.LogWarning("MediaFire page request failed: HTTP {StatusCode}", (int)response.StatusCode);
                return HostResolverResult.Failed(reason, $"HTTP {(int)response.StatusCode}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for error states
            if (html.Contains("File Removed") || html.Contains("This file is no longer available"))
            {
                Logger?.LogDebug("MediaFire file removed: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File has been removed from MediaFire"
                );
            }

            if (html.Contains("Invalid or Deleted File"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "Invalid or deleted file"
                );
            }

            if (html.Contains("Password Protected"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "File is password protected"
                );
            }

            // Extract download URL from the page
            var directUrl = ExtractDownloadUrl(html);
            if (string.IsNullOrEmpty(directUrl))
            {
                Logger?.LogWarning("Failed to extract MediaFire download URL from page: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not find download link on MediaFire page"
                );
            }

            // Extract file metadata
            var filename = ExtractFilename(html) ?? ExtractFilenameFromUrl(directUrl);
            var fileSize = ExtractFileSize(html);

            Logger?.LogDebug("MediaFire resolved: {Filename}, Size: {Size}", filename, fileSize);

            return new HostResolverResult
            {
                Success = true,
                DirectUrl = directUrl,
                Filename = filename,
                FileSize = fileSize,
                FailureReason = HostResolverFailureReason.None
            };
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving MediaFire URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    /// <summary>
    /// Extracts the direct download URL from MediaFire page HTML.
    /// </summary>
    internal static string? ExtractDownloadUrl(string html)
    {
        // Pattern 1: aria-label="Download file" href="..."
        var match = DownloadButtonPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        // Pattern 2: id="downloadButton" href="..."
        match = DownloadButtonIdPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        // Pattern 3: class="download_link" href="..."
        match = DownloadLinkClassPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        // Pattern 4: Direct link in JavaScript
        match = JavaScriptDownloadPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        return null;
    }

    /// <summary>
    /// Extracts filename from MediaFire page.
    /// </summary>
    internal static string? ExtractFilename(string html)
    {
        // Pattern 1: class="filename"
        var match = FilenameClassPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim());
        }

        // Pattern 2: meta og:title
        match = OgTitlePattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["title"].Value.Trim());
        }

        // Pattern 3: div title attribute
        match = DivTitlePattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["title"].Value.Trim());
        }

        return null;
    }

    /// <summary>
    /// Extracts file size from MediaFire page.
    /// </summary>
    internal static long? ExtractFileSize(string html)
    {
        // Pattern: "123.45 MB" or "1.2 GB"
        var match = FileSizePattern().Match(html);
        if (match.Success)
        {
            if (decimal.TryParse(match.Groups["size"].Value, out var size))
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
        }

        return null;
    }

    // Regex patterns for parsing MediaFire pages

    [GeneratedRegex(@"aria-label=[""']Download file[""'][^>]*href=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DownloadButtonPattern();

    [GeneratedRegex(@"id=[""']downloadButton[""'][^>]*href=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DownloadButtonIdPattern();

    [GeneratedRegex(@"class=[""'][^""']*download_link[^""']*[""'][^>]*href=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DownloadLinkClassPattern();

    [GeneratedRegex(@"window\.location\.href\s*=\s*[""'](?<url>https?://download[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptDownloadPattern();

    [GeneratedRegex(@"class=[""'][^""']*filename[^""']*[""'][^>]*>(?<filename>[^<]+)<", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameClassPattern();

    [GeneratedRegex(@"<meta\s+property=[""']og:title[""']\s+content=[""'](?<title>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    [GeneratedRegex(@"<div[^>]*title=[""'](?<title>[^""']+\.(cbz|cbr|zip|rar|pdf))[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DivTitlePattern();

    [GeneratedRegex(@"(?<size>\d+(?:\.\d+)?)\s*(?<unit>B|KB|MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex FileSizePattern();
}
