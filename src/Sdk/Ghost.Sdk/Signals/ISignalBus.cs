namespace Ghost.Sdk.Signals;

/// <summary>
/// Event bus for spider lifecycle signals.
/// </summary>
public interface ISignalBus
{
    /// <summary>
    /// Emits a signal to all registered handlers.
    /// </summary>
    /// <typeparam name="T">The type of signal to emit.</typeparam>
    /// <param name="signal">The signal instance to emit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all handlers have processed the signal.</returns>
    Task EmitAsync<T>(T signal, CancellationToken ct = default) where T : ISignal;

    /// <summary>
    /// Subscribes a handler to signals of type T.
    /// </summary>
    /// <typeparam name="T">The type of signal to subscribe to.</typeparam>
    /// <param name="handler">The async handler function.</param>
    /// <returns>A disposable subscription that can be disposed to unsubscribe.</returns>
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : ISignal;
}
