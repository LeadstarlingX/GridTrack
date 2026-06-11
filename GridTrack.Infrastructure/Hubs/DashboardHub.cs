using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;


[Authorize]
public sealed class DashboardHub : Hub
{
    public async Task JoinDistrict(string districtId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, districtId);

    public async Task LeaveDistrict(string districtId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, districtId);

    // Returns server UTC timestamp; client computes round-trip by diffing with send time.
    public Task<long> Ping(long clientSentMs) => Task.FromResult(clientSentMs);

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
