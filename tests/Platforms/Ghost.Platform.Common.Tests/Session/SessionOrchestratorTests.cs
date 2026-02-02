using Ghost.Abstractions;
using Ghost.Platform.Common.Session;
using Ghost.Pool;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Platform.Common.Tests.Session;

public class SessionOrchestratorTests
{
    private readonly Mock<IProxyProvider> _proxyProviderMock;
    private readonly Mock<ITieredBrowserPool> _browserPoolMock;
    private readonly SessionOrchestratorOptions _options;

    public SessionOrchestratorTests()
    {
        _proxyProviderMock = new Mock<IProxyProvider>();
        _browserPoolMock = new Mock<ITieredBrowserPool>();
        _options = new SessionOrchestratorOptions
        {
            MaxConcurrentHttpSessions = 10,
            MaxConcurrentBrowserSessions = 5,
            DefaultSessionTtl = TimeSpan.FromMinutes(5),
            EnableAutoRecycling = false
        };
    }

    [Fact]
    public async Task AllocateSessionAsync_WithHttpType_CreatesHttpSession()
    {
        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var sessionId = await orchestrator.AllocateSessionAsync(context);

        Assert.NotNull(sessionId);
        Assert.StartsWith("session_", sessionId);

        var httpSession = await orchestrator.GetHttpSessionAsync(sessionId);
        Assert.NotNull(httpSession);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task AllocateSessionAsync_WithBrowserType_CreatesBrowserSession()
    {
        var browserSessionMock = new Mock<IBrowserSession>();
        browserSessionMock.Setup(s => s.SessionId).Returns("browser_123");

        _browserPoolMock
            .Setup(p => p.AcquireBrowserAsync(Tier.Hot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(browserSessionMock.Object);

        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "glassdoor",
            CountryCode: "US",
            SessionType: SessionType.Browser);

        var sessionId = await orchestrator.AllocateSessionAsync(context);

        Assert.NotNull(sessionId);

        var browserSession = await orchestrator.GetBrowserSessionAsync(sessionId);
        Assert.NotNull(browserSession);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task AllocateSessionAsync_WithHighComplexity_RoutesToBrowser()
    {
        var browserSessionMock = new Mock<IBrowserSession>();
        browserSessionMock.Setup(s => s.SessionId).Returns("browser_456");

        _browserPoolMock
            .Setup(p => p.AcquireBrowserAsync(Tier.Hot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(browserSessionMock.Object);

        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "google",
            CountryCode: "US",
            SessionType: default,
            ComplexityScore: 85);

        var sessionId = await orchestrator.AllocateSessionAsync(context);

        var browserSession = await orchestrator.GetBrowserSessionAsync(sessionId);
        Assert.NotNull(browserSession);

        _browserPoolMock.Verify(
            p => p.AcquireBrowserAsync(Tier.Hot, It.IsAny<CancellationToken>()),
            Times.Once);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task GetSessionHealthAsync_ReturnsCorrectHealthStatus()
    {
        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var sessionId = await orchestrator.AllocateSessionAsync(context);
        var health = await orchestrator.GetSessionHealthAsync(sessionId);

        Assert.Equal(sessionId, health.SessionId);
        Assert.Equal(SessionHealth.Healthy, health.Health);
        Assert.True(health.Uptime > TimeSpan.Zero);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task AllocateSessionWithAffinityAsync_CreatesAffinityMapping()
    {
        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var affinityOptions = new SessionAffinityOptions(
            AffinityKey: "user_123",
            AffinityDuration: TimeSpan.FromMinutes(10));

        var sessionId1 = await orchestrator.AllocateSessionWithAffinityAsync(context, affinityOptions);
        var sessionId2 = await orchestrator.AllocateSessionWithAffinityAsync(context, affinityOptions);

        Assert.Equal(sessionId1, sessionId2);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task RecycleSessionAsync_RemovesSession()
    {
        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var sessionId = await orchestrator.AllocateSessionAsync(context);
        await orchestrator.RecycleSessionAsync(sessionId);

        var httpSession = await orchestrator.GetHttpSessionAsync(sessionId);
        Assert.Null(httpSession);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ReturnsAllSessions()
    {
        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(_options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var sessionId1 = await orchestrator.AllocateSessionAsync(context);
        var sessionId2 = await orchestrator.AllocateSessionAsync(context);

        var activeSessions = await orchestrator.GetActiveSessionsAsync();

        Assert.Equal(2, activeSessions.Count);
        Assert.Contains(sessionId1, activeSessions);
        Assert.Contains(sessionId2, activeSessions);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task PerformHealthCheckSweepAsync_RecyclesExpiredSessions()
    {
        var options = new SessionOrchestratorOptions
        {
            MaxConcurrentHttpSessions = 10,
            DefaultSessionTtl = TimeSpan.FromMilliseconds(1),
            FailureTrackingWindow = TimeSpan.FromMinutes(5)
        };

        var orchestrator = new SessionOrchestrator(
            _proxyProviderMock.Object,
            _browserPoolMock.Object,
            Options.Create(options),
            NullLogger<SessionOrchestrator>.Instance);

        var context = new SessionAllocationContext(
            PlatformName: "indeed",
            CountryCode: "US",
            SessionType: SessionType.Http);

        var sessionId = await orchestrator.AllocateSessionAsync(context);

        await Task.Delay(100);

        var recycledCount = await orchestrator.PerformHealthCheckSweepAsync();

        Assert.True(recycledCount >= 1);

        await orchestrator.DisposeAsync();
    }
}
