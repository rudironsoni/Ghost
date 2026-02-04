namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Represents the execution context for a single request flowing through the spider pipeline.
/// This is a value type (struct) designed for zero-allocation hot path performance.
/// </summary>
/// <remarks>
/// <para>
/// PipelineContext is designed as a struct to avoid heap allocations during pipeline
/// execution. It contains only essential data and references needed for request processing.
/// For complex state that needs to survive across multiple requests, use the SpiderStateBox.
/// </para>
/// <para>
/// The context is immutable by design - modifications to the request or state should be
/// made through the referenced objects, not by creating new context instances.
/// </para>
/// <para>
/// Thread Safety: While the context struct itself is a value type, the Request and StateBox
/// references may be shared across threads. Ensure proper synchronization when accessing
/// mutable state in the StateBox or Request objects.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var context = new PipelineContext
/// {
///     Request = httpRequest,
///     RequestId = 12345,
///     CancellationToken = cancellationToken,
///     StateBox = spiderState
/// };
/// 
/// await pipeline.ExecuteAsync(context);
/// </code>
/// </example>
public readonly struct PipelineContext
{
    /// <summary>
    /// Gets the request object being processed through the pipeline.
    /// This is typically an HTTP request or a spider-specific request wrapper.
    /// </summary>
    /// <remarks>
    /// The request object should contain all information necessary to process
    /// the request, including URL, headers, method, and payload. Middleware
    /// can read and modify the request as it flows through the pipeline.
    /// </remarks>
    public required object Request { get; init; }

    /// <summary>
    /// Gets the unique identifier for this request.
    /// This ID can be used for correlation, logging, and tracking requests through the pipeline.
    /// </summary>
    /// <remarks>
    /// Request IDs should be monotonically increasing or globally unique within
    /// the spider's lifetime. They are useful for debugging and observability.
    /// </remarks>
    public required long RequestId { get; init; }

    /// <summary>
    /// Gets the cancellation token for this pipeline execution.
    /// Middleware should check this token periodically for cooperative cancellation.
    /// </summary>
    /// <remarks>
    /// The cancellation token may be triggered by:
    /// - User-initiated cancellation
    /// - Timeout policies
    /// - Spider shutdown
    /// - Resource exhaustion
    /// 
    /// Middleware should respect cancellation tokens and throw OperationCanceledException
    /// when cancellation is requested.
    /// </remarks>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the optional state box containing complex spider state that outlives individual requests.
    /// This is null for simple spiders that don't require shared state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The StateBox is an escape hatch for scenarios where:
    /// - State needs to be shared across multiple pipeline executions
    /// - Complex objects or collections need to be maintained
    /// - Reference semantics are required for state management
    /// </para>
    /// <para>
    /// For simple spiders without complex state requirements, this will be null.
    /// Always check for null before accessing the StateBox.
    /// </para>
    /// <para>
    /// The StateBox is thread-safe and can be accessed concurrently from multiple
    /// pipeline executions. Use the provided atomic operations for counters and
    /// the concurrent dictionary for complex state.
    /// </para>
    /// </remarks>
    public SpiderStateBox? StateBox { get; init; }

    /// <summary>
    /// Throws an OperationCanceledException if cancellation has been requested.
    /// This is a convenience method for checking the cancellation token.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// Thrown when cancellation has been requested via the CancellationToken.
    /// </exception>
    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Gets a value indicating whether cancellation has been requested.
    /// </summary>
    public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

    /// <summary>
    /// Attempts to cast the Request to the specified type.
    /// </summary>
    /// <typeparam name="TRequest">The expected type of the request.</typeparam>
    /// <returns>The request cast to TRequest if successful; otherwise, default(TRequest).</returns>
    /// <remarks>
    /// This method provides a safe way to access strongly-typed request objects
    /// in middleware that expects a specific request type.
    /// </remarks>
    public TRequest? GetRequestAs<TRequest>() where TRequest : class
    {
        return Request as TRequest;
    }
}
