using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Represents a compiled, high-performance pipeline ready for execution.
/// The pipeline is compiled using expression trees for optimal performance.
/// </summary>
/// <remarks>
/// <para>
/// CompiledPipeline is the result of the PipelineBuilder compilation process.
/// It contains a delegate chain that represents the entire middleware pipeline,
/// compiled using expression trees for maximum performance.
/// </para>
/// <para>
/// Once compiled, the pipeline is immutable and thread-safe. A single compiled
/// pipeline instance can be used concurrently to process multiple requests.
/// </para>
/// <para>
/// Performance characteristics:
/// - Zero allocations during pipeline execution (except middleware-specific allocations)
/// - Direct method invocations through compiled delegates
/// - Minimal overhead compared to manual middleware chaining
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var pipeline = builder.Build();
///
/// var context = new PipelineContext
/// {
///     Request = request,
///     RequestId = 1,
///     CancellationToken = cancellationToken,
///     StateBox = stateBox
/// };
///
/// await pipeline.ExecuteAsync(context);
/// </code>
/// </example>
public sealed class CompiledPipeline
{
    private readonly PipelineDelegate _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledPipeline"/> class.
    /// </summary>
    /// <param name="pipeline">The compiled pipeline delegate.</param>
    /// <param name="middlewareNames">The ordered list of middleware names for diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when pipeline or middlewareNames is null.
    /// </exception>
    internal CompiledPipeline(PipelineDelegate pipeline, IReadOnlyList<string> middlewareNames)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        MiddlewareNames = middlewareNames ?? throw new ArgumentNullException(nameof(middlewareNames));
    }

    /// <summary>
    /// Gets the ordered list of middleware names in this pipeline.
    /// This is useful for diagnostics and debugging.
    /// </summary>
    public IReadOnlyList<string> MiddlewareNames { get; }

    /// <summary>
    /// Gets the number of middleware components in this pipeline.
    /// </summary>
    public int MiddlewareCount => MiddlewareNames.Count;

    /// <summary>
    /// Executes the pipeline with the given context.
    /// </summary>
    /// <param name="context">The pipeline context containing request data and state.</param>
    /// <returns>A task representing the asynchronous pipeline execution.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the context's cancellation token.
    /// </exception>
    /// <exception cref="Exception">
    /// Any exception thrown by middleware components will propagate to the caller
    /// unless caught by error handling middleware in the pipeline.
    /// </exception>
    /// <remarks>
    /// This method is thread-safe and can be called concurrently with different contexts.
    /// The context parameter is passed by value (it's a struct), so each execution
    /// has its own copy of the context.
    /// </remarks>
    public Task ExecuteAsync(PipelineContext context)
    {
        return _pipeline(context);
    }

    /// <summary>
    /// Gets a string representation of the pipeline showing all middleware in order.
    /// </summary>
    /// <returns>A string describing the pipeline structure.</returns>
    public override string ToString()
    {
        return $"CompiledPipeline with {MiddlewareCount} middleware: [{string.Join(" -> ", MiddlewareNames)}]";
    }
}
