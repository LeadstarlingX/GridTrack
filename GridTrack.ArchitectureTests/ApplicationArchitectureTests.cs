using NetArchTest.Rules;

namespace GridTrack.ArchitectureTests;

public class ApplicationArchitectureTests : ArchitectureTest
{
    [Test]
    public async Task Application_Should_Not_Depend_On_Higher_Layers()
    {
        var shouldNotDependOn = new[]
        {
            InfrastructureNamespace,
            PresentationNamespace,
            ApiNamespace
        };

        foreach (var ns in shouldNotDependOn)
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .ShouldNot()
                .HaveDependencyOn(ns)
                .GetResult();

            await Assert.That(result.IsSuccessful).IsTrue();
        }
    }

    [Test]
    public async Task Application_Should_Not_Depend_On_EF_Core()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
}
