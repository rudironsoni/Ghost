using System.Net;
using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class ProxyManagerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        // Act
        var act = () => new ProxyManager(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        // Arrange
        var options = new ProxyOptions();

        // Act
        var act = () => new ProxyManager(options);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetNextProxyAsync_WhenNoProxies_ShouldReturnNull()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        var proxy = await manager.GetNextProxyAsync();

        // Assert
        proxy.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetNextProxyAsync_WithSingleProxy_ShouldReturnProxy()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy1.example.com", 8080);

        // Act
        var proxy = await manager.GetNextProxyAsync();

        // Assert
        proxy.Should().NotBeNull();
        proxy!.Address!.Host.Should().Be("proxy1.example.com");
        proxy.Address.Port.Should().Be(8080);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetNextProxyAsync_WithMultipleProxies_ShouldRotateRoundRobin()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy1.example.com", 8080);
        manager.AddProxy("proxy2.example.com", 8080);
        manager.AddProxy("proxy3.example.com", 8080);

        // Act
        var proxy1 = await manager.GetNextProxyAsync();
        var proxy2 = await manager.GetNextProxyAsync();
        var proxy3 = await manager.GetNextProxyAsync();
        var proxy4 = await manager.GetNextProxyAsync();

        // Assert
        var hosts = new[] { proxy1!.Address!.Host, proxy2!.Address!.Host, proxy3!.Address!.Host };
        hosts.Should().Contain("proxy1.example.com");
        hosts.Should().Contain("proxy2.example.com");
        hosts.Should().Contain("proxy3.example.com");
        
        // Fourth call should wrap around
        proxy4!.Address!.Host.Should().Be(proxy1!.Address!.Host);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddProxy_WithNullHost_ShouldThrow()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        var act = () => manager.AddProxy(null!, 8080);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddProxy_WithEmptyHost_ShouldThrow()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        var act = () => manager.AddProxy(string.Empty, 8080);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddProxy_WithAuthentication_ShouldSetCredentials()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        manager.AddProxy("proxy.example.com", 8080, "username", "password");

        // Assert
        var proxy = await manager.GetNextProxyAsync();
        proxy.Should().NotBeNull();
        proxy!.Credentials.Should().NotBeNull();

        var credentials = proxy.Credentials as NetworkCredential;
        credentials.Should().NotBeNull();
        credentials!.UserName.Should().Be("username");
        credentials.Password.Should().Be("password");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddProxy_WithoutAuthentication_ShouldNotSetCredentials()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        manager.AddProxy("proxy.example.com", 8080);

        // Assert
        var proxy = await manager.GetNextProxyAsync();
        proxy.Should().NotBeNull();
        proxy!.Credentials.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportFailureAsync_ShouldIncrementFailureCount()
    {
        // Arrange
        var options = new ProxyOptions { MaxFailures = 2 };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy.example.com", 8080);

        var proxy = await manager.GetNextProxyAsync();

        // Act - Report failure once, proxy should still be available
        await manager.ReportFailureAsync(proxy!);
        var proxy2 = await manager.GetNextProxyAsync();

        // Assert
        proxy2.Should().NotBeNull();
        proxy2!.Address!.Host.Should().Be("proxy.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportFailureAsync_WhenExceedingMaxFailures_ShouldExcludeProxy()
    {
        // Arrange
        var options = new ProxyOptions { MaxFailures = 2 };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy1.example.com", 8080);
        manager.AddProxy("proxy2.example.com", 8080);

        var proxy1 = await manager.GetNextProxyAsync();

        // Act - Report failures exceeding threshold
        await manager.ReportFailureAsync(proxy1!);
        await manager.ReportFailureAsync(proxy1!);
        await manager.ReportFailureAsync(proxy1!);

        // Get next proxy - should skip failed proxy
        var nextProxy = await manager.GetNextProxyAsync();

        // Assert
        nextProxy.Should().NotBeNull();
        nextProxy!.Address!.Host.Should().NotBe("proxy1.example.com");
        nextProxy.Address.Host.Should().Be("proxy2.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportFailureAsync_WhenAllProxiesFailed_ShouldReturnNull()
    {
        // Arrange
        var options = new ProxyOptions { MaxFailures = 1 };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy1.example.com", 8080);
        manager.AddProxy("proxy2.example.com", 8080);

        var proxy1 = await manager.GetNextProxyAsync();
        var proxy2 = await manager.GetNextProxyAsync();

        // Act - Fail both proxies
        await manager.ReportFailureAsync(proxy1!);
        await manager.ReportFailureAsync(proxy1!);
        await manager.ReportFailureAsync(proxy2!);
        await manager.ReportFailureAsync(proxy2!);

        var nextProxy = await manager.GetNextProxyAsync();

        // Assert
        nextProxy.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportSuccessAsync_ShouldResetFailureCount()
    {
        // Arrange
        var options = new ProxyOptions { MaxFailures = 2 };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy.example.com", 8080);

        var proxy = await manager.GetNextProxyAsync();

        // Act - Report failure then success
        await manager.ReportFailureAsync(proxy!);
        await manager.ReportFailureAsync(proxy!);
        await manager.ReportSuccessAsync(proxy!);

        // Report more failures
        await manager.ReportFailureAsync(proxy!);
        var nextProxy = await manager.GetNextProxyAsync();

        // Assert - Should still be available since success reset the counter
        nextProxy.Should().NotBeNull();
        nextProxy!.Address!.Host.Should().Be("proxy.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportFailureAsync_WithNullProxy_ShouldThrow()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        var act = async () => await manager.ReportFailureAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportSuccessAsync_WithNullProxy_ShouldThrow()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        var act = async () => await manager.ReportSuccessAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetNextProxyAsync_AfterRetryPeriod_ShouldRecoverFailedProxy()
    {
        // Arrange
        var options = new ProxyOptions 
        { 
            MaxFailures = 1,
            RetryAfter = TimeSpan.FromMilliseconds(100) 
        };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy.example.com", 8080);

        var proxy = await manager.GetNextProxyAsync();

        // Act - Fail the proxy
        await manager.ReportFailureAsync(proxy!);
        await manager.ReportFailureAsync(proxy!);

        // Should return null immediately
        var nullProxy = await manager.GetNextProxyAsync();

        // Wait for retry period
        await Task.Delay(150);

        // Should recover after retry period
        var recoveredProxy = await manager.GetNextProxyAsync();

        // Assert
        nullProxy.Should().BeNull();
        recoveredProxy.Should().NotBeNull();
        recoveredProxy!.Address!.Host.Should().Be("proxy.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetNextProxyAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy1.example.com", 8080);
        manager.AddProxy("proxy2.example.com", 8080);
        manager.AddProxy("proxy3.example.com", 8080);

        // Act - Make concurrent calls
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => manager.GetNextProxyAsync())
            .ToArray();

        var proxies = await Task.WhenAll(tasks);

        // Assert - All calls should succeed
        proxies.Should().AllSatisfy(p => p.Should().NotBeNull());
        proxies.Should().OnlyContain(p => 
            p!.Address!.Host == "proxy1.example.com" ||
            p.Address.Host == "proxy2.example.com" ||
            p.Address.Host == "proxy3.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReportFailureAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var options = new ProxyOptions { MaxFailures = 100 };
        var manager = new ProxyManager(options);
        manager.AddProxy("proxy.example.com", 8080);

        var proxy = await manager.GetNextProxyAsync();

        // Act - Report failures concurrently
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => manager.ReportFailureAsync(proxy!))
            .ToArray();

        await Task.WhenAll(tasks);

        // Should still be excluded after MaxFailures
        var nextProxy = await manager.GetNextProxyAsync();

        // Assert - Proxy should be excluded
        nextProxy.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddProxy_SameHostAndPort_ShouldUpdateExisting()
    {
        // Arrange
        var options = new ProxyOptions();
        var manager = new ProxyManager(options);

        // Act
        manager.AddProxy("proxy.example.com", 8080, "user1", "pass1");
        manager.AddProxy("proxy.example.com", 8080, "user2", "pass2");

        // Assert
        var proxy = await manager.GetNextProxyAsync();
        proxy.Should().NotBeNull();

        var credentials = proxy!.Credentials as NetworkCredential;
        credentials.Should().NotBeNull();
        credentials!.UserName.Should().Be("user2");
        credentials.Password.Should().Be("pass2");
    }
}
