using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Middleware that implements exponential backoff retry logic for failed requests.
/// </summary>
/// <remarks>
/// <para>
/// This middleware automatically retries failed requests with exponential backoff delays
/// between attempts. The delay increases exponentially with each retry to avoid overwhelming
/// a struggling service while still providing reasonable retry behavior.
/// </para>
/// <para>
/// The middleware can be configured to retry only specific types of exceptions or HTTP
/// status codes, and supports jitter to prevent retry storms when multiple clients are
/// retrying simultaneously.
/// </para>
/// <para>
/// Configuration keys:
/// - MaxRetries: Maximum number of retry attempts (default: 3)
/// - InitialDelayMs: Initial delay in milliseconds (default: 1000)
/// - MaxDelayMs: Maximum delay in milliseconds (default: 30000)
/// - BackoffMultiplier: Multiplier for exponential backoff (default: 2.0)
/// - UseJitter: Add random jitter to delays (default: true)
/// - RetryOnTimeout: Retry on timeout exceptions (default: true)
/// </para>
/// </remarks>
public sealed class RetryMiddleware : IPipelineMiddleware
{
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;
    private readonly int _maxDelayMs;
    private readonly double _backoffMultiplier;
    private readonly bool _useJitter;
    private readonly bool _retryOnTimeout;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryMiddleware"/> class.
    /// </summary>
    /// <param name="configuration">The middleware configuration dictionary.</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public RetryMiddleware(Dictionary<string, object> configuration, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _maxRetries = configuration.TryGetValue("MaxRetries", out object? mr) && mr is int maxRetries
            ? maxRetries
            : 3;

        _initialDelayMs = configuration.TryGetValue("InitialDelayMs", out object? id) && id is int initialDelay
            ? initialDelay
            : 1000;

        _maxDelayMs = configuration.TryGetValue("MaxDelayMs", out object? md) && md is int maxDelay
            ? maxDelay
            : 30000;

        _backoffMultiplier = configuration.TryGetValue("BackoffMultiplier", out object? bm) && bm is double backoffMultiplier
            ? backoffMultiplier
            : 2.0;

        _useJitter = configuration.TryGetValue("UseJitter", out object? uj) && uj is bool useJitter
            ? useJitter
            : true;

        _retryOnTimeout = configuration.TryGetValue("RetryOnTimeout", out object? rot) && rot is bool retryOnTimeout
            ? retryOnTimeout
            : true;

        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Invokes the middleware to execute the request with retry logic.
    /// </summary>
    /// <param name="context">The pipeline context containing the request.</param>
    /// <param name="continuation">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AggregateException">
    /// Thrown when all retry attempts have been exhausted.
    /// </exception>
    public async Task InvokeAsync(PipelineContext context, PipelineDelegate continuation)
    {
        List<Exception> exceptions = [];
        int attempt = 0;

        while (attempt <= _maxRetries)
        {
            try
            {
                await continuation(context).ConfigureAwait(false);

                // Success - record the attempt if it was a retry
                if (attempt > 0 && context.StateBox != null)
                {
                    context.StateBox.IncrementRetryCount();
                }

                return;
            }
            catch (OperationCanceledException)
            {
                // Don't retry on cancellation
                throw;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                attempt++;

                // Check if we should retry this exception
                if (!ShouldRetry(ex) || attempt > _maxRetries)
                {
                    // If this was our last attempt or we shouldn't retry, throw aggregate
                    if (exceptions.Count == 1)
                    {
                        throw;
                    }

                    throw new AggregateException(
                        $"Request failed after {attempt} attempts.", exceptions);
                }

                // Calculate delay with exponential backoff
                int delay = CalculateDelay(attempt);

                // Wait before retrying
                await Task.Delay(TimeSpan.FromMilliseconds(delay), _timeProvider, context.CancellationToken).ConfigureAwait(false);

                // Increment retry counter in state box if available
                context.StateBox?.IncrementRetryCount();
            }
        }
    }

    /// <summary>
    /// Determines if an exception should trigger a retry.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>True if the exception is retryable; otherwise, false.</returns>
    private bool ShouldRetry(Exception exception)
    {
        return exception switch
        {
            TimeoutException => _retryOnTimeout,
            TaskCanceledException => _retryOnTimeout,
            HttpRequestException => true,
            InvalidOperationException => false,
            _ => true // Retry by default for unknown exceptions
        };
    }

    /// <summary>
    /// Calculates the delay before the next retry attempt using exponential backoff.
    /// </summary>
    /// <param name="attempt">The current attempt number (1-based).</param>
    /// <returns>The delay duration in milliseconds.</returns>
    private int CalculateDelay(int attempt)
    {
        // Calculate exponential backoff: initialDelay * (multiplier ^ (attempt - 1))
        double delay = _initialDelayMs * Math.Pow(_backoffMultiplier, attempt - 1);

        // Cap at max delay
        delay = Math.Min(delay, _maxDelayMs);

        // Add jitter if enabled (±25% of the delay)
        if (_useJitter)
        {
            double jitterRange = delay * 0.25;
            double jitter = (Random.Shared.NextDouble() * 2 - 1) * jitterRange; // Random between -25% and +25%
            delay += jitter;
        }

        return (int)Math.Max(0, delay);
    }
}
