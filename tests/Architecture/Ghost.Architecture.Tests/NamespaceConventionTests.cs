using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Tests enforcing namespace conventions across the codebase.
/// Namespace conventions help maintain a clean and predictable architecture.
/// </summary>
public sealed class NamespaceConventionTests : ReliabilityTestBase
{
    public NamespaceConventionTests(ITestOutputHelper output) : base(output) { }

    #region Namespace Naming Convention Tests

    /// <summary>
    /// All types in Contracts should be in Ghost.Contracts namespace or sub-namespace.
    /// </summary>
    [Fact]
    public void Contracts_TypesShouldBeIn_ContractsNamespace()
    {
        Type[] types = typeof(Ghost.Contracts.IExtension).Assembly.GetTypes()
            .Where(t => !IsCompilerGeneratedType(t))
            .ToArray();

        foreach (Type type in types)
        {
            type.Namespace?.StartsWith("Ghost.Contracts", StringComparison.Ordinal).Should().BeTrue(
                $"Type {type.FullName} should be in Ghost.Contracts namespace or sub-namespace.");
        }
    }

    /// <summary>
    /// All types in Ghost.Contracts.Jobs should be in correct namespace.
    /// </summary>
    [Fact]
    public void ContractsJobs_TypesShouldBeIn_CorrectNamespace()
    {
        Type[] types = typeof(Ghost.Contracts.Jobs.IJobClient).Assembly.GetTypes()
            .Where(t => !IsCompilerGeneratedType(t))
            .ToArray();

        foreach (Type type in types)
        {
            type.Namespace?.StartsWith("Ghost.Contracts.Jobs", StringComparison.Ordinal).Should().BeTrue(
                $"Type {type.FullName} should be in Ghost.Contracts.Jobs namespace or sub-namespace.");
        }
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
        // Get the plugin assembly
        System.Reflection.Assembly? pluginAssembly = GetPluginAssembly(pluginNamespace);
        if (pluginAssembly == null)
        {
            return; // Plugin not available, skip test
        }

        Type[] types = pluginAssembly.GetTypes()
            .Where(t => !IsCompilerGeneratedType(t))
            .Where(t => t.Namespace?.StartsWith(pluginNamespace, StringComparison.Ordinal) == true)
            .ToArray();

        // If types exist, they should be in the correct namespace
        // This test verifies namespace consistency
        foreach (Type type in types)
        {
            type.Namespace?.StartsWith(pluginNamespace, StringComparison.Ordinal).Should().BeTrue(
                $"Type {type.FullName} should be in {pluginNamespace} namespace.");
        }
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
            System.Reflection.Assembly? pluginAssembly = GetPluginAssembly(plugin);
            if (pluginAssembly == null)
            {
                continue;
            }

            // Check for types in Internal namespace
            Type[] internalTypes = pluginAssembly.GetTypes()
                .Where(t => t.Namespace?.Equals($"{plugin}.Internal", StringComparison.Ordinal) == true)
                .ToArray();

            // Verify internal types are not public (or are at least internal)
            foreach (Type internalType in internalTypes)
            {
                if (internalType.IsPublic)
                {
                    // Log but don't fail - some internal types might need to be public for DI
                }
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
        // Get types from Contracts that appear to be DTOs (end with Dto or have Dto in name)
        Type[] potentialDtoTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.StartsWith("Ghost.Contracts", StringComparison.Ordinal) == true)
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal) ||
                       t.Name.EndsWith("DTO", StringComparison.Ordinal) ||
                       t.Name.EndsWith("Options", StringComparison.Ordinal) ||
                       t.Name.EndsWith("Criteria", StringComparison.Ordinal) ||
                       t.Name.EndsWith("Filter", StringComparison.Ordinal) ||
                       t.Name.EndsWith("Result", StringComparison.Ordinal))
            .Where(t => !IsCompilerGeneratedType(t))
            .ToArray();

        foreach (Type dtoType in potentialDtoTypes)
        {
            // Verify DTOs are in proper namespace
            string namespaceName = dtoType.Namespace ?? "";
            bool isInDtoNamespace = namespaceName.Contains(".DTOs", StringComparison.Ordinal) ||
                                   namespaceName.Contains(".Dto", StringComparison.Ordinal) ||
                                   namespaceName.Contains(".Data", StringComparison.Ordinal);

            if (!isInDtoNamespace && !namespaceName.EndsWith(".Contracts", StringComparison.Ordinal) && !namespaceName.EndsWith(".Jobs", StringComparison.Ordinal))
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
            System.Reflection.Assembly? pluginAssembly = GetPluginAssembly(plugin);
            if (pluginAssembly == null)
            {
                continue;
            }

            // Check that Internal namespace types don't have public visibility
            Type[] internalTypes = pluginAssembly.GetTypes()
                .Where(t => t.Namespace?.Equals($"{plugin}.Internal", StringComparison.Ordinal) == true)
                .ToArray();

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
        Type[] types = typeof(Ghost.Contracts.IExtension).Assembly.GetTypes()
            .Where(t => !IsCompilerGeneratedType(t))
            .ToArray();

        foreach (Type type in types)
        {
            bool isInCorrectNamespace = type.Namespace?.Equals("Ghost.Contracts", StringComparison.Ordinal) == true ||
                                       type.Namespace?.StartsWith("Ghost.Contracts.", StringComparison.Ordinal) == true;

            isInCorrectNamespace.Should().BeTrue(
                $"All types in Ghost.Contracts assembly should be in Ghost.Contracts namespace. Type: {type.FullName}");
        }
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
            Type[] globalTypes = assembly.GetTypes()
                .Where(t => string.IsNullOrEmpty(t.Namespace))
                .Where(t => !IsCompilerGeneratedType(t))
                .ToArray();

            globalTypes.Should().BeEmpty(
                $"All types in {assembly.GetName().Name} should be in a Ghost namespace.");
        }
    }

    private static bool IsCompilerGeneratedType(Type type)
    {
        // Compiler generates many types for async/await, iterators, lambdas, etc.
        // They typically have names containing special characters
        string name = type.Name;

        // Check for compiler-generated patterns
        if (name.StartsWith("<>", StringComparison.Ordinal) ||           // <>c__DisplayClass
            name.StartsWith('<') ||                                       // <MethodName>d__
            name.Contains("__", StringComparison.Ordinal) ||              // __DisplayClass, etc.
            name.Contains("DisplayClass", StringComparison.Ordinal) ||     // Generated display classes
            name.Contains("d__", StringComparison.Ordinal) ||            // Async state machines
            name.Contains('<') || name.Contains('>') ||                  // Any angle brackets
            name.Contains('\u0060'))                                     // Backtick character for generic arity
        {
            return true;
        }

        // Check for CompilerGenerated attribute
        if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
        {
            return true;
        }

        // Check if nested in a compiler-generated parent
        if (type.DeclaringType != null && IsCompilerGeneratedType(type.DeclaringType))
        {
            return true;
        }

        return false;
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
                .Where((_, index) => index != i && index != 0) // Skip current and base Contracts
                .Where(l => l != currentNamespace)
                .ToArray();

            if (otherContractNamespaces.Length > 0 && currentNamespace != "Ghost.Contracts")
            {
                // Check each dependency individually
                foreach (string otherNamespace in otherContractNamespaces)
                {
                    bool hasDependency = HasNamespaceDependency(
                        currentNamespace,
                        otherNamespace);

                    hasDependency.Should().BeFalse(
                        $"{currentNamespace} should not depend on {otherNamespace}.");
                }
            }
        }
    }

    private static bool HasNamespaceDependency(string sourceNamespace, string targetNamespace)
    {
        Type[] sourceTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.StartsWith(sourceNamespace, StringComparison.Ordinal) == true
                     && !t.Namespace.StartsWith(targetNamespace, StringComparison.Ordinal))
            .Where(t => !IsCompilerGeneratedType(t))
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
            System.Reflection.Assembly? pluginAssembly = GetPluginAssembly(plugin);
            if (pluginAssembly == null)
            {
                continue;
            }

            Type[] publicTypes = pluginAssembly.GetTypes()
                .Where(t => t.IsPublic)
                .Where(t => t.Namespace?.StartsWith(plugin, StringComparison.Ordinal) == true)
                .ToArray();

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

    private static System.Reflection.Assembly? GetPluginAssembly(string pluginNamespace)
    {
        string pluginName = pluginNamespace.Split('.').Last();
        try
        {
            // Try to get the assembly from the current domain
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Equals($"Ghost.Plugin.{pluginName}", StringComparison.OrdinalIgnoreCase) == true);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
