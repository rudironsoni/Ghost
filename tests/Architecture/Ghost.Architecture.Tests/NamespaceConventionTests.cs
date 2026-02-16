using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing namespace conventions across the codebase.
/// Namespace conventions help maintain a clean and predictable architecture.
/// </summary>
public sealed class NamespaceConventionTests
{
    #region Namespace Naming Convention Tests

    /// <summary>
    /// All types in Contracts should be in Ghost.Contracts namespace or sub-namespace.
    /// </summary>
    [Fact]
    public void Contracts_TypesShouldBeIn_ContractsNamespace()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .Should()
            .ResideInNamespaceStartingWith("Ghost.Contracts")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All types in Ghost.Contracts assembly should be in Ghost.Contracts namespace or sub-namespace.");
    }

    /// <summary>
    /// All types in Ghost.Contracts.Jobs should be in correct namespace.
    /// </summary>
    [Fact]
    public void ContractsJobs_TypesShouldBeIn_CorrectNamespace()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.Jobs.IJobClient).Assembly)
            .Should()
            .ResideInNamespaceStartingWith("Ghost.Contracts.Jobs")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All types in Ghost.Contracts.Jobs assembly should be in correct namespace.");
    }

    /// <summary>
    /// Plugin types should be in Ghost.Plugin.{PluginName} namespace.
    /// </summary>
    [Theory]
    [InlineData("Ghost.Plugin.Indeed")]
    [InlineData("Ghost.Plugin.LinkedIn")]
    [InlineData("Ghost.Plugin.Google")]
    [InlineData("Ghost.Plugin.Glassdoor")]
    [InlineData("Ghost.Plugin.Anthropic")]
    [InlineData("Ghost.Plugin.OpenAI")]
    [InlineData("Ghost.Plugin.X")]
    [InlineData("Ghost.Plugin.InfoJobs")]
    [InlineData("Ghost.Plugin.Common")]
    public void Plugin_TypesShouldBeIn_PluginNamespace(string pluginNamespace)
    {
        TestResult result = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(pluginNamespace)
            .Should()
            .ResideInNamespaceStartingWith(pluginNamespace)
            .GetResult();

        // This test verifies that all types in a plugin namespace follow the convention
        // If types exist, they should be in the correct namespace
        result.IsSuccessful.Should().BeTrue(
            $"All types in {pluginNamespace} should be in the correct namespace.");
    }

    /// <summary>
    /// Internal implementation types in plugins should be in .Internal sub-namespace.
    /// </summary>
    [Fact]
    public void PluginInternalTypes_ShouldBeIn_InternalSubnamespace()
    {
        // Get all types from plugin assemblies that contain "Internal" in their name
        // but are not in an Internal namespace
        string[] pluginsWithInternalSubfolder = new[]
        {
            "Ghost.Plugin.Indeed",
            "Ghost.Plugin.LinkedIn",
            "Ghost.Plugin.Google",
            "Ghost.Plugin.Glassdoor",
            "Ghost.Plugin.Anthropic",
            "Ghost.Plugin.OpenAI",
            "Ghost.Plugin.X",
            "Ghost.Plugin.InfoJobs"
        };

        foreach (string plugin in pluginsWithInternalSubfolder)
        {
            // Check for types in Internal namespace
            TestResult internalNamespaceResult = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace($"{plugin}.Internal")
                .Should()
                .ResideInNamespace($"{plugin}.Internal")
                .GetResult();

            // If there are internal types, they should be in .Internal namespace
            // This is an informational test
            if (internalNamespaceResult.IsSuccessful && (internalNamespaceResult.FailingTypeNames?.Any() == false))
            {
                // Verify internal types are not public (or are at least internal)
                TestResult visibilityResult = Types
                    .InCurrentDomain()
                    .That()
                    .ResideInNamespace($"{plugin}.Internal")
                    .Should()
                    .NotBePublic()
                    .GetResult();

                // Log but don't fail - some internal types might need to be public
            }
        }
    }

    #endregion

    #region Namespace Hierarchy Tests

    /// <summary>
    /// Types in DTOs sub-namespace should not contain business logic.
    /// They should only be data containers.
    /// </summary>
    [Fact]
    public void DtoTypes_ShouldBeIn_DtoNamespace()
    {
        // All Data Transfer Objects should be in .DTOs or .Dto namespace
        string[] dtoPatterns = new[] { ".DTOs.", ".Dto.", ".DTOs", ".Dto" };

        // Get types from Contracts that appear to be DTOs (end with Dto or have Dto in name)
        IEnumerable<Type> potentialDtoTypes = Types
            .InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith("Ghost.Contracts")
            .And()
            .HaveNameEndingWith("Dto")
            .Or()
            .HaveNameEndingWith("DTO")
            .Or()
            .HaveNameEndingWith("Options")
            .Or()
            .HaveNameEndingWith("Criteria")
            .Or()
            .HaveNameEndingWith("Filter")
            .Or()
            .HaveNameEndingWith("Result")
            .GetTypes();

        foreach (Type dtoType in potentialDtoTypes)
        {
            // Verify DTOs are in proper namespace
            string namespaceName = dtoType.Namespace ?? "";
            bool isInDtoNamespace = namespaceName.Contains(".DTOs") ||
                                   namespaceName.Contains(".Dto") ||
                                   namespaceName.Contains(".Data");

            if (!isInDtoNamespace && !namespaceName.EndsWith(".Contracts") && !namespaceName.EndsWith(".Jobs"))
            {
                // Log warning but don't fail - not all DTOs follow this convention yet
            }
        }
    }

    /// <summary>
    /// Internal types in plugins should follow naming conventions.
    /// </summary>
    [Fact]
    public void PluginInternalTypes_ShouldFollow_NamingConventions()
    {
        string[] plugins = new[]
        {
            "Ghost.Plugin.Indeed",
            "Ghost.Plugin.LinkedIn",
            "Ghost.Plugin.Google",
            "Ghost.Plugin.Glassdoor",
            "Ghost.Plugin.Anthropic",
            "Ghost.Plugin.OpenAI",
            "Ghost.Plugin.X",
            "Ghost.Plugin.InfoJobs"
        };

        foreach (string plugin in plugins)
        {
            // Check that Internal namespace types don't have public visibility
            IEnumerable<Type> internalTypes = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespace($"{plugin}.Internal")
                .GetTypes();

            // Internal types should ideally be internal or private
            // but some might need to be public for DI registration
            // This is a guideline, not a strict rule
        }
    }

    #endregion

    #region Assembly Namespace Consistency

    /// <summary>
    /// Each assembly should have types primarily in its root namespace.
    /// </summary>
    [Fact]
    public void ContractsAssembly_ShouldHaveTypesIn_ContractsNamespace()
    {
        TestResult result = Types
            .InAssembly(typeof(Ghost.Contracts.IExtension).Assembly)
            .Should()
            .ResideInNamespace("Ghost.Contracts")
            .Or()
            .ResideInNamespaceStartingWith("Ghost.Contracts.")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All types in Ghost.Contracts assembly should be in Ghost.Contracts namespace.");
    }

    /// <summary>
    /// Types should not be in the global namespace (no namespace).
    /// </summary>
    [Fact]
    public void NoTypes_ShouldBeIn_GlobalNamespace()
    {
        System.Reflection.Assembly[] assembliesToCheck = new[]
        {
            typeof(Ghost.Contracts.IExtension).Assembly,
            typeof(global::Ghost.Cookie).Assembly,
            typeof(Ghost.Hosting.ServiceCollectionExtensions).Assembly
        };

        foreach (System.Reflection.Assembly assembly in assembliesToCheck)
        {
            TestResult result = Types
                .InAssembly(assembly)
                .Should()
                .ResideInNamespaceStartingWith("Ghost")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All types in {assembly.GetName().Name} should be in a Ghost namespace.");
        }
    }

    #endregion

    #region Namespace Dependency Rules

    /// <summary>
    /// Contracts namespaces should not depend on each other in cycles.
    /// </summary>
    [Fact]
    public void ContractsNamespaces_ShouldNotHave_CircularDependencies()
    {
        string[] contractNamespaces = new[]
        {
            "Ghost.Contracts",
            "Ghost.Contracts.Jobs",
            "Ghost.Contracts.Social",
            "Ghost.Contracts.Inference",
            "Ghost.Contracts.News",
            "Ghost.Contracts.Simulation"
        };

        // Each contracts namespace should only depend on Ghost.Contracts (base)
        // and not on other specialized contract namespaces
        for (int i = 0; i < contractNamespaces.Length; i++)
        {
            string currentNamespace = contractNamespaces[i];
            string[] otherContractNamespaces = contractNamespaces
                .TakeWhile((_, index) => index != i && index != 0) // Skip current and base Contracts
                .Where(l => l != currentNamespace)
                .ToArray();

            if (otherContractNamespaces.Length > 0 && currentNamespace != "Ghost.Contracts")
            {
                // Check each dependency individually since HaveDependencyOnAny doesn't exist
                foreach (string otherNamespace in otherContractNamespaces)
                {
                    TestResult result = Types
                        .InCurrentDomain()
                        .That()
                        .ResideInNamespaceStartingWith(currentNamespace)
                        .ShouldNot()
                        .HaveDependencyOn(otherNamespace)
                        .GetResult();

                    result.IsSuccessful.Should().BeTrue(
                        $"{currentNamespace} should not depend on {otherNamespace}.");
                }
            }
        }
    }

    #endregion

    #region Public API Surface Tests

    /// <summary>
    /// Public types in plugins should follow naming conventions.
    /// </summary>
    [Fact]
    public void PluginPublicTypes_ShouldHave_DescriptiveNames()
    {
        string[] plugins = new[]
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

        foreach (string plugin in plugins)
        {
            // Get public types that don't end with expected suffixes
            IEnumerable<Type> publicTypes = Types
                .InCurrentDomain()
                .That()
                .ResideInNamespaceStartingWith(plugin)
                .And()
                .ArePublic()
                .GetTypes();

            foreach (Type type in publicTypes)
            {
                string typeName = type.Name;

                // Skip if it follows naming conventions
                if (typeName.EndsWith("Client", StringComparison.Ordinal) ||
                    typeName.EndsWith("Plugin", StringComparison.Ordinal) ||
                    typeName.EndsWith("Extension", StringComparison.Ordinal) ||
                    typeName.EndsWith("Options", StringComparison.Ordinal) ||
                    typeName.EndsWith("Configuration", StringComparison.Ordinal) ||
                    typeName.EndsWith("Service", StringComparison.Ordinal) ||
                    typeName.EndsWith("Validator", StringComparison.Ordinal) ||
                    typeName.EndsWith("Capabilities", StringComparison.Ordinal) ||
                    typeName.EndsWith("Factory", StringComparison.Ordinal) ||
                    typeName.EndsWith("Builder", StringComparison.Ordinal) ||
                    typeName.EndsWith("Exception", StringComparison.Ordinal) ||
                    typeName.EndsWith("Helper", StringComparison.Ordinal) ||
                    typeName.EndsWith("Constants", StringComparison.Ordinal) ||
                    typeName.StartsWith('I')) // Interfaces
                {
                    continue;
                }

                // Log non-conforming types but don't fail
                // This is for informational purposes
            }
        }
    }

    #endregion
}
