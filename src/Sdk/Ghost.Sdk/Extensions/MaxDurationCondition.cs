using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Close condition that triggers when the spider has been running for a maximum duration.
/// </summary>
/// <remarks>
/// This condition is useful for time-bounded scraping operations or preventing runaway spiders.
/// Evaluates the elapsed time since <see cref="SpiderContext.StartTime"/>.
/// </remarks>
public sealed class MaxDurationCondition : ICloseCondition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxDurationCondition"/> class.
    /// </summary>
    /// <param name="maxDuration">The maximum duration before closing.</param>
    /// <exception cref="ArgumentException">Thrown when maxDuration is less than or equal to zero.</exception>
    public MaxDurationCondition(TimeSpan maxDuration)
    {
        if (maxDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("MaxDuration must be positive", nameof(maxDuration));
        }

        MaxDuration = maxDuration;
    }

    /// <inheritdoc/>
    public string Name => $"MaxDuration({MaxDuration})";

    /// <summary>
    /// Determines whether the maximum duration has been exceeded.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the spider has been running longer than or equal to the maximum duration; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public Task<bool> IsMetAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.Duration >= MaxDuration);
    }

    /// <summary>
    /// Gets the configured maximum duration.
    /// </summary>
    /// <value>The maximum duration before the condition is met.</value>
    public TimeSpan MaxDuration { get; }
}
