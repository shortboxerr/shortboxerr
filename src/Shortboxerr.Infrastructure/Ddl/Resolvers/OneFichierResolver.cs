using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for 1fichier file hosting links.
/// Parses 1fichier share pages to extract direct download URLs.
/// Note: Free users have wait times; premium accounts get instant downloads.
/// </summary>
public partial class OneFichierResolver : BaseHostResolver
{
    public OneFichierResolver(ILogger<OneFichierResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "1fichier";
    public override string DisplayName => "1fichier";
    public override IReadOnlyList<string> SupportedHosts => new[] { "1fichier.com", "1fichier.fr", "1fichier.info" };
    public override int Priority => 6;

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving 1fichier URL: {Url}", url);

        try
        {
            using var client = CreateHttpClient(options);

            // Step 1: Fetch the download page
            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = ClassifyHttpStatus(response.StatusCode);
                Logger?.LogWarning("1fichier page request failed: HTTP {StatusCode}", (int)response.StatusCode);
                return HostResolverResult.Failed(reason, $"HTTP {(int)response.StatusCode}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for error states
            if (html.Contains("File not found") || 
                html.Contains("The requested file could not be found") ||
                html.Contains("Bad link") ||
                html.Contains("fichier n'existe pas"))
            {
                Logger?.LogDebug("1fichier file not found: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File not found on 1fichier"
                );
            }

            if (html.Contains("This file has been removed"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.FileNotFound,
                    "File has been removed from 1fichier"
                );
            }

            if (html.Contains("Password"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "File is password protected"
                );
            }

            if (html.Contains("Access denied") || html.Contains("Premium only"))
            {
                return HostResolverResult.Failed(
                    HostResolverFailureReason.AuthenticationRequired,
                    "Premium account required for this file"
                );
            }

            // Check for rate limiting (free users have wait times)
            var waitTime = ExtractWaitTime(html);
            if (waitTime > 0)
            {
                Logger?.LogDebug("1fichier free tier wait required: {Seconds}s", waitTime);
                // For now, we don't wait - just report the URL needs a wait
                // A more sophisticated implementation would wait or return a special status
            }

            // Step 2: Extract the download URL
            // 1fichier uses a form POST to get the actual download link
            var directUrl = ExtractDirectDownloadUrl(html);
            
            if (string.IsNullOrEmpty(directUrl))
            {
                // Try to find the download form and generate the download URL
                directUrl = await TryGetDownloadUrlFromForm(url, html, client, cancellationToken);
            }

            if (string.IsNullOrEmpty(directUrl))
            {
                Logger?.LogWarning("Failed to extract 1fichier download URL from page: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not find download link on 1fichier page"
                );
            }

            // Extract file metadata
            var filename = ExtractFilename(html) ?? ExtractFilenameFromUrl(directUrl);
            var fileSize = ExtractFileSize(html);

            Logger?.LogDebug("1fichier resolved: {Filename}, Size: {Size}", filename, fileSize);

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
            Logger?.LogError(ex, "Unexpected error resolving 1fichier URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    /// <summary>
    /// Extracts wait time in seconds for free users.
    /// </summary>
    internal static int ExtractWaitTime(string html)
    {
        // Pattern: "Please wait <span>60</span> seconds" or similar
        var match = WaitTimePattern().Match(html);
        if (match.Success && int.TryParse(match.Groups["seconds"].Value, out var seconds))
        {
            return seconds;
        }

        // Alternative pattern: counter variable
        match = CounterPattern().Match(html);
        if (match.Success && int.TryParse(match.Groups["seconds"].Value, out seconds))
        {
            return seconds;
        }

        return 0;
    }

    /// <summary>
    /// Extracts direct download URL if already present on page.
    /// </summary>
    internal static string? ExtractDirectDownloadUrl(string html)
    {
        // Pattern 1: Direct link to .cz or .fr CDN
        var match = DirectLinkPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        // Pattern 2: Download button with href
        match = DownloadButtonPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["url"].Value);
        }

        return null;
    }

    /// <summary>
    /// Tries to get the download URL by submitting the download form.
    /// </summary>
    private async Task<string?> TryGetDownloadUrlFromForm(string originalUrl, string html, HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Check if this is a page with a download form
            var formMatch = DownloadFormPattern().Match(html);
            if (!formMatch.Success)
            {
                return null;
            }

            var formAction = formMatch.Groups["action"].Value;
            if (string.IsNullOrEmpty(formAction))
            {
                formAction = originalUrl;
            }
            else if (!formAction.StartsWith("http"))
            {
                var uri = new Uri(originalUrl);
                formAction = $"{uri.Scheme}://{uri.Host}{formAction}";
            }

            // Extract any hidden form fields
            var formData = new Dictionary<string, string>();
            
            var hiddenFields = HiddenFieldPattern().Matches(html);
            foreach (Match field in hiddenFields)
            {
                var name = field.Groups["name"].Value;
                var value = field.Groups["value"].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    formData[name] = HttpUtility.HtmlDecode(value);
                }
            }

            // Add common form fields
            if (!formData.ContainsKey("submit"))
            {
                formData["submit"] = "Download";
            }

            using var content = new FormUrlEncodedContent(formData);
            using var response = await client.PostAsync(formAction, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseHtml = await response.Content.ReadAsStringAsync(cancellationToken);
                return ExtractDirectDownloadUrl(responseHtml);
            }

            // Check if we got redirected to the download
            if (response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                response.StatusCode == System.Net.HttpStatusCode.Found)
            {
                var location = response.Headers.Location;
                if (location != null)
                {
                    return location.ToString();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger?.LogDebug(ex, "Failed to get download URL from form for {Url}", originalUrl);
            return null;
        }
    }

    /// <summary>
    /// Extracts filename from 1fichier page.
    /// </summary>
    internal static string? ExtractFilename(string html)
    {
        // Pattern 1: File name in specific class
        var match = FilenameClassPattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["filename"].Value.Trim());
        }

        // Pattern 2: File name in title tag
        match = TitlePattern().Match(html);
        if (match.Success)
        {
            var title = HttpUtility.HtmlDecode(match.Groups["title"].Value.Trim());
            // Title often contains " - Download" suffix
            var downloadIndex = title.IndexOf(" - Download", StringComparison.OrdinalIgnoreCase);
            if (downloadIndex > 0)
            {
                title = title[..downloadIndex];
            }
            return title;
        }

        // Pattern 3: og:title meta tag
        match = OgTitlePattern().Match(html);
        if (match.Success)
        {
            return HttpUtility.HtmlDecode(match.Groups["title"].Value.Trim());
        }

        return null;
    }

    /// <summary>
    /// Extracts file size from 1fichier page.
    /// </summary>
    internal static long? ExtractFileSize(string html)
    {
        // Pattern: "Size: 123.45 MB" or similar
        var match = FileSizePattern().Match(html);
        if (match.Success && decimal.TryParse(match.Groups["size"].Value, out var size))
        {
            var unit = match.Groups["unit"].Value.ToUpperInvariant();
            return unit switch
            {
                "B" => (long)size,
                "KB" or "KO" => (long)(size * 1024),
                "MB" or "MO" => (long)(size * 1024 * 1024),
                "GB" or "GO" => (long)(size * 1024 * 1024 * 1024),
                _ => null
            };
        }

        return null;
    }

    // Regex patterns for parsing 1fichier pages

    [GeneratedRegex(@"(?:Please wait|wait)\s*(?:<[^>]+>)?(?<seconds>\d+)(?:</[^>]+>)?\s*second", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WaitTimePattern();

    [GeneratedRegex(@"var\s+count\s*=\s*(?<seconds>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CounterPattern();

    [GeneratedRegex(@"href=[""'](?<url>https?://(?:cz|fr|cf|cdn)\d*\.1fichier\.com/[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DirectLinkPattern();

    [GeneratedRegex(@"<a[^>]*class=[""'][^""']*btn-dl[^""']*[""'][^>]*href=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadButtonPattern();

    [GeneratedRegex(@"<form[^>]*action=[""'](?<action>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadFormPattern();

    [GeneratedRegex(@"<input[^>]*type=[""']hidden[""'][^>]*name=[""'](?<name>[^""']+)[""'][^>]*value=[""'](?<value>[^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenFieldPattern();

    [GeneratedRegex(@"class=[""'][^""']*file[-_]?name[^""']*[""'][^>]*>(?<filename>[^<]+)<", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameClassPattern();

    [GeneratedRegex(@"<title>(?<title>[^<]+)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"<meta\s+property=[""']og:title[""']\s+content=[""'](?<title>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitlePattern();

    [GeneratedRegex(@"(?<size>\d+(?:[.,]\d+)?)\s*(?<unit>B|KB|KO|MB|MO|GB|GO)", RegexOptions.IgnoreCase)]
    private static partial Regex FileSizePattern();
}
