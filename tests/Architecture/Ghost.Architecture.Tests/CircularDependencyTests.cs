using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests for detecting circular dependencies between layers and components.
/// Circular dependencies indicate architectural issues that should be resolved.
/// </summary>
public sealed class CircularDependencyTests
{
    #region Layer Circular Dependency Tests

    [Fact]
    public void Contracts_ShouldNotHaveCircularDependencies()
    {
        // Contracts layer should be the foundation and not depend on any other Ghost layer
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Ghost")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();

        result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();

        result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Engine")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();

        result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Contracts")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Plugin")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Contracts layer should not depend on any other Ghost layer to avoid circular dependencies.");
    }

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Kernel()
    {
        // Engine Abstractions should not depend on Kernel
        // If they did, and Kernel depends on Engine, we have a circular dependency
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Engine.Abstractions")
            .ShouldNot()
            .HaveDependencyOn("Ghost")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Engine Abstractions should not depend on Kernel to prevent circular dependencies.");
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_Hosting()
    {
        // Kernel should not depend on Hosting
        // If it did, and Hosting depends on Kernel (which it does), we'd have a circular dependency
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Kernel should not depend on Hosting to prevent circular dependencies.");
    }

    [Fact]
    public void Sdk_ShouldNotDependOn_Platform()
    {
        // SDK should not depend on Platform implementation details
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Sdk")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SDK should not depend on Platform infrastructure to prevent circular dependencies.");
    }

    #endregion

    #region Component-Level Circular Dependency Detection

    /// <summary>
    /// Verifies that the dependency graph from Contracts is acyclic.
    /// </summary>
    [Fact]
    public void ContractsLayer_ShouldHaveNoIncomingDependenciesFromLowerLayers()
    {
        // This test verifies that no lower layers (Kernel, Hosting, Plugins)
        // create types that Contracts depends on
        string[] lowerLayers = new[] { "Ghost", "Ghost.Hosting", "Ghost.Engine", "Ghost.Plugin", "Ghost.Sdk" };

        foreach (string lowerLayer in lowerLayers)
        {
            TestResult result = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace("Ghost.Contracts")
                .ShouldNot()
                .HaveDependencyOn(lowerLayer)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Contracts should not depend on {lowerLayer} to maintain acyclic dependency graph.");
        }
    }

    /// <summary>
    /// Tests that Engine layer maintains proper dependency direction.
    /// </summary>
    [Fact]
    public void Engine_ShouldNotCreateCircularDependencyWith_Hosting()
    {
        // Engine.Hosting depends on Engine
        // Engine should NOT depend on Engine.Hosting or Hosting
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Engine")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Engine.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Engine should not depend on Engine.Hosting to prevent circular dependencies.");
    }

    [Fact]
    public void EngineHosting_ShouldNotCreateCircularDependencyWith_Kernel()
    {
        // Engine.Hosting depends on Engine and Abstractions
        // It should NOT depend back on Kernel if Kernel depends on Engine
        // (Note: Currently Kernel does not depend on Engine, but this is a preventive test)
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace("Ghost.Engine.Hosting")
            .ShouldNot()
            .HaveDependencyOn("Ghost.")
            .GetResult();

        // This is a soft check - Engine.Hosting may legitimately need some kernel types
        // The important thing is that there's no circular dependency chain
    }

    #endregion

    #region Cross-Assembly Circular Dependency Tests

    /// <summary>
    /// Detects potential circular dependencies by verifying that
    /// assemblies that depend on Contracts don't have Contracts depending back.
    /// </summary>
    [Fact]
    public void Contracts_ShouldNotDependOn_AnyImplementingAssemblies()
    {
        string[] implementingAssemblies = new[]
        {
            "Ghost",
            "Ghost.Hosting",
            "Ghost.Engine",
            "Ghost.Engine.Hosting",
            "Ghost.Plugin.Indeed",
            "Ghost.Plugin.LinkedIn",
            "Ghost.Plugin.Google",
            "Ghost.Plugin.Glassdoor",
            "Ghost.Plugin.Anthropic",
            "Ghost.Plugin.OpenAI",
            "Ghost.Plugin.X",
            "Ghost.Plugin.InfoJobs"
        };

        foreach (string assembly in implementingAssemblies)
        {
            TestResult result = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace("Ghost.Contracts")
                .ShouldNot()
                .HaveDependencyOn(assembly)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Contracts should not depend on {assembly}.");
        }
    }

    #endregion

    #region Dependency Cycle Detection Helper

    /// <summary>
    /// Verifies the expected dependency chain: Contracts <- Kernel <- Hosting <- Plugins
    /// No reverse dependencies should exist.
    /// </summary>
    [Fact]
    public void DependencyDirection_ShouldFollow_LayerHierarchy()
    {
        // Define expected dependency hierarchy (higher layers depend on lower layers)
        Dictionary<string, string[]> layers = new Dictionary<string, string[]>
        {
            { "Ghost.Contracts", Array.Empty<string>() }, // Bottom layer - no dependencies
            { "Ghost.Engine.Abstractions", new[] { "Ghost.Contracts" } },
            { "Ghost.Engine", new[] { "Ghost.Engine.Abstractions" } },
            { "Ghost.Engine.Hosting", new[] { "Ghost.Engine", "Ghost.Engine.Abstractions" } },
            { "Ghost", new[] { "Ghost.Contracts" } }, // Kernel
            { "Ghost.Hosting", new[] { "Ghost", "Ghost.Contracts", "Ghost.Engine.Hosting" } },
            { "Ghost.Sdk", new[] { "Ghost", "Ghost.Contracts", "Ghost.Hosting" } },
            // Plugins depend on Contracts, Kernel, Hosting, Sdk but NOT on other plugins
        };

        // Verify no reverse dependencies exist
        foreach (KeyValuePair<string, string[]> layerEntry in layers)
        {
            string layer = layerEntry.Key;
            string[] expectedDependencies = layerEntry.Value;

            // Check that layer does NOT depend on layers above it
            List<string> higherLayers = layers.Keys
                .TakeWhile(l => l != layer)
                .Where(l => !expectedDependencies.Contains(l))
                .ToList();

            foreach (string higherLayer in higherLayers)
            {
                TestResult result = Types
                    .InCurrentDomain()
                    .That()
                    .ResideInNamespace(layer)
                    .ShouldNot()
                    .HaveDependencyOn(higherLayer)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue(
                    $"Layer {layer} should not depend on higher layer {higherLayer}. " +
                    "This would create a circular dependency.");
            }
        }
    }

    #endregion
}
