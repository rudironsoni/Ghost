using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NSubstitute;
using Xunit;

namespace Ghost.Core.Tests;

public class GhostKernelTests
{
    [Fact]
    public async Task NewSessionAsync_UsesOptions_ToCreateContext()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        var context = Substitute.For<IBrowserContext>();

        browser.NewContextAsync(Arg.Any<BrowserNewContextOptions>())
            .Returns(Task.FromResult(context));

        // create private instance via non-public ctor
        var ctor = typeof(GhostKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IPlaywright), typeof(IBrowser), typeof(int), typeof(bool) }, null)!;
        var kernel = (GhostKernel)ctor.Invoke(new object[] { playwright, browser, 10, false }); // Disable stealth for this test

        var session = await kernel.NewSessionAsync(new SessionOptions { ViewportWidth = 500, ViewportHeight = 600, UserAgent = "ua" });
        session.Should().NotBeNull();

        await kernel.DisposeAsync();

        await browser.Received().NewContextAsync(Arg.Is<BrowserNewContextOptions>(o => o.ViewportSize!.Width == 500 && o.ViewportSize.Height == 600 && o.UserAgent == "ua"));
        await browser.Received().DisposeAsync();
        playwright.Received().Dispose();
    }

    [Fact]
    public void Constructor_NullBrowser_ThrowsArgumentNullException()
    {
        var playwright = Substitute.For<IPlaywright>();
        var ctor = typeof(GhostKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IPlaywright), typeof(IBrowser), typeof(int), typeof(bool) }, null)!;
        Action act = () => ctor.Invoke(new object?[] { playwright, null, 10, true });
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentNullException>();
    }

    [Fact]
    public async Task NewSessionAsync_RespectsMaxConcurrentSessions()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        var context = Substitute.For<IBrowserContext>();
        browser.NewContextAsync(Arg.Any<BrowserNewContextOptions>())
            .Returns(Task.FromResult(context));

        // Create kernel with max 1 concurrent session
        var ctor = typeof(GhostKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IPlaywright), typeof(IBrowser), typeof(int), typeof(bool) }, null)!;
        var kernel = (GhostKernel)ctor.Invoke(new object[] { playwright, browser, 1, false });

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

    [Fact]
    public async Task NewSessionAsync_EnablesStealth_InjectsScript()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        var context = Substitute.For<IBrowserContext>();
        browser.NewContextAsync(Arg.Any<BrowserNewContextOptions>())
            .Returns(Task.FromResult(context));

        // Create kernel with stealth enabled
        var ctor = typeof(GhostKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IPlaywright), typeof(IBrowser), typeof(int), typeof(bool) }, null)!;
        var kernel = (GhostKernel)ctor.Invoke(new object[] { playwright, browser, 10, true });

        var session = await kernel.NewSessionAsync();
        session.Should().NotBeNull();

        // Verify script injection
        // AddInitScriptAsync usually takes just a string in the simple overload we used
        await context.Received(1).AddInitScriptAsync(Arg.Any<string>(), Arg.Any<string?>());
    }
}
