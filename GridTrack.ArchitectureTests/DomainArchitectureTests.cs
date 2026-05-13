using System.Reflection;
using GridTrack.Domain.Abstractions;
using NetArchTest.Rules;

namespace GridTrack.ArchitectureTests;

public class DomainArchitectureTests : ArchitectureTest
{
    [Test]
    public async Task Entities_Should_Have_Private_Constructors_And_Public_Factory_Methods()
    {
        var entityTypes = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(BaseEntity))
            .And()
            .AreNotAbstract()
            .GetTypes()
            .ToList();

        var failing = new List<string>();

        foreach (var entityType in entityTypes)
        {
            var ctors = entityType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
            var hasPrivateOrProtectedCtor = ctors.Any(c => c.IsPrivate || c.IsFamily);
            if (!hasPrivateOrProtectedCtor)
            {
                failing.Add($"{entityType.FullName}: missing private/protected ctor");
                continue;
            }

            var factoryMethods = entityType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == entityType ||
                            (m.ReturnType.IsGenericType && m.ReturnType.GetGenericArguments().First() == entityType))
                .ToList();

            if (!factoryMethods.Any())
            {
                failing.Add($"{entityType.FullName}: missing public static factory method returning the entity");
            }
        }

        await Assert.That(failing).IsEmpty();
    }
    

    [Test]
    public async Task ValueObjects_Should_Be_Immutable_Records()
    {
        var allTypes = Types.InAssembly(DomainAssembly)
            .That()
            .DoNotInherit(typeof(BaseEntity))
            .And().DoNotImplementInterface(typeof(IDomainEvent))
            .And().AreNotAbstract()
            .And().ArePublic()
            .GetTypes()
            .ToList();

        var valueObjectTypes = allTypes
            .Where(t => IsRecord(t))
            .Where(t => t.Namespace != DomainNamespace)
            .Where(t => !t.Name.EndsWith("Errors") && !t.Name.EndsWith("Service"))
            .ToList();

        var failing = new List<string>();

        foreach (var type in valueObjectTypes)
        {
            var mutableProperties = type.GetProperties()
                .Where(p => p.CanWrite && p.GetSetMethod(false) != null)
                .Select(p => p.Name)
                .ToList();

            if (mutableProperties.Any())
            {
                failing.Add($"{type.FullName}: mutable properties -> {string.Join(", ", mutableProperties)}");
            }
        }

        await Assert.That(failing).IsEmpty();
    }

    // Helper to detect C# record types (compiler artifact method)
    private static bool IsRecord(Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    [Test]
    public async Task DomainEvents_Should_Be_Sealed_Records()
    {
        var domainEventTypes = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .GetTypes()
            .ToList();

        var failing = domainEventTypes
            .Where(t => !t.IsSealed || !t.IsClass)
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Entities_Should_Inherit_From_BaseEntity()
    {
        var entityTypes = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(BaseEntity))
            .GetTypes()
            .ToList();

        var failing = entityTypes
            .Where(t => !t.IsSubclassOf(typeof(BaseEntity)))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Entities_Should_Have_Private_Setters()
    {
        var entityTypes = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(BaseEntity))
            .GetTypes()
            .ToList();

        var failing = new List<string>();

        foreach (var entityType in entityTypes)
        {
            var publicWritable = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToList();

            foreach (var prop in publicWritable)
            {
                var setMethod = prop.GetSetMethod(false);
                if (setMethod?.IsPublic == true)
                {
                    var isInitOnly = setMethod.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

                    if (!isInitOnly)
                    {
                        failing.Add($"{entityType.FullName}.{prop.Name} has a public setter");
                    }
                }
            }
        }

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task Repository_Interfaces_Should_Reside_In_Domain()
    {
        var repositoryTypes = Types.InAssembly(DomainAssembly)
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
    public async Task Entities_Should_Have_Private_Parameterless_Constructor()
    {
        var entityTypes = Types.InAssembly(DomainAssembly)
            .That()
            .Inherit(typeof(BaseEntity))
            .And()
            .AreNotAbstract()
            .GetTypes()
            .ToList();
        
        var failing = new List<string>();

        foreach (var entityType in entityTypes)
        {
            var ctors = entityType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
            var hasPrivateParameterless = ctors.Any(c => c.IsPrivate && c.GetParameters().Length == 0);
            if (!hasPrivateParameterless)
            {
                failing.Add(entityType.FullName ?? entityType.Name);
            }
        }

        await Assert.That(failing).IsEmpty();
    }

    [Test]
    public async Task DomainLayer_Should_Not_Depend_On_ApplicationLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }

    [Test]
    public async Task DomainEvents_Should_Have_DomainEvent_Suffix()
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
    public async Task Error_Classes_Should_Be_Static()
    {
        var errorClasses = Types.InAssembly(DomainAssembly)
            .That()
            .HaveNameEndingWith("Errors")
            .GetTypes()
            .ToList();

        var failing = errorClasses
            .Where(t => !t.IsAbstract || !t.IsSealed)
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        await Assert.That(failing).IsEmpty();
    }
    
    
    [Test]
    public async Task Domain_Should_Not_Depend_On_Higher_Layers()
    {
        var shouldNotDependOn = new[]
        {
            ApplicationNamespace,
            InfrastructureNamespace,
            PresentationNamespace,
            ApiNamespace
        };

        foreach (var ns in shouldNotDependOn)
        {
            var result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOn(ns)
                .GetResult();

            await Assert.That(result.IsSuccessful).IsTrue();
        }
    }

    [Test]
    public async Task Domain_Should_Not_Depend_On_EF_Core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
    
    
    [Test]
    public async Task ValueObjects_Should_Be_Sealed()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ResideInNamespaceMatching($"{DomainNamespace}.*\\.ValueObjects")
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        await Assert.That(result.IsSuccessful).IsTrue();
    }
    
}