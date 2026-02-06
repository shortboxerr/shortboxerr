using System.Net;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Base class for download host resolvers with common functionality.
/// </summary>
public abstract class BaseHostResolver : IDownloadHostResolver
{
    protected readonly ILogger? Logger;

    // Default User-Agent (matches common browser)
    protected const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    protected BaseHostResolver(ILogger? logger = null)
    {
        Logger = logger;
    }

    public abstract string HostId { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyList<string> SupportedHosts { get; }
    public virtual int Priority => 10;
    public virtual bool IsAvailable => true;

    public virtual bool CanResolve(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var lowerUrl = url.ToLowerInvariant();
        return SupportedHosts.Any(host => lowerUrl.Contains(host.ToLowerInvariant()));
    }

    public abstract Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default);

    public virtual async Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateHttpClient(new HostResolverOptions { TimeoutSeconds = 15 });
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new HostVerifyResult
                {
                    IsAvailable = true,
                    FileSize = response.Content.Headers.ContentLength,
                    Filename = ExtractFilenameFromContentDisposition(response)
                };
            }

            var reason = ClassifyHttpStatus(response.StatusCode);
            return new HostVerifyResult
            {
                IsAvailable = false,
                FailureReason = reason,
                Message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (TaskCanceledException)
        {
            return new HostVerifyResult
            {
                IsAvailable = false,
                FailureReason = HostResolverFailureReason.Timeout,
                Message = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to verify URL: {Url}", url);
            return new HostVerifyResult
            {
                IsAvailable = false,
                FailureReason = HostResolverFailureReason.NetworkError,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates an HTTP client with the given options.
    /// </summary>
    protected virtual HttpClient CreateHttpClient(HostResolverOptions options)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30)
        };

        var userAgent = options.UserAgent ?? DefaultUserAgent;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        return client;
    }

    /// <summary>
    /// Extracts filename from Content-Disposition header.
    /// </summary>
    protected static string? ExtractFilenameFromContentDisposition(HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        return contentDisposition?.FileName?.Trim('"') ??
               contentDisposition?.FileNameStar?.Trim('"');
    }

    /// <summary>
    /// Extracts filename from URL path.
    /// </summary>
    protected static string? ExtractFilenameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(filename) ? null : Uri.UnescapeDataString(filename);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Classifies HTTP status code to failure reason.
    /// </summary>
    protected static HostResolverFailureReason ClassifyHttpStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.NotFound => HostResolverFailureReason.FileNotFound,
            HttpStatusCode.Gone => HostResolverFailureReason.LinkExpired,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => HostResolverFailureReason.AuthenticationRequired,
            HttpStatusCode.TooManyRequests => HostResolverFailureReason.RateLimited,
            >= HttpStatusCode.InternalServerError => HostResolverFailureReason.HostUnavailable,
            _ => HostResolverFailureReason.Unknown
        };
    }

    /// <summary>
    /// Classifies an exception to failure reason.
    /// </summary>
    protected static HostResolverFailureReason ClassifyException(Exception ex)
    {
        return ex switch
        {
            TaskCanceledException or OperationCanceledException => HostResolverFailureReason.Timeout,
            HttpRequestException httpEx when httpEx.Message.Contains("Name or service not known") => HostResolverFailureReason.HostUnavailable,
            HttpRequestException => HostResolverFailureReason.NetworkError,
            _ => HostResolverFailureReason.Unknown
        };
    }
}
