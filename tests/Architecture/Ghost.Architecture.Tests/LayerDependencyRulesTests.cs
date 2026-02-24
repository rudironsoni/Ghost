using FluentAssertions;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Hosting;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing layer dependency rules:
/// Contracts -> (no internal dependencies)
/// Kernel -> Contracts only
/// Platform -> Kernel, Contracts
/// Engine -> Contracts, Engine.Abstractions
/// Plugins -> Contracts, Kernel, Sdk, Hosting (abstractions only)
/// </summary>
public sealed class LayerDependencyRulesTests : ReliabilityTestBase
{
    public LayerDependencyRulesTests(ITestOutputHelper output) : base(output) { }

    #region Contracts Layer Tests

    [Fact]
    public void Contracts_ShouldNotDependOn_Kernel()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(Ghost.Contracts.IExtension).Assembly,
            "Ghost.Contracts",
            "Ghost");

        hasDependency.Should().BeFalse("Contracts should not depend on Kernel");
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Plugins()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(Ghost.Contracts.IExtension).Assembly,
            "Ghost.Contracts",
            "Ghost.Plugin");

        hasDependency.Should().BeFalse("Contracts should not depend on Plugins");
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Hosting()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(Ghost.Contracts.IExtension).Assembly,
            "Ghost.Contracts",
            "Ghost.Hosting");

        hasDependency.Should().BeFalse("Contracts should not depend on Hosting");
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Engine()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(Ghost.Contracts.IExtension).Assembly,
            "Ghost.Contracts",
            "Ghost.Engine");

        hasDependency.Should().BeFalse("Contracts should not depend on Engine");
    }

    #endregion

    #region Engine Abstractions Tests

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Kernel()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(IGhostEngine).Assembly,
            "Ghost.Engine.Abstractions",
            "Ghost");

        hasDependency.Should().BeFalse(
            "Engine Abstractions should not depend on Kernel to maintain proper layer separation.");
    }

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Plugins()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(IGhostEngine).Assembly,
            "Ghost.Engine.Abstractions",
            "Ghost.Plugin");

        hasDependency.Should().BeFalse("Engine Abstractions should not depend on Plugins");
    }

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Hosting()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(IGhostEngine).Assembly,
            "Ghost.Engine.Abstractions",
            "Ghost.Hosting");

        hasDependency.Should().BeFalse("Engine Abstractions should not depend on Hosting");
    }

    #endregion

    #region Kernel Layer Tests

    [Fact]
    public void Kernel_ShouldOnlyDependOn_Contracts()
    {
        // Kernel should NOT depend on Plugins
        bool hasDependency = HasNamespaceDependency(
            typeof(global::Ghost.Cookie).Assembly,
            "Ghost",
            "Ghost.Plugin");

        hasDependency.Should().BeFalse("Kernel should not depend on Plugins");
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_Hosting()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(global::Ghost.Cookie).Assembly,
            "Ghost",
            "Ghost.Hosting");

        hasDependency.Should().BeFalse("Kernel should not depend on Hosting");
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_EngineImplementations()
    {
        // Kernel can use Engine.Abstractions but not Engine implementations
        bool hasDependency = HasNamespaceDependency(
            typeof(global::Ghost.Cookie).Assembly,
            "Ghost",
            "Ghost.Engine.Hosting");

        hasDependency.Should().BeFalse("Kernel should not depend on Engine implementations");
    }

    #endregion

    #region Hosting Layer Tests

    [Fact]
    public void Hosting_ShouldNotDependOn_Plugins()
    {
        bool hasDependency = HasNamespaceDependency(
            typeof(ServiceCollectionExtensions).Assembly,
            "Ghost.Hosting",
            "Ghost.Plugin");

        hasDependency.Should().BeFalse("Hosting should not depend on Plugins");
    }

    #endregion

    #region Sdk Layer Rules

    [Fact]
    public void Sdk_ShouldNotDependOn_Plugins()
    {
        // SDK provides utilities that plugins use, so Sdk should not depend on plugins
        bool hasDependency = HasNamespaceDependency(
            typeof(Ghost.Sdk.Spider.Engine.Spider).Assembly,
            "Ghost.Sdk",
            "Ghost.Plugin");

        hasDependency.Should().BeFalse("SDK should not depend on Plugins");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if types in the source namespace depend on types in the target namespace.
    /// </summary>
    private static bool HasNamespaceDependency(System.Reflection.Assembly assembly, string sourceNamespace, string targetNamespace)
    {
        Type[] sourceTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(sourceNamespace, StringComparison.Ordinal) == true
                     && !t.Namespace.StartsWith(targetNamespace, StringComparison.Ordinal))
            .ToArray();

        foreach (Type type in sourceTypes)
        {
            Type[] referencedTypes = GetReferencedTypes(type);
            if (referencedTypes.Any(rt => rt.Namespace?.StartsWith(targetNamespace, StringComparison.Ordinal) == true))
            {
                return true;
            }
        }

        return false;
    }

    private static Type[] GetReferencedTypes(Type type)
    {
        HashSet<Type> referencedTypes = new();

        // Check base type
        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            referencedTypes.Add(type.BaseType);
        }

        // Check interfaces
        foreach (Type interfaceType in type.GetInterfaces())
        {
            referencedTypes.Add(interfaceType);
        }

        // Check properties
        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
        {
            referencedTypes.Add(property.PropertyType);
        }

        // Check fields
        foreach (System.Reflection.FieldInfo field in type.GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
        {
            referencedTypes.Add(field.FieldType);
        }

        // Check methods
        foreach (System.Reflection.MethodInfo method in type.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
        {
            referencedTypes.Add(method.ReturnType);
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                referencedTypes.Add(parameter.ParameterType);
            }
        }

        // Check constructors
        foreach (System.Reflection.ConstructorInfo constructor in type.GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance))
        {
            foreach (System.Reflection.ParameterInfo parameter in constructor.GetParameters())
            {
                referencedTypes.Add(parameter.ParameterType);
            }
        }

        return referencedTypes.Where(t => t != null).ToArray();
    }

    #endregion
}
