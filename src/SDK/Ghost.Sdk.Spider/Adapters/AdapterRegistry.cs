using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Registry for discovering and managing content adapter types.
/// </summary>
/// <remarks>
/// The AdapterRegistry maintains a mapping of adapter names and content types
/// to their implementation types. It supports automatic discovery through reflection
/// and manual registration of adapter types.
/// </remarks>
public class AdapterRegistry
{
    private readonly Dictionary<string, Type> _adaptersByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ContentType, List<Type>> _adaptersByContentType = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AdapterRegistry"/> class.
    /// </summary>
    public AdapterRegistry()
    {
        // Register built-in adapters
        RegisterBuiltInAdapters();
    }

    /// <summary>
    /// Registers an adapter type.
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type to register.</typeparam>
    /// <param name="name">The name to register the adapter under.</param>
    /// <param name="supportedContentTypes">The content types this adapter supports.</param>
    /// <exception cref="ArgumentException">Thrown when the adapter type does not implement IContentAdapter.</exception>
    public void Register<TAdapter>(string name, params ContentType[] supportedContentTypes)
        where TAdapter : IContentAdapter
    {
        Register(typeof(TAdapter), name, supportedContentTypes);
    }

    /// <summary>
    /// Registers an adapter type.
    /// </summary>
    /// <param name="adapterType">The adapter type to register.</param>
    /// <param name="name">The name to register the adapter under.</param>
    /// <param name="supportedContentTypes">The content types this adapter supports.</param>
    /// <exception cref="ArgumentException">Thrown when the adapter type does not implement IContentAdapter.</exception>
    public void Register(Type adapterType, string name, params ContentType[] supportedContentTypes)
    {
        ArgumentNullException.ThrowIfNull(adapterType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!typeof(IContentAdapter).IsAssignableFrom(adapterType))
        {
            throw new ArgumentException(
                $"Type {adapterType.Name} does not implement {nameof(IContentAdapter)}",
                nameof(adapterType));
        }

        lock (_lock)
        {
            _adaptersByName[name] = adapterType;

            foreach (var contentType in supportedContentTypes)
            {
                if (!_adaptersByContentType.TryGetValue(contentType, out var adapters))
                {
                    adapters = new List<Type>();
                    _adaptersByContentType[contentType] = adapters;
                }

                if (!adapters.Contains(adapterType))
                {
                    adapters.Add(adapterType);
                }
            }
        }
    }

    /// <summary>
    /// Gets an adapter type by name.
    /// </summary>
    /// <param name="name">The adapter name.</param>
    /// <returns>The adapter type if found; otherwise, null.</returns>
    public Type? GetAdapterType(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            return _adaptersByName.TryGetValue(name, out var type) ? type : null;
        }
    }

    /// <summary>
    /// Gets all adapter types that support a specific content type.
    /// </summary>
    /// <param name="contentType">The content type to filter by.</param>
    /// <returns>A collection of adapter types supporting the content type.</returns>
    public IEnumerable<Type> GetAdaptersByContentType(ContentType contentType)
    {
        lock (_lock)
        {
            return _adaptersByContentType.TryGetValue(contentType, out var adapters)
                ? adapters.ToList()
                : Enumerable.Empty<Type>();
        }
    }

    /// <summary>
    /// Gets all registered adapter types.
    /// </summary>
    /// <returns>A collection of all registered adapter types.</returns>
    public IEnumerable<Type> GetAllAdapterTypes()
    {
        lock (_lock)
        {
            return _adaptersByName.Values.Distinct().ToList();
        }
    }

    /// <summary>
    /// Checks if an adapter is registered.
    /// </summary>
    /// <param name="name">The adapter name.</param>
    /// <returns>True if the adapter is registered; otherwise, false.</returns>
    public bool IsRegistered(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            return _adaptersByName.ContainsKey(name);
        }
    }

    /// <summary>
    /// Unregisters an adapter by name.
    /// </summary>
    /// <param name="name">The adapter name to unregister.</param>
    /// <returns>True if the adapter was unregistered; false if it was not found.</returns>
    public bool Unregister(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_lock)
        {
            if (!_adaptersByName.TryGetValue(name, out var adapterType))
            {
                return false;
            }

            _adaptersByName.Remove(name);

            // Remove from content type mappings
            foreach (var contentTypeList in _adaptersByContentType.Values)
            {
                contentTypeList.Remove(adapterType);
            }

            return true;
        }
    }

    /// <summary>
    /// Discovers and registers all adapter types from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for adapters.</param>
    /// <returns>The number of adapters discovered and registered.</returns>
    public int DiscoverAdapters(System.Reflection.Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var adapterTypes = assembly.GetTypes()
            .Where(t => typeof(IContentAdapter).IsAssignableFrom(t) &&
                       !t.IsAbstract &&
                       !t.IsInterface)
            .ToList();

        var count = 0;
        foreach (var type in adapterTypes)
        {
            var name = type.Name.Replace("Adapter", string.Empty);

            // Try to determine supported content types from the type
            var supportedTypes = DetermineSupportedContentTypes(type);

            Register(type, name, supportedTypes);
            count++;
        }

        return count;
    }

    private void RegisterBuiltInAdapters()
    {
        // Register adapters from current assembly
        Register<StaticHtmlAdapter>("StaticHtml", ContentType.Html);
        Register<JavaScriptAdapter>("JavaScript", ContentType.Html, ContentType.Json);
        Register<GraphQLAdapter>("GraphQL", ContentType.Json);
    }

    private static ContentType[] DetermineSupportedContentTypes(Type adapterType)
    {
        // Default heuristics based on adapter name
        var name = adapterType.Name.ToLowerInvariant();
        var types = new List<ContentType>();

        if (name.Contains("html") || name.Contains("static"))
        {
            types.Add(ContentType.Html);
        }

        if (name.Contains("json") || name.Contains("api") || name.Contains("graphql"))
        {
            types.Add(ContentType.Json);
        }

        if (name.Contains("xml"))
        {
            types.Add(ContentType.Xml);
        }

        if (name.Contains("text"))
        {
            types.Add(ContentType.Text);
        }

        // If no content types determined, assume it can handle all
        return types.Count > 0 ? types.ToArray() : new[] { ContentType.Html, ContentType.Json };
    }
}
