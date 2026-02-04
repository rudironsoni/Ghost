using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Factory for creating and managing content adapter instances.
/// </summary>
/// <remarks>
/// The AdapterFactory is responsible for creating adapter instances based on
/// request properties, managing adapter lifecycle, and providing adapter selection
/// logic. It works in conjunction with the <see cref="AdapterRegistry"/> to
/// discover and instantiate appropriate adapters.
/// </remarks>
public class AdapterFactory
{
    private readonly AdapterRegistry _registry;
    private readonly ILogger<AdapterFactory> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdapterFactory"/> class.
    /// </summary>
    /// <param name="registry">The adapter registry.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="logger">The logger instance.</param>
    public AdapterFactory(
        AdapterRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<AdapterFactory> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an adapter for the specified request.
    /// </summary>
    /// <param name="request">The content request to create an adapter for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the most suitable adapter for the request, or null if no suitable adapter is found.
    /// </returns>
    /// <remarks>
    /// This method uses the following strategy to select an adapter:
    /// <list type="number">
    /// <item>If the request has an adapter preference in metadata, try to use that adapter</item>
    /// <item>If the request specifies an expected content type, find adapters for that type</item>
    /// <item>Query all available adapters to see which can handle the request</item>
    /// <item>Return the first adapter that reports it can handle the request</item>
    /// </list>
    /// </remarks>
    public async Task<IContentAdapter?> CreateAdapterAsync(
        Request request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Creating adapter for request: {RequestId}, URL: {Url}", request.RequestId, request.Url);

        // Check for adapter preference in metadata
        if (request.Metadata.TryGetValue("AdapterPreference", out var preference) &&
            preference is string preferredName)
        {
            var preferredAdapter = await TryCreateAdapterByNameAsync(preferredName, request, cancellationToken);
            if (preferredAdapter != null)
            {
                _logger.LogInformation("Using preferred adapter: {AdapterName} for request: {RequestId}",
                    preferredName, request.RequestId);
                return preferredAdapter;
            }

            _logger.LogWarning("Preferred adapter {AdapterName} not available for request: {RequestId}",
                preferredName, request.RequestId);
        }

        // Try to find adapter by expected content type
        if (request.ExpectedContentType != ContentType.Unknown)
        {
            var adapterByType = await TryCreateAdapterByContentTypeAsync(
                request.ExpectedContentType,
                request,
                cancellationToken);

            if (adapterByType != null)
            {
                _logger.LogInformation("Using adapter for content type {ContentType}: {AdapterName} for request: {RequestId}",
                    request.ExpectedContentType, adapterByType.Name, request.RequestId);
                return adapterByType;
            }
        }

        // Try all available adapters
        var adapter = await TryCreateAnyAdapterAsync(request, cancellationToken);
        if (adapter != null)
        {
            _logger.LogInformation("Selected adapter: {AdapterName} for request: {RequestId}",
                adapter.Name, request.RequestId);
            return adapter;
        }

        _logger.LogWarning("No suitable adapter found for request: {RequestId}, URL: {Url}",
            request.RequestId, request.Url);
        return null;
    }

    /// <summary>
    /// Creates an adapter by name.
    /// </summary>
    /// <param name="adapterName">The name of the adapter to create.</param>
    /// <returns>The created adapter, or null if the adapter is not found or not available.</returns>
    /// <remarks>
    /// This method is useful when you know exactly which adapter you want to use,
    /// bypassing the automatic selection logic.
    /// </remarks>
    public IContentAdapter? CreateAdapterByName(string adapterName)
    {
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be null or empty.", nameof(adapterName));

        _logger.LogDebug("Creating adapter by name: {AdapterName}", adapterName);

        var adapterType = _registry.GetAdapterType(adapterName);
        if (adapterType == null)
        {
            _logger.LogWarning("Adapter type not found: {AdapterName}", adapterName);
            return null;
        }

        var adapter = CreateAdapterInstance(adapterType);
        if (adapter == null || !adapter.IsAvailable)
        {
            _logger.LogWarning("Adapter not available: {AdapterName}", adapterName);
            return null;
        }

        return adapter;
    }

    /// <summary>
    /// Creates all available adapters for a specific content type.
    /// </summary>
    /// <param name="contentType">The content type to create adapters for.</param>
    /// <returns>A collection of adapters that support the specified content type.</returns>
    /// <remarks>
    /// This method is useful for implementing fallback strategies where multiple
    /// adapters may be tried in sequence.
    /// </remarks>
    public IEnumerable<IContentAdapter> CreateAdaptersByContentType(ContentType contentType)
    {
        _logger.LogDebug("Creating adapters for content type: {ContentType}", contentType);

        var adapterTypes = _registry.GetAdaptersByContentType(contentType);
        var adapters = new List<IContentAdapter>();

        foreach (var adapterType in adapterTypes)
        {
            var adapter = CreateAdapterInstance(adapterType);
            if (adapter != null && adapter.IsAvailable)
            {
                adapters.Add(adapter);
                _logger.LogDebug("Created adapter: {AdapterName} for content type: {ContentType}",
                    adapter.Name, contentType);
            }
        }

        _logger.LogInformation("Created {Count} adapters for content type: {ContentType}",
            adapters.Count, contentType);

        return adapters;
    }

    /// <summary>
    /// Gets all available adapters registered in the system.
    /// </summary>
    /// <returns>A collection of all available adapter instances.</returns>
    /// <remarks>
    /// This method creates instances of all registered adapters that are currently available.
    /// Useful for diagnostics, testing, or implementing custom adapter selection logic.
    /// </remarks>
    public IEnumerable<IContentAdapter> GetAllAvailableAdapters()
    {
        _logger.LogDebug("Getting all available adapters");

        var adapterTypes = _registry.GetAllAdapterTypes();
        var adapters = new List<IContentAdapter>();

        foreach (var adapterType in adapterTypes)
        {
            var adapter = CreateAdapterInstance(adapterType);
            if (adapter != null && adapter.IsAvailable)
            {
                adapters.Add(adapter);
            }
        }

        _logger.LogInformation("Found {Count} available adapters", adapters.Count);
        return adapters;
    }

    private async Task<IContentAdapter?> TryCreateAdapterByNameAsync(
        string adapterName,
        Request request,
        CancellationToken cancellationToken)
    {
        var adapter = CreateAdapterByName(adapterName);
        if (adapter == null)
        {
            return null;
        }

        var canHandle = await adapter.CanHandleAsync(request, cancellationToken);
        return canHandle ? adapter : null;
    }

    private async Task<IContentAdapter?> TryCreateAdapterByContentTypeAsync(
        ContentType contentType,
        Request request,
        CancellationToken cancellationToken)
    {
        var adapters = CreateAdaptersByContentType(contentType);

        foreach (var adapter in adapters)
        {
            var canHandle = await adapter.CanHandleAsync(request, cancellationToken);
            if (canHandle)
            {
                return adapter;
            }
        }

        return null;
    }

    private async Task<IContentAdapter?> TryCreateAnyAdapterAsync(
        Request request,
        CancellationToken cancellationToken)
    {
        var adapters = GetAllAvailableAdapters();

        foreach (var adapter in adapters)
        {
            var canHandle = await adapter.CanHandleAsync(request, cancellationToken);
            if (canHandle)
            {
                return adapter;
            }
        }

        return null;
    }

    private IContentAdapter? CreateAdapterInstance(Type adapterType)
    {
        try
        {
            var adapter = ActivatorUtilities.CreateInstance(_serviceProvider, adapterType) as IContentAdapter;
            return adapter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create adapter instance: {AdapterType}", adapterType.Name);
            return null;
        }
    }
}
