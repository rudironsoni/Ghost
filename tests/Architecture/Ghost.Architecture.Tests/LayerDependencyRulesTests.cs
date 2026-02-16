using FluentAssertions;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Hosting;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing layer dependency rules:
/// Contracts -> (no internal dependencies)
/// Kernel -> Contracts only
/// Platform -> Kernel, Contracts
/// Engine -> Contracts, Engine.Abstractions
/// Plugins -> Contracts, Kernel, Sdk, Hosting (abstractions only)
/// </summary>
public sealed class LayerDependencyRulesTests
{
    #region Contracts Layer Tests

    [Fact]
    public void Contracts_ShouldNotDependOn_Kernel()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Plugins()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Hosting()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Contracts_ShouldNotDependOn_Engine()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Engine")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    #endregion

    #region Engine Abstractions Tests

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Kernel()
    {
        TestResult result = Types
            .InAssembly(typeof(IGhostEngine).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Plugins()
    {
        TestResult result = Types
            .InAssembly(typeof(IGhostEngine).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Hosting()
    {
        TestResult result = Types
            .InAssembly(typeof(IGhostEngine).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    #endregion

    #region Kernel Layer Tests

    [Fact]
    public void Kernel_ShouldOnlyDependOn_Contracts()
    {
        // Kernel should NOT depend on Plugins
        // Use global::Ghost.Cookie as a representative type from the Ghost namespace
        TestResult result = Types
            .InAssembly(typeof(global::Ghost.Cookie).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_Hosting()
    {
        // Use global::Ghost.Cookie as a representative type from the Ghost namespace
        TestResult result = Types
            .InAssembly(typeof(global::Ghost.Cookie).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_EngineImplementations()
    {
        // Kernel can use Engine.Abstractions but not Engine implementations
        // Use global::Ghost.Cookie as a representative type from the Ghost namespace
        TestResult result = Types
            .InAssembly(typeof(global::Ghost.Cookie).Assembly)
            .That()
            .ResideInNamespace("Ghost")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Engine.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    #endregion

    #region Hosting Layer Tests

    [Fact]
    public void Hosting_ShouldNotDependOn_Plugins()
    {
        TestResult result = Types
            .InAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    #endregion

    #region Sdk Layer Rules

    [Fact]
    public void Sdk_ShouldNotDependOn_Plugins()
    {
        // SDK provides utilities that plugins use, so Sdk should not depend on plugins
        TestResult result = Types
            .InAssembly(typeof(Ghost.Sdk.Spider.Engine.Spider).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    #endregion
}
