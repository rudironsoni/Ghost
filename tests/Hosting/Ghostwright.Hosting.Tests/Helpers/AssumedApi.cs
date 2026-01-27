using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Hosting
{
    // Minimal assumed API surface used by tests when production project isn't present.
    public interface IExtension
    {
        string Name { get; }
        Version Version { get; }
        IReadOnlyList<Type> ProvidedServices { get; }
        IReadOnlyList<Type> RequiredServices { get; }
        void ConfigureServices(IServiceCollection services, IConfiguration config);
    }

    public class GhostwriterOptions
    {
        public string Kernel { get; set; } = "DefaultKernel";
        public bool ValidateExtensionDependencies { get; set; } = true;
    }

    public class GhostwriterBuilder
    {
        private readonly List<IExtension> _extensions = new();
        public GhostwriterOptions Options { get; } = new();

        public GhostwriterBuilder ConfigureKernel(Action<GhostwriterOptions> configure)
        {
            configure?.Invoke(Options);
            return this;
        }

        public GhostwriterBuilder UseExtension<T>() where T : IExtension, new()
        {
            _extensions.Add(new T());
            return this;
        }

        public GhostwriterBuilder UseExtension(IExtension extension)
        {
            _extensions.Add(extension ?? throw new ArgumentNullException(nameof(extension)));
            return this;
        }

        public IServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Options);

            var loader = new ExtensionLoader(_extensions, Options);
            loader.LoadExtensions(services, new ConfigurationBuilder().Build());

            return services.BuildServiceProvider();
        }
    }

    public class ExtensionException : Exception
    {
        public string ExtensionName { get; }
        public ExtensionException(string extensionName, string message) : base(message)
        {
            ExtensionName = extensionName;
        }

        public override string Message => base.Message + (string.IsNullOrEmpty(ExtensionName) ? string.Empty : $" (Extension: {ExtensionName})");
    }

    public class ExtensionLoader
    {
        private readonly List<IExtension> _extensions;
        private readonly GhostwriterOptions _options;

        public ExtensionLoader(IEnumerable<IExtension> extensions, GhostwriterOptions options)
        {
            _extensions = new List<IExtension>(extensions ?? Array.Empty<IExtension>());
            _options = options ?? new GhostwriterOptions();
        }

        public void ValidateExtensions()
        {
            if (!_options.ValidateExtensionDependencies)
                return;

            var provided = new Dictionary<Type, IExtension>();
            foreach (var ext in _extensions)
            {
                foreach (var prov in ext.ProvidedServices)
                {
                    provided[prov] = ext;
                }
            }

            // Check missing dependencies
            foreach (var ext in _extensions)
            {
                foreach (var req in ext.RequiredServices)
                {
                    if (!provided.ContainsKey(req))
                        throw new ExtensionException(ext.Name, $"Missing dependency: {req.FullName}");
                }
            }

            // Detect cycles using DFS
            var graph = new Dictionary<IExtension, List<IExtension>>();
            foreach (var ext in _extensions)
            {
                graph[ext] = new List<IExtension>();
                foreach (var req in ext.RequiredServices)
                {
                    if (provided.TryGetValue(req, out var provider))
                    {
                        graph[ext].Add(provider);
                    }
                }
            }

            var visited = new Dictionary<IExtension, int>(); // 0 unvisited,1 visiting,2 done
            foreach (var node in graph.Keys)
            {
                if (HasCycle(node))
                {
                    throw new ExtensionException(node.Name, "Circular dependency detected");
                }
            }

            bool HasCycle(IExtension node)
            {
                if (visited.TryGetValue(node, out var state))
                {
                    if (state == 1) return true;
                    return false;
                }
                visited[node] = 1;
                foreach (var n in graph[node])
                {
                    if (HasCycle(n)) return true;
                }
                visited[node] = 2;
                return false;
            }
        }

        public void LoadExtensions(IServiceCollection services, IConfiguration config)
        {
            ValidateExtensions();

            // Topological sort
            var provided = new Dictionary<Type, IExtension>();
            foreach (var ext in _extensions)
            {
                foreach (var prov in ext.ProvidedServices)
                    provided[prov] = ext;
            }

            var graph = new Dictionary<IExtension, List<IExtension>>();
            var indegree = new Dictionary<IExtension, int>();
            foreach (var ext in _extensions)
            {
                graph[ext] = new List<IExtension>();
                indegree[ext] = 0;
            }
            foreach (var ext in _extensions)
            {
                foreach (var req in ext.RequiredServices)
                {
                    if (provided.TryGetValue(req, out var provider))
                    {
                        graph[provider].Add(ext);
                        indegree[ext]++;
                    }
                }
            }

            var q = new Queue<IExtension>();
            foreach (var kv in indegree)
                if (kv.Value == 0) q.Enqueue(kv.Key);

            var ordered = new List<IExtension>();
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                ordered.Add(cur);
                foreach (var nxt in graph[cur])
                {
                    indegree[nxt]--;
                    if (indegree[nxt] == 0) q.Enqueue(nxt);
                }
            }

            if (ordered.Count != _extensions.Count)
                throw new ExtensionException("Unknown", "Unable to order extensions (cycle?)");

            foreach (var ext in ordered)
            {
                ext.ConfigureServices(services, config);
            }
        }
    }

    // Simple marker for tests
    public interface IInferenceClient { }
}
