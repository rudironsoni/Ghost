using Ghost.Kernel;
using Ghost.Stealth;

namespace Ghost.Pool;

public sealed class PooledBrowserSession : IDisposable, IAsyncDisposable
{
    public required IBrowserSession Session { get; set; }
    public required Tier Tier { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastUsedAt { get; set; }
    public required bool IsAvailable { get; set; }
    public required int UseCount { get; set; }

    public bool IsExpired(TimeSpan maxAge, TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        return timeProvider.GetUtcNow().DateTime - CreatedAt > maxAge;
    }

    public void Dispose()
    {
        // Synchronously dispose by blocking on the async operation
        // This is the recommended pattern when implementing both IDisposable and IAsyncDisposable
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Session is not null)
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
