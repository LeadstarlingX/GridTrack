

namespace GridTrack.IntegrationTests.Abstractions;

public abstract class BaseIntegrationTest
{
    
    [ClassDataSource<IntegrationTestWebAppFactory>(Shared = SharedType.PerAssembly)]
    public static IntegrationTestWebAppFactory factory { get; set; }
    
}