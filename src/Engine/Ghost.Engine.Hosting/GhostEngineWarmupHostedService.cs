using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Scheduler;
using Microsoft.Extensions.Hosting;

namespace Ghost.Engine.Hosting;

internal sealed class GhostEngineWarmupHostedService : IHostedService
{
    private readonly IGhostEngine _engine;
    private readonly IRequestScheduler _scheduler;

    public GhostEngineWarmupHostedService(IGhostEngine engine, IRequestScheduler scheduler)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _engine;
        _ = await _scheduler.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
