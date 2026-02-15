namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents a condition that determines when a strategy should execute.
/// </summary>
public class ConditionConfiguration
{
    /// <summary>
    /// Gets or sets the type of condition.
    /// </summary>
    public required ConditionType Type { get; init; }

    /// <summary>
    /// Gets or sets the operator for comparing values.
    /// </summary>
    public ConditionOperator Operator { get; init; } = ConditionOperator.Equals;

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets or sets the field to evaluate (for custom conditions).
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to negate the condition result.
    /// </summary>
    public bool Negate { get; init; }

    /// <summary>
    /// Gets or sets the logical operator for combining with other conditions.
    /// </summary>
    public LogicalOperator LogicalOperator { get; init; } = LogicalOperator.And;

    /// <summary>
    /// Gets or sets additional parameters for the condition.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Defines the types of conditions that can trigger strategy execution.
/// </summary>
public enum ConditionType
{
    /// <summary>
    /// Always execute the strategy.
    /// </summary>
    Always,

    /// <summary>
    /// Execute when a timeout occurs.
    /// </summary>
    Timeout,

    /// <summary>
    /// Execute based on HTTP status code.
    /// </summary>
    StatusCode,

    /// <summary>
    /// Execute when a specific element is not found.
    /// </summary>
    ElementNotFound,

    /// <summary>
    /// Execute when any previous strategy failed.
    /// </summary>
    AnyFailed,

    /// <summary>
    /// Execute when all previous strategies failed.
    /// </summary>
    AllFailed,

    /// <summary>
    /// Execute based on content matching.
    /// </summary>
    ContentMatch,

    /// <summary>
    /// Execute based on a custom condition expression.
    /// </summary>
    Custom,

    /// <summary>
    /// Execute when the previous attempt was successful.
    /// </summary>
    PreviousSuccess,

    /// <summary>
    /// Execute when the previous attempt failed.
    /// </summary>
    PreviousFailed,

    /// <summary>
    /// Execute based on retry count.
    /// </summary>
    RetryCount,

    /// <summary>
    /// Execute based on elapsed time.
    /// </summary>
    ElapsedTime
}

/// <summary>
/// Defines operators for comparing condition values.
/// </summary>
public enum ConditionOperator
{
    /// <summary>
    /// Equal to.
    /// </summary>
    Equals,

    /// <summary>
    /// Not equal to.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Greater than.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal to.
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Less than.
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal to.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Contains the value.
    /// </summary>
    Contains,

    /// <summary>
    /// Does not contain the value.
    /// </summary>
    NotContains,

    /// <summary>
    /// Starts with the value.
    /// </summary>
    StartsWith,

    /// <summary>
    /// Ends with the value.
    /// </summary>
    EndsWith,

    /// <summary>
    /// Matches a regular expression.
    /// </summary>
    Regex,

    /// <summary>
    /// Value is in a list.
    /// </summary>
    In,

    /// <summary>
    /// Value is not in a list.
    /// </summary>
    NotIn
}

/// <summary>
/// Defines logical operators for combining conditions.
/// </summary>
public enum LogicalOperator
{
    /// <summary>
    /// Logical AND operation.
    /// </summary>
    And,

    /// <summary>
    /// Logical OR operation.
    /// </summary>
    Or
}
