using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Interface for defining spider shutdown conditions.
/// </summary>
/// <remarks>
/// Conditions are evaluated to determine if a spider should gracefully close.
/// Implementations can check resource limits, time constraints, external signals,
/// or custom business logic.
/// </remarks>
public interface ICloseCondition
{
    /// <summary>
    /// Gets the human-readable name of this condition.
    /// </summary>
    /// <value>A descriptive name used in logging and diagnostics.</value>
    public string Name { get; }

    /// <summary>
    /// Determines whether this condition has been met.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the condition is met and the spider should close; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method should execute quickly and avoid blocking operations.
    /// It will be called frequently during spider execution.
    /// </remarks>
    public Task<bool> IsMetAsync(SpiderContext context, CancellationToken ct = default);
}
