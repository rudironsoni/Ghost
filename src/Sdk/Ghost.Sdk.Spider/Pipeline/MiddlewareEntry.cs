using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Represents a middleware entry in the pipeline configuration.
/// Contains both the middleware instance and its associated configuration.
/// </summary>
/// <remarks>
/// This class is used internally by the pipeline builder to maintain
/// the ordered list of middleware components before compilation.
/// </remarks>
internal sealed class MiddlewareEntry
{
    /// <summary>
    /// Gets the middleware instance.
    /// </summary>
    public required IPipelineMiddleware Middleware { get; init; }

    /// <summary>
    /// Gets the configuration for this middleware instance.
    /// </summary>
    public required MiddlewareConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the order in which this middleware was added to the pipeline.
    /// Lower values execute earlier in the pipeline.
    /// </summary>
    public required int Order { get; init; }
}
