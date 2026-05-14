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
    public async Task Repository_Interfaces_Should_Reside_In_Application()
    {
        var repositoryTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .ToList();

        var failing = repositoryTypes
            .Where(t => !t.IsInterface)
            .Select(t => t.FullName ?? t.Name)
            .ToList();
        
        await Assert.That(failing).IsEmpty();
        
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
    
    [Test]
    public async Task Application_Should_Not_Depend_On_SignalR()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.SignalR")
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
    
}
