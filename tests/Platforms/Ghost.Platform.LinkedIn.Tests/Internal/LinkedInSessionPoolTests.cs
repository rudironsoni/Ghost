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

public class LinkedInSessionPoolTests
{
    [Fact]
    public void ConstructorRejectsInvalidMaxSize()
    {
        var kernel = CreateKernelSubstitute();
        var options = new LinkedInSessionPoolOptions { MaxSize = 0, WarmCount = 0 };

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

        var options = new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 };
        var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);

        var acquired = await pool.AcquireAsync(CancellationToken.None);

        acquired.Should().BeSameAs(session.Object);
        pool.GetMetrics().TotalCreated.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseSkipsReuseWhenPoolAtCapacity()
    {
        var kernel = CreateKernelSubstitute();
        var session1 = CreateSession("cap-1");
        var session2 = CreateSession("cap-2");
        kernel.SetupSequence(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session1.Object)
            .ReturnsAsync(session2.Object);

        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);
        var first = await pool.AcquireAsync(CancellationToken.None);
        var second = await pool.AcquireAsync(CancellationToken.None);

        pool.Release(first);
        pool.Release(second);

        var metrics = pool.GetMetrics();
        metrics.AvailableCount.Should().Be(1);
        session2.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ReleaseRecyclesSessionIntoPool()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s2");
        kernel.Setup(k => k.NewSessionAsync(It.IsAny<SessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session.Object);

        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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

        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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
        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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

        var options = new LinkedInSessionPoolOptions
        {
            MaxSize = 1,
            WarmCount = 0,
            MaxIdleTime = TimeSpan.FromMilliseconds(1),
            MaxLifetime = TimeSpan.FromMinutes(5)
        };

        var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);
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

        var options = new LinkedInSessionPoolOptions
        {
            MaxSize = 1,
            WarmCount = 0,
            MaxLifetime = TimeSpan.FromMilliseconds(1),
            MaxIdleTime = TimeSpan.FromMinutes(5)
        };

        var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);
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

        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);
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

        var options = new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 };
        var pool = new LinkedInSessionPool(kernel.Object, options, NullLogger<LinkedInSessionPool>.Instance);

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

        var pool = new LinkedInSessionPool(kernel.Object, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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
}
