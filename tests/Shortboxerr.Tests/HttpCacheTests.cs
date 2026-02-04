using Microsoft.AspNetCore.Http;
using Shortboxerr.Api.Caching;
using Xunit;

namespace Shortboxerr.Tests;

public class HttpCacheTests
{
    #region ETag Generation Tests

    [Fact]
    public void GenerateETag_FromTimestamp_ReturnsConsistentValue()
    {
        // Arrange
        var timestamp = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        
        // Act
        var etag1 = ETagHelper.GenerateETag(timestamp);
        var etag2 = ETagHelper.GenerateETag(timestamp);
        
        // Assert
        Assert.Equal(etag1, etag2);
        Assert.StartsWith("\"", etag1);
        Assert.EndsWith("\"", etag1);
    }

    [Fact]
    public void GenerateETag_DifferentTimestamps_ReturnsDifferentValues()
    {
        // Arrange
        var timestamp1 = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        var timestamp2 = new DateTime(2026, 2, 4, 12, 31, 0, DateTimeKind.Utc);
        
        // Act
        var etag1 = ETagHelper.GenerateETag(timestamp1);
        var etag2 = ETagHelper.GenerateETag(timestamp2);
        
        // Assert
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void GenerateETag_FromIdAndTimestamp_IncludesBothInHash()
    {
        // Arrange
        var id = 123;
        var timestamp = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        
        // Act
        var etag1 = ETagHelper.GenerateETag(id, timestamp);
        var etag2 = ETagHelper.GenerateETag(id, timestamp);
        
        // Assert
        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void GenerateETag_DifferentIds_ReturnsDifferentValues()
    {
        // Arrange
        var timestamp = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        
        // Act
        var etag1 = ETagHelper.GenerateETag(1, timestamp);
        var etag2 = ETagHelper.GenerateETag(2, timestamp);
        
        // Assert
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void GenerateETag_FromString_ReturnsConsistentValue()
    {
        // Arrange
        var version = "v1.2.3";
        
        // Act
        var etag1 = ETagHelper.GenerateETag(version);
        var etag2 = ETagHelper.GenerateETag(version);
        
        // Assert
        Assert.Equal(etag1, etag2);
    }

    #endregion

    #region ETag Validation Tests

    [Fact]
    public void IsNotModified_MatchingETag_ReturnsTrue()
    {
        // Arrange
        var currentETag = "\"ABC123\"";
        var request = CreateMockRequest(ifNoneMatch: "\"ABC123\"");
        
        // Act
        var result = ETagHelper.IsNotModified(request, currentETag);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNotModified_NonMatchingETag_ReturnsFalse()
    {
        // Arrange
        var currentETag = "\"ABC123\"";
        var request = CreateMockRequest(ifNoneMatch: "\"XYZ789\"");
        
        // Act
        var result = ETagHelper.IsNotModified(request, currentETag);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNotModified_NoHeader_ReturnsFalse()
    {
        // Arrange
        var currentETag = "\"ABC123\"";
        var request = CreateMockRequest(ifNoneMatch: null);
        
        // Act
        var result = ETagHelper.IsNotModified(request, currentETag);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNotModified_WildcardETag_ReturnsTrue()
    {
        // Arrange
        var currentETag = "\"ABC123\"";
        var request = CreateMockRequest(ifNoneMatch: "*");
        
        // Act
        var result = ETagHelper.IsNotModified(request, currentETag);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNotModified_MultipleETags_MatchesOneReturnsTrue()
    {
        // Arrange
        var currentETag = "\"ABC123\"";
        var request = CreateMockRequest(ifNoneMatch: "\"XYZ789\", \"ABC123\", \"DEF456\"");
        
        // Act
        var result = ETagHelper.IsNotModified(request, currentETag);
        
        // Assert
        Assert.True(result);
    }

    #endregion

    #region If-Modified-Since Tests

    [Fact]
    public void IsNotModifiedSince_OlderResource_ReturnsFalse()
    {
        // Arrange
        var lastModified = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        var request = CreateMockRequest(ifModifiedSince: "Wed, 04 Feb 2026 12:00:00 GMT");
        
        // Act
        var result = ETagHelper.IsNotModifiedSince(request, lastModified);
        
        // Assert
        Assert.False(result); // Resource is newer than client's version
    }

    [Fact]
    public void IsNotModifiedSince_NewerOrSameResource_ReturnsTrue()
    {
        // Arrange
        var lastModified = new DateTime(2026, 2, 4, 12, 0, 0, DateTimeKind.Utc);
        var request = CreateMockRequest(ifModifiedSince: "Wed, 04 Feb 2026 12:30:00 GMT");
        
        // Act
        var result = ETagHelper.IsNotModifiedSince(request, lastModified);
        
        // Assert
        Assert.True(result); // Client has same or newer version
    }

    [Fact]
    public void IsNotModifiedSince_NoHeader_ReturnsFalse()
    {
        // Arrange
        var lastModified = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        var request = CreateMockRequest(ifModifiedSince: null);
        
        // Act
        var result = ETagHelper.IsNotModifiedSince(request, lastModified);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNotModifiedSince_InvalidDateFormat_ReturnsFalse()
    {
        // Arrange
        var lastModified = new DateTime(2026, 2, 4, 12, 30, 0, DateTimeKind.Utc);
        var request = CreateMockRequest(ifModifiedSince: "invalid-date");
        
        // Act
        var result = ETagHelper.IsNotModifiedSince(request, lastModified);
        
        // Assert
        Assert.False(result);
    }

    #endregion

    #region HttpCacheSettings Tests

    [Fact]
    public void HttpCacheSettings_DefaultValues()
    {
        // Arrange & Act
        var settings = new HttpCacheSettings();
        
        // Assert
        Assert.Equal(120, settings.MaxAgeSeconds);
        Assert.False(settings.IsPrivate);
        Assert.False(settings.NoStore);
        Assert.True(settings.IncludeETag);
        Assert.True(settings.IncludeLastModified);
    }

    #endregion

    #region Helpers

    private static HttpRequest CreateMockRequest(
        string? ifNoneMatch = null,
        string? ifModifiedSince = null)
    {
        var context = new DefaultHttpContext();
        
        if (ifNoneMatch != null)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }
        
        if (ifModifiedSince != null)
        {
            context.Request.Headers.IfModifiedSince = ifModifiedSince;
        }
        
        return context.Request;
    }

    #endregion
}
