using System.Linq.Expressions;
using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Compilation;

/// <summary>
/// Compiles a sequence of middleware components into a high-performance pipeline
/// using expression trees.
/// </summary>
/// <remarks>
/// <para>
/// The PipelineCompiler transforms a list of middleware entries into a compiled
/// delegate chain. This approach provides several benefits:
/// - Near-zero overhead compared to manual chaining
/// - Direct method invocations through compiled expressions
/// - Single delegate representing the entire pipeline
/// - Elimination of runtime type checks and dynamic dispatch
/// </para>
/// <para>
/// The compilation process builds the pipeline in reverse order, starting with
/// the terminal middleware and working backwards to create the delegate chain.
/// Each middleware receives a delegate to invoke the next middleware, forming
/// a linked chain of execution.
/// </para>
/// </remarks>
internal static class PipelineCompiler
{
    /// <summary>
    /// Compiles a list of middleware entries into an executable pipeline.
    /// </summary>
    /// <param name="middlewareEntries">
    /// The ordered list of middleware entries to compile. The list should be
    /// in execution order (first middleware to execute comes first).
    /// </param>
    /// <returns>A compiled pipeline ready for execution.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when middlewareEntries is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the middleware list is empty or compilation fails.
    /// </exception>
    /// <remarks>
    /// The compilation process:
    /// 1. Validates the middleware list
    /// 2. Creates the terminal delegate (end of pipeline)
    /// 3. Iterates backwards through middleware, building the delegate chain
    /// 4. Compiles the final expression tree into executable code
    /// 5. Returns a CompiledPipeline wrapping the delegate
    /// </remarks>
    public static CompiledPipeline Compile(IReadOnlyList<MiddlewareEntry> middlewareEntries)
    {
        ArgumentNullException.ThrowIfNull(middlewareEntries);

        if (middlewareEntries.Count == 0)
        {
            throw new InvalidOperationException("Cannot compile an empty pipeline. Add at least one middleware component.");
        }

        // Extract middleware names for diagnostics
        var middlewareNames = middlewareEntries
            .Select(e => e.Configuration.Name ?? e.Middleware.GetType().Name)
            .ToList();

        // Build the pipeline delegate chain
        var pipelineDelegate = BuildPipelineDelegate(middlewareEntries);

        return new CompiledPipeline(pipelineDelegate, middlewareNames);
    }

    /// <summary>
    /// Builds the pipeline delegate by chaining middleware in reverse order.
    /// </summary>
    private static PipelineDelegate BuildPipelineDelegate(IReadOnlyList<MiddlewareEntry> middlewareEntries)
    {
        // Start with a terminal delegate that does nothing
        PipelineDelegate next = _ => Task.CompletedTask;

        // Iterate backwards through middleware to build the chain
        for (int i = middlewareEntries.Count - 1; i >= 0; i--)
        {
            var entry = middlewareEntries[i];
            var middleware = entry.Middleware;
            var currentNext = next;

            // Capture the middleware and next delegate in a closure
            // This creates the delegate chain
            next = context => middleware.InvokeAsync(context, currentNext);
        }

        return next;
    }

    /// <summary>
    /// Builds an optimized pipeline delegate using expression trees for even better performance.
    /// This is an alternative compilation strategy that can be used for specific scenarios.
    /// </summary>
    /// <remarks>
    /// This method demonstrates how to use expression trees to compile the pipeline.
    /// The basic closure-based approach above is simpler and has comparable performance
    /// for most scenarios. Use this approach when you need additional optimizations or
    /// want to inline middleware logic at compile time.
    /// </remarks>
    internal static PipelineDelegate BuildOptimizedPipelineDelegate(IReadOnlyList<MiddlewareEntry> middlewareEntries)
    {
        // Parameter for the context
        var contextParameter = Expression.Parameter(typeof(PipelineContext), "context");

        // Build terminal expression (completed task)
        Expression currentExpression = Expression.Constant(Task.CompletedTask);

        // Build the chain in reverse order using expression trees
        for (int i = middlewareEntries.Count - 1; i >= 0; i--)
        {
            var entry = middlewareEntries[i];
            var middleware = entry.Middleware;

            // Create a constant expression for the middleware instance
            var middlewareConstant = Expression.Constant(middleware, typeof(IPipelineMiddleware));

            // Create a delegate expression for the next middleware
            var nextDelegate = Expression.Lambda<PipelineDelegate>(
                currentExpression,
                contextParameter
            ).Compile();

            var nextDelegateConstant = Expression.Constant(nextDelegate, typeof(PipelineDelegate));

            // Call InvokeAsync on the middleware
            var invokeMethod = typeof(IPipelineMiddleware).GetMethod(nameof(IPipelineMiddleware.InvokeAsync))!;
            currentExpression = Expression.Call(
                middlewareConstant,
                invokeMethod,
                contextParameter,
                nextDelegateConstant
            );
        }

        // Compile the entire expression tree into a delegate
        var lambda = Expression.Lambda<PipelineDelegate>(currentExpression, contextParameter);
        return lambda.Compile();
    }

    /// <summary>
    /// Validates that all middleware entries are properly configured.
    /// </summary>
    /// <param name="middlewareEntries">The middleware entries to validate.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when validation fails.
    /// </exception>
    internal static void ValidateMiddleware(IReadOnlyList<MiddlewareEntry> middlewareEntries)
    {
        for (int i = 0; i < middlewareEntries.Count; i++)
        {
            var entry = middlewareEntries[i];
            
            if (entry.Middleware == null)
            {
                throw new InvalidOperationException(
                    $"Middleware entry at position {i} has a null middleware instance.");
            }

            if (entry.Configuration == null)
            {
                throw new InvalidOperationException(
                    $"Middleware entry at position {i} has a null configuration.");
            }

            if (!entry.Configuration.Enabled)
            {
                throw new InvalidOperationException(
                    $"Middleware entry at position {i} is disabled but was included in compilation. " +
                    "Disabled middleware should be filtered out before compilation.");
            }
        }
    }
}
