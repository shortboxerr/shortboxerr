using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl.Resolvers;

/// <summary>
/// Resolver for Zippyshare links.
/// Zippyshare shut down in March 2023 after 17 years of operation.
/// This resolver exists to gracefully detect defunct Zippyshare links
/// and return appropriate error messages instead of cryptic failures.
/// </summary>
public class ZippyshareResolver : BaseHostResolver
{
    /// <summary>
    /// The date Zippyshare shut down (March 19, 2023).
    /// </summary>
    public static readonly DateTime ShutdownDate = new(2023, 3, 19, 0, 0, 0, DateTimeKind.Utc);

    public ZippyshareResolver(ILogger<ZippyshareResolver>? logger = null)
        : base(logger)
    {
    }

    public override string HostId => "Zippyshare";
    public override string DisplayName => "Zippyshare (Defunct)";
    
    public override IReadOnlyList<string> SupportedHosts => new[]
    {
        "zippyshare.com",
        "www.zippyshare.com",
        // Regional subdomains that Zippyshare used
        "www1.zippyshare.com",
        "www2.zippyshare.com",
        "www3.zippyshare.com",
        "www4.zippyshare.com",
        "www5.zippyshare.com",
        "www6.zippyshare.com",
        "www7.zippyshare.com",
        "www8.zippyshare.com",
        "www9.zippyshare.com",
        "www10.zippyshare.com",
        "www11.zippyshare.com",
        "www12.zippyshare.com",
        "www13.zippyshare.com",
        "www14.zippyshare.com",
        "www15.zippyshare.com",
        "www16.zippyshare.com",
        "www17.zippyshare.com",
        "www18.zippyshare.com",
        "www19.zippyshare.com",
        "www20.zippyshare.com"
    };

    public override int Priority => 99; // Low priority since it's defunct

    /// <summary>
    /// Zippyshare is no longer available.
    /// </summary>
    public override bool IsAvailable => false;

    public override Task<HostResolverResult> ResolveAsync(string url, HostResolverOptions? options = null, CancellationToken cancellationToken = default)
    {
        Logger?.LogDebug("Zippyshare link detected (defunct service): {Url}", url);

        // Return a clear error indicating the service is defunct
        return Task.FromResult(HostResolverResult.Failed(
            HostResolverFailureReason.HostUnavailable,
            "Zippyshare shut down on March 19, 2023 after 17 years of operation. This link is no longer valid."
        ));
    }

    public override Task<HostVerifyResult> VerifyAsync(string url, CancellationToken cancellationToken = default)
    {
        Logger?.LogDebug("Zippyshare link verification (defunct service): {Url}", url);

        return Task.FromResult(new HostVerifyResult
        {
            IsAvailable = false,
            FailureReason = HostResolverFailureReason.HostUnavailable,
            Message = "Zippyshare shut down on March 19, 2023. This link is no longer valid."
        });
    }

    /// <summary>
    /// Checks if a URL is a Zippyshare link.
    /// </summary>
    public static bool IsZippyshareUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var lowerUrl = url.ToLowerInvariant();
        return lowerUrl.Contains("zippyshare.com");
    }

    /// <summary>
    /// Extracts the server number from a Zippyshare URL (for historical reference).
    /// </summary>
    /// <example>
    /// https://www15.zippyshare.com/v/abc123/file.html => 15
    /// </example>
    public static int? ExtractServerNumber(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            // Extract number from wwwN.zippyshare.com
            if (host.StartsWith("www") && host.EndsWith(".zippyshare.com"))
            {
                var numberPart = host.Replace("www", "").Replace(".zippyshare.com", "");
                if (int.TryParse(numberPart, out var serverNum))
                {
                    return serverNum;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the file key from a Zippyshare URL (for historical reference).
    /// </summary>
    /// <example>
    /// https://www15.zippyshare.com/v/abc123/file.html => abc123
    /// </example>
    public static string? ExtractFileKey(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // Format: /v/{key}/file.html
            if (segments.Length >= 2 && segments[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                return segments[1];
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
