using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests.Internal;

public class LinkedInSessionPoolTests
{
    [Fact]
    public void ConstructorRejectsInvalidMaxSize()
    {
        var kernel = CreateKernelSubstitute();
        var options = new LinkedInSessionPoolOptions { MaxSize = 0, WarmCount = 0 };

        var act = () => new LinkedInSessionPool(kernel, options, NullLogger<LinkedInSessionPool>.Instance);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AcquireAsyncReturnsNewSessionWhenPoolEmpty()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s1");
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var options = new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 };
        var pool = new LinkedInSessionPool(kernel, options, NullLogger<LinkedInSessionPool>.Instance);

        var acquired = await pool.AcquireAsync(CancellationToken.None);

        acquired.Should().BeSameAs(session);
        pool.GetMetrics().TotalCreated.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseSkipsReuseWhenPoolAtCapacity()
    {
        var kernel = CreateKernelSubstitute();
        var session1 = CreateSession("cap-1");
        var session2 = CreateSession("cap-2");
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session1), Task.FromResult(session2));

        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);
        var first = await pool.AcquireAsync(CancellationToken.None);
        var second = await pool.AcquireAsync(CancellationToken.None);

        pool.Release(first);
        pool.Release(second);

        var metrics = pool.GetMetrics();
        metrics.AvailableCount.Should().Be(1);
        await session2.Received().DisposeAsync();
    }

    [Fact]
    public async Task ReleaseRecyclesSessionIntoPool()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s2");
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

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
        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

        pool.Release(session);

        await session.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task PruneAsyncRemovesExpiredIdleSessions()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s5");
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var options = new LinkedInSessionPoolOptions
        {
            MaxSize = 1,
            WarmCount = 0,
            MaxIdleTime = TimeSpan.FromMilliseconds(1),
            MaxLifetime = TimeSpan.FromMinutes(5)
        };

        var pool = new LinkedInSessionPool(kernel, options, NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(acquired);

        await Task.Delay(10);
        await pool.PruneAsync(CancellationToken.None);

        await session.Received().DisposeAsync();
        pool.GetMetrics().AvailableCount.Should().Be(0);
    }

    [Fact]
    public async Task PruneAsyncMarksInUseExpiredSessionsForRecycle()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s6");
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var options = new LinkedInSessionPoolOptions
        {
            MaxSize = 1,
            WarmCount = 0,
            MaxLifetime = TimeSpan.FromMilliseconds(1),
            MaxIdleTime = TimeSpan.FromMinutes(5)
        };

        var pool = new LinkedInSessionPool(kernel, options, NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);

        await Task.Delay(10);
        await pool.PruneAsync(CancellationToken.None);
        pool.Release(acquired);

        await session.Received().DisposeAsync();
    }

    [Fact]
    public async Task ReleaseDisposesDisconnectedSessions()
    {
        var kernel = CreateKernelSubstitute();
        var session = CreateSession("s7", isConnected: false);
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);
        var acquired = await pool.AcquireAsync(CancellationToken.None);
        pool.Release(acquired);

        await session.Received().DisposeAsync();
    }

    [Fact]
    public async Task WarmupAsyncCreatesUpToRemainingCapacity()
    {
        var kernel = CreateKernelSubstitute();
        var sessions = new Queue<IBrowserSession>(new[]
        {
            CreateSession("w1"),
            CreateSession("w2"),
            CreateSession("w3")
        });
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(sessions.Dequeue()));

        var options = new LinkedInSessionPoolOptions { MaxSize = 2, WarmCount = 0 };
        var pool = new LinkedInSessionPool(kernel, options, NullLogger<LinkedInSessionPool>.Instance);

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
        kernel.NewSessionAsync(Arg.Any<SessionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        var pool = new LinkedInSessionPool(kernel, new LinkedInSessionPoolOptions { MaxSize = 1, WarmCount = 0 }, NullLogger<LinkedInSessionPool>.Instance);

        await pool.AcquireAsync(CancellationToken.None);
        var metrics = pool.GetMetrics();

        metrics.TotalCreated.Should().Be(1);
        metrics.AverageAcquisitionTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    private static IBrowserSession CreateSession(string id, bool isConnected = true)
    {
        var session = Substitute.For<IBrowserSession>();
        session.SessionId.Returns(id);
        session.IsConnected.Returns(isConnected);
#pragma warning disable CA2012
        session.DisposeAsync().Returns(_ => new ValueTask(Task.CompletedTask));
#pragma warning restore CA2012
        return session;
    }

    private static IGhostKernel CreateKernelSubstitute()
    {
        return Substitute.For<IGhostKernel>();
    }
}
