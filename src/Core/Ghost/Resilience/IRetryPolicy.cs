using System;
using System.Threading.Tasks;

namespace Ghost.Resilience;

/// <summary>
/// Defines a retry policy for executing asynchronous actions with transient failure handling.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an asynchronous action with retry behavior for retryable failures.
    /// </summary>
    /// <typeparam name="T">The result type of the action.</typeparam>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="isRetryable">Predicate that determines whether the exception is retryable.</param>
    /// <returns>The result of the action if successful.</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Exception, bool> isRetryable);

    /// <summary>
    /// Executes an asynchronous action without a return value with retry behavior.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="isRetryable">Predicate that determines whether the exception is retryable.</param>
    Task ExecuteAsync(Func<Task> action, Func<Exception, bool> isRetryable);

    /// <summary>
    /// Gets or sets the retry policy configuration options.
    /// </summary>
    RetryPolicyOptions Options { get; set; }

    /// <summary>
    /// Gets the current attempt number for the active execution.
    /// Returns 0 when no execution is in progress.
    /// </summary>
    int CurrentAttempt { get; }
}
