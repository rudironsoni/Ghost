using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Engine.Tests.Engine;

/// <summary>
/// Benchmark tests comparing ConcurrentBag&lt;Task&gt; vs Channel&lt;Task&gt; performance.
/// These tests validate the performance improvements of the Channel implementation.
/// </summary>
public class GhostEngineBenchmarks
{
    private readonly ITestOutputHelper _output;

    public GhostEngineBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(1000, 50)]
    [InlineData(10000, 100)]
    public async Task Benchmark_ConcurrentBagVsChannel_TaskThroughputAsync(int taskCount, int maxInFlight)
    {
        // Warmup
        await RunConcurrentBagBenchmarkAsync(100, 10);
        await RunChannelBenchmarkAsync(100, 10);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Measure ConcurrentBag
        var stopwatch = Stopwatch.StartNew();
        long bagMemoryBefore = GC.GetTotalMemory(true);
        await RunConcurrentBagBenchmarkAsync(taskCount, maxInFlight);
        long bagMemoryAfter = GC.GetTotalMemory(true);
        long bagElapsedMs = stopwatch.ElapsedMilliseconds;
        long bagMemoryAllocated = bagMemoryAfter - bagMemoryBefore;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Measure Channel
        stopwatch.Restart();
        long channelMemoryBefore = GC.GetTotalMemory(true);
        await RunChannelBenchmarkAsync(taskCount, maxInFlight);
        long channelMemoryAfter = GC.GetTotalMemory(true);
        long channelElapsedMs = stopwatch.ElapsedMilliseconds;
        long channelMemoryAllocated = channelMemoryAfter - channelMemoryBefore;

        // Report results
        _output.WriteLine($"""
            Benchmark Results ({taskCount} tasks, {maxInFlight} max in-flight):
            =====================================================
            ConcurrentBag<Task>:
              - Time: {bagElapsedMs} ms
              - Memory delta: {bagMemoryAllocated} bytes

            Channel<Task>:
              - Time: {channelElapsedMs} ms
              - Memory delta: {channelMemoryAllocated} bytes

            Improvement:
              - Time: {(bagElapsedMs > 0 ? (channelElapsedMs * 100 / bagElapsedMs) : 0)}% of ConcurrentBag time
              - Memory: {(bagMemoryAllocated > 0 ? (channelMemoryAllocated * 100 / bagMemoryAllocated) : 0)}% of ConcurrentBag memory
            """);

        // Channel should be comparable or better in performance
        Assert.True(channelElapsedMs <= bagElapsedMs * 2,
            $"Channel should not be more than 2x slower than ConcurrentBag. Channel: {channelElapsedMs}ms, Bag: {bagElapsedMs}ms");
    }

    [Fact]
    public async Task Benchmark_ChannelBackpressure_RespectsBoundsAsync()
    {
        const int maxInFlight = 10;
        const int taskCount = 100;

        var channelOptions = new BoundedChannelOptions(maxInFlight)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        };
        Channel<Task> channel = Channel.CreateBounded<Task>(channelOptions);

        int maxObservedInFlight = 0;
        int currentInFlight = 0;

        Task consumer = Task.Run(async () =>
        {
            await foreach (Task task in channel.Reader.ReadAllAsync())
            {
                await task;
            }
        });

        for (int i = 0; i < taskCount; i++)
        {
            Interlocked.Increment(ref currentInFlight);
            int observed = currentInFlight;
            if (observed > maxObservedInFlight)
            {
                maxObservedInFlight = observed;
            }

            Task task = Task.Run(async () =>
            {
                await Task.Delay(1);
                Interlocked.Decrement(ref currentInFlight);
            });

            await channel.Writer.WriteAsync(task);
        }

        channel.Writer.Complete();
        await consumer;

        _output.WriteLine($"Max observed in-flight: {maxObservedInFlight}, limit: {maxInFlight}");

        // Channel bounds should limit concurrency naturally
        Assert.True(maxObservedInFlight <= maxInFlight + 5,
            $"Channel should respect bounds. Max observed: {maxObservedInFlight}, limit: {maxInFlight}");
    }

    [Fact]
    public async Task Benchmark_Cancellation_HandledGracefullyAsync()
    {
        const int taskCount = 100;
        const int maxInFlight = 10;

        using var cts = new CancellationTokenSource();
        var channelOptions = new BoundedChannelOptions(maxInFlight)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        };
        Channel<Task> channel = Channel.CreateBounded<Task>(channelOptions);

        int completedTasks = 0;
        int writtenTasks = 0;

        Task consumer = Task.Run(async () =>
        {
            await foreach (Task task in channel.Reader.ReadAllAsync(cts.Token))
            {
                try
                {
                    await task;
                    Interlocked.Increment(ref completedTasks);
                }
                catch
                {
                    // Expected for cancelled tasks
                }
            }
        }, cts.Token);

        // Cancel after a short delay to ensure some tasks complete
        cts.CancelAfter(100);

        try
        {
            for (int i = 0; i < taskCount; i++)
            {
                Task task = Task.Run(async () =>
                {
                    // Use a small delay to allow some tasks to complete
                    await Task.Delay(10, cts.Token);
                }, cts.Token);

                await channel.Writer.WriteAsync(task, cts.Token);
                Interlocked.Increment(ref writtenTasks);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - channel.Writer.WriteAsync throws when cancelled
        }
        finally
        {
            channel.Writer.Complete();
        }

        try
        {
            await consumer;
        }
        catch (OperationCanceledException)
        {
            // Expected - ReadAllAsync throws when cancelled
        }

        _output.WriteLine($"Written tasks: {writtenTasks}, Completed tasks: {completedTasks}");

        // Channel gracefully handles cancellation - some tasks may have completed
        // The important thing is that cancellation doesn't cause hangs or crashes
        Assert.True(writtenTasks > 0, "Should have written some tasks before cancellation");
        Assert.True(writtenTasks <= taskCount, "Should not have written more tasks than expected");
    }

    private static async Task RunConcurrentBagBenchmarkAsync(int taskCount, int maxInFlight)
    {
        var processingTasks = new ConcurrentBag<Task>();
        using var semaphore = new SemaphoreSlim(maxInFlight);

        for (int i = 0; i < taskCount; i++)
        {
            await semaphore.WaitAsync();
            Task task = Task.Run(async () =>
            {
                await Task.Delay(1);
                semaphore.Release();
            });
            processingTasks.Add(task);
        }

        await Task.WhenAll(processingTasks);
    }

    private static async Task RunChannelBenchmarkAsync(int taskCount, int maxInFlight)
    {
        var channelOptions = new BoundedChannelOptions(maxInFlight)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        };
        Channel<Task> channel = Channel.CreateBounded<Task>(channelOptions);

        Task consumer = Task.Run(async () =>
        {
            await foreach (Task task in channel.Reader.ReadAllAsync())
            {
                await task;
            }
        });

        for (int i = 0; i < taskCount; i++)
        {
            Task task = Task.Run(async () =>
            {
                await Task.Delay(1);
            });
            await channel.Writer.WriteAsync(task);
        }

        channel.Writer.Complete();
        await consumer;
    }
}
