using System.Reflection;
using Ghost.Kernel;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Hosting.Tests;

public sealed class GhostKernelHostedServiceTests
{
    private static GhostKernel CreateTestKernel(Mock<IPlaywright> playwright, Mock<IBrowser> browser)
    {
        ConstructorInfo ctor = typeof(GhostKernel).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic)[0];
        return (GhostKernel)ctor.Invoke(new object?[]
        {
            playwright.Object, browser.Object, 1, false, "Chromium", null
        });
    }

    [Fact]
    public async Task StartAsync_Completes_Successfully()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);

        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        var hostedService = new GhostKernelHostedService(kernel, mockLifetime.Object);

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - StartAsync should complete without error
        mockLifetime.Verify(l => l.ApplicationStopping, Times.Once);

        // Cleanup
        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_Disposes_Kernel_Async()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);
        mockBrowser.Setup(b => b.CloseAsync()).Returns(Task.CompletedTask);
        mockBrowser.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);

        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        var hostedService = new GhostKernelHostedService(kernel, mockLifetime.Object);

        // Act
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        mockBrowser.Verify(b => b.CloseAsync(), Times.Once);
        mockBrowser.Verify(b => b.DisposeAsync(), Times.Once);
        mockPlaywright.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public void Constructor_Throws_When_Kernel_Is_Null()
    {
        // Arrange
        var mockLifetime = new Mock<IHostApplicationLifetime>();

        // Act & Assert
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            new GhostKernelHostedService(null!, mockLifetime.Object));
        Assert.Equal("kernel", ex.ParamName);
    }

    [Fact]
    public async Task Constructor_Throws_When_Lifetime_Is_Null()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();

        // Act & Assert
        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        try
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
                new GhostKernelHostedService(kernel, null!));
            Assert.Equal("lifetime", ex.ParamName);
        }
        finally
        {
            await kernel.DisposeAsync();
        }
    }

    [Fact]
    public async Task Constructor_Registers_ApplicationStopping_Handler()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);

        // Act
        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        try
        {
            _ = new GhostKernelHostedService(kernel, mockLifetime.Object);

            // Assert
            mockLifetime.Verify(l => l.ApplicationStopping, Times.Once);
        }
        finally
        {
            await kernel.DisposeAsync();
        }
    }

    [Fact]
    public async Task StopAsync_Handles_Cancellation_Requested()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var lifetimeCts = new CancellationTokenSource();
        var stopCts = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(lifetimeCts.Token);
        mockBrowser.Setup(b => b.CloseAsync()).Returns(Task.CompletedTask);
        mockBrowser.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);

        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        var hostedService = new GhostKernelHostedService(kernel, mockLifetime.Object);

        stopCts.Cancel();

        // Act - should complete without throwing even if token is cancelled
        await hostedService.StopAsync(stopCts.Token);

        // Assert - disposal still happens even with cancelled token
        mockBrowser.Verify(b => b.CloseAsync(), Times.Once);
    }

    [Fact]
    public async Task Full_Lifecycle_Start_And_Stop_Works()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);
        mockBrowser.Setup(b => b.CloseAsync()).Returns(Task.CompletedTask);
        mockBrowser.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);

        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        var hostedService = new GhostKernelHostedService(kernel, mockLifetime.Object);

        // Act - Full lifecycle
        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        // Assert
        mockBrowser.Verify(b => b.CloseAsync(), Times.Once);
        mockBrowser.Verify(b => b.DisposeAsync(), Times.Once);
        mockPlaywright.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public async Task HostedService_Implements_IHostedService()
    {
        // Arrange
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();
        var cancellationTokenSource = new CancellationTokenSource();

        mockLifetime.Setup(l => l.ApplicationStopping).Returns(cancellationTokenSource.Token);

        // Act
        GhostKernel kernel = CreateTestKernel(mockPlaywright, mockBrowser);
        try
        {
            var hostedService = new GhostKernelHostedService(kernel, mockLifetime.Object);

            // Assert
            Assert.IsAssignableFrom<IHostedService>(hostedService);
        }
        finally
        {
            await kernel.DisposeAsync();
        }
    }
}
