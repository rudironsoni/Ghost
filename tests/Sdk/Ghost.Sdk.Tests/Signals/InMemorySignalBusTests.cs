using FluentAssertions;
using Ghost.Sdk.Signals;
using Xunit;

namespace Ghost.Sdk.Tests.Signals;

public sealed class InMemorySignalBusTests : IAsyncLifetime
{
    private InMemorySignalBus _signalBus = null!;

    public Task InitializeAsync()
    {
        _signalBus = new InMemorySignalBus();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _signalBus.DisposeAsync();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithValidSignal_DeliversToSubscriber()
    {
        // Arrange
        var receivedSignals = new List<SpiderStartedSignal>();
        var tcs = new TaskCompletionSource();

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            receivedSignals.Add(signal);
            tcs.SetResult();
            await Task.CompletedTask;
        });

        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act
        await _signalBus.EmitAsync(signal);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        receivedSignals.Should().ContainSingle();
        receivedSignals[0].SpiderId.Should().Be("spider-1");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithMultipleSubscribers_DeliversToAll()
    {
        // Arrange
        var received1 = new List<SpiderStartedSignal>();
        var received2 = new List<SpiderStartedSignal>();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            received1.Add(signal);
            tcs1.SetResult();
            await Task.CompletedTask;
        });

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            received2.Add(signal);
            tcs2.SetResult();
            await Task.CompletedTask;
        });

        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act
        await _signalBus.EmitAsync(signal);
        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        received1.Should().ContainSingle();
        received2.Should().ContainSingle();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Subscribe_WhenDisposed_UnsubscribesHandler()
    {
        // Arrange
        var receivedSignals = new List<SpiderStartedSignal>();
        var subscription = _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            receivedSignals.Add(signal);
            await Task.CompletedTask;
        });

        var signal1 = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act - emit before unsubscribe
        await _signalBus.EmitAsync(signal1);
        await Task.Delay(100); // Give time for processing

        subscription.Dispose();

        var signal2 = new SpiderStartedSignal("spider-2", DateTimeOffset.UtcNow);
        await _signalBus.EmitAsync(signal2);
        await Task.Delay(100); // Give time for processing

        // Assert - should only receive first signal
        receivedSignals.Should().ContainSingle();
        receivedSignals[0].SpiderId.Should().Be("spider-1");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithDifferentSignalTypes_DeliversToCorrectSubscribers()
    {
        // Arrange
        var startedSignals = new List<SpiderStartedSignal>();
        var closedSignals = new List<SpiderClosedSignal>();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            startedSignals.Add(signal);
            tcs1.SetResult();
            await Task.CompletedTask;
        });

        _signalBus.Subscribe<SpiderClosedSignal>(async (signal, ct) =>
        {
            closedSignals.Add(signal);
            tcs2.SetResult();
            await Task.CompletedTask;
        });

        var startedSignal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);
        var closedSignal = new SpiderClosedSignal("spider-1", DateTimeOffset.UtcNow, "test");

        // Act
        await _signalBus.EmitAsync(startedSignal);
        await _signalBus.EmitAsync(closedSignal);
        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        startedSignals.Should().ContainSingle();
        closedSignals.Should().ContainSingle();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        CancellationToken? receivedToken = null;
        var tcs = new TaskCompletionSource();

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            receivedToken = ct;
            tcs.SetResult();
            await Task.CompletedTask;
        });

        var cts = new CancellationTokenSource();
        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act
        await _signalBus.EmitAsync(signal, cts.Token);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        receivedToken.Should().NotBeNull();
        receivedToken.Value.Should().Be(cts.Token);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithNullSignal_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _signalBus.EmitAsync<SpiderStartedSignal>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Subscribe_WithNullHandler_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _signalBus.Subscribe<SpiderStartedSignal>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_WithHandlerException_ContinuesProcessing()
    {
        // Arrange
        var received1 = new List<SpiderStartedSignal>();
        var received2 = new List<SpiderStartedSignal>();
        var tcs = new TaskCompletionSource();

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            throw new InvalidOperationException("Handler 1 failed");
        });

        _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) =>
        {
            received2.Add(signal);
            tcs.SetResult();
            await Task.CompletedTask;
        });

        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act
        await _signalBus.EmitAsync(signal);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert - second handler should still receive the signal
        received2.Should().ContainSingle();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task DisposeAsync_CompletesGracefully()
    {
        // Arrange
        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);
        await _signalBus.EmitAsync(signal);

        // Act
        var act = async () => await _signalBus.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task EmitAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        await _signalBus.DisposeAsync();
        var signal = new SpiderStartedSignal("spider-1", DateTimeOffset.UtcNow);

        // Act
        var act = async () => await _signalBus.EmitAsync(signal);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task Subscribe_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        await _signalBus.DisposeAsync();

        // Act
        var act = () => _signalBus.Subscribe<SpiderStartedSignal>(async (signal, ct) => await Task.CompletedTask);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
