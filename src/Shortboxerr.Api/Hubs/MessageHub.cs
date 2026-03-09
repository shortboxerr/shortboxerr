using Microsoft.AspNetCore.SignalR;
using Shortboxerr.Core.SignalR;

namespace Shortboxerr.Api.Hubs;

/// <summary>
/// SignalR hub for real-time push notifications (*arr parity).
/// Broadcasts events for downloads, imports, searches, and system status.
/// </summary>
public class MessageHub : Hub
{
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(ILogger<MessageHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Implementation of IMessageBroadcaster using SignalR hub context.
/// </summary>
public class SignalRMessageBroadcaster : IMessageBroadcaster
{
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly ILogger<SignalRMessageBroadcaster> _logger;

    public SignalRMessageBroadcaster(
        IHubContext<MessageHub> hubContext,
        ILogger<SignalRMessageBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastDownloadStartedAsync(DownloadStartedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting download started: {Title}", message.Title);
        await _hubContext.Clients.All.SendAsync("DownloadStarted", message, cancellationToken);
    }

    public async Task BroadcastDownloadCompletedAsync(DownloadCompletedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting download completed: {Title}", message.Title);
        await _hubContext.Clients.All.SendAsync("DownloadCompleted", message, cancellationToken);
    }

    public async Task BroadcastImportCompletedAsync(ImportCompletedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting import completed: {SeriesTitle} #{IssueNumber}", message.SeriesTitle, message.IssueNumber);
        await _hubContext.Clients.All.SendAsync("ImportCompleted", message, cancellationToken);
    }

    public async Task BroadcastSearchResultsAsync(SearchResultsMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting search results: {Count} results", message.ResultCount);
        await _hubContext.Clients.All.SendAsync("SearchResults", message, cancellationToken);
    }

    public async Task BroadcastQueueUpdateAsync(QueueUpdateMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting queue update: {Action}", message.Action);
        await _hubContext.Clients.All.SendAsync("QueueUpdate", message, cancellationToken);
    }

    public async Task BroadcastSystemStatusAsync(SystemStatusMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting system status: {Status}", message.Status);
        await _hubContext.Clients.All.SendAsync("SystemStatus", message, cancellationToken);
    }
}
