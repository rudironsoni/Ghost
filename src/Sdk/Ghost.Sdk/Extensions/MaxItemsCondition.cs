using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Close condition that triggers when the spider has processed a maximum number of items.
/// </summary>
/// <remarks>
/// This condition is useful for limiting the output size of a spider or testing
/// with a sample of data. Evaluates the <see cref="SpiderContext.ItemCount"/> property.
/// </remarks>
public sealed class MaxItemsCondition : ICloseCondition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxItemsCondition"/> class.
    /// </summary>
    /// <param name="maxItems">The maximum number of items before closing.</param>
    /// <exception cref="ArgumentException">Thrown when maxItems is less than or equal to zero.</exception>
    public MaxItemsCondition(int maxItems)
    {
        if (maxItems <= 0)
        {
            throw new ArgumentException("MaxItems must be greater than zero", nameof(maxItems));
        }

        MaxItems = maxItems;
    }

    /// <inheritdoc/>
    public string Name => $"MaxItems({MaxItems})";

    /// <summary>
    /// Determines whether the maximum item count has been reached.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the item count is greater than or equal to the maximum; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public Task<bool> IsMetAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.ItemCount >= MaxItems);
    }

    /// <summary>
    /// Gets the configured maximum item count.
    /// </summary>
    /// <value>The maximum number of items before the condition is met.</value>
    public int MaxItems { get; }
}
