using Ghost.Core;
using Ghost.Stealth;

namespace Ghost.Pool;

public sealed class PooledBrowserSession : IDisposable
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
        Session.DisposeAsync().AsTask().Wait();
    }
}
