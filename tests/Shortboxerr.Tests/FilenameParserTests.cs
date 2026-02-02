using Shortboxerr.Core.Services;

namespace Shortboxerr.Tests;

public class FilenameParserTests
{
    private readonly FilenameParser _parser = new();

    [Theory]
    [InlineData("Amazing Spider-Man #001.cbz", "Amazing Spider-Man", 1, false)]
    [InlineData("Batman #123.cbr", "Batman", 123, false)]
    [InlineData("X-Men #50.cbz", "X-Men", 50, false)]
    [InlineData("Saga #54.1.cbz", "Saga", 54.1, false)]
    public void Parse_SingleIssueWithHash_ExtractsCorrectly(
        string filename, string expectedSeries, decimal expectedIssue, bool expectedCollection)
    {
        // Act
        var (info, confidence, isCollection) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedSeries, info.SeriesTitle);
        Assert.Equal(expectedIssue, info.IssueNumber);
        Assert.Equal(expectedCollection, isCollection);
        Assert.True(confidence > 0);
    }

    [Theory]
    [InlineData("Amazing Spider-Man 001.cbz", "Amazing Spider-Man", 1)]
    [InlineData("Batman - 045.cbr", "Batman", 45)]
    [InlineData("Invincible 100.cbz", "Invincible", 100)]
    public void Parse_SingleIssueTrailingNumber_ExtractsCorrectly(
        string filename, string expectedSeries, decimal expectedIssue)
    {
        // Act
        var (info, confidence, isCollection) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedSeries, info.SeriesTitle);
        Assert.Equal(expectedIssue, info.IssueNumber);
        Assert.False(isCollection);
    }

    [Theory]
    [InlineData("Batman (2016) #001.cbz", "Batman", 1, 2016)]
    [InlineData("Amazing Spider-Man (1963) #129.cbz", "Amazing Spider-Man", 129, 1963)]
    public void Parse_IssueWithYear_ExtractsYearCorrectly(
        string filename, string expectedSeries, decimal expectedIssue, int expectedYear)
    {
        // Act
        var (info, confidence, _) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedSeries, info.SeriesTitle);
        Assert.Equal(expectedIssue, info.IssueNumber);
        Assert.Equal(expectedYear, info.Year);
    }

    [Theory]
    [InlineData("Batman Vol. 1 TPB.cbz", true, 1)]
    [InlineData("Saga Compendium Vol. 1.cbz", true, 1)]
    [InlineData("X-Men Omnibus.cbz", true, null)]
    [InlineData("Batman Hardcover Vol 2.cbz", true, 2)]
    public void Parse_Collection_DetectsTypeAndVolume(
        string filename, bool expectedCollection, int? expectedVolume)
    {
        // Act
        var (info, confidence, isCollection) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedCollection, isCollection);
        Assert.Equal(expectedVolume, info.VolumeNumber);
        // EditionIndicator should be set for collections
        Assert.True(isCollection);
    }

    [Theory]
    [InlineData("Batman (Marvel).cbz", "Marvel")]
    [InlineData("Spider-Man - DC.cbz", "DC")]
    [InlineData("Image Saga #1.cbz", "Image")]
    public void Parse_Publisher_ExtractsPublisher(string filename, string expectedPublisher)
    {
        // Act
        var (info, _, _) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedPublisher, info.Publisher);
    }

    [Theory]
    [InlineData("Batman #10.cbz", 10)]
    [InlineData("X-Men #05.cbz", 5)]
    public void Parse_IssueNumber_ExtractsCorrectly(string filename, decimal expectedIssue)
    {
        // Act
        var (info, _, _) = _parser.Parse(filename);

        // Assert
        Assert.Equal(expectedIssue, info.IssueNumber);
    }

    [Theory]
    [InlineData("Batman [scan].cbz")]
    [InlineData("Amazing Spider-Man (digital).cbz")]
    public void Parse_WithTags_ExtractsTags(string filename)
    {
        // Act
        var (info, _, _) = _parser.Parse(filename);

        // Assert
        Assert.NotEmpty(info.Tags);
    }

    [Theory]
    [InlineData("file.cbz", 0)]
    [InlineData("Batman #1.cbz", 25)] // Has issue number
    [InlineData("Batman (2020) #1.cbz", 35)] // Has issue + year
    public void Parse_Confidence_IncreasesWithMoreInfo(string filename, int minConfidence)
    {
        // Act
        var (_, confidence, _) = _parser.Parse(filename);

        // Assert
        Assert.True(confidence >= minConfidence, $"Expected confidence >= {minConfidence}, got {confidence}");
    }

    [Fact]
    public void Parse_CollectionIndicators_AllDetected()
    {
        var collectionFilenames = new[]
        {
            "Batman TPB.cbz",
            "Batman Trade Paperback.cbz",
            "Batman Hardcover.cbz",
            "Batman HC.cbz",
            "Batman Omnibus.cbz",
            "Batman Compendium.cbz",
            "Batman Absolute Edition.cbz",
            "Batman Deluxe.cbz",
            "Batman Vol. 1.cbz",
            "Batman Book One.cbz"
        };

        foreach (var filename in collectionFilenames)
        {
            var (_, _, isCollection) = _parser.Parse(filename);
            Assert.True(isCollection, $"Expected {filename} to be detected as collection");
        }
    }
}

