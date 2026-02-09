namespace Ghost.Sdk.Middleware;

/// <summary>
/// Implements retry logic with exponential backoff for handling transient failures.
/// </summary>
/// <remarks>
/// This policy automatically retries operations that fail with retryable exceptions,
/// using exponential backoff to progressively increase the delay between attempts.
/// This approach helps prevent overwhelming struggling servers while maximizing the
/// chance of eventual success for transient failures.
/// </remarks>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffRetryPolicy"/> class.
    /// </summary>
    /// <param name="options">Configuration options for retry behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public ExponentialBackoffRetryPolicy(RetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Executes an asynchronous operation with exponential backoff retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>The result of the operation after successful execution.</returns>
    /// <remarks>
    /// The operation will be retried up to MaxRetries times when retryable exceptions occur.
    /// The delay between retries follows exponential backoff: delay = InitialDelay * (BackoffMultiplier ^ attempt),
    /// capped at MaxDelay. Retryable exceptions include HttpRequestException, TimeoutException,
    /// and TaskCanceledException (when not triggered by the cancellation token).
    /// </remarks>
    /// <exception cref="Exception">
    /// Throws the last exception encountered if all retry attempts are exhausted,
    /// or immediately if the exception is not retryable.
    /// </exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var attempt = 0;
        var delay = _options.InitialDelay;

        while (true)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetryable(ex, ct) && attempt < _options.MaxRetries)
            {
                attempt++;
                await Task.Delay(delay, ct).ConfigureAwait(false);

                // Calculate next delay with exponential backoff
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(
                        delay.TotalMilliseconds * _options.BackoffMultiplier,
                        _options.MaxDelay.TotalMilliseconds
                    )
                );
            }
        }
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <param name="ct">The cancellation token to check if the exception was triggered by cancellation.</param>
    /// <returns>True if the exception represents a transient failure; otherwise, false.</returns>
    /// <remarks>
    /// Retryable exceptions include:
    /// - HttpRequestException: Network errors, connection failures
    /// - TimeoutException: Request timeouts
    /// - TaskCanceledException: Operation timeouts (but not user-initiated cancellation)
    /// </remarks>
    private static bool IsRetryable(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            HttpRequestException => true,
            TimeoutException => true,
            // Only retry TaskCanceledException if it wasn't triggered by our cancellation token
            TaskCanceledException => !ct.IsCancellationRequested,
            _ => false
        };
    }
}
