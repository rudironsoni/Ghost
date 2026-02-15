namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents a chain of strategies that execute in sequence with coordinated results.
/// </summary>
public class StrategyChain
{
    /// <summary>
    /// Gets or sets the unique name of the strategy chain.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the strategies in the chain.
    /// </summary>
    public required List<StrategyConfiguration> Strategies { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to stop execution on first failure.
    /// </summary>
    public bool StopOnFailure { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to stop execution on first success.
    /// </summary>
    public bool StopOnSuccess { get; init; }

    /// <summary>
    /// Gets or sets the strategy for aggregating results from multiple strategies.
    /// </summary>
    public ResultAggregationStrategy AggregationStrategy { get; init; } = ResultAggregationStrategy.FirstSuccess;

    /// <summary>
    /// Gets or sets the maximum time to wait for the entire chain to complete.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to execute strategies in parallel.
    /// </summary>
    public bool Parallel { get; init; }

    /// <summary>
    /// Gets or sets the maximum degree of parallelism if executing in parallel.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;

    /// <summary>
    /// Gets or sets additional metadata for the chain.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets a description of what this chain does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the tags for categorizing this chain.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to continue executing subsequent strategies
    /// even after a success, allowing for result aggregation.
    /// </summary>
    public bool CollectAll { get; init; }

    /// <summary>
    /// Gets or sets the minimum number of successful strategies required for the chain to succeed.
    /// </summary>
    public int? MinimumSuccessCount { get; init; }
}

/// <summary>
/// Defines strategies for aggregating results from multiple strategy executions.
/// </summary>
public enum ResultAggregationStrategy
{
    /// <summary>
    /// Return the first successful result.
    /// </summary>
    FirstSuccess,

    /// <summary>
    /// Return the last successful result.
    /// </summary>
    LastSuccess,

    /// <summary>
    /// Merge all successful results into a collection.
    /// </summary>
    MergeAll,

    /// <summary>
    /// Return the result with the best performance (fastest).
    /// </summary>
    BestPerformance,

    /// <summary>
    /// Return the result with the most complete data.
    /// </summary>
    MostComplete,

    /// <summary>
    /// Return all results as a collection.
    /// </summary>
    CollectAll,

    /// <summary>
    /// Return the result with the highest priority strategy.
    /// </summary>
    HighestPriority
}
