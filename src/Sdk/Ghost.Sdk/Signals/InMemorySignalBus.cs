using System.Threading.Channels;

namespace Ghost.Sdk.Signals;

/// <summary>
/// In-memory implementation of <see cref="ISignalBus"/> using channels for async signal delivery.
/// </summary>
public sealed class InMemorySignalBus : ISignalBus, IAsyncDisposable
{
    private readonly Dictionary<Type, List<SubscriptionInfo>> _subscriptions = new();
    private readonly Channel<SignalEnvelope> _channel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySignalBus"/> class.
    /// </summary>
    public InMemorySignalBus()
    {
        _channel = Channel.CreateUnbounded<SignalEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _processingTask = ProcessSignalsAsync(_cts.Token);
    }

    /// <inheritdoc/>
    public async Task EmitAsync<T>(T signal, CancellationToken ct = default) where T : ISignal
    {
        ArgumentNullException.ThrowIfNull(signal);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var envelope = new SignalEnvelope(typeof(T), signal, ct);
        await _channel.Writer.WriteAsync(envelope, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : ISignal
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Type signalType = typeof(T);
        var subscription = new Subscription<T>(this, signalType, handler);

        lock (_subscriptions)
        {
            if (!_subscriptions.TryGetValue(signalType, out List<SubscriptionInfo>? subscriptions))
            {
                subscriptions = [];
                _subscriptions[signalType] = subscriptions;
            }

            subscriptions.Add(new SubscriptionInfo(subscription.Id, async (signal, ct) => await handler((T)signal, ct).ConfigureAwait(false)));
        }

        return subscription;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal shutdown
        await _cts.CancelAsync().ConfigureAwait(false);
        _channel.Writer.Complete();

        // Wait for processing to complete
        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _cts.Dispose();
    }

    private void Unsubscribe(Type signalType, Guid subscriptionId)
    {
        lock (_subscriptions)
        {
            if (_subscriptions.TryGetValue(signalType, out List<SubscriptionInfo>? subscriptions))
            {
                subscriptions.RemoveAll(s => s.Id == subscriptionId);
                if (subscriptions.Count == 0)
                {
                    _subscriptions.Remove(signalType);
                }
            }
        }
    }

    private async Task ProcessSignalsAsync(CancellationToken ct)
    {
        await foreach (SignalEnvelope? envelope in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            List<SubscriptionInfo>? subscriptionsCopy;

            lock (_subscriptions)
            {
                if (!_subscriptions.TryGetValue(envelope.SignalType, out List<SubscriptionInfo>? subscriptions) || subscriptions.Count == 0)
                {
                    continue;
                }

                subscriptionsCopy = new List<SubscriptionInfo>(subscriptions);
            }

            // Process handlers in parallel
            IEnumerable<Task> tasks = subscriptionsCopy.Select(sub => InvokeHandlerAsync(sub, envelope));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private static async Task InvokeHandlerAsync(SubscriptionInfo subscription, SignalEnvelope envelope)
    {
        try
        {
            await subscription.Handler(envelope.Signal, envelope.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Handler was canceled, ignore
        }
        catch
        {
            // Swallow exceptions from handlers to prevent one handler from breaking others
            // In production, you might want to log these
        }
    }

    private sealed record SignalEnvelope(Type SignalType, ISignal Signal, CancellationToken CancellationToken);

    private sealed record SubscriptionInfo(Guid Id, Func<ISignal, CancellationToken, Task> Handler);

    private sealed class Subscription<T> : IDisposable where T : ISignal
    {
        private readonly InMemorySignalBus _bus;
        private readonly Type _signalType;
        private bool _disposed;

        public Subscription(InMemorySignalBus bus, Type signalType, Func<T, CancellationToken, Task> handler)
        {
            _bus = bus;
            _signalType = signalType;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _bus.Unsubscribe(_signalType, Id);
        }
    }
}
