using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Google Drive file sharing links.
/// Handles the conversion of share URLs to direct download URLs,
/// including handling the virus scan warning for large files.
/// </summary>
public partial class GoogleDriveResolver : BaseHostResolver
{
    private const string DirectDownloadBaseUrl = "https://drive.google.com/uc?export=download&id=";
    private const string ConfirmDownloadUrl = "https://drive.google.com/uc?export=download&confirm=t&id=";

    public GoogleDriveResolver(ILogger<GoogleDriveResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "GoogleDrive";
    public override string DisplayName => "Google Drive";
    public override IReadOnlyList<string> SupportedHosts => new[] { "drive.google.com", "docs.google.com" };
    public override int Priority => 4;

    public override async Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HostResolverOptions();

        Logger?.LogDebug("Resolving Google Drive URL: {Url}", url);

        try
        {
            // Extract file ID from URL
            var fileId = ExtractFileId(url);
            if (string.IsNullOrEmpty(fileId))
            {
                Logger?.LogWarning("Failed to extract Google Drive file ID from URL: {Url}", url);
                return HostResolverResult.Failed(
                    HostResolverFailureReason.ParseError,
                    "Could not extract file ID from Google Drive URL"
                );
            }

            Logger?.LogDebug("Extracted Google Drive file ID: {FileId}", fileId);

            // Build direct download URL
            var directUrl = $"{DirectDownloadBaseUrl}{fileId}";

            using var client = CreateHttpClient(options);

            // First, try HEAD request to get metadata
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, directUrl);
            using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Check if this is the virus scan warning page (for large files)
            // Google returns 200 OK with HTML content for the warning page
            var contentType = headResponse.Content.Headers.ContentType?.MediaType;

            if (headResponse.IsSuccessStatusCode)
            {
                // If it's HTML, it's likely the virus scan warning page
                if (contentType != null && contentType.Contains("text/html"))
                {
                    Logger?.LogDebug("Google Drive returned HTML - likely virus scan warning, using confirm URL");
                    // Use the confirmed download URL (bypasses virus scan warning)
                    directUrl = $"{ConfirmDownloadUrl}{fileId}";

                    // Verify the confirmed URL
                    using var confirmRequest = new HttpRequestMessage(HttpMethod.Head, directUrl);
                    using var confirmResponse = await client.SendAsync(confirmRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (!confirmResponse.IsSuccessStatusCode)
                    {
                        return HandleNonSuccessResponse(confirmResponse);
                    }

                    var confirmFilename = ExtractFilenameFromContentDisposition(confirmResponse);
                    var confirmFileSize = confirmResponse.Content.Headers.ContentLength;

                    return new HostResolverResult
                    {
                        Success = true,
                        DirectUrl = directUrl,
                        Filename = confirmFilename,
                        FileSize = confirmFileSize,
                        ContentType = confirmResponse.Content.Headers.ContentType?.MediaType,
                        FailureReason = HostResolverFailureReason.None
                    };
                }

                // Direct download available (small file or already has necessary cookies)
                var filename = ExtractFilenameFromContentDisposition(headResponse);
                var fileSize = headResponse.Content.Headers.ContentLength;

                Logger?.LogDebug("Google Drive resolved: {Filename}, Size: {Size}", filename, fileSize);

                return new HostResolverResult
                {
                    Success = true,
                    DirectUrl = directUrl,
                    Filename = filename,
                    FileSize = fileSize,
                    ContentType = contentType,
                    FailureReason = HostResolverFailureReason.None
                };
            }

            return HandleNonSuccessResponse(headResponse);
        }
        catch (TaskCanceledException)
        {
            return HostResolverResult.Failed(HostResolverFailureReason.Timeout, "Request timed out");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error resolving Google Drive URL: {Url}", url);
            return HostResolverResult.Failed(HostResolverFailureReason.Unknown, ex.Message);
        }
    }

    private HostResolverResult HandleNonSuccessResponse(HttpResponseMessage response)
    {
        var reason = ClassifyHttpStatus(response.StatusCode);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.FileNotFound,
                "File not found or has been removed"
            );
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.AuthenticationRequired,
                "File is not publicly shared or requires permission"
            );
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return HostResolverResult.Failed(
                HostResolverFailureReason.RateLimited,
                "Too many requests - file download limit reached"
            );
        }

        Logger?.LogWarning("Google Drive request failed: HTTP {StatusCode}", (int)response.StatusCode);
        return HostResolverResult.Failed(reason, $"HTTP {(int)response.StatusCode}");
    }

    /// <summary>
    /// Extracts the file ID from various Google Drive URL formats.
    /// </summary>
    /// <remarks>
    /// Supported formats:
    /// - https://drive.google.com/file/d/{fileId}/view
    /// - https://drive.google.com/open?id={fileId}
    /// - https://drive.google.com/uc?id={fileId}
    /// - https://docs.google.com/uc?id={fileId}
    /// - https://drive.google.com/u/0/uc?id={fileId}
    /// </remarks>
    internal static string? ExtractFileId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // Pattern 1: /file/d/{fileId}/
        var match = FileIdFromPathPattern().Match(url);
        if (match.Success)
        {
            return match.Groups["fileId"].Value;
        }

        // Pattern 2: id={fileId} in query string
        match = FileIdFromQueryPattern().Match(url);
        if (match.Success)
        {
            return match.Groups["fileId"].Value;
        }

        // Pattern 3: /folders/{fileId} (folder link)
        match = FolderIdPattern().Match(url);
        if (match.Success)
        {
            return match.Groups["fileId"].Value;
        }

        return null;
    }

    /// <summary>
    /// Checks if the URL is a folder link rather than a file link.
    /// </summary>
    internal static bool IsFolderLink(string url)
    {
        return url.Contains("/folders/", StringComparison.OrdinalIgnoreCase);
    }

    // /file/d/{fileId}/ or /file/d/{fileId}?
    [GeneratedRegex(@"/file/d/(?<fileId>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdFromPathPattern();

    // id={fileId} or &id={fileId}
    [GeneratedRegex(@"[?&]id=(?<fileId>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FileIdFromQueryPattern();

    // /folders/{fileId}
    [GeneratedRegex(@"/folders/(?<fileId>[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FolderIdPattern();
}
