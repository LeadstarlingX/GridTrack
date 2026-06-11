using GridTrack.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GridTrack.Infrastructure.UnitTests.Hubs;

/// <summary>Records every SendCoreAsync call for test assertion.</summary>
internal sealed class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> Calls { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Calls.Add((method, args));
        return Task.CompletedTask;
    }
}

/// <summary>Captures the group name on each Group() call; uses a single GroupProxy for assertions.</summary>
internal sealed class FakeHubClients : IHubClients
{
    public string? LastGroupName { get; private set; }

    public FakeClientProxy GroupProxy { get; } = new();
    public FakeClientProxy AllProxy { get; } = new();

    public IClientProxy Group(string groupName)
    {
        LastGroupName = groupName;
        return GroupProxy;
    }

    public IClientProxy All => AllProxy;

    // Unused by DashboardPushService — throw so tests catch unexpected calls
    public IClientProxy Client(string connectionId) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
    public IClientProxy User(string userId) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
    public IClientProxy Others => throw new NotImplementedException();
    public IClientProxy Caller => throw new NotImplementedException();
}

/// <summary>Wires FakeHubClients into IHubContext&lt;DashboardHub&gt;.</summary>
internal sealed class FakeHubContext : IHubContext<DashboardHub>
{
    public FakeHubClients FakeClients { get; } = new();
    public IHubClients Clients => FakeClients;
    public IGroupManager Groups => throw new NotImplementedException();
}
