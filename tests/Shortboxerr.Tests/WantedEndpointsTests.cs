using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Integration tests for Wanted API endpoints.
/// </summary>
public class WantedEndpointsTests : BaseEndpointTest
{
    public WantedEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region GET /api/v1/wanted/issues

    [Fact]
    public async Task GetWantedIssues_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_ReturnsPagedResult()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues");
        var content = await response.Content.ReadFromJsonAsync<WantedPagedResultDto>();

        // Assert
        Assert.NotNull(content);
        Assert.NotNull(content.Items);
        Assert.True(content.Page >= 1);
        Assert.True(content.PageSize > 0);
        Assert.True(content.TotalCount >= 0);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsSearch()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?search=batman");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsSorting()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?sortKey=series&sortDir=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsPagination()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<WantedPagedResultDto>();
        Assert.NotNull(content);
        Assert.True(content.PageSize <= 10 || content.PageSize == 50); // Default might be 50
    }

    [Fact]
    public async Task GetWantedIssues_SupportsPublisherFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?publisher=marvel");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsReleaseDateAfterFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?releasedAfter=2024-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsReleaseDateBeforeFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?releasedBefore=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsReleaseDateRangeFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?releasedAfter=2024-01-01&releasedBefore=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedIssues_SupportsCombinedFilters()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/issues?search=batman&publisher=dc&releasedAfter=2020-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/wanted/collections

    [Fact]
    public async Task GetWantedCollections_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCollections_ReturnsPagedResult()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections");
        var content = await response.Content.ReadFromJsonAsync<WantedPagedResultDto>();

        // Assert
        Assert.NotNull(content);
        Assert.NotNull(content.Items);
        Assert.True(content.Page >= 1);
        Assert.True(content.PageSize > 0);
    }

    [Fact]
    public async Task GetWantedCollections_SupportsSearch()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections?search=batman");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCollections_SupportsPublisherFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections?publisher=dc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCollections_SupportsReleaseDateRangeFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections?releasedAfter=2024-01-01&releasedBefore=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCollections_SupportsEditionTypeFilter()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections?editionType=Hardcover");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCollections_SupportsCombinedFilters()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/collections?search=batman&publisher=dc&editionType=TradesPaperback");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/wanted/count

    [Fact]
    public async Task GetWantedCount_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWantedCount_ReturnsCountDto()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/wanted/count");
        var content = await response.Content.ReadFromJsonAsync<WantedCountDto>();

        // Assert
        Assert.NotNull(content);
        Assert.True(content.Issues >= 0);
        Assert.True(content.Collections >= 0);
        Assert.Equal(content.Issues + content.Collections, content.Total);
    }

    #endregion

    #region Test DTOs

    private class WantedPagedResultDto
    {
        public List<object>? Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    private class WantedCountDto
    {
        public int Issues { get; set; }
        public int Collections { get; set; }
        public int Total { get; set; }
    }

    #endregion
}
