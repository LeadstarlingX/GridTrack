using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;


[Authorize]
public sealed class DashboardHub : Hub
{
    private static readonly Meter HubMeter = new("gridtrack.hub", "1.0");
    private static readonly UpDownCounter<int> ConnectedClients =
        HubMeter.CreateUpDownCounter<int>("hub.connections.active", description: "Number of active SignalR connections");

    public async Task JoinDistrict(string districtId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, districtId);

    public async Task LeaveDistrict(string districtId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, districtId);

    // Subscribe to all drivers whose district belongs to the named district group.
    // The server broadcasts to the "dg:{groupId}" SignalR group on every position tick.
    public async Task JoinDistrictGroup(string districtGroupId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"dg:{districtGroupId}");

    public async Task LeaveDistrictGroup(string districtGroupId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dg:{districtGroupId}");

    // Returns server UTC timestamp; client computes round-trip by diffing with send time.
    public Task<long> Ping(long clientSentMs) => Task.FromResult(clientSentMs);

    public override async Task OnConnectedAsync()
    {
        ConnectedClients.Add(1);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedClients.Add(-1);
        await base.OnDisconnectedAsync(exception);
    }
}
