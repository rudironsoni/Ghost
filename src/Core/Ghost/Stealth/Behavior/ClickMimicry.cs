using Microsoft.Playwright;

namespace Ghost.Stealth.Behavior;

/// <summary>
/// Provides human-like click behavior with mouse movement and randomized targeting.
/// </summary>
public sealed class ClickMimicry
{
    private readonly MouseMimicry _mouseMimicry;
    private readonly TimingMimicry _timingMimicry;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickMimicry"/> class.
    /// </summary>
    /// <param name="mouseMimicry">Mouse movement mimicry service.</param>
    /// <param name="timingMimicry">Timing mimicry service.</param>
    public ClickMimicry(MouseMimicry mouseMimicry, TimingMimicry timingMimicry)
    {
        _mouseMimicry = mouseMimicry ?? throw new ArgumentNullException(nameof(mouseMimicry));
        _timingMimicry = timingMimicry ?? throw new ArgumentNullException(nameof(timingMimicry));
    }

    /// <summary>
    /// Clicks an element in a human-like manner with cursor movement and timing delays.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="element">The element to click.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClickHumanLikeAsync(
        IMouse mouse,
        ILocator element,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        ArgumentNullException.ThrowIfNull(element);

        // Get element bounding box
        var box = await element.BoundingBoxAsync() ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate random click position within element bounds
        // Avoid clicking too close to edges (5px margin)
        var margin = 5f;
        var usableWidth = Math.Max(box.Width - (2 * margin), 1);
        var usableHeight = Math.Max(box.Height - (2 * margin), 1);

        var offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        var offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        var clickX = box.X + offsetX;
        var clickY = box.Y + offsetY;

        // Move mouse to click position using Bezier curve
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken);

        // Short pause before clicking (simulates human aim/focus)
        await _timingMimicry.PreClickDelayAsync(cancellationToken);

        // Perform the click
        await mouse.ClickAsync(clickX, clickY);

        // Pause after clicking (simulates waiting for response)
        await _timingMimicry.PostClickDelayAsync(cancellationToken);
    }

    /// <summary>
    /// Double-clicks an element in a human-like manner.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="element">The element to double-click.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DoubleClickHumanLikeAsync(
        IMouse mouse,
        ILocator element,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        ArgumentNullException.ThrowIfNull(element);

        // Get element bounding box
        var box = await element.BoundingBoxAsync() ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate click position (same for both clicks)
        var margin = 5f;
        var usableWidth = Math.Max(box.Width - (2 * margin), 1);
        var usableHeight = Math.Max(box.Height - (2 * margin), 1);

        var offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        var offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        var clickX = box.X + offsetX;
        var clickY = box.Y + offsetY;

        // Move mouse to click position
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken);

        // Short pause before clicking
        await _timingMimicry.PreClickDelayAsync(cancellationToken);

        // Perform double-click
        await mouse.DblClickAsync(clickX, clickY);

        // Pause after clicking
        await _timingMimicry.PostClickDelayAsync(cancellationToken);
    }

    /// <summary>
    /// Right-clicks an element in a human-like manner.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="element">The element to right-click.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RightClickHumanLikeAsync(
        IMouse mouse,
        ILocator element,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        ArgumentNullException.ThrowIfNull(element);

        // Get element bounding box
        var box = await element.BoundingBoxAsync() ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate click position
        var margin = 5f;
        var usableWidth = Math.Max(box.Width - (2 * margin), 1);
        var usableHeight = Math.Max(box.Height - (2 * margin), 1);

        var offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        var offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        var clickX = box.X + offsetX;
        var clickY = box.Y + offsetY;

        // Move mouse to click position
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken);

        // Short pause before clicking
        await _timingMimicry.PreClickDelayAsync(cancellationToken);

        // Perform right-click
        await mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right });

        // Pause after clicking
        await _timingMimicry.PostClickDelayAsync(cancellationToken);
    }
}
