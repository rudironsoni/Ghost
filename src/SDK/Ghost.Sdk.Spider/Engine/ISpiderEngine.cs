using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Defines the contract for the spider execution engine.
/// </summary>
/// <remarks>
/// The spider engine orchestrates the complete crawling lifecycle including
/// request scheduling, content extraction, pipeline execution, and storage.
/// </remarks>
public interface ISpiderEngine
{
    /// <summary>
    /// Starts the spider with the specified spider instance.
    /// </summary>
    /// <param name="spider">The spider to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the spider result.</returns>
    public Task<SpiderResult> StartAsync(Spider spider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the spider engine gracefully.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses spider execution.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes spider execution.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current execution context for the running spider.
    /// </summary>
    /// <returns>The execution context, or null if no spider is running.</returns>
    public ExecutionContext? GetCurrentContext();
}
