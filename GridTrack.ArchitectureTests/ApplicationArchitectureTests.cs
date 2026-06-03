using System.Reflection;
using GridTrack.Domain.Abstractions;
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

    [Test]
    public async Task Application_EventHandlers_Namespace_Should_Be_Empty()
    {
        var types = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("GridTrack.Application.EventHandlers")
            .GetTypes()
            .ToList();

        await Assert.That(types).IsEmpty();
    }

    [Test]
    public async Task Broadcast_Handlers_Should_Reside_In_CQRS_Handlers_Namespace()
    {
        var broadcastHandlers = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("BroadcastHandler")
            .GetTypes()
            .ToList();

        var failing = broadcastHandlers
            .Where(t => t.Namespace != "GridTrack.Application.CQRS.Handlers")
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Application_Handlers_Must_Not_Return_Domain_Event_Types()
    {
        var failing = new List<string>();

        foreach (var type in ApplicationAssembly.GetTypes().Where(t => t.Name.EndsWith("Handler")))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                         .Where(m => m.Name == "Handle"))
            {
                var ret = method.ReturnType;
                var inner = ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(Task<>)
                    ? ret.GetGenericArguments()[0]
                    : ret;

                if (inner != typeof(IDomainEvent) && typeof(IDomainEvent).IsAssignableFrom(inner))
                    failing.Add($"{type.Name}.Handle returns {inner.Name}");
            }
        }

        await Assert.That(failing).IsEmpty();
    }

}
