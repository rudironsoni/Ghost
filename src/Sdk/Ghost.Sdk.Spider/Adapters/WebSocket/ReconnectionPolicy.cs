namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Defines the reconnection policy for WebSocket connections.
/// </summary>
/// <remarks>
/// This class controls how the WebSocket adapter handles connection failures
/// and automatic reconnection attempts, including backoff strategies and limits.
/// </remarks>
public class ReconnectionPolicy
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic reconnection is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically reconnect on connection failure; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts.
    /// </summary>
    /// <value>
    /// The maximum number of times to attempt reconnection. Defaults to 5.
    /// Set to -1 for unlimited attempts.
    /// </value>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the initial delay before the first reconnection attempt.
    /// </summary>
    /// <value>
    /// The delay before the first reconnection attempt. Defaults to 1 second.
    /// </value>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between reconnection attempts.
    /// </summary>
    /// <value>
    /// The maximum delay for exponential backoff. Defaults to 30 seconds.
    /// </value>
    /// <remarks>
    /// When using exponential backoff, the delay increases with each attempt but
    /// never exceeds this maximum value.
    /// </remarks>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential backoff.
    /// </summary>
    /// <value>
    /// The multiplier applied to the delay after each failed attempt. Defaults to 2.0.
    /// </value>
    /// <remarks>
    /// With a multiplier of 2.0 and initial delay of 1s, the delays will be:
    /// 1s, 2s, 4s, 8s, 16s, etc., up to the maximum delay.
    /// </remarks>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether to use exponential backoff.
    /// </summary>
    /// <value>
    /// <c>true</c> to use exponential backoff; <c>false</c> for fixed delay.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to add random jitter to delays.
    /// </summary>
    /// <value>
    /// <c>true</c> to add random jitter; otherwise, <c>false</c>. Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// Jitter helps prevent thundering herd problems when many clients reconnect
    /// simultaneously. The jitter is +/- 25% of the calculated delay.
    /// </remarks>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to reconnect on normal close.
    /// </summary>
    /// <value>
    /// <c>true</c> to reconnect even when the server closes normally; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    /// <remarks>
    /// When false, reconnection only occurs for abnormal closures (network errors,
    /// timeouts, etc.). When true, reconnection happens for all closures.
    /// </remarks>
    public bool ReconnectOnNormalClose { get; set; }

    /// <summary>
    /// Gets or sets the timeout for reconnection attempts.
    /// </summary>
    /// <value>
    /// The maximum time to wait for a reconnection to succeed. Defaults to 30 seconds.
    /// </value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconnectionPolicy"/> class.
    /// </summary>
    public ReconnectionPolicy()
    {
    }

    /// <summary>
    /// Creates a disabled reconnection policy.
    /// </summary>
    /// <returns>A new <see cref="ReconnectionPolicy"/> instance with reconnection disabled.</returns>
    public static ReconnectionPolicy Disabled()
    {
        return new ReconnectionPolicy { Enabled = false };
    }

    /// <summary>
    /// Creates a default reconnection policy.
    /// </summary>
    /// <returns>A new <see cref="ReconnectionPolicy"/> instance with default settings.</returns>
    public static ReconnectionPolicy Default()
    {
        return new ReconnectionPolicy();
    }

    /// <summary>
    /// Creates an aggressive reconnection policy with unlimited attempts.
    /// </summary>
    /// <returns>A new <see cref="ReconnectionPolicy"/> instance configured for aggressive reconnection.</returns>
    public static ReconnectionPolicy Aggressive()
    {
        return new ReconnectionPolicy
        {
            Enabled = true,
            MaxAttempts = -1,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(10),
            UseExponentialBackoff = true,
            UseJitter = true
        };
    }

    /// <summary>
    /// Calculates the delay for a given reconnection attempt.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (0-based).</param>
    /// <returns>The delay duration to wait before the next attempt.</returns>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        if (!UseExponentialBackoff)
        {
            return ApplyJitter(InitialDelay);
        }

        double delay = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attemptNumber);
        double cappedDelay = Math.Min(delay, MaxDelay.TotalMilliseconds);
        var timeSpan = TimeSpan.FromMilliseconds(cappedDelay);

        return ApplyJitter(timeSpan);
    }

    /// <summary>
    /// Applies random jitter to a delay value.
    /// </summary>
    /// <param name="delay">The base delay value.</param>
    /// <returns>The delay with jitter applied if enabled.</returns>
    private TimeSpan ApplyJitter(TimeSpan delay)
    {
        if (!UseJitter)
        {
            return delay;
        }

        double jitterRange = delay.TotalMilliseconds * 0.25; // +/- 25%
        double jitter = (Random.Shared.NextDouble() * 2 - 1) * jitterRange;
        double jitteredDelay = delay.TotalMilliseconds + jitter;

        return TimeSpan.FromMilliseconds(Math.Max(0, jitteredDelay));
    }

    /// <summary>
    /// Validates the reconnection policy.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        if (Enabled)
        {
            if (MaxAttempts < -1 || MaxAttempts == 0)
            {
                throw new ArgumentException("MaxAttempts must be -1 (unlimited) or greater than 0.", nameof(MaxAttempts));
            }

            if (InitialDelay <= TimeSpan.Zero)
            {
                throw new ArgumentException("InitialDelay must be greater than zero.", nameof(InitialDelay));
            }

            if (MaxDelay <= TimeSpan.Zero)
            {
                throw new ArgumentException("MaxDelay must be greater than zero.", nameof(MaxDelay));
            }

            if (MaxDelay < InitialDelay)
            {
                throw new ArgumentException("MaxDelay must be greater than or equal to InitialDelay.", nameof(MaxDelay));
            }

            if (BackoffMultiplier <= 1.0)
            {
                throw new ArgumentException("BackoffMultiplier must be greater than 1.0.", nameof(BackoffMultiplier));
            }

            if (ConnectionTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException("ConnectionTimeout must be greater than zero.", nameof(ConnectionTimeout));
            }
        }
    }
}
