

using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace GridTrack.IntegrationTests.Abstractions;

public abstract class BaseIntegrationTest
{
    protected IntegrationTestWebAppFactory factory = null!;
    protected IServiceScope _scope = null!;
    protected IMessageBus Sender = null!;
    
    [ClassDataSource<IntegrationTestWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static IntegrationTestWebAppFactory Factory { get; set; } = null!;
    
    [Before(Test)]
    public async Task SetupTest()
    {
        factory = Factory;

        _scope = factory.Services.CreateScope();
        Sender = _scope.ServiceProvider.GetRequiredService<IMessageBus>();
    }

    [After(Test)]
    public async ValueTask TeardownTest()
    {
        if (_scope is IAsyncDisposable asyncScope)
            await asyncScope.DisposeAsync();
        else
            _scope.Dispose();
    }
    
}