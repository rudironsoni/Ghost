using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Monitors spider execution and determines when to gracefully close based on registered conditions.
/// </summary>
/// <remarks>
/// This extension evaluates multiple shutdown conditions in order, allowing spiders to
/// automatically close when resource limits, time constraints, or custom conditions are met.
/// Designed for use in long-running scraping operations where graceful shutdown is essential.
/// </remarks>
public sealed class CloseSpiderExtension : ICloseSpiderExtension
{
    private readonly List<ICloseCondition> _conditions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloseSpiderExtension"/> class.
    /// </summary>
    /// <param name="conditions">The collection of conditions to evaluate for shutdown.</param>
    /// <exception cref="ArgumentNullException">Thrown when conditions is null.</exception>
    /// <remarks>
    /// Conditions are evaluated in the order provided. The first condition that returns
    /// <c>true</c> will trigger a shutdown signal.
    /// </remarks>
    public CloseSpiderExtension(IEnumerable<ICloseCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        _conditions = conditions.ToList();
    }

    /// <summary>
    /// Determines whether the spider should close based on registered conditions.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if any condition is met and the spider should close; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Conditions are evaluated sequentially. Evaluation stops at the first condition
    /// that returns <c>true</c>. Returns <c>false</c> if no conditions are registered or
    /// all conditions return <c>false</c>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public async Task<bool> ShouldCloseAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (ICloseCondition condition in _conditions)
        {
            if (await condition.IsMetAsync(context, ct).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the count of registered conditions.
    /// </summary>
    /// <value>The number of conditions that will be evaluated.</value>
    /// <remarks>
    /// This property is useful for diagnostics and testing to verify
    /// the extension is properly configured.
    /// </remarks>
    public int ConditionCount => _conditions.Count;

    /// <summary>
    /// Gets the registered conditions.
    /// </summary>
    /// <value>A read-only collection of conditions.</value>
    /// <remarks>
    /// Exposed for diagnostics and testing purposes. The returned collection
    /// is a snapshot and modifications will not affect the extension.
    /// </remarks>
    public IReadOnlyList<ICloseCondition> Conditions => _conditions.AsReadOnly();
}
