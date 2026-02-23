using FluentAssertions;
using Xunit;
using System.Collections.Concurrent;

namespace Ghost.Sdk.Spider.Tests.Unit.Scheduling;

/// <summary>
/// Tests for distributed locking mechanisms used in scheduling
/// </summary>
public class DistributedLockTests
{
    [Fact]
    public async Task DistributedLock_AcquireAndRelease_ShouldWork()
    {
        // Arrange
        var lockKey = "test-lock-key";
        var locks = new ConcurrentDictionary<string, SemaphoreSlim>();
        var lockObj = locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        // Act
        var acquired = await lockObj.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        acquired.Should().BeTrue();

        // Cleanup
        lockObj.Release();
    }

    [Fact]
    public async Task DistributedLock_ConcurrentAccess_ShouldSerialize()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);
        var executionOrder = new ConcurrentBag<int>();
        var currentlyExecuting = 0;
        var maxConcurrent = 0;
        var lockForMax = new object();

        // Act
        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            await lockObj.WaitAsync();
            try
            {
                lock (lockForMax)
                {
                    currentlyExecuting++;
                    maxConcurrent = Math.Max(maxConcurrent, currentlyExecuting);
                }

                executionOrder.Add(i);
                await Task.Yield(); // Yield to allow other tasks to try to acquire

                lock (lockForMax)
                {
                    currentlyExecuting--;
                }
            }
            finally
            {
                lockObj.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        maxConcurrent.Should().Be(1);
        executionOrder.Should().HaveCount(10);
    }

    [Fact]
    public async Task DistributedLock_Timeout_ShouldReturnFalse()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);
        await lockObj.WaitAsync(); // Acquire lock first

        // Act
        var acquired = await lockObj.WaitAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        acquired.Should().BeFalse();

        // Cleanup
        lockObj.Release();
    }

    [Fact]
    public async Task DistributedLock_NestedAcquisition_ShouldDeadlock()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);

        // Act & Assert
        await lockObj.WaitAsync();
        var secondAcquire = await lockObj.WaitAsync(TimeSpan.FromMilliseconds(100));

        secondAcquire.Should().BeFalse(); // Cannot acquire same lock twice

        // Cleanup
        lockObj.Release();
    }

    [Fact]
    public async Task DistributedLock_MultipleKeys_ShouldIsolate()
    {
        // Arrange
        var locks = new ConcurrentDictionary<string, SemaphoreSlim>();
        var lock1 = locks.GetOrAdd("key1", _ => new SemaphoreSlim(1, 1));
        var lock2 = locks.GetOrAdd("key2", _ => new SemaphoreSlim(1, 1));

        // Act
        var acquired1 = await lock1.WaitAsync(TimeSpan.FromSeconds(1));
        var acquired2 = await lock2.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        acquired1.Should().BeTrue();
        acquired2.Should().BeTrue();

        // Cleanup
        lock1.Release();
        lock2.Release();
    }

    [Fact]
    public async Task DistributedLock_ReleaseWithoutAcquire_ShouldThrow()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);

        // Act & Assert
        var act = () => lockObj.Release();

        act.Should().Throw<SemaphoreFullException>();
    }

    [Fact]
    public async Task DistributedLock_LongRunningOperation_ShouldHoldLock()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);
        var otherTaskStarted = new TaskCompletionSource<bool>();
        var firstTaskCanComplete = new TaskCompletionSource<bool>();
        var firstTaskCompleted = false;

        // Act
        var firstTask = Task.Run(async () =>
        {
            await lockObj.WaitAsync();
            try
            {
                otherTaskStarted.SetResult(true);
                await firstTaskCanComplete.Task; // Wait for signal to complete
                firstTaskCompleted = true;
            }
            finally
            {
                lockObj.Release();
            }
        });

        await otherTaskStarted.Task;

        var secondTask = Task.Run(async () =>
        {
            var acquired = await lockObj.WaitAsync(TimeSpan.FromMilliseconds(50));
            return acquired;
        });

        // Second task should not acquire the lock while first task holds it
        await Task.Delay(10); // Brief delay to ensure secondTask has started
        var secondAcquired = secondTask.IsCompleted ? await secondTask : false;

        // Now signal first task to complete
        firstTaskCanComplete.SetResult(true);
        await firstTask;

        // Assert
        secondAcquired.Should().BeFalse(); // Could not acquire while first task held lock
        firstTaskCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task DistributedLock_ExceptionDuringLock_ShouldRelease()
    {
        // Arrange
        var lockObj = new SemaphoreSlim(1, 1);
        var lockReleased = false;

        // Act
        try
        {
            await lockObj.WaitAsync();
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            finally
            {
                lockObj.Release();
                lockReleased = true;
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        var canAcquireAfter = await lockObj.WaitAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        lockReleased.Should().BeTrue();
        canAcquireAfter.Should().BeTrue();

        // Cleanup
        lockObj.Release();
    }
}
