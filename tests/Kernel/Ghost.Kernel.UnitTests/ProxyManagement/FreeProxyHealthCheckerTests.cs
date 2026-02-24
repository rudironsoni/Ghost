using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.ProxyManagement;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.ProxyManagement;

public sealed class FreeProxyHealthCheckerTests : ReliabilityTestBase
{
    public FreeProxyHealthCheckerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task CheckHealthAsync_WithNullProxy_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = new FreeProxyHealthChecker(NullLogger<FreeProxyHealthChecker>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await checker.CheckHealthAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CheckHealthAsync_WithInvalidProxy_ReturnsUnhealthyResult()
    {
        // Arrange
        var checker = new FreeProxyHealthChecker(
            NullLogger<FreeProxyHealthChecker>.Instance,
            TimeSpan.FromSeconds(2),
            0.8);

        var proxy = new ProxyInfo("http://invalid-proxy-host:9999", null, null);

        // Act
        ProxyHealthCheckResult result = await checker.CheckHealthAsync(proxy, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Proxy.Should().Be(proxy);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ShouldRemoveProxy_WithLowSuccessRate_ReturnsTrue()
    {
        // Arrange
        var checker = new FreeProxyHealthChecker(
            NullLogger<FreeProxyHealthChecker>.Instance,
            TimeSpan.FromSeconds(5),
            0.8);

        // Act
        bool shouldRemove = checker.ShouldRemoveProxy(totalRequests: 100, successfulRequests: 70);

        // Assert
        shouldRemove.Should().BeTrue("success rate of 70% is below 80% threshold");
    }

    [Fact]
    public void ShouldRemoveProxy_WithHighSuccessRate_ReturnsFalse()
    {
        // Arrange
        var checker = new FreeProxyHealthChecker(
            NullLogger<FreeProxyHealthChecker>.Instance,
            TimeSpan.FromSeconds(5),
            0.8);

        // Act
        bool shouldRemove = checker.ShouldRemoveProxy(totalRequests: 100, successfulRequests: 85);

        // Assert
        shouldRemove.Should().BeFalse("success rate of 85% is above 80% threshold");
    }

    [Fact]
    public void CalculateSuccessRate_WithValidData_ReturnsCorrectRate()
    {
        // Act
        double successRate = FreeProxyHealthChecker.CalculateSuccessRate(totalRequests: 100, successfulRequests: 85);

        // Assert
        successRate.Should().Be(0.85);
    }

    [Fact]
    public void CalculateSuccessRate_WithZeroRequests_ReturnsZero()
    {
        // Act
        double successRate = FreeProxyHealthChecker.CalculateSuccessRate(totalRequests: 0, successfulRequests: 0);

        // Assert
        successRate.Should().Be(0.0);
    }
}
