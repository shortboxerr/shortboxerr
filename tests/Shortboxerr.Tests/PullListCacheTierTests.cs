using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for intelligent pull list cache tier functionality.
/// </summary>
public class PullListCacheTierTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PullListCacheTierTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWeeklyReleases_IncludesCacheMetadata()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - /api/v1/pulllist/week is the current week endpoint
        var response = await client.GetAsync("/api/v1/pulllist/week");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        // Verify cache metadata exists
        Assert.True(doc.RootElement.TryGetProperty("cacheMetadata", out var cacheMetadata), 
            "Response should include cacheMetadata");
        
        Assert.True(cacheMetadata.TryGetProperty("tier", out _), 
            "CacheMetadata should include tier");
        Assert.True(cacheMetadata.TryGetProperty("releaseDay", out _), 
            "CacheMetadata should include releaseDay");
        Assert.True(cacheMetadata.TryGetProperty("transitionDate", out _), 
            "CacheMetadata should include transitionDate");
    }

    [Fact]
    public async Task GetWeeklyDiscovery_IncludesCacheMetadata()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - /api/v1/pulllist/discover/week is the current week discovery endpoint
        var response = await client.GetAsync("/api/v1/pulllist/discover/week");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        // Verify cache metadata exists
        Assert.True(doc.RootElement.TryGetProperty("cacheMetadata", out var cacheMetadata), 
            "Response should include cacheMetadata");
        
        Assert.True(cacheMetadata.TryGetProperty("tier", out var tier), 
            "CacheMetadata should include tier");
        
        // Current week should be Active tier (0 as enum value)
        Assert.Equal(0, tier.GetInt32()); // CacheTier.Active = 0
    }

    [Fact]
    public async Task GetPastWeek_ShouldReturnHistoricalTier()
    {
        // Arrange
        var client = _factory.CreateClient();
        var pastDate = DateTime.Today.AddDays(-14).ToString("yyyy-MM-dd");

        // Act
        var response = await client.GetAsync($"/api/v1/pulllist/discover/week/{pastDate}");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        // Verify cache metadata shows Historical tier
        Assert.True(doc.RootElement.TryGetProperty("cacheMetadata", out var cacheMetadata));
        Assert.True(cacheMetadata.TryGetProperty("tier", out var tier));
        
        // Past weeks (beyond buffer) should be Historical tier (1 as enum value)
        Assert.Equal(1, tier.GetInt32()); // CacheTier.Historical = 1
    }

    [Fact]
    public async Task GetPullListSettings_ReturnsCacheTierSettings()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/pulllist/settings");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        // Verify new cache tier settings exist
        Assert.True(doc.RootElement.TryGetProperty("cacheBufferDays", out var bufferDays),
            "Settings should include cacheBufferDays");
        Assert.True(bufferDays.GetInt32() >= 0, "cacheBufferDays should be non-negative");
        
        Assert.True(doc.RootElement.TryGetProperty("historicalCacheTtlDays", out var historicalTtl),
            "Settings should include historicalCacheTtlDays");
        Assert.True(historicalTtl.GetInt32() > 0, "historicalCacheTtlDays should be positive");
        
        Assert.True(doc.RootElement.TryGetProperty("historicalRefreshEnabled", out _),
            "Settings should include historicalRefreshEnabled");
        
        Assert.True(doc.RootElement.TryGetProperty("activeCacheTtlMinutes", out var activeTtl),
            "Settings should include activeCacheTtlMinutes");
        Assert.True(activeTtl.GetInt32() > 0, "activeCacheTtlMinutes should be positive");
    }

    [Fact]
    public async Task UpdateCacheSettings_PersistsCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // First get current settings
        var getResponse = await client.GetAsync("/api/v1/pulllist/settings");
        getResponse.EnsureSuccessStatusCode();
        var currentContent = await getResponse.Content.ReadAsStringAsync();
        using var currentDoc = JsonDocument.Parse(currentContent);
        
        // Update cache settings - must include all required fields with proper enum values
        var updatePayload = new
        {
            weekStartDay = 0, // DayOfWeek.Sunday
            releaseDay = 3,   // DayOfWeek.Wednesday
            defaultMonitoringMode = 1, // SeriesMonitoringMode.FutureIssues
            searchDelayHours = 6,
            autoAddToWanted = true,
            includeAnnualsInAutoAdd = true,
            includeSpecialsInAutoAdd = false,
            skipVariantCovers = true,
            releaseDayProcessingHours = new[] { 6, 12 },
            upcomingWeeksToShow = 4,
            pastWeeksToShow = 4,
            exportWeeklyPullList = false,
            autoExportOnReleaseDay = false,
            weeklyExportFormat = 0, // WeeklyExportFormat.Json
            // New cache tier settings
            cacheBufferDays = 3,
            historicalCacheTtlDays = 14,
            historicalRefreshEnabled = true,
            historicalRefreshIntervalDays = 5,
            activeCacheTtlMinutes = 15
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/pulllist/settings", updatePayload);
        
        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify the settings were saved
        var verifyResponse = await client.GetAsync("/api/v1/pulllist/settings");
        verifyResponse.EnsureSuccessStatusCode();
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        using var verifyDoc = JsonDocument.Parse(verifyContent);
        
        Assert.Equal(3, verifyDoc.RootElement.GetProperty("cacheBufferDays").GetInt32());
        Assert.Equal(14, verifyDoc.RootElement.GetProperty("historicalCacheTtlDays").GetInt32());
        Assert.True(verifyDoc.RootElement.GetProperty("historicalRefreshEnabled").GetBoolean());
        Assert.Equal(5, verifyDoc.RootElement.GetProperty("historicalRefreshIntervalDays").GetInt32());
        Assert.Equal(15, verifyDoc.RootElement.GetProperty("activeCacheTtlMinutes").GetInt32());
    }
}
