using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Indeed.Internal;

/// <summary>
/// Handles retry logic for Indeed API requests.
/// Single responsibility: Retry orchestration.
/// </summary>
public sealed class IndeedRetryHandler
{
    private readonly ILogger<IndeedRetryHandler>? _logger;
    private readonly int _maxRetries;
    private readonly int _baseDelayMs;

    public IndeedRetryHandler(ILogger<IndeedRetryHandler>? logger = null, int maxRetries = 3, int baseDelayMs = 1000)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _baseDelayMs = baseDelayMs;
    }

    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> shouldRetry,
        CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                T result = await operation().ConfigureAwait(false);

                if (!shouldRetry(result))
                {
                    return result;
                }

                // Result indicates retry is needed
                if (attempt < _maxRetries - 1)
                {
                    await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                if (attempt < _maxRetries - 1)
                {
                    await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        if (lastException != null)
        {
            throw lastException;
        }

        return await operation().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async operation with retry logic.
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<(T Result, bool Success)>> operation,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            (T result, bool success) = await operation().ConfigureAwait(false);

            if (success)
            {
                return result;
            }

            if (attempt < _maxRetries - 1)
            {
                await DelayForRetryAsync(attempt, ct).ConfigureAwait(false);
            }
        }

        // Return default after all retries exhausted
        return default!;
    }

    /// <summary>
    /// Delays for the specified retry attempt using exponential backoff.
    /// </summary>
    public async Task DelayForRetryAsync(int attempt, CancellationToken ct)
    {
        int delayMs = (int)(Math.Pow(2, attempt) * _baseDelayMs);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delays for the specified retry attempt using exponential backoff with custom base.
    /// </summary>
    public async Task DelayForRetryAsync(int attempt, int baseMs, CancellationToken ct)
    {
        int delayMs = (int)(Math.Pow(2, attempt) * baseMs);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
    }
}
