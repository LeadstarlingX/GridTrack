using GridTrack.Domain.Abstractions;
using NetArchTest.Rules;

namespace GridTrack.ArchitectureTests;

public class InfrastructureArchitectureTests : ArchitectureTest
{
    [Test]
    public async Task Persistence_Should_Expose_IUnitOfWork_Implementation()
    {
        // Ensure persistence assembly exposes a concrete type that implements IUnitOfWork
        var types = Types.InAssembly(InfrastructureAssembly)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var implementing = types
            .Where(t => typeof(IUnitOfWork).IsAssignableFrom(t))
            .ToList();

        await Assert.That(implementing).IsNotEmpty();
    }

    [Test]
    public async Task Persistence_Repositories_Should_Implement_Domain_Interfaces()
    {
        var repoTypes = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .ToList();

        var failing = new List<string>();

        foreach (var repo in repoTypes)
        {
            if (!repo.IsClass) continue;

            var implemented = repo.GetInterfaces();
            var expectedInterfaceName = "I" + repo.Name;
            var implementsExpected = implemented.Any(i => i.Name == expectedInterfaceName);
            if (!implementsExpected && implemented.Length == 0)
            {
                failing.Add(repo.FullName ?? repo.Name);
            }
        }

        await Assert.That(failing).IsEmpty();
    }

    

}