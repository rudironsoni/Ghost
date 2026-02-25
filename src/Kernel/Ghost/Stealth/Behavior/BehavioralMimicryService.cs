using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ghost.Stealth.Behavior;

/// <summary>
/// Comprehensive service for human-like browser automation behavior.
/// Combines mouse movements, scrolling, timing, and click patterns to avoid detection.
/// </summary>
public sealed partial class BehavioralMimicryService
{
    private readonly ILogger<BehavioralMimicryService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BehavioralMimicryService"/> class.
    /// </summary>
    public BehavioralMimicryService(ILogger<BehavioralMimicryService> logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Mouse = new MouseMimicry();
        Scroll = new ScrollMimicry();
        Timing = new TimingMimicry();
        Click = new ClickMimicry(Mouse, Timing);
    }

    /// <summary>
    /// Gets the mouse movement mimicry service.
    /// </summary>
    public MouseMimicry Mouse { get; }

    /// <summary>
    /// Gets the scroll mimicry service.
    /// </summary>
    public ScrollMimicry Scroll { get; }

    /// <summary>
    /// Gets the timing mimicry service.
    /// </summary>
    public TimingMimicry Timing { get; }

    /// <summary>
    /// Gets the click mimicry service.
    /// </summary>
    public ClickMimicry Click { get; }

    /// <summary>
    /// Navigates to a URL with human-like behavior (delays before/after navigation).
    /// </summary>
    /// <param name="page">The Playwright page to navigate.</param>
    /// <param name="url">The URL to navigate to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task NavigateHumanLikeAsync(
        Microsoft.Playwright.IPage page,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        LogNavigating(url);

        // Small delay before navigation (simulates thinking/URL typing)
        await Timing.ReadingDelayAsync(cancellationToken).ConfigureAwait(false);

        // Navigate
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);

        // Delay after navigation (simulates page scanning)
        await Timing.NavigationDelayAsync(cancellationToken).ConfigureAwait(false);

        LogNavigationCompleted(url);
    }

    /// <summary>
    /// Fills a form field with human-like typing and timing.
    /// </summary>
    /// <param name="page">The Playwright page containing the form.</param>
    /// <param name="element">The input element to fill.</param>
    /// <param name="text">The text to type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task FillFormFieldHumanLikeAsync(
        Microsoft.Playwright.IPage page,
        ILocator element,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(text);

        LogFillingFormField();

        // Click the field to focus it
        await Click.ClickHumanLikeAsync(page.Mouse, element, cancellationToken).ConfigureAwait(false);

        // Delay before typing (simulates thinking)
        await Timing.FormFieldDelayAsync(cancellationToken).ConfigureAwait(false);

        // Type with human-like speed (50-150ms per character)
        await element.PressSequentiallyAsync(text, new() { Delay = Random.Shared.Next(50, 151) }).ConfigureAwait(false);

        LogFormFieldFilled();
    }

    /// <summary>
    /// Scrolls to an element and clicks it with full human-like behavior.
    /// </summary>
    /// <param name="page">The Playwright page containing the element.</param>
    /// <param name="element">The element to scroll to and click.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ScrollAndClickHumanLikeAsync(
        Microsoft.Playwright.IPage page,
        ILocator element,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(element);

        LogScrollingAndClicking();

        // Scroll element into view
        await Scroll.ScrollIntoViewHumanLikeAsync(element, page.Mouse, cancellationToken).ConfigureAwait(false);

        // Brief pause after scrolling (simulates locating element)
        await Timing.ReadingDelayAsync(cancellationToken).ConfigureAwait(false);

        // Click the element
        await Click.ClickHumanLikeAsync(page.Mouse, element, cancellationToken).ConfigureAwait(false);

        LogClickCompleted();
    }

    /// <summary>
    /// Simulates human reading behavior by performing random micro-scrolls and pauses.
    /// </summary>
    /// <param name="page">The Playwright page to read.</param>
    /// <param name="durationSeconds">How long to simulate reading (in seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SimulateReadingAsync(
        Microsoft.Playwright.IPage page,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Must be positive.");
        }

        LogSimulatingReading(durationSeconds);

        DateTime endTime = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(durationSeconds);

        while (_timeProvider.GetUtcNow().UtcDateTime < endTime && !cancellationToken.IsCancellationRequested)
        {
            // Random micro-scroll
            await Scroll.MicroScrollAsync(page.Mouse, cancellationToken).ConfigureAwait(false);

            // Reading pause (1-3 seconds)
            await Timing.CustomDelayAsync(1000, 3000, cancellationToken).ConfigureAwait(false);
        }

        LogReadingCompleted();
    }

    /// <summary>
    /// Waits for a page to load with human-like behavior (random delay after load).
    /// </summary>
    /// <param name="page">The Playwright page to wait for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitForPageLoadHumanLikeAsync(
        Microsoft.Playwright.IPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        LogWaitingForPageLoad();

        // Wait for network to be idle
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

        // Human-like delay after page load (simulates scanning page)
        await Timing.NavigationDelayAsync(cancellationToken).ConfigureAwait(false);

        LogPageLoadCompleted();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating to {Url} with human-like behavior")]
    partial void LogNavigating(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigation to {Url} completed with human-like timing")]
    partial void LogNavigationCompleted(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Filling form field with human-like behavior")]
    partial void LogFillingFormField();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Form field filled with human-like timing")]
    partial void LogFormFieldFilled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scrolling to element and clicking with human-like behavior")]
    partial void LogScrollingAndClicking();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Element clicked with human-like behavior")]
    partial void LogClickCompleted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Simulating human reading for {DurationSeconds} seconds")]
    partial void LogSimulatingReading(int durationSeconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Reading simulation completed")]
    partial void LogReadingCompleted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Waiting for page load with human-like behavior")]
    partial void LogWaitingForPageLoad();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page load completed with human-like timing")]
    partial void LogPageLoadCompleted();
}
