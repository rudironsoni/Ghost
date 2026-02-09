using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Close condition that triggers when the spider has processed a maximum number of pages (requests).
/// </summary>
/// <remarks>
/// This condition is useful for limiting crawl depth or testing with a bounded number of requests.
/// Evaluates the <see cref="SpiderContext.RequestCount"/> property.
/// </remarks>
public sealed class MaxPagesCondition : ICloseCondition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxPagesCondition"/> class.
    /// </summary>
    /// <param name="maxPages">The maximum number of pages before closing.</param>
    /// <exception cref="ArgumentException">Thrown when maxPages is less than or equal to zero.</exception>
    public MaxPagesCondition(int maxPages)
    {
        if (maxPages <= 0)
        {
            throw new ArgumentException("MaxPages must be greater than zero", nameof(maxPages));
        }

        MaxPages = maxPages;
    }

    /// <inheritdoc/>
    public string Name => $"MaxPages({MaxPages})";

    /// <summary>
    /// Determines whether the maximum page count has been reached.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the request count is greater than or equal to the maximum; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public Task<bool> IsMetAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.RequestCount >= MaxPages);
    }

    /// <summary>
    /// Gets the configured maximum page count.
    /// </summary>
    /// <value>The maximum number of pages before the condition is met.</value>
    public int MaxPages { get; }
}
