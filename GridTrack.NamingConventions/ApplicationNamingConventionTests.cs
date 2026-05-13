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

    private static bool HasHandleMethod(Type type)
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(m => m.Name == "Handle");

    private static bool IsTaskLike(Type returnType)
        => returnType == typeof(Task) ||
           (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));

    private static bool IsRecord(Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
}
