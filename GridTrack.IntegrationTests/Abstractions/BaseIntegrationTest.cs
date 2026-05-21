

using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace GridTrack.IntegrationTests.Abstractions;

public abstract class BaseIntegrationTest
{
    
    [ClassDataSource<IntegrationTestWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static IntegrationTestWebAppFactory Factory { get; set; } = null!;
    

    
}