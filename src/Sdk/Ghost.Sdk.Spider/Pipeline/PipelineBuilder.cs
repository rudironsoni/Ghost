using Ghost.Sdk.Spider.Pipeline.Compilation;
using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Provides a fluent API for building and configuring spider pipelines.
/// The builder allows you to add middleware components in order and compile
/// them into a high-performance executable pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The PipelineBuilder follows the builder pattern to provide a fluent, readable
/// API for pipeline configuration. Middleware is added in the order it should execute,
/// and the builder handles the compilation into an optimized pipeline.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var pipeline = new PipelineBuilder()
///     .Use(new LoggingMiddleware())
///     .Use(new RateLimitMiddleware())
///     .Use(new FetchMiddleware())
///     .Build();
/// </code>
/// </para>
/// <para>
/// Thread Safety: The builder itself is NOT thread-safe. Build the pipeline
/// on a single thread, then use the resulting CompiledPipeline concurrently.
/// </para>
/// </remarks>
public sealed class PipelineBuilder
{
    private readonly List<MiddlewareEntry> _middlewareEntries = [];
    private int _nextOrder;

    /// <summary>
    /// Adds a middleware component to the pipeline with default configuration.
    /// </summary>
    /// <param name="middleware">The middleware instance to add.</param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when middleware is null.
    /// </exception>
    /// <remarks>
    /// Middleware is executed in the order it is added to the builder.
    /// The first middleware added will be the first to execute when a request
    /// enters the pipeline.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Use(new LoggingMiddleware());
    /// </code>
    /// </example>
    public PipelineBuilder Use(IPipelineMiddleware middleware)
    {
        return Use(middleware, MiddlewareConfiguration.Default());
    }

    /// <summary>
    /// Adds a middleware component to the pipeline with the specified configuration.
    /// </summary>
    /// <param name="middleware">The middleware instance to add.</param>
    /// <param name="configuration">The configuration for this middleware.</param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when middleware or configuration is null.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Use(
    ///     new RetryMiddleware(),
    ///     MiddlewareConfiguration.Critical("RetryHandler")
    /// );
    /// </code>
    /// </example>
    public PipelineBuilder Use(IPipelineMiddleware middleware, MiddlewareConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        ArgumentNullException.ThrowIfNull(configuration);

        var entry = new MiddlewareEntry
        {
            Middleware = middleware,
            Configuration = configuration,
            Order = _nextOrder++
        };

        _middlewareEntries.Add(entry);
        return this;
    }

    /// <summary>
    /// Adds a middleware component to the pipeline with a name.
    /// </summary>
    /// <param name="middleware">The middleware instance to add.</param>
    /// <param name="name">The name for this middleware (used for diagnostics).</param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when middleware or name is null.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Use(new CustomMiddleware(), "CustomProcessor");
    /// </code>
    /// </example>
    public PipelineBuilder Use(IPipelineMiddleware middleware, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Use(middleware, MiddlewareConfiguration.WithName(name));
    }

    /// <summary>
    /// Adds a middleware component using a factory function.
    /// The factory is invoked immediately to create the middleware instance.
    /// </summary>
    /// <param name="factory">A function that creates the middleware instance.</param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when factory is null or returns null.
    /// </exception>
    /// <remarks>
    /// This overload is useful for middleware that requires complex initialization
    /// or needs to be configured inline.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Use(() => new RateLimitMiddleware(maxRequests: 100));
    /// </code>
    /// </example>
    public PipelineBuilder Use(Func<IPipelineMiddleware> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        IPipelineMiddleware middleware = factory() ?? throw new ArgumentNullException(nameof(factory),
            "Middleware factory returned null.");
        return Use(middleware);
    }

    /// <summary>
    /// Adds a middleware component using an inline delegate.
    /// This creates an anonymous middleware that executes the provided function.
    /// </summary>
    /// <param name="middlewareFunc">
    /// A function that implements the middleware logic, receiving the context and next delegate.
    /// </param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when middlewareFunc is null.
    /// </exception>
    /// <remarks>
    /// This overload is convenient for simple middleware that doesn't require a full class.
    /// For complex logic or reusable middleware, prefer implementing IPipelineMiddleware.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Use(async (context, next) =>
    /// {
    ///     Console.WriteLine($"Before: {context.RequestId}");
    ///     await continuation(context);
    ///     Console.WriteLine($"After: {context.RequestId}");
    /// });
    /// </code>
    /// </example>
    public PipelineBuilder Use(Func<PipelineContext, PipelineDelegate, Task> middlewareFunc)
    {
        ArgumentNullException.ThrowIfNull(middlewareFunc);
        var middleware = new InlineMiddleware(middlewareFunc);
        return Use(middleware);
    }

    /// <summary>
    /// Removes all middleware with the specified name from the pipeline.
    /// </summary>
    /// <param name="name">The name of the middleware to remove.</param>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    /// <remarks>
    /// This method removes all middleware entries where the configuration name
    /// matches the specified name. If no matching middleware is found, the
    /// method has no effect.
    /// </remarks>
    public PipelineBuilder Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _middlewareEntries.RemoveAll(e => e.Configuration.Name == name);
        return this;
    }

    /// <summary>
    /// Clears all middleware from the pipeline.
    /// </summary>
    /// <returns>The pipeline builder for fluent chaining.</returns>
    public PipelineBuilder Clear()
    {
        _middlewareEntries.Clear();
        _nextOrder = 0;
        return this;
    }

    /// <summary>
    /// Gets the number of middleware components currently in the builder.
    /// </summary>
    public int Count => _middlewareEntries.Count;

    /// <summary>
    /// Builds and compiles the pipeline into an executable form.
    /// </summary>
    /// <returns>A compiled pipeline ready for execution.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the pipeline is empty or compilation fails.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The build process:
    /// 1. Filters out disabled middleware
    /// 2. Validates all middleware entries
    /// 3. Compiles the middleware chain using expression trees
    /// 4. Returns an immutable, thread-safe pipeline
    /// </para>
    /// <para>
    /// After building, the builder can be reused to create additional pipelines.
    /// The built pipeline is completely independent of the builder.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var pipeline = builder.Build();
    ///
    /// // The builder can be reused
    /// builder.Use(new AdditionalMiddleware());
    /// var pipeline2 = builder.Build();
    /// </code>
    /// </example>
    public CompiledPipeline Build()
    {
        // Filter out disabled middleware
        var enabledMiddleware = _middlewareEntries
            .Where(e => e.Configuration.Enabled)
            .ToList();

        if (enabledMiddleware.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot build a pipeline with no enabled middleware. Add at least one middleware component.");
        }

        // Validate middleware before compilation
        PipelineCompiler.ValidateMiddleware(enabledMiddleware);

        // Compile the pipeline
        return PipelineCompiler.Compile(enabledMiddleware);
    }

    /// <summary>
    /// Creates a copy of this builder with the same middleware configuration.
    /// </summary>
    /// <returns>A new pipeline builder with the same middleware entries.</returns>
    /// <remarks>
    /// This creates a shallow copy - the middleware instances themselves are shared
    /// between the original and cloned builder.
    /// </remarks>
    public PipelineBuilder Clone()
    {
        var clone = new PipelineBuilder();
        clone._middlewareEntries.AddRange(_middlewareEntries);
        clone._nextOrder = _nextOrder;
        return clone;
    }

    /// <summary>
    /// Anonymous middleware implementation for inline delegates.
    /// </summary>
    private sealed class InlineMiddleware : IPipelineMiddleware
    {
        private readonly Func<PipelineContext, PipelineDelegate, Task> _middlewareFunc;

        public InlineMiddleware(Func<PipelineContext, PipelineDelegate, Task> middlewareFunc)
        {
            _middlewareFunc = middlewareFunc;
        }

        public Task InvokeAsync(PipelineContext context, PipelineDelegate continuation)
        {
            return _middlewareFunc(context, continuation);
        }
    }
}
