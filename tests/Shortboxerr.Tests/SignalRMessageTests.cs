using Shortboxerr.Core.SignalR;
using Xunit;

namespace Shortboxerr.Tests;

public class SignalRMessageTests
{
    [Fact]
    public void ImportCompletedMessage_HasCorrectType()
    {
        var message = new ImportCompletedMessage
        {
            SeriesTitle = "Batman",
            IssueNumber = "123",
            FilePath = "/library/Batman/Batman 123.cbz",
            Success = true
        };

        Assert.Equal("ImportCompletedMessage", message.Type);
        Assert.Equal("Batman", message.SeriesTitle);
        Assert.Equal("123", message.IssueNumber);
        Assert.True(message.Success);
    }

    [Fact]
    public void ImportCompletedMessage_CanRepresentFailure()
    {
        var message = new ImportCompletedMessage
        {
            SeriesTitle = "Unknown",
            IssueNumber = "?",
            FilePath = "/downloads/file.cbz",
            Success = false,
            ErrorMessage = "Could not match to series"
        };

        Assert.False(message.Success);
        Assert.Equal("Could not match to series", message.ErrorMessage);
    }

    [Fact]
    public void SearchResultsMessage_HasCorrectType()
    {
        var message = new SearchResultsMessage
        {
            SearchQuery = "Auto-Search",
            ResultCount = 5,
            SeriesTitle = "Spider-Man"
        };

        Assert.Equal("SearchResultsMessage", message.Type);
        Assert.Equal("Auto-Search", message.SearchQuery);
        Assert.Equal(5, message.ResultCount);
        Assert.Equal("Spider-Man", message.SeriesTitle);
    }

    [Fact]
    public void DownloadStartedMessage_HasCorrectType()
    {
        var message = new DownloadStartedMessage
        {
            Title = "Batman #123",
            Url = "https://example.com/batman123.cbz",
            SeriesTitle = "Batman",
            IssueNumber = "123",
            DownloadClient = "Mega",
            SizeBytes = 50_000_000
        };

        Assert.Equal("DownloadStartedMessage", message.Type);
        Assert.Equal("Batman #123", message.Title);
        Assert.Equal("Mega", message.DownloadClient);
        Assert.Equal(50_000_000, message.SizeBytes);
    }

    [Fact]
    public void DownloadCompletedMessage_HasCorrectType()
    {
        var message = new DownloadCompletedMessage
        {
            Title = "Batman #123",
            FilePath = "/downloads/batman123.cbz",
            SizeBytes = 50_000_000,
            Duration = TimeSpan.FromMinutes(2),
            Success = true
        };

        Assert.Equal("DownloadCompletedMessage", message.Type);
        Assert.True(message.Success);
        Assert.Equal(50_000_000, message.SizeBytes);
        Assert.Equal(TimeSpan.FromMinutes(2), message.Duration);
    }

    [Fact]
    public void QueueUpdateMessage_HasCorrectType()
    {
        var message = new QueueUpdateMessage
        {
            Action = "Added",
            QueueCount = 10,
            ItemId = 123,
            ItemTitle = "Batman #1"
        };

        Assert.Equal("QueueUpdateMessage", message.Type);
        Assert.Equal("Added", message.Action);
        Assert.Equal(10, message.QueueCount);
        Assert.Equal(123, message.ItemId);
    }

    [Fact]
    public void SystemStatusMessage_HasCorrectType()
    {
        var message = new SystemStatusMessage
        {
            Status = "Healthy",
            Message = "All systems operational",
            Component = "Indexer"
        };

        Assert.Equal("SystemStatusMessage", message.Type);
        Assert.Equal("Healthy", message.Status);
        Assert.Equal("Indexer", message.Component);
    }

    [Fact]
    public void AllMessages_HaveTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        
        var messages = new SignalRMessage[]
        {
            new ImportCompletedMessage { SeriesTitle = "X", IssueNumber = "1", FilePath = "/x" },
            new SearchResultsMessage { SearchQuery = "test", ResultCount = 0 },
            new DownloadStartedMessage { Title = "X", Url = "https://x.com" },
            new DownloadCompletedMessage { Title = "X", FilePath = "/x" },
            new QueueUpdateMessage { Action = "Updated", QueueCount = 0 },
            new SystemStatusMessage { Status = "ok" }
        };

        var after = DateTime.UtcNow.AddSeconds(1);

        foreach (var message in messages)
        {
            Assert.True(message.Timestamp >= before, $"{message.Type} timestamp should be >= before");
            Assert.True(message.Timestamp <= after, $"{message.Type} timestamp should be <= after");
        }
    }
}
