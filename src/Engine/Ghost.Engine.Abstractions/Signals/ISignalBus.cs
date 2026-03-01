namespace Ghost.Engine.Abstractions.Signals;

public interface ISignalBus
{
    public Task PublishAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default);

    public ValueTask<ISignalSubscription> SubscribeAsync<TSignal>(
        Func<TSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}

public interface ISignalSubscription : IAsyncDisposable
{
}
