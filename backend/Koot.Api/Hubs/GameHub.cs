using Microsoft.AspNetCore.SignalR;

namespace Koot.Api.Hubs;

/// <summary>
/// Placeholder SignalR hub for live game sessions. Implementation lands in KOOT-7.
/// </summary>
public class GameHub : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTimeOffset.UtcNow);
}
