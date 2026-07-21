using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;


[Authorize]
public sealed class DashboardHub(IDistrictGroupCache groupCache) : Hub
{
    private static readonly Meter HubMeter = new("gridtrack.hub", "1.0");
    private static readonly UpDownCounter<int> ConnectedClients =
        HubMeter.CreateUpDownCounter<int>(
            "hub.connections.active",
            description: "Number of active SignalR connections");

    public async Task JoinDistrict(string districtId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, districtId);

    public async Task LeaveDistrict(string districtId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, districtId);

    public async Task JoinDistrictGroup(string districtGroupId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"dg:{districtGroupId}");

    public async Task LeaveDistrictGroup(string districtGroupId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dg:{districtGroupId}");

    public Task<long> Ping(long clientSentMs) => Task.FromResult(clientSentMs);

    public override async Task OnConnectedAsync()
    {
        ConnectedClients.Add(1);

        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var role = user.FindFirstValue("role");

            // Capture before hub scope is disposed; DefaultGroupManager wraps a singleton.
            var connectionId = Context.ConnectionId;
            var groups       = Groups;

            if (role == "GeneralObserver")
            {
                // Fire-and-forget: OnConnectedAsync must return before the handshake response
                // is sent, so blocking on Redis here would exceed the client's 15-second timeout.
                _ = Task.Run(async () =>
                {
                    var allIds = await groupCache.GetAllGroupIdsAsync(default);
                    foreach (var id in allIds)
                        await groups.AddToGroupAsync(connectionId, $"dg:{id}");
                });
            }
            else if (role == "Observer")
            {
                // sectorId claims carry DistrictGroup UUIDs — join them directly.
                var sectorIds = user.FindAll("sectorId").Select(c => c.Value).ToList();
                _ = Task.WhenAll(sectorIds.Select(sid =>
                    groups.AddToGroupAsync(connectionId, $"dg:{sid}")));
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedClients.Add(-1);
        await base.OnDisconnectedAsync(exception);
    }
}