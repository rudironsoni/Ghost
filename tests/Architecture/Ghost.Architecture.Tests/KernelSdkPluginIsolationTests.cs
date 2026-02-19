using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Enforces the FW-001 guardrail:
/// Kernel and SDK MUST NOT depend on plugin implementations.
/// </summary>
public sealed class KernelSdkPluginIsolationTests
{
    private const string KernelProjectPath = "src/Kernel/Ghost/Ghost.csproj";

    private static readonly string[] SdkProjectPaths =
    {
        "src/Sdk/Ghost.Sdk/Ghost.Sdk.csproj",
        "src/Sdk/Ghost.Sdk.Spider/Ghost.Sdk.Spider.csproj"
    };

    [Fact]
    public void KernelProject_ShouldNotReference_PluginProjects()
    {
        string[] pluginReferences = GetProjectReferences(KernelProjectPath)
            .Where(IsPluginProjectReference)
            .ToArray();

        pluginReferences.Should().BeEmpty(
            $"Kernel project MUST NOT reference plugin projects. Found: {string.Join(", ", pluginReferences)}");
    }

    [Theory]
    [MemberData(nameof(GetSdkProjectPaths))]
    public void SdkProjects_ShouldNotReference_PluginProjects(string sdkProjectPath)
    {
        string[] pluginReferences = GetProjectReferences(sdkProjectPath)
            .Where(IsPluginProjectReference)
            .ToArray();

        pluginReferences.Should().BeEmpty(
            $"SDK project '{sdkProjectPath}' MUST NOT reference plugin projects. Found: {string.Join(", ", pluginReferences)}");
    }

    [Fact]
    public void KernelAssembly_ShouldNotReference_PluginAssemblies()
    {
        string[] pluginAssemblyReferences = GetPluginAssemblyReferences(typeof(global::Ghost.Cookie).Assembly);

        pluginAssemblyReferences.Should().BeEmpty(
            $"Kernel assembly MUST NOT reference plugin assemblies. Found: {string.Join(", ", pluginAssemblyReferences)}");
    }

    [Fact]
    public void SdkAssemblies_ShouldNotReference_PluginAssemblies()
    {
        Assembly[] sdkAssemblies =
        {
            typeof(Ghost.Sdk.Console.TelnetConfiguration).Assembly,
            typeof(Ghost.Sdk.Spider.Engine.Spider).Assembly
        };

        foreach (Assembly assembly in sdkAssemblies)
        {
            string[] pluginAssemblyReferences = GetPluginAssemblyReferences(assembly);

            pluginAssemblyReferences.Should().BeEmpty(
                $"SDK assembly '{assembly.GetName().Name}' MUST NOT reference plugin assemblies. " +
                $"Found: {string.Join(", ", pluginAssemblyReferences)}");
        }
    }

    public static IEnumerable<object[]> GetSdkProjectPaths()
    {
        return SdkProjectPaths.Select(path => new object[] { path });
    }

    private static IEnumerable<string> GetProjectReferences(string projectRelativePath)
    {
        string projectPath = Path.Combine(GetRepositoryRoot(), projectRelativePath);
        File.Exists(projectPath).Should().BeTrue($"Expected project file at '{projectPath}'.");

        XDocument projectDocument = XDocument.Load(projectPath);

        return projectDocument
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("ProjectReference", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!);
    }

    private static bool IsPluginProjectReference(string projectReference)
    {
        string normalized = projectReference.Replace('\\', '/');

        return normalized.Contains("/Plugins/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Ghost.Plugin.", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetPluginAssemblyReferences(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("Ghost.Plugin.", StringComparison.Ordinal))
            .Select(name => name!)
            .ToArray();
    }

    private static string GetRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        string? testProjectDirectory = Path.GetDirectoryName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(testProjectDirectory))
        {
            throw new InvalidOperationException("Could not determine test project directory.");
        }

        string repositoryRoot = Path.GetFullPath(Path.Combine(testProjectDirectory, "..", "..", ".."));
        string solutionPath = Path.Combine(repositoryRoot, "Ghost.sln");
        if (!File.Exists(solutionPath))
        {
            throw new InvalidOperationException("Could not locate repository root containing Ghost.sln.");
        }

        return repositoryRoot;
    }
}
