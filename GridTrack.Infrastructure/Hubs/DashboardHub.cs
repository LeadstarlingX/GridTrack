using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.Hubs;

/// <summary>
/// Real-time dashboard hub. Clients subscribe by calling JoinDistrict/LeaveDistrict.
/// The server pushes four event types: DriverPositionUpdated, DeliveryUpdated,
/// AnomalyBroadcast, ForecastOverlayUpdated.
/// Hub URL: /hubs/dashboard
/// </summary>
[Authorize]
public sealed class DashboardHub : Hub
{
    /// <summary>Adds the caller to a district group to receive district-scoped events.</summary>
    public async Task JoinDistrict(string districtId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, districtId);

    /// <summary>Removes the caller from a district group.</summary>
    public async Task LeaveDistrict(string districtId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, districtId);

    public override async Task OnConnectedAsync()
    {
        // JWT is validated by the [Authorize] attribute before this fires.
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
