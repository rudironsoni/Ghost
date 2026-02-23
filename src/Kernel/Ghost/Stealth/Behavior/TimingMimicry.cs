namespace Ghost.Stealth.Behavior;

/// <summary>
/// Provides human-like timing delays between actions to avoid detection.
/// </summary>
public sealed class TimingMimicry
{
    private readonly Random _random;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingMimicry"/> class.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    public TimingMimicry(TimeProvider? timeProvider = null)
    {
        _random = new Random();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Delay between page navigations (2-5 seconds).
    /// </summary>
    public async Task NavigationDelayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(2000, 5001)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Short delay before clicking an element (0.5-1.5 seconds).
    /// </summary>
    public async Task PreClickDelayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(500, 1501)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delay after clicking an element (1-3 seconds).
    /// </summary>
    public async Task PostClickDelayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(1000, 3001)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delay between form field interactions (0.2-0.8 seconds).
    /// </summary>
    public async Task FormFieldDelayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(200, 801)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Very short delay for reading/scanning content (0.5-2 seconds).
    /// </summary>
    public async Task ReadingDelayAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(500, 2001)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Custom delay with specified range.
    /// </summary>
    /// <param name="minMilliseconds">Minimum delay in milliseconds.</param>
    /// <param name="maxMilliseconds">Maximum delay in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CustomDelayAsync(
        int minMilliseconds,
        int maxMilliseconds,
        CancellationToken cancellationToken = default)
    {
        if (minMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minMilliseconds), "Must be non-negative.");
        }

        if (maxMilliseconds <= minMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMilliseconds),
                "Must be greater than minMilliseconds.");
        }

        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(minMilliseconds, maxMilliseconds + 1)), _timeProvider, cancellationToken).ConfigureAwait(false);
    }
}
