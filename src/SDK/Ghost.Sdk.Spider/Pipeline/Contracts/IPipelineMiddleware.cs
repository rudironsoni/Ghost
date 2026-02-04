namespace Ghost.Sdk.Spider.Pipeline.Contracts;

/// <summary>
/// Defines the contract for middleware components in the spider pipeline.
/// Middleware components can inspect and modify requests, perform operations,
/// and control the flow of pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// Middleware components are executed in the order they are registered in the pipeline.
/// Each middleware receives the pipeline context and a delegate to invoke the next middleware.
/// </para>
/// <para>
/// Middleware can:
/// - Inspect and modify the request before passing it to the next middleware
/// - Perform operations before and after the next middleware executes
/// - Short-circuit the pipeline by not calling the next delegate
/// - Handle exceptions and implement error recovery logic
/// - Update shared state in the SpiderStateBox
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingMiddleware : IPipelineMiddleware
/// {
///     public async Task InvokeAsync(PipelineContext context, PipelineDelegate next)
///     {
///         Console.WriteLine($"Processing request {context.RequestId}");
///         await next(context);
///         Console.WriteLine($"Completed request {context.RequestId}");
///     }
/// }
/// </code>
/// </example>
public interface IPipelineMiddleware
{
    /// <summary>
    /// Invokes the middleware with the given context and next delegate.
    /// </summary>
    /// <param name="context">
    /// The pipeline context containing the request, state, and cancellation token.
    /// </param>
    /// <param name="next">
    /// The delegate to invoke the next middleware in the pipeline.
    /// Call this to continue pipeline execution, or omit to short-circuit.
    /// </param>
    /// <returns>A task representing the asynchronous middleware execution.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the context's cancellation token.
    /// </exception>
    Task InvokeAsync(PipelineContext context, PipelineDelegate next);
}
