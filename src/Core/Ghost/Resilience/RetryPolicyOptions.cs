using System;

namespace Ghost.Resilience;

/// <summary>
/// Configuration options for retry policies.
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>
    /// Maximum number of retry attempts to perform before giving up.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay used for exponential backoff calculations.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay cap for exponential backoff.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to add jitter to delay calculations.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}
