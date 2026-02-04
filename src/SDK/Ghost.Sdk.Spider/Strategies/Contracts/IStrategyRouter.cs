namespace Ghost.Sdk.Spider.Strategies.Contracts;

/// <summary>
/// Defines a router for executing extraction strategies with fallback support.
/// </summary>
public interface IStrategyRouter
{
    /// <summary>
    /// Executes strategies in order based on their priority and conditions.
    /// </summary>
    /// <param name="context">The strategy context containing state and configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The extraction result from the first successful strategy.</returns>
    Task<ExtractionResult> ExecuteAsync(StrategyContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a specific strategy by name.
    /// </summary>
    /// <param name="strategyName">The name of the strategy to execute.</param>
    /// <param name="context">The strategy context containing state and configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The extraction result from the specified strategy.</returns>
    Task<ExtractionResult> ExecuteStrategyAsync(string strategyName, StrategyContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a chain of strategies with coordinated results.
    /// </summary>
    /// <param name="chain">The strategy chain to execute.</param>
    /// <param name="context">The strategy context containing state and configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The aggregated extraction result from the chain.</returns>
    Task<ExtractionResult> ExecuteChainAsync(StrategyChain chain, StrategyContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metrics for all strategies.
    /// </summary>
    /// <returns>A dictionary of strategy names and their metrics.</returns>
    IReadOnlyDictionary<string, StrategyMetrics> GetMetrics();

    /// <summary>
    /// Resets metrics for all strategies.
    /// </summary>
    void ResetMetrics();
}
