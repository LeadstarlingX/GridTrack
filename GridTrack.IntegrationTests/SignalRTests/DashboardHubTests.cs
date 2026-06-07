using FluentAssertions;
using GridTrack.Infrastructure.Hubs;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GridTrack.IntegrationTests.SignalRTests;

public class DashboardHubTests : BaseIntegrationTest
{
    private static HubConnection BuildConnection(string token)
    {
        var handler = Factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl($"http://localhost/hubs/dashboard?access_token={token}",
                o => o.HttpMessageHandlerFactory = _ => handler)
            .Build();
    }

    [Test]
    [NotInParallel(Order = 1000)]
    public async Task Hub_Rejects_Unauthenticated_Connection()
    {
        var conn = BuildConnection("invalid-token");

        var act = async () => await conn.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*401*");
    }

    [Test]
    [NotInParallel(Order = 1001)]
    public async Task Hub_Accepts_Authenticated_Connection()
    {
        var conn = BuildConnection(TestAuthHandler.ValidToken);

        await conn.StartAsync();

        conn.State.Should().Be(HubConnectionState.Connected);

        await conn.StopAsync();
    }

    [Test]
    [NotInParallel(Order = 1002)]
    public async Task JoinDistrict_Allows_Client_To_Receive_GroupMessages()
    {
        var conn1 = BuildConnection(TestAuthHandler.ValidToken);
        var conn2 = BuildConnection(TestAuthHandler.ValidToken);

        var received = new List<string>();
        conn1.On<object>("DriverPositionUpdated", payload =>
            received.Add(payload.ToString()!));

        await conn1.StartAsync();
        await conn2.StartAsync();

        await conn1.InvokeAsync("JoinDistrict", "mezzeh");

        // conn2 triggers a broadcast to the group (use hub context directly)
        await using var scope = Factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider
            .GetRequiredService<IHubContext<DashboardHub>>();

        await hubContext.Clients.Group("mezzeh")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid(), lat = 33.5, lng = 36.2 }]);

        await Task.Delay(200); // allow message delivery

        received.Should().HaveCount(1);

        await conn1.StopAsync();
        await conn2.StopAsync();
    }

    [Test]
    [NotInParallel(Order = 1003)]
    public async Task LeaveDistrict_Stops_Receiving_GroupMessages()
    {
        var conn = BuildConnection(TestAuthHandler.ValidToken);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", payload =>
            received.Add(payload.ToString()!));

        await conn.StartAsync();
        await conn.InvokeAsync("JoinDistrict", "mezzeh");
        await conn.InvokeAsync("LeaveDistrict", "mezzeh");

        await using var scope = Factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider
            .GetRequiredService<IHubContext<DashboardHub>>();

        await hubContext.Clients.Group("mezzeh")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid() }]);

        await Task.Delay(200);

        received.Should().BeEmpty();

        await conn.StopAsync();
    }

    [Test]
    [NotInParallel(Order = 1004)]
    public async Task Client_Not_In_District_Does_Not_Receive_GroupMessage()
    {
        var conn = BuildConnection(TestAuthHandler.ValidToken);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", payload =>
            received.Add(payload.ToString()!));

        await conn.StartAsync();
        await conn.InvokeAsync("JoinDistrict", "malki"); // joined malki, not mezzeh

        await using var scope = Factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider
            .GetRequiredService<IHubContext<DashboardHub>>();

        await hubContext.Clients.Group("mezzeh")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid() }]);

        await Task.Delay(200);

        received.Should().BeEmpty();

        await conn.StopAsync();
    }
}