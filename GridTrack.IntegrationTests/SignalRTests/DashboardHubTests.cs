using FluentAssertions;
using GridTrack.Domain.DistrictGroups;
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
            .WithUrl("http://localhost/hubs/dashboard", o =>
            {
                o.HttpMessageHandlerFactory = _ => handler;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
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

    [Test]
    [NotInParallel(Order = 1005)]
    public async Task JoinDistrictGroup_Allows_Client_To_Receive_DistrictGroup_Messages()
    {
        var groupId = Guid.NewGuid();
        var conn    = BuildConnection(TestAuthHandler.ValidToken);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", payload => received.Add(payload.ToString()!));

        await conn.StartAsync();
        await conn.InvokeAsync("JoinDistrictGroup", groupId.ToString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DashboardHub>>();

        await hubContext.Clients.Group($"dg:{groupId}")
            .SendCoreAsync("DriverPositionUpdated",
                [new { driverId = Guid.NewGuid(), lat = 33.5, lng = 36.2 }]);

        await Task.Delay(200);

        received.Should().HaveCount(1);

        await conn.StopAsync();
    }

    [Test]
    [NotInParallel(Order = 1006)]
    public async Task LeaveDistrictGroup_Stops_Receiving_DistrictGroup_Messages()
    {
        var groupId  = Guid.NewGuid();
        var conn     = BuildConnection(TestAuthHandler.ValidToken);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", payload => received.Add(payload.ToString()!));

        await conn.StartAsync();
        await conn.InvokeAsync("JoinDistrictGroup", groupId.ToString());
        await conn.InvokeAsync("LeaveDistrictGroup", groupId.ToString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DashboardHub>>();

        await hubContext.Clients.Group($"dg:{groupId}")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid() }]);

        await Task.Delay(200);

        received.Should().BeEmpty();

        await conn.StopAsync();
    }
    
    [Test]
    [NotInParallel(Order = 1007)]
    public async Task Observer_AutoJoins_Sector_Group_On_Connect()
    {
        // Seed a district group and ensure the cache picks it up.
        var groupId = Guid.NewGuid();
        await SeedAsync(ctx =>
        {
            ctx.Set<DistrictGroup>().Add(DistrictGroup.Create(groupId, "Test Sector", ["mezzeh"]).Value);
            return Task.CompletedTask;
        });
    
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IDistrictGroupCache>().Invalidate();
    
        // Connect with an Observer token that carries this sector's ID.
        var token    = TestAuthHandler.ObserverSectorPrefix + groupId;
        var conn     = BuildConnection(token);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", m => received.Add(m.ToString()!));
    
        await conn.StartAsync();
        await conn.InvokeAsync<long>("Ping", 0L);
    
        // Send to the sector group — no explicit JoinDistrictGroup call.
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DashboardHub>>();
        await hubContext.Clients.Group($"dg:{groupId}")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid() }]);
    
        await Task.Delay(300);
    
        received.Should().HaveCount(1);
    
        await conn.StopAsync();
        await ResetDatabaseAsync();
    }

    [Test]
    [NotInParallel(Order = 1008)]
    public async Task GeneralObserver_AutoJoins_All_Groups_On_Connect()
    {
        var groupId = Guid.NewGuid();
        await SeedAsync(ctx =>
        {
            ctx.Set<DistrictGroup>().Add(DistrictGroup.Create(groupId, "Test Sector 2", ["malki"]).Value);
            return Task.CompletedTask;
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IDistrictGroupCache>().Invalidate();

        var conn     = BuildConnection(TestAuthHandler.GeneralObserverToken);
        var received = new List<string>();
        conn.On<object>("DriverPositionUpdated", m => received.Add(m.ToString()!));

        await conn.StartAsync();
        await conn.InvokeAsync<long>("Ping", 0L);

        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DashboardHub>>();
        await hubContext.Clients.Group($"dg:{groupId}")
            .SendCoreAsync("DriverPositionUpdated", [new { driverId = Guid.NewGuid() }]);

        await Task.Delay(300);

        received.Should().HaveCount(1);

        await conn.StopAsync();
        await ResetDatabaseAsync();
    }
    
}