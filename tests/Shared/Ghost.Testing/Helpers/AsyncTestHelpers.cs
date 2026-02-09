namespace Ghost.Testing.Helpers;

/// <summary>
/// Helper utilities for async test scenarios including timeouts,
/// polling, and common async patterns.
/// </summary>
public static class AsyncTestHelpers
{
    /// <summary>
    /// Polls a condition until it returns true or timeout is reached.
    /// </summary>
    public static async Task<bool> PollAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? interval = null)
    {
        interval ??= TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(interval.Value);
        }

        return false;
    }

    /// <summary>
    /// Waits for a task to complete or throws TimeoutException.
    /// </summary>
    public static async Task<T> WithTimeoutAsync<T>(Task<T> task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));

        if (completedTask != task)
        {
            throw new TimeoutException($"Operation did not complete within {timeout.TotalSeconds}s");
        }

        cts.Cancel();
        return await task;
    }

    /// <summary>
    /// Retries an operation a specified number of times with exponential backoff.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null)
    {
        initialDelay ??= TimeSpan.FromMilliseconds(100);
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < maxAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts >= maxAttempts)
                {
                    break;
                }

                var delay = TimeSpan.FromMilliseconds(
                    initialDelay.Value.TotalMilliseconds * Math.Pow(2, attempts - 1));
                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException(
            $"Operation failed after {maxAttempts} attempts",
            lastException);
    }

    /// <summary>
    /// Creates a cancellation token that cancels after the specified timeout.
    /// </summary>
    public static CancellationToken CreateTimeoutToken(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        return cts.Token;
    }
}
