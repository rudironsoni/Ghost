using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Core.Tests;

public class GhostKernelTests
{
    private static GhostKernel CreateKernel(IPlaywright playwright, IBrowser browser, bool useStealth = false)
    {
        // Get the private constructor (there's only one)
        ConstructorInfo ctor = typeof(GhostKernel).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)[0];
        return (GhostKernel)ctor.Invoke(new object?[] { playwright, browser, 1, useStealth, "Chromium", null });
    }
    [Fact]
    public async Task NewSessionAsyncUsesOptionsToCreateContext()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockBrowser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        // create private instance via non-public ctor
        GhostKernel kernel = CreateKernel(mockPlaywright.Object, mockBrowser.Object); // Disable stealth for this test

        IBrowserSession session = await kernel.NewSessionAsync(new SessionOptions { ViewportWidth = 500, ViewportHeight = 600, UserAgent = "ua" });
        session.Should().NotBeNull();

        await kernel.DisposeAsync();

        mockBrowser.Verify(b => b.NewContextAsync(It.Is<BrowserNewContextOptions>(o => o.ViewportSize!.Width == 500 && o.ViewportSize.Height == 600 && o.UserAgent == "ua")), Times.Once);
        mockBrowser.Verify(b => b.DisposeAsync(), Times.Once);
        mockPlaywright.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public void ConstructorNullBrowserThrowsArgumentNullException()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        ConstructorInfo ctor = typeof(GhostKernel).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
        Action act = () => ctor.Invoke(new object?[] { mockPlaywright.Object, null, 10, true, "Chromium", null });
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentNullException>();
    }

    [Fact]
    public async Task NewSessionAsyncRespectsMaxConcurrentSessions()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();
        mockBrowser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        // Create kernel with max 1 concurrent session
        GhostKernel kernel = CreateKernel(mockPlaywright.Object, mockBrowser.Object);

        // 1. Start first session (should succeed)
        IBrowserSession session1 = await kernel.NewSessionAsync();
        session1.Should().NotBeNull();

        // 2. Try start second session (should block/timeout because limit is 1)
        // We use a short timeout to verify it blocks
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await kernel.NewSessionAsync(ct: cts.Token).ConfigureAwait(false));

        // 3. Dispose first session
        await session1.DisposeAsync();

        // 4. Start second session (should now succeed)
        IBrowserSession session2 = await kernel.NewSessionAsync();
        session2.Should().NotBeNull();

        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task NewSessionAsyncEnablesStealthInjectsScript()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();
        mockBrowser.Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        // Create kernel with stealth enabled
        GhostKernel kernel = CreateKernel(mockPlaywright.Object, mockBrowser.Object, useStealth: true);

        IBrowserSession session = await kernel.NewSessionAsync();
        session.Should().NotBeNull();

        // Verify script injection
        // AddInitScriptAsync usually takes just a string in the simple overload we used
        mockContext.Verify(c => c.AddInitScriptAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }
}
