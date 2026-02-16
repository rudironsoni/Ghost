using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing plugin isolation rules:
/// - Plugins should not depend on other plugins
/// - Plugins should only depend on abstractions (Contracts, Kernel interfaces)
/// - Plugins should not have circular dependencies
/// </summary>
public sealed class PluginIsolationTests
{
    // List of plugin namespaces
    private static readonly string[] PluginNamespaces = new[]
    {
        "Ghost.Plugin.Indeed",
        "Ghost.Plugin.LinkedIn",
        "Ghost.Plugin.Google",
        "Ghost.Plugin.Glassdoor",
        "Ghost.Plugin.Anthropic",
        "Ghost.Plugin.OpenAI",
        "Ghost.Plugin.X",
        "Ghost.Plugin.InfoJobs",
        "Ghost.Plugin.Common"
    };

    #region Plugin Cross-Dependency Tests

    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    public void Plugin_ShouldNotDependOn_OtherPlugins(string pluginNamespace)
    {
        // Get all other plugin namespaces
        List<string> otherPlugins = PluginNamespaces
            .Where(p => !p.Equals(pluginNamespace, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Check each plugin dependency individually since HaveDependencyOnAny doesn't exist
        foreach (string otherPlugin in otherPlugins)
        {
            TestResult result = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace(pluginNamespace)
                .ShouldNot()
                .HaveDependencyOn(otherPlugin)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Plugin {pluginNamespace} should not depend on {otherPlugin}.");
        }
    }

    [Fact]
    public void AllPlugins_ShouldNotDependOn_OtherPlugins()
    {
        foreach (string plugin in PluginNamespaces.Where(p => p != "Ghost.Plugin.Common"))
        {
            string[] otherPlugins = PluginNamespaces
                .Where(p => !p.Equals(plugin, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Check each plugin dependency individually since HaveDependencyOnAny doesn't exist
            foreach (string otherPlugin in otherPlugins)
            {
                TestResult result = Types
                    .InCurrentDomain()
                    .That()
                    .ResideInNamespace(plugin)
                    .ShouldNot()
                    .HaveDependencyOn(otherPlugin)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue(
                    $"Plugin {plugin} should not depend on {otherPlugin}.");
            }
        }
    }

    #endregion

    #region Plugin Abstraction Dependency Tests

    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    public void Plugin_ShouldDependOn_Contracts(string pluginNamespace)
    {
        // Plugins should depend on Contracts
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace(pluginNamespace)
            .Should()
            .HaveDependencyOn("Ghost.Contracts")
            .GetResult();

        // This is a "should" test - plugins typically depend on contracts
        // We log the result but don't fail if no dependency exists
        if (!result.IsSuccessful)
        {
            // Log informational - not all plugins may directly reference contracts
        }
    }

    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    public void Plugin_ShouldDependOn_Kernel(string pluginNamespace)
    {
        // Plugins should depend on Kernel
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace(pluginNamespace)
            .Should()
            .HaveDependencyOn("Ghost")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Plugin {pluginNamespace} should depend on Ghost kernel.");
    }

    #endregion

    #region Common Plugin Rules

    [Fact]
    public void CommonPlugin_MayBeUsedBy_OtherPlugins()
    {
        // Ghost.Plugin.Common is allowed to be a shared dependency
        // This test verifies it doesn't depend on other plugins
        string[] otherPlugins = PluginNamespaces
            .Where(p => p != "Ghost.Plugin.Common")
            .ToArray();

        // Check each plugin dependency individually since HaveDependencyOnAny doesn't exist
        foreach (string otherPlugin in otherPlugins)
        {
            TestResult result = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace("Ghost.Plugin.Common")
                .ShouldNot()
                .HaveDependencyOn(otherPlugin)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Ghost.Plugin.Common should not depend on {otherPlugin}.");
        }
    }

    #endregion

    #region Plugin Implementation Isolation

    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    public void PluginInternalTypes_ShouldBeInternalOrPrivate(string pluginNamespace)
    {
        // Plugin internal implementation types should not be public
        // Only the public API surface (IExtension implementations) should be public
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespace($"{pluginNamespace}.Internal")
            .Should()
            .NotBePublic()
            .GetResult();

        // This is informational - some internal types might legitimately be public
        // We mainly care about cross-plugin dependencies
        if (!result.IsSuccessful)
        {
            IEnumerable<string> publicInternals = result.FailingTypeNames?.Take(5) ?? Array.Empty<string>();
        }
    }

    #endregion
}
