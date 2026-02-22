using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Resilience;

/// <summary>
/// Implements an exponential backoff retry policy with optional jitter.
/// </summary>
public sealed class RetryPolicy : IRetryPolicy
{
    private readonly AsyncLocal<int> _currentAttempt = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicy"/> class.
    /// </summary>
    /// <param name="options">Optional options override.</param>
    public RetryPolicy(RetryPolicyOptions? options = null)
    {
        Options = options ?? new RetryPolicyOptions();
    }

    /// <inheritdoc />
    public RetryPolicyOptions Options
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <inheritdoc />
    public int CurrentAttempt
    {
        get
        {
            return _currentAttempt.Value;
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Exception, bool> isRetryable)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(isRetryable);

        RetryPolicyOptions options = SnapshotOptions(Options);
        ValidateOptions(options);

        int previousAttempt = _currentAttempt.Value;
        _currentAttempt.Value = 0;

        try
        {
            while (true)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (isRetryable(ex) && _currentAttempt.Value < options.MaxRetries)
                {
                    TimeSpan delay = CalculateDelay(_currentAttempt.Value, options);
                    _currentAttempt.Value++;
                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _currentAttempt.Value = previousAttempt;
        }
    }

    /// <summary>
    /// Executes an asynchronous action without a return value using retry behavior.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="isRetryable">Predicate that determines whether the exception is retryable.</param>
    public async Task ExecuteAsync(Func<Task> action, Func<Exception, bool> isRetryable)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(isRetryable);

        await ExecuteAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, isRetryable).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an HTTP action with built-in HTTP status code and exception classification.
    /// </summary>
    /// <param name="action">The HTTP action to execute.</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> ExecuteHttpAsync(Func<Task<HttpResponseMessage>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        RetryPolicyOptions options = SnapshotOptions(Options);
        ValidateOptions(options);

        int previousAttempt = _currentAttempt.Value;
        _currentAttempt.Value = 0;

        try
        {
            while (true)
            {
                HttpResponseMessage? response = null;

                try
                {
                    response = await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (RetryableErrorClassifier.IsRetryable(ex) && _currentAttempt.Value < options.MaxRetries)
                {
                    TimeSpan delay = CalculateDelay(_currentAttempt.Value, options);
                    _currentAttempt.Value++;
                    await Task.Delay(delay).ConfigureAwait(false);
                    continue;
                }

                if (response != null &&
                    RetryableErrorClassifier.IsRetryable(response.StatusCode) &&
                    _currentAttempt.Value < options.MaxRetries)
                {
                    response.Dispose();
                    TimeSpan delay = CalculateDelay(_currentAttempt.Value, options);
                    _currentAttempt.Value++;
                    await Task.Delay(delay).ConfigureAwait(false);
                    continue;
                }

                if (response is null)
                {
                    throw new InvalidOperationException("HTTP action returned null response.");
                }

                return response;
            }
        }
        finally
        {
            _currentAttempt.Value = previousAttempt;
        }
    }

    private static TimeSpan CalculateDelay(int attempt, RetryPolicyOptions options)
    {
        TimeSpan baseDelay = options.BaseDelay;
        TimeSpan maxDelay = options.MaxDelay;
        bool useJitter = options.UseJitter;

        var exponential = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        double jitter = useJitter ? Random.Shared.NextDouble() * 0.5 : 0;
        var delay = TimeSpan.FromMilliseconds(exponential.TotalMilliseconds * (1 + jitter));
        double clamped = Math.Min(delay.TotalMilliseconds, maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(clamped);
    }

    private static RetryPolicyOptions SnapshotOptions(RetryPolicyOptions options)
    {
        return new RetryPolicyOptions
        {
            MaxRetries = options.MaxRetries,
            BaseDelay = options.BaseDelay,
            MaxDelay = options.MaxDelay,
            UseJitter = options.UseJitter
        };
    }

    private static void ValidateOptions(RetryPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetries must be non-negative.");
        }

        if (options.BaseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "BaseDelay must be positive.");
        }

        if (options.MaxDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDelay must be positive.");
        }

        if (options.MaxDelay < options.BaseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDelay must be greater than or equal to BaseDelay.");
        }
    }
}
