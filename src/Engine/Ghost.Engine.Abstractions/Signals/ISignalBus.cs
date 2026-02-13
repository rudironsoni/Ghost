namespace Ghost.Engine.Abstractions.Signals;

public interface ISignalBus
{
    Task PublishAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default);

    ValueTask<ISignalSubscription> SubscribeAsync<TSignal>(
        Func<TSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);
}

public interface ISignalSubscription : IAsyncDisposable
{
}
