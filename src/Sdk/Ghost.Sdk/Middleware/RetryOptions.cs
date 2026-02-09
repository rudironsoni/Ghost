namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for exponential backoff retry policy.
/// </summary>
/// <remarks>
/// These options control the retry behavior including maximum attempts, initial delay,
/// maximum delay, and the exponential backoff multiplier. The retry policy uses exponential
/// backoff to space out retry attempts, giving servers time to recover from transient failures.
/// </remarks>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts before giving up.
    /// </summary>
    /// <value>Default is 3 retries.</value>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before the first retry.
    /// </summary>
    /// <value>Default is 1 second.</value>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    /// <value>Default is 30 seconds.</value>
    /// <remarks>
    /// This cap prevents exponential backoff from producing excessively long delays.
    /// </remarks>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the multiplier for exponential backoff calculation.
    /// </summary>
    /// <value>Default is 2.0 (doubles the delay each retry).</value>
    /// <remarks>
    /// Each retry delay is calculated as: delay = previous_delay * BackoffMultiplier,
    /// up to the MaxDelay limit.
    /// </remarks>
    public double BackoffMultiplier { get; set; } = 2.0;
}
