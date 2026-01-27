using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Patchright;
using Xunit;

namespace Ghostwright.Core.Tests;

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
        var ctor = typeof(GhostwriterKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IBrowser) }, null)!;
        var kernel = (GhostwriterKernel)ctor.Invoke(new object[] { browser });

        var session = await kernel.NewSessionAsync(new SessionOptions { ViewportWidth = 500, ViewportHeight = 600, UserAgent = "ua" });
        session.Should().NotBeNull();

        await kernel.DisposeAsync();

        await browser.Received().NewContextAsync(Arg.Is<BrowserNewContextOptions>(o => o.ViewportWidth == 500 && o.ViewportHeight == 600 && o.UserAgent == "ua"), Arg.Any<CancellationToken>());
        await browser.Received().DisposeAsync();
    }

    [Fact]
    public void Constructor_NullBrowser_ThrowsArgumentNullException()
    {
        var ctor = typeof(GhostwriterKernel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IBrowser) }, null)!;
        Action act = () => ctor.Invoke(new object?[] { null });
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentNullException>();
    }
}
