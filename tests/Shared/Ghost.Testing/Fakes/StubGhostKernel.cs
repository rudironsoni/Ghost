using Ghost.Core;

namespace Ghost.Testing.Fakes;

public sealed class StubGhostKernel : IGhostKernel
{
    private readonly List<IBrowserSession> _sessions = [];

    public Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new FakeBrowserSession();
        _sessions.Add(session);
        return Task.FromResult<IBrowserSession>(session);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (IBrowserSession session in _sessions)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        _sessions.Clear();
    }
}
