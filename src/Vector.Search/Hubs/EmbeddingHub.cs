namespace Vector.Search.Hubs;

public class EmbeddingHub(ILogger<EmbeddingHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly ILogger<EmbeddingHub> _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<string> Start()
    {
        _logger.LogDebug("Received start signal from client: {ConnectionId}", Context.ConnectionId);
        return await Task.FromResult("Started");
    }
}
