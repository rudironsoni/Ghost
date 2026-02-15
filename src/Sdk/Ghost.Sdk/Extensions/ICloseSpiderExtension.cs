using Ghost.Sdk.Spider.Contracts;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Interface for monitoring spider shutdown conditions.
/// </summary>
/// <remarks>
/// Implementations evaluate registered conditions to determine when a spider should
/// gracefully close. This enables automatic shutdown based on resource limits, time
/// constraints, or custom business logic.
/// </remarks>
public interface ICloseSpiderExtension
{
    /// <summary>
    /// Determines whether the spider should close based on registered conditions.
    /// </summary>
    /// <param name="context">The current spider execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if any condition is met and the spider should close; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method is typically called after each request or at regular intervals.
    /// Conditions are evaluated in registration order, and the first matching condition
    /// triggers a shutdown signal.
    /// </remarks>
    public Task<bool> ShouldCloseAsync(SpiderContext context, CancellationToken ct = default);
}
