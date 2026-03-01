using System;
using System.Collections.Generic;
using System.Linq;
using Ghost.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Hosting;

/// <summary>
/// Responsible for validating extension dependencies and loading them in a correct order.
/// </summary>
internal sealed class ExtensionLoader
{
    /// <summary>
    /// Validates the provided extensions for missing dependencies and cycles.
    /// </summary>
    /// <param name="extensions">List of extensions to validate.</param>
    /// <param name="kernelProvidedServices">Services provided by the kernel (e.g., IBrowserSession).</param>
    public static void ValidateExtensions(IReadOnlyList<IExtension> extensions, IReadOnlySet<Type>? kernelProvidedServices = null)
    {
        if (extensions is null) ArgumentNullException.ThrowIfNull(extensions);

        // Collect all provided service types (start with kernel-provided if any)
        HashSet<Type> provided = kernelProvidedServices != null
            ? new HashSet<Type>(kernelProvidedServices)
            : new HashSet<Type>();

        foreach (IExtension ext in extensions)
        {
            foreach (Type t in ext.ProvidedServices)
            {
                provided.Add(t);
            }
        }

        // Check required services are provided by some extension or the kernel
        foreach (IExtension ext in extensions)
        {
            foreach (Type req in ext.RequiredServices)
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
    /// <param name="kernelProvidedServices">Services provided by the kernel.</param>
    public static void LoadExtensions(IReadOnlyList<IExtension> extensions, IServiceCollection services, IConfiguration configuration, IReadOnlySet<Type>? kernelProvidedServices = null)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Validate first
        ValidateExtensions(extensions, kernelProvidedServices);

        List<IExtension> ordered = TopologicalSort(extensions);

        foreach (IExtension ext in ordered)
        {
            try
            {
                // Contract uses ConfigureServices as the registration entry point
                ext.ConfigureServices(services, configuration);
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
        foreach (IExtension ext in extensions)
        {
            List<IExtension> list = [];
            foreach (Type req in ext.RequiredServices)
            {
                IExtension? provider = extensions.FirstOrDefault(e => e.ProvidedServices.Contains(req));
                if (provider != null && !ReferenceEquals(provider, ext))
                {
                    list.Add(provider);
                }
            }
            dependsOn[ext] = list;
        }

        var result = new List<IExtension>(extensions.Count);
        Dictionary<IExtension, bool> visited = [];

        foreach (IExtension ext in extensions)
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

        foreach (IExtension dep in graph[node])
        {
            if (!visited.TryGetValue(dep, out bool inProcess))
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
