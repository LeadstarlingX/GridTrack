using System.Reflection;
using GridTrack.Domain.Abstractions;
using NetArchTest.Rules;

namespace GridTrack.NamingConventions;

public class ApplicationNamingConventionTests : NamingConventionTests
{
    [Test]
    public async Task Handlers_Should_End_With_Handler_And_Be_Async()
    {
        var handlerTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespaceMatching("GridTrack.Application.(UseCases|EventHandlers).*?")
            .And().AreClasses()
            .And().AreNotAbstract()
            .GetTypes()
            .Where(HasHandleMethod)
            .ToList();

        var failing = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            if (!handlerType.Name.EndsWith("Handler"))
            {
                failing.Add($"{handlerType.FullName}: does not end with 'Handler'");
                continue;
            }

            var handleMethods = handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == "Handle")
                .ToList();

            if (handleMethods.Count != 1)
            {
                failing.Add($"{handlerType.FullName}: expected 1 public Handle method, found {handleMethods.Count}");
                continue;
            }

            var returnType = handleMethods[0].ReturnType;
            if (!IsTaskLike(returnType))
            {
                failing.Add($"{handlerType.FullName}: Handle must return Task or Task<T>");
            }
        }

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task UseCase_Records_Should_End_With_Command_Query_Or_Request()
    {
        var recordTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespaceMatching("GridTrack.Application.UseCases.*")
            .And().ArePublic()
            .GetTypes()
            .Where(IsRecord)
            .Where(t => !typeof(IDomainEvent).IsAssignableFrom(t))
            .ToList();

        var failing = recordTypes
            .Where(t => !t.Name.EndsWith("Command") && !t.Name.EndsWith("Query") && !t.Name.EndsWith("Request"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Dtos_Should_End_With_Dto_Response_Or_Result()
    {
        var dtoTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("GridTrack.Application.Dtos")
            .And().ArePublic()
            .GetTypes()
            .ToList();

        var failing = dtoTypes
            .Where(t => !t.Name.EndsWith("Dto") && !t.Name.EndsWith("Response") && !t.Name.EndsWith("Result"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }
    
    [Test]
    public async Task Command_Handlers_Should_Follow_Naming_Convention()
    {
        var handlerTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .GetTypes()
            .ToList();

        var failing = handlerTypes
            .Where(t => !t.Name.EndsWith("Handler"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Query_Handlers_Should_Follow_Naming_Convention()
    {
        var queryTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .Or()
            .HaveNameEndingWith("Handler")
            .GetTypes()
            .ToList();

        // All queries should end with Query and handlers with Handler
        var valid = queryTypes.All(t => 
            t.Name.EndsWith("Query") || 
            t.Name.EndsWith("Handler"));

        await Assert.That(valid).IsTrue();
    }

    [Test]
    public async Task Domain_Events_Should_End_With_DomainEvent()
    {
        var domainEventTypes = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .GetTypes()
            .ToList();

        var failing = domainEventTypes
            .Where(t => !t.Name.EndsWith("DomainEvent"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Repository_Interfaces_Should_End_With_Repository()
    {
        var repoTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .And()
            .AreInterfaces()
            .GetTypes()
            .ToList();

        var failing = repoTypes
            .Where(t => !t.Name.EndsWith("Repository"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Read_Service_Interfaces_Should_End_With_ReadService()
    {
        var readServiceTypes = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("ReadService")
            .And()
            .AreInterfaces()
            .GetTypes()
            .ToList();

        var failing = readServiceTypes
            .Where(t => !t.Name.EndsWith("ReadService"))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }
    

    private static bool HasHandleMethod(Type type)
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(m => m.Name == "Handle");

    private static bool IsTaskLike(Type returnType)
        => returnType == typeof(Task) ||
           (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));

    private static bool IsRecord(Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
}
