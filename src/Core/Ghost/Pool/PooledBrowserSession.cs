using Ghost.Core;
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

    public bool IsExpired(TimeSpan maxAge)
    {
        return DateTime.UtcNow - CreatedAt > maxAge;
    }

    public void Dispose()
    {
        // Fire-and-forget async disposal to avoid blocking
        // Consumers should use DisposeAsync() for proper cleanup
        _ = DisposeAsync().AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        if (Session is not null)
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
