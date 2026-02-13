using System;
using Ghost.Abstractions;
using Ghost.Platform.Common.Session;
using Moq;
using Xunit;

namespace Ghost.Platform.Common.Tests.Session;

public class SessionFactoryTests
{
    private readonly Mock<IProxyProvider> _mockProxyProvider;

    public SessionFactoryTests()
    {
        _mockProxyProvider = new Mock<IProxyProvider>();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithValidParameters()
    {
        var factory = new SessionFactory(_mockProxyProvider.Object);
        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_ShouldThrowWhenProxyProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionFactory(null!));
    }

    [Fact]
    public void CreateSession_ShouldReturnRotatingProxySession()
    {
        var factory = new SessionFactory(_mockProxyProvider.Object);
        var session = factory.CreateSession();
        Assert.NotNull(session);
    }

    [Fact]
    public void CreateSession_WithOptions_ShouldReturnRotatingProxySession()
    {
        var factory = new SessionFactory(_mockProxyProvider.Object);
        var options = new RotatingProxySessionOptions();
        var session = factory.CreateSession(options);
        Assert.NotNull(session);
    }

    [Fact]
    public void CreatePlatformSession_ShouldReturnRotatingProxySession()
    {
        var factory = new SessionFactory(_mockProxyProvider.Object);
        var session = factory.CreatePlatformSession("glassdoor");
        Assert.NotNull(session);
    }
}
