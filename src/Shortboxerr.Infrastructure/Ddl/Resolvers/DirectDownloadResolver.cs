using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for direct HTTP download links.
/// Handles standard HTTP GET downloads with Content-Disposition support.
/// </summary>
public class DirectDownloadResolver : BaseHostResolver
{
    // File extensions that indicate direct downloads
    private static readonly string[] DirectFileExtensions = { ".cbz", ".cbr", ".zip", ".rar", ".pdf", ".cb7", ".7z" };

    public DirectDownloadResolver(ILogger<DirectDownloadResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "Direct";
    public override string DisplayName => "Direct Download";
    public override IReadOnlyList<string> SupportedHosts => Array.Empty<string>(); // Handled via CanResolve
    public override int Priority => 0; // Highest priority - direct links are best

    /// <summary>
    /// Direct downloads are handled based on URL pattern/extension, not hostname.
    /// </summary>
    public override bool CanResolve(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Check if URL has a direct file extension
        var lowerUrl = url.ToLowerInvariant();
        if (DirectFileExtensions.Any(ext => lowerUrl.EndsWith(ext) || lowerUrl.Contains(ext + "?")))
        {
            return true;
        }

        // Check if URL contains download indicators
        if (lowerUrl.Contains("/download/") || lowerUrl.Contains("/dl/") || lowerUrl.Contains("attachment="))
        {
            return true;
        }

        return false;
    }

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving direct download URL: {Url}", url);

        try
        {
            using var client = CreateHttpClient(options);

            // Use HEAD request first to get metadata without downloading
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!headResponse.IsSuccessStatusCode)
            {
                var reason = ClassifyHttpStatus(headResponse.StatusCode);
                Logger?.LogWarning("Direct download HEAD request failed: HTTP {StatusCode}", (int)headResponse.StatusCode);

                return HostResolverResult.Failed(
                    reason,
                    $"HTTP {(int)headResponse.StatusCode}: {headResponse.ReasonPhrase}"
                );
            }

            // Extract metadata
            var filename = ExtractFilenameFromContentDisposition(headResponse) ??
                          ExtractFilenameFromUrl(url);
            var fileSize = headResponse.Content.Headers.ContentLength;
            var contentType = headResponse.Content.Headers.ContentType?.MediaType;

            // Check if server supports range requests (for resume)
            var supportsResume = headResponse.Headers.AcceptRanges.Contains("bytes");

            Logger?.LogDebug("Direct download resolved: {Filename}, Size: {Size}, ContentType: {ContentType}, Resume: {Resume}",
                filename, fileSize, contentType, supportsResume);

            // For direct downloads, the URL itself is the direct URL
            var result = HostResolverResult.Succeeded(url, filename, fileSize);

            // Store additional metadata
            if (!string.IsNullOrEmpty(contentType))
            {
                result = result with { ContentType = contentType };
            }

            return result;
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            Logger?.LogWarning(ex, "Direct download resolution failed: {Url}", url);
            return HostResolverResult.Failed(ClassifyException(ex), ex.Message);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving direct download: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }
}
