namespace Ghost.Sdk.Spider.Pipeline.Contracts;

/// <summary>
/// Represents the delegate signature for pipeline middleware execution.
/// Middleware components use this delegate to invoke the next middleware in the pipeline.
/// </summary>
/// <param name="context">The pipeline context containing request data and state.</param>
/// <returns>A task representing the asynchronous operation.</returns>
/// <remarks>
/// This delegate forms the core of the pipeline execution model. Each middleware
/// receives this delegate as a parameter and can choose to invoke it to continue
/// the pipeline or short-circuit by not calling it.
/// </remarks>
public delegate Task PipelineDelegate(PipelineContext context);
