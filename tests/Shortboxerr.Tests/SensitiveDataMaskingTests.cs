using Xunit;

namespace Shortboxerr.Tests;

public class SensitiveDataMaskingTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("?name=test", "?name=test")]
    [InlineData("?apikey=abc123", "?apikey=***")]
    [InlineData("?api_key=abc123", "?api_key=***")]
    [InlineData("?token=secret123", "?token=***")]
    [InlineData("?password=mysecret", "?password=***")]
    [InlineData("?secret=hidden", "?secret=***")]
    [InlineData("?name=test&apikey=abc123", "?name=test&apikey=***")]
    [InlineData("?apikey=abc123&name=test", "?apikey=***&name=test")]
    [InlineData("?APIKEY=ABC123", "?APIKEY=***")]
    [InlineData("?ApiKey=abc123", "?ApiKey=***")]
    [InlineData("?name=test&apikey=abc123&token=xyz789", "?name=test&apikey=***&token=***")]
    public void MaskSensitiveQueryParams_MasksCorrectly(string input, string expected)
    {
        // Act
        var result = Program.MaskSensitiveQueryParams(input);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void MaskSensitiveQueryParams_HandlesNull()
    {
        // Act
        var result = Program.MaskSensitiveQueryParams(null!);
        
        // Assert
        Assert.Null(result);
    }
}
