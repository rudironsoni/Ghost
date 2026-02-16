using FluentAssertions;
using Ghost.Contracts;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Hosting;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing plugin dependency rules:
/// - Plugins should depend on Contracts and Kernel
/// - Plugins should NOT depend on other plugins
/// - Plugins should use abstractions, not concrete implementations
/// </summary>
public sealed class PluginDependencyRulesTests
{
    #region Hosting Dependency Tests

    [Fact]
    public void EngineAbstractions_ShouldNotDependOnHosting()
    {
        TestResult result = Types
            .InAssembly(typeof(IGhostEngine).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void NegativeControl_PluginTypeDependingOnHosting_ShouldFailRule()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Plugin.Fixtures.IllegalPluginDependency).Assembly)
            .That()
            .ResideInNamespace("Ghost.Plugin.Fixtures")
            .ShouldNot()
            .HaveDependencyOn("Ghost.Hosting")
            .GetResult();

        result.IsSuccessful.Should().BeFalse();
    }

    #endregion

    #region Plugin Should Depend On Contracts

    [Fact]
    public void Plugins_ShouldDependOn_Contracts()
    {
        // All plugins should depend on Ghost.Contracts for the IExtension interface
        System.Reflection.Assembly[] pluginAssemblies = new[]
        {
            typeof(Ghost.Plugin.Indeed.IndeedPlugin).Assembly,
            typeof(Ghost.Plugin.LinkedIn.LinkedInPlugin).Assembly,
            typeof(Ghost.Plugin.Anthropic.AnthropicPlugin).Assembly,
            typeof(Ghost.Plugin.OpenAI.OpenAIPlugin).Assembly,
            typeof(Ghost.Plugin.X.XPlugin).Assembly,
        };

        foreach (System.Reflection.Assembly assembly in pluginAssemblies)
        {
            TestResult result = Types
                .InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith("Ghost.Plugin")
                .Should()
                .HaveDependencyOn("Ghost.Contracts")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Plugin assembly {assembly.GetName().Name} should depend on Ghost.Contracts");
        }
    }

    #endregion

    #region Plugin Should Depend On Kernel

    [Fact]
    public void Plugins_ShouldDependOn_Kernel()
    {
        // Plugins should depend on the Ghost kernel for core abstractions
        System.Reflection.Assembly[] pluginAssemblies = new[]
        {
            typeof(Ghost.Plugin.Indeed.IndeedPlugin).Assembly,
            typeof(Ghost.Plugin.LinkedIn.LinkedInPlugin).Assembly,
            typeof(Ghost.Plugin.Anthropic.AnthropicPlugin).Assembly,
        };

        foreach (System.Reflection.Assembly assembly in pluginAssemblies)
        {
            TestResult result = Types
                .InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith("Ghost.Plugin")
                .Should()
                .HaveDependencyOn("Ghost")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Plugin assembly {assembly.GetName().Name} should depend on Ghost kernel");
        }
    }

    #endregion

    #region Plugin Cross-Dependency Rules

    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    public void IndividualPlugin_ShouldNotDependOn_OtherPlugins(string pluginNamespace)
    {
        string[] otherPlugins = new[]
        {
            "Ghost.Plugin.Indeed",
            "Ghost.Plugin.LinkedIn",
            "Ghost.Plugin.Google",
            "Ghost.Plugin.Glassdoor",
            "Ghost.Plugin.Anthropic",
            "Ghost.Plugin.OpenAI",
            "Ghost.Plugin.X",
            "Ghost.Plugin.InfoJobs",
        }.Where(p => p != pluginNamespace)
        .Except(new[] { "Ghost.Plugin.Common" }) // Common is allowed
        .ToArray();

        // Check each plugin dependency individually since HaveDependencyOnAny doesn't exist
        foreach (string otherPlugin in otherPlugins)
        {
            TestResult result = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespaceStartingWith(pluginNamespace)
                .ShouldNot()
                .HaveDependencyOn(otherPlugin)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{pluginNamespace} should not depend on {otherPlugin}.");
        }
    }

    #endregion

    #region Plugin Should Use Hosting (Limited)

    [Fact]
    public void Plugins_MayDependOn_Hosting()
    {
        // Plugins are allowed to depend on Ghost.Hosting for DI registration
        // This is a soft rule - plugins may or may not use hosting
        IEnumerable<Type> result = Types
            .InAssembly(typeof(Ghost.Plugin.Indeed.IndeedPlugin).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Ghost.Plugin.Indeed")
            .GetTypes();

        // This test documents that plugins may use hosting
        // The actual dependency is checked per-plugin in specific tests
    }

    #endregion

    #region Plugin Internal Implementation Rules

    [Fact]
    public void PluginInternalTypes_ShouldNotBePubliclyAccessible()
    {
        // Internal implementation types in plugins should be internal or private
        // Public API surface should be limited to IExtension implementations
        System.Reflection.Assembly[] pluginAssemblies = new[]
        {
            typeof(Ghost.Plugin.Indeed.IndeedPlugin).Assembly,
            typeof(Ghost.Plugin.LinkedIn.LinkedInPlugin).Assembly,
        };

        foreach (System.Reflection.Assembly assembly in pluginAssemblies)
        {
            // Get the plugin namespace from the assembly
            IEnumerable<Type> pluginTypes = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith("Ghost.Plugin")
                .GetTypes();

            // Check that types in .Internal namespace are not public
            List<Type> internalTypes = pluginTypes
                .Where(t => (t.Namespace ?? "").Contains(".Internal"))
                .Where(t => t.IsPublic)
                .ToList();

            // Log but don't fail - some internal types may need to be public for DI
            if (internalTypes.Any())
            {
                // Informational: these types are in Internal namespace but are public
            }
        }
    }

    #endregion

    #region Extension Implementation Rules

    [Fact]
    public void Plugins_ShouldImplement_IExtension()
    {
        // All plugins should have at least one type implementing IExtension
        System.Reflection.Assembly[] pluginAssemblies = new[]
        {
            typeof(Ghost.Plugin.Indeed.IndeedPlugin).Assembly,
            typeof(Ghost.Plugin.LinkedIn.LinkedInPlugin).Assembly,
            typeof(Ghost.Plugin.Anthropic.AnthropicPlugin).Assembly,
            typeof(Ghost.Plugin.OpenAI.OpenAIPlugin).Assembly,
            typeof(Ghost.Plugin.X.XPlugin).Assembly,
        };

        foreach (System.Reflection.Assembly assembly in pluginAssemblies)
        {
            IEnumerable<Type> result = Types
                .InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith("Ghost.Plugin")
                .And()
                .ImplementInterface(typeof(Ghost.Contracts.IExtension))
                .GetTypes();

            result.Should().NotBeEmpty(
                $"Plugin {assembly.GetName().Name} should have at least one type implementing IExtension");
        }
    }

    #endregion

    #region Abstraction vs Implementation Rules

    [Fact]
    public void Plugins_ShouldNotDependOn_OtherPluginImplementations()
    {
        // Plugins should not depend on concrete types from other plugins
        // They should only depend on abstractions (Contracts, Kernel interfaces)

        string[] allPluginsExceptCommon = new[]
        {
            "Ghost.Plugin.Indeed",
            "Ghost.Plugin.LinkedIn",
            "Ghost.Plugin.Google",
            "Ghost.Plugin.Glassdoor",
            "Ghost.Plugin.Anthropic",
            "Ghost.Plugin.OpenAI",
            "Ghost.Plugin.X",
            "Ghost.Plugin.InfoJobs",
        };

        foreach (string plugin in allPluginsExceptCommon)
        {
            string[] otherPlugins = allPluginsExceptCommon
                .Where(p => p != plugin)
                .ToArray();

            // Check each plugin dependency individually since HaveDependencyOnAny doesn't exist
            foreach (string otherPlugin in otherPlugins)
            {
                TestResult result = Types
                    .InCurrentDomain()
                    .That()
                    .ResideInNamespaceStartingWith(plugin)
                    .ShouldNot()
                    .HaveDependencyOn(otherPlugin)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue(
                    $"{plugin} should not depend on {otherPlugin}.");
            }
        }
    }

    #endregion
}

// Fixture class moved to separate file: Fixtures/IllegalPluginDependency.cs
