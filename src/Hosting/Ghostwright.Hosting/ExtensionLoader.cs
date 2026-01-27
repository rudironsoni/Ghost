using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Hosting;

/// <summary>
/// Responsible for validating extension dependencies and loading them in a correct order.
/// </summary>
internal sealed class ExtensionLoader
{
    /// <summary>
    /// Validates the provided extensions for missing dependencies and cycles.
    /// </summary>
    /// <param name="extensions">List of extensions to validate.</param>
    public void ValidateExtensions(IReadOnlyList<IExtension> extensions)
    {
        if (extensions is null) ArgumentNullException.ThrowIfNull(extensions);

        // Collect all provided service types
        var provided = new HashSet<Type>();
        foreach (var ext in extensions)
        {
            foreach (var t in ext.ProvidedServices)
            {
                provided.Add(t);
            }
        }

        // Check required services are provided by some extension
        foreach (var ext in extensions)
        {
            foreach (var req in ext.RequiredServices)
            {
                if (!provided.Contains(req))
                {
                    throw new ExtensionException(ext.Name, $"Required service '{req.FullName}' is not provided by any extension.");
                }
            }
        }

        // Detect cycles by attempting topological sort
        TopologicalSort(extensions); // will throw on cycle
    }

    /// <summary>
    /// Load the extensions in dependency order by calling Register on each.
    /// </summary>
    /// <param name="extensions">Extensions to load.</param>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration instance.</param>
    public void LoadExtensions(IReadOnlyList<IExtension> extensions, IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Validate first
        ValidateExtensions(extensions);

        var ordered = TopologicalSort(extensions);

        foreach (var ext in ordered)
        {
            try
            {
                ext.Register(services, configuration);
            }
            catch (Exception ex)
            {
                throw new ExtensionException(ext.Name, $"Failed to register extension: {ex.Message}");
            }
        }
    }

    private static List<IExtension> TopologicalSort(IReadOnlyList<IExtension> extensions)
    {
        // Build dependency map: ext -> list of extensions it depends on
        var indexByName = extensions.Select((ext, idx) => (ext.Name, idx)).ToDictionary(x => x.Name, x => x.idx);

        var dependsOn = new Dictionary<IExtension, List<IExtension>>();
        foreach (var ext in extensions)
        {
            var list = new List<IExtension>();
            foreach (var req in ext.RequiredServices)
            {
                var provider = extensions.FirstOrDefault(e => e.ProvidedServices.Contains(req));
                if (provider != null && !ReferenceEquals(provider, ext))
                {
                    list.Add(provider);
                }
            }
            dependsOn[ext] = list;
        }

        var result = new List<IExtension>(extensions.Count);
        var visited = new Dictionary<IExtension, bool>();

        foreach (var ext in extensions)
        {
            if (!visited.ContainsKey(ext))
            {
                Visit(ext, dependsOn, visited, result);
            }
        }

        return result;
    }

    private static void Visit(IExtension node, Dictionary<IExtension, List<IExtension>> graph, Dictionary<IExtension, bool> visited, List<IExtension> result)
    {
        visited[node] = true; // visiting

        foreach (var dep in graph[node])
        {
            if (!visited.TryGetValue(dep, out var inProcess))
            {
                Visit(dep, graph, visited, result);
            }
            else if (inProcess)
            {
                // cycle detected
                throw new ExtensionException(node.Name, "Circular extension dependency detected.");
            }
        }

        visited[node] = false; // mark done
        if (!result.Contains(node)) result.Add(node);
    }
}
