using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Worker.Tests;

/// <summary>
/// Test probe to track scoped service disposal timing.
/// </summary>
public sealed class ScopedProbe : IDisposable, IAsyncDisposable
{
    private readonly TaskCompletionSource _disposeSignal;
    private int _disposeStarted;
    private int _disposeCompleted;

    public ScopedProbe()
    {
        _disposeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Gets a value indicating whether disposal has started.
    /// </summary>
    public bool DisposeStarted => Interlocked.CompareExchange(ref _disposeStarted, 0, 0) == 1;

    /// <summary>
    /// Gets a value indicating whether disposal has completed.
    /// </summary>
    public bool DisposeCompleted => Interlocked.CompareExchange(ref _disposeCompleted, 0, 0) == 1;

    /// <summary>
    /// Gets a task that completes when disposal starts.
    /// </summary>
    public Task DisposeStartedTask => _disposeSignal.Task;

    /// <summary>
    /// Throws if disposal has started.
    /// </summary>
    public void EnsureNotDisposed()
    {
        if (DisposeStarted)
        {
            throw new InvalidOperationException("Scope has been disposed.");
        }
    }

    /// <summary>
    /// Gets the current disposal state for verification.
    /// </summary>
    public string GetState()
    {
        return $"Started: {DisposeStarted}, Completed: {DisposeCompleted}";
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposeStarted, 1);
        _disposeSignal.TrySetResult();
        Interlocked.Exchange(ref _disposeCompleted, 1);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
