using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Unit tests for RssFeedService.
/// </summary>
public class RssFeedServiceTests
{
    private readonly RssFeedService _service;
    
    public RssFeedServiceTests()
    {
        var httpClient = new HttpClient();
        _service = new RssFeedService(httpClient);
    }
    
    #region RSS 2.0 Parsing Tests
    
    [Fact]
    public void ParseFeed_Rss2_ReturnsValidResult()
    {
        var rss = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <link>https://example.com</link>
    <description>A test feed</description>
    <item>
      <title>Batman #100</title>
      <link>https://example.com/batman-100</link>
      <pubDate>Mon, 01 Jan 2024 12:00:00 GMT</pubDate>
      <category>DC</category>
      <category>Batman</category>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.Equal("Test Feed", result.Title);
        Assert.Single(result.Items);
        Assert.Equal("Batman #100", result.Items[0].Title);
        Assert.Equal("https://example.com/batman-100", result.Items[0].Link);
        Assert.Equal(2, result.Items[0].Categories.Count);
    }
    
    [Fact]
    public void ParseFeed_Rss2_ParsesMultipleItems()
    {
        var rss = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Comics Feed</title>
    <item>
      <title>Batman #100</title>
      <link>https://example.com/batman-100</link>
    </item>
    <item>
      <title>Spider-Man #50</title>
      <link>https://example.com/spider-man-50</link>
    </item>
    <item>
      <title>X-Men #25</title>
      <link>https://example.com/x-men-25</link>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.Equal(3, result.Items.Count);
    }
    
    [Fact]
    public void ParseFeed_Rss2_ParsesDates()
    {
        // Use correct day of week for the dates
        var rss = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Test Item</title>
      <link>https://example.com/test</link>
      <pubDate>Mon, 15 Jan 2024 10:00:00 +0000</pubDate>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.NotNull(result.Items[0].PubDate);
        Assert.Equal(15, result.Items[0].PubDate!.Value.Day);
        Assert.Equal(2024, result.Items[0].PubDate!.Value.Year);
    }
    
    [Fact]
    public void ParseFeed_Rss2_ParsesEnclosure()
    {
        var rss = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Test Item</title>
      <link>https://example.com/test</link>
      <enclosure url=""https://example.com/file.cbz"" type=""application/zip"" length=""12345678""/>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.Equal("https://example.com/file.cbz", result.Items[0].EnclosureUrl);
        Assert.Equal("application/zip", result.Items[0].EnclosureType);
        Assert.Equal(12345678, result.Items[0].EnclosureLength);
    }
    
    [Fact]
    public void ParseFeed_Rss2_SkipsItemsWithoutTitleOrLink()
    {
        var rss = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test Feed</title>
    <item>
      <title>Valid Item</title>
      <link>https://example.com/valid</link>
    </item>
    <item>
      <title>Missing Link</title>
    </item>
    <item>
      <link>https://example.com/missing-title</link>
    </item>
    <item>
      <title></title>
      <link>https://example.com/empty-title</link>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("Valid Item", result.Items[0].Title);
    }
    
    #endregion
    
    #region Atom Parsing Tests
    
    [Fact]
    public void ParseFeed_Atom_ReturnsValidResult()
    {
        var atom = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"">
  <title>Atom Test Feed</title>
  <subtitle>A test Atom feed</subtitle>
  <link href=""https://example.com"" rel=""alternate""/>
  <updated>2024-01-15T12:00:00Z</updated>
  <entry>
    <title>Batman #100</title>
    <link href=""https://example.com/batman-100"" rel=""alternate""/>
    <published>2024-01-14T10:00:00Z</published>
    <id>urn:uuid:batman-100</id>
    <summary>The 100th issue of Batman</summary>
  </entry>
</feed>";
        
        var result = _service.ParseFeed(atom);
        
        Assert.True(result.Success);
        Assert.Equal("Atom Test Feed", result.Title);
        Assert.Single(result.Items);
        Assert.Equal("Batman #100", result.Items[0].Title);
        Assert.Equal("https://example.com/batman-100", result.Items[0].Link);
        Assert.Equal("The 100th issue of Batman", result.Items[0].Description);
    }
    
    [Fact]
    public void ParseFeed_Atom_ParsesCategories()
    {
        var atom = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"">
  <title>Test Feed</title>
  <entry>
    <title>Test Item</title>
    <link href=""https://example.com/test""/>
    <category term=""comics""/>
    <category term=""dc""/>
    <category term=""batman""/>
  </entry>
</feed>";
        
        var result = _service.ParseFeed(atom);
        
        Assert.True(result.Success);
        Assert.Equal(3, result.Items[0].Categories.Count);
        Assert.Contains("comics", result.Items[0].Categories);
        Assert.Contains("dc", result.Items[0].Categories);
    }
    
    #endregion
    
    #region Error Handling Tests
    
    [Fact]
    public void ParseFeed_InvalidXml_ReturnsError()
    {
        var invalidXml = "not valid xml <>";
        
        var result = _service.ParseFeed(invalidXml);
        
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("XML", result.Error);
    }
    
    [Fact]
    public void ParseFeed_UnknownFormat_ReturnsError()
    {
        var unknownXml = @"<?xml version=""1.0""?>
<unknown>
  <data>test</data>
</unknown>";
        
        var result = _service.ParseFeed(unknownXml);
        
        Assert.False(result.Success);
        Assert.Contains("Unknown feed format", result.Error);
    }
    
    [Fact]
    public void ParseFeed_EmptyFeed_ReturnsEmptyItems()
    {
        var emptyFeed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Empty Feed</title>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(emptyFeed);
        
        Assert.True(result.Success);
        Assert.Empty(result.Items);
    }
    
    #endregion
    
    #region Date Parsing Tests
    
    [Theory]
    [InlineData("Mon, 01 Jan 2024 12:00:00 GMT")]
    [InlineData("Mon, 01 Jan 2024 12:00:00 UTC")]
    [InlineData("Mon, 01 Jan 2024 12:00:00 +0000")]
    [InlineData("01 Jan 2024 12:00:00 GMT")]
    public void ParseFeed_Rss2_ParsesVariousDateFormats(string dateFormat)
    {
        var rss = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Test</title>
    <item>
      <title>Item</title>
      <link>https://example.com</link>
      <pubDate>{dateFormat}</pubDate>
    </item>
  </channel>
</rss>";
        
        var result = _service.ParseFeed(rss);
        
        Assert.True(result.Success);
        Assert.NotNull(result.Items[0].PubDate);
        Assert.Equal(2024, result.Items[0].PubDate!.Value.Year);
    }
    
    #endregion
}

/// <summary>
/// Unit tests for DdlCategories.
/// </summary>
public class DdlCategoriesTests
{
    [Fact]
    public void AllCategories_ReturnsKnownCategories()
    {
        var categories = DdlCategories.AllCategories;
        
        Assert.Contains("dc", categories);
        Assert.Contains("marvel", categories);
        Assert.Contains("image", categories);
        Assert.True(categories.Count >= 10);
    }
    
    [Theory]
    [InlineData("dc", "DC Comics")]
    [InlineData("marvel", "Marvel Comics")]
    [InlineData("image", "Image Comics")]
    [InlineData("dark-horse", "Dark Horse")]
    [InlineData("tp-hc", "Trade Paperbacks & Hardcovers")]
    public void GetDisplayName_ReturnsExpectedName(string slug, string expected)
    {
        var displayName = DdlCategories.GetDisplayName(slug);
        
        Assert.Equal(expected, displayName);
    }
    
    [Fact]
    public void GetDisplayName_UnknownSlug_ConvertsTitleCase()
    {
        var displayName = DdlCategories.GetDisplayName("some-unknown-category");
        
        Assert.Equal("Some Unknown Category", displayName);
    }
}
