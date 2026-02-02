using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Infrastructure.Ddl;

/// <summary>
/// Mock DDL site adapter for testing and development.
/// Returns fake data that matches expected patterns.
/// </summary>
public class MockDdlSiteAdapter : BaseDdlSiteAdapter
{
    public override string SiteType => "MockDdl";
    public override string DisplayName => "Mock DDL (Testing)";
    public override string DefaultBaseUrl => "https://mock.ddl.local";
    public override bool RequiresAuthentication => false;
    public override int DefaultRateLimitPerMinute => 60; // No real limit for mock
    
    private readonly DdlReleaseParser _parser = new();
    
    // Sample releases for testing
    private static readonly string[] SampleReleases = new[]
    {
        "Amazing Spider-Man 001 (2022) (Digital) (Zone-Empire).cbz",
        "Amazing Spider-Man 002 (2022) (Digital) (Zone-Empire).cbz",
        "Batman #150 (DC) (2023) (Digital) (Zone-Empire).cbz",
        "X-Men v1 001 (1963) (Digital) (Glorith-HD).cbz",
        "Saga Vol. 01 TPB (2012) (Digital) (Zone-Empire).cbz",
        "Immortal Hulk 001 (2018) (Digital) (Zone-Empire).cbr",
        "Walking Dead TPB Vol 1 (Image) (2004) (Digital).cbz",
        "Invincible Deluxe Edition Vol. 1 (Image) (2020) (Digital).cbz",
        "Superman #1 (DC) (2023) (Digital) (DCP).cbz",
        "Wonder Woman 800 (DC) (2023) (Digital) (DCP).cbz"
    };

    public override Task<DdlSearchResult> SearchAsync(DdlSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var candidates = new List<DdlCandidate>();
            var searchTerm = query.SeriesTitle?.ToLowerInvariant() ?? query.RawQuery?.ToLowerInvariant() ?? "";
            
            foreach (var release in SampleReleases)
            {
                // Filter by search term
                if (!string.IsNullOrEmpty(searchTerm) && !release.ToLowerInvariant().Contains(searchTerm))
                {
                    continue;
                }
                
                var parsed = _parser.Parse(release);
                
                // Filter by issue number
                if (query.IssueNumber.HasValue && parsed.IssueNumber != query.IssueNumber)
                {
                    continue;
                }
                
                // Filter by year
                if (query.Year.HasValue && parsed.Year != query.Year)
                {
                    continue;
                }
                
                // Filter by collections only
                if (query.CollectionsOnly && !parsed.IsCollection)
                {
                    continue;
                }
                
                candidates.Add(new DdlCandidate
                {
                    Id = Guid.NewGuid().ToString(),
                    ReleaseTitle = release,
                    SourceSite = SiteType,
                    SourceUrl = $"{DefaultBaseUrl}/release/{Uri.EscapeDataString(release)}",
                    ParsedInfo = parsed,
                    DownloadLinks = new List<DdlDownloadLink>
                    {
                        new()
                        {
                            Url = $"{DefaultBaseUrl}/download/{Uri.EscapeDataString(release)}",
                            LinkType = DdlLinkType.Direct,
                            Priority = 0
                        }
                    },
                    Size = GetMockSize(parsed.IsCollection),
                    DateFound = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72)),
                    QualityScore = parsed.Confidence
                });
            }
            
            // Apply limit and offset
            var total = candidates.Count;
            candidates = candidates
                .Skip(query.Offset)
                .Take(query.Limit)
                .ToList();
            
            stopwatch.Stop();
            
            return Task.FromResult(DdlSearchResult.Ok(
                candidates, 
                SiteType, 
                total, 
                stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(DdlSearchResult.Error(ex.Message, SiteType, stopwatch.Elapsed));
        }
    }

    public override Task<DdlSearchResult> GetLatestAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        return SearchAsync(new DdlSearchQuery { Limit = limit }, cancellationToken);
    }

    public override Task<IReadOnlyList<DdlDownloadLink>> ExtractLinksAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        // Mock: just return a single direct link
        var links = new List<DdlDownloadLink>
        {
            new()
            {
                Url = pageUrl.Replace("/release/", "/download/"),
                LinkType = DdlLinkType.Direct,
                Priority = 0
            }
        };
        
        return Task.FromResult<IReadOnlyList<DdlDownloadLink>>(links);
    }

    public override Task<bool> VerifyLinkAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        // Mock: all links are valid
        return Task.FromResult(true);
    }

    public override Task<DdlSiteTestResult> TestConnectionAsync(DdlSiteCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        // Mock: always succeeds
        return Task.FromResult(new DdlSiteTestResult
        {
            Success = true,
            Message = "Mock site connection successful",
            SampleResultCount = SampleReleases.Length,
            LatencyMs = 5
        });
    }

    private static long GetMockSize(bool isCollection)
    {
        // Return realistic file sizes
        return isCollection
            ? Random.Shared.NextInt64(50_000_000, 500_000_000) // 50-500MB for collections
            : Random.Shared.NextInt64(10_000_000, 80_000_000); // 10-80MB for singles
    }
}

