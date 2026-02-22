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
        ArgumentNullException.ThrowIfNull(mouseMimicry);
        ArgumentNullException.ThrowIfNull(timingMimicry);
        _mouseMimicry = mouseMimicry;
        _timingMimicry = timingMimicry;
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
        LocatorBoundingBoxResult box = await element.BoundingBoxAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate random click position within element bounds
        // Avoid clicking too close to edges (5px margin)
        float margin = 5f;
        float usableWidth = Math.Max(box.Width - (2 * margin), 1);
        float usableHeight = Math.Max(box.Height - (2 * margin), 1);

        float offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        float offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        float clickX = box.X + offsetX;
        float clickY = box.Y + offsetY;

        // Move mouse to click position using Bezier curve
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken).ConfigureAwait(false);

        // Short pause before clicking (simulates human aim/focus)
        await _timingMimicry.PreClickDelayAsync(cancellationToken).ConfigureAwait(false);

        // Perform the click
        await mouse.ClickAsync(clickX, clickY).ConfigureAwait(false);

        // Pause after clicking (simulates waiting for response)
        await _timingMimicry.PostClickDelayAsync(cancellationToken).ConfigureAwait(false);
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
        LocatorBoundingBoxResult box = await element.BoundingBoxAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate click position (same for both clicks)
        float margin = 5f;
        float usableWidth = Math.Max(box.Width - (2 * margin), 1);
        float usableHeight = Math.Max(box.Height - (2 * margin), 1);

        float offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        float offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        float clickX = box.X + offsetX;
        float clickY = box.Y + offsetY;

        // Move mouse to click position
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken).ConfigureAwait(false);

        // Short pause before clicking
        await _timingMimicry.PreClickDelayAsync(cancellationToken).ConfigureAwait(false);

        // Perform double-click
        await mouse.DblClickAsync(clickX, clickY).ConfigureAwait(false);

        // Pause after clicking
        await _timingMimicry.PostClickDelayAsync(cancellationToken).ConfigureAwait(false);
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
        LocatorBoundingBoxResult box = await element.BoundingBoxAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Element is not visible or does not have a bounding box.");

        // Calculate click position
        float margin = 5f;
        float usableWidth = Math.Max(box.Width - (2 * margin), 1);
        float usableHeight = Math.Max(box.Height - (2 * margin), 1);

        float offsetX = margin + (float)(_random.NextDouble() * usableWidth);
        float offsetY = margin + (float)(_random.NextDouble() * usableHeight);

        float clickX = box.X + offsetX;
        float clickY = box.Y + offsetY;

        // Move mouse to click position
        await _mouseMimicry.MoveHumanLikeAsync(mouse, clickX, clickY, cancellationToken).ConfigureAwait(false);

        // Short pause before clicking
        await _timingMimicry.PreClickDelayAsync(cancellationToken).ConfigureAwait(false);

        // Perform right-click
        await mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right }).ConfigureAwait(false);

        // Pause after clicking
        await _timingMimicry.PostClickDelayAsync(cancellationToken).ConfigureAwait(false);
    }
}
