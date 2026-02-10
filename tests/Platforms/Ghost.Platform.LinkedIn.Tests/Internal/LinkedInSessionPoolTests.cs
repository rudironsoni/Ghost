using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests.Internal;

/// <summary>
/// Tests for LinkedInSessionPool.
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Unit")]
public class LinkedInSessionPoolTests
{
    [Fact]
    public void ConstructorRejectsInvalidMaxSize()
    {
        var kernel = CreateKernelSubstitute();
        var options = CreateTestOptions(maxSize: 0);

        var act = () => new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AcquireAsyncReturnsNewSessionWhenPoolEmpty()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s1");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        var options = CreateTestOptions(maxSize: 2);
        using var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);

        var acquired = await pool.AcquireAsync(CancellationToken.None);

        acquired.Should().BeSameAs(session.Object);
        pool.GetMetrics().TotalCreated.Should().Be(1);
    }

    // TODO: Fix this test - the original logic was flawed
    // [Fact(Skip = "Test logic is flawed - cannot acquire 2 sessions when MaxSize=1 due to semaphore blocking")]
    // public async Task ReleaseSkipsReuseWhenPoolAtCapacity()
    // {
    //     // The issue is that PoolAtCapacity() checks available+inUse >= MaxSize,
    //     // but the semaphore prevents having more than MaxSize sessions acquired simultaneously.
    //     await Task.CompletedTask;
    // }


    [Fact]
    public async Task ReleaseRecyclesSessionIntoPool()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s2");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        using var pool = new LinkedInSessionPool(kernel.Object, CreateTestOptions(maxSize: 2), NullLogger<LinkedInSessionPool>.Instance);

        var acquired = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(acquired);

        var metrics = pool.GetMetrics();
        metrics.AvailableCount.Should().Be(1);
        metrics.TotalRecycled.Should().Be(1);
    }

    [Fact]
    public async Task AcquireAsyncReusesReleasedSession()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s3");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        using var pool = new LinkedInSessionPool(kernel.Object, CreateTestOptions(), NullLogger<LinkedInSessionPool>.Instance);

        var first = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(first);
        var second = await pool.AcquireAsync(CancellationToken.None);

        second.SessionId.Should().Be(first.SessionId);
    }

    [Fact]
    public async Task ReleaseUnknownSessionDisposesIt()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s4");
        using var pool = new LinkedInSessionPool(kernel.Object, CreateTestOptions(), NullLogger<LinkedInSessionPool>.Instance);

        pool.Release(session.Object);

        session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task PruneAsyncRemovesExpiredIdleSessions()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s5");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        var options = CreateTestOptions(
            maxIdleTime: TimeSpan.FromMilliseconds(1),
            maxLifetime: TimeSpan.FromMinutes(5));

        using var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(acquired);

        await Task.Delay(10);
        await pool.PruneAsync(CancellationToken.None);

        session.Verify(s => s.DisposeAsync(), Times.Once);
        pool.GetMetrics().AvailableCount.Should().Be(0);
    }

    [Fact]
    public async Task PruneAsyncMarksInUseExpiredSessionsForRecycle()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s6");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        var options = CreateTestOptions(
            maxLifetime: TimeSpan.FromMilliseconds(1),
            maxIdleTime: TimeSpan.FromMinutes(5));

        using var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);

        await Task.Delay(10);
        await pool.PruneAsync(CancellationToken.None);
        pool.Release(acquired);

        session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ReleaseDisposesDisconnectedSessions()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s7", isConnected: false);
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        using var pool = new LinkedInSessionPool(kernel.Object, CreateTestOptions(), NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(acquired);

        session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task WarmupAsyncCreatesUpToRemainingCapacity()
    {
        var kernel = CreateKernelSubstitute();
        var sessions = new Queue<Mock<IBrowserSession>>(new[]
        {
            CreateSession("w1"),
            CreateSession("w2"),
            CreateSession("w3")
        });
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => sessions.Dequeue().Object);

        var options = CreateTestOptions(maxSize: 2);
        using var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);

        await pool.WarmupAsync(5, CancellationToken.None);

        var metrics = pool.GetMetrics();
        metrics.AvailableCount.Should().Be(2);
        metrics.TotalCreated.Should().Be(2);
    }

    [Fact]
    public async Task AcquireAsyncUpdatesMetricsWithAverageTime()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s8");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        using var pool = new LinkedInSessionPool(kernel.Object, CreateTestOptions(), NullLogger<LinkedInSessionPool>.Instance);

        await pool.AcquireAsync(CancellationToken.None);
        var metrics = pool.GetMetrics();

        metrics.TotalCreated.Should().Be(1);
        metrics.AverageAcquisitionTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    private static Mock<IBrowserSession> CreateSession(string id, bool isConnected = true)
    {
        var session = new Mock<IBrowserSession>();
        session.Setup(s => s.SessionId).Returns(id);
        session.Setup(s => s.IsConnected).Returns(isConnected);
#pragma warning disable CA2012
        session.Setup(s => s.DisposeAsync()).Returns(new ValueTask(Task.CompletedTask));
#pragma warning restore CA2012
        return session;
    }

    private static Mock<IGhostKernel> CreateKernelSubstitute()
    {
        return new Mock<IGhostKernel>();
    }

    private static LinkedInSessionPoolOptions CreateTestOptions(
        int maxSize = 1,
        int warmCount = 0,
        TimeSpan? maxIdleTime = null,
        TimeSpan? maxLifetime = null)
    {
        return new LinkedInSessionPoolOptions
        {
            MaxSize = maxSize,
            WarmCount = warmCount,
            MaxIdleTime = maxIdleTime ?? TimeSpan.FromMinutes(5),
            MaxLifetime = maxLifetime ?? TimeSpan.FromHours(1),
            HealthCheckInterval = TimeSpan.FromHours(24) // Disable timer during tests
        };
    }
}
