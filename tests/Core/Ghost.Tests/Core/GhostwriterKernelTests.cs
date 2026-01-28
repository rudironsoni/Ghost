using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Patchright;
using Xunit;

namespace Ghost.Core.Tests;

public class GhostwriterKernelTests
{
    [Fact]
    public async Task NewSessionAsync_UsesOptions_ToCreateContext()
    {
        var browser = Substitute.For<IBrowser>();
        var context = Substitute.For<IBrowserContext>();

        browser.NewContextAsync(Arg.Any<BrowserNewContextOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(context));

        // create private instance via non-public ctor
        var ctor = typeof(GhostwriterKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IBrowser), typeof(int) }, null)!;
        var kernel = (GhostwriterKernel)ctor.Invoke(new object[] { browser, 10 });

        var session = await kernel.NewSessionAsync(new SessionOptions { ViewportWidth = 500, ViewportHeight = 600, UserAgent = "ua" });
        session.Should().NotBeNull();

        await kernel.DisposeAsync();

        await browser.Received().NewContextAsync(Arg.Is<BrowserNewContextOptions>(o => o.ViewportWidth == 500 && o.ViewportHeight == 600 && o.UserAgent == "ua"), Arg.Any<CancellationToken>());
        await browser.Received().DisposeAsync();
    }

    [Fact]
    public void Constructor_NullBrowser_ThrowsArgumentNullException()
    {
        var ctor = typeof(GhostwriterKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IBrowser), typeof(int) }, null)!;
        Action act = () => ctor.Invoke(new object?[] { null, 10 });
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentNullException>();
    }

    [Fact]
    public async Task NewSessionAsync_RespectsMaxConcurrentSessions()
    {
        var browser = Substitute.For<IBrowser>();
        var context = Substitute.For<IBrowserContext>();
        browser.NewContextAsync(Arg.Any<BrowserNewContextOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(context));

        // Create kernel with max 1 concurrent session
        var ctor = typeof(GhostwriterKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IBrowser), typeof(int) }, null)!;
        var kernel = (GhostwriterKernel)ctor.Invoke(new object[] { browser, 1 });

        // 1. Start first session (should succeed)
        var session1 = await kernel.NewSessionAsync();
        session1.Should().NotBeNull();

        // 2. Try start second session (should block/timeout because limit is 1)
        // We use a short timeout to verify it blocks
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await kernel.NewSessionAsync(ct: cts.Token));

        // 3. Dispose first session
        await session1.DisposeAsync();

        // 4. Start second session (should now succeed)
        var session2 = await kernel.NewSessionAsync();
        session2.Should().NotBeNull();
        
        await kernel.DisposeAsync();
    }
}
