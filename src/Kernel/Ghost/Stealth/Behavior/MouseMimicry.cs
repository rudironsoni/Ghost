using Microsoft.Playwright;

namespace Ghost.Stealth.Behavior;

/// <summary>
/// Provides human-like mouse movement using Bezier curves to avoid detection.
/// </summary>
public sealed class MouseMimicry
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MouseMimicry"/> class.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    public MouseMimicry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Moves the mouse to the target position using a Bezier curve path.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="targetX">Target X coordinate.</param>
    /// <param name="targetY">Target Y coordinate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MoveHumanLikeAsync(
        IMouse mouse,
        float targetX,
        float targetY,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        // Start from (0, 0) as we cannot get current position from Playwright
        (float startX, float startY) = (0f, 0f);

        // Don't move if we're already at the target
        if (Math.Abs(startX - targetX) < 1 && Math.Abs(startY - targetY) < 1)
        {
            return;
        }

        // Generate random control point for Bezier curve
        (float controlX, float controlY) = GetRandomControlPoint(startX, startY, targetX, targetY);

        // Random number of steps (20-50 for smooth movement)
        int steps = Random.Shared.Next(20, 51);
        double stepIncrement = 1.0 / steps;

        for (int i = 0; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double t = i * stepIncrement;
            (float x, float y) = CalculateBezierPoint(t, (startX, startY), (controlX, controlY), (targetX, targetY));

            await mouse.MoveAsync(x, y).ConfigureAwait(false);

            // Variable delay between steps (5-20ms) for human-like speed variance
            if (i < steps)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 21)), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Calculates a point on a quadratic Bezier curve at parameter t.
    /// Formula: B(t) = (1-t)²P₀ + 2(1-t)tP₁ + t²P₂
    /// </summary>
    private static (float x, float y) CalculateBezierPoint(
        double t,
        (float x, float y) start,
        (float x, float y) control,
        (float x, float y) end)
    {
        double oneMinusT = 1 - t;
        double oneMinusTSquared = oneMinusT * oneMinusT;
        double tSquared = t * t;

        float x = (float)(oneMinusTSquared * start.x + (2 * oneMinusT * t * control.x) + (tSquared * end.x));
        float y = (float)(oneMinusTSquared * start.y + (2 * oneMinusT * t * control.y) + (tSquared * end.y));

        return (x, y);
    }

    /// <summary>
    /// Generates a random control point for the Bezier curve.
    /// The control point is offset from the midpoint to create a natural curve.
    /// </summary>
    private static (float x, float y) GetRandomControlPoint(
        float startX,
        float startY,
        float endX,
        float endY)
    {
        // Calculate midpoint
        float midX = (startX + endX) / 2;
        float midY = (startY + endY) / 2;

        // Calculate distance for offset (10-30% of total distance)
        double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        double offsetMagnitude = distance * (Random.Shared.NextDouble() * 0.2 + 0.1); // 10-30%

        // Random angle offset
        double angle = Random.Shared.NextDouble() * Math.PI * 2;

        // Apply offset to midpoint
        float controlX = (float)(midX + (offsetMagnitude * Math.Cos(angle)));
        float controlY = (float)(midY + (offsetMagnitude * Math.Sin(angle)));

        return (controlX, controlY);
    }
}
