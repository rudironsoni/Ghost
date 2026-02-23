namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Implements a token bucket rate limiting algorithm for controlling request throughput.
/// </summary>
/// <remarks>
/// <para>
/// The token bucket algorithm allows burst traffic while maintaining an average rate limit.
/// Tokens are added to the bucket at a fixed rate up to a maximum capacity. Each request
/// consumes one token. If no tokens are available, the request must wait.
/// </para>
/// <para>
/// This implementation is thread-safe and uses lock-free operations where possible for
/// optimal performance in high-concurrency scenarios.
/// </para>
/// </remarks>
public sealed class TokenBucketRateLimiter
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly double _tokensPerSecond;
    private readonly TimeProvider _timeProvider;
    private double _tokens;
    private DateTime _lastRefill;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketRateLimiter"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens the bucket can hold (burst size).</param>
    /// <param name="tokensPerSecond">The rate at which tokens are added to the bucket.</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when capacity or tokensPerSecond is less than or equal to zero.
    /// </exception>
    public TokenBucketRateLimiter(int capacity, double tokensPerSecond, TimeProvider? timeProvider = null)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
        if (tokensPerSecond <= 0)
            throw new ArgumentException("Tokens per second must be greater than zero.", nameof(tokensPerSecond));

        _capacity = capacity;
        _tokensPerSecond = tokensPerSecond;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tokens = capacity;
        _lastRefill = _timeProvider.GetUtcNow().DateTime;
    }

    /// <summary>
    /// Gets the current number of available tokens.
    /// </summary>
    public double AvailableTokens
    {
        get
        {
            lock (_lock)
            {
                RefillTokens();
                return _tokens;
            }
        }
    }

    /// <summary>
    /// Attempts to acquire a token from the bucket without waiting.
    /// </summary>
    /// <returns>True if a token was acquired; false if no tokens are available.</returns>
    public bool TryAcquire()
    {
        lock (_lock)
        {
            RefillTokens();

            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Waits until a token becomes available and then acquires it.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the wait operation.</param>
    /// <returns>A task that completes when a token has been acquired.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan waitTime;
            lock (_lock)
            {
                RefillTokens();

                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return;
                }

                // Calculate how long to wait for the next token
                double tokensNeeded = 1.0 - _tokens;
                waitTime = TimeSpan.FromSeconds(tokensNeeded / _tokensPerSecond);
            }

            // Wait outside the lock to avoid blocking other threads
            await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Refills the bucket based on elapsed time since last refill.
    /// Must be called with lock held.
    /// </summary>
    private void RefillTokens()
    {
        DateTime now = _timeProvider.GetUtcNow().DateTime;
        double elapsed = (now - _lastRefill).TotalSeconds;

        if (elapsed > 0)
        {
            double tokensToAdd = elapsed * _tokensPerSecond;
            _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }

    /// <summary>
    /// Resets the bucket to its full capacity.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _tokens = _capacity;
            _lastRefill = _timeProvider.GetUtcNow().DateTime;
        }
    }
}
