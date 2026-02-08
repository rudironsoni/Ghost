using Microsoft.Playwright;

namespace Ghost.Stealth.Behavior;

/// <summary>
/// Provides human-like mouse movement using Bezier curves to avoid detection.
/// </summary>
public sealed class MouseMimicry
{
    private readonly Random _random = new();

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
        var (startX, startY) = (0f, 0f);

        // Don't move if we're already at the target
        if (Math.Abs(startX - targetX) < 1 && Math.Abs(startY - targetY) < 1)
        {
            return;
        }

        // Generate random control point for Bezier curve
        var (controlX, controlY) = GetRandomControlPoint(startX, startY, targetX, targetY);

        // Random number of steps (20-50 for smooth movement)
        var steps = _random.Next(20, 51);
        var stepIncrement = 1.0 / steps;

        for (var i = 0; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var t = i * stepIncrement;
            var (x, y) = CalculateBezierPoint(t, (startX, startY), (controlX, controlY), (targetX, targetY));

            await mouse.MoveAsync(x, y);

            // Variable delay between steps (5-20ms) for human-like speed variance
            if (i < steps)
            {
                await Task.Delay(_random.Next(5, 21), cancellationToken);
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
        var oneMinusT = 1 - t;
        var oneMinusTSquared = oneMinusT * oneMinusT;
        var tSquared = t * t;

        var x = (float)(oneMinusTSquared * start.x + (2 * oneMinusT * t * control.x) + (tSquared * end.x));
        var y = (float)(oneMinusTSquared * start.y + (2 * oneMinusT * t * control.y) + (tSquared * end.y));

        return (x, y);
    }

    /// <summary>
    /// Generates a random control point for the Bezier curve.
    /// The control point is offset from the midpoint to create a natural curve.
    /// </summary>
    private (float x, float y) GetRandomControlPoint(
        float startX,
        float startY,
        float endX,
        float endY)
    {
        // Calculate midpoint
        var midX = (startX + endX) / 2;
        var midY = (startY + endY) / 2;

        // Calculate distance for offset (10-30% of total distance)
        var distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        var offsetMagnitude = distance * (_random.NextDouble() * 0.2 + 0.1); // 10-30%

        // Random angle offset
        var angle = _random.NextDouble() * Math.PI * 2;

        // Apply offset to midpoint
        var controlX = (float)(midX + (offsetMagnitude * Math.Cos(angle)));
        var controlY = (float)(midY + (offsetMagnitude * Math.Sin(angle)));

        return (controlX, controlY);
    }
}
