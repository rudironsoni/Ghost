using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using NSubstitute;
using Xunit;

namespace Ghost.Hosting.Tests;

public class GhostKernelHostedServiceTests
{
    private static GhostKernel CreateKernel(IPlaywright playwright, IBrowser browser)
    {
        var ctor = typeof(GhostKernel).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(IPlaywright), typeof(IBrowser), typeof(int), typeof(bool), typeof(string) }, null)!;
        return (GhostKernel)ctor.Invoke(new object[] { playwright, browser, 1, false, "Chromium" });
    }

    [Fact]
    public async Task StopAsyncCallsDisposeAsyncOnKernel()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();

        var kernel = CreateKernel(playwright, browser);

        var lifetime = Substitute.For<IHostApplicationLifetime>();

        var service = new GhostKernelHostedService(kernel, lifetime);

        await service.StopAsync(CancellationToken.None);

        // kernel.DisposeAsync will close browser and dispose playwright; verify browser closed/disposed
        await browser.Received().CloseAsync();
        await browser.Received().DisposeAsync();
        playwright.Received().Dispose();
    }

    [Fact]
    public async Task ApplicationStoppingCancellationTriggersKernelDisposeAsync()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        var kernel = CreateKernel(playwright, browser);

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var cts = new CancellationTokenSource();
        lifetime.ApplicationStopping.Returns(cts.Token);

        var service = new GhostKernelHostedService(kernel, lifetime);

        // Cancel the token to simulate application stopping
        cts.Cancel();

        // Give a small delay for any registered callbacks to run
        await Task.Delay(10);

        // If the hosted service registered for ApplicationStopping and invoked DisposeAsync,
        // the kernel's DisposeAsync will result in browser/playwright cleanup. Verify those were called.
        // NOTE: The callback logic in HostedService might be implemented synchronously (via Dispose) or async.
        // The current implementation in GhostKernelHostedService registers OnStopping which calls Dispose() synchronously.
        // GhostKernel.Dispose() calls DisposeAsync().GetAwaiter().GetResult().
        // So this should work.

        await browser.Received().CloseAsync();
        await browser.Received().DisposeAsync();
        playwright.Received().Dispose();
    }
}
