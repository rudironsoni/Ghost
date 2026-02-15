namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Contains configuration options for a middleware component in the pipeline.
/// </summary>
/// <remarks>
/// This class allows middleware to be configured with additional metadata
/// that controls its behavior or affects pipeline compilation.
/// </remarks>
public sealed class MiddlewareConfiguration
{
    /// <summary>
    /// Gets or sets the name of the middleware for debugging and diagnostics.
    /// </summary>
    /// <remarks>
    /// If not explicitly set, the name defaults to the middleware type name.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this middleware is enabled.
    /// Disabled middleware is skipped during pipeline compilation.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets arbitrary metadata associated with this middleware.
    /// This can be used to pass configuration data to the middleware or
    /// to store information used by pipeline interceptors.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exceptions from this middleware
    /// should be allowed to propagate or should be caught by error handlers.
    /// </summary>
    /// <remarks>
    /// When true, exceptions from this middleware will not be caught by
    /// pipeline-level error handling middleware. Use this for critical
    /// middleware where failures should immediately halt the pipeline.
    /// </remarks>
    public bool CriticalFailure { get; set; }

    /// <summary>
    /// Creates a default configuration instance.
    /// </summary>
    /// <returns>A new MiddlewareConfiguration with default values.</returns>
    public static MiddlewareConfiguration Default() => new();

    /// <summary>
    /// Creates a configuration instance with the specified name.
    /// </summary>
    /// <param name="name">The middleware name.</param>
    /// <returns>A new MiddlewareConfiguration with the specified name.</returns>
    public static MiddlewareConfiguration WithName(string name) => new() { Name = name };

    /// <summary>
    /// Creates a configuration instance marked as critical.
    /// </summary>
    /// <param name="name">The middleware name.</param>
    /// <returns>A new MiddlewareConfiguration marked as critical.</returns>
    public static MiddlewareConfiguration Critical(string? name = null) => new()
    {
        Name = name,
        CriticalFailure = true
    };
}
