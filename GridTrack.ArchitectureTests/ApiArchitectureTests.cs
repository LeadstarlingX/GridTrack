using NetArchTest.Rules;

namespace GridTrack.ArchitectureTests;

public class ApiArchitectureTests : ArchitectureTest
{
    [Test]
    public async Task Api_Should_Not_Depend_On_Domain()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn(DomainNamespace)
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
    
}