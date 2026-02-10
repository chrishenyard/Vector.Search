using System.Collections.Concurrent;

namespace Vector.Search.Hubs;

public class ChunkHub(ILogger<ChunkHub> logger) : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly ILogger<ChunkHub> _logger = logger;
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections =
            new();

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        _userConnections.TryAdd(Context.ConnectionId, []);
        return base.OnConnectedAsync();
    }

    public int GetConnectionCount()
    {
        return _userConnections.Count;
    }
}
