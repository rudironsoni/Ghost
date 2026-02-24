using Microsoft.Playwright;

namespace Ghost.Stealth.Behavior;

/// <summary>
/// Provides human-like scrolling with acceleration and deceleration patterns.
/// </summary>
public sealed class ScrollMimicry
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollMimicry"/> class.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    public ScrollMimicry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Scrolls the page in a human-like manner with variable speed and timing.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="deltaY">Total vertical scroll amount (positive = down, negative = up).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ScrollHumanLikeAsync(
        IMouse mouse,
        int deltaY,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        if (deltaY == 0)
        {
            return;
        }

        // Random number of steps (10-30 for smooth scrolling)
        int steps = Random.Shared.Next(10, 31);

        for (int i = 0; i < steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Calculate speed factor using sine wave for acceleration/deceleration
            // This creates a smooth ease-in-ease-out effect
            double progress = (double)i / steps;
            double speedFactor = Math.Sin(progress * Math.PI);

            // Base step size with speed variation
            double baseStepSize = (double)deltaY / steps;
            double stepSize = baseStepSize * (0.5 + speedFactor); // Range: 50-150% of base

            // Perform scroll step
            await mouse.WheelAsync(0, (float)stepSize).ConfigureAwait(false);

            // Variable delay between steps (20-100ms) for natural rhythm
            if (i < steps - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 101)), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Scrolls an element into view with human-like behavior.
    /// </summary>
    /// <param name="element">The element to scroll to.</param>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ScrollIntoViewHumanLikeAsync(
        ILocator element,
        IMouse mouse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(mouse);

        // Get element position
        LocatorBoundingBoxResult? box = await element.BoundingBoxAsync().ConfigureAwait(false);
        if (box is null)
        {
            // Element not visible, use default scroll into view
            await element.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            return;
        }

        // Calculate scroll distance needed
        Microsoft.Playwright.IPage page = element.Page;
        PageViewportSizeResult? viewportSize = page.ViewportSize;

        if (viewportSize is null)
        {
            // No viewport info, use default
            await element.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            return;
        }

        // Check if element is already in viewport
        bool isInViewport = box.Y >= 0 && box.Y + box.Height <= viewportSize.Height;
        if (isInViewport)
        {
            return;
        }

        // Calculate scroll distance to center element in viewport
        int scrollY = (int)(box.Y - (viewportSize.Height / 2) + (box.Height / 2));

        // Perform human-like scroll
        await ScrollHumanLikeAsync(mouse, scrollY, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a random micro-scroll to simulate human reading behavior.
    /// </summary>
    /// <param name="mouse">The mouse interface from Playwright page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MicroScrollAsync(
        IMouse mouse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        // Small random scroll (50-150 pixels)
        int deltaY = Random.Shared.Next(50, 151);

        // Random direction (60% down, 40% up - humans tend to scroll down more)
        if (Random.Shared.NextDouble() < 0.4)
        {
            deltaY = -deltaY;
        }

        await ScrollHumanLikeAsync(mouse, deltaY, cancellationToken).ConfigureAwait(false);
    }
}
