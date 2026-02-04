namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents the configuration for a single extraction strategy.
/// </summary>
public class StrategyConfiguration
{
    /// <summary>
    /// Gets or sets the unique name of the strategy.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the priority of the strategy (lower values execute first).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this strategy is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the timeout for this strategy.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of retries for this strategy.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets or sets the conditions that must be met for this strategy to execute.
    /// </summary>
    public List<ConditionConfiguration> Conditions { get; init; } = new();

    /// <summary>
    /// Gets or sets the fallback conditions that trigger this strategy after another fails.
    /// </summary>
    public List<ConditionConfiguration> FallbackConditions { get; init; } = new();

    /// <summary>
    /// Gets or sets additional parameters specific to this strategy.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Gets or sets the tags for categorizing this strategy.
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets or sets a description of what this strategy does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the strategy type identifier.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this strategy should stop execution on success.
    /// </summary>
    public bool StopOnSuccess { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this strategy should stop execution on failure.
    /// </summary>
    public bool StopOnFailure { get; init; } = false;

    /// <summary>
    /// Gets or sets the delay before executing this strategy.
    /// </summary>
    public TimeSpan? DelayBefore { get; init; }

    /// <summary>
    /// Gets or sets the delay after executing this strategy.
    /// </summary>
    public TimeSpan? DelayAfter { get; init; }
}
