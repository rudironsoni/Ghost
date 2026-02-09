namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for retry policies that automatically retry failed operations.
/// </summary>
/// <remarks>
/// Implementations of this interface provide retry logic with various backoff strategies
/// to handle transient failures such as network errors, timeouts, and server overload.
/// </remarks>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an asynchronous operation with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="ct">Cancellation token to observe.</param>
    /// <returns>The result of the operation after successful execution.</returns>
    /// <remarks>
    /// The operation will be retried according to the policy's configuration when
    /// retryable exceptions occur. Non-retryable exceptions are thrown immediately.
    /// </remarks>
    /// <exception cref="Exception">
    /// Throws the last exception encountered if all retry attempts are exhausted.
    /// </exception>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
}
